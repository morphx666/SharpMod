using SharpMod;
using static PrettyConsole.Color;

namespace SharpModConsolePlayer.Renderer {
    internal static class ConsoleRenderer {
        private const int ChannelWidth = 20;
        private enum ViewMode { Patterns, Samples }

        internal static void InitializeConsole() {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.CursorVisible = false;
            Console.Clear();
        }

        internal static void RestoreConsole() {
            Console.Clear();
            Console.CursorVisible = true;
        }

        internal static async Task RenderLoop(Func<SoundFile?> getSoundFile, bool showSampleProgress) {
            int fromChannel = 0;
            int fromSample = 0;
            uint lastRow = uint.MaxValue;
            uint lastCurrentPattern = uint.MaxValue;
            int lastWidth = Console.WindowWidth;
            int lastHeight = Console.WindowHeight;
            ViewMode mode = ViewMode.Patterns;
            bool forceRedraw = true;
            SoundFile? lastSoundFile = null;

            while(true) {
                await Task.Delay(30);

                SoundFile? sf = getSoundFile();
                if(sf == null) continue;

                if(!ReferenceEquals(sf, lastSoundFile)) {
                    lastSoundFile = sf;
                    fromChannel = Math.Min(fromChannel, MaxFromChannel(sf));
                    fromSample = 0;
                    lastRow = uint.MaxValue;
                    lastCurrentPattern = uint.MaxValue;
                    Channel.ResetVuMeters();
                    Console.Clear();
                    forceRedraw = true;
                }

                int width = Console.WindowWidth;
                int height = Console.WindowHeight;
                if(width != lastWidth || height != lastHeight) {
                    lastWidth = width;
                    lastHeight = height;
                    fromChannel = Math.Min(fromChannel, MaxFromChannel(sf));
                    Console.Clear();
                    forceRedraw = true;
                }
                if(width <= 0 || height <= 0) continue;

                if(!HandleInput(sf, ref fromChannel, ref fromSample, ref mode, ref forceRedraw)) return;

                try {
                    if(mode == ViewMode.Samples) {
                        Info.Render(sf);
                        if(forceRedraw || showSampleProgress) {
                            Samples.Render(sf, showSampleProgress, fromSample);
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

                    SongProgress.Render(sf);
                } catch(ArgumentOutOfRangeException) {
                    // Window resized mid-frame; next iteration will detect the new size and redraw.
                    forceRedraw = true;
                }

                if(Dialog.IsOpen) Dialog.ShowMessage();
            }
        }

        private static int MaxFromChannel(SoundFile sf) {
            int width = Console.WindowWidth;
            int fitFromRight = Math.Max(0, (width - Channel.VisibleWidth) / ChannelWidth);
            return Math.Max(0, (int)sf.ActiveChannels - 1 - fitFromRight);
        }

        private static bool HandleInput(SoundFile sf, ref int fromChannel, ref int fromSample, ref ViewMode mode, ref bool forceRedraw) {
            while(Console.KeyAvailable) {
                ConsoleKeyInfo info = Console.ReadKey(intercept: true);
                ConsoleKey key = info.Key;
                int previousFromChannel = fromChannel;

                if(key >= ConsoleKey.D1 && key <= ConsoleKey.D9) {
                    int bank = (info.Modifiers & ConsoleModifiers.Control) != 0 ? 2
                             : (info.Modifiers & ConsoleModifiers.Shift) != 0 ? 1
                             : 0;
                    uint channel = (uint)(bank * 12 + (key - ConsoleKey.D1));
                    sf.ToggleMute(channel);
                    forceRedraw = true;
                    continue;
                }

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
                        }
                        break;
                    case ConsoleKey.DownArrow:
                        if(mode == ViewMode.Samples) {
                            fromSample = Math.Min(sf.Instruments.Length - 1, fromSample + 1);
                            if(fromSample + Console.WindowHeight - Samples.FirstSampleRow >= sf.Instruments.Length - 1) {
                                fromSample = Math.Max(0, sf.Instruments.Length - Console.WindowHeight + Samples.FirstSampleRow);
                            }
                        }
                        break;
                    case ConsoleKey.PageUp:
                        sf.Position = Math.Max(sf.Position - 10, 0);
                        break;
                    case ConsoleKey.PageDown:
                        sf.Position = Math.Min(sf.Position + 10, sf.PositionCount - 1);
                        break;
                    case ConsoleKey.Home:
                        if(OpenAlStreamPlayer.playlistIndex > 0) {
                            OpenAlStreamPlayer.request = PlaybackRequest.Previous;
                            OpenAlStreamPlayer.isPlaying = false;
                        }
                        break;
                    case ConsoleKey.End:
                        if(OpenAlStreamPlayer.playlistIndex < OpenAlStreamPlayer.playlistCount - 1) {
                            OpenAlStreamPlayer.request = PlaybackRequest.Next;
                            OpenAlStreamPlayer.isPlaying = false;
                        }
                        break;
                    case ConsoleKey.Escape:
                        if(Dialog.IsOpen) {
                            Dialog.Close();
                            Console.Clear();
                            forceRedraw = true;
                            break;
                        } else {
                            OpenAlStreamPlayer.request = PlaybackRequest.Quit;
                            OpenAlStreamPlayer.isPlaying = false;
                            return false;
                        }
                    case ConsoleKey.Q:
                        OpenAlStreamPlayer.request = PlaybackRequest.Quit;
                        OpenAlStreamPlayer.isPlaying = false;
                        return false;
                    case ConsoleKey.F1:
                        Dialog.SetMessage([
                            $"{Green}F1{Default}                       Show this help",
                            $"{Green}Tab{Default}                      Toggle between patterns and samples view",
                            $"{Green}Left{Default} / {Green}Right{Default}           Scroll channels horizontally",
                            $"{Green}Up{Default} / {Green}Down{Default}              Scroll samples vertically",
                            $"{Green}PageUp{Default} / {Green}PageDown{Default}      Seek track backward/forward",
                            $"{Green}Home{Default} / {Green}End{Default}             Jump to previous/next file in the playlist",
                            $"{Green}1{Default} - {Green}9{Default}                  Toggle mute on channels 1-9",
                            $"{Green}Shift{Default} + {Green}1{Default} - {Green}9{Default}        Toggle mute on channels 10-18",
                            $"{Green}Ctrl{Default} + {Green}1{Default} - {Green}9{Default}         Toggle mute on channels 19-27",
                            $"{Green}Esc {Default}| {Green}Q{Default}                Stop playback and exit"
                        ]);
                        Dialog.ShowMessage();
                        break;
                }
            }
            return true;
        }

        private static void RenderHeaderAndVuMeters(SoundFile sf, int fromChannel) {
            Info.Render(sf);

            int width = Console.WindowWidth;
            for(int i = 0; fromChannel + i < sf.ActiveChannels; i++) {
                int x = i * ChannelWidth;
                if(x >= width) break;
                int cellWidth = Math.Min(Channel.VisibleWidth, width - x);
                Channel.RenderVuMeter(sf, fromChannel + i, x, cellWidth);
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
                int cellWidth = Math.Min(Channel.VisibleWidth, width - x);
                Channel.Render(sf, fromChannel + i, patternIndex, x, cellWidth);
            }
        }
    }
}