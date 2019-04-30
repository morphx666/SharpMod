using System;
using System.IO;
using System.Text;

/*
    This is a verbatim implementation of the magnificent code developed by Olivier Lapicque for his Mod95 player.

    For more information, visit https://openmpt.org/legacy_software

    Code ported to c# by Xavier Flix (https://github.com/morphx666) on 2019/ 4/25
    S3M (partial) support added by Xavier Flix on 2019/ 4/29
*/

namespace SharpMod {
    public partial class SoundFile {
        public SoundFile(string fileName, uint sampleRate, bool is16Bit, bool isStereo, bool loop) {
            byte[] s = new byte[1024];
            int i;
            S3MTools.S3MFileHeader s3mFH = new S3MTools.S3MFileHeader();
            bool isS3M = false;

            Type = 0;
            Rate = sampleRate;
            Is16Bit = is16Bit;
            IsStereo = isStereo;
            Loop = loop;
            ActiveChannels = 0;

            mFile = new FileInfo(fileName).Open(FileMode.Open, FileAccess.Read, FileShare.Read);

            Type = 1;
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
                            if(Encoding.Default.GetString(s).TrimEnd((char)0) == "SCRM") {
                                isS3M = true;
                                mFile.Seek(0, SeekOrigin.Begin);
                                s3mFH = S3MTools.LoadStruct<S3MTools.S3MFileHeader>(mFile);

                                ActiveSamples = (uint)s3mFH.smpNum;
                                int j = 0;
                                for(i = 0; i < s3mFH.channels.Length; i++) {
                                    if(s3mFH.channels[i] != 0xFF) j++;
                                }
                                ActiveChannels = (uint)j;
                            } else {
                                ActiveSamples = 15;
                            }
                        }
                    }
                }
            }

            Instruments = new ModInstrument[ActiveSamples + 1];
            for(i = 0; i < Instruments.Length; i++) {
                Instruments[i].name = new byte[32];
            }

            mFile.Seek(0, SeekOrigin.Begin);
            if(isS3M) {
                ParseS3MFile(96, s3mFH);
                Type = 3;
            } else {
                ParseModFile(20);
                Type = 2;
            }
            CloseFile(true);
        }

        private void ParseModFile(int offset) {
            int i, j, k, nbp;
            byte[] bTab = new byte[32];

            MusicSpeed = 6;
            MusicTempo = 125;

            mFile.Read(Instruments[0].name, 0, offset);

            for(i = 1; i <= (int)ActiveSamples; i++) {
                mFile.Read(bTab, 0, 30);
                Array.Copy(bTab, Instruments[i].name, 22);

                if((j = (bTab[22] << 9) | (bTab[23] << 1)) < 4) j = 0;
                Instruments[i].Length = (uint)j;

                if((j = bTab[24]) > 7) j &= 7; else j = (j & 7) + 8;
                Instruments[i].FineTune = FineTuneTable[j];
                Instruments[i].Volume = bTab[25];
                if(Instruments[i].Volume > 0x40) Instruments[i].Volume = 0x40;
                Instruments[i].Volume <<= 2;

                if((j = (int)((uint)bTab[26] << 9) | (int)((uint)bTab[27] << 1)) < 4) j = 0;
                if((k = (int)((uint)bTab[28] << 9) | (int)((uint)bTab[29] << 1)) < 4) k = 0;
                if(j + k > (int)Instruments[i].Length) {
                    j >>= 1;
                    k = j + ((k + 1) >> 1);
                } else k += j;
                if(Instruments[i].Length != 0) {
                    if(j >= (int)Instruments[i].Length) j = (int)(Instruments[i].Length - 1);
                    if(k > (int)Instruments[i].Length) k = (int)Instruments[i].Length;
                    if((j > k) || (k < 4) || (k - j <= 4)) j = k = 0;
                }
                Instruments[i].LoopStart = (uint)j;
                Instruments[i].LoopEnd = (uint)k;
            }

            for(i = 0; i < 32; i++) {
                j = 31;
                while((j >= 0) && (Instruments[i].name[j] <= ' ')) Instruments[i].name[j--] = 0;
                while(j >= 0) {
                    if(Instruments[i].name[j] < ' ') Instruments[i].name[j] = (byte)' ';
                    j--;
                }
            }

            mFile.Read(bTab, 0, 2);
            k = bTab[0];
            if(mFile.Read(Order, 0, 128) != 128) {
                CloseFile(false);
                return;
            }

            nbp = 0;
            for(j = 0; j < 128; j++) {
                i = Order[j];
                if((i < 64) && (nbp <= i)) nbp = i + 1;
            }
            j = 0xFF;
            if((k == 0) || (k > 0x7F)) k = 0x7F;
            while((j >= k) && (Order[j] == 0)) Order[j--] = 0xFF;
            if(ActiveSamples == 31) mFile.Seek(4, SeekOrigin.Current);
            if(nbp == 0) {
                CloseFile(false);
                return;
            }

            // Reading channels
            Patterns = new byte[64][];
            for(i = 0; i < nbp; i++) {
                Patterns[i] = new byte[ActiveChannels * 256];
                mFile.Read(Patterns[i], 0, (int)ActiveChannels * 256);
            }

            // Reading instruments
            for(i = 1; i <= (int)ActiveSamples; i++) if(Instruments[i].Length != 0) {
                    Instruments[i].Sample = new byte[Instruments[i].Length + 1];
                    mFile.Read(Instruments[i].Sample, 0, (int)Instruments[i].Length);
                    Instruments[i].Sample[Instruments[i].Length] = Instruments[i].Sample[Instruments[i].Length - 1];
                }
        }

        private void ParseS3MFile(int offset, S3MTools.S3MFileHeader s3m) {
            int i, j;
            long p;
            byte[] tmp = new byte[2];
            S3MTools.S3MSampleHeader smpH;

            MusicSpeed = s3m.speed;
            MusicTempo = s3m.tempo;

            mFile.Seek(offset, SeekOrigin.Begin);

            Order = new byte[s3m.ordNum];
            mFile.Read(Order, 0, s3m.ordNum);

            // Skip Sample Header Offsets (for now?)
            UInt16[] sampleHeaderOffsets = new UInt16[s3m.smpNum];
            for(i = 0; i < s3m.smpNum * 2; i += 2) {
                mFile.Read(tmp, 0, 2);
                sampleHeaderOffsets[i / 2] = (UInt16)(BitConverter.ToUInt16(tmp, 0));
            }

            UInt16[] patternsOffsets = new UInt16[s3m.patNum];
            for(i = 0; i < s3m.patNum * 2; i += 2) {
                mFile.Read(tmp, 0, 2);
                patternsOffsets[i / 2] = BitConverter.ToUInt16(tmp, 0);
            }

            for(i = 1; i <= (int)ActiveSamples; i++) {
                mFile.Position = sampleHeaderOffsets[i - 1] * 16;
                smpH = S3MTools.LoadStruct<S3MTools.S3MSampleHeader>(mFile);

                Array.Copy(smpH.name, Instruments[i].name, smpH.name.Length);
                Instruments[i].Length = smpH.length;

                int note = FrequencyToNote(smpH.c5speed);
                double f = Math.Pow(2.0, (note - 136) / 12.0) * 8000.0;
                Instruments[i].FineTune = (uint)f;

                Instruments[i].LoopStart = smpH.loopStart;
                Instruments[i].LoopEnd = smpH.loopEnd;

                Instruments[i].Volume = smpH.defaultVolume;
                if(Instruments[i].Volume > 0x40) Instruments[i].Volume = 0x40;
                Instruments[i].Volume <<= 2;

                if(smpH.Magic == "SCRS") {
                    Instruments[i].Sample = new byte[Instruments[i].Length];
                    UInt32 sampleOffset = (uint)((smpH.dataPointer[1] << 4) | (smpH.dataPointer[2] << 12) | (smpH.dataPointer[0] << 20));
                    p = mFile.Position;
                    mFile.Seek(sampleOffset, SeekOrigin.Begin);
                    mFile.Read(Instruments[i].Sample, 0, (int)Instruments[i].Length);
                    mFile.Position = p;

                    for(j = 0; j < Instruments[i].Sample.Length; j++) {
                        Instruments[i].Sample[j] -= 0x80;
                    }
                }
            }

            Patterns = new byte[s3m.patNum][];
            byte[] pattern = new byte[6];
            for(i = 0; i < s3m.patNum; i++) {
                // Unpack patterns
                System.Collections.Generic.List<byte> bl = new System.Collections.Generic.List<byte>();
                mFile.Position = patternsOffsets[i] * 16 + 2;
                int row = 0;
                int chn = 0;
                while(row < 64) {
                    mFile.Read(pattern, 0, 1);
                    if(pattern[0] == 0) {
                        for(j = chn; j < ActiveChannels; j++) bl.AddRange(new byte[6]);
                        chn = 0;
                        row++;
                        continue;
                    }

                    if((pattern[0] & 0x20) != 0) mFile.Read(pattern, 1, 2);
                    if((pattern[0] & 0x40) != 0) mFile.Read(pattern, 3, 1);
                    if((pattern[0] & 0x80) != 0) mFile.Read(pattern, 4, 2);
                    bl.AddRange(pattern);
                    chn++;
                }
                Patterns[i] = bl.ToArray();
            }
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
    }
}