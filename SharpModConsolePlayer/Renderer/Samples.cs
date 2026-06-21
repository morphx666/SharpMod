using SharpMod;
using PrettyConsole;
using static PrettyConsole.Color;

namespace SharpModConsolePlayer.Renderer {
    internal static class Samples {
        private const int HeaderRow = Info.InfoRow + 1;
        internal const int FirstSampleRow = HeaderRow + 3;
        private const int NameWidth = 28;

        internal static void Render(SoundFile sf, bool showProgress, int fromSample = 0) {
            int width = Console.WindowWidth;
            int height = Console.WindowHeight - 1; // reserve last row for the song progress bar
            if(width <= 0) return;

            RenderHeader(sf, width);

            int total = sf.Instruments != null ? sf.Instruments.Length - 1 : 0;
            int maxRows = Math.Max(0, height - FirstSampleRow);
            int rows = Math.Min(total - fromSample, maxRows);

            for(int i = 0; i < rows; i++) {
                RenderSample(sf, i + 1 + fromSample, FirstSampleRow + i, width, showProgress);
            }
            for(int i = rows; i < maxRows; i++) {
                ClearRow(FirstSampleRow + i, width);
            }
        }

        private static void RenderHeader(SoundFile sf, int width) {
            Console.SetCursorPosition(0, HeaderRow);
            uint seconds = sf.Length;
            string lengthValue = seconds >= 3600
                ? $"{seconds / 3600}:{seconds / 60 % 60:D2}:{seconds % 60:D2}"
                : $"{seconds / 60}:{seconds % 60:D2}";
            string bpmValue = $"{sf.AverageTempo}";

            int prefixWidth = 2 + Info.TitleWidth + 1; // " │" + title spaces + "|"
            int remaining = Math.Max(0, width - prefixWidth);
            string s0 = Channel.ClipSegment(" Length: ", ref remaining);
            string s1 = Channel.ClipSegment("~", ref remaining);
            string s2 = Channel.ClipSegment(lengthValue, ref remaining);
            string s3 = Channel.ClipSegment("  BPM: ", ref remaining);
            string s4 = Channel.ClipSegment("~", ref remaining);
            string s5 = Channel.ClipSegment(bpmValue, ref remaining);

            Console.WriteLineInterpolated($"{Default}{Magenta} │{DarkMagenta}{new WhiteSpace(Info.TitleWidth)}{DarkGray}|{Cyan}{s0}{DarkGray}{s1}{White}{s2}{Cyan}{s3}{DarkGray}{s4}{White}{s5}{Default}");
            Console.WriteInterpolated($"{Default}{Magenta} └{DarkMagenta}{sf.FileName}{Default}");

            Console.SetCursorPosition(0, HeaderRow + 2);
            string text = $"  #  {"Name",-NameWidth}    {"Length",6}   {"Vol",3}   {"Fmt",4}  {"LoopStart",9}  {"LoopEnd",7}";
            if(text.Length > width) text = text[..width];
            int pad = Math.Max(0, width - text.Length);
            Console.WriteInterpolated($"{Default}{Yellow}{text}{new WhiteSpace(pad)}{Default}");
        }

        private static void RenderSample(SoundFile sf, int index, int row, int width, bool showProgress) {
            var ins = sf.Instruments[index];
            string name = SanitizeName(ins.Name ?? string.Empty);
            if(name.Length > NameWidth) name = name[..NameWidth];
            else name = name.PadRight(NameWidth);

            bool empty = ins.Length == 0;
            AnsiToken nameColor = empty ? DarkGray : White;
            AnsiToken numColor = empty ? DarkGray : Cyan;
            AnsiToken fmtColor = empty ? DarkGray : Blue;

            string fmt = empty ? "    " : $"{(ins.Is16Bit ? "16" : " 8")}/{(ins.IsStereo ? "S" : "M")}";

            int filled = (showProgress && !empty) ? ComputeProgressChars(sf, index) : 0;
            string nameFilled = name[..filled];
            string nameRest = name[filled..];

            string line = $" {index,2}  {name}    {ins.Length,6}   {ins.Volume,3}   {fmt,4}  {ins.LoopStart,9}  {ins.LoopEnd,7}";
            int pad = Math.Max(0, width - line.Length);

            Console.SetCursorPosition(0, row);
            Console.WriteInterpolated($"{Default}{DarkGray} {index,2}  {nameColor}{DarkBlueBackground}{nameFilled}{DefaultBackground}{nameRest}    {numColor}{ins.Length,6}   {Green}{ins.Volume,3}   {fmtColor}{fmt,4}  {Magenta}{ins.LoopStart,9}  {ins.LoopEnd,7}{Default}{new WhiteSpace(pad)}");
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
            Console.WriteInterpolated($"{Default}{new WhiteSpace(width)}");
        }

        // Sample names are decoded from raw CP437 bytes and may contain embedded
        // C0 control characters (NUL, BS, ESC, …) that render as zero columns or
        // get swallowed by the terminal's ANSI parser, throwing every following
        // row out of alignment. Replace them with spaces so visible width matches
        // string length. Lazy allocation: clean names pass through unchanged.
        private static string SanitizeName(string s) {
            char[]? buf = null;
            for(int i = 0; i < s.Length; i++) {
                char c = s[i];
                if(c < 0x20 || c == 0x7F) {
                    buf ??= s.ToCharArray();
                    buf[i] = ' ';
                }
            }
            return buf == null ? s : new string(buf);
        }
    }
}
