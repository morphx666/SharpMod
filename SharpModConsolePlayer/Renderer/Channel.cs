using SharpMod;
using PrettyConsole;
using static PrettyConsole.Color;

// https://github.com/dusrdev/PrettyConsole

namespace SharpModConsolePlayer.Renderer {
    internal class Channel {
        internal const int RowsPerPattern = 64;
        internal const int HeaderRow = Info.InfoRow + 1;
        internal const int VuMeterRow = HeaderRow + 1;
        internal const int FirstPatternRow = VuMeterRow + 1;
        private const int ColumnWidth = 14;
        internal const int VisibleWidth = ColumnWidth + 2;
        private const int VuMeterMaxVolume = 256;
        private const float VuDecayPerFrame = 16f;
        private static readonly float[] vuLevels = new float[32];

        public static void Render(SoundFile sf, int channelIndex, uint patternIndex, int consoleCol, int maxWidth) {
            if(maxWidth <= 0) return;
            int height = Console.WindowHeight - 1; // reserve last row for the song progress bar
            int center = FirstPatternRow + (height - FirstPatternRow) / 2;

            RenderHeader(channelIndex + 1, consoleCol, maxWidth);

            // Snapshot mutable state so the audio thread can't shift it mid-render
            int currentPatternRow = (int)sf.Row;
            uint currentPattern = sf.CurrentPattern;
            uint nextPattern = sf.NextPattern;

            int previousPatternIndex = currentPattern > 0 ? sf.Order[currentPattern - 1] : -1;
            int nextPatternIndex = nextPattern != 0xFF ? sf.Order[nextPattern] : -1;
            int[] patternIndices = [previousPatternIndex, (int)patternIndex, nextPatternIndex];

            for(int i = 0; i < patternIndices.Length; i++) {
                int pi = patternIndices[i];
                int patternRelative = i - 1; // -1, 0, +1
                bool isActivePattern = patternRelative == 0;

                for(int row = 0; row < RowsPerPattern; row++) {
                    int consoleRow = ComputeConsoleRow(center, currentPatternRow, row, patternRelative);
                    if(consoleRow < FirstPatternRow || consoleRow >= height) continue;

                    if(pi == -1) {
                        ClearRow(consoleCol, consoleRow, maxWidth);
                    } else {
                        string command = sf.CommandToString((uint)pi, (uint)row, channelIndex);
                        RenderRow(command, consoleCol, consoleRow, maxWidth, isActivePattern, isActivePattern && row == currentPatternRow);
                    }
                }
            }
        }

        internal static int ComputeConsoleRow(int center, int currentPatternRow, int row, int patternRelative)
            => center - currentPatternRow + row + patternRelative * RowsPerPattern;

        private static void RenderHeader(int channelNumber, int col, int maxWidth) {
            if(maxWidth <= 0) return;
            Console.SetCursorPosition(col, HeaderRow);
            string text = $"Channel {channelNumber}";
            int pad = Math.Max(0, VisibleWidth - text.Length);
            int left = pad / 2;
            int right = pad - left;
            string content = new string(' ', left) + text + new string(' ', right);
            if(content.Length > maxWidth) content = content[..maxWidth];
            Console.WriteInterpolated($"{Default}{Blue}{content}{Default}");
        }

        public static void RenderVuMeter(SoundFile sf, int channelIndex, int col, int maxWidth) {
            if(maxWidth <= 0) return;
            var ch = sf.Channels[channelIndex];
            bool isActive = ch.Length > 0 && ch.Pos < ch.Length;
            float target = isActive ? ch.CurrentVolume : 0f;
            if(target < 0f) target = 0f;
            if(target > VuMeterMaxVolume) target = VuMeterMaxVolume;

            float level = Math.Max(target, vuLevels[channelIndex] - VuDecayPerFrame);
            vuLevels[channelIndex] = level;

            int filled = (int)(level * ColumnWidth / VuMeterMaxVolume);

            int greenCount = Math.Min(filled, 8);
            int yellowCount = Math.Max(0, Math.Min(filled, 11) - 8);
            int redCount = Math.Max(0, filled - 11);
            int emptyCount = ColumnWidth - filled;

            int remaining = maxWidth;
            int leadSpace = Math.Min(1, remaining); remaining -= leadSpace;
            int gEmit = Math.Min(greenCount, remaining); remaining -= gEmit;
            int yEmit = Math.Min(yellowCount, remaining); remaining -= yEmit;
            int rEmit = Math.Min(redCount, remaining); remaining -= rEmit;
            int eEmit = Math.Min(emptyCount, remaining); remaining -= eEmit;
            int trailSpace = Math.Min(1, remaining);

            Console.SetCursorPosition(col, VuMeterRow);
            Console.WriteInterpolated($"{Default}{new string(' ', leadSpace)}{Green}{new string('\u2588', gEmit)}{Yellow}{new string('\u2588', yEmit)}{Red}{new string('\u2588', rEmit)}{new string(' ', eEmit + trailSpace)}{Default}");
        }

        private static void ClearRow(int col, int row, int maxWidth) {
            if(maxWidth <= 0) return;
            Console.SetCursorPosition(col, row);
            Console.WriteInterpolated($"{Default}{new string(' ', Math.Min(VisibleWidth, maxWidth))}");
        }

        private static void RenderRow(string command, int col, int row, int maxWidth, bool isActivePattern, bool isActiveRow) {
            if(maxWidth <= 0) return;
            ReadOnlySpan<char> s = command.AsSpan();

            Console.SetCursorPosition(col, row);

            var foreColor = isActiveRow ? White : DarkGray;
            var backColor = isActiveRow ? DarkGrayBackground : Default;

            if(s.Length < 14) {
                string placeholder = " ... .. ... ... ";
                if(placeholder.Length > maxWidth) placeholder = placeholder[..maxWidth];
                if(isActiveRow) {
                    Console.WriteInterpolated($"{backColor}{foreColor}{placeholder}{Default}");
                } else {
                    Console.WriteInterpolated($"{foreColor}{placeholder}{Default}");
                }
                return;
            }

            ReadOnlySpan<char> note = s[..3];
            ReadOnlySpan<char> inst = s.Slice(4, 2);
            ReadOnlySpan<char> vol = s.Slice(7, 3);
            ReadOnlySpan<char> efx = s.Slice(11, 3);

            AnsiToken nc = !isActivePattern || IsPlaceholder(note) ? DarkGray : White;
            AnsiToken ic = !isActivePattern || IsPlaceholder(inst) ? DarkGray : Green;
            AnsiToken vc = !isActivePattern || IsPlaceholder(vol) ? DarkGray : Cyan;
            AnsiToken ec = !isActivePattern || IsPlaceholder(efx) ? DarkGray : Yellow;

            int remaining = maxWidth;
            string seg0 = ClipSegment(" ", ref remaining);
            string seg1 = ClipSegment(string.Concat(note, " "), ref remaining);
            string seg2 = ClipSegment(string.Concat(inst, " "), ref remaining);
            string seg3 = ClipSegment(string.Concat(vol, " "), ref remaining);
            string seg4 = ClipSegment(string.Concat(efx, " "), ref remaining);

            if(isActiveRow) {
                Console.WriteInterpolated($"{backColor}{foreColor}{seg0}{nc}{seg1}{ic}{seg2}{vc}{seg3}{ec}{seg4}{Default}");
            } else {
                Console.WriteInterpolated($"{seg0}{nc}{seg1}{ic}{seg2}{vc}{seg3}{ec}{seg4}{Default}");
            }
        }

        private static string ClipSegment(string s, ref int remaining) {
            if(remaining <= 0) return string.Empty;
            if(remaining >= s.Length) { remaining -= s.Length; return s; }
            string clipped = s[..remaining];
            remaining = 0;
            return clipped;
        }

        private static bool IsPlaceholder(ReadOnlySpan<char> s) {
            for(int i = 0; i < s.Length; i++) {
                char c = s[i];
                if(c != '.' && c != ' ') return false;
            }
            return true;
        }
    }
}