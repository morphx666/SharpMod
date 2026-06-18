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

    Code ported to C# by Xavier Flix (https://github.com/morphx666) on 2019/ 4/25
    S3M (partial) support added by Xavier Flix on 2019/ 4/29
    XM  (partial) support added by Xavier Flix on 2019/ 5/ 4
    STM (basic)   support added by Xavier Flix on 2026/ 6/18
*/

namespace SharpMod {
    public partial class SoundFile {
        public readonly string FileName;

        public SoundFile(string fileName, uint sampleRate, bool is16Bit, bool isStereo, bool loop) {
            byte[] s = new byte[1024];
            S3MTools.S3MFileHeader s3mFH = new S3MTools.S3MFileHeader();
            XMTools.XMFileHeader xmFH = new XMTools.XMFileHeader();
            STMTools.STMFileHeader stmFH = new STMTools.STMFileHeader();

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
                            mFile.Seek(0x00, SeekOrigin.Begin);
                            mFile.Read(s, 0, 17);
                            string xmTag = Encoding.Default.GetString(s).TrimEnd((char)0);
                            if(magic == "SCRM") {
                                Type = Types.S3M;
                                mFile.Seek(0, SeekOrigin.Begin);
                                s3mFH = SoundFile.LoadStruct<S3MTools.S3MFileHeader>(mFile);
                            } else if(xmTag == "Extended Module: ") {
                                Type = Types.XM;
                                mFile.Seek(0, SeekOrigin.Begin);
                                xmFH = SoundFile.LoadStruct<XMTools.XMFileHeader>(mFile);
                            } else {
                                // STM validation must run before the XXXX-as-S3M fallback because
                                // ST2 stuffs the reserved field with 0x58 (= 'X'), which collides.
                                mFile.Seek(0, SeekOrigin.Begin);
                                stmFH = SoundFile.LoadStruct<STMTools.STMFileHeader>(mFile);
                                if(STMTools.IsValidHeader(stmFH)) {
                                    Type = Types.STM;
                                } else if(magic == "XXXX") {
                                    Type = Types.S3M;
                                    mFile.Seek(0, SeekOrigin.Begin);
                                    s3mFH = SoundFile.LoadStruct<S3MTools.S3MFileHeader>(mFile);
                                } else {
                                    ActiveSamples = 15;
                                }
                            }
                        }
                    }
                }
            }

            mFile.Seek(0, SeekOrigin.Begin);
            switch(Type) {
                case Types.MOD: ParseModFile(20); break;
                case Types.S3M: ParseS3MFile(96, s3mFH); break;
                case Types.XM: ParseXMFile(80, xmFH); break;
                case Types.STM: ParseSTMFile(stmFH); break;
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

            for(i = 0; i < mInstruments.Length; i++) {
                j = mInstruments[i].name.Length - 1;
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

            // Default Amiga panning (LRRL for 4-channel MODs; odd=left, even=right otherwise)
            for(i = 0; i < ActiveChannels; i++) {
                bool right;
                if(ActiveChannels == 4) right = ((i & 3) == 1) || ((i & 3) == 2);
                else right = (i & 1) == 0;
                mChannels[i].Pan = (short)(right ? 256 : 0);
            }
        }

        private void ParseS3MFile(int offset, S3MTools.S3MFileHeader s3m) {
            int i, j = 0;
            long p;
            S3MTools.S3MSampleHeader smpH;

            mTitle = s3m.Name;

            ActiveSamples = (uint)s3m.smpNum;

            // Build map: S3M channel index -> active slot (or -1 for AdLib / unused channels)
            sbyte[] chnMap = new sbyte[32];
            for(i = 0; i < 32; i++) {
                byte ch = s3m.channels[i];
                byte chType = (byte)(ch & 0x7F);
                if(ch == 0xFF || chType >= 0x10) {
                    chnMap[i] = -1;
                } else {
                    chnMap[i] = (sbyte)j++;
                }
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

            UInt16[] sampleHeaderOffsets = new UInt16[s3m.smpNum];
            for(i = 0; i < s3m.smpNum; i++) {
                sampleHeaderOffsets[i] = mFile.ReadUInt16();
            }

            UInt16[] patternsOffsets = new UInt16[s3m.patNum];
            for(i = 0; i < s3m.patNum; i++) {
                patternsOffsets[i] = mFile.ReadUInt16();
            }

            // Optional 32-byte extended panning table follows the pattern parapointers
            byte[] panTable = null;
            if(s3m.usePanningTable == (byte)S3MTools.S3MFileHeader.S3MMagic.idPanning) {
                panTable = new byte[32];
                mFile.Read(panTable, 0, 32);
            }

            // Default per-channel pan from S3M header (low nibble of channels[i]) and pan table override.
            // If master volume's high bit is clear the file is mono and all channels center.
            bool stereoOut = (s3m.masterVolume & 0x80) != 0;
            for(i = 0; i < 32; i++) {
                sbyte slot = chnMap[i];
                if(slot < 0) continue;
                short pan;
                if(!stereoOut) {
                    pan = 128;
                } else if(panTable != null && (panTable[i] & 0x20) != 0) {
                    pan = (short)((panTable[i] & 0x0F) * 17);
                } else {
                    byte chType = (byte)(s3m.channels[i] & 0x7F);
                    pan = (short)(chType < 8 ? 64 : 192);
                }
                mChannels[slot].Pan = pan;
            }

            long fileLen = mFile.Length;
            int smpHdrSize = Marshal.SizeOf(typeof(S3MTools.S3MSampleHeader));

            for(i = 1; i <= (int)ActiveSamples; i++) {
                long hdrPos = sampleHeaderOffsets[i - 1] * 16L;
                if(hdrPos == 0 || hdrPos + smpHdrSize > fileLen) continue;

                mFile.Position = hdrPos;
                smpH = SoundFile.LoadStruct<S3MTools.S3MSampleHeader>(mFile);

                Array.Copy(smpH.name, mInstruments[i].name, smpH.name.Length);
                mInstruments[i].Length = smpH.length;

                int note = FrequencyToNote(smpH.c5speed);
                //double f = Math.Pow(2.0, (note - 136) / 12.0) * 8000.0;
                //double f = Math.Pow(2.0, (note - 136) / 12.0) * 8372.018;
                double f = Math.Pow(2.0, (note - 136) / 12.0) * 8169;
                mInstruments[i].FineTune = (uint)f;

                // Only honor loopStart/loopEnd when the smpLoop flag is set; ST3 leaves stale loop
                // values in non-looping samples (matches OpenMPT's ConvertToMPT behavior).
                bool hasLoop = (smpH.flags & (byte)S3MTools.S3MSampleHeader.SampleFlags.smpLoop) != 0;
                if(hasLoop && smpH.loopEnd > smpH.loopStart && smpH.loopEnd <= smpH.length) {
                    mInstruments[i].LoopStart = smpH.loopStart;
                    mInstruments[i].LoopEnd = smpH.loopEnd;
                }

                mInstruments[i].Volume = smpH.defaultVolume;
                if(mInstruments[i].Volume > 0x40) mInstruments[i].Volume = 0x40;
                mInstruments[i].Volume <<= 2;

                if(smpH.Magic == "SCRS") {
                    bool is16 = (smpH.flags & (byte)S3MTools.S3MSampleHeader.SampleFlags.smp16Bit) != 0;
                    bool isStereo = (smpH.flags & (byte)S3MTools.S3MSampleHeader.SampleFlags.smpStereo) != 0;
                    mInstruments[i].Is16Bit = is16;
                    mInstruments[i].IsStereo = isStereo;
                    int sampleBytes = (int)smpH.length * (is16 ? 2 : 1) * (isStereo ? 2 : 1);
                    if(sampleBytes <= 0) continue;

                    UInt32 sampleOffset = (uint)((smpH.dataPointer[1] << 4) | (smpH.dataPointer[2] << 12) | (smpH.dataPointer[0] << 20));
                    if(sampleOffset == 0 || sampleOffset + (uint)sampleBytes > fileLen) continue;

                    mInstruments[i].Sample = new byte[sampleBytes];
                    p = mFile.Position;
                    mFile.Seek(sampleOffset, SeekOrigin.Begin);
                    mFile.Read(mInstruments[i].Sample, 0, sampleBytes);
                    mFile.Position = p;

                    // Only the "new" format (formatVersion == 2) stores unsigned samples
                    if(s3m.formatVersion == (UInt16)S3MTools.S3MFileHeader.S3MFormatVersion.newVersion) {
                        if(is16) {
                            for(j = 1; j < sampleBytes; j += 2) mInstruments[i].Sample[j] ^= 0x80;
                        } else {
                            for(j = 0; j < sampleBytes; j++) mInstruments[i].Sample[j] -= 0x80;
                        }
                    }
                }
            }

            // ST3-saved S3M stores Cxx (Pattern Break) param in BCD; IT-saved keeps it hex
            bool fromIT = (s3m.cwtv & (UInt16)S3MTools.S3MFileHeader.S3MTrackerVersions.trackerMask) == (UInt16)S3MTools.S3MFileHeader.S3MTrackerVersions.trkImpulseTracker;

            mPatterns = new byte[s3m.patNum][];
            byte[] pattern = new byte[6];
            int rowSize = (int)ActiveChannels * 6;
            for(i = 0; i < s3m.patNum; i++) {
                byte[] rowBuf = new byte[64 * rowSize];
                long patStart = patternsOffsets[i] * 16L;

                if(patStart == 0 || patStart + 2 > fileLen) {
                    mPatterns[i] = rowBuf;
                    continue;
                }
                mFile.Position = patStart;
                int packedLen = mFile.ReadUInt16();
                long dataEnd = Math.Min(mFile.Position + packedLen, fileLen);

                int row = 0;
                while(row < 64 && mFile.Position < dataEnd) {
                    int b = mFile.ReadByte();
                    if(b <= 0) {
                        if(b == 0) row++;
                        continue;
                    }

                    int s3mChn = b & 0x1F;
                    byte flagBits = (byte)(b & 0xE0);
                    Array.Clear(pattern, 0, pattern.Length);

                    if((flagBits & 0x20) != 0 && mFile.Position + 2 <= dataEnd) mFile.Read(pattern, 1, 2);
                    if((flagBits & 0x40) != 0 && mFile.Position + 1 <= dataEnd) mFile.Read(pattern, 3, 1);
                    if((flagBits & 0x80) != 0 && mFile.Position + 2 <= dataEnd) mFile.Read(pattern, 4, 2);

                    int slot = chnMap[s3mChn];
                    if(slot < 0) continue;

                    // Convert ST3 BCD Pattern Break (Cxy) param to a row number (10x + y)
                    if((flagBits & 0x80) != 0 && pattern[4] == 3 && !fromIT) {
                        pattern[5] = (byte)((pattern[5] >> 4) * 10 + (pattern[5] & 0x0F));
                    }

                    pattern[0] = (byte)(flagBits | (slot & 0x1F));
                    Buffer.BlockCopy(pattern, 0, rowBuf, row * rowSize + slot * 6, 6);
                }
                mPatterns[i] = rowBuf;
            }
        }

        private void ParseXMFile(int offset, XMTools.XMFileHeader xm) {
            int i;

            mTitle = xm.Name;

            ActiveChannels = xm.channels;
            ActiveSamples = xm.instruments;

            MusicSpeed = xm.speed > 0 ? xm.speed : 6u;
            MusicTempo = xm.tempo > 0 ? xm.tempo : 125u;

            for(i = 0; i < ActiveChannels; i++) mChannels[i].Pan = 128;

            // Orders: pad to 256 with 0xFF so out-of-range CurrentPattern hits the engine's end-of-song check.
            mFile.Position = offset;
            mOrder = new byte[256];
            for(int k = 0; k < 256; k++) mOrder[k] = 0xFF;
            int orderCount = Math.Min((int)xm.orders, 256);
            if(orderCount > 0) mFile.Read(mOrder, 0, orderCount);

            mFile.Position = xm.size + 60;

            int rowSize = 6 * (int)xm.channels;
            int patternBytes = 64 * rowSize;
            int patternCount = Math.Max((int)xm.patterns, 1);
            mPatterns = new byte[patternCount][];

            for(i = 0; i < xm.patterns; i++) {
                UInt32 headerSize = mFile.ReadUInt32();
                long headerStart = mFile.Position - 4;
                mFile.Position += 1; // packing type, always 0

                int numRows;
                if(xm.version == 0x0102) {
                    numRows = mFile.ReadByte() + 1;
                } else {
                    numRows = mFile.ReadUInt16();
                }
                if(numRows < 1) numRows = 1;
                if(numRows > 64) numRows = 64; // FIXME: basic implementation only, XM can have up to 256 rows

                UInt16 packedSize = mFile.ReadUInt16();
                // Always advance to the declared header end before reading data
                mFile.Position = headerStart + headerSize;

                byte[] patBuf = new byte[patternBytes];

                if(packedSize > 0) {
                    long dataStart = mFile.Position;
                    long dataEnd = dataStart + packedSize;
                    for(int row = 0; row < numRows && mFile.Position < dataEnd; row++) {
                        for(int ch = 0; ch < xm.channels && mFile.Position < dataEnd; ch++) {
                            int info = mFile.ReadByte();
                            if(info < 0) break;

                            byte rawNote = 0, rawInst = 0, rawVol = 0, rawCmd = 0, rawParam = 0;
                            bool hasNote, hasInst, hasVol, hasCmd, hasParam;

                            if((info & (byte)XMTools.PatternFlags.IsPackByte) != 0) {
                                hasNote  = (info & (byte)XMTools.PatternFlags.NotePresent)    != 0;
                                hasInst  = (info & (byte)XMTools.PatternFlags.InstrPresent)   != 0;
                                hasVol   = (info & (byte)XMTools.PatternFlags.VolPresent)     != 0;
                                hasCmd   = (info & (byte)XMTools.PatternFlags.CommandPresent) != 0;
                                hasParam = (info & (byte)XMTools.PatternFlags.ParamPresent)   != 0;
                                if(hasNote) rawNote = (byte)mFile.ReadByte();
                            } else {
                                rawNote = (byte)info;
                                hasNote = true;
                                hasInst = hasVol = hasCmd = hasParam = true;
                            }

                            if(hasInst)  rawInst  = (byte)mFile.ReadByte();
                            if(hasVol)   rawVol   = (byte)mFile.ReadByte();
                            if(hasCmd)   rawCmd   = (byte)mFile.ReadByte();
                            if(hasParam) rawParam = (byte)mFile.ReadByte();

                            EncodeXMCell(patBuf, row * rowSize + ch * 6, rawNote, rawInst, rawVol, rawCmd, rawParam);
                        }
                    }
                    mFile.Position = dataEnd;
                }

                mPatterns[i] = patBuf;
            }
            for(i = (int)xm.patterns; i < patternCount; i++) {
                mPatterns[i] = new byte[patternBytes];
            }

            // Instruments: 1..N, with mInstruments[0] reserved for the "no instrument" slot.
            mInstruments = new ModInstrument[xm.instruments + 1];
            for(i = 0; i < mInstruments.Length; i++) mInstruments[i].name = new byte[32];

            for(i = 1; i <= xm.instruments; i++) {
                long instStart = mFile.Position;
                UInt32 instSize = mFile.ReadUInt32();
                if(instSize < 29) instSize = 29;

                // Read the rest of the instrument header (29 bytes minimum, 263 with sample header info)
                int rawLen = (int)Math.Min(instSize, 263u);
                byte[] instHdr = new byte[rawLen];
                instHdr[0] = (byte)(instSize & 0xFF);
                instHdr[1] = (byte)((instSize >> 8) & 0xFF);
                instHdr[2] = (byte)((instSize >> 16) & 0xFF);
                instHdr[3] = (byte)((instSize >> 24) & 0xFF);
                mFile.Read(instHdr, 4, rawLen - 4);

                Array.Copy(instHdr, 4, mInstruments[i].name, 0, Math.Min(22, mInstruments[i].name.Length));

                int numSamples = rawLen >= 29 ? BitConverter.ToUInt16(instHdr, 27) : 0;
                mFile.Position = instStart + instSize;

                if(numSamples <= 0) continue;

                XMTools.XMSample[] smpHdrs = new XMTools.XMSample[numSamples];
                for(int s = 0; s < numSamples; s++) smpHdrs[s] = SoundFile.LoadStruct<XMTools.XMSample>(mFile);

                for(int s = 0; s < numSamples; s++) {
                    int sampleBytes = (int)smpHdrs[s].length;
                    if(sampleBytes <= 0) continue;

                    byte[] data = new byte[sampleBytes];
                    mFile.Read(data, 0, sampleBytes);

                    if(s != 0) continue; // basic implementation: keep only the first sample per instrument

                    bool is16 = (smpHdrs[s].flags & (byte)XMTools.XMSample.XMSampleFlags.sample16Bit) != 0;
                    int bytesPerSample = is16 ? 2 : 1;
                    int sampleCount = sampleBytes / bytesPerSample;

                    // XM samples are delta-encoded
                    if(is16) {
                        short prev = 0;
                        for(int n = 0; n + 1 < sampleBytes; n += 2) {
                            short delta = (short)(data[n] | (data[n + 1] << 8));
                            prev = (short)(prev + delta);
                            data[n]     = (byte)(prev & 0xFF);
                            data[n + 1] = (byte)((prev >> 8) & 0xFF);
                        }
                    } else {
                        sbyte prev = 0;
                        for(int n = 0; n < sampleBytes; n++) {
                            prev = (sbyte)(prev + (sbyte)data[n]);
                            data[n] = (byte)prev;
                        }
                    }

                    mInstruments[i].Length = (uint)sampleCount;
                    mInstruments[i].Sample = data;
                    mInstruments[i].Is16Bit = is16;
                    mInstruments[i].IsStereo = false;

                    int vol = smpHdrs[s].vol;
                    if(vol > 0x40) vol = 0x40;
                    mInstruments[i].Volume = vol << 2;

                    sbyte relnote = (sbyte)smpHdrs[s].relnote;
                    sbyte finetune = (sbyte)smpHdrs[s].finetune;
                    double c5speed = 8363.0 * Math.Pow(2.0, (relnote * 128.0 + finetune) / (12.0 * 128.0));
                    int note = FrequencyToNote(c5speed);
                    double f = Math.Pow(2.0, (note - 136) / 12.0) * 8169;
                    mInstruments[i].FineTune = (uint)f;

                    bool hasLoop = (smpHdrs[s].flags & ((byte)XMTools.XMSample.XMSampleFlags.sampleLoop
                                                     | (byte)XMTools.XMSample.XMSampleFlags.sampleBidiLoop)) != 0;
                    if(hasLoop && smpHdrs[s].loopLength > 0) {
                        mInstruments[i].LoopStart = smpHdrs[s].loopStart / (uint)bytesPerSample;
                        mInstruments[i].LoopEnd = (smpHdrs[s].loopStart + smpHdrs[s].loopLength) / (uint)bytesPerSample;
                    }
                }
            }
        }

        private void ParseSTMFile(STMTools.STMFileHeader stm) {
            int i;

            mTitle = stm.SongName;

            ActiveChannels = 4;
            ActiveSamples = 31;

            byte initTempo = stm.initTempo;
            if(stm.verMinor < 21) initTempo = (byte)(((initTempo / 10) << 4) + initTempo % 10);
            if(initTempo == 0) initTempo = 0x60;
            MusicSpeed = (uint)(initTempo >> 4);
            MusicTempo = 125; // ST2 has a peculiar tick-length model; use a sensible default BPM

            // ST2 default panning: even channels left, odd channels right
            for(i = 0; i < 4; i++) mChannels[i].Pan = (short)((i & 1) != 0 ? 64 : 192);

            mInstruments = new ModInstrument[ActiveSamples + 1];
            for(i = 0; i < mInstruments.Length; i++) mInstruments[i].name = new byte[32];

            // 31 sample headers immediately follow the 48-byte file header
            mFile.Seek(48, SeekOrigin.Begin);
            ushort[] sampleOffsets = new ushort[31];
            for(int idx = 1; idx <= 31; idx++) {
                STMTools.STMSampleHeader sh = SoundFile.LoadStruct<STMTools.STMSampleHeader>(mFile);

                Array.Copy(sh.filename, mInstruments[idx].name, Math.Min(sh.filename.Length, mInstruments[idx].name.Length));

                uint len = sh.length;
                if(len < 2) len = 0;
                mInstruments[idx].Length = len;

                if(sh.sampleRate > 0) {
                    int note = FrequencyToNote(sh.sampleRate);
                    double f = Math.Pow(2.0, (note - 136) / 12.0) * 8169;
                    mInstruments[idx].FineTune = (uint)f;
                } else {
                    mInstruments[idx].FineTune = FineTuneTable[8];
                }

                int vol = sh.volume;
                if(vol > 0x40) vol = 0x40;
                mInstruments[idx].Volume = vol << 2;

                if(sh.loopStart < len && sh.loopEnd > sh.loopStart && sh.loopEnd != 0xFFFF) {
                    mInstruments[idx].LoopStart = sh.loopStart;
                    mInstruments[idx].LoopEnd = Math.Min((uint)sh.loopEnd, len);
                }

                sampleOffsets[idx - 1] = sh.offset;
            }

            // Order list: 64 entries on verMinor==0, 128 otherwise. Pad to 256 with 0xFF.
            int orderCount = stm.verMinor == 0 ? 64 : 128;
            mOrder = new byte[256];
            for(int k = 0; k < 256; k++) mOrder[k] = 0xFF;
            byte[] tmpOrder = new byte[orderCount];
            mFile.Read(tmpOrder, 0, orderCount);
            for(int k = 0; k < orderCount; k++) {
                byte v = tmpOrder[k];
                if(v == 99 || v == 255) mOrder[k] = 0xFF;
                else if(v < 64) mOrder[k] = v;
                // values >63 are invalid; leave as 0xFF (treated as end of song by the engine)
            }

            // Patterns: fixed 64 rows x 4 channels, packed run-length cells
            int numPatterns = stm.numPatterns;
            mPatterns = new byte[numPatterns][];
            int rowSize = 4 * 6;
            int patternBytes = 64 * rowSize;

            for(int p = 0; p < numPatterns; p++) {
                byte[] patBuf = new byte[patternBytes];
                for(int cellIdx = 0; cellIdx < 64 * 4; cellIdx++) {
                    int row = cellIdx >> 2;
                    int chn = cellIdx & 3;
                    int idx = row * rowSize + chn * 6;

                    int note = mFile.ReadByte();
                    if(note < 0) break;

                    byte insvol = 0, volcmd = 0, cmdinf = 0;
                    if(note == 0xFC) continue;                              // empty cell, no more bytes
                    if(note == 0xFD) {                                      // note cut, no more bytes
                        patBuf[idx + 0] = (byte)(0x20 | (chn & 0x1F));
                        patBuf[idx + 1] = 0xFE;
                        continue;
                    }
                    if(note != 0xFB) {                                      // 0xFB = zeroed cell, no extra bytes
                        insvol = (byte)mFile.ReadByte();
                        volcmd = (byte)mFile.ReadByte();
                        cmdinf = (byte)mFile.ReadByte();
                    }

                    EncodeSTMCell(patBuf, idx, chn, (byte)note, insvol, volcmd, cmdinf, stm.verMinor);
                }
                mPatterns[p] = patBuf;
            }

            // Sample data: 8-bit signed mono PCM at offset = sampleOffsets[i] << 4
            long fileLen = mFile.Length;
            for(int s = 1; s <= 31; s++) {
                if(mInstruments[s].Length == 0 || mInstruments[s].Volume == 0) continue;

                long sampleOffset = (long)sampleOffsets[s - 1] << 4;
                if(sampleOffset <= 48 || sampleOffset >= fileLen) continue;
                int sampleBytes = (int)mInstruments[s].Length;
                if(sampleOffset + sampleBytes > fileLen) sampleBytes = (int)(fileLen - sampleOffset);
                if(sampleBytes <= 0) continue;

                mFile.Position = sampleOffset;
                mInstruments[s].Sample = new byte[mInstruments[s].Length];
                mFile.Read(mInstruments[s].Sample, 0, sampleBytes);
                mInstruments[s].Is16Bit = false;
                mInstruments[s].IsStereo = false;
            }
        }

        private static void EncodeSTMCell(byte[] buf, int idx, int chn, byte note, byte insvol, byte volcmd, byte cmdinf, byte verMinor) {
            byte mode = 0;
            byte encNote = 0, instr = 0, vol = 0, cmd = 0, param = 0;

            if(note == 0xFE) {
                encNote = 0xFE; // note cut
                mode |= 0x20;
            } else if(note < 0x60) {
                // STM note byte: (octave << 4) | semitone, with octave 0 = C-2 (MIDI 36).
                // Engine note byte: (octave << 4) | semitone, with octave 0 = C-1 (MIDI 12).
                // Adding 0x20 (two octaves) maps STM C-2 -> engine C-3, preserving pitch.
                encNote = (byte)(note + 0x20);
                mode |= 0x20;
            }

            int rawInst = insvol >> 3;
            if(rawInst > 31) rawInst = 0;
            instr = (byte)rawInst;

            int rawVol = (insvol & 0x07) | ((volcmd & 0xF0) >> 1);
            if(rawVol <= 64) {
                vol = (byte)rawVol;
                mode |= 0x40;
            }

            // STM effects 1..10 map directly to S3M letters A..J; 11..15 are no-ops in ST2.
            int effIdx = volcmd & 0x0F;
            byte effParam = cmdinf;
            if(effIdx >= 1 && effIdx <= 10) {
                bool keep = true;
                switch(effIdx) {
                    case 1: // A - Set Speed: BCD on old versions, then take high nibble
                        if(verMinor < 21) effParam = (byte)(((effParam / 10) << 4) + effParam % 10);
                        effParam = (byte)(effParam >> 4);
                        if(effParam == 0) keep = false;
                        break;
                    case 3: // C - Pattern Break: BCD -> decimal row number
                        effParam = (byte)(((effParam >> 4) * 10) + (effParam & 0x0F));
                        break;
                    case 4: // D - Volume Slide: lower nibble has precedence, no fine slides
                        if((effParam & 0x0F) != 0) effParam &= 0x0F;
                        else effParam &= 0xF0;
                        if(effParam == 0) keep = false;
                        break;
                    default:
                        if(effParam == 0) keep = false; // ST2 has no effect memory
                        break;
                }
                if(keep) {
                    cmd = (byte)effIdx; // engine stores effect as 1..26 (A..Z)
                    param = effParam;
                    mode |= 0x80;
                }
            }

            if(mode == 0) return;

            mode |= (byte)(chn & 0x1F);
            buf[idx + 0] = mode;
            buf[idx + 1] = encNote;
            buf[idx + 2] = instr;
            buf[idx + 3] = vol;
            buf[idx + 4] = cmd;
            buf[idx + 5] = param;
        }

        private static void EncodeXMCell(byte[] buf, int idx, byte rawNote, byte rawInst, byte rawVol, byte rawCmd, byte rawParam) {
            byte mode = 0;
            byte note = 0;
            byte inst = rawInst;
            byte vol = 0;
            byte cmd = 0;
            byte param = rawParam;

            if(rawNote >= 1 && rawNote <= 96) {
                int n = rawNote - 1;
                note = (byte)(((n / 12) << 4) | (n % 12));
                mode |= 0x20;
            } else if(rawNote == 97) {
                note = 0xFF; // key off -> note cut in the engine
                mode |= 0x20;
            }

            // Volume column: only the set-volume range (0x10..0x50) is supported in the basic implementation
            if(rawVol >= 0x10 && rawVol <= 0x50) {
                vol = (byte)(rawVol - 0x10);
                mode |= 0x40;
            }

            if(rawCmd == 0x0C) {
                // XM Cxx Set Volume -> route into the volume column
                vol = rawParam > 0x40 ? (byte)0x40 : rawParam;
                mode |= 0x40;
            } else if(rawCmd < XMTools.XMEffectTable.Length) {
                byte mapped = XMTools.XMEffectTable[rawCmd];
                if(mapped != 0) {
                    cmd = mapped;
                    mode |= 0x80;
                }
            }

            buf[idx + 0] = mode;
            buf[idx + 1] = note;
            buf[idx + 2] = inst;
            buf[idx + 3] = vol;
            buf[idx + 4] = cmd;
            buf[idx + 5] = param;
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