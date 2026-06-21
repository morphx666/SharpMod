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
        internal bool Randomize { get; init; } = false;
        internal string ExportPath { get; init; } = string.Empty;
        internal int SampleHeight { get; init; } = 2;
        internal bool ShowMetadata { get; init; } = false;

        private static readonly int[] ValidSampleRates = [8000, 11025, 16000, 22050, 32000, 44100, 48000, 88200, 96000];
        private static readonly int[] ValidBitDepths = [8, 16];
        private static readonly int[] ValidSampleHeights = [0, 1, 2, 3];

        private const int KeyColumnWidth = 25;
        private const int DescColumnWidth = 45;

        // KeyVisibleWidth must be kept in sync by hand with the visible character count of each row's WriteKey lambda.
        private static readonly (int KeyVisibleWidth, string Description, Action WriteKey)[] keyBindings = [
            (2,  "Show this help",                             () => Console.WriteInterpolated($"{Green}F1{Default}")),
            (3,  "Toggle between patterns and samples view.",  () => Console.WriteInterpolated($"{Green}Tab{Default}")),
            (5,  "Toggle pause",                               () => Console.WriteInterpolated($"{Green}Space{Default}")),
            (12, "Scroll channels horizontally",               () => Console.WriteInterpolated($"{Green}Left{Default} / {Green}Right{Default}")),
            (9,  "Scroll samples vertically",                  () => Console.WriteInterpolated($"{Green}Up{Default} / {Green}Down{Default}")),
            (17, "Seek track backward/forward",                () => Console.WriteInterpolated($"{Green}PageUp{Default} / {Green}PageDown{Default}")),
            (10, "Jump to previous/next file in the playlist", () => Console.WriteInterpolated($"{Green}Home{Default} / {Green}End{Default}")),
            (5,  "Toggle mute on channels 1-9",                () => Console.WriteInterpolated($"{Green}1{Default} - {Green}9{Default}")),
            (13, "Toggle mute on channels 10-18",              () => Console.WriteInterpolated($"{Green}Shift{Default} + {Green}1{Default} - {Green}9{Default}")),
            (12, "Toggle mute on channels 19-27",              () => Console.WriteInterpolated($"{Green}Ctrl{Default} + {Green}1{Default} - {Green}9{Default}")),
            (7,  "Stop playback and exit",                     () => Console.WriteInterpolated($"{Green}Esc {Default}| {Green}Q{Default}")),
        ];

        internal static Cli? Parse(string[] args) {
            if(args.Length == 0) {
                PrintUsage();
                return null;
            }

            List<string> modFiles = [];
            int sampleRate = 44100;
            int bitDepth = 16;
            bool loop = false;
            string exportPath = string.Empty;
            bool randomize = false;
            int sampleHeight = 2;
            bool showMetadata = false;

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
                    case "-x":
                    case "--export":
                        if(i + 1 >= args.Length) { PrintError($"Option {a} requires a path."); return null; }
                        exportPath = args[++i];
                        break;
                    case "-z":
                    case "--randomize":
                        randomize = true;
                        break;
                    case "-H":
                    case "--sample-height":
                        if(!TryReadIntOption(args, ref i, a, ValidSampleHeights, out sampleHeight)) return null;
                        break;
                    case "-m":
                    case "--metadata":
                        showMetadata = true;
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
                ExportPath = exportPath,
                Randomize = randomize,
                SampleHeight = sampleHeight,
                ShowMetadata = showMetadata
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
            Console.WriteLineInterpolated($"  {Green}-x{Default}, {Green}--export{Default} {DarkGray}<path>{Default}      Render the track to a WAV file at {DarkGray}<path>{Default} (no live playback)");
            Console.WriteLineInterpolated($"  {Green}-z{Default}, {Green}--randomize{Default}          Randomize the order of files in the playlist");
            Console.WriteLineInterpolated($"  {Green}-H{Default}, {Green}--sample-height{Default} {DarkGray}<n>{Default}  Console rows per sample waveform. Default: {White}2{Default} ({DarkGray}0 hides the waveform{Default})");
            Console.WriteLineInterpolated($"                              Valid: {DarkGray}0, 1, 2, 3{Default}");
            Console.WriteLineInterpolated($"  {Green}-m{Default}, {Green}--metadata{Default}           Show sample metadata columns (Length, Vol, Fmt, LoopStart, LoopEnd)");
            Console.WriteLineInterpolated($"  {Green}-h{Default}, {Green}--help{Default}               Show this help and exit");
            Console.NewLine();

            Console.WriteLineInterpolated($"{Yellow}EXAMPLES{Default}");
            Console.WriteLineInterpolated($"  {DarkGray}# Play a single file{Default}");
            Console.WriteLineInterpolated($"  {White}{name}{Default} {Cyan}\"mods/Future Crew - Second Reality.S3M\"{Default}");
            Console.WriteLineInterpolated($"  {DarkGray}# Play every supported file in a directory (recursively){Default}");
            Console.WriteLineInterpolated($"  {White}{name}{Default} {Cyan}mods{Default}");
            Console.WriteLineInterpolated($"  {DarkGray}# Play every .XM file matched by a glob pattern{Default}");
            Console.WriteLineInterpolated($"  {White}{name}{Default} {Cyan}mods/*.XM{Default}");
            Console.NewLine();

            Console.WriteLineInterpolated($"{Yellow}KEYS{Default}");
            PrintKeyBindings("  ");
        }

        internal static void PrintKeyBindings(string prefix = "", string suffix = "") {
            int col = Console.CursorLeft;
            int row = Console.CursorTop;
            for(int i = 0; i < keyBindings.Length; i++) {
                var (keyWidth, description, writeKey) = keyBindings[i];
                Console.SetCursorPosition(col, row + i);
                Console.WriteInterpolated($"{Cyan}{prefix}{Default}");
                writeKey();
                Console.WriteLineInterpolated($"{new WhiteSpace(KeyColumnWidth - keyWidth)}{description}{new WhiteSpace(DescColumnWidth - description.Length)}{Cyan}{suffix}{Default}");
            }
        }
    }
}
