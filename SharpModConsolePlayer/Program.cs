using System.Runtime.InteropServices;
using OpenTK.Audio.OpenAL;
using SharpMod;

namespace SharpModConsolePlayer {
    internal class Program {
        private const int ChannelWidth = 20;
        private const int BufferLength = 6000;
        private const int TargetQueueDepth = 3;

        private enum ViewMode { Patterns, Samples }

        private static bool isPlaying = false;

        static async Task Main(string[] args) {
            Cli? cli = Cli.Parse(args);
            if(cli == null) return;

            SoundFile sf = LoadSoundFile(cli);

            InitializeConsole();
            _ = Task.Run(() => RenderLoop(sf, cli.ShowSampleProgress));
            await Play(sf, cli.SampleRate, cli.BitDepth, cli.Channels);
            RestoreConsole();
        }

        private static SoundFile LoadSoundFile(Cli cli)
            => new(cli.ModFile, (uint)cli.SampleRate, cli.BitDepth == 16, cli.Channels == 2, cli.Loop);

        private static void InitializeConsole() {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.CursorVisible = false;
            Console.Clear();
        }

        private static void RestoreConsole() {
            Console.Clear();
            Console.CursorVisible = true;
        }

        private static async Task RenderLoop(SoundFile sf, bool showSampleProgress) {
            int fromChannel = 0;
            int fromSample = 0;
            uint lastRow = uint.MaxValue;
            uint lastCurrentPattern = uint.MaxValue;
            ViewMode mode = ViewMode.Patterns;
            bool forceRedraw = true;

            while(true) {
                await Task.Delay(30);

                if(!HandleInput(sf, ref fromChannel, ref fromSample, ref mode, ref forceRedraw)) return;

                if(mode == ViewMode.Samples) {
                    Renderer.Info.Render(sf);
                    if(forceRedraw || showSampleProgress) {
                        Renderer.Samples.Render(sf, showSampleProgress, fromSample);
                        forceRedraw = false;
                    }
                } else {
                    RenderHeaderAndVuMeters(sf, fromChannel);

                    uint currentRow = sf.Row;
                    uint currentPattern = sf.CurrentPattern;
                    if(forceRedraw || currentRow != lastRow || currentPattern != lastCurrentPattern) {
                        lastRow = currentRow;
                        lastCurrentPattern = currentPattern;
                        forceRedraw = false;
                        RenderPatterns(sf, fromChannel);
                    }
                }

                Renderer.SongProgress.Render(sf);
            }
        }

        private static int MaxFromChannel(SoundFile sf) {
            int width = Console.WindowWidth;
            int fitFromRight = Math.Max(0, (width - Renderer.Channel.VisibleWidth) / ChannelWidth);
            return Math.Max(0, (int)sf.ActiveChannels - 1 - fitFromRight);
        }

        private static bool HandleInput(SoundFile sf, ref int fromChannel, ref int fromSample, ref ViewMode mode, ref bool forceRedraw) {
            while(Console.KeyAvailable) {
                ConsoleKey key = Console.ReadKey(intercept: true).Key;
                int previousFromChannel = fromChannel;
                switch(key) {
                    case ConsoleKey.LeftArrow:
                        fromChannel = Math.Max(0, fromChannel - 1);
                        break;
                    case ConsoleKey.RightArrow:
                        fromChannel = Math.Min(MaxFromChannel(sf), fromChannel + 1);
                        break;
                    case ConsoleKey.Tab:
                        mode = mode == ViewMode.Patterns ? ViewMode.Samples : ViewMode.Patterns;
                        Console.Clear();
                        forceRedraw = true;
                        break;
                    case ConsoleKey.UpArrow:
                        if(mode == ViewMode.Samples) {
                            fromSample = Math.Max(0, fromSample - 1);
                            forceRedraw = true;
                        }
                        break;
                    case ConsoleKey.DownArrow:
                        if(mode == ViewMode.Samples) {
                            fromSample = Math.Min(sf.Instruments.Length - 1, fromSample + 1);
                            if(fromSample + Console.WindowHeight - Renderer.Samples.FirstSampleRow >= sf.Instruments.Length - 1) {
                                fromSample = Math.Max(0, sf.Instruments.Length - Console.WindowHeight + Renderer.Samples.FirstSampleRow);
                            }
                            forceRedraw = true;
                        }
                        break;
                    case ConsoleKey.Escape:
                        isPlaying = false;
                        return false;
                }

                if(fromChannel != previousFromChannel) {
                    Console.Clear();
                    forceRedraw = true;
                }
            }
            return true;
        }

        private static void RenderHeaderAndVuMeters(SoundFile sf, int fromChannel) {
            Renderer.Info.Render(sf);

            int width = Console.WindowWidth;
            for(int i = 0; fromChannel + i < sf.ActiveChannels; i++) {
                int x = i * ChannelWidth;
                if(x >= width) break;
                int cellWidth = Math.Min(Renderer.Channel.VisibleWidth, width - x);
                Renderer.Channel.RenderVuMeter(sf, fromChannel + i, x, cellWidth);
            }
        }

        private static void RenderPatterns(SoundFile sf, int fromChannel) {
            uint patternIndex = sf.Pattern;
            if(patternIndex == 0xFF) {
                patternIndex = sf.Order.Last((o) => o != 0xFF);
            }
            int width = Console.WindowWidth;
            for(int i = 0; fromChannel + i < sf.ActiveChannels; i++) {
                int x = i * ChannelWidth;
                if(x >= width) break;
                int cellWidth = Math.Min(Renderer.Channel.VisibleWidth, width - x);
                Renderer.Channel.Render(sf, fromChannel + i, patternIndex, x, cellWidth);
            }
        }

        private static async Task Play(SoundFile sndFile, int sampleRate, int bitDepth, int channels) {
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