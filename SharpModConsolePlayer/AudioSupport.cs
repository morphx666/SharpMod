using System.Runtime.InteropServices;
using OpenTK.Audio.OpenAL;
using SharpMod;

namespace SharpModConsolePlayer {
    internal static class AudioSupport {
        public static bool isPlaying = false;
        private const int BufferLength = 6000;
        private const int TargetQueueDepth = 3;

        public static SoundFile LoadSoundFile(Cli cli)
            => new(cli.ModFile, (uint)cli.SampleRate, cli.BitDepth == 16, cli.Channels == 2, cli.Loop);

        public static async Task Play(SoundFile sndFile, int sampleRate, int bitDepth, int channels) {
            isPlaying = true;

            InitializeAudioContext(sampleRate);
            ALFormat alf = GetAlFormat(bitDepth, channels);

            byte[] buffer = new byte[BufferLength];
            GCHandle pinnedBufferHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            IntPtr bufferPtr = pinnedBufferHandle.AddrOfPinnedObject();

            int alSrc = AL.GenSource();
            PrimeSource(alSrc, alf, bufferPtr, sampleRate);

            bool bufferIsClear = false;
            uint totalPositions = sndFile.PositionCount;

            while(isPlaying) {
                DrainProcessedBuffers(alSrc);

                AL.GetSource(alSrc, ALGetSourcei.BuffersQueued, out int queued);
                if(queued >= TargetQueueDepth) {
                    await Task.Delay(10);
                    continue;
                }

                FillNextBuffer(sndFile, buffer, ref bufferIsClear);

                int buf = AL.GenBuffer();
                AL.BufferData(buf, alf, bufferPtr, BufferLength, sampleRate);
                AL.SourceQueueBuffer(alSrc, buf);

                EnsurePlaying(alSrc);

                if(sndFile.Position >= totalPositions) isPlaying = false;
            }
        }

        private static void InitializeAudioContext(int sampleRate) {
            ALDevice device = ALC.OpenDevice(null);
            ALContextAttributes attributes = new() { Frequency = sampleRate };
            ALContext context = ALC.CreateContext(device, attributes);
            ALC.MakeContextCurrent(context);
        }

        private static ALFormat GetAlFormat(int bitDepth, int channels)
            => bitDepth == 16
                ? (channels == 2 ? ALFormat.Stereo16 : ALFormat.Mono16)
                : (channels == 2 ? ALFormat.Stereo8 : ALFormat.Mono8);

        private static void PrimeSource(int alSrc, ALFormat alf, IntPtr bufferPtr, int sampleRate) {
            // Prime the source with one silent buffer so playback can start
            // without a conditional inside the main loop. The primer stays in
            // the queue and is drained below once OpenAL marks it processed.
            //  https://github.com/morphx666/SharpMod/blob/4c46ce08023391139b074ce08e1b58c661a42199/SharpModPlayer/FormMain.cs#L182
            int primer = AL.GenBuffer();
            AL.BufferData(primer, alf, bufferPtr, BufferLength, sampleRate);
            AL.SourceQueueBuffer(alSrc, primer);
            AL.SourcePlay(alSrc);
        }

        private static void DrainProcessedBuffers(int alSrc) {
            // Drain buffers OpenAL has finished playing. The source id (not
            // the buffer id) must be passed to SourceUnqueueBuffer; strict
            // implementations such as Apple's OpenAL framework on macOS
            // reject DeleteBuffer on a still-queued buffer, so the queue
            // would otherwise grow unboundedly until the source stalls
            // (~1024 buffers ≈ 35 s of audio).
            AL.GetSource(alSrc, ALGetSourcei.BuffersProcessed, out int processed);
            for(int i = 0; i < processed; i++) {
                int done = AL.SourceUnqueueBuffer(alSrc);
                AL.DeleteBuffer(done);
            }
        }

        private static void FillNextBuffer(SoundFile sndFile, byte[] buffer, ref bool bufferIsClear) {
            uint dataReadLength = sndFile.Read(buffer, BufferLength);
            if(dataReadLength == 0) {
                if(!bufferIsClear) {
                    Array.Clear(buffer, 0, BufferLength);
                    bufferIsClear = true;
                }
            } else if(bufferIsClear) bufferIsClear = false;
        }

        private static void EnsurePlaying(int alSrc) {
            // Resume the source if it underran while the producer was busy.
            AL.GetSource(alSrc, ALGetSourcei.SourceState, out int state);
            if((ALSourceState)state != ALSourceState.Playing) AL.SourcePlay(alSrc);
        }
    }
}