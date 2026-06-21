using PrettyConsole;
using System.Text.RegularExpressions;
using static PrettyConsole.Color;

namespace SharpModConsolePlayer.Renderer {
    internal static class Dialog {
        public static bool IsOpen { get; set; } = false;
        private static string title = "";
        private static Action messageBuilder = null!;
        private static int width = 0;
        private static int height = 0;
        private const char glyphHorizontal = '─';
        private const char glyphVertical = '│';
        private const char glyphTopLeft = '┌';
        private const char glyphTopRight = '┐';
        private const char glyphBottomLeft = '└';
        private const char glyphBottomRight = '┘';

        public static void SetMessage(string title, int width, int height, Action messageBuilder) {
            Dialog.title = title;
            Dialog.width = width;
            Dialog.height = height;
            Dialog.messageBuilder = messageBuilder;
            IsOpen = true;
        }

        internal static void ShowMessage() {
            int width = Console.WindowWidth;
            int height = Console.WindowHeight;
            int left = (width - Dialog.width) / 2;
            int top = (height - Dialog.height) / 2;
            int innerWidth = Dialog.width - 2;
            string rule = new(glyphHorizontal, innerWidth);

            Console.SetCursorPosition(left, top);

            if(!string.IsNullOrEmpty(title)) {
                int titleLength = title.Length;
                int padding = (innerWidth - titleLength) / 2;
                string paddedTitle = new string(glyphHorizontal, padding) + title + new string(glyphHorizontal, innerWidth - titleLength - padding);
                Console.WriteInterpolated($"{Default}{Cyan}{glyphTopLeft}{paddedTitle}{glyphTopRight}{Default}");
            } else {
                Console.WriteInterpolated($"{Default}{Cyan}{glyphTopLeft}{rule}{glyphTopRight}{Default}");
            }
            Console.SetCursorPosition(left, ++top);

            Dialog.messageBuilder();
            Console.WriteInterpolated($"{new WhiteSpace(left)}{Cyan}{glyphBottomLeft}{rule}{glyphBottomRight}{Default}");

            IsOpen = true;
        }

        public static void Close() {
            IsOpen = false;
        }
    }
}