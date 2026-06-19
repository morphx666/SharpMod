using PrettyConsole;
using System.Text.RegularExpressions;
using static PrettyConsole.Color;

namespace SharpModConsolePlayer.Renderer {
    internal static class Dialog {
        private static readonly List<string> message = [];
        private static readonly List<string> stringMessage = [];

        public static bool IsOpen { get; set; } = false;

        public static void SetMessage(string[] lines) {
            message.Clear();
            message.AddRange(lines);
            stringMessage.Clear();
            stringMessage.AddRange(lines.Select(line => Regex.Replace(line, @"\{.*?\}", "").Replace("AnsiToken", "")));

            IsOpen = true;
        }

        internal static void ShowMessage() {
            int width = Console.WindowWidth;
            int height = Console.WindowHeight;
            int boxWidth = Math.Min(width - 4, stringMessage.Max(line => line.Length) + 4);
            int boxHeight = message.Count + 2;
            int left = (width - boxWidth) / 2;
            int top = (height - boxHeight) / 2;
            int innerWidth = boxWidth - 2;
            string rule = new('─', innerWidth);

            // Draw box
            Console.SetCursorPosition(left, top);
            Console.WriteInterpolated($"{Default}{Cyan}┌{rule}┐{Default}");
            for(int i = 1; i < boxHeight - 1; i++) {
                Console.SetCursorPosition(left, top + i);
                Console.WriteInterpolated($"{Cyan}│{Default}{new WhiteSpace(innerWidth)}{Cyan}│{Default}");
            }
            Console.SetCursorPosition(left, top + boxHeight - 1);
            Console.WriteInterpolated($"{Cyan}└{rule}┘{Default}");

            // Write message
            for(int i = 0; i < message.Count; i++) {
                int msgTop = top + 1 + i;
                Console.SetCursorPosition(left + 1, msgTop);
                //Console.WriteInterpolated(message[i]);
                Console.Write(stringMessage[i]);
            }

            IsOpen = true;
        }

        public static void Close() {
            IsOpen = false;
        }
    }
}