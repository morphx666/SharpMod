using OpenTK.Audio;
using OpenTK.Audio.OpenAL;

namespace SharpModConsolePlayer {
    internal class Program {
        static void Main(string[] args) {
            string modFileFullPath = @"/Users/xavier/Downloads/HOUSE/House Journey.mod";
            int sampleRate = 44100;
            int bitDepth = 16;
            int channels = 2;
            SharpMod.SoundFile sf = new(modFileFullPath, (uint)sampleRate, bitDepth == 16, channels == 2, false);

            StartAudio(sf, sampleRate, bitDepth, channels);

            while(true) {
                Thread.Sleep(100);
            }
        }

        private static void StartAudio(SharpMod.SoundFile? sndFile, int sampleRate, int bitDepth, int channels) {
            ALDevice device = ALC.OpenDevice(null);
            ALContextAttributes attributes = new() {
                Frequency = sampleRate
            };
            ALContext context = ALC.CreateContext(device, attributes);
            ALC.MakeContextCurrent(context);

            int bufLen = 6000;
            int bufLen2 = bufLen / 2;
            byte[] buffer = new byte[bufLen];

            // pinned buffer
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(buffer, System.Runtime.InteropServices.GCHandleType.Pinned);

            ALFormat alf = bitDepth == 16 ?
                            (channels == 2 ? ALFormat.Stereo16 : ALFormat.Mono16) :
                            (channels == 2 ? ALFormat.Stereo8 : ALFormat.Mono8);

            int alSrc = AL.GenSource();

            // All this crap is just to prevent having a conditional (if)
            // inside the while loop, which is only executed once:
            //  if(AL.GetSourceState(alSrc) != ALSourceState.Playing) AL.SourcePlay(alSrc);
            //  https://github.com/morphx666/SharpMod/blob/4c46ce08023391139b074ce08e1b58c661a42199/SharpModPlayer/FormMain.cs#L182
            int buf = AL.GenBuffer();
            AL.BufferData(buf, alf, handle.AddrOfPinnedObject(), bufLen, sampleRate);
            AL.SourceQueueBuffer(alSrc, buf);
            AL.SourcePlay(alSrc);
            AL.SourceUnqueueBuffer(buf);
            AL.DeleteBuffer(buf);

            Task.Run(() => {
                int n;
                int frame = 0;
                int bpos;
                bool bufferIsClear = false;

                while(true) {
                    if(sndFile != null) {
                        n = (int)sndFile.Read(buffer, (uint)bufLen);

                        if(n == 0) {
                            if(!bufferIsClear) {
                                Array.Clear(buffer, 0, bufLen);
                                bufferIsClear = true;
                            }
                        } else if(bufferIsClear) bufferIsClear = false;
                    }

                    buf = AL.GenBuffer();

                    AL.BufferData(buf, alf, handle.AddrOfPinnedObject(), bufLen, sampleRate);
                    AL.SourceQueueBuffer(alSrc, buf);

                    do {
                        Thread.Sleep(5);
                        AL.GetSource(alSrc, ALGetSourcei.ByteOffset, out bpos);
                    } while(bpos + bufLen2 < bufLen * frame);
                    frame++;

                    AL.SourceUnqueueBuffer(buf);
                    AL.DeleteBuffer(buf);
                }
            });
        }
    }
}