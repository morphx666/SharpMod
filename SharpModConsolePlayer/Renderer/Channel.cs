using SharpMod;
using PrettyConsole;                // Extension members + OutputPipe
using static System.Console;        // Optional for terser call sites
using static PrettyConsole.Color;   // Optional for terser color tokens

// https://github.com/dusrdev/PrettyConsole

namespace SharpModConsolePlayer.Renderer {
    internal class Channel {
        public static void Render(SoundFile sf, int channelIndex) {
            Console.SetCursorPosition(0, 0);

            var channel = sf.Channels[channelIndex];
            Console.WriteInterpolated($"{Markup.Bold}Channel{Markup.ResetBold} {channelIndex}");
        }
    }
}