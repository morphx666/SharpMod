using SharpMod;
using PrettyConsole;
using static PrettyConsole.Color;

namespace SharpModConsolePlayer.Renderer {
    internal static class Info {
        internal const int InfoRow = 0;
        internal const int TitleWidth = 24;

        internal static void Render(SoundFile sf) {
            int width = Console.WindowWidth;
            if(width <= 0) return;

            string title = sf.Title ?? string.Empty;
            if(title.Length > TitleWidth) title = title[..TitleWidth];
            else title = title.PadRight(TitleWidth);

            int remaining = width;
            string s0 = Channel.ClipSegment(" ", ref remaining);
            string s1 = Channel.ClipSegment(string.Concat(title, " "), ref remaining);
            string s2 = Channel.ClipSegment("| ", ref remaining);
            string s3 = Channel.ClipSegment("Type:", ref remaining);
            string typeStr = sf.Type.ToString();
            if(typeStr.Length > 3) typeStr = typeStr[..3];
            string s4 = Channel.ClipSegment($" {typeStr,-3}  ", ref remaining);
            string s5 = Channel.ClipSegment("Channels:", ref remaining);
            string s6 = Channel.ClipSegment($" {sf.ActiveChannels,2}  ", ref remaining);
            string s7 = Channel.ClipSegment("Pattern:", ref remaining);
            string s8 = Channel.ClipSegment($" {sf.CurrentPattern,3}  ", ref remaining);
            string s9 = Channel.ClipSegment("Row:", ref remaining);
            string s10 = Channel.ClipSegment($" {sf.Row,2}  ", ref remaining);
            string s11 = Channel.ClipSegment("Tempo/Speed:", ref remaining);
            string s12 = Channel.ClipSegment($" {sf.MusicTempo,3}/{sf.MusicSpeed,3}", ref remaining);

            Console.SetCursorPosition(0, InfoRow);
            Console.WriteInterpolated($"{Default}{s0}{Magenta}{s1}{DarkGray}{s2}{Cyan}{s3}{White}{s4}{Cyan}{s5}{White}{s6}{Cyan}{s7}{White}{s8}{Cyan}{s9}{White}{s10}{Cyan}{s11}{White}{s12}{Default}");
        }
    }
}
