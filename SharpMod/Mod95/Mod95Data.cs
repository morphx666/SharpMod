using System.IO;

namespace SharpMod {
    public partial class SoundFile {
        private const int MOD_PRECISION = 10;
        private const int MOD_FRACMASK = 1023;
        private const int MOD_AMIGAC2 = 0x1AB;

        // Ordered by format complexity, simplest first. The numeric values are not persisted
        // anywhere, so this ordering is purely for readability.
        public enum Types {
            INVALID = 0,
            WAV  = 1, // Raw PCM passthrough (no patterns/effects)
            STM  = 2, // ScreamTracker 2 - 4 channels, minimal effects
            C669 = 3, // Composer 669 / UNIS 669 - 8 channels, 8 effects, per-pattern speed/break
            MOD  = 4, // ProTracker / NoiseTracker - Amiga periods, fine-tune, ~32 effects
            S3M  = 5, // ScreamTracker 3 - parapointers, C5Speed, full S3M effect set
            XM   = 6  // FastTracker 2 - multi-sample instruments, envelopes, ~36 effects
        }

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

        // OpenMPT pre-amp attenuation curve (Sndmix.cpp PreAmpTable). Indexed by
        // min(declared channels, 31) / 2. Grows sub-linearly so multi-channel
        // formats get headroom without the 5-8x gain shortfall a linear divider
        // produces; calibrated so chCount=4 maps to the original Mod95 divisor.
        private static readonly byte[] PreAmpTable = {
            0x60, 0x60, 0x60, 0x70,
            0x80, 0x88, 0x90, 0x98,
            0xA0, 0xA4, 0xA8, 0xAC,
            0xB0, 0xB4, 0xB8, 0xBC,
        };

        public enum Effects {
            CMD_NONE = 0,
            CMD_ARPEGGIO = 1,
            CMD_PORTAMENTOUP = 2,
            CMD_PORTAMENTODOWN = 3,
            CMD_TONEPORTAMENTO = 4,
            CMD_VIBRATO = 5,
            CMD_TONEPORTAVOL = 6,
            CMD_VIBRATOVOL = 7,
            CMD_TREMOLO = 8,
            CMD_PANNING8 = 9,
            CMD_OFFSET = 10,
            CMD_VOLUMESLIDE = 11,
            CMD_POSITIONJUMP = 12,
            CMD_VOLUME = 13,
            CMD_PATTERNBREAK = 14,
            CMD_RETRIG = 15,
            CMD_SPEED = 16,
            CMD_TEMPO = 17,
            CMD_TREMOR = 18,
            CMD_MODCMDEX = 19,
            CMD_S3MCMDEX = 20,
            CMD_CHANNELVOLUME = 21,
            CMD_CHANNELVOLSLIDE = 22,
            CMD_GLOBALVOLUME = 23,
            CMD_GLOBALVOLSLIDE = 24,
            CMD_KEYOFF = 25,
            CMD_FINEVIBRATO = 26,
            CMD_PANBRELLO = 27,
            CMD_XFINEPORTAUPDOWN = 28,
            CMD_PANNINGSLIDE = 29,
            CMD_SETENVPOSITION = 30,
            CMD_MIDI = 31,
            CMD_SMOOTHMIDI = 32,
            CMD_DELAYCUT = 33,
            CMD_XPARAM = 34,
            CMD_NOTESLIDEUP = 35, // IMF Gxy / PTM Jxy (Slide y notes up every x ticks)
            CMD_NOTESLIDEDOWN = 36, // IMF Hxy / PTM Kxy (Slide y notes down every x ticks)
            CMD_NOTESLIDEUPRETRIG = 37, // PTM Lxy (Slide y notes up every x ticks + retrigger note)
            CMD_NOTESLIDEDOWNRETRIG = 38, // PTM Mxy (Slide y notes down every x ticks + retrigger note)
            CMD_REVERSEOFFSET = 39, // PTM Nxx Revert sample + offset
            CMD_DBMECHO = 40, // DBM enable/disable echo
            CMD_OFFSETPERCENTAGE = 41, // PLM Percentage Offset
            MAX_EFFECTS = 42,

            INVALID = 0xFF
        }

        public struct ModInstrument {
            public uint Length;
            public uint LoopStart, LoopEnd;
            public uint FineTune;
            public int Volume;
            public byte[] Sample;
            public bool Is16Bit;
            public bool IsStereo;
            internal byte[] name;
            // Optional per-sample default panning (FT2/XM). HasDefaultPan stays false for
            // formats that don't supply one so the channel pan from the format header / loader
            // is left untouched on note trigger.
            public bool HasDefaultPan;
            public short DefaultPan;        // engine range 0..256 (0 = full left, 128 = center, 256 = full right)
            public string Name { get { return Helpers.LegacyEncoding.Cp437.GetString(name).TrimEnd('\0', ' '); } }
        }

        public struct ModChannel {
            public uint InstrumentIndex;
            public uint FineTune;
            public uint Pos, Inc;
            public uint Length, LoopStart, LoopEnd;
            public uint SampleCount;
            public int Volume, VolumeSlide, OldVolumeSlide;
            public int Period, OldPeriod;
            public int FreqSlide, OldFreqSlide;
            public int PortamentoDest, PortamentoSlide;
            public int VibratoPos, VibratoSlide, VibratoType;
            public int TremoloPos, TremoloSlide, TremoloType;
            public int Count1, Count2;
            public int Period1, Period2;
            public bool Portamento, Vibrato, Tremolo;
            public bool Is16Bit, IsStereo;
            public short Pan;
            public byte[] Sample;
            public int OldVol;
            public short CurrentVolume, NextInstrumentIndex;
            public bool Muted;
        }

        private readonly Stream file;
        private readonly bool ownsStream;
        private string title;
        private string trackerName = "";
        private ModInstrument[] instruments;
        private readonly ModChannel[] channels = new ModChannel[32];
        private byte[] order = new byte[256];
        private byte[][] patterns;
    }
}