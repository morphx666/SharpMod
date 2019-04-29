using System.IO;
using System.Text;

namespace SharpMod {
    public partial class SoundFile {
        private const int MOD_PRECISION = 10;
        private const int MOD_FRACMASK = 1023;
        private const int MOD_AMIGAC2 = 0x1AB;

        private static readonly uint[] FineTuneTable = {
            7895, 7941, 7985, 8046, 8107, 8169, 8232, 8280,
            8363, 8413, 8463, 8529, 8581, 8651, 8723, 8757,
        };

        // Sinus table
        private static readonly int[] ModSinusTable = {
            0, 12, 25, 37, 49, 60, 71, 81, 90, 98, 106, 112, 117, 122, 125, 126,
            127, 126, 125, 122, 117, 112, 106, 98, 90, 81, 71, 60, 49, 37, 25, 12,
            0, -12, -25, -37, -49, -60, -71, -81, -90, -98, -106, -112, -117, -122, -125, -126,
            -127, -126, -125, -122, -117, -112, -106, -98, -90, -81, -71, -60, -49, -37, -25, -12
        };

        // Triangle wave table (ramp down)
        private static readonly int[] ModRampDownTable = {
            0, -4, -8, -12, -16, -20, -24, -28, -32, -36, -40, -44, -48, -52, -56, -60,
            -64, -68, -72, -76, -80, -84, -88, -92, -96, -100, -104, -108, -112, -116, -120, -124,
            127, 123, 119, 115, 111, 107, 103, 99, 95, 91, 87, 83, 79, 75, 71, 67,
            63, 59, 55, 51, 47, 43, 39, 35, 31, 27, 23, 19, 15, 11, 7, 3
        };

        // Square wave table
        private static readonly int[] ModSquareTable = {
            127, 127, 127, 127, 127, 127, 127, 127, 127, 127, 127, 127, 127, 127, 127, 127,
            127, 127, 127, 127, 127, 127, 127, 127, 127, 127, 127, 127, 127, 127, 127, 127,
            -127, -127, -127, -127, -127, -127, -127, -127, -127, -127, -127, -127, -127, -127, -127, -127,
            -127, -127, -127, -127, -127, -127, -127, -127, -127, -127, -127, -127, -127, -127, -127, -127
        };

        // Random wave table
        private static readonly int[] ModRandomTable = {
            98, -127, -43, 88, 102, 41, -65, -94, 125, 20, -71, -86, -70, -32, -16, -96,
            17, 72, 107, -5, 116, -69, -62, -40, 10, -61, 65, 109, -18, -38, -13, -76,
            -23, 88, 21, -94, 8, 106, 21, -112, 6, 109, 20, -88, -30, 9, -127, 118,
            42, -34, 89, -4, -51, -72, 21, -29, 112, 123, 84, -101, -92, 98, -54, -95
        };

        public struct ModInstrument {
            public uint Length;
            public uint LoopStart, LoopEnd;
            public uint FineTune;
            public int Volume;
            public byte[] Sample;
            internal byte[] name;
            public string Name { get { return Encoding.UTF8.GetString(name).Trim('\0'); } }
        }

        public struct ModChannel {
            public uint InstrumentIndex;
            public uint FineTune;
            public uint Pos, Inc;
            public uint Length, LoopStart, LoopEnd;
            public int Volume, VolumeSlide, OldVolumeSlide;
            public int Period, OldPeriod;
            public int FreqSlide, OldFreqSlide;
            public int PortamentoDest, PortamentoSlide;
            public int VibratoPos, VibratoSlide, VibratoType;
            public int TremoloPos, TremoloSlide, TremoloType;
            public int Count1, Count2;
            public int Period1, Period2;
            public bool Portamento, Vibrato, Tremolo;
            public byte[] Sample;
            public int OldVol;
            public short CurrentVolume, NextInstrumentIndex;
        }

        private readonly FileStream mFile;
        public readonly ModInstrument[] Instruments;
        public readonly ModChannel[] Channels = new ModChannel[32];
        private byte[] order = new byte[256];
        private byte[][] patterns;
    }
}
