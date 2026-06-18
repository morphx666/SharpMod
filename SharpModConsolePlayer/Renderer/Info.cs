using SharpMod;
using PrettyConsole;
using static PrettyConsole.Color;

namespace SharpModConsolePlayer.Renderer {
    internal static class Info {
        internal const int InfoRow = 0;
        private const int TitleWidth = 24;

        internal static void Render(SoundFile sf) {
            int width = Console.WindowWidth;
            if(width <= 0) return;

            string title = sf.Title ?? string.Empty;
            if(title.Length > TitleWidth) title = title[..TitleWidth];
            else title = title.PadRight(TitleWidth);

            int remaining = width;
            string s0 = ClipSegment(" ", ref remaining);
            string s1 = ClipSegment(string.Concat(title, " "), ref remaining);
            string s2 = ClipSegment("| ", ref remaining);
            string s3 = ClipSegment("Type:", ref remaining);
            string typeStr = sf.Type.ToString();
            if(typeStr.Length > 3) typeStr = typeStr[..3];
            string s4 = ClipSegment($" {typeStr,-3}  ", ref remaining);
            string s5 = ClipSegment("Channels:", ref remaining);
            string s6 = ClipSegment($" {sf.ActiveChannels,2}  ", ref remaining);
            string s7 = ClipSegment("Pattern:", ref remaining);
            string s8 = ClipSegment($" {sf.CurrentPattern,3}  ", ref remaining);
            string s9 = ClipSegment("Row:", ref remaining);
            string s10 = ClipSegment($" {sf.Row,2}  ", ref remaining);
            string s11 = ClipSegment("Tempo/Speed:", ref remaining);
            string s12 = ClipSegment($" {sf.MusicTempo,3}/{sf.MusicSpeed,3}", ref remaining);

            Console.SetCursorPosition(0, InfoRow);
            Console.WriteInterpolated($"{Default}{s0}{Magenta}{s1}{DarkGray}{s2}{Cyan}{s3}{White}{s4}{Cyan}{s5}{White}{s6}{Cyan}{s7}{White}{s8}{Cyan}{s9}{White}{s10}{Cyan}{s11}{White}{s12}{Default}");
        }

        private static string ClipSegment(string s, ref int remaining) {
            if(remaining <= 0) return string.Empty;
            if(remaining >= s.Length) { remaining -= s.Length; return s; }
            string clipped = s[..remaining];
            remaining = 0;
            return clipped;
        }
    }
}
