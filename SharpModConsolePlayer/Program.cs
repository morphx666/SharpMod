using OpenTK.Audio.OpenAL;
using SharpMod;

namespace SharpModConsolePlayer {
    internal class Program {
        private static bool isPlaying = false;

        static async Task Main(string[] args) {
            string modFileFullPath = @"/Users/xavier/Downloads/HOUSE/Flip House.mod";
            int sampleRate = 44100;
            int bitDepth = 16;
            int channels = 2;
            SoundFile sf = new(modFileFullPath, (uint)sampleRate, bitDepth == 16, channels == 2, false);

            Console.Clear();
            _ = Task.Run(async () => {
                while(true) {
                    await Task.Delay(60);
                    //RenderUI(sf);
                    Renderer.Channel.Render(sf, 0);
                }
            });

            //sf.Position = (uint)(sf.PositionCount - 150);
            await Play(sf, sampleRate, bitDepth, channels);
        }

        private static void RenderUI(SoundFile sf) {
            Console.SetCursorPosition(0, 0);
            Console.WriteLine($"Title: {sf.Title}");
            Console.WriteLine($"Type: {sf.Type}");
            Console.WriteLine($"Rate: {sf.Rate}");
            Console.WriteLine($"Active Channels: {sf.ActiveChannels}");
            Console.WriteLine($"Active Samples: {sf.ActiveSamples}");
            Console.WriteLine($"Music Speed: {sf.MusicSpeed}");
            Console.WriteLine($"Music Tempo: {sf.MusicTempo}");
            Console.WriteLine($"Speed Count: {sf.SpeedCount}");
            Console.WriteLine($"Buffer Count: {sf.BufferCount}");
            Console.WriteLine($"Pattern: {sf.Pattern}");
            Console.WriteLine($"Current Pattern: {sf.CurrentPattern}");
            Console.WriteLine($"Next Pattern: {sf.NextPattern}");
            Console.WriteLine($"Row: {sf.Row}");
        }

        private static async Task Play(SharpMod.SoundFile sndFile, int sampleRate, int bitDepth, int channels) {
            isPlaying = true;

            ALDevice device = ALC.OpenDevice(null);
            ALContextAttributes attributes = new() {
                Frequency = sampleRate
            };
            ALContext context = ALC.CreateContext(device, attributes);
            ALC.MakeContextCurrent(context);

            int bufLen = 6000;
            int bufLen2 = bufLen / 2;
            byte[] buffer = new byte[bufLen];
            var pinnedBufferHandle = System.Runtime.InteropServices.GCHandle.Alloc(buffer, System.Runtime.InteropServices.GCHandleType.Pinned);

            ALFormat alf = bitDepth == 16 ?
                            (channels == 2 ? ALFormat.Stereo16 : ALFormat.Mono16) :
                            (channels == 2 ? ALFormat.Stereo8 : ALFormat.Mono8);

            int alSrc = AL.GenSource();

            // All this crap is just to prevent having a conditional (if)
            // inside the while loop, which is only executed once:
            //  if(AL.GetSourceState(alSrc) != ALSourceState.Playing) AL.SourcePlay(alSrc);
            //  https://github.com/morphx666/SharpMod/blob/4c46ce08023391139b074ce08e1b58c661a42199/SharpModPlayer/FormMain.cs#L182
            int buf = AL.GenBuffer();
            AL.BufferData(buf, alf, pinnedBufferHandle.AddrOfPinnedObject(), bufLen, sampleRate);
            AL.SourceQueueBuffer(alSrc, buf);
            AL.SourcePlay(alSrc);
            AL.SourceUnqueueBuffer(buf);
            AL.DeleteBuffer(buf);

            int dataReadLength;
            int bufferPosition;
            int frame = 0;
            bool bufferIsClear = false;
            uint totalPositions = sndFile.PositionCount;

            while(isPlaying) {
                dataReadLength = (int)sndFile.Read(buffer, (uint)bufLen);
                if(dataReadLength == 0) {
                    if(!bufferIsClear) {
                        Array.Clear(buffer, 0, bufLen);
                        bufferIsClear = true;
                    }
                } else if(bufferIsClear) bufferIsClear = false;

                buf = AL.GenBuffer();
                AL.BufferData(buf, alf, pinnedBufferHandle.AddrOfPinnedObject(), bufLen, sampleRate);
                AL.SourceQueueBuffer(alSrc, buf);

                do {
                    await Task.Delay(10);
                    AL.GetSource(alSrc, ALGetSourcei.ByteOffset, out bufferPosition);

                    if(sndFile.Position >= totalPositions) {
                        isPlaying = false;
                        break;
                    }

                } while(bufferPosition + bufLen2 <= bufLen * frame);
                frame++;

                AL.SourceUnqueueBuffer(buf);
                AL.DeleteBuffer(buf);
            }
        }
    }
}