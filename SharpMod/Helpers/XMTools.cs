using System;
using System.Runtime.InteropServices;
using System.Text;

// https://www.fileformat.info/format/xm/corion.htm

namespace SharpMod {
    public class XMTools {
        public enum PatternFlags {
            IsPackByte = 0x80,
            AllFlags = 0xFF,
            NotePresent = 0x01,
            InstrPresent = 0x02,
            VolPresent = 0x04,
            CommandPresent = 0x08,
            ParamPresent = 0x10
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct XMFileHeader {
            public enum XMHeaderFlags {
                linearSlides = 0x01,
                extendedFilterRange = 0x1000
            }

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] internal byte[] signature;     // "Extended Module: "
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)] internal byte[] songName;      // Song Name, not null-terminated (any nulls are treated as spaces)
            public byte eof;                // DOS EOF Character (0x1A)
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)] internal byte[] trackerName;   // Software that was used to create the XM file
            public UInt16 version;           // File version (1.02 - 1.04 are supported)
            public UInt32 size;              // Header Size
            public UInt16 orders;            // Number of Orders
            public UInt16 restartPos;        // Restart Position
            public UInt16 channels;          // Number of Channels
            public UInt16 patterns;          // Number of Patterns
            public UInt16 instruments;       // Number of Instruments
            public UInt16 flags;             // Song Flags
            public UInt16 speed;             // Default Speed
            public UInt16 tempo;             // Default Tempo

            public string Signature { get { return Encoding.UTF8.GetString(signature).Trim('\0'); } }
            public string Name { get { return Encoding.UTF8.GetString(songName).Trim('\0'); } }
            public string Tracker { get { return Encoding.UTF8.GetString(trackerName).Trim('\0'); } }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct XMInstrument {
            // Envelope Flags
            public enum XMEnvelopeFlags {
                envEnabled = 0x01,
                envSustain = 0x02,
                envLoop = 0x04
            }

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 96)] internal byte[] sampleMap;      // Note -> Sample assignment
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 24)] internal UInt16[] volEnv;        // Volume envelope nodes / values (0...64)
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 24)] internal UInt16[] panEnv;        // Panning envelope nodes / values (0...63)
            public byte volPoints;          // Volume envelope length
            public byte panPoints;          // Panning envelope length
            public byte volSustain;     // Volume envelope sustain point
            public byte volLoopStart;       // Volume envelope loop start point
            public byte volLoopEnd;     // Volume envelope loop end point
            public byte panSustain;     // Panning envelope sustain point
            public byte panLoopStart;       // Panning envelope loop start point
            public byte panLoopEnd;     // Panning envelope loop end point
            public byte volFlags;           // Volume envelope flags
            public byte panFlags;           // Panning envelope flags
            public byte vibType;            // Sample Auto-Vibrato Type
            public byte vibSweep;           // Sample Auto-Vibrato Sweep
            public byte vibDepth;           // Sample Auto-Vibrato Depth
            public byte vibRate;            // Sample Auto-Vibrato Rate
            public UInt16 volFade;           // Volume Fade-Out
            public byte midiEnabled;        // MIDI Out Enabled (0 / 1)
            public byte midiChannel;        // MIDI Channel (0...15)
            public UInt16 midiProgram;       // MIDI Program (0...127)
            public UInt16 pitchWheelRange;   // MIDI Pitch Wheel Range (0...36 halftones)
            public byte muteComputer;       // Mute instrument if MIDI is enabled (0 / 1)
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] internal byte[] reserved;       // Reserved

            public enum EnvType {
                Volume,
                Panning,
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct XMInstrumentHeader {
            public UInt32 size;              // Size of XMInstrumentHeader + XMInstrument
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 22)] internal byte[] name;          // Instrument Name, not null-terminated (any nulls are treated as spaces)
            public byte type;               // Instrument Type (Apparently FT2 writes some crap here, but it's the same crap for all instruments of the same module!)
            public UInt16 numSamples;        // Number of Samples associated with instrument
            public UInt32 sampleHeaderSize;  // Size of XMSample
            public XMInstrument instrument;

            public string Name { get { return Encoding.UTF8.GetString(name).Trim('\0'); } }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct XIInstrumentHeader {
            public enum Versions {
                fileVersion = 0x102
            }

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 21)] internal byte[] signature;     // "Extended Instrument: "
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 22)] internal byte[] name;          // Instrument Name, not null-terminated (any nulls are treated as spaces)
            public byte eof;                // DOS EOF Character (0x1A)
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)] internal byte[] trackerName;   // Software that was used to create the XI file
            public UInt16 version;           // File Version (1.02)
            public XMInstrument instrument;
            public UInt16 numSamples;        // Number of embedded sample headers + samples

            public string Signature { get { return Encoding.UTF8.GetString(signature).Trim('\0'); } }
            public string Name { get { return Encoding.UTF8.GetString(name).Trim('\0'); } }
            public string Tracker { get { return Encoding.UTF8.GetString(trackerName).Trim('\0'); } }
        };

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct XMSample {
            public enum XMSampleFlags {
                sampleLoop = 0x01,
                sampleBidiLoop = 0x02,
                sample16Bit = 0x10,
                sampleStereo = 0x20,

                sampleADPCM = 0xAD     // MODPlugin :(
            }

            public UInt32 length;        // Sample Length (in bytes)
            public UInt32 loopStart;     // Loop Start (in bytes)
            public UInt32 loopLength;    // Loop Length (in bytes)
            public byte vol;            // Default Volume
            public byte finetune;        // Sample Finetune
            public byte flags;          // Sample Flags
            public byte pan;            // Sample Panning
            public byte relnote;     // Sample Transpose
            public byte reserved;       // Reserved (abused for ModPlug's ADPCM compression)
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 22)] internal byte[] name;      // Sample Name, not null-terminated (any nulls are treated as spaces)

            public string Name { get { return Encoding.UTF8.GetString(name).Trim('\0'); } }
        }
    }
}
