using SharpMod;
using SharpModConsolePlayer.Renderer;

namespace SharpModConsolePlayer {
    internal class Program {
        static async Task Main(string[] args) {
            Cli? cli = Cli.Parse(args);
            if(cli == null) return;

            SoundFile sf = AudioSupport.LoadSoundFile(cli);

            ConsoleRenderer.InitializeConsole();
            _ = Task.Run(() => ConsoleRenderer.RenderLoop(sf, cli.ShowSampleProgress));
            await AudioSupport.Play(sf, cli.SampleRate, cli.BitDepth, cli.Channels);
            ConsoleRenderer.RestoreConsole();
        }
    }
}