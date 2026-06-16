using SharpMod;
using PrettyConsole;
using static PrettyConsole.Color;
using System.Diagnostics;

// https://github.com/dusrdev/PrettyConsole

namespace SharpModConsolePlayer.Renderer {
    internal class Channel {
        public static void Render(SoundFile sf, int channelIndex, uint patternIndex, int x) {
            int height = Console.WindowHeight;
            int center = height / 2;
            int currentPatternRow = (int)sf.Row;

            int previousPatternIndex = sf.CurrentPattern > 0 ? sf.Order[sf.CurrentPattern - 1] : -1;
            int nextPatternIndex =  sf.NextPattern != 0xFF ? sf.Order[sf.NextPattern] : -1;
            int[] patternIndices = [previousPatternIndex, (int)patternIndex, nextPatternIndex];

            int offset = -1;
            for(int i = 0; i < patternIndices.Length; i++) {
                int pi = patternIndices[i];
                if(pi == -1) continue;

                for(int row = 0; row < 64; row++) {
                    int consoleRow = center + (row - currentPatternRow) + 64 * offset;
                    if(consoleRow < 0 || consoleRow >= height) continue;

                    string command = sf.CommandToString((uint)pi, (uint)row, channelIndex);
                    RenderPattern(command, x, consoleRow, center, pi == patternIndex);
                }

                offset++;
            }
        }

        private static void RenderPattern(string command, int x, int consoleRow, int center, bool isActive) {
            ReadOnlySpan<char> s = command.AsSpan();
            bool isCurrent = consoleRow == center;

            Console.SetCursorPosition(x, consoleRow);

            var foreColor = isActive ? White : DarkGray;
            var backColor = isCurrent ? DarkGrayBackground : BlackBackground;

            if(s.Length < 14) {
                if(isCurrent) {
                    Console.WriteInterpolated($"{backColor}{foreColor} ... .. ... ... {Default}");
                } else {
                    Console.WriteInterpolated($"{foreColor} ... .. ... ... {Default}");
                }
            }

            ReadOnlySpan<char> note = s[..3];
            ReadOnlySpan<char> inst = s.Slice(4, 2);
            ReadOnlySpan<char> vol = s.Slice(7, 3);
            ReadOnlySpan<char> efx = s.Slice(11, 3);

            AnsiToken nc = IsPlaceholder(note) ? DarkGray : White;
            AnsiToken ic = IsPlaceholder(inst) ? DarkGray : Green;
            AnsiToken vc = IsPlaceholder(vol) ? DarkGray : Cyan;
            AnsiToken ec = IsPlaceholder(efx) ? DarkGray : Yellow;

            if(isCurrent) {
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