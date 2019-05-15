using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

// http://www.shikadi.net/moddingwiki/S3M_Format
// https://www.fileformat.info/format/mod/corion.htm
// https://en.wikipedia.org/wiki/C_(musical_note)

namespace SharpMod {
    public class S3MTools {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct S3MFileHeader {
            public enum S3MMagic {
                idEOF = 0x1A,
                idS3MType = 0x10,
                idPanning = 0xFC
            }

            public enum S3MTrackerVersions {
                trackerMask = 0xF000,
                versionMask = 0x0FFF,

                trkScreamTracker = 0x1000,
                trkImagoOrpheus = 0x2000,
                trkImpulseTracker = 0x3000,
                trkSchismTracker = 0x4000,
                trkOpenMPT = 0x5000,
                trkBeRoTracker = 0x6000,
                trkCreamTracker = 0x7000,

                trkST3_20 = 0x1320,
                trkIT2_14 = 0x3214,
                trkBeRoTrackerOld = 0x4100,     // Used from 2004 to 2012
                trkCamoto = 0xCA00
            }

            public enum S3MHeaderFlags {
                st2Vibrato = 0x01,              // Vibrato is twice as deep. Cannot be enabled from UI.
                zeroVolOptim = 0x08,            // Volume 0 optimisations
                amigaLimits = 0x10,             // Enforce Amiga limits
                fastVolumeSlides = 0x40         // Fast volume slides (like in ST3.00)
            }

            // S3M Format Versions
            public enum S3MFormatVersion {
                oldVersion = 0x01,  // Old Version, signed samples
                newVersion = 0x02  // New Version, unsigned samples
            }

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 28)] internal byte[] name;         // Song Title
            public byte dosEof;                        // Supposed to be 0x1A, but even ST3 seems to ignore this sometimes (see STRSHINE.S3M by Purple Motion)
            public byte fileType;                      // File Type, 0x10 = ST3 module
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)] internal byte[] reserved1;     // Reserved
            public UInt16 ordNum;                      // Number of order items
            public UInt16 smpNum;                      // Number of sample parapointers
            public UInt16 patNum;                      // Number of pattern parapointers
            public UInt16 flags;                       // Flags, see S3MHeaderFlags
            public UInt16 cwtv;                        // "Made With" Tracker ID, see S3MTrackerVersions
            public UInt16 formatVersion;               // Format Version, see S3MFormatVersion
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] internal byte[] magic;         // "SCRM" magic bytes
            public byte globalVol;                     // Default Global Volume (0...64)
            public byte speed;                         // Default Speed (1...254)
            public byte tempo;                         // Default Tempo (33...255)
            public byte masterVolume;                  // Sample Volume (0...127, stereo if high bit is set)
            public byte ultraClicks;                   // Number of channels used for ultra click removal
            public byte usePanningTable;               // 0xFC => read extended panning table
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] internal byte[] reserved2;     // More reserved bytes
            public UInt16 special;                     // Pointer to special custom data (unused)
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] channels;     // Channel setup

            public string Name { get { return Encoding.UTF8.GetString(name).Trim('\0'); } }
            public string Magic { get { return Encoding.UTF8.GetString(magic).Trim('\0'); } }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct S3MSampleHeader {
            public enum SampleType {
                typeNone = 0,
                typePCM = 1,
                typeAdMel = 2
            };

            public enum SampleFlags {
                smpLoop = 0x01,
                smpStereo = 0x02,
                smp16Bit = 0x04,
            };

            public enum SamplePacking {
                pUnpacked = 0x00,   // PCM
                pDP30ADPCM = 0x01,  // Unused packing type
                pADPCM = 0x04,  // MODPlugin ADPCM :(
            };

            byte sampleType;     // Sample type, see SampleType
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)] public byte[] filename;      // Sample filename
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public byte[] dataPointer; // Pointer to sample data (divided by 16)
            public UInt32 length;            // Sample length, in samples
            public UInt32 loopStart;         // Loop start, in samples
            public UInt32 loopEnd;           // Loop end, in samples
            public byte defaultVolume;      // Default volume (0...64)
            public byte reserved1;         // Reserved
            public byte pack;               // Packing algorithm, SamplePacking
            public byte flags;              // Sample flags
            public UInt32 c5speed;           // Middle-C (C-5) frequency
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)] public byte[] reserved2;     // Reserved + Internal ST3 stuff
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 28)] public byte[] name;          // Sample name
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public byte[] magic;          // "SCRS" magic bytes ("SCRI" for Adlib instruments)

            public string FileName { get { return Encoding.UTF8.GetString(filename).Trim('\0'); } }
            public string Name { get { return Encoding.UTF8.GetString(name).Trim('\0'); } }
            public string Magic { get { return Encoding.UTF8.GetString(magic).Trim('\0'); } }
        }

        public static SoundFile.Effects ConvertEffect(SoundFile.Effects c, int fromIT) {
            SoundFile.Effects e = SoundFile.Effects.INVALID;
            switch((int)c | 0x40) {
                case 'A': e = SoundFile.Effects.CMD_SPEED; break;
                case 'B': e = SoundFile.Effects.CMD_POSITIONJUMP; break;
                case 'C': e = SoundFile.Effects.CMD_PATTERNBREAK; break;//if(!fromIT) m.param = (m.param >> 4) * 10 + (m.param & 0x0F); break;
                case 'D': e = SoundFile.Effects.CMD_VOLUMESLIDE; break;
                case 'E': e = SoundFile.Effects.CMD_PORTAMENTODOWN; break;
                case 'F': e = SoundFile.Effects.CMD_PORTAMENTOUP; break;
                case 'G': e = SoundFile.Effects.CMD_TONEPORTAMENTO; break;
                case 'H': e = SoundFile.Effects.CMD_VIBRATO; break;
                case 'I': e = SoundFile.Effects.CMD_TREMOR; break;
                case 'J': e = SoundFile.Effects.CMD_ARPEGGIO; break;
                case 'K': e = SoundFile.Effects.CMD_VIBRATOVOL; break;
                case 'L': e = SoundFile.Effects.CMD_TONEPORTAVOL; break;
                case 'M': e = SoundFile.Effects.CMD_CHANNELVOLUME; break;
                case 'N': e = SoundFile.Effects.CMD_CHANNELVOLSLIDE; break;
                case 'O': e = SoundFile.Effects.CMD_OFFSET; break;
                case 'P': e = SoundFile.Effects.CMD_PANNINGSLIDE; break;
                case 'Q': e = SoundFile.Effects.CMD_RETRIG; break;
                case 'R': e = SoundFile.Effects.CMD_TREMOLO; break;
                case 'S': e = SoundFile.Effects.CMD_S3MCMDEX; break;
                case 'T': e = SoundFile.Effects.CMD_TEMPO; break;
                case 'U': e = SoundFile.Effects.CMD_FINEVIBRATO; break;
                case 'V': e = SoundFile.Effects.CMD_GLOBALVOLUME; break;
                case 'W': e = SoundFile.Effects.CMD_GLOBALVOLSLIDE; break;
                case 'X': e = SoundFile.Effects.CMD_PANNING8; break;
                case 'Y': e = SoundFile.Effects.CMD_PANBRELLO; break;
                case 'Z': e = SoundFile.Effects.CMD_MIDI; break;
            }
            return e;
        }
    }
}
