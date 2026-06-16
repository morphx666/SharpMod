using SharpMod;
using PrettyConsole;
using static PrettyConsole.Color;

namespace SharpModConsolePlayer.Renderer {
    internal class Info {
        internal const int InfoRow = 0;
        private const int TitleWidth = 24;

        public static void Render(SoundFile sf) {
            string title = sf.Title ?? string.Empty;
            if(title.Length > TitleWidth) title = title[..TitleWidth];
            else title = title.PadRight(TitleWidth);

            Console.SetCursorPosition(0, InfoRow);
            Console.WriteInterpolated($"{Default} {Magenta}{title}  {Cyan}Channels:{White} {sf.ActiveChannels,2}  {Cyan}Pattern:{White} {sf.CurrentPattern,3}  {Cyan}Row:{White} {sf.Row,2}  {Cyan}BPM:{White} {sf.MusicTempo,3}{Default}");
        }
    }
}
