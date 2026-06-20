using System.Runtime.InteropServices;
using OpenTK.Audio.OpenAL;
using SharpMod;

namespace SharpModConsolePlayer {
    internal enum PlaybackRequest { None, Previous, Next, Quit }

    internal static class OpenAlStreamPlayer {
        internal static bool IsPlaying = false;
        internal static bool IsPaused = false;
        internal static PlaybackRequest request = PlaybackRequest.None;
        internal static int PlaylistIndex = 0;
        internal static int PlaylistCount = 0;
        private const int BufferLength = 6000;
        private const int TargetQueueDepth = 3;

        // The device/context/source and the pinned mix buffer are created on the first track and
        // reused for the rest of the playlist. Re-creating them per track (as we used to) leaked
        // ALDevice/ALContext/ALSource handles and left stale sources playing on a stale context,
        // which broke audio on the second track in directory-mode playback.
        private static bool audioInitialized;
        private static int alSrc;
        private static byte[] sharedBuffer = [];
        private static GCHandle pinnedBufferHandle;
        private static IntPtr bufferPtr;

        internal static SoundFile LoadSoundFile(Cli cli) {
            IsPaused = false;
            IsPlaying = false;

            return new(cli.ModFile, (uint)cli.SampleRate, cli.BitDepth == 16, cli.Channels == 2, cli.Loop);
        }

        internal static async Task Play(SoundFile sndFile, int sampleRate, int bitDepth, int channels) {
            IsPlaying = true;

            ALFormat alf = GetAlFormat(bitDepth, channels);

            if(!audioInitialized) {
                InitializeAudioContext(sampleRate);
                sharedBuffer = new byte[BufferLength];
                pinnedBufferHandle = GCHandle.Alloc(sharedBuffer, GCHandleType.Pinned);
                bufferPtr = pinnedBufferHandle.AddrOfPinnedObject();
                alSrc = AL.GenSource();
                audioInitialized = true;
            } else {
                ResetSource(alSrc);
                Array.Clear(sharedBuffer, 0, BufferLength);
            }
            PrimeSource(alSrc, alf, bufferPtr, sampleRate);

            byte[] buffer = sharedBuffer;
            bool bufferIsClear = false;
            uint totalPositions = sndFile.PositionCount;

            while(IsPlaying) {
                if(IsPaused) {
                    await Task.Delay(100);
                    continue;
                }
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

                if(sndFile.Position >= totalPositions) IsPlaying = false;
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

        private static void ResetSource(int src) {
            // Halt the source and drain its full queue so the next track does not
            // hear the tail of the previous one and so buffer ids do not leak.
            AL.SourceStop(src);
            AL.GetSource(src, ALGetSourcei.BuffersQueued, out int queued);
            for(int i = 0; i < queued; i++) {
                int done = AL.SourceUnqueueBuffer(src);
                AL.DeleteBuffer(done);
            }
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