using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using SharpMod.Helpers;

/*
    This is a verbatim implementation of the magnificent code developed by Olivier Lapicque for his Mod95 player.

    For more information, visit https://openmpt.org/legacy_software

    Code ported to c# by Xavier Flix (https://github.com/morphx666) on 2019/ 4/25
    S3M (partial) support added by Xavier Flix on 2019/ 4/29
*/

namespace SharpMod {
    public partial class SoundFile {
        public readonly string FileName;

        public SoundFile(string fileName, uint sampleRate, bool is16Bit, bool isStereo, bool loop) {
            byte[] s = new byte[1024];
            S3MTools.S3MFileHeader s3mFH = new S3MTools.S3MFileHeader();
            XMTools.XMFileHeader xmFH = new XMTools.XMFileHeader();

            Type = Types.INVALID;
            Rate = sampleRate;
            Is16Bit = is16Bit;
            IsStereo = isStereo;
            Loop = loop;
            ActiveChannels = 0;

            FileName = fileName;
            mFile = new FileInfo(fileName).Open(FileMode.Open, FileAccess.Read, FileShare.Read);

            Type = Types.MOD;
            mFile.Seek(0x438, SeekOrigin.Begin);
            mFile.Read(s, 0, 4);
            s[4] = 0;
            ActiveSamples = 31;
            ActiveChannels = 4;

            if(Encoding.Default.GetString(s).TrimEnd((char)0) == "M.K.") {
                ActiveChannels = 4;
            } else {
                if((s[0] == 'F') && (s[1] == 'L') && (s[2] == 'T') && (s[3] >= '1') && (s[3] <= '9')) {
                    ActiveChannels = (uint)s[3] - 48;
                } else {
                    if((s[0] >= '1') && (s[0] <= '9') && (s[1] == 'C') && (s[2] == 'H') && (s[3] == 'N')) {
                        ActiveChannels = (uint)s[0] - 48;
                    } else {
                        if((s[0] == '1') && (s[1] >= '0') && (s[1] <= '6') && (s[2] == 'C') && (s[3] == 'H')) {
                            ActiveChannels = (uint)s[1] - 48 + 10;
                        } else {
                            mFile.Seek(0x2c, SeekOrigin.Begin);
                            mFile.Read(s, 0, 4);
                            string magic = Encoding.Default.GetString(s).TrimEnd((char)0);
                            if(magic == "SCRM") {
                                Type = Types.S3M;
                                mFile.Seek(0, SeekOrigin.Begin);
                                s3mFH = SoundFile.LoadStruct<S3MTools.S3MFileHeader>(mFile);
                            } else if(magic == "XXXX") {
                                Type = Types.S3M;
                                mFile.Seek(0, SeekOrigin.Begin);
                                s3mFH = SoundFile.LoadStruct<S3MTools.S3MFileHeader>(mFile);
                            } else {
                                mFile.Seek(0x00, SeekOrigin.Begin);
                                mFile.Read(s, 0, 17);
                                if(Encoding.Default.GetString(s).TrimEnd((char)0) == "Extended Module: ") {
                                    Type = Types.XM;
                                    mFile.Seek(0, SeekOrigin.Begin);
                                    xmFH = SoundFile.LoadStruct<XMTools.XMFileHeader>(mFile);
                                } else {
                                    ActiveSamples = 15;
                                }
                            }
                        }
                    }
                }
            }

            mFile.Seek(0, SeekOrigin.Begin);
            if(Type == Types.MOD) {
                ParseModFile(20);
            } else {
                switch(Type) {
                    case Types.S3M: ParseS3MFile(96, s3mFH); break;
                    case Types.XM: ParseXMFile(80, xmFH); break;
                }
            }
            CloseFile(true);
        }

        private void ParseModFile(int offset) {
            int i, j, k, nbp;
            byte[] bTab = new byte[32];

            mInstruments = new ModInstrument[ActiveSamples + 1];
            for(i = 0; i < mInstruments.Length; i++) {
                mInstruments[i].name = new byte[32];
            }

            MusicSpeed = 6;
            MusicTempo = 125;

            mFile.Read(mInstruments[0].name, 0, offset);
            mTitle = mInstruments[0].Name;

            for(i = 1; i <= (int)ActiveSamples; i++) {
                mFile.Read(bTab, 0, 30);
                Array.Copy(bTab, mInstruments[i].name, 22);

                if((j = (bTab[22] << 9) | (bTab[23] << 1)) < 4) j = 0;
                mInstruments[i].Length = (uint)j;

                if((j = bTab[24]) > 7) j &= 7; else j = (j & 7) + 8;
                mInstruments[i].FineTune = FineTuneTable[j];
                mInstruments[i].Volume = bTab[25];
                if(mInstruments[i].Volume > 0x40) mInstruments[i].Volume = 0x40;
                mInstruments[i].Volume <<= 2;

                if((j = (int)((uint)bTab[26] << 9) | (int)((uint)bTab[27] << 1)) < 4) j = 0;
                if((k = (int)((uint)bTab[28] << 9) | (int)((uint)bTab[29] << 1)) < 4) k = 0;
                if(j + k > (int)mInstruments[i].Length) {
                    j >>= 1;
                    k = j + ((k + 1) >> 1);
                } else k += j;
                if(mInstruments[i].Length != 0) {
                    if(j >= (int)mInstruments[i].Length) j = (int)(mInstruments[i].Length - 1);
                    if(k > (int)mInstruments[i].Length) k = (int)mInstruments[i].Length;
                    if((j > k) || (k < 4) || (k - j <= 4)) j = k = 0;
                }
                mInstruments[i].LoopStart = (uint)j;
                mInstruments[i].LoopEnd = (uint)k;
            }

            for(i = 0; i < 32; i++) {
                j = 31;
                while((j >= 0) && (mInstruments[i].name[j] <= ' ')) mInstruments[i].name[j--] = 0;
                while(j >= 0) {
                    if(mInstruments[i].name[j] < ' ') mInstruments[i].name[j] = (byte)' ';
                    j--;
                }
            }

            mFile.Read(bTab, 0, 2);
            k = bTab[0];
            if(mFile.Read(mOrder, 0, 128) != 128) {
                CloseFile(false);
                return;
            }

            nbp = 0;
            for(j = 0; j < 128; j++) {
                i = mOrder[j];
                if((i < 64) && (nbp <= i)) nbp = i + 1;
            }
            j = 0xFF;
            if((k == 0) || (k > 0x7F)) k = 0x7F;
            while((j >= k) && (mOrder[j] == 0)) mOrder[j--] = 0xFF;
            if(ActiveSamples == 31) mFile.Seek(4, SeekOrigin.Current);
            if(nbp == 0) {
                CloseFile(false);
                return;
            }

            // Reading channels
            mPatterns = new byte[64][];
            for(i = 0; i < nbp; i++) {
                mPatterns[i] = new byte[ActiveChannels * 256];
                mFile.Read(mPatterns[i], 0, (int)ActiveChannels * 256);
            }

            // Reading instruments
            for(i = 1; i <= (int)ActiveSamples; i++) if(mInstruments[i].Length != 0) {
                    mInstruments[i].Sample = new byte[mInstruments[i].Length + 1];
                    mFile.Read(mInstruments[i].Sample, 0, (int)mInstruments[i].Length);
                    mInstruments[i].Sample[mInstruments[i].Length] = mInstruments[i].Sample[mInstruments[i].Length - 1];
                }
        }

        private void ParseS3MFile(int offset, S3MTools.S3MFileHeader s3m) {
            int i, j = 0;
            long p;
            S3MTools.S3MSampleHeader smpH;

            mTitle = s3m.Name;

            ActiveSamples = (uint)s3m.smpNum;
            for(i = 0; i < s3m.channels.Length; i++) {
                if(s3m.channels[i] != 0xFF) j++;
            }
            ActiveChannels = (uint)j;

            mInstruments = new ModInstrument[ActiveSamples + 1];
            for(i = 0; i < mInstruments.Length; i++) {
                mInstruments[i].name = new byte[32];
            }

            MusicSpeed = s3m.speed;
            MusicTempo = s3m.tempo;

            mFile.Position = offset;

            mOrder = new byte[s3m.ordNum];
            mFile.Read(mOrder, 0, s3m.ordNum);

            // Skip Sample Header Offsets (for now?)
            UInt16[] sampleHeaderOffsets = new UInt16[s3m.smpNum];
            for(i = 0; i < s3m.smpNum * 2; i += 2) {
                sampleHeaderOffsets[i / 2] = mFile.ReadUInt16();
            }

            UInt16[] patternsOffsets = new UInt16[s3m.patNum];
            for(i = 0; i < s3m.patNum * 2; i += 2) {
                patternsOffsets[i / 2] = mFile.ReadUInt16();
            }

            for(i = 1; i <= (int)ActiveSamples; i++) {
                mFile.Position = sampleHeaderOffsets[i - 1] * 16;
                smpH = SoundFile.LoadStruct<S3MTools.S3MSampleHeader>(mFile);

                Array.Copy(smpH.name, mInstruments[i].name, smpH.name.Length);
                mInstruments[i].Length = smpH.length;

                int note = FrequencyToNote(smpH.c5speed);
                //double f = Math.Pow(2.0, (note - 136) / 12.0) * 8000.0;
                double f = Math.Pow(2.0, (note - 136) / 12.0) * 8372.018;
                mInstruments[i].FineTune = (uint)f;

                mInstruments[i].LoopStart = smpH.loopStart;
                mInstruments[i].LoopEnd = smpH.loopEnd;

                mInstruments[i].Volume = smpH.defaultVolume;
                if(mInstruments[i].Volume > 0x40) mInstruments[i].Volume = 0x40;
                mInstruments[i].Volume <<= 2;

                if(smpH.Magic == "SCRS") {
                    mInstruments[i].Sample = new byte[mInstruments[i].Length];
                    UInt32 sampleOffset = (uint)((smpH.dataPointer[1] << 4) | (smpH.dataPointer[2] << 12) | (smpH.dataPointer[0] << 20));
                    p = mFile.Position;
                    mFile.Seek(sampleOffset, SeekOrigin.Begin);
                    mFile.Read(mInstruments[i].Sample, 0, (int)mInstruments[i].Length);
                    mFile.Position = p;

                    for(j = 0; j < mInstruments[i].Sample.Length; j++) {
                        mInstruments[i].Sample[j] -= 0x80;
                    }
                }
            }

            mPatterns = new byte[s3m.patNum][];
            byte[] pattern = new byte[6];
            for(i = 0; i < s3m.patNum; i++) {
                // Unpack patterns
                List<byte> bl = new List<byte>();
                mFile.Position = patternsOffsets[i] * 16 + 2;
                int row = 0;
                int chn = 0;
                while(row < 64) {
                    mFile.Read(pattern, 0, 1);
                    if(pattern[0] == 0) { // Pad row, to the right, with empty channels
                        for(j = chn; j < ActiveChannels; j++) bl.AddRange(new byte[6]);
                        chn = 0;
                        row++;
                        continue;
                    } else {  // Pad row, to the left, with empty channels
                        int chnIdx = pattern[0] & 0x1F;
                        for(j = chn; j < chnIdx; j++) bl.AddRange(new byte[6]);
                        chn = chnIdx;
                    }

                    if((pattern[0] & 0x20) != 0) mFile.Read(pattern, 1, 2);
                    if((pattern[0] & 0x40) != 0) mFile.Read(pattern, 3, 1);
                    if((pattern[0] & 0x80) != 0) mFile.Read(pattern, 4, 2);
                    bl.AddRange(pattern);
                    chn++;
                }
                mPatterns[i] = bl.ToArray();
            }
        }

        private void ParseXMFile(int offset, XMTools.XMFileHeader xm) {
            int i;

            mTitle = xm.Name;

            ActiveChannels = xm.channels;

            MusicSpeed = xm.speed;
            MusicTempo = xm.tempo;

            mFile.Position = offset;
            mOrder = new byte[xm.orders];
            mFile.Read(mOrder, 0, xm.orders);

            mFile.Position = xm.size + 60;

            if(xm.version >= 0x0104) {
                mPatterns = new byte[xm.patterns][];
                for(i = 0; i < xm.patterns; i++) {
                    UInt32 headerSize = mFile.ReadUInt32();
                    mFile.Position += 1;

                    int numRows;
                    if(xm.version == 0x0102) {
                        numRows = mFile.ReadByte() + 1;
                    } else {
                        numRows = mFile.ReadUInt16();
                    }
                    if(numRows == 0 || numRows > 64) numRows = 64; //FIXME: Apparently, XM files can support patterns with up to 1024 rows

                    UInt16 packedSize = mFile.ReadUInt16();

                    List<byte> bl = new List<byte>();
                    byte[] pattern = new byte[6];

                    long curPos = mFile.Position;
                    while(mFile.Position - curPos < packedSize) {
                        Array.Clear(pattern, 0, pattern.Length);

                        int info = (byte)mFile.ReadByte();
                        if((info & (byte)XMTools.PatternFlags.IsPackByte) != 0) {
                            if((info & (byte)XMTools.PatternFlags.NotePresent) != 0) {
                                pattern[0] = 0x20;
                                pattern[1] = (byte)mFile.ReadByte();
                            }
                        } else {
                            pattern[0] = 0x20;
                            pattern[1] = (byte)info;
                            info = (byte)XMTools.PatternFlags.AllFlags;
                        }

                        if((info & (byte)XMTools.PatternFlags.InstrPresent) != 0) { pattern[2] = (byte)mFile.ReadByte(); }
                        if((info & (byte)XMTools.PatternFlags.VolPresent) != 0) { pattern[0] |= 0x40; pattern[3] = (byte)mFile.ReadByte(); }
                        if((info & (byte)XMTools.PatternFlags.CommandPresent) != 0) { pattern[0] |= 0x80; pattern[4] = (byte)mFile.ReadByte(); }
                        if((info & (byte)XMTools.PatternFlags.ParamPresent) != 0) { pattern[0] |= 0x80; pattern[5] = (byte)mFile.ReadByte(); }

                        //if(mPatterns[i][2] == 0xFF) mPatterns[i][2] = 0;

                        //FIXME Extended Volume Commands are not Implemented
                        if(pattern[3] >= 0x10 && pattern[3] <= 0x50) {
                            pattern[3] -= 0x10;
                        } else if(pattern[3] >= 0x60) {
                            pattern[3] &= 0x0F;
                        }
                        bl.AddRange(pattern);
                    }
                    while(bl.Count < numRows * 6 * xm.channels) bl.AddRange(new byte[6]);
                    mPatterns[i] = bl.ToArray();
                }
            }

            for(i = 1; i <= xm.instruments; i++) {
                UInt32 headerSize = mFile.ReadUInt32();
                if(headerSize == 0) headerSize = (UInt32)Marshal.SizeOf(typeof(XMTools.XMInstrumentHeader));

                mFile.Position -= 4;
                XMTools.XMInstrumentHeader xIH = SoundFile.LoadStruct<XMTools.XMInstrumentHeader>(mFile);

                mInstruments[i] = new ModInstrument();

                Debugger.Break();
            }

            Debugger.Break();
        }

        private void CloseFile(bool isValid) {
            mFile.Close();

            // Default settings	
            Pattern = 0;
            CurrentPattern = 0;
            NextPattern = 0;
            BufferCount = 0;
            SpeedCount = 0;
            Row = 0x3F;
            IsValid = isValid;
        }

        private static T LoadStruct<T>(FileStream fs) {
            byte[] sb = new byte[Marshal.SizeOf(typeof(T))];
            fs.Read(sb, 0, sb.Length);
            GCHandle pb = GCHandle.Alloc(sb, GCHandleType.Pinned);
            var s = (T)Marshal.PtrToStructure(pb.AddrOfPinnedObject(), typeof(T));
            pb.Free();
            return s;
        }
    }
}