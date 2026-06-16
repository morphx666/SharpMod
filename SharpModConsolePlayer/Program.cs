using OpenTK.Audio.OpenAL;
using SharpMod;

namespace SharpModConsolePlayer {
    internal class Program {
        private static bool isPlaying = false;

        static async Task Main(string[] args) {
            //string modFile = @"Z:\Music\Music (C)\MODS\HOUSE\Calling Loulou.mod";
            string modFile = @"Z:\Music\Music (C)\MODS\Pet Shop Boys\Domino Dancing.mod";
            //string modeFile = @"/Users/xavier/Downloads/HOUSE/ACIDOFIL.MOD";
            int sampleRate = 44100;
            int bitDepth = 16;
            int channels = 2;
            SoundFile sf = new(modFile, (uint)sampleRate, bitDepth == 16, channels == 2, false);

            Console.CursorVisible = false;
            Console.Clear();
            _ = Task.Run(async () => {
                const int channelWidth = 20;
                int fromChannel = 0;
                uint lastRow = uint.MaxValue;
                uint lastCurrentPattern = uint.MaxValue;
                bool forceRedraw = true;

                while(true) {
                    await Task.Delay(30);

                    while(Console.KeyAvailable) {
                        ConsoleKey key = Console.ReadKey(intercept: true).Key;
                        int previousFromChannel = fromChannel;
                        if(key == ConsoleKey.LeftArrow) {
                            fromChannel = Math.Max(0, fromChannel - 1);
                        } else if(key == ConsoleKey.RightArrow) {
                            fromChannel = Math.Min((int)sf.ActiveChannels - 1, fromChannel + 1);
                        }
                        if(fromChannel != previousFromChannel) {
                            Console.Clear();
                            forceRedraw = true;
                        }
                    }

                    uint currentRow = sf.Row;
                    uint currentPattern = sf.CurrentPattern;
                    if(!forceRedraw && currentRow == lastRow && currentPattern == lastCurrentPattern) continue;
                    lastRow = currentRow;
                    lastCurrentPattern = currentPattern;
                    forceRedraw = false;

                    uint patternIndex = sf.Pattern;
                    if(patternIndex == 0xFF) {
                        patternIndex = sf.Order.Last((o) => o != 0xFF);
                    }
                    for(int i = 0; fromChannel + i < sf.ActiveChannels; i++) {
                        int x = i * channelWidth;
                        if(x >= Console.WindowWidth) break;
                        Renderer.Channel.Render(sf, fromChannel + i, patternIndex, x);
                    }
                }
            });

            //sf.Position = (uint)(sf.PositionCount - 150);
            await Play(sf, sampleRate, bitDepth, channels);

            Console.CursorVisible = true;
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