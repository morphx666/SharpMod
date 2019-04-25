using System;
using System.IO;
using System.Text;

namespace SharpMod {
    public class SoundFile {
        private const int MOD_PRECISION = 10;
        private const int MOD_FRACMASK = 1023;
        private const int MOD_AMIGAC2 = 0x1AB;

        private static uint[] FineTuneTable = {
            7895,7941,7985,8046,8107,8169,8232,8280,
            8363,8413,8463,8529,8581,8651,8723,8757,
        };

        // Sinus table
        private static int[] ModSinusTable = {
            0,12,25,37,49,60,71,81,90,98,106,112,117,122,125,126,
            127,126,125,122,117,112,106,98,90,81,71,60,49,37,25,12,
            0,-12,-25,-37,-49,-60,-71,-81,-90,-98,-106,-112,-117,-122,-125,-126,
            -127,-126,-125,-122,-117,-112,-106,-98,-90,-81,-71,-60,-49,-37,-25,-12
        };

        // Triangle wave table (ramp down)
        private static int[] ModRampDownTable = {
            0,-4,-8,-12,-16,-20,-24,-28,-32,-36,-40,-44,-48,-52,-56,-60,
            -64,-68,-72,-76,-80,-84,-88,-92,-96,-100,-104,-108,-112,-116,-120,-124,
            127,123,119,115,111,107,103,99,95,91,87,83,79,75,71,67,
            63,59,55,51,47,43,39,35,31,27,23,19,15,11,7,3
        };

        // Square wave table
        private static int[] ModSquareTable = {
            127,127,127,127,127,127,127,127,127,127,127,127,127,127,127,127,
            127,127,127,127,127,127,127,127,127,127,127,127,127,127,127,127,
            -127,-127,-127,-127,-127,-127,-127,-127,-127,-127,-127,-127,-127,-127,-127,-127,
            -127,-127,-127,-127,-127,-127,-127,-127,-127,-127,-127,-127,-127,-127,-127,-127
        };

        // Random wave table
        private static int[] ModRandomTable = {
            98,-127,-43,88,102,41,-65,-94,125,20,-71,-86,-70,-32,-16,-96,
            17,72,107,-5,116,-69,-62,-40,10,-61,65,109,-18,-38,-13,-76,
            -23,88,21,-94,8,106,21,-112,6,109,20,-88,-30,9,-127,118,
            42,-34,89,-4,-51,-72,21,-29,112,123,84,-101,-92,98,-54,-95
        };

        private struct MODINSTRUMENT {
            public uint nLength, nLoopStart, nLoopEnd;
            public uint nFineTune;
            public int nVolume;
            public byte[] pSample;
        }

        private struct MODCHANNEL {
            public uint nSample, nFineTune;
            public uint nPos, nInc;
            public uint nLength, nLoopStart, nLoopEnd;
            public int nVolume, nVolumeSlide, nOldVolumeSlide;
            public int nPeriod, nOldPeriod, nFreqSlide, nOldFreqSlide;
            public int nPortamentoDest, nPortamentoSlide;
            public int nVibratoPos, nVibratoSlide, nVibratoType;
            public int nTremoloPos, nTremoloSlide, nTremoloType;
            public int nCount1, nCount2;
            public int nPeriod1, nPeriod2;
            public bool bPortamento, bVibrato, bTremolo;
            public byte[] pSample;
            public int nOldVol;
            public short nCurrentVol, nNextIns;
        }

        private FileStream m_File;
        private MODINSTRUMENT[] Ins = new MODINSTRUMENT[32];
        private MODCHANNEL[] Chn = new MODCHANNEL[32];
        private byte[][] m_szNames = new byte[32][];
        private byte[] Order = new byte[256];
        private byte[][] Patterns = new byte[64][];
        private uint m_nType, m_nRate, m_nChannels, m_nSamples;
        private uint m_nMusicSpeed, m_nMusicTempo, m_nSpeedCount, m_nBufferCount;
        private uint m_nPattern, m_nCurrentPattern, m_nNextPattern, m_nRow;
        private bool m_bHigh, m_bStereo, m_bLoop;

        public SoundFile(string lpszPathName, uint nRate, bool bHigh, bool bStereo, bool bLoop) {
            byte[] s = new byte[1024];
            string ss;
            int i, j, k, nbp;
            byte[] bTab = new byte[32];

            for(i = 0; i < m_szNames.Length; i++) {
                m_szNames[i] = new byte[32];
            }

            m_nType = 0;
            m_nRate = nRate;
            m_bHigh = bHigh;
            m_bStereo = bStereo;
            m_bLoop = bLoop;
            m_nChannels = 0;

            m_File = new FileInfo(lpszPathName).Open(FileMode.Open, FileAccess.Read, FileShare.Read);

            m_nType = 1;
            m_File.Seek(0x438, SeekOrigin.Begin);
            m_File.Read(s, 0, 4);
            s[4] = 0;
            m_nSamples = 31;
            m_nChannels = 4;

            ss = Encoding.Default.GetString(s).TrimEnd((char)0);

            if(ss == "M.K.") {
                m_nChannels = 4;
            } else {
                if((ss == "FLT1") && (s[3] <= '9')) {
                    m_nChannels = s[3];
                } else {
                    if((s[0] >= '1') && (s[0] <= '9') && (s[1] == 'C') && (s[2] == 'H') && (s[3] == 'N')) {
                        m_nChannels = s[0];
                    } else {
                        if((s[0] == '1') && (s[1] >= '0') && (s[1] <= '6') && (s[2] == 'C') && (s[3] == 'H')) {
                            m_nChannels = s[1] - (uint)10;
                        } else {
                            m_nSamples = 15;
                        }
                    }
                }
            }

            m_File.Seek(0, SeekOrigin.Begin);
            m_File.Read(m_szNames[0], 0, 20);

            for(i = 1; i <= (int)m_nSamples; i++) {
                m_File.Read(bTab, 0, 30);
                Array.Copy(bTab, m_szNames[i], 22);

                if((j = (bTab[22] << 9) | (bTab[23] << 1)) < 4) j = 0;
                Ins[i].nLength = (uint)j;
                if((j = bTab[24]) > 7) j &= 7; else j = (j & 7) + 8;
                Ins[i].nFineTune = FineTuneTable[j];
                Ins[i].nVolume = bTab[25];
                if(Ins[i].nVolume > 0x40) Ins[i].nVolume = 0x40;
                Ins[i].nVolume <<= 2;

                if((j = (int)((uint)bTab[26] << 9) | (int)((uint)bTab[27] << 1)) < 4) j = 0;
                if((k = (int)((uint)bTab[28] << 9) | (int)((uint)bTab[29] << 1)) < 4) k = 0;
                if(j + k > (int)Ins[i].nLength) {
                    j >>= 1;
                    k = j + ((k + 1) >> 1);
                } else k += j;
                if(Ins[i].nLength != 0) {
                    if(j >= (int)Ins[i].nLength) j = (int)(Ins[i].nLength - 1);
                    if(k > (int)Ins[i].nLength) k = (int)Ins[i].nLength;
                    if((j > k) || (k < 4) || (k - j <= 4)) j = k = 0;
                }
                Ins[i].nLoopStart = (uint)j;
                Ins[i].nLoopEnd = (uint)k;
            }

            for(i = 0; i < 32; i++) {
                j = 31;
                while((j >= 0) && (m_szNames[i][j] <= ' ')) m_szNames[i][j--] = 0;
                while(j >= 0) {
                    if(m_szNames[i][j] < ' ') m_szNames[i][j] = (byte)' ';
                    j--;
                }
            }

            m_File.Read(bTab, 0, 2);
            k = bTab[0];
            if(m_File.Read(Order, 0, 128) != 128) throw new Exception("EX01");
            nbp = 0;
            for(j = 0; j < 128; j++) {
                i = Order[j];
                if((i < 64) && (nbp <= i)) nbp = i + 1;
            }
            j = 0xFF;
            if((k == 0) || (k > 0x7F)) k = 0x7F;
            while((j >= k) && (Order[j] == 0)) Order[j--] = 0xFF;
            if(m_nSamples == 31) m_File.Seek(4, SeekOrigin.Current);
            if(nbp == 0) throw new Exception("EX02");

            // Reading channels
            for(i = 0; i < nbp; i++) {
                Patterns[i] = new byte[m_nChannels * 256];
                m_File.Read(Patterns[i], 0, (int)m_nChannels * 256);
            }

            // Reading instruments
            for(i = 1; i <= (int)m_nSamples; i++) if(Ins[i].nLength != 0) {
                    Ins[i].pSample = new byte[Ins[i].nLength + 1];
                    m_File.Read(Ins[i].pSample, 0, (int)Ins[i].nLength);
                    Ins[i].pSample[Ins[i].nLength] = Ins[i].pSample[Ins[i].nLength - 1];
                }

            m_File.Close();
            m_nType = 2;

            // Default settings	
            m_nMusicSpeed = 6;
            m_nMusicTempo = 125;
            m_nPattern = 0;
            m_nCurrentPattern = 0;
            m_nNextPattern = 0;
            m_nBufferCount = 0;
            m_nSpeedCount = 0;
            m_nRow = 0x3F;
        }

        public uint GetNumPatterns() {
            for(uint i = 0; i < 128; i++) if(Order[i] >= 64) return i;
            return 128;
        }

        public void SetCurrentPos(uint nPos) {
            uint nPattern = nPos >> 6;
            uint nRow = nPos & 0x3F;
            if(nPattern > 127) nPattern = 0;
            if(nRow != 0) {
                m_nCurrentPattern = nPattern;
                m_nNextPattern = nPattern + 1;
                m_nPattern = Order[m_nCurrentPattern];
                m_nRow = nRow - 1;
            } else {
                m_nCurrentPattern = nPattern;
                m_nNextPattern = nPattern;
                m_nPattern = Order[m_nCurrentPattern];
                m_nRow = 0x3F;
            }
            m_nBufferCount = 0;
        }

        public uint Read(byte[] lpBuffer, uint cbBuffer) {
            //byte[] p = (byte[])lpBuffer.Clone(); // Clone???
            byte[] p = lpBuffer; // Clone???
            uint lRead, lMax, lSampleSize;
            short adjustvol = (short)m_nChannels;
            short[] CurrentVol = new short[32];
            byte[][] pSample = new byte[32][];
            bool[] bTrkDest = new bool[32];
            uint j;

            if(m_nType == 0) return 0;
            lSampleSize = 1;
            if(m_bHigh) lSampleSize *= 2;
            if(m_bStereo) lSampleSize *= 2;
            lMax = cbBuffer / lSampleSize;
            if((lMax == 0) || (p == null)) return 0;
            if(m_nType == 1) return (uint)(m_File.Read(lpBuffer, 0, (int)(lMax * lSampleSize)) / lSampleSize);

            // Memorize channels settings
            for(j = 0; j < m_nChannels; j++) {
                CurrentVol[j] = Chn[j].nCurrentVol;
                if(Chn[j].nLength != 0) {
                    pSample[j] = new byte[Chn[j].pSample.Length];
                    Array.Copy(Chn[j].pSample, pSample[j], Chn[j].pSample.Length);
                } else {
                }
                if(m_nChannels == 4)
                    bTrkDest[j] = (((j & 3) == 1) || ((j & 3) == 2)) ? true : false;
                else
                    bTrkDest[j] = ((j & 1) != 0) ? false : true;
            }
            if(m_nPattern >= 64) return 0;

            // Fill audio buffer
            int pIndex = 0;
            for(lRead = 0; lRead < lMax; lRead++, pIndex += (int)lSampleSize) {
                if(m_nBufferCount == 0) {
                    ReadNote();
                    // Memorize channels settings
                    for(j = 0; j < m_nChannels; j++) {
                        CurrentVol[j] = Chn[j].nCurrentVol;
                        if(Chn[j].nLength != 0) {
                            pSample[j] = new byte[Chn[j].pSample.Length];
                            Array.Copy(Chn[j].pSample, pSample[j], Chn[j].pSample.Length);
                        } else {
                            pSample[j] = null;
                        }
                    }
                }
                m_nBufferCount--;

                int vRight = 0, vLeft = 0;
                for(uint i = 0; i < m_nChannels; i++) if(pSample[i] != null) {
                        // Read sample
                        int poshi = (int)(Chn[i].nPos >> MOD_PRECISION);
                        short poslo = (short)(Chn[i].nPos & MOD_FRACMASK);
                        short srcvol = (sbyte)pSample[i][poshi];
                        short destvol = (sbyte)pSample[i][poshi + 1];
                        int vol = srcvol + ((int)(poslo * (destvol - srcvol)) >> MOD_PRECISION);
                        vol *= CurrentVol[i];
                        if(bTrkDest[i]) vRight += vol; else vLeft += vol;
                        Chn[i].nOldVol = vol;
                        Chn[i].nPos += Chn[i].nInc;
                        if(Chn[i].nPos >= Chn[i].nLength) {
                            Chn[i].nLength = Chn[i].nLoopEnd;
                            Chn[i].nPos = (Chn[i].nPos & MOD_FRACMASK) + Chn[i].nLoopStart;
                            if(Chn[i].nLength != 0) pSample[i] = null;
                        }
                    } else {
                        int vol = Chn[i].nOldVol;
                        if(bTrkDest[i]) vRight += vol; else vLeft += vol;
                    }

                // Sample ready
                if(m_bStereo) {
                    // Stereo - Surround
                    int vol = vRight;
                    vRight = (vRight * 13 + vLeft * 3) / (adjustvol * 8);
                    vLeft = (vLeft * 13 + vol * 3) / (adjustvol * 8);
                    if(m_bHigh) {
                        // 16-Bit
                        p[pIndex + 0] = (byte)(((uint)vRight) & 0xFF);
                        p[pIndex + 1] = (byte)(((uint)vRight) >> 8);
                        p[pIndex + 2] = (byte)(((uint)vLeft) & 0xFF);
                        p[pIndex + 3] = (byte)(((uint)vLeft) >> 8);
                    } else {
                        // 8-Bit
                        p[pIndex + 0] = (byte)((((uint)vRight) >> 8) + 0x80);
                        p[pIndex + 1] = (byte)((((uint)vLeft) >> 8) + 0x80);
                    }
                } else {
                    // Mono
                    int vol = (vRight + vLeft) / adjustvol;
                    if(m_bHigh) {
                        // 16-Bit
                        p[pIndex + 0] = (byte)(((uint)vol) & 0xFF);
                        p[pIndex + 1] = (byte)(((uint)vol) >> 8);
                    } else {
                        // 8-Bit
                        p[pIndex + 0] = (byte)((((uint)vol) >> 8) + 0x80);
                    }
                }
            }
            return lRead * lSampleSize;
        }

        private bool ReadNote() {
            if(m_nSpeedCount == 0) {
                m_nRow = (m_nRow + 1) & 0x3F;
                if(m_nRow == 0) {
                    m_nCurrentPattern = m_nNextPattern;
                    m_nNextPattern++;
                    m_nPattern = Order[m_nCurrentPattern];
                }
                if(m_nPattern >= 64) {
                    m_nMusicSpeed = 6;
                    m_nMusicTempo = 125;
                    if(!m_bLoop) {
                        m_nBufferCount = (m_nRate * 5) / (m_nMusicTempo * 2);
                        return false;
                    }
                    m_nCurrentPattern = 0;
                    m_nNextPattern = 1;
                    m_nPattern = Order[m_nCurrentPattern];
                }
                int pIndex = (int)(m_nRow * m_nChannels * 4);
                byte[] p = Patterns[m_nPattern];
                for(uint nChn = 0; nChn < m_nChannels; nChn++, pIndex += 4) {
                    byte A0 = p[pIndex + 0], A1 = p[pIndex + 1], A2 = p[pIndex + 2], A3 = p[pIndex + 3];
                    uint period = (((uint)A0 & 0x0F) << 8) | (A1);
                    uint instr = ((uint)A2 >> 4) | (uint)(A0 & 0x10);
                    uint command = (uint)(A2 & 0x0F);
                    uint param = A3;
                    bool bVib = Chn[nChn].bVibrato;
                    bool bTrem = Chn[nChn].bTremolo;

                    // Reset channels data
                    Chn[nChn].nVolumeSlide = 0;
                    Chn[nChn].nFreqSlide = 0;
                    Chn[nChn].nOldPeriod = Chn[nChn].nPeriod;
                    Chn[nChn].bPortamento = false;
                    Chn[nChn].bVibrato = false;
                    Chn[nChn].bTremolo = false;
                    if(instr > 31) instr = 0;
                    if(instr != 0) Chn[nChn].nNextIns = (short)instr;
                    if(period != 0) {
                        if(Chn[nChn].nNextIns != 0) {
                            Chn[nChn].nSample = instr;
                            Chn[nChn].nVolume = Ins[instr].nVolume;
                            Chn[nChn].nPos = 0;
                            Chn[nChn].nLength = Ins[instr].nLength << MOD_PRECISION;
                            Chn[nChn].nFineTune = Ins[instr].nFineTune << MOD_PRECISION;
                            Chn[nChn].nLoopStart = Ins[instr].nLoopStart << MOD_PRECISION;
                            Chn[nChn].nLoopEnd = Ins[instr].nLoopEnd << MOD_PRECISION;
                            Chn[nChn].pSample = Ins[instr].pSample;
                            Chn[nChn].nNextIns = 0;
                        }
                        if((command != 0x03) || (Chn[nChn].nPeriod == 0)) {
                            Chn[nChn].nPeriod = (int)period;
                            Chn[nChn].nLength = Ins[Chn[nChn].nSample].nLength << MOD_PRECISION;
                            Chn[nChn].nPos = 0;
                        }
                        Chn[nChn].nPortamentoDest = (int)period;
                    }
                    switch(command) {
                        // 00: Arpeggio
                        case 0x00:
                            if((param == 0) || (Chn[nChn].nPeriod == 0)) break;
                            Chn[nChn].nCount2 = 3;
                            Chn[nChn].nPeriod2 = Chn[nChn].nPeriod;
                            Chn[nChn].nCount1 = 2;
                            Chn[nChn].nPeriod1 = (int)(Chn[nChn].nPeriod + (param & 0x0F));
                            Chn[nChn].nPeriod += (int)((param >> 4) & 0x0F);
                            break;
                        // 01: Portamento Up
                        case 0x01:
                            if(param == 0) param = (uint)Chn[nChn].nOldFreqSlide;
                            Chn[nChn].nOldFreqSlide = (int)param;
                            Chn[nChn].nFreqSlide = -(int)param;
                            break;
                        // 02: Portamento Down
                        case 0x02:
                            if(param == 0) param = (uint)Chn[nChn].nOldFreqSlide;
                            Chn[nChn].nOldFreqSlide = (int)param;
                            Chn[nChn].nFreqSlide = (int)param;
                            break;
                        // 03: Tone-Portamento
                        case 0x03:
                            if(param == 0) param = (uint)Chn[nChn].nPortamentoSlide;
                            Chn[nChn].nPortamentoSlide = (int)param;
                            Chn[nChn].bPortamento = false;
                            break;
                        // 04: Vibrato
                        case 0x04:
                            if(!bVib) Chn[nChn].nVibratoPos = 0;
                            if(param == 0) Chn[nChn].nVibratoSlide = (int)param;
                            Chn[nChn].bVibrato = false;
                            break;
                        // 05: Tone-Portamento + Volume Slide
                        case 0x05:
                            if(period != 0) {
                                Chn[nChn].nPortamentoDest = (int)period;
                                if(Chn[nChn].nOldPeriod != 0) Chn[nChn].nPeriod = Chn[nChn].nOldPeriod;
                            }
                            Chn[nChn].bPortamento = false;
                            if(param != 0) {
                                if((param & 0xF0) != 0) Chn[nChn].nVolumeSlide = (int)((param >> 4) << 2);
                                else Chn[nChn].nVolumeSlide = -(int)((param & 0x0F) << 2);
                                Chn[nChn].nOldVolumeSlide = Chn[nChn].nVolumeSlide;
                            }
                            break;
                        // 06: Vibrato + Volume Slide
                        case 0x06:
                            if(!bVib) Chn[nChn].nVibratoPos = 0;
                            Chn[nChn].bVibrato = false;
                            if(param != 0) {
                                if((param & 0xF0) != 0) Chn[nChn].nVolumeSlide = (int)((param >> 4) << 2);
                                else Chn[nChn].nVolumeSlide = -(int)((param & 0x0F) << 2);
                                Chn[nChn].nOldVolumeSlide = Chn[nChn].nVolumeSlide;
                            }
                            break;
                        // 07: Tremolo
                        case 0x07:
                            if(!bTrem) Chn[nChn].nTremoloPos = 0;
                            if(param == 0) Chn[nChn].nTremoloSlide = (int)param;
                            Chn[nChn].bTremolo = false;
                            break;
                        // 09: Set Offset
                        case 0x09:
                            if(param > 0) {
                                param <<= 8 + MOD_PRECISION;
                                if(param < Chn[nChn].nLength) Chn[nChn].nPos = param;
                            }
                            break;
                        // 0A: Volume Slide
                        case 0x0A:
                            if(param != 0) {
                                if((param & 0xF0) != 0) Chn[nChn].nVolumeSlide = (int)((param >> 4) << 2);
                                else Chn[nChn].nVolumeSlide = -(int)((param & 0x0F) << 2);
                                Chn[nChn].nOldVolumeSlide = Chn[nChn].nVolumeSlide;
                            }
                            break;
                        // 0B: Position Jump
                        case 0x0B:
                            param &= 0x7F;
                            m_nNextPattern = param;
                            m_nRow = 0x3F;
                            break;
                        // 0C: Set Volume
                        case 0x0C:
                            if(param > 0x40) param = 0x40;
                            param <<= 2;
                            Chn[nChn].nVolume = (int)param;
                            break;
                        // 0B: Pattern Break
                        case 0x0D:
                            m_nRow = 0x3F;
                            break;
                        // 0E: Extended Effects
                        case 0x0E:
                            command = param >> 4;
                            param &= 0x0F;
                            switch(command) {
                                // 0xE1: Fine Portamento Up
                                case 0x01:
                                    if(Chn[nChn].nPeriod != 0) {
                                        Chn[nChn].nPeriod -= (int)param;
                                        if(Chn[nChn].nPeriod < 1) Chn[nChn].nPeriod = 1;
                                    }
                                    break;
                                // 0xE2: Fine Portamento Down
                                case 0x02:
                                    if(Chn[nChn].nPeriod != 0) {
                                        Chn[nChn].nPeriod += (int)param;
                                    }
                                    break;
                                // 0xE3: Set Glissando Control (???)
                                // 0xE4: Set Vibrato WaveForm
                                case 0x04:
                                    Chn[nChn].nVibratoType = (int)(param & 0x03);
                                    break;
                                // 0xE5: Set Finetune
                                case 0x05:
                                    Chn[nChn].nFineTune = FineTuneTable[param];
                                    break;
                                // 0xE6: Pattern Loop
                                // 0xE7: Set Tremolo WaveForm
                                case 0x07:
                                    Chn[nChn].nTremoloType = (int)(param & 0x03);
                                    break;
                                // 0xE9: Retrig + Fine Volume Slide
                                // 0xEA: Fine Volume Up
                                case 0x0A:
                                    Chn[nChn].nVolume += (int)(param << 2);
                                    break;
                                // 0xEB: Fine Volume Down
                                case 0x0B:
                                    Chn[nChn].nVolume -= (int)(param << 2);
                                    break;
                                // 0xEC: Note Cut
                                case 0x0C:
                                    Chn[nChn].nCount1 = (int)(param + 1);
                                    Chn[nChn].nPeriod1 = 0;
                                    break;
                            }
                            break;
                        // 0F: Set Speed
                        case 0x0F:
                            if((param != 0) && (param < 0x20)) m_nMusicSpeed = param;
                            else
                                if(param >= 0x20) m_nMusicTempo = param;
                            break;
                    }
                }
                m_nSpeedCount = m_nMusicSpeed;
            }

            if(m_nPattern >= 64) return false;
            // Update channels data
            for(uint nChn = 0; nChn < m_nChannels; nChn++) {
                Chn[nChn].nVolume += Chn[nChn].nVolumeSlide;
                if(Chn[nChn].nVolume < 0) Chn[nChn].nVolume = 0;
                if(Chn[nChn].nVolume > 0x100) Chn[nChn].nVolume = 0x100;
                if(Chn[nChn].nCount1 != 0) {
                    Chn[nChn].nCount1--;
                    if(Chn[nChn].nCount1 == 0) Chn[nChn].nPeriod = Chn[nChn].nPeriod1;
                }
                if(Chn[nChn].nCount2 != 0) {
                    Chn[nChn].nCount2--;
                    if(Chn[nChn].nCount2 == 0) Chn[nChn].nPeriod = Chn[nChn].nPeriod2;
                }
                if(Chn[nChn].nPeriod != 0) {
                    Chn[nChn].nCurrentVol = (short)Chn[nChn].nVolume;
                    if(Chn[nChn].bTremolo) {
                        int vol = Chn[nChn].nCurrentVol;
                        switch(Chn[nChn].nTremoloType) {
                            case 1:
                                vol += ModRampDownTable[Chn[nChn].nTremoloPos] * (Chn[nChn].nTremoloSlide & 0x0F) / 127;
                                break;
                            case 2:
                                vol += ModSquareTable[Chn[nChn].nTremoloPos] * (Chn[nChn].nTremoloSlide & 0x0F) / 127;
                                break;
                            case 3:
                                vol += ModRandomTable[Chn[nChn].nTremoloPos] * (Chn[nChn].nTremoloSlide & 0x0F) / 127;
                                break;
                            default:
                                vol += ModSinusTable[Chn[nChn].nTremoloPos] * (Chn[nChn].nTremoloSlide & 0x0F) / 127;
                                break;
                        }
                        if(vol < 0) vol = 0;
                        if(vol > 0x100) vol = 0x100;
                        Chn[nChn].nCurrentVol = (short)vol;
                        Chn[nChn].nTremoloPos = (Chn[nChn].nTremoloPos + (Chn[nChn].nTremoloSlide >> 4)) & 0x3F;
                    }
                    if((Chn[nChn].bPortamento) && (Chn[nChn].nPortamentoDest != 0)) {
                        if(Chn[nChn].nPeriod < Chn[nChn].nPortamentoDest) {
                            Chn[nChn].nPeriod += Chn[nChn].nPortamentoSlide;
                            if(Chn[nChn].nPeriod > Chn[nChn].nPortamentoDest)
                                Chn[nChn].nPeriod = Chn[nChn].nPortamentoDest;
                        }
                        if(Chn[nChn].nPeriod > Chn[nChn].nPortamentoDest) {
                            Chn[nChn].nPeriod -= Chn[nChn].nPortamentoSlide;
                            if(Chn[nChn].nPeriod < Chn[nChn].nPortamentoDest)
                                Chn[nChn].nPeriod = Chn[nChn].nPortamentoDest;
                        }
                    }
                    Chn[nChn].nPeriod += Chn[nChn].nFreqSlide;
                    if(Chn[nChn].nPeriod < 1) Chn[nChn].nPeriod = 1;
                    int period = Chn[nChn].nPeriod;
                    if(Chn[nChn].bVibrato) {
                        switch(Chn[nChn].nVibratoType) {
                            case 1:
                                period += ModRampDownTable[Chn[nChn].nVibratoPos] * (Chn[nChn].nVibratoSlide & 0x0F) / 127;
                                break;
                            case 2:
                                period += ModSquareTable[Chn[nChn].nVibratoPos] * (Chn[nChn].nVibratoSlide & 0x0F) / 127;
                                break;
                            case 3:
                                period += ModRandomTable[Chn[nChn].nVibratoPos] * (Chn[nChn].nVibratoSlide & 0x0F) / 127;
                                break;
                            default:
                                period += ModSinusTable[Chn[nChn].nVibratoPos] * (Chn[nChn].nVibratoSlide & 0x0F) / 127;
                                break;
                        }
                        Chn[nChn].nVibratoPos = (Chn[nChn].nVibratoPos + (Chn[nChn].nVibratoSlide >> 4)) & 0x3F;
                    }
                    if(period < 1) period = 1;
                    Chn[nChn].nInc = (uint)((Chn[nChn].nFineTune * MOD_AMIGAC2) / (period * m_nRate));
                } else {
                    Chn[nChn].nInc = 0;
                    Chn[nChn].nPos = 0;
                    Chn[nChn].nLength = 0;
                }
            }
            m_nBufferCount = (m_nRate * 5) / (m_nMusicTempo * 2);
            m_nSpeedCount--;
            return true;
        }
    }
}