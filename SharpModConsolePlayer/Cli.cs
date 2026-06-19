using System.Reflection;
using PrettyConsole;
using static PrettyConsole.Color;

namespace SharpModConsolePlayer {
    internal class Cli {
        internal string ModFile { get; set; } = string.Empty;
        internal List<string> ModFiles { get; init; } = [];
        internal int SampleRate { get; init; } = 44100;
        internal int BitDepth { get; init; } = 16;
        internal int Channels { get; init; } = 2;
        internal bool Loop { get; init; } = false;
        internal bool ShowSampleProgress { get; init; } = true;
        internal string ExportPath { get; init; } = string.Empty;

        private static readonly int[] ValidSampleRates = [8000, 11025, 16000, 22050, 32000, 44100, 48000, 88200, 96000];
        private static readonly int[] ValidBitDepths = [8, 16];

        internal static Cli? Parse(string[] args) {
            if(args.Length == 0) {
                PrintUsage();
                return null;
            }

            List<string> modFiles = [];
            int sampleRate = 44100;
            int bitDepth = 16;
            bool loop = false;
            bool showSampleProgress = true;
            string exportPath = string.Empty;

            for(int i = 0; i < args.Length; i++) {
                string a = args[i];
                switch(a) {
                    case "-h":
                    case "--help":
                        PrintUsage();
                        return null;
                    case "-r":
                    case "--sample-rate":
                        if(!TryReadIntOption(args, ref i, a, ValidSampleRates, out sampleRate)) return null;
                        break;
                    case "-b":
                    case "--bit-depth":
                        if(!TryReadIntOption(args, ref i, a, ValidBitDepths, out bitDepth)) return null;
                        break;
                    case "-l":
                    case "--loop":
                        loop = true;
                        break;
                    case "-P":
                    case "--no-sample-progress":
                        showSampleProgress = false;
                        break;
                    case "-o":
                    case "--export":
                        if(i + 1 >= args.Length) { PrintError($"Option {a} requires a path."); return null; }
                        exportPath = args[++i];
                        break;
                    default:
                        if(a.StartsWith('-')) {
                            PrintError($"Unknown option: {a}");
                            return null;
                        }
                        modFiles.Add(a);
                        break;
                }
            }

            if(modFiles.Count == 0) {
                PrintError("Missing required <modfile> argument.");
                return null;
            }

            if(exportPath.Length > 0) loop = false;

            return new Cli {
                ModFile = modFiles[0],
                ModFiles = modFiles,
                SampleRate = sampleRate,
                BitDepth = bitDepth,
                Loop = loop,
                ShowSampleProgress = showSampleProgress,
                ExportPath = exportPath,
            };
        }

        private static bool TryReadIntOption(string[] args, ref int i, string name, int[] allowed, out int value) {
            value = 0;
            if(i + 1 >= args.Length) {
                PrintError($"Option {name} requires a value.");
                return false;
            }
            string raw = args[++i];
            if(!int.TryParse(raw, out value)) {
                PrintError($"Option {name} expects an integer, got '{raw}'.");
                return false;
            }
            if(Array.IndexOf(allowed, value) < 0) {
                PrintError($"Option {name} value '{raw}' is not allowed. Valid: {string.Join(", ", allowed)}.");
                return false;
            }
            return true;
        }

        private static void PrintError(string message) {
            Console.WriteLineInterpolated($"{Red}error:{Default} {message}");
            Console.NewLine();
            PrintUsage();
        }

        private static void PrintUsage() {
            string name = Assembly.GetExecutingAssembly().GetName().Name ?? "SharpModConsolePlayer";
            string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

            Console.WriteLineInterpolated($"{Magenta}{name}{Default} {DarkGray} {version}{Default}");
            Console.WriteLineInterpolated($"{DarkGray}A console player for MOD/S3M/XM tracker files.{Default}");
            Console.NewLine();

            Console.WriteLineInterpolated($"{Yellow}USAGE{Default}");
            Console.WriteLineInterpolated($"  {White}{name}{Default} {Cyan}<modfile>{Default} [{Green}options{Default}]");
            Console.NewLine();

            Console.WriteLineInterpolated($"{Yellow}ARGUMENTS{Default}");
            Console.WriteLineInterpolated($"  {Cyan}<modfile>{Default}                Can be a single file, a directory (recursively searches for supported files)");
            Console.WriteLineInterpolated($"                           or a glob pattern (e.g. {DarkGray}music/*.mod{Default})");
            Console.NewLine();

            Console.WriteLineInterpolated($"{Yellow}OPTIONS{Default}");
            Console.WriteLineInterpolated($"  {Green}-r{Default}, {Green}--sample-rate{Default} {DarkGray}<hz>{Default}   Output sample rate in Hz. Default: {White}44100{Default}");
            Console.WriteLineInterpolated($"                              Valid: {DarkGray}{string.Join(", ", ValidSampleRates)}{Default}");
            Console.WriteLineInterpolated($"  {Green}-b{Default}, {Green}--bit-depth{Default} {DarkGray}<n>{Default}      Output bit depth. Default: {White}16{Default}");
            Console.WriteLineInterpolated($"                              Valid: {DarkGray}8, 16{Default}");
            Console.WriteLineInterpolated($"  {Green}-l{Default}, {Green}--loop{Default}               Loop the track when it ends");
            Console.WriteLineInterpolated($"  {Green}-P{Default}, {Green}--no-sample-progress{Default} Disable the in-name playback progress bar in the samples view");
            Console.WriteLineInterpolated($"  {Green}-o{Default}, {Green}--export{Default} {DarkGray}<path>{Default}      Render the track to a WAV file at {DarkGray}<path>{Default} (no live playback)");
            Console.WriteLineInterpolated($"  {Green}-h{Default}, {Green}--help{Default}               Show this help and exit");
            Console.NewLine();

            Console.WriteLineInterpolated($"{Yellow}KEYS{Default}");
            Console.WriteLineInterpolated($"  {Green}F1{Default}                       Show this help");
            Console.WriteLineInterpolated($"  {Green}Tab{Default}                      Toggle between patterns and samples view");
            Console.WriteLineInterpolated($"  {Green}Left{Default} / {Green}Right{Default}             Scroll channels horizontally");
            Console.WriteLineInterpolated($"  {Green}Up{Default} / {Green}Down{Default}                Scroll samples vertically");
            Console.WriteLineInterpolated($"  {Green}PageUp{Default} / {Green}PageDown{Default}        Seek track backward/forward");
            Console.WriteLineInterpolated($"  {Green}Home{Default} / {Green}End{Default}               Jump to previous/next file in the playlist");
            Console.WriteLineInterpolated($"  {Green}1{Default} - {Green}9{Default}                    Toggle mute on channels 1-9");
            Console.WriteLineInterpolated($"  {Green}Shift{Default} + {Green}1{Default} - {Green}9{Default}            Toggle mute on channels 10-18");
            Console.WriteLineInterpolated($"  {Green}Ctrl{Default} + {Green}1{Default} - {Green}9{Default}             Toggle mute on channels 19-27");
            Console.WriteLineInterpolated($"  {Green}Esc {Default}| {Green}Q{Default}                  Stop playback and exit");
        }
    }
}
