using SharpMod;
using PrettyConsole;
using static PrettyConsole.Color;

// https://github.com/dusrdev/PrettyConsole

namespace SharpModConsolePlayer.Renderer {
    internal static class Channel {
        internal const int RowsPerPattern = 64;
        private const int HeaderRow = Info.InfoRow + 1;
        private const int VuMeterRow = HeaderRow + 1;
        private const int FirstPatternRow = VuMeterRow + 1;
        private const int ColumnWidth = 14;
        private const int HalfWidth = ColumnWidth / 2;
        internal const int VisibleWidth = ColumnWidth + 2;
        private const int VuMeterMaxVolume = 256;
        private const float VuDecayPerFrame = 16f;
        private static readonly float[] vuLevelsL = new float[32];
        private static readonly float[] vuLevelsR = new float[32];

        // Heavy horizontal box-drawing char: '\u2501'  ━
        private const char vuChar = '\u2501';

        internal static void ResetVuMeters() {
            Array.Clear(vuLevelsL, 0, vuLevelsL.Length);
            Array.Clear(vuLevelsR, 0, vuLevelsR.Length);
        }

        internal static void Render(SoundFile sf, int channelIndex, uint patternIndex, int consoleCol, int maxWidth) {
            if(maxWidth <= 0) return;
            int height = Console.WindowHeight - 1; // reserve last row for the song progress bar
            int playHead = FirstPatternRow + (height - FirstPatternRow) / 2;

            RenderHeader(channelIndex + 1, consoleCol, maxWidth, sf.Channels[channelIndex].Muted);

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
                    int consoleRow = ComputeConsoleRow(playHead, currentPatternRow, row, patternRelative);
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

        internal static int ComputeConsoleRow(int playHead, int currentPatternRow, int row, int patternRelative)
            => playHead - currentPatternRow + row + patternRelative * RowsPerPattern;

        private static void RenderHeader(int channelNumber, int col, int maxWidth, bool muted) {
            if(maxWidth <= 0) return;
            Console.SetCursorPosition(col, HeaderRow);
            string text = muted ? $"Channel {channelNumber} [M]" : $"Channel {channelNumber}";
            int totalLen = Math.Min(VisibleWidth, maxWidth);
            int textLen = Math.Min(text.Length, totalLen);
            int pad = totalLen - textLen;
            int left = pad / 2;
            int right = pad - left;
            ReadOnlySpan<char> textSpan = text.AsSpan(0, textLen);
            AnsiToken color = muted ? Red : Blue;
            Console.WriteInterpolated($"{Default}{color}{new WhiteSpace(left)}{textSpan}{new WhiteSpace(right)}{Default}");
        }

        internal static void RenderVuMeter(SoundFile sf, int channelIndex, int col, int maxWidth) {
            if(maxWidth <= 0) return;
            var ch = sf.Channels[channelIndex];
            bool isActive = ch.Length > 0 && ch.Pos < ch.Length;

            float targetL, targetR;
            if(isActive) {
                float vol = ch.CurrentVolume;
                if(ch.IsStereo) {
                    // Stereo sample: the mixer ignores Pan and routes L/R straight through.
                    targetL = vol;
                    targetR = vol;
                } else {
                    // Mono sample: the mixer pans across L/R using Pan (0=left, 256=right).
                    int pan = ch.Pan;
                    if(pan < 0) pan = 0; else if(pan > VuMeterMaxVolume) pan = VuMeterMaxVolume;
                    targetL = vol * (VuMeterMaxVolume - pan) / VuMeterMaxVolume;
                    targetR = vol * pan / VuMeterMaxVolume;
                }
            } else {
                targetL = 0f;
                targetR = 0f;
            }
            if(targetL < 0f) targetL = 0f; else if(targetL > VuMeterMaxVolume) targetL = VuMeterMaxVolume;
            if(targetR < 0f) targetR = 0f; else if(targetR > VuMeterMaxVolume) targetR = VuMeterMaxVolume;

            float levelL = Math.Max(targetL, vuLevelsL[channelIndex] - VuDecayPerFrame);
            float levelR = Math.Max(targetR, vuLevelsR[channelIndex] - VuDecayPerFrame);
            vuLevelsL[channelIndex] = levelL;
            vuLevelsR[channelIndex] = levelR;

            int filledL = (int)(levelL * HalfWidth / VuMeterMaxVolume);
            int filledR = (int)(levelR * HalfWidth / VuMeterMaxVolume);
            if(filledL > HalfWidth) filledL = HalfWidth;
            if(filledR > HalfWidth) filledR = HalfWidth;

            // Centered bar: left half (cells 0..6) grows from the center outward to the left,
            // right half (cells 7..13) grows from the center outward to the right.
            Span<char> cells = stackalloc char[ColumnWidth];
            cells.Fill(' ');
            for(int i = 0; i < filledL; i++) cells[HalfWidth - 1 - i] = vuChar;
            for(int i = 0; i < filledR; i++) cells[HalfWidth + i] = vuChar;

            // Color regions by position, intensity grows outward from the center:
            //   pos  0  | 1..2  | 3..6  ||  7..10 | 11..12 | 13
            //   red | yellow | green || green  | yellow | red
            int remaining = maxWidth;
            int leadSpace = Math.Min(1, remaining); remaining -= leadSpace;
            int rL = Math.Min(1, remaining); remaining -= rL;
            int yL = Math.Min(2, remaining); remaining -= yL;
            int gL = Math.Min(4, remaining); remaining -= gL;
            int gR = Math.Min(4, remaining); remaining -= gR;
            int yR = Math.Min(2, remaining); remaining -= yR;
            int rR = Math.Min(1, remaining); remaining -= rR;
            int trailSpace = Math.Min(1, remaining);

            ReadOnlySpan<char> sRedL = cells.Slice(0, rL);
            ReadOnlySpan<char> sYelL = cells.Slice(1, yL);
            ReadOnlySpan<char> sGrnL = cells.Slice(3, gL);
            ReadOnlySpan<char> sGrnR = cells.Slice(7, gR);
            ReadOnlySpan<char> sYelR = cells.Slice(11, yR);
            ReadOnlySpan<char> sRedR = cells.Slice(13, rR);

            Console.SetCursorPosition(col, VuMeterRow);
            Console.WriteInterpolated($"{Default}{new WhiteSpace(leadSpace)}{Red}{sRedL}{Yellow}{sYelL}{Green}{sGrnL}{sGrnR}{Yellow}{sYelR}{Red}{sRedR}{new WhiteSpace(trailSpace)}{Default}");
        }

        private static void ClearRow(int col, int row, int maxWidth) {
            if(maxWidth <= 0) return;
            Console.SetCursorPosition(col, row);
            Console.WriteInterpolated($"{Default}{new WhiteSpace(Math.Min(VisibleWidth, maxWidth))}");
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