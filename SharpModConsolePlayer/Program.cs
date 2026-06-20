using SharpMod;
using SharpModConsolePlayer.Renderer;

namespace SharpModConsolePlayer {
    internal static class Program {
        static readonly string[] supportedExtensions = [".mod", ".stm", ".s3m", ".xm"];
        static SoundFile? currentSoundFile;

        static async Task Main(string[] args) {
            Cli? cli = Cli.Parse(args);
            if(cli == null) return;
            List<string> filesToPlay = [];

            foreach(string input in cli.ModFiles) {
                if(File.Exists(input)) {
                    filesToPlay.Add(input);
                } else if(input.Contains('?') || input.Contains('*')) {
                    string directory = Path.GetDirectoryName(input) ?? ".";
                    string pattern = Path.GetFileName(input);
                    if(!Directory.Exists(directory)) {
                        Console.WriteLine($"Directory not found: {directory}");
                        continue;
                    }

                    string[] files = [.. Directory.GetFiles(directory, pattern, SearchOption.AllDirectories).Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))];
                    if(cli.Randomize) {
                        Random rng = new();
                        files = [.. files.OrderBy(_ => rng.Next())];
                    }
                    if(files.Length == 0) {
                        Console.WriteLine($"No files found matching pattern '{pattern}' in directory: {directory}");
                        continue;
                    }

                    filesToPlay.AddRange(files);
                } else if(Directory.Exists(input)) {
                    string[] files = [..Directory.GetFiles(input, "*.*", SearchOption.AllDirectories).Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))];
                    if(cli.Randomize) {
                        Random rng = new();
                        files = [.. files.OrderBy(_ => rng.Next())];
                    }
                    if(files.Length == 0) {
                        Console.WriteLine($"No files found in directory: {input}");
                        continue;
                    }

                    filesToPlay.AddRange(files);
                } else {
                    Console.WriteLine($"File or directory not found: {input}");
                }
            }

            if(filesToPlay.Count == 0) {
                Console.WriteLine("No valid files to play.");
                return;
            }

            ConsoleRenderer.InitializeConsole();
            _ = Task.Run(() => ConsoleRenderer.RenderLoop(() => currentSoundFile, cli.ShowSampleProgress));

            OpenAlStreamPlayer.PlaylistCount = filesToPlay.Count;
            int idx = 0;
            while(idx >= 0 && idx < filesToPlay.Count) {
                OpenAlStreamPlayer.PlaylistIndex = idx;
                cli.ModFile = filesToPlay[idx];
                await PlayFile(cli);

                PlaybackRequest req = OpenAlStreamPlayer.request;
                OpenAlStreamPlayer.request = PlaybackRequest.None;

                if(req == PlaybackRequest.Quit) break;
                if(req == PlaybackRequest.Previous) idx--;
                else idx++;
            }
            ConsoleRenderer.RestoreConsole();
        }

        static async Task PlayFile(Cli cli) {
            SoundFile sf = OpenAlStreamPlayer.LoadSoundFile(cli);

            if(cli.ExportPath.Length > 0) {
                WavExporter.Export(sf, cli.ExportPath, cli.SampleRate, cli.BitDepth, cli.Channels);
                return;
            }

            currentSoundFile = sf;
            await OpenAlStreamPlayer.Play(sf, cli.SampleRate, cli.BitDepth, cli.Channels);
        }
    }
}