using SharpMod;
using PrettyConsole;
using static PrettyConsole.Color;

namespace SharpModConsolePlayer.Renderer {
    internal static class SongProgress {
        // static ProgressBar progress = new() {
        //     ProgressChar = '■',
        //     ForegroundColor = Color.DarkGray,
        //     ProgressColor = Color.Cyan,
        // };

        internal static void Render(SoundFile sf) {
            int width = Console.WindowWidth;
            int row = Console.WindowHeight - 1;
            if(width <= 0 || row < 0) return;

            uint pos = sf.Position;
            uint total = sf.PositionCount;
            if(total == 0) total = 1;
            if(pos > total) pos = total;

            int filled = (int)((long)pos * width / total);
            if(filled < 0) filled = 0;
            if(filled > width) filled = width;
            int empty = width - filled;

            Console.SetCursorPosition(0, row);
            Console.WriteInterpolated($"{Default}{Cyan}{new string('\u2588', filled)}{DarkGray}{new string('\u2588', empty)}{Default}");

            // Not using it b/c lack of customization options as well as considerable flickering
            // progress.Update((int)(pos * 100.0 / total), $"Playing {pos}/{total} rows");
        }
    }
}
