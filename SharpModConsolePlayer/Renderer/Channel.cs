using SharpMod;
using PrettyConsole;
using static PrettyConsole.Color;

// https://github.com/dusrdev/PrettyConsole

namespace SharpModConsolePlayer.Renderer {
    internal class Channel {
        public static void Render(SoundFile sf, int channelIndex, int x) {
            uint pattern = sf.Pattern;
            uint currentRow = sf.Row;
            int height = Console.WindowHeight;
            int center = height / 2;

            for(uint row = 0; row < 64; row++) {
                string command = sf.CommandToString(pattern, row, channelIndex);
                ReadOnlySpan<char> s = command.AsSpan();
                bool isCurrent = row == currentRow;

                int y = (int)(row + center - currentRow);
                if(y < 0 || y >= height) continue;
                Console.SetCursorPosition(x, y);
                if(s.Length < 14) {
                    if(isCurrent) {
                        Console.WriteInterpolated($"{DarkGrayBackground}{DarkGray} ... .. ... ... {Default}");
                    } else {
                        Console.WriteInterpolated($"{DarkGray} ... .. ... ... {Default}");
                    }
                    continue;
                }

                ReadOnlySpan<char> note = s[..3];
                ReadOnlySpan<char> inst = s.Slice(4, 2);
                ReadOnlySpan<char> vol  = s.Slice(7, 3);
                ReadOnlySpan<char> efx  = s.Slice(11, 3);

                AnsiToken nc = IsPlaceholder(note) ? DarkGray : White;
                AnsiToken ic = IsPlaceholder(inst) ? DarkGray : Green;
                AnsiToken vc = IsPlaceholder(vol)  ? DarkGray : Cyan;
                AnsiToken ec = IsPlaceholder(efx)  ? DarkGray : Yellow;

                if(isCurrent) {
                    Console.WriteInterpolated($"{DarkGrayBackground} {nc}{note} {ic}{inst} {vc}{vol} {ec}{efx} {Default}");
                } else {
                    Console.WriteInterpolated($" {nc}{note} {ic}{inst} {vc}{vol} {ec}{efx} {Default}");
                }
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