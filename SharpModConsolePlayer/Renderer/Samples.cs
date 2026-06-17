using SharpMod;
using PrettyConsole;
using static PrettyConsole.Color;

namespace SharpModConsolePlayer.Renderer {
    internal class Samples {
        internal const int HeaderRow = Info.InfoRow + 1;
        internal const int FirstSampleRow = HeaderRow + 1;
        private const int NameWidth = 28;

        public static void Render(SoundFile sf, bool showProgress) {
            int width = Console.WindowWidth;
            int height = Console.WindowHeight - 1; // reserve last row for the song progress bar
            if(width <= 0) return;

            RenderHeader(width);

            int total = sf.Instruments != null ? sf.Instruments.Length - 1 : 0;
            int maxRows = Math.Max(0, height - FirstSampleRow);
            int rows = Math.Min(total, maxRows);

            for(int i = 0; i < rows; i++) {
                RenderSample(sf, i + 1, FirstSampleRow + i, width, showProgress);
            }
            for(int i = rows; i < maxRows; i++) {
                ClearRow(FirstSampleRow + i, width);
            }
        }

        private static void RenderHeader(int width) {
            Console.SetCursorPosition(0, HeaderRow);
            string text = $"  #  {"Name".PadRight(NameWidth)}    Length    Vol  LoopStart  LoopEnd";
            if(text.Length > width) text = text[..width];
            int pad = Math.Max(0, width - text.Length);
            Console.WriteInterpolated($"{Default}{Yellow}{text}{new string(' ', pad)}{Default}");
        }

        private static void RenderSample(SoundFile sf, int index, int row, int width, bool showProgress) {
            var ins = sf.Instruments[index];
            string name = ins.Name ?? string.Empty;
            if(name.Length > NameWidth) name = name[..NameWidth];
            else name = name.PadRight(NameWidth);

            bool empty = ins.Length == 0;
            AnsiToken nameColor = empty ? DarkGray : White;
            AnsiToken numColor = empty ? DarkGray : Cyan;

            int filled = (showProgress && !empty) ? ComputeProgressChars(sf, index) : 0;
            string nameFilled = name[..filled];
            string nameRest = name[filled..];

            string line = $" {index,2}  {name}    {ins.Length,6}   {ins.Volume,3}  {ins.LoopStart,9}  {ins.LoopEnd,7}";
            int pad = Math.Max(0, width - line.Length);

            Console.SetCursorPosition(0, row);
            Console.WriteInterpolated($"{Default}{DarkGray} {index,2}  {nameColor}{DarkBlueBackground}{nameFilled}{DefaultBackground}{nameRest}    {numColor}{ins.Length,6}   {Green}{ins.Volume,3}  {Magenta}{ins.LoopStart,9}  {ins.LoopEnd,7}{Default}{new string(' ', pad)}");
        }

        private static int ComputeProgressChars(SoundFile sf, int instrumentIndex) {
            float maxProgress = 0f;
            var channels = sf.Channels;
            for(int c = 0; c < channels.Length; c++) {
                var ch = channels[c];
                if(ch.Length == 0 || ch.InstrumentIndex != (uint)instrumentIndex) continue;
                if(ch.Pos >= ch.Length) continue;

                float p;
                if(ch.LoopEnd > ch.LoopStart) {
                    // Looped sample: once the loop is active, playback cycles in
                    // [LoopStart, LoopEnd], so map the bar to that region. Pos may
                    // briefly sit below LoopStart on the very first pass; clamp it.
                    p = ch.Pos <= ch.LoopStart
                        ? 0f
                        : (float)(ch.Pos - ch.LoopStart) / (ch.LoopEnd - ch.LoopStart);
                } else {
                    p = (float)ch.Pos / ch.Length;
                }
                if(p > maxProgress) maxProgress = p;
            }
            int filled = (int)(maxProgress * NameWidth);
            if(filled < 0) filled = 0;
            if(filled > NameWidth) filled = NameWidth;
            return filled;
        }

        private static void ClearRow(int row, int width) {
            Console.SetCursorPosition(0, row);
            Console.WriteInterpolated($"{Default}{new string(' ', width)}");
        }
    }
}
