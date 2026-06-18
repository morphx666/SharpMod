using SharpMod;

namespace SharpModConsolePlayer.Renderer {
    internal static class ConsoleRenderer {
        private const int ChannelWidth = 20;
        private enum ViewMode { Patterns, Samples }

        public static void InitializeConsole() {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.CursorVisible = false;
            Console.Clear();
        }

        public static void RestoreConsole() {
            Console.Clear();
            Console.CursorVisible = true;
        }

        public static async Task RenderLoop(SoundFile sf, bool showSampleProgress) {
            int fromChannel = 0;
            uint lastRow = uint.MaxValue;
            uint lastCurrentPattern = uint.MaxValue;
            ViewMode mode = ViewMode.Patterns;
            bool forceRedraw = true;

            while(true) {
                await Task.Delay(30);

                if(!HandleInput(sf, ref fromChannel, ref mode, ref forceRedraw)) return;

                if(mode == ViewMode.Samples) {
                    Renderer.Info.Render(sf);
                    if(forceRedraw || showSampleProgress) {
                        Renderer.Samples.Render(sf, showSampleProgress);
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

        private static bool HandleInput(SoundFile sf, ref int fromChannel, ref ViewMode mode, ref bool forceRedraw) {
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
                    case ConsoleKey.Escape:
                        AudioSupport.isPlaying = false;
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
    }
}