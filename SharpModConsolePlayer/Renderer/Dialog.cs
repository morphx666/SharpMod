using PrettyConsole;
using System.Text.RegularExpressions;
using static PrettyConsole.Color;

namespace SharpModConsolePlayer.Renderer {
    internal static class Dialog {
        private static readonly List<string> lines = [];
        private static readonly List<string> stringLines = [];

        public static bool IsOpen { get; set; } = false;
        private static string title = "";

        public static void SetMessage(string title, string[] lines) {
            Dialog.title = title;
            Dialog.lines.Clear();
            Dialog.lines.AddRange(lines);
            stringLines.Clear();
            stringLines.AddRange(lines.Select(line => Regex.Replace(line, @"\{.*?\}", "").Replace("AnsiToken", "")));

            IsOpen = true;
        }

        internal static void ShowMessage() {
            int width = Console.WindowWidth;
            int height = Console.WindowHeight;
            int boxWidth = Math.Min(width - 4, Math.Max(stringLines.Max(line => line.Length) + 4, title.Length + 4));
            int boxHeight = lines.Count + 2;
            int left = (width - boxWidth) / 2;
            int top = (height - boxHeight) / 2;
            int innerWidth = boxWidth - 2;
            string rule = new('─', innerWidth);

            // Draw box
            Console.SetCursorPosition(left, top);
            // Use this formula: $"{Default}{Cyan}┌{rule}┐{Default}"
            // But interpolate the title:
            if(!string.IsNullOrEmpty(title)) {
                int titleLength = title.Length;
                int padding = (innerWidth - titleLength) / 2;
                string paddedTitle = new string('─', padding) + title + new string('─', innerWidth - titleLength - padding);
                Console.WriteInterpolated($"{Default}{Cyan}┌{paddedTitle}┐{Default}");
            } else {
                Console.WriteInterpolated($"{Default}{Cyan}┌{rule}┐{Default}");
            }
            for(int i = 1; i < boxHeight - 1; i++) {
                Console.SetCursorPosition(left, top + i);
                Console.WriteInterpolated($"{Cyan}│{Default}{new WhiteSpace(innerWidth)}{Cyan}│{Default}");
            }
            Console.SetCursorPosition(left, top + boxHeight - 1);
            Console.WriteInterpolated($"{Cyan}└{rule}┘{Default}");

            // Write message
            for(int i = 0; i < lines.Count; i++) {
                int msgTop = top + 1 + i;
                Console.SetCursorPosition(left + 1, msgTop);
                //var msg = new PrettyConsoleInterpolatedStringHandler();
                //msg.AppendLiteral(message[i]);
                //Console.WriteInterpolated(ref msg);
                Console.Write(stringLines[i]);
            }

            IsOpen = true;
        }

        public static void Close() {
            IsOpen = false;
        }
    }
}