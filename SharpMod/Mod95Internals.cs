using System;
using System.Collections.Generic;
using System.Text;

namespace SharpMod {
    public partial class SoundFile {
        private uint GetNumPatterns() {
            for(uint i = 0; i < 128; i++) if(order[i] >= 64) return i;
            return 128;
        }

        private uint GetLength() {
            uint dwElapsedTime = 0, nRow = 0, nSpeedCount = 0, nCurrentPattern = 0, nNextPattern = 0, nPattern = 0;
            uint nMusicSpeed = 6, nMusicTempo = 125;

            for(; ; ) {
                if(nSpeedCount == 0) {
                    nRow = (nRow + 1) & 0x3F;
                    if(nRow == 0) {
                        nCurrentPattern = nNextPattern;
                        nNextPattern++;
                        nPattern = order[nCurrentPattern];
                    }
                    if(nPattern >= 64) goto EndMod;

                    int pIndex = (int)(nRow * mChannels * 4);
                    byte[] p = patterns[nPattern];
                    for(uint nChn = 0; nChn < mChannels; nChn++, pIndex += 4) {
                        uint command = (uint)(p[2] & 0x0F);
                        uint param = p[3];

                        switch(command) {
                            // 0B: Position Jump
                            case 0x0B:
                                param &= 0x7F;
                                if(param <= nCurrentPattern) goto EndMod;
                                nNextPattern = param;
                                nRow = 0x3F;
                                break;
                            // 0B: Pattern Break
                            case 0x0D:
                                nRow = 0x3F;
                                break;
                            // 0F: Set Speed
                            case 0x0F:
                                if((param != 0) && (param < 0x20)) nMusicSpeed = param;
                                else
                                    if(param >= 0x20) nMusicTempo = param;
                                break;
                        }
                    }
                    nSpeedCount = nMusicSpeed;
                }
                if(nPattern >= 64) goto EndMod;
                dwElapsedTime += 5000 / (nMusicTempo * 2);
                nSpeedCount--;
            }
        EndMod:
            return (dwElapsedTime + 500) / 1000;
        }

        private void SetCurrentPos(uint nPos) {
            uint nPattern = nPos >> 6;
            uint nRow = nPos & 0x3F;
            if(nPattern > 127) nPattern = 0;
            if(nRow != 0) {
                mCurrentPattern = nPattern;
                mNextPattern = nPattern + 1;
                mPattern = order[mCurrentPattern];
                mRow = nRow - 1;
            } else {
                mCurrentPattern = nPattern;
                mNextPattern = nPattern;
                mPattern = order[mCurrentPattern];
                mRow = 0x3F;
            }
            mBufferCount = 0;
        }

        public uint Read(byte[] lpBuffer, uint cbBuffer) {
            //byte[] p = (byte[])lpBuffer.Clone(); // Clone???
            byte[] p = lpBuffer; // Clone???
            uint lRead, lMax, lSampleSize;
            short adjustvol = (short)mChannels;
            short[] CurrentVol = new short[32];
            byte[][] pSample = new byte[32][];
            bool[] bTrkDest = new bool[32];
            uint j;

            if(mType == 0) return 0;
            lSampleSize = 1;
            if(m16Bit) lSampleSize *= 2;
            if(mStereo) lSampleSize *= 2;
            lMax = cbBuffer / lSampleSize;
            if((lMax == 0) || (p == null)) return 0;
            if(mType == 1) return (uint)(mFile.Read(lpBuffer, 0, (int)(lMax * lSampleSize)) / lSampleSize);

            // Memorize channels settings
            for(j = 0; j < mChannels; j++) {
                CurrentVol[j] = channels[j].CurrentVol;
                if(channels[j].Length != 0) {
                    pSample[j] = new byte[channels[j].Sample.Length];
                    Array.Copy(channels[j].Sample, pSample[j], channels[j].Sample.Length);
                } else {
                }
                if(mChannels == 4)
                    bTrkDest[j] = (((j & 3) == 1) || ((j & 3) == 2)) ? true : false;
                else
                    bTrkDest[j] = ((j & 1) != 0) ? false : true;
            }
            if(mPattern >= 64) return 0;

            // Fill audio buffer
            int pIndex = 0;
            for(lRead = 0; lRead < lMax; lRead++, pIndex += (int)lSampleSize) {
                if(mBufferCount == 0) {
                    ReadNote();
                    // Memorize channels settings
                    for(j = 0; j < mChannels; j++) {
                        CurrentVol[j] = channels[j].CurrentVol;
                        if(channels[j].Length != 0) {
                            pSample[j] = new byte[channels[j].Sample.Length];
                            Array.Copy(channels[j].Sample, pSample[j], channels[j].Sample.Length);
                        } else {
                            pSample[j] = null;
                        }
                    }
                }
                mBufferCount--;

                int vRight = 0, vLeft = 0;
                for(uint i = 0; i < mChannels; i++) if(pSample[i] != null) {
                        // Read sample
                        int poshi = (int)(channels[i].Pos >> MOD_PRECISION);
                        short poslo = (short)(channels[i].Pos & MOD_FRACMASK);
                        short srcvol = (sbyte)pSample[i][poshi];
                        short destvol = (sbyte)pSample[i][poshi + 1];
                        int vol = srcvol + ((int)(poslo * (destvol - srcvol)) >> MOD_PRECISION);
                        vol *= CurrentVol[i];
                        if(bTrkDest[i]) vRight += vol; else vLeft += vol;
                        channels[i].OldVol = vol;
                        channels[i].Pos += channels[i].Inc;
                        if(channels[i].Pos >= channels[i].Length) {
                            channels[i].Length = channels[i].LoopEnd;
                            channels[i].Pos = (channels[i].Pos & MOD_FRACMASK) + channels[i].LoopStart;
                            if(channels[i].Length != 0) pSample[i] = null;
                        }
                    } else {
                        int vol = channels[i].OldVol;
                        if(bTrkDest[i]) vRight += vol; else vLeft += vol;
                    }

                // Sample ready
                if(mStereo) {
                    // Stereo - Surround
                    int vol = vRight;
                    vRight = (vRight * 13 + vLeft * 3) / (adjustvol * 8);
                    vLeft = (vLeft * 13 + vol * 3) / (adjustvol * 8);
                    if(m16Bit) {
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
                    if(m16Bit) {
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
            if(mSpeedCount == 0) {
                mRow = (mRow + 1) & 0x3F;
                if(mRow == 0) {
                    mCurrentPattern = mNextPattern;
                    mNextPattern++;
                    mPattern = order[mCurrentPattern];
                }
                if(mPattern >= 64) {
                    mMusicSpeed = 6;
                    mMusicTempo = 125;
                    if(!mLoop) {
                        mBufferCount = (mRate * 5) / (mMusicTempo * 2);
                        return false;
                    }
                    mCurrentPattern = 0;
                    mNextPattern = 1;
                    mPattern = order[mCurrentPattern];
                }
                int pIndex = (int)(mRow * mChannels * 4);
                byte[] p = patterns[mPattern];
                for(uint chnIdx = 0; chnIdx < mChannels; chnIdx++, pIndex += 4) {
                    byte A0 = p[pIndex + 0], A1 = p[pIndex + 1], A2 = p[pIndex + 2], A3 = p[pIndex + 3];
                    uint period = (((uint)A0 & 0x0F) << 8) | (A1);
                    uint instIdx = ((uint)A2 >> 4) | (uint)(A0 & 0x10);
                    uint command = (uint)(A2 & 0x0F);
                    uint param = A3;
                    bool bVib = channels[chnIdx].Vibrato;
                    bool bTrem = channels[chnIdx].Tremolo;

                    // Reset channels data
                    channels[chnIdx].VolumeSlide = 0;
                    channels[chnIdx].FreqSlide = 0;
                    channels[chnIdx].OldPeriod = channels[chnIdx].Period;
                    channels[chnIdx].Portamento = false;
                    channels[chnIdx].Vibrato = false;
                    channels[chnIdx].Tremolo = false;
                    if(instIdx > 31) instIdx = 0;
                    if(instIdx != 0) channels[chnIdx].NextIns = (short)instIdx;
                    if(period != 0) {
                        if(channels[chnIdx].NextIns != 0) {
                            channels[chnIdx].InstrumentIndex = instIdx;
                            channels[chnIdx].Volume = instruments[instIdx].Volume;
                            channels[chnIdx].Pos = 0;
                            channels[chnIdx].Length = instruments[instIdx].Length << MOD_PRECISION;
                            channels[chnIdx].FineTune = instruments[instIdx].FineTune << MOD_PRECISION;
                            channels[chnIdx].LoopStart = instruments[instIdx].LoopStart << MOD_PRECISION;
                            channels[chnIdx].LoopEnd = instruments[instIdx].LoopEnd << MOD_PRECISION;
                            channels[chnIdx].Sample = instruments[instIdx].Sample;
                            channels[chnIdx].NextIns = 0;
                        }
                        if((command != 0x03) || (channels[chnIdx].Period == 0)) {
                            channels[chnIdx].Period = (int)period;
                            channels[chnIdx].Length = instruments[channels[chnIdx].InstrumentIndex].Length << MOD_PRECISION;
                            channels[chnIdx].Pos = 0;
                        }
                        channels[chnIdx].PortamentoDest = (int)period;
                    }
                    switch(command) {
                        // 00: Arpeggio
                        case 0x00:
                            if((param == 0) || (channels[chnIdx].Period == 0)) break;
                            channels[chnIdx].Count2 = 3;
                            channels[chnIdx].Period2 = channels[chnIdx].Period;
                            channels[chnIdx].Count1 = 2;
                            channels[chnIdx].Period1 = (int)(channels[chnIdx].Period + (param & 0x0F));
                            channels[chnIdx].Period += (int)((param >> 4) & 0x0F);
                            break;
                        // 01: Portamento Up
                        case 0x01:
                            if(param == 0) param = (uint)channels[chnIdx].OldFreqSlide;
                            channels[chnIdx].OldFreqSlide = (int)param;
                            channels[chnIdx].FreqSlide = -(int)param;
                            break;
                        // 02: Portamento Down
                        case 0x02:
                            if(param == 0) param = (uint)channels[chnIdx].OldFreqSlide;
                            channels[chnIdx].OldFreqSlide = (int)param;
                            channels[chnIdx].FreqSlide = (int)param;
                            break;
                        // 03: Tone-Portamento
                        case 0x03:
                            if(param == 0) param = (uint)channels[chnIdx].PortamentoSlide;
                            channels[chnIdx].PortamentoSlide = (int)param;
                            channels[chnIdx].Portamento = false;
                            break;
                        // 04: Vibrato
                        case 0x04:
                            if(!bVib) channels[chnIdx].VibratoPos = 0;
                            if(param == 0) channels[chnIdx].VibratoSlide = (int)param;
                            channels[chnIdx].Vibrato = false;
                            break;
                        // 05: Tone-Portamento + Volume Slide
                        case 0x05:
                            if(period != 0) {
                                channels[chnIdx].PortamentoDest = (int)period;
                                if(channels[chnIdx].OldPeriod != 0) channels[chnIdx].Period = channels[chnIdx].OldPeriod;
                            }
                            channels[chnIdx].Portamento = false;
                            if(param != 0) {
                                if((param & 0xF0) != 0) channels[chnIdx].VolumeSlide = (int)((param >> 4) << 2);
                                else channels[chnIdx].VolumeSlide = -(int)((param & 0x0F) << 2);
                                channels[chnIdx].OldVolumeSlide = channels[chnIdx].VolumeSlide;
                            }
                            break;
                        // 06: Vibrato + Volume Slide
                        case 0x06:
                            if(!bVib) channels[chnIdx].VibratoPos = 0;
                            channels[chnIdx].Vibrato = false;
                            if(param != 0) {
                                if((param & 0xF0) != 0) channels[chnIdx].VolumeSlide = (int)((param >> 4) << 2);
                                else channels[chnIdx].VolumeSlide = -(int)((param & 0x0F) << 2);
                                channels[chnIdx].OldVolumeSlide = channels[chnIdx].VolumeSlide;
                            }
                            break;
                        // 07: Tremolo
                        case 0x07:
                            if(!bTrem) channels[chnIdx].TremoloPos = 0;
                            if(param == 0) channels[chnIdx].TremoloSlide = (int)param;
                            channels[chnIdx].Tremolo = false;
                            break;
                        // 09: Set Offset
                        case 0x09:
                            if(param > 0) {
                                param <<= 8 + MOD_PRECISION;
                                if(param < channels[chnIdx].Length) channels[chnIdx].Pos = param;
                            }
                            break;
                        // 0A: Volume Slide
                        case 0x0A:
                            if(param != 0) {
                                if((param & 0xF0) != 0) channels[chnIdx].VolumeSlide = (int)((param >> 4) << 2);
                                else channels[chnIdx].VolumeSlide = -(int)((param & 0x0F) << 2);
                                channels[chnIdx].OldVolumeSlide = channels[chnIdx].VolumeSlide;
                            }
                            break;
                        // 0B: Position Jump
                        case 0x0B:
                            param &= 0x7F;
                            mNextPattern = param;
                            mRow = 0x3F;
                            break;
                        // 0C: Set Volume
                        case 0x0C:
                            if(param > 0x40) param = 0x40;
                            param <<= 2;
                            channels[chnIdx].Volume = (int)param;
                            break;
                        // 0B: Pattern Break
                        case 0x0D:
                            mRow = 0x3F;
                            break;
                        // 0E: Extended Effects
                        case 0x0E:
                            command = param >> 4;
                            param &= 0x0F;
                            switch(command) {
                                // 0xE1: Fine Portamento Up
                                case 0x01:
                                    if(channels[chnIdx].Period != 0) {
                                        channels[chnIdx].Period -= (int)param;
                                        if(channels[chnIdx].Period < 1) channels[chnIdx].Period = 1;
                                    }
                                    break;
                                // 0xE2: Fine Portamento Down
                                case 0x02:
                                    if(channels[chnIdx].Period != 0) {
                                        channels[chnIdx].Period += (int)param;
                                    }
                                    break;
                                // 0xE3: Set Glissando Control (???)
                                // 0xE4: Set Vibrato WaveForm
                                case 0x04:
                                    channels[chnIdx].VibratoType = (int)(param & 0x03);
                                    break;
                                // 0xE5: Set Finetune
                                case 0x05:
                                    channels[chnIdx].FineTune = FineTuneTable[param];
                                    break;
                                // 0xE6: Pattern Loop
                                // 0xE7: Set Tremolo WaveForm
                                case 0x07:
                                    channels[chnIdx].TremoloType = (int)(param & 0x03);
                                    break;
                                // 0xE9: Retrig + Fine Volume Slide
                                // 0xEA: Fine Volume Up
                                case 0x0A:
                                    channels[chnIdx].Volume += (int)(param << 2);
                                    break;
                                // 0xEB: Fine Volume Down
                                case 0x0B:
                                    channels[chnIdx].Volume -= (int)(param << 2);
                                    break;
                                // 0xEC: Note Cut
                                case 0x0C:
                                    channels[chnIdx].Count1 = (int)(param + 1);
                                    channels[chnIdx].Period1 = 0;
                                    break;
                            }
                            break;
                        // 0F: Set Speed
                        case 0x0F:
                            if((param != 0) && (param < 0x20)) mMusicSpeed = param;
                            else
                                if(param >= 0x20) mMusicTempo = param;
                            break;
                    }
                }
                mSpeedCount = mMusicSpeed;
            }

            if(mPattern >= 64) return false;
            // Update channels data
            for(uint nChn = 0; nChn < mChannels; nChn++) {
                channels[nChn].Volume += channels[nChn].VolumeSlide;
                if(channels[nChn].Volume < 0) channels[nChn].Volume = 0;
                if(channels[nChn].Volume > 0x100) channels[nChn].Volume = 0x100;
                if(channels[nChn].Count1 != 0) {
                    channels[nChn].Count1--;
                    if(channels[nChn].Count1 == 0) channels[nChn].Period = channels[nChn].Period1;
                }
                if(channels[nChn].Count2 != 0) {
                    channels[nChn].Count2--;
                    if(channels[nChn].Count2 == 0) channels[nChn].Period = channels[nChn].Period2;
                }
                if(channels[nChn].Period != 0) {
                    channels[nChn].CurrentVol = (short)channels[nChn].Volume;
                    if(channels[nChn].Tremolo) {
                        int vol = channels[nChn].CurrentVol;
                        switch(channels[nChn].TremoloType) {
                            case 1:
                                vol += ModRampDownTable[channels[nChn].TremoloPos] * (channels[nChn].TremoloSlide & 0x0F) / 127;
                                break;
                            case 2:
                                vol += ModSquareTable[channels[nChn].TremoloPos] * (channels[nChn].TremoloSlide & 0x0F) / 127;
                                break;
                            case 3:
                                vol += ModRandomTable[channels[nChn].TremoloPos] * (channels[nChn].TremoloSlide & 0x0F) / 127;
                                break;
                            default:
                                vol += ModSinusTable[channels[nChn].TremoloPos] * (channels[nChn].TremoloSlide & 0x0F) / 127;
                                break;
                        }
                        if(vol < 0) vol = 0;
                        if(vol > 0x100) vol = 0x100;
                        channels[nChn].CurrentVol = (short)vol;
                        channels[nChn].TremoloPos = (channels[nChn].TremoloPos + (channels[nChn].TremoloSlide >> 4)) & 0x3F;
                    }
                    if((channels[nChn].Portamento) && (channels[nChn].PortamentoDest != 0)) {
                        if(channels[nChn].Period < channels[nChn].PortamentoDest) {
                            channels[nChn].Period += channels[nChn].PortamentoSlide;
                            if(channels[nChn].Period > channels[nChn].PortamentoDest)
                                channels[nChn].Period = channels[nChn].PortamentoDest;
                        }
                        if(channels[nChn].Period > channels[nChn].PortamentoDest) {
                            channels[nChn].Period -= channels[nChn].PortamentoSlide;
                            if(channels[nChn].Period < channels[nChn].PortamentoDest)
                                channels[nChn].Period = channels[nChn].PortamentoDest;
                        }
                    }
                    channels[nChn].Period += channels[nChn].FreqSlide;
                    if(channels[nChn].Period < 1) channels[nChn].Period = 1;
                    int period = channels[nChn].Period;
                    if(channels[nChn].Vibrato) {
                        switch(channels[nChn].VibratoType) {
                            case 1:
                                period += ModRampDownTable[channels[nChn].VibratoPos] * (channels[nChn].VibratoSlide & 0x0F) / 127;
                                break;
                            case 2:
                                period += ModSquareTable[channels[nChn].VibratoPos] * (channels[nChn].VibratoSlide & 0x0F) / 127;
                                break;
                            case 3:
                                period += ModRandomTable[channels[nChn].VibratoPos] * (channels[nChn].VibratoSlide & 0x0F) / 127;
                                break;
                            default:
                                period += ModSinusTable[channels[nChn].VibratoPos] * (channels[nChn].VibratoSlide & 0x0F) / 127;
                                break;
                        }
                        channels[nChn].VibratoPos = (channels[nChn].VibratoPos + (channels[nChn].VibratoSlide >> 4)) & 0x3F;
                    }
                    if(period < 1) period = 1;
                    channels[nChn].Inc = (uint)((channels[nChn].FineTune * MOD_AMIGAC2) / (period * mRate));
                } else {
                    channels[nChn].Inc = 0;
                    channels[nChn].Pos = 0;
                    channels[nChn].Length = 0;
                }
            }
            mBufferCount = (mRate * 5) / (mMusicTempo * 2);
            mSpeedCount--;
            return true;
        }

        public string GetName(int index) {
            return Encoding.UTF8.GetString(names[index]).Trim('\0');
        }
    }
}
