using System;

namespace SharpMod {
    public partial class SoundFile {
        private uint GetNumPatterns() {
            for(uint i = 0; i < 128; i++) if(mOrder[i] >= 64) return i;
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
                        nPattern = mOrder[nCurrentPattern];
                    }
                    if(nPattern >= mPatterns.Length) goto EndMod;

                    int inc = Type == Types.MOD ? 4 : 6;
                    int pIndex = (int)(nRow * ActiveChannels * inc);
                    byte[] p = mPatterns[nPattern];
                    for(uint nChn = 0; nChn < ActiveChannels; nChn++, pIndex += inc) {
                        uint command = 0xFF;
                        uint param = 0;

                        switch(Type) {
                            case Types.MOD:
                                command = (uint)(p[2] & 0x0F);
                                param = p[3];
                                break;
                            case Types.S3M:
                                param = p[5];
                                command = (uint)S3MTools.ConvertEffect((Effects)p[4], (int)param);
                                break;
                        }

                        switch((Effects)(command + (Type == Types.MOD ? 1 : 0))) {
                            // 0B: Position Jump
                            case Effects.CMD_POSITIONJUMP:
                                param &= 0x7F;
                                if(param <= nCurrentPattern) goto EndMod;
                                nNextPattern = param;
                                nRow = 0x3F;
                                break;
                            // 0B: Pattern Break
                            case Effects.CMD_PATTERNBREAK:
                                nRow = 0x3F;
                                break;
                            // 0F: Set Speed
                            case Effects.CMD_SPEED:
                                if((param != 0) && (param < 0x20)) nMusicSpeed = param;
                                else
                                    if(param >= 0x20) nMusicTempo = param;
                                break;
                        }
                    }
                    nSpeedCount = nMusicSpeed;
                }
                if(nPattern >= mPatterns.Length) goto EndMod;
                dwElapsedTime += 5000 / (nMusicTempo * 2);
                nSpeedCount--;
            }
        EndMod:
            return (dwElapsedTime + 500) / 1000;
        }

        private uint GetTotalPos() {
            uint nPos = 0;
            for(int i = 0; i < mOrder.Length; i++) {
                if(mOrder[i] != 0xFF) nPos += 64;
            }
            return nPos;
        }

        private void SetCurrentPos(uint nPos) {
            uint nPattern = nPos >> 6;
            uint nRow = nPos & 0x3F;
            if(nPattern > 127) nPattern = 0;
            if(nRow != 0) {
                CurrentPattern = nPattern;
                NextPattern = nPattern + 1;
                Pattern = mOrder[CurrentPattern];
                Row = nRow - 1;
            } else {
                CurrentPattern = nPattern;
                NextPattern = nPattern;
                Pattern = mOrder[CurrentPattern];
                Row = 0x3F;
            }
            BufferCount = 0;
        }

        public uint Read(byte[] lpBuffer, uint cbBuffer) {
            byte[] p = lpBuffer;
            uint lRead, lMax, lSampleSize;
            short adjustvol = (short)ActiveChannels;
            short[] CurrentVol = new short[32];
            byte[][] pSample = new byte[32][];
            bool[] bTrkDest = new bool[32];
            uint j;

            if(Type == Types.INVALID) return 0;
            lSampleSize = 1;
            if(Is16Bit) lSampleSize *= 2;
            if(IsStereo) lSampleSize *= 2;
            lMax = cbBuffer / lSampleSize;
            if((lMax == 0) || (p == null)) return 0;
            if(Type == Types.WAV) return (uint)(mFile.Read(lpBuffer, 0, (int)(lMax * lSampleSize)) / lSampleSize);

            // Memorize channels settings
            for(j = 0; j < ActiveChannels; j++) {
                CurrentVol[j] = mChannels[j].CurrentVolume;
                if(mChannels[j].Length != 0) {
                    pSample[j] = new byte[mChannels[j].Sample.Length];
                    Array.Copy(mChannels[j].Sample, pSample[j], mChannels[j].Sample.Length);
                }
                if(ActiveChannels == 4)
                    bTrkDest[j] = (((j & 3) == 1) || ((j & 3) == 2)) ? true : false;
                else
                    bTrkDest[j] = ((j & 1) != 0) ? false : true;
            }
            if(Pattern >= mPatterns.Length) return 0;

            // Fill audio buffer
            int pIndex = 0;
            for(lRead = 0; lRead < lMax; lRead++, pIndex += (int)lSampleSize) {
                if(BufferCount == 0) {
                    ReadNote();
                    // Memorize channels settings
                    for(j = 0; j < ActiveChannels; j++) {
                        CurrentVol[j] = mChannels[j].CurrentVolume;
                        if(mChannels[j].Length != 0) {
                            pSample[j] = new byte[mChannels[j].Sample.Length];
                            Array.Copy(mChannels[j].Sample, pSample[j], mChannels[j].Sample.Length);
                        } else {
                            pSample[j] = null;
                        }
                    }
                }
                BufferCount--;

                int vRight = 0, vLeft = 0;
                for(uint i = 0; i < ActiveChannels; i++) if(pSample[i] != null) {
                    // Read sample
                    int poshi = (int)(mChannels[i].Pos >> MOD_PRECISION);
                    if((poshi + 1) >= pSample[i].Length) continue; // Until S3M's FineTune is correctly set, this will overflow...
                    short poslo = (short)(mChannels[i].Pos & MOD_FRACMASK);
                    short srcvol = (sbyte)pSample[i][poshi];
                    short destvol = (sbyte)pSample[i][poshi + 1];
                    int vol = srcvol + ((int)(poslo * (destvol - srcvol)) >> MOD_PRECISION);
                    vol *= CurrentVol[i];
                    if(bTrkDest[i]) vRight += vol; else vLeft += vol;
                    mChannels[i].OldVol = vol;
                    mChannels[i].Pos += mChannels[i].Inc;
                    if(mChannels[i].Pos >= mChannels[i].Length) {
                        mChannels[i].Length = mChannels[i].LoopEnd;
                        mChannels[i].Pos = (mChannels[i].Pos & MOD_FRACMASK) + mChannels[i].LoopStart;
                        if(mChannels[i].Length != 0) pSample[i] = null;
                    }
                } else {
                    int vol = mChannels[i].OldVol;
                    if(bTrkDest[i]) vRight += vol; else vLeft += vol;
                }

                // Sample ready
                if(IsStereo) {
                    // Stereo - Surround
                    int vol = vRight;
                    vRight = (vRight * 13 + vLeft * 3) / (adjustvol * 8);
                    vLeft = (vLeft * 13 + vol * 3) / (adjustvol * 8);
                    if(Is16Bit) {
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
                    if(Is16Bit) {
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
            if(SpeedCount == 0) {
                Row = (Row + 1) & 0x3F;
                if(Row == 0) {
                    CurrentPattern = NextPattern;
                    NextPattern++;
                    Pattern = mOrder[CurrentPattern];
                }
                if(Pattern >= mPatterns.Length) {
                    if(Type == Types.MOD) {
                        MusicSpeed = 6;
                        MusicTempo = 125;
                    }
                    if(!Loop) {
                        BufferCount = (Rate * 5) / (MusicTempo * 2);
                        return false;
                    }
                    CurrentPattern = 0;
                    NextPattern = 1;
                    Pattern = mOrder[CurrentPattern];
                }

                int inc = Type == Types.MOD ? 4 : 6;
                int pIndex = (int)(Row * ActiveChannels * inc);
                byte[] p = mPatterns[Pattern];
                for(int i = 0; (i < ActiveChannels) && (pIndex < p.Length); i++, pIndex += inc) {
                    uint period;
                    uint instIdx;
                    uint command;
                    uint param;
                    int chnIdx;

                    if(Type == Types.S3M) { // S3M
                        int mode = p[pIndex + 0];
                        if(mode == 0) continue;

                        period = 0;
                        instIdx = 0;
                        command = 0xFF;
                        param = 0;
                        chnIdx = mode & 0x1F;

                        if((mode & 0x20) != 0) {
                            instIdx = p[pIndex + 2];

                            int note = p[pIndex + 1];
                            if(note < 0xF0) {
                                if((note >= 0xFB) && (note <= 0xFF)) note = 0;
                                int octave = note >> 4;
                                int semitone = note & 0x0F;
                                note = semitone + 12 * octave + 12;// + 1;

                                double f = Math.Pow(2.0, (note - 69.0) / 12.0) * 440.0;
                                period = (uint)(Rate / f);
                            }
                        };

                        //if((mode & 0x40) != 0) mChannels[chnIdx].Volume = p[pIndex + 3] << 2;

                        if((mode & 0x40) != 0) {
                            mChannels[chnIdx].Volume = p[pIndex + 3] << 2;
                        } else {
                            if(instIdx < mInstruments.Length) mChannels[chnIdx].Volume = mInstruments[instIdx].Volume;
                        }

                        if((mode & 0x80) != 0) {
                            command = p[pIndex + 4];
                            param = p[pIndex + 5];
                            command = (uint)S3MTools.ConvertEffect((Effects)command, (int)param);
                        }
                    } else { // MOD
                        chnIdx = i;
                        byte A0 = p[pIndex + 0], A1 = p[pIndex + 1], A2 = p[pIndex + 2], A3 = p[pIndex + 3];
                        period = (((uint)A0 & 0x0F) << 8) | (A1);
                        instIdx = ((uint)A2 >> 4) | (uint)(A0 & 0x10);
                        command = (uint)(A2 & 0x0F) + 1; // The +1 adapts the MOD effects to the S3M effects
                        param = A3;
                    }
                    bool bVib = mChannels[chnIdx].Vibrato;
                    bool bTrem = mChannels[chnIdx].Tremolo;

                    // Reset channels data
                    mChannels[chnIdx].VolumeSlide = 0;
                    mChannels[chnIdx].FreqSlide = 0;
                    mChannels[chnIdx].OldPeriod = mChannels[chnIdx].Period;
                    mChannels[chnIdx].Portamento = false;
                    mChannels[chnIdx].Vibrato = false;
                    mChannels[chnIdx].Tremolo = false;
                    if(instIdx >= (mInstruments.Length - 1)) instIdx = 0;
                    if(instIdx != 0) mChannels[chnIdx].NextInstrumentIndex = (short)instIdx;
                    if(period != 0) {
                        if(mChannels[chnIdx].NextInstrumentIndex != 0) {
                            mChannels[chnIdx].InstrumentIndex = instIdx;
                            if(Type == Types.MOD) mChannels[chnIdx].Volume = mInstruments[instIdx].Volume;
                            mChannels[chnIdx].Pos = 0;
                            mChannels[chnIdx].Length = mInstruments[instIdx].Length << MOD_PRECISION;
                            mChannels[chnIdx].FineTune = mInstruments[instIdx].FineTune << MOD_PRECISION;
                            mChannels[chnIdx].LoopStart = mInstruments[instIdx].LoopStart << MOD_PRECISION;
                            mChannels[chnIdx].LoopEnd = mInstruments[instIdx].LoopEnd << MOD_PRECISION;
                            mChannels[chnIdx].Sample = mInstruments[instIdx].Sample;
                            mChannels[chnIdx].NextInstrumentIndex = 0;
                        }
                        if(((Effects)command != Effects.CMD_TONEPORTAMENTO) || (mChannels[chnIdx].Period == 0)) {
                            mChannels[chnIdx].Period = (int)period;
                            mChannels[chnIdx].Length = mInstruments[mChannels[chnIdx].InstrumentIndex].Length << MOD_PRECISION;
                            mChannels[chnIdx].Pos = 0;
                        }
                        mChannels[chnIdx].PortamentoDest = (int)period;
                    }
                    switch((Effects)command) {
                        case Effects.CMD_ARPEGGIO:
                            if((param == 0) || (mChannels[chnIdx].Period == 0)) break;
                            mChannels[chnIdx].Count2 = 3;
                            mChannels[chnIdx].Period2 = mChannels[chnIdx].Period;
                            mChannels[chnIdx].Count1 = 2;
                            mChannels[chnIdx].Period1 = (int)(mChannels[chnIdx].Period + (param & 0x0F));
                            mChannels[chnIdx].Period += (int)((param >> 4) & 0x0F);
                            break;
                        case Effects.CMD_PORTAMENTOUP:
                            if(param == 0) param = (uint)mChannels[chnIdx].OldFreqSlide;
                            mChannels[chnIdx].OldFreqSlide = (int)param;
                            mChannels[chnIdx].FreqSlide = -(int)param;
                            break;
                        case Effects.CMD_PORTAMENTODOWN:
                            if(param == 0) param = (uint)mChannels[chnIdx].OldFreqSlide;
                            mChannels[chnIdx].OldFreqSlide = (int)param;
                            mChannels[chnIdx].FreqSlide = (int)param;
                            break;
                        case Effects.CMD_TONEPORTAMENTO:
                            if(param == 0) param = (uint)mChannels[chnIdx].PortamentoSlide;
                            mChannels[chnIdx].PortamentoSlide = (int)param;
                            mChannels[chnIdx].Portamento = true;
                            break;
                        case Effects.CMD_VIBRATO:
                            if(!bVib) mChannels[chnIdx].VibratoPos = 0;
                            if(param == 0) mChannels[chnIdx].VibratoSlide = (int)param;
                            mChannels[chnIdx].Vibrato = true;
                            break;
                        case Effects.CMD_TONEPORTAVOL:
                            if(period != 0) {
                                mChannels[chnIdx].PortamentoDest = (int)period;
                                if(mChannels[chnIdx].OldPeriod != 0) mChannels[chnIdx].Period = mChannels[chnIdx].OldPeriod;
                            }
                            mChannels[chnIdx].Portamento = true;
                            if(param != 0) {
                                if((param & 0xF0) != 0) mChannels[chnIdx].VolumeSlide = (int)((param >> 4) << 2);
                                else mChannels[chnIdx].VolumeSlide = -(int)((param & 0x0F) << 2);
                                mChannels[chnIdx].OldVolumeSlide = mChannels[chnIdx].VolumeSlide;
                            }
                            break;
                        case Effects.CMD_VIBRATOVOL:
                            if(!bVib) mChannels[chnIdx].VibratoPos = 0;
                            mChannels[chnIdx].Vibrato = true;
                            if(param != 0) {
                                if((param & 0xF0) != 0) mChannels[chnIdx].VolumeSlide = (int)((param >> 4) << 2);
                                else mChannels[chnIdx].VolumeSlide = -(int)((param & 0x0F) << 2);
                                mChannels[chnIdx].OldVolumeSlide = mChannels[chnIdx].VolumeSlide;
                            }
                            break;
                        case Effects.CMD_TREMOLO:
                            if(!bTrem) mChannels[chnIdx].TremoloPos = 0;
                            if(param == 0) mChannels[chnIdx].TremoloSlide = (int)param;
                            mChannels[chnIdx].Tremolo = true;
                            break;
                        case Effects.CMD_PANNING8:
                            // Not Implemented
                            mChannels[chnIdx].Volume = (int)(param << 2);
                            break;
                        case Effects.CMD_OFFSET:
                            if(param > 0) {
                                param <<= 8 + MOD_PRECISION;
                                if(param < mChannels[chnIdx].Length) mChannels[chnIdx].Pos = param;
                            }
                            break;
                        case Effects.CMD_VOLUMESLIDE:
                            if(param != 0) {
                                if((param & 0xF0) != 0) mChannels[chnIdx].VolumeSlide = (int)((param >> 4) << 2);
                                else mChannels[chnIdx].VolumeSlide = -(int)((param & 0x0F) << 2);
                                mChannels[chnIdx].OldVolumeSlide = mChannels[chnIdx].VolumeSlide;
                            }
                            break;
                        case Effects.CMD_POSITIONJUMP:
                            param &= 0x7F;
                            NextPattern = param;
                            Row = 0x3F;
                            break;
                        case Effects.CMD_VOLUME:
                            if(param > 0x40) param = 0x40;
                            param <<= 2;
                            mChannels[chnIdx].Volume = (int)param;
                            break;
                        case Effects.CMD_PATTERNBREAK:
                            Row = 0x3F;
                            break;
                        // 0E: Extended Effects
                        case Effects.CMD_RETRIG:
                            command = param >> 4;
                            param &= 0x0F;
                            switch(command) {
                                // 0xE1: Fine Portamento Up
                                case 0x01:
                                    if(mChannels[chnIdx].Period != 0) {
                                        mChannels[chnIdx].Period -= (int)param;
                                        if(mChannels[chnIdx].Period < 1) mChannels[chnIdx].Period = 1;
                                    }
                                    break;
                                // 0xE2: Fine Portamento Down
                                case 0x02:
                                    if(mChannels[chnIdx].Period != 0) {
                                        mChannels[chnIdx].Period += (int)param;
                                    }
                                    break;
                                // 0xE3: Set Glissando Control (???)
                                // 0xE4: Set Vibrato WaveForm
                                case 0x04:
                                    mChannels[chnIdx].VibratoType = (int)(param & 0x03);
                                    break;
                                // 0xE5: Set Finetune
                                case 0x05:
                                    mChannels[chnIdx].FineTune = FineTuneTable[param];
                                    break;
                                // 0xE6: Pattern Loop
                                // 0xE7: Set Tremolo WaveForm
                                case 0x07:
                                    mChannels[chnIdx].TremoloType = (int)(param & 0x03);
                                    break;
                                // 0xE9: Retrig + Fine Volume Slide
                                // 0xEA: Fine Volume Up
                                case 0x0A:
                                    mChannels[chnIdx].Volume += (int)(param << 2);
                                    break;
                                // 0xEB: Fine Volume Down
                                case 0x0B:
                                    mChannels[chnIdx].Volume -= (int)(param << 2);
                                    break;
                                // 0xEC: Note Cut
                                case 0x0C:
                                    mChannels[chnIdx].Count1 = (int)(param + 1);
                                    mChannels[chnIdx].Period1 = 0;
                                    break;
                            }
                            break;
                        case Effects.CMD_SPEED:
                            if((param != 0) && (param < 0x20)) MusicSpeed = param;
                            else
                                if(param >= 0x20) MusicTempo = param;
                            break;
                    }
                }
                SpeedCount = MusicSpeed;
            }

            if(Pattern >= mPatterns.Length) return false;

            // Update channels data
            for(uint nChn = 0; nChn < ActiveChannels; nChn++) {
                mChannels[nChn].Volume += mChannels[nChn].VolumeSlide;
                if(mChannels[nChn].Volume < 0) mChannels[nChn].Volume = 0;
                if(mChannels[nChn].Volume > 0x100) mChannels[nChn].Volume = 0x100;
                if(mChannels[nChn].Count1 != 0) {
                    mChannels[nChn].Count1--;
                    if(mChannels[nChn].Count1 == 0) mChannels[nChn].Period = mChannels[nChn].Period1;
                }
                if(mChannels[nChn].Count2 != 0) {
                    mChannels[nChn].Count2--;
                    if(mChannels[nChn].Count2 == 0) mChannels[nChn].Period = mChannels[nChn].Period2;
                }
                if(mChannels[nChn].Period != 0) {
                    mChannels[nChn].CurrentVolume = (short)mChannels[nChn].Volume;
                    if(mChannels[nChn].Tremolo) {
                        int vol = mChannels[nChn].CurrentVolume;
                        switch(mChannels[nChn].TremoloType) {
                            case 1:
                                vol += ModRampDownTable[mChannels[nChn].TremoloPos] * (mChannels[nChn].TremoloSlide & 0x0F) / 127;
                                break;
                            case 2:
                                vol += ModSquareTable[mChannels[nChn].TremoloPos] * (mChannels[nChn].TremoloSlide & 0x0F) / 127;
                                break;
                            case 3:
                                vol += ModRandomTable[mChannels[nChn].TremoloPos] * (mChannels[nChn].TremoloSlide & 0x0F) / 127;
                                break;
                            default:
                                vol += ModSinusTable[mChannels[nChn].TremoloPos] * (mChannels[nChn].TremoloSlide & 0x0F) / 127;
                                break;
                        }
                        if(vol < 0) vol = 0;
                        if(vol > 0x100) vol = 0x100;
                        mChannels[nChn].CurrentVolume = (short)vol;
                        mChannels[nChn].TremoloPos = (mChannels[nChn].TremoloPos + (mChannels[nChn].TremoloSlide >> 4)) & 0x3F;
                    }
                    if((mChannels[nChn].Portamento) && (mChannels[nChn].PortamentoDest != 0)) {
                        if(mChannels[nChn].Period < mChannels[nChn].PortamentoDest) {
                            mChannels[nChn].Period += mChannels[nChn].PortamentoSlide;
                            if(mChannels[nChn].Period > mChannels[nChn].PortamentoDest)
                                mChannels[nChn].Period = mChannels[nChn].PortamentoDest;
                        }
                        if(mChannels[nChn].Period > mChannels[nChn].PortamentoDest) {
                            mChannels[nChn].Period -= mChannels[nChn].PortamentoSlide;
                            if(mChannels[nChn].Period < mChannels[nChn].PortamentoDest)
                                mChannels[nChn].Period = mChannels[nChn].PortamentoDest;
                        }
                    }
                    mChannels[nChn].Period += mChannels[nChn].FreqSlide;
                    if(mChannels[nChn].Period < 1) mChannels[nChn].Period = 1;
                    int period = mChannels[nChn].Period;
                    if(mChannels[nChn].Vibrato) {
                        switch(mChannels[nChn].VibratoType) {
                            case 1:
                                period += ModRampDownTable[mChannels[nChn].VibratoPos] * (mChannels[nChn].VibratoSlide & 0x0F) / 127;
                                break;
                            case 2:
                                period += ModSquareTable[mChannels[nChn].VibratoPos] * (mChannels[nChn].VibratoSlide & 0x0F) / 127;
                                break;
                            case 3:
                                period += ModRandomTable[mChannels[nChn].VibratoPos] * (mChannels[nChn].VibratoSlide & 0x0F) / 127;
                                break;
                            default:
                                period += ModSinusTable[mChannels[nChn].VibratoPos] * (mChannels[nChn].VibratoSlide & 0x0F) / 127;
                                break;
                        }
                        mChannels[nChn].VibratoPos = (mChannels[nChn].VibratoPos + (mChannels[nChn].VibratoSlide >> 4)) & 0x3F;
                    }
                    if(period < 1) period = 1;
                    mChannels[nChn].Inc = (uint)((mChannels[nChn].FineTune * MOD_AMIGAC2) / (period * Rate));
                } else {
                    mChannels[nChn].Inc = 0;
                    mChannels[nChn].Pos = 0;
                    mChannels[nChn].Length = 0;
                }
            }
            BufferCount = (Rate * 5) / (MusicTempo * 2);
            SpeedCount--;
            return true;
        }
    }
}
