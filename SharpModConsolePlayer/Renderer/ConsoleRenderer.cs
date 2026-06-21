using SharpMod;
using static PrettyConsole.Color;

namespace SharpModConsolePlayer.Renderer {
    internal static class ConsoleRenderer {
        private const int ChannelWidth = 20;
        private const uint PreviousPatternRowThreshold = 1;
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
            var NextTrack = () => {
                if(OpenAlStreamPlayer.PlaylistIndex < OpenAlStreamPlayer.PlaylistCount - 1) {
                    OpenAlStreamPlayer.request = PlaybackRequest.Next;
                    OpenAlStreamPlayer.IsPlaying = false;
                }
            };
            var PreviousTrack = () => {
                if(OpenAlStreamPlayer.PlaylistIndex > 0) {
                    OpenAlStreamPlayer.request = PlaybackRequest.Previous;
                    OpenAlStreamPlayer.IsPlaying = false;
                }
            };

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
                    case ConsoleKey.Spacebar:
                        OpenAlStreamPlayer.IsPaused = !OpenAlStreamPlayer.IsPaused;
                        break;
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
                    case ConsoleKey.PageUp: {
                        // If we are well into the current pattern (row >= threshold), snap to its row 0.
                        // Otherwise step back one order entry. The threshold is comfortably above the
                        // number of rows the engine can advance between two keypresses, so a second
                        // PageUp after the first reliably falls into the "step back" branch.
                        uint orderIndex = sf.Position / 64;
                        uint row = sf.Position % 64;
                        if(row < PreviousPatternRowThreshold) sf.Position = (orderIndex - 1) * 64;
                        else sf.Position = orderIndex * 64;
                        break;
                    }
                    case ConsoleKey.PageDown: {
                        // OpenMPT "Next Pattern": step forward one order entry to row 0.
                        uint orderIndex = sf.Position / 64;
                        uint maxOrder = sf.PositionCount / 64;
                        if(maxOrder > 0 && orderIndex + 1 < maxOrder) sf.Position = (orderIndex + 1) * 64;
                        break;
                    }
                    case ConsoleKey.Home:
                        NextTrack();
                        break;
                    case ConsoleKey.End:
                        PreviousTrack();
                        break;
                    case ConsoleKey.Escape:
                        if(Dialog.IsOpen) {
                            Dialog.Close();
                            Console.Clear();
                            forceRedraw = true;
                            break;
                        } else {
                            OpenAlStreamPlayer.request = PlaybackRequest.Quit;
                            OpenAlStreamPlayer.IsPlaying = false;
                            return false;
                        }
                    case ConsoleKey.Q:
                        OpenAlStreamPlayer.request = PlaybackRequest.Quit;
                        OpenAlStreamPlayer.IsPlaying = false;
                        return false;
                    case ConsoleKey.F1:
                        if(Dialog.IsOpen) {
                            Dialog.Close();
                            Console.Clear();
                            forceRedraw = true;
                            break;
                        }
                        Dialog.SetMessage(" Shortcuts ", 74, 12, () => Cli.PrintKeyBindings($"│ ", $" │"));
                        Dialog.ShowMessage();
                        break;
                    default:
                        if(info.Modifiers.HasFlag(ConsoleModifiers.Control)) {
                            switch(info.KeyChar) {
                                case '\u0001':
                                    PreviousTrack();
                                    break;
                                case '\u0005':
                                    NextTrack();
                                    break;
                            }
                        }
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