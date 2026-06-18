using System.Reflection;
using PrettyConsole;
using static PrettyConsole.Color;

namespace SharpModConsolePlayer {
    internal class Cli {
        internal string ModFile { get; init; } = string.Empty;
        internal int SampleRate { get; init; } = 44100;
        internal int BitDepth { get; init; } = 16;
        internal int Channels { get; init; } = 2;
        internal bool Loop { get; init; } = false;
        internal bool ShowSampleProgress { get; init; } = true;

        private static readonly int[] ValidSampleRates = [8000, 11025, 16000, 22050, 32000, 44100, 48000, 88200, 96000];
        private static readonly int[] ValidBitDepths = [8, 16];

        internal static Cli? Parse(string[] args) {
            if(args.Length == 0) {
                PrintUsage();
                return null;
            }

            string modFile = string.Empty;
            int sampleRate = 44100;
            int bitDepth = 16;
            bool loop = false;
            bool showSampleProgress = true;

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
                    default:
                        if(a.StartsWith('-')) {
                            PrintError($"Unknown option: {a}");
                            return null;
                        }
                        if(modFile.Length > 0) {
                            PrintError($"Unexpected argument: {a}");
                            return null;
                        }
                        modFile = a;
                        break;
                }
            }

            if(modFile.Length == 0) {
                PrintError("Missing required <modfile> argument.");
                return null;
            }

            return new Cli {
                ModFile = modFile,
                SampleRate = sampleRate,
                BitDepth = bitDepth,
                Loop = loop,
                ShowSampleProgress = showSampleProgress,
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
            Console.WriteLineInterpolated($"  {Cyan}<modfile>{Default}                Path to the tracker module to play");
            Console.NewLine();

            Console.WriteLineInterpolated($"{Yellow}OPTIONS{Default}");
            Console.WriteLineInterpolated($"  {Green}-r{Default}, {Green}--sample-rate{Default} {DarkGray}<hz>{Default}   Output sample rate in Hz. Default: {White}44100{Default}");
            Console.WriteLineInterpolated($"                              Valid: {DarkGray}{string.Join(", ", ValidSampleRates)}{Default}");
            Console.WriteLineInterpolated($"  {Green}-b{Default}, {Green}--bit-depth{Default} {DarkGray}<n>{Default}      Output bit depth. Default: {White}16{Default}");
            Console.WriteLineInterpolated($"                              Valid: {DarkGray}8, 16{Default}");
            Console.WriteLineInterpolated($"  {Green}-l{Default}, {Green}--loop{Default}               Loop the track when it ends");
            Console.WriteLineInterpolated($"  {Green}-P{Default}, {Green}--no-sample-progress{Default} Disable the in-name playback progress bar in the samples view");
            Console.WriteLineInterpolated($"  {Green}-h{Default}, {Green}--help{Default}               Show this help and exit");
            Console.NewLine();

            Console.WriteLineInterpolated($"{Yellow}KEYS{Default}");
            Console.WriteLineInterpolated($"  {Green}Tab{Default}                      Toggle between patterns and samples view");
            Console.WriteLineInterpolated($"  {Green}Left{Default} / {Green}Right{Default}             Scroll channels horizontally");
            Console.WriteLineInterpolated($"  {Green}Up{Default} / {Green}Down{Default}                Scroll samples vertically");
            Console.WriteLineInterpolated($"  {Green}PageUp{Default} / {Green}PageDown{Default}        Seek track backward/forward");
            Console.WriteLineInterpolated($"  {Green}Esc {Default}| {Green}Q{Default}                  Stop playback and exit");
        }
    }
}
