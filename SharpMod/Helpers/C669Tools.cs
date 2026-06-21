using System.Runtime.InteropServices;
using SharpMod.Helpers;

// https://moddingwiki.shikadi.net/wiki/669_Format
// https://github.com/OpenMPT/openmpt (soundlib/Load_669.cpp)

namespace SharpMod {
    public class C669Tools {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct C669FileHeader {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]   internal byte[] magic;       // "if" (Composer 669) or "JN" (UNIS 669)
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 108)] internal byte[] message;     // 3 lines x 36 bytes song message
            public byte samples;                                                                // number of samples (0..64)
            public byte patterns;                                                               // number of patterns (0..128)
            public byte restartPos;                                                             // loop-to order index
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)] public byte[] orders;        // 0xFF = end, 0xFE = skip
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)] public byte[] tempos;        // per-pattern ticks-per-row (1..15)
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)] public byte[] breaks;        // per-pattern last valid row (0..63)

            public bool IsExtended { get { return magic[0] == (byte)'J' && magic[1] == (byte)'N'; } }
            public bool IsComposer { get { return magic[0] == (byte)'i' && magic[1] == (byte)'f'; } }
            public string Message {
                get { return LegacyEncoding.Cp437.GetString(message).TrimEnd('\0', ' '); }
            }
            public string SongName {
                get {
                    byte[] line = new byte[36];
                    System.Array.Copy(message, 0, line, 0, 36);
                    return LegacyEncoding.Cp437.GetString(line).TrimEnd('\0', ' ');
                }
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct C669Sample {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 13)] public byte[] filename;
            public uint length;
            public uint loopStart;
            public uint loopEnd;

            public string FileName { get { return LegacyEncoding.Cp437.GetString(filename).TrimEnd('\0', ' '); } }
        }

        // Mirrors OpenMPT Load_669.cpp::ValidateHeader. 669's two-byte magic is weak so we
        // also validate the per-pattern tempo/break tables and the order list to keep
        // unrelated files from being misidentified as 669.
        public static bool IsValidHeader(C669FileHeader h) {
            if(!h.IsComposer && !h.IsExtended) return false;
            if(h.samples > 64) return false;
            if(h.restartPos >= 128) return false;
            if(h.patterns > 128) return false;
            for(int i = 0; i < 128; i++) {
                if(h.orders[i] >= 128 && h.orders[i] < 0xFE) return false;
                if(h.orders[i] < 128 && h.tempos[i] == 0) return false;
                if(h.tempos[i] > 15) return false;
                if(h.breaks[i] >= 64) return false;
            }
            return true;
        }
    }
}
