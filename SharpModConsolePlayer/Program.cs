using SharpMod;
using SharpModConsolePlayer.Renderer;

namespace SharpModConsolePlayer {
    internal class Program {
        static async Task Main(string[] args) {
            Cli? cli = Cli.Parse(args);
            if(cli == null) return;

            SoundFile sf = OpenAlStreamPlayer.LoadSoundFile(cli);

            if(cli.ExportPath.Length > 0) {
                WavExporter.Export(sf, cli.ExportPath, cli.SampleRate, cli.BitDepth, cli.Channels, cli.ExportDuration);
                return;
            }

            ConsoleRenderer.InitializeConsole();
            _ = Task.Run(() => ConsoleRenderer.RenderLoop(sf, cli.ShowSampleProgress));
            await OpenAlStreamPlayer.Play(sf, cli.SampleRate, cli.BitDepth, cli.Channels);
            ConsoleRenderer.RestoreConsole();
        }
    }
}