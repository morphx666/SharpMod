using System;
using System.IO;
using System.Text;

/*
    This is a verbatim implementation of the magnificent code developed by Olivier Lapicque for his Mod95 player.

    For more information, visit https://openmpt.org/legacy_software

    Code ported to c# by Xavier Flix (https://github.com/morphx666) on 2014/ 04/25
*/

namespace SharpMod {
    public partial class SoundFile {
        public SoundFile(string fileName, uint sampleRate, bool is16Bit, bool isStereo, bool loop) {
            byte[] s = new byte[1024];
            string ss;
            int i, j, k, nbp;
            byte[] bTab = new byte[32];

            for(i = 0; i < Instruments.Length; i++) {
                Instruments[i].name = new byte[32];
            }

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

            ss = Encoding.Default.GetString(s).TrimEnd((char)0);

            if(ss == "M.K.") {
                ActiveChannels = 4;
            } else {
                if((ss == "FLT1") && (s[3] <= '9')) {
                    ActiveChannels = s[3];
                } else {
                    if((s[0] >= '1') && (s[0] <= '9') && (s[1] == 'C') && (s[2] == 'H') && (s[3] == 'N')) {
                        ActiveChannels = s[0];
                    } else {
                        if((s[0] == '1') && (s[1] >= '0') && (s[1] <= '6') && (s[2] == 'C') && (s[3] == 'H')) {
                            ActiveChannels = s[1] - (uint)10;
                        } else {
                            ActiveSamples = 15;
                        }
                    }
                }
            }

            mFile.Seek(0, SeekOrigin.Begin);
            mFile.Read(Instruments[0].name, 0, 20);

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
            if(mFile.Read(order, 0, 128) != 128) {
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
            if(ActiveSamples == 31) mFile.Seek(4, SeekOrigin.Current);
            if(nbp == 0) {
                CloseFile(false);
                return;
            }

            // Reading channels
            for(i = 0; i < nbp; i++) {
                patterns[i] = new byte[ActiveChannels * 256];
                mFile.Read(patterns[i], 0, (int)ActiveChannels * 256);
            }

            // Reading instruments
            for(i = 1; i <= (int)ActiveSamples; i++) if(Instruments[i].Length != 0) {
                    Instruments[i].Sample = new byte[Instruments[i].Length + 1];
                    mFile.Read(Instruments[i].Sample, 0, (int)Instruments[i].Length);
                    Instruments[i].Sample[Instruments[i].Length] = Instruments[i].Sample[Instruments[i].Length - 1];
                }

            Type = 2;
            CloseFile(true);
        }

        private void CloseFile(bool isValid) {
            mFile.Close();

            // Default settings	
            MusicSpeed = 6;
            MusicTempo = 125;
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