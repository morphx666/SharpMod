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
        public SoundFile(string lpszPathName, uint nRate, bool bHigh, bool bStereo, bool bLoop) {
            byte[] s = new byte[1024];
            string ss;
            int i, j, k, nbp;
            byte[] bTab = new byte[32];

            for(i = 0; i < names.Length; i++) {
                names[i] = new byte[32];
            }

            mType = 0;
            mRate = nRate;
            m16Bit = bHigh;
            mStereo = bStereo;
            mLoop = bLoop;
            mChannels = 0;

            mFile = new FileInfo(lpszPathName).Open(FileMode.Open, FileAccess.Read, FileShare.Read);

            mType = 1;
            mFile.Seek(0x438, SeekOrigin.Begin);
            mFile.Read(s, 0, 4);
            s[4] = 0;
            mSamples = 31;
            mChannels = 4;

            ss = Encoding.Default.GetString(s).TrimEnd((char)0);

            if(ss == "M.K.") {
                mChannels = 4;
            } else {
                if((ss == "FLT1") && (s[3] <= '9')) {
                    mChannels = s[3];
                } else {
                    if((s[0] >= '1') && (s[0] <= '9') && (s[1] == 'C') && (s[2] == 'H') && (s[3] == 'N')) {
                        mChannels = s[0];
                    } else {
                        if((s[0] == '1') && (s[1] >= '0') && (s[1] <= '6') && (s[2] == 'C') && (s[3] == 'H')) {
                            mChannels = s[1] - (uint)10;
                        } else {
                            mSamples = 15;
                        }
                    }
                }
            }

            mFile.Seek(0, SeekOrigin.Begin);
            mFile.Read(names[0], 0, 20);

            for(i = 1; i <= (int)mSamples; i++) {
                mFile.Read(bTab, 0, 30);
                Array.Copy(bTab, names[i], 22);

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

            for(i = 0; i < 32; i++) {
                j = 31;
                while((j >= 0) && (names[i][j] <= ' ')) names[i][j--] = 0;
                while(j >= 0) {
                    if(names[i][j] < ' ') names[i][j] = (byte)' ';
                    j--;
                }
            }

            mFile.Read(bTab, 0, 2);
            k = bTab[0];
            if(mFile.Read(order, 0, 128) != 128) {
                CloseFile();
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
            if(mSamples == 31) mFile.Seek(4, SeekOrigin.Current);
            if(nbp == 0) {
                CloseFile();
                return;
            }

            // Reading channels
            for(i = 0; i < nbp; i++) {
                patterns[i] = new byte[mChannels * 256];
                mFile.Read(patterns[i], 0, (int)mChannels * 256);
            }

            // Reading instruments
            for(i = 1; i <= (int)mSamples; i++) if(instruments[i].Length != 0) {
                    instruments[i].Sample = new byte[instruments[i].Length + 1];
                    mFile.Read(instruments[i].Sample, 0, (int)instruments[i].Length);
                    instruments[i].Sample[instruments[i].Length] = instruments[i].Sample[instruments[i].Length - 1];
                }

            mType = 2;
            CloseFile();
        }

        private void CloseFile() {
            mFile.Close();

            // Default settings	
            mMusicSpeed = 6;
            mMusicTempo = 125;
            mPattern = 0;
            mCurrentPattern = 0;
            mNextPattern = 0;
            mBufferCount = 0;
            mSpeedCount = 0;
            mRow = 0x3F;
        }
    }
}