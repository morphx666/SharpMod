using SharpMod;
using SharpModConsolePlayer.Renderer;

namespace SharpModConsolePlayer {
    internal static class Program {
        static readonly string[] supportedExtensions = new[] { ".mod", ".stm", ".s3m", ".xm" };

        static async Task Main(string[] args) {
            Cli? cli = Cli.Parse(args);
            if(cli == null) return;

            if(File.Exists(cli.ModFile)) {
                await PlayFile(cli);
            } else if(cli.ModFile.Contains('?') || cli.ModFile.Contains('*')) {
                string directory = Path.GetDirectoryName(cli.ModFile) ?? ".";
                string pattern = Path.GetFileName(cli.ModFile);
                if(!Directory.Exists(directory)) {
                    Console.WriteLine($"Directory not found: {directory}");
                    return;
                }

                string[] files = Directory.GetFiles(directory, pattern, SearchOption.TopDirectoryOnly);
                if(files.Length == 0) {
                    Console.WriteLine($"No files found matching pattern: {cli.ModFile}");
                    return;
                }

                foreach(string file in files) {
                    cli.ModFile = file;
                    await PlayFile(cli);
                }
            } else if(Directory.Exists(cli.ModFile)) {
                string[] files = [.. Directory.GetFiles(cli.ModFile, "*.*", SearchOption.AllDirectories).Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))];
                if(files.Length == 0) {
                    Console.WriteLine($"No files found in directory: {cli.ModFile}");
                    return;
                }

                foreach(string file in files) {
                    cli.ModFile = file;
                    await PlayFile(cli);
                }
            } else if(Directory.Exists(cli.ModFile)) {
                string[] files = [.. Directory.GetFiles(cli.ModFile, "*.*", SearchOption.AllDirectories).Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))];
                if(files.Length == 0) {
                    Console.WriteLine($"No files found in directory: {cli.ModFile}");
                    return;
                }

                foreach(string file in files) {
                    cli.ModFile = file;
                    await PlayFile(cli);
                }
            } else {
                Console.WriteLine($"File or directory not found: {cli.ModFile}");
            }
        }

        static async Task PlayFile(Cli cli) {
            SoundFile sf = OpenAlStreamPlayer.LoadSoundFile(cli);

            if(cli.ExportPath.Length > 0) {
                WavExporter.Export(sf, cli.ExportPath, cli.SampleRate, cli.BitDepth, cli.Channels);
                return;
            }

            ConsoleRenderer.InitializeConsole();
            _ = Task.Run(() => ConsoleRenderer.RenderLoop(sf, cli.ShowSampleProgress));
            await OpenAlStreamPlayer.Play(sf, cli.SampleRate, cli.BitDepth, cli.Channels);
            ConsoleRenderer.RestoreConsole();
        }
    }
}