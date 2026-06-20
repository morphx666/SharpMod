using System;
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
            file = new FileInfo(fileName).Open(FileMode.Open, FileAccess.Read, FileShare.Read);

            Type = Types.MOD;
            file.Seek(0x438, SeekOrigin.Begin);
            file.Read(s, 0, 4);
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
                            file.Seek(0x2c, SeekOrigin.Begin);
                            file.Read(s, 0, 4);
                            string magic = Encoding.Default.GetString(s).TrimEnd((char)0);
                            file.Seek(0x00, SeekOrigin.Begin);
                            file.Read(s, 0, 17);
                            string xmTag = Encoding.Default.GetString(s).TrimEnd((char)0);
                            if(magic == "SCRM") {
                                Type = Types.S3M;
                                file.Seek(0, SeekOrigin.Begin);
                                s3mFH = LoadStruct<S3MTools.S3MFileHeader>(file);
                            } else if(xmTag == "Extended Module: ") {
                                Type = Types.XM;
                                file.Seek(0, SeekOrigin.Begin);
                                xmFH = LoadStruct<XMTools.XMFileHeader>(file);
                            } else {
                                // STM validation must run before the XXXX-as-S3M fallback because
                                // ST2 stuffs the reserved field with 0x58 (= 'X'), which collides.
                                file.Seek(0, SeekOrigin.Begin);
                                stmFH = LoadStruct<STMTools.STMFileHeader>(file);
                                if(STMTools.IsValidHeader(stmFH)) {
                                    Type = Types.STM;
                                } else if(magic == "XXXX") {
                                    Type = Types.S3M;
                                    file.Seek(0, SeekOrigin.Begin);
                                    s3mFH = LoadStruct<S3MTools.S3MFileHeader>(file);
                                } else {
                                    ActiveSamples = 15;
                                }
                            }
                        }
                    }
                }
            }

            file.Seek(0, SeekOrigin.Begin);
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

            instruments = new ModInstrument[ActiveSamples + 1];
            for(i = 0; i < instruments.Length; i++) {
                instruments[i].name = new byte[32];
            }

            MusicSpeed = 6;
            MusicTempo = 125;

            file.Read(instruments[0].name, 0, offset);
            title = instruments[0].Name;

            for(i = 1; i <= (int)ActiveSamples; i++) {
                file.Read(bTab, 0, 30);
                Array.Copy(bTab, instruments[i].name, 22);

                if((j = (bTab[22] << 9) | (bTab[23] << 1)) < 4) j = 0;
                instruments[i].Length = (uint)j;

                if((j = bTab[24]) > 7) j &= 7; else j = (j & 7) + 8;
                instruments[i].FineTune = FineTuneTable[j];
                instruments[i].Volume = bTab[25];
                if(instruments[i].Volume > 0x40) instruments[i].Volume = 0x40;
                instruments[i].Volume <<= 2;

                if((j = (int)((uint)bTab[26] << 9) | (int)((uint)bTab[27] << 1)) < 4) j = 0;
                if((k = (int)((uint)bTab[28] << 9) | (int)((uint)bTab[29] << 1)) < 4) k = 0;
                if(j + k > (int)instruments[i].Length) {
                    j >>= 1;
                    k = j + ((k + 1) >> 1);
                } else k += j;
                if(instruments[i].Length != 0) {
                    if(j >= (int)instruments[i].Length) j = (int)(instruments[i].Length - 1);
                    if(k > (int)instruments[i].Length) k = (int)instruments[i].Length;
                    if((j > k) || (k < 4) || (k - j <= 4)) j = k = 0;
                }
                instruments[i].LoopStart = (uint)j;
                instruments[i].LoopEnd = (uint)k;
            }

            for(i = 0; i < instruments.Length; i++) {
                j = instruments[i].name.Length - 1;
                while((j >= 0) && (instruments[i].name[j] <= ' ')) instruments[i].name[j--] = 0;
                while(j >= 0) {
                    if(instruments[i].name[j] < ' ') instruments[i].name[j] = (byte)' ';
                    j--;
                }
            }

            file.Read(bTab, 0, 2);
            k = bTab[0];
            if(file.Read(order, 0, 128) != 128) {
                CloseFile(false);
                return;
            }

            nbp = 0;
            for(j = 0; j < 128; j++) {
                i = order[j];
                if((i < 64) && (nbp <= i)) nbp = i + 1;
            }
            j = 0xFF;
            if((k == 0) || (k > 0x7F)) k = 0x7F;
            while((j >= k) && (order[j] == 0)) order[j--] = 0xFF;
            if(ActiveSamples == 31) file.Seek(4, SeekOrigin.Current);
            if(nbp == 0) {
                CloseFile(false);
                return;
            }

            // Reading channels
            patterns = new byte[64][];
            for(i = 0; i < nbp; i++) {
                patterns[i] = new byte[ActiveChannels * 256];
                file.Read(patterns[i], 0, (int)ActiveChannels * 256);
            }

            // Reading instruments
            for(i = 1; i <= (int)ActiveSamples; i++) if(instruments[i].Length != 0) {
                instruments[i].Sample = new byte[instruments[i].Length + 1];
                file.Read(instruments[i].Sample, 0, (int)instruments[i].Length);
                instruments[i].Sample[instruments[i].Length] = instruments[i].Sample[instruments[i].Length - 1];
            }

            // Default Amiga panning (LRRL for 4-channel MODs; odd=left, even=right otherwise)
            for(i = 0; i < ActiveChannels; i++) {
                bool right;
                if(ActiveChannels == 4) right = ((i & 3) == 1) || ((i & 3) == 2);
                else right = (i & 1) == 0;
                channels[i].Pan = (short)(right ? 256 : 0);
            }
        }

        private void ParseS3MFile(int offset, S3MTools.S3MFileHeader s3m) {
            int i, j = 0;
            long p;
            S3MTools.S3MSampleHeader smpH;

            title = s3m.Name;

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

            instruments = new ModInstrument[ActiveSamples + 1];
            for(i = 0; i < instruments.Length; i++) {
                instruments[i].name = new byte[32];
            }

            MusicSpeed = s3m.speed;
            MusicTempo = s3m.tempo;

            file.Position = offset;

            order = new byte[s3m.ordNum];
            file.Read(order, 0, s3m.ordNum);

            UInt16[] sampleHeaderOffsets = new UInt16[s3m.smpNum];
            for(i = 0; i < s3m.smpNum; i++) {
                sampleHeaderOffsets[i] = file.ReadUInt16();
            }

            UInt16[] patternsOffsets = new UInt16[s3m.patNum];
            for(i = 0; i < s3m.patNum; i++) {
                patternsOffsets[i] = file.ReadUInt16();
            }

            // Optional 32-byte extended panning table follows the pattern parapointers
            byte[] panTable = null;
            if(s3m.usePanningTable == (byte)S3MTools.S3MFileHeader.S3MMagic.idPanning) {
                panTable = new byte[32];
                file.Read(panTable, 0, 32);
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
                channels[slot].Pan = pan;
            }

            long fileLen = file.Length;
            int smpHdrSize = Marshal.SizeOf(typeof(S3MTools.S3MSampleHeader));

            for(i = 1; i <= (int)ActiveSamples; i++) {
                long hdrPos = sampleHeaderOffsets[i - 1] * 16L;
                if(hdrPos == 0 || hdrPos + smpHdrSize > fileLen) continue;

                file.Position = hdrPos;
                smpH = LoadStruct<S3MTools.S3MSampleHeader>(file);

                Array.Copy(smpH.name, instruments[i].name, smpH.name.Length);
                instruments[i].Length = smpH.length;

                instruments[i].FineTune = smpH.c5speed;

                // Only honor loopStart/loopEnd when the smpLoop flag is set; ST3 leaves stale loop
                // values in non-looping samples (matches OpenMPT's ConvertToMPT behavior).
                bool hasLoop = (smpH.flags & (byte)S3MTools.S3MSampleHeader.SampleFlags.smpLoop) != 0;
                if(hasLoop && smpH.loopEnd > smpH.loopStart && smpH.loopEnd <= smpH.length) {
                    instruments[i].LoopStart = smpH.loopStart;
                    instruments[i].LoopEnd = smpH.loopEnd;
                }

                instruments[i].Volume = smpH.defaultVolume;
                if(instruments[i].Volume > 0x40) instruments[i].Volume = 0x40;
                instruments[i].Volume <<= 2;

                if(smpH.Magic == "SCRS") {
                    bool is16 = (smpH.flags & (byte)S3MTools.S3MSampleHeader.SampleFlags.smp16Bit) != 0;
                    bool isStereo = (smpH.flags & (byte)S3MTools.S3MSampleHeader.SampleFlags.smpStereo) != 0;
                    instruments[i].Is16Bit = is16;
                    instruments[i].IsStereo = isStereo;
                    int sampleBytes = (int)smpH.length * (is16 ? 2 : 1) * (isStereo ? 2 : 1);
                    if(sampleBytes <= 0) continue;

                    UInt32 sampleOffset = (uint)((smpH.dataPointer[1] << 4) | (smpH.dataPointer[2] << 12) | (smpH.dataPointer[0] << 20));
                    if(sampleOffset == 0 || sampleOffset + (uint)sampleBytes > fileLen) continue;

                    instruments[i].Sample = new byte[sampleBytes];
                    p = file.Position;
                    file.Seek(sampleOffset, SeekOrigin.Begin);
                    file.Read(instruments[i].Sample, 0, sampleBytes);
                    file.Position = p;

                    // Only the "new" format (formatVersion == 2) stores unsigned samples
                    if(s3m.formatVersion == (UInt16)S3MTools.S3MFileHeader.S3MFormatVersion.newVersion) {
                        if(is16) {
                            for(j = 1; j < sampleBytes; j += 2) instruments[i].Sample[j] ^= 0x80;
                        } else {
                            for(j = 0; j < sampleBytes; j++) instruments[i].Sample[j] -= 0x80;
                        }
                    }
                }
            }

            // ST3-saved S3M stores Cxx (Pattern Break) param in BCD; IT-saved keeps it hex
            bool fromIT = (s3m.cwtv & (UInt16)S3MTools.S3MFileHeader.S3MTrackerVersions.trackerMask) == (UInt16)S3MTools.S3MFileHeader.S3MTrackerVersions.trkImpulseTracker;

            patterns = new byte[s3m.patNum][];
            byte[] pattern = new byte[6];
            int rowSize = (int)ActiveChannels * 6;
            for(i = 0; i < s3m.patNum; i++) {
                byte[] rowBuf = new byte[64 * rowSize];
                long patStart = patternsOffsets[i] * 16L;

                if(patStart == 0 || patStart + 2 > fileLen) {
                    patterns[i] = rowBuf;
                    continue;
                }
                file.Position = patStart;
                int packedLen = file.ReadUInt16();
                long dataEnd = Math.Min(file.Position + packedLen, fileLen);

                int row = 0;
                while(row < 64 && file.Position < dataEnd) {
                    int b = file.ReadByte();
                    if(b <= 0) {
                        if(b == 0) row++;
                        continue;
                    }

                    int s3mChn = b & 0x1F;
                    byte flagBits = (byte)(b & 0xE0);
                    Array.Clear(pattern, 0, pattern.Length);

                    if((flagBits & 0x20) != 0 && file.Position + 2 <= dataEnd) file.Read(pattern, 1, 2);
                    if((flagBits & 0x40) != 0 && file.Position + 1 <= dataEnd) file.Read(pattern, 3, 1);
                    if((flagBits & 0x80) != 0 && file.Position + 2 <= dataEnd) file.Read(pattern, 4, 2);

                    int slot = chnMap[s3mChn];
                    if(slot < 0) continue;

                    // Convert ST3 BCD Pattern Break (Cxy) param to a row number (10x + y)
                    if((flagBits & 0x80) != 0 && pattern[4] == 3 && !fromIT) {
                        pattern[5] = (byte)((pattern[5] >> 4) * 10 + (pattern[5] & 0x0F));
                    }

                    pattern[0] = (byte)(flagBits | (slot & 0x1F));
                    Buffer.BlockCopy(pattern, 0, rowBuf, row * rowSize + slot * 6, 6);
                }
                patterns[i] = rowBuf;
            }
        }

        private void ParseXMFile(int offset, XMTools.XMFileHeader xm) {
            int i;

            title = xm.Name;
            trackerName = xm.Tracker;
            RestartPos = xm.restartPos;

            ActiveChannels = xm.channels;
            ActiveSamples = xm.instruments;

            MusicSpeed = xm.speed > 0 ? xm.speed : 6u;
            MusicTempo = xm.tempo > 0 ? xm.tempo : 125u;

            for(i = 0; i < ActiveChannels; i++) channels[i].Pan = 128;

            // Orders: pad to 256 with 0xFF so out-of-range CurrentPattern hits the engine's end-of-song check.
            file.Position = offset;
            order = new byte[256];
            for(int k = 0; k < 256; k++) order[k] = 0xFF;
            int orderCount = Math.Min((int)xm.orders, 256);
            if(orderCount > 0) file.Read(order, 0, orderCount);

            // Clamp RestartPos to a valid order slot - some files store it past the order count.
            if(RestartPos >= orderCount) RestartPos = 0;

            file.Position = xm.size + 60;

            int rowSize = 6 * (int)xm.channels;
            int patternBytes = 64 * rowSize;
            int patternCount = Math.Max((int)xm.patterns, 1);
            patterns = new byte[patternCount][];

            for(i = 0; i < xm.patterns; i++) {
                UInt32 headerSize = file.ReadUInt32();
                long headerStart = file.Position - 4;
                file.Position += 1; // packing type, always 0

                int fileRows;
                if(xm.version == 0x0102) {
                    fileRows = file.ReadByte() + 1;
                } else {
                    fileRows = file.ReadUInt16();
                }
                if(fileRows < 1) fileRows = 1;
                // Engine is locked to 64-row patterns (Row mask is 0x3F); rows beyond that are
                // decoded but silently dropped. We still iterate through the full packed payload
                // so the file position lands exactly at the next pattern/instrument header.
                int storedRows = Math.Min(fileRows, 64);

                UInt16 packedSize = file.ReadUInt16();
                // Always advance to the declared header end before reading data
                file.Position = headerStart + headerSize;

                byte[] patBuf = new byte[patternBytes];

                if(packedSize > 0) {
                    long dataStart = file.Position;
                    long dataEnd = dataStart + packedSize;
                    for(int row = 0; row < fileRows && file.Position < dataEnd; row++) {
                        for(int ch = 0; ch < xm.channels && file.Position < dataEnd; ch++) {
                            int info = file.ReadByte();
                            if(info < 0) break;

                            byte rawNote = 0, rawInst = 0, rawVol = 0, rawCmd = 0, rawParam = 0;
                            bool hasNote, hasInst, hasVol, hasCmd, hasParam;

                            if((info & (byte)XMTools.PatternFlags.IsPackByte) != 0) {
                                hasNote  = (info & (byte)XMTools.PatternFlags.NotePresent)    != 0;
                                hasInst  = (info & (byte)XMTools.PatternFlags.InstrPresent)   != 0;
                                hasVol   = (info & (byte)XMTools.PatternFlags.VolPresent)     != 0;
                                hasCmd   = (info & (byte)XMTools.PatternFlags.CommandPresent) != 0;
                                hasParam = (info & (byte)XMTools.PatternFlags.ParamPresent)   != 0;
                                if(hasNote) rawNote = (byte)file.ReadByte();
                            } else {
                                rawNote = (byte)info;
                                hasNote = true;
                                hasInst = hasVol = hasCmd = hasParam = true;
                            }

                            if(hasInst)  rawInst  = (byte)file.ReadByte();
                            if(hasVol)   rawVol   = (byte)file.ReadByte();
                            if(hasCmd)   rawCmd   = (byte)file.ReadByte();
                            if(hasParam) rawParam = (byte)file.ReadByte();

                            if(row < storedRows) {
                                EncodeXMCell(patBuf, row * rowSize + ch * 6, (byte)ch, rawNote, rawInst, rawVol, rawCmd, rawParam);
                            }
                        }
                    }
                    file.Position = dataEnd;
                }

                patterns[i] = patBuf;
            }
            for(i = (int)xm.patterns; i < patternCount; i++) {
                patterns[i] = new byte[patternBytes];
            }

            // Instruments: 1..N, with mInstruments[0] reserved for the "no instrument" slot.
            instruments = new ModInstrument[xm.instruments + 1];
            for(i = 0; i < instruments.Length; i++) instruments[i].name = new byte[32];

            for(i = 1; i <= xm.instruments; i++) {
                long instStart = file.Position;
                UInt32 instSize = file.ReadUInt32();
                if(instSize < 29) instSize = 29;

                // Read the rest of the instrument header (29 bytes minimum, 263 with sample header info)
                int rawLen = (int)Math.Min(instSize, 263u);
                byte[] instHdr = new byte[rawLen];
                instHdr[0] = (byte)(instSize & 0xFF);
                instHdr[1] = (byte)((instSize >> 8) & 0xFF);
                instHdr[2] = (byte)((instSize >> 16) & 0xFF);
                instHdr[3] = (byte)((instSize >> 24) & 0xFF);
                file.Read(instHdr, 4, rawLen - 4);

                Array.Copy(instHdr, 4, instruments[i].name, 0, Math.Min(22, instruments[i].name.Length));

                int numSamples = rawLen >= 29 ? BitConverter.ToUInt16(instHdr, 27) : 0;
                file.Position = instStart + instSize;

                if(numSamples <= 0) continue;

                XMTools.XMSample[] smpHdrs = new XMTools.XMSample[numSamples];
                for(int s = 0; s < numSamples; s++) smpHdrs[s] = LoadStruct<XMTools.XMSample>(file);

                for(int s = 0; s < numSamples; s++) {
                    int rawBytes = (int)smpHdrs[s].length;
                    if(rawBytes <= 0) continue;

                    // ADPCM (MODPlugin extension, signalled by reserved == 0xAD): block layout is
                    // 16-byte step table + nibble-packed sample data. Skip the block size so
                    // subsequent samples line up; we don't decode it.
                    bool isADPCM = smpHdrs[s].reserved == (byte)XMTools.XMSample.XMSampleFlags.sampleADPCM;
                    int diskBytes = isADPCM ? 16 + ((rawBytes + 1) / 2) : rawBytes;

                    byte[] data = new byte[diskBytes];
                    file.Read(data, 0, diskBytes);

                    if(s != 0 || isADPCM) continue; // basic implementation: keep only the first sample per instrument

                    bool is16 = (smpHdrs[s].flags & (byte)XMTools.XMSample.XMSampleFlags.sample16Bit) != 0;
                    bool isStereo = (smpHdrs[s].flags & (byte)XMTools.XMSample.XMSampleFlags.sampleStereo) != 0;
                    int bytesPerSample = is16 ? 2 : 1;
                    int chanCount = isStereo ? 2 : 1;
                    int frameBytes = bytesPerSample * chanCount;
                    if(frameBytes <= 0) continue;
                    int sampleCount = rawBytes / frameBytes; // frames-per-channel

                    // XM samples are delta-encoded. Stereo XMs store the left channel first, then
                    // the right channel (each independently delta-encoded), which matches the
                    // mixer's "L then R" layout in Read().
                    int blockBytes = sampleCount * bytesPerSample;
                    for(int ch = 0; ch < chanCount; ch++) {
                        int blockStart = ch * blockBytes;
                        if(is16) {
                            short prev = 0;
                            for(int n = 0; n + 1 < blockBytes; n += 2) {
                                int o = blockStart + n;
                                short delta = (short)(data[o] | (data[o + 1] << 8));
                                prev = (short)(prev + delta);
                                data[o]     = (byte)(prev & 0xFF);
                                data[o + 1] = (byte)((prev >> 8) & 0xFF);
                            }
                        } else {
                            sbyte prev = 0;
                            for(int n = 0; n < blockBytes; n++) {
                                prev = (sbyte)(prev + (sbyte)data[blockStart + n]);
                                data[blockStart + n] = (byte)prev;
                            }
                        }
                    }

                    instruments[i].Length = (uint)sampleCount;
                    instruments[i].Sample = data;
                    instruments[i].Is16Bit = is16;
                    instruments[i].IsStereo = isStereo;

                    int vol = smpHdrs[s].vol;
                    if(vol > 0x40) vol = 0x40;
                    instruments[i].Volume = vol << 2;

                    // XM pan is 0..255 with 128 = center. The engine's range is 0..256, so map
                    // 0xFF to the right rail to avoid a 1-unit off-by-one bias.
                    instruments[i].HasDefaultPan = true;
                    instruments[i].DefaultPan = (short)(smpHdrs[s].pan == 0xFF ? 256 : smpHdrs[s].pan);

                    sbyte relnote = (sbyte)smpHdrs[s].relnote;
                    sbyte finetune = (sbyte)smpHdrs[s].finetune;
                    double c5speed = 8363.0 * Math.Pow(2.0, (relnote * 128.0 + finetune) / (12.0 * 128.0));
                    instruments[i].FineTune = (uint)c5speed;

                    bool hasLoop = (smpHdrs[s].flags & ((byte)XMTools.XMSample.XMSampleFlags.sampleLoop
                                                     | (byte)XMTools.XMSample.XMSampleFlags.sampleBidiLoop)) != 0;
                    if(hasLoop && smpHdrs[s].loopLength > 0) {
                        // loopStart/loopLength are in bytes per channel; divide to get frame indices.
                        instruments[i].LoopStart = smpHdrs[s].loopStart / (uint)bytesPerSample;
                        instruments[i].LoopEnd = (smpHdrs[s].loopStart + smpHdrs[s].loopLength) / (uint)bytesPerSample;
                    }
                }
            }
        }

        private void ParseSTMFile(STMTools.STMFileHeader stm) {
            int i;

            title = stm.SongName;

            ActiveChannels = 4;
            ActiveSamples = 31;

            byte initTempo = stm.initTempo;
            if(stm.verMinor < 21) initTempo = (byte)(((initTempo / 10) << 4) + initTempo % 10);
            if(initTempo == 0) initTempo = 0x60;
            MusicSpeed = (uint)(initTempo >> 4);
            MusicTempo = 125; // ST2 has a peculiar tick-length model; use a sensible default BPM

            // ST2 default panning: even channels left, odd channels right
            for(i = 0; i < 4; i++) channels[i].Pan = (short)((i & 1) != 0 ? 64 : 192);

            instruments = new ModInstrument[ActiveSamples + 1];
            for(i = 0; i < instruments.Length; i++) instruments[i].name = new byte[32];

            // 31 sample headers immediately follow the 48-byte file header
            file.Seek(48, SeekOrigin.Begin);
            ushort[] sampleOffsets = new ushort[31];
            for(int idx = 1; idx <= 31; idx++) {
                STMTools.STMSampleHeader sh = LoadStruct<STMTools.STMSampleHeader>(file);

                Array.Copy(sh.filename, instruments[idx].name, Math.Min(sh.filename.Length, instruments[idx].name.Length));

                uint len = sh.length;
                if(len < 2) len = 0;
                instruments[idx].Length = len;

                if(sh.sampleRate > 0) {
                    instruments[idx].FineTune = sh.sampleRate;
                } else {
                    instruments[idx].FineTune = FineTuneTable[8];
                }

                int vol = sh.volume;
                if(vol > 0x40) vol = 0x40;
                instruments[idx].Volume = vol << 2;

                if(sh.loopStart < len && sh.loopEnd > sh.loopStart && sh.loopEnd != 0xFFFF) {
                    instruments[idx].LoopStart = sh.loopStart;
                    instruments[idx].LoopEnd = Math.Min((uint)sh.loopEnd, len);
                }

                sampleOffsets[idx - 1] = sh.offset;
            }

            // Order list: 64 entries on verMinor==0, 128 otherwise. Pad to 256 with 0xFF.
            int orderCount = stm.verMinor == 0 ? 64 : 128;
            order = new byte[256];
            for(int k = 0; k < 256; k++) order[k] = 0xFF;
            byte[] tmpOrder = new byte[orderCount];
            file.Read(tmpOrder, 0, orderCount);
            for(int k = 0; k < orderCount; k++) {
                byte v = tmpOrder[k];
                if(v == 99 || v == 255) order[k] = 0xFF;
                else if(v < 64) order[k] = v;
                // values >63 are invalid; leave as 0xFF (treated as end of song by the engine)
            }

            // Patterns: fixed 64 rows x 4 channels, packed run-length cells
            int numPatterns = stm.numPatterns;
            patterns = new byte[numPatterns][];
            int rowSize = 4 * 6;
            int patternBytes = 64 * rowSize;

            for(int p = 0; p < numPatterns; p++) {
                byte[] patBuf = new byte[patternBytes];
                for(int cellIdx = 0; cellIdx < 64 * 4; cellIdx++) {
                    int row = cellIdx >> 2;
                    int chn = cellIdx & 3;
                    int idx = row * rowSize + chn * 6;

                    int note = file.ReadByte();
                    if(note < 0) break;

                    byte insvol = 0, volcmd = 0, cmdinf = 0;
                    if(note == 0xFC) continue;                              // empty cell, no more bytes
                    if(note == 0xFD) {                                      // note cut, no more bytes
                        patBuf[idx + 0] = (byte)(0x20 | (chn & 0x1F));
                        patBuf[idx + 1] = 0xFE;
                        continue;
                    }
                    if(note != 0xFB) {                                      // 0xFB = zeroed cell, no extra bytes
                        insvol = (byte)file.ReadByte();
                        volcmd = (byte)file.ReadByte();
                        cmdinf = (byte)file.ReadByte();
                    }

                    EncodeSTMCell(patBuf, idx, chn, (byte)note, insvol, volcmd, cmdinf, stm.verMinor);
                }
                patterns[p] = patBuf;
            }

            // Sample data: 8-bit signed mono PCM at offset = sampleOffsets[i] << 4
            long fileLen = file.Length;
            for(int s = 1; s <= 31; s++) {
                if(instruments[s].Length == 0 || instruments[s].Volume == 0) continue;

                long sampleOffset = (long)sampleOffsets[s - 1] << 4;
                if(sampleOffset <= 48 || sampleOffset >= fileLen) continue;
                int sampleBytes = (int)instruments[s].Length;
                if(sampleOffset + sampleBytes > fileLen) sampleBytes = (int)(fileLen - sampleOffset);
                if(sampleBytes <= 0) continue;

                file.Position = sampleOffset;
                instruments[s].Sample = new byte[instruments[s].Length];
                file.Read(instruments[s].Sample, 0, sampleBytes);
                instruments[s].Is16Bit = false;
                instruments[s].IsStereo = false;
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

        private static void EncodeXMCell(byte[] buf, int idx, byte chn, byte rawNote, byte rawInst, byte rawVol, byte rawCmd, byte rawParam) {
            byte mode = 0;
            byte note = 0;
            byte inst = rawInst;
            byte vol = 0;
            byte cmd = 0;
            byte param = rawParam;
            bool noteSet = false;

            if(rawNote >= 1 && rawNote <= 96) {
                int n = rawNote - 1;
                note = (byte)(((n / 12) << 4) | (n % 12));
                mode |= 0x20;
                noteSet = true;
            } else if(rawNote == 97) {
                note = 0xFF; // key off -> note cut in the engine
                mode |= 0x20;
                noteSet = true;
            }

            // Set-volume range (0x10..0x50) maps straight to the engine's vol column slot.
            if(rawVol >= 0x10 && rawVol <= 0x50) {
                vol = (byte)(rawVol - 0x10);
                mode |= 0x40;
            }

            bool cmdSet = false;
            if(rawCmd == 0x0C) {
                // XM Cxx Set Volume -> route into the volume column
                vol = rawParam > 0x40 ? (byte)0x40 : rawParam;
                mode |= 0x40;
            } else if(rawCmd == 0x14) {
                // Kxx - Key Off. The engine has no envelope/fadeout so this is realised as a
                // note-cut event, but only when the cell isn't already triggering a fresh note
                // (otherwise the retrigger would be silenced immediately).
                if(!noteSet) {
                    note = 0xFE;
                    mode |= 0x20;
                }
            } else if(rawCmd == 0x21) {
                // Xxy - Extra Fine Portamento (X1xy up, X2xy down). The engine has no
                // distinct extra-fine slot, so this is folded into the E1x/E2x fine porta
                // path via CMD_RETRIG. Real XM uses a 4x finer granularity; we accept the
                // coarser step rather than dropping the effect entirely.
                byte sub = (byte)((rawParam >> 4) & 0x0F);
                byte sval = (byte)(rawParam & 0x0F);
                if(sub == 1 || sub == 2) {
                    cmd = 'Q' - 'A' + 1;
                    param = (byte)((sub << 4) | sval);
                    mode |= 0x80;
                    cmdSet = true;
                }
            } else if(rawCmd < XMTools.XMEffectTable.Length) {
                byte mapped = XMTools.XMEffectTable[rawCmd];
                // XM effect 0x00 is Arpeggio. A 0/00 cell is "no effect" and must not be
                // tagged as J00, otherwise every otherwise-empty cell would acquire the
                // arpeggio command and consume the engine's single effect slot.
                if(mapped != 0 && !(rawCmd == 0 && rawParam == 0)) {
                    cmd = mapped;
                    mode |= 0x80;
                    cmdSet = true;
                }
            }

            // Extended volume column (0x60..0xFF). XM lets the volume column carry its own
            // mini-effect alongside the effect column; the engine only has a single effect
            // slot per cell, so the translation is only emitted when the effect column is
            // empty. Ranges that have no engine counterpart (panning slides) are skipped.
            if(!cmdSet && rawVol >= 0x60) {
                byte vp = (byte)(rawVol & 0x0F);
                switch(rawVol & 0xF0) {
                    case 0x60: // -x vol slide down (Dxy, down nibble)
                        cmd = 'D' - 'A' + 1;
                        param = vp;
                        mode |= 0x80;
                        break;
                    case 0x70: // +x vol slide up (Dxy, up nibble)
                        cmd = 'D' - 'A' + 1;
                        param = (byte)(vp << 4);
                        mode |= 0x80;
                        break;
                    case 0x80: // Dx fine vol slide down -> EBx
                        cmd = 'Q' - 'A' + 1;
                        param = (byte)(0xB0 | vp);
                        mode |= 0x80;
                        break;
                    case 0x90: // Ux fine vol slide up -> EAx
                        cmd = 'Q' - 'A' + 1;
                        param = (byte)(0xA0 | vp);
                        mode |= 0x80;
                        break;
                    case 0xA0: // Sx set vibrato speed (depth carries over in FT2; approximated here)
                        cmd = 'H' - 'A' + 1;
                        param = (byte)(vp << 4);
                        mode |= 0x80;
                        break;
                    case 0xB0: // Vx vibrato with depth (speed carries over in FT2; approximated here)
                        cmd = 'H' - 'A' + 1;
                        param = vp;
                        mode |= 0x80;
                        break;
                    case 0xC0: // Px set panning. Nibble expands to the 0..255 range; Cx -> right rail.
                        cmd = 'X' - 'A' + 1;
                        param = vp == 0x0F ? (byte)0xFF : (byte)(vp << 4);
                        mode |= 0x80;
                        break;
                    // 0xD0 / 0xE0 (pan slide left/right): no CMD_PANNINGSLIDE handler in the engine,
                    // so dropped to avoid silently misrouting to an unrelated effect.
                    case 0xF0: // Mx tone portamento (speed in upper nibble)
                        cmd = 'G' - 'A' + 1;
                        param = (byte)(vp << 4);
                        mode |= 0x80;
                        break;
                }
            }

            // The engine extracts the destination channel from `mode & 0x1F` when reading
            // S3M/XM cells. Omitting it routes every cell to channel 0, collapsing all
            // channels onto one and silencing whichever cell isn't the last in the row.
            // Empty cells (no flag bits set) are left zeroed so the engine's `mode == 0`
            // skip still applies and per-channel effect memory survives empty rows.
            if(mode == 0) return;
            mode |= (byte)(chn & 0x1F);
            buf[idx + 0] = mode;
            buf[idx + 1] = note;
            buf[idx + 2] = inst;
            buf[idx + 3] = vol;
            buf[idx + 4] = cmd;
            buf[idx + 5] = param;
        }

        private void CloseFile(bool isValid) {
            file.Close();

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