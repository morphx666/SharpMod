using SharpMod;
using PrettyConsole;                // Extension members + OutputPipe
using static System.Console;        // Optional for terser call sites
using static PrettyConsole.Color;   // Optional for terser color tokens

// https://github.com/dusrdev/PrettyConsole

namespace SharpModConsolePlayer.Renderer {
    internal class Channel {
        const int CHANNEL_WIDTH = 14;

        public static void Render(SoundFile sf, int channelIndex) {
            Console.SetCursorPosition(0, 0);

            for(int row = 0; row < 64; row++) {
                string command = sf.CommandToString(sf.Pattern, row, channelIndex);
            }
        }
    }
}