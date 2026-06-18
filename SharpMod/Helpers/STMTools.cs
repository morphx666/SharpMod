using System.Runtime.InteropServices;
using System.Text;

// https://moddingwiki.shikadi.net/wiki/STM_Format
// https://github.com/OpenMPT/openmpt (soundlib/Load_stm.cpp)

namespace SharpMod {
    public class STMTools {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct STMFileHeader {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)] internal byte[] songname;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]  internal byte[] trackername;
            public byte dosEof;                                                 // 0x1A (some broken files use 0x02)
            public byte filetype;                                               // 1 = song, 2 = module (only 2 is loadable)
            public byte verMajor;                                               // 2 for ST2
            public byte verMinor;                                               // 0, 10, 20 or 21
            public byte initTempo;                                              // BCD on verMinor < 21, hex otherwise
            public byte numPatterns;
            public byte globalVolume;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 13)] internal byte[] reserved;

            public string SongName    { get { return Encoding.UTF8.GetString(songname).TrimEnd('\0'); } }
            public string TrackerName { get { return Encoding.UTF8.GetString(trackername); } }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct STMSampleHeader {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)] public byte[] filename;
            public byte zero;                                                   // putup10.stm has 46 instead of 0
            public byte disk;
            public ushort offset;                                               // 20-bit file offset / 16
            public ushort length;
            public ushort loopStart;
            public ushort loopEnd;                                              // 0xFFFF means no loop
            public byte volume;
            public byte reserved2;
            public ushort sampleRate;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] internal byte[] reserved3;

            public string FileName { get { return Encoding.UTF8.GetString(filename).TrimEnd('\0'); } }
        }

        // Mirrors OpenMPT Load_stm.cpp::ValidateHeader. Rejects files that are clearly not STM
        // while still accepting putup10.stm-style broken-but-real modules (dosEof == 2).
        public static bool IsValidHeader(STMFileHeader h) {
            if(h.filetype != 2) return false;
            if(h.dosEof != 0x1A && h.dosEof != 0x02) return false;
            if(h.verMajor != 2) return false;
            if(h.verMinor != 0 && h.verMinor != 10 && h.verMinor != 20 && h.verMinor != 21) return false;
            if(h.numPatterns > 64) return false;
            if(h.globalVolume > 64 && h.globalVolume != 0x58) return false;
            // ST2/ST3 don't validate the tracker string, but require it to look like ASCII so we
            // don't generate false positives against the few magic bytes STM has.
            foreach(byte c in h.trackername) {
                if(c < 0x20 || c >= 0x7F) return false;
            }
            return true;
        }
    }
}
