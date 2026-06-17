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
        private const int VuMeterMaxVolume = 256;
        private const float VuDecayPerFrame = 16f;
        private static readonly float[] vuLevels = new float[32];

        public static void Render(SoundFile sf, int channelIndex, uint patternIndex, int consoleCol) {
            int height = Console.WindowHeight;
            int center = FirstPatternRow + (height - FirstPatternRow) / 2;

            RenderHeader(channelIndex + 1, consoleCol);

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
                        ClearRow(consoleCol, consoleRow);
                    } else {
                        string command = sf.CommandToString((uint)pi, (uint)row, channelIndex);
                        RenderRow(command, consoleCol, consoleRow, isActivePattern, isActivePattern && row == currentPatternRow);
                    }
                }
            }
        }

        internal static int ComputeConsoleRow(int center, int currentPatternRow, int row, int patternRelative)
            => center - currentPatternRow + row + patternRelative * RowsPerPattern;

        private static void RenderHeader(int channelNumber, int col) {
            Console.SetCursorPosition(col, HeaderRow);
            string text = $"Channel {channelNumber}";
            int pad = Math.Max(0, ColumnWidth + 2 - text.Length);
            int left = pad / 2;
            int right = pad - left;
            Console.WriteInterpolated($"{Default}{Blue}{new string(' ', left)}{text}{new string(' ', right)}{Default}");
        }

        public static void RenderVuMeter(SoundFile sf, int channelIndex, int col) {
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

            Console.SetCursorPosition(col, VuMeterRow);
            Console.WriteInterpolated($"{Default} {Green}{new string('\u2588', greenCount)}{Yellow}{new string('\u2588', yellowCount)}{Red}{new string('\u2588', redCount)}{new string(' ', emptyCount)} {Default}");
        }

        private static void ClearRow(int col, int row) {
            Console.SetCursorPosition(col, row);
            Console.WriteInterpolated($"{Default}                ");
        }

        private static void RenderRow(string command, int col, int row, bool isActivePattern, bool isActiveRow) {
            ReadOnlySpan<char> s = command.AsSpan();

            Console.SetCursorPosition(col, row);

            var foreColor = isActiveRow ? White : DarkGray;
            var backColor = isActiveRow ? DarkGrayBackground : Default;

            if(s.Length < 14) {
                if(isActiveRow) {
                    Console.WriteInterpolated($"{backColor}{foreColor} ... .. ... ... {Default}");
                } else {
                    Console.WriteInterpolated($"{foreColor} ... .. ... ... {Default}");
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

            if(isActiveRow) {
                Console.WriteInterpolated($"{backColor} {nc}{note} {ic}{inst} {vc}{vol} {ec}{efx} {Default}");
            } else {
                Console.WriteInterpolated($" {nc}{note} {ic}{inst} {vc}{vol} {ec}{efx} {Default}");
            }
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