using System;
using System.Diagnostics;

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
                    if(nPattern >= patterns.Length) goto EndMod;

                    int pIndex = (int)(nRow * ActiveChannels * 4);
                    byte[] p = patterns[nPattern];
                    for(uint nChn = 0; nChn < ActiveChannels; nChn++, pIndex += 4) {
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
                if(nPattern >= patterns.Length) goto EndMod;
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
                CurrentPattern = nPattern;
                NextPattern = nPattern + 1;
                Pattern = order[CurrentPattern];
                Row = nRow - 1;
            } else {
                CurrentPattern = nPattern;
                NextPattern = nPattern;
                Pattern = order[CurrentPattern];
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

            if(Type == 0) return 0;
            lSampleSize = 1;
            if(Is16Bit) lSampleSize *= 2;
            if(IsStereo) lSampleSize *= 2;
            lMax = cbBuffer / lSampleSize;
            if((lMax == 0) || (p == null)) return 0;
            if(Type == 1) return (uint)(mFile.Read(lpBuffer, 0, (int)(lMax * lSampleSize)) / lSampleSize);

            // Memorize channels settings
            for(j = 0; j < ActiveChannels; j++) {
                CurrentVol[j] = Channels[j].CurrentVolume;
                if(Channels[j].Length != 0) {
                    pSample[j] = new byte[Channels[j].Sample.Length];
                    Array.Copy(Channels[j].Sample, pSample[j], Channels[j].Sample.Length);
                }
                if(ActiveChannels == 4)
                    bTrkDest[j] = (((j & 3) == 1) || ((j & 3) == 2)) ? true : false;
                else
                    bTrkDest[j] = ((j & 1) != 0) ? false : true;
            }
            if(Pattern >= patterns.Length) return 0;

            // Fill audio buffer
            int pIndex = 0;
            for(lRead = 0; lRead < lMax; lRead++, pIndex += (int)lSampleSize) {
                if(BufferCount == 0) {
                    ReadNote();
                    // Memorize channels settings
                    for(j = 0; j < ActiveChannels; j++) {
                        CurrentVol[j] = Channels[j].CurrentVolume;
                        if(Channels[j].Length != 0) {
                            pSample[j] = new byte[Channels[j].Sample.Length];
                            Array.Copy(Channels[j].Sample, pSample[j], Channels[j].Sample.Length);
                        } else {
                            pSample[j] = null;
                        }
                    }
                }
                BufferCount--;

                int vRight = 0, vLeft = 0;
                for(uint i = 0; i < ActiveChannels; i++) if(pSample[i] != null) {
                        // Read sample
                        int poshi = (int)(Channels[i].Pos >> MOD_PRECISION);
                        if((poshi + 1) >= pSample[i].Length) continue; // Until S3M's FineTune is correctly set, this will overflow...
                        short poslo = (short)(Channels[i].Pos & MOD_FRACMASK);
                        short srcvol = (sbyte)pSample[i][poshi];
                        short destvol = (sbyte)pSample[i][poshi + 1];
                        int vol = srcvol + ((int)(poslo * (destvol - srcvol)) >> MOD_PRECISION);
                        vol *= CurrentVol[i];
                        if(bTrkDest[i]) vRight += vol; else vLeft += vol;
                        Channels[i].OldVol = vol;
                        Channels[i].Pos += Channels[i].Inc;
                        if(Channels[i].Pos >= Channels[i].Length) {
                            Channels[i].Length = Channels[i].LoopEnd;
                            Channels[i].Pos = (Channels[i].Pos & MOD_FRACMASK) + Channels[i].LoopStart;
                            if(Channels[i].Length != 0) pSample[i] = null;
                        }
                    } else {
                        int vol = Channels[i].OldVol;
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
                    Pattern = order[CurrentPattern];
                }
                if(Pattern >= patterns.Length) {
                    if(Type == 2) {
                        MusicSpeed = 6;
                        MusicTempo = 125;
                    }
                    if(!Loop) {
                        BufferCount = (Rate * 5) / (MusicTempo * 2);
                        return false;
                    }
                    CurrentPattern = 0;
                    NextPattern = 1;
                    Pattern = order[CurrentPattern];
                }
                int inc = Type == 2 ? 4 : 6;
                int pIndex = (int)(Row * ActiveChannels * inc);
                byte[] p = patterns[Pattern];
                for(int i = 0; (i < ActiveChannels) && (pIndex < p.Length); i++, pIndex += inc) {
                    uint period;
                    uint instIdx;
                    uint command;
                    uint param;
                    int chnIdx;

                    if(Type == 3) { // S3M
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
                                int octave = note >> 4;
                                int semitone = note & 0x0F;
                                note = semitone + 12 * octave + 12 + 1;

                                double f = Math.Pow(2.0, (note - 69.0) / 12.0) * 440.0;
                                period = (uint)(Rate / f);
                            }
                        };

                        if((mode & 0x40) != 0) {
                            Channels[chnIdx].Volume = p[pIndex + 3] << 2;
                        } else {
                            if(instIdx < Instruments.Length) Channels[chnIdx].Volume = Instruments[instIdx].Volume;
                        }

                        if((mode & 0x80) != 0) {
                            command = p[pIndex + 4];
                            command = (uint)S3MTools.ConvertEffect((Effects)command);
                            param = p[pIndex + 5];
                        }
                    } else { // MOD
                        chnIdx = i;
                        byte A0 = p[pIndex + 0], A1 = p[pIndex + 1], A2 = p[pIndex + 2], A3 = p[pIndex + 3];
                        period = (((uint)A0 & 0x0F) << 8) | (A1);
                        instIdx = ((uint)A2 >> 4) | (uint)(A0 & 0x10);
                        command = (uint)(A2 & 0x0F);
                        param = A3;
                    }
                    bool bVib = Channels[chnIdx].Vibrato;
                    bool bTrem = Channels[chnIdx].Tremolo;

                    // Reset channels data
                    Channels[chnIdx].VolumeSlide = 0;
                    Channels[chnIdx].FreqSlide = 0;
                    Channels[chnIdx].OldPeriod = Channels[chnIdx].Period;
                    Channels[chnIdx].Portamento = false;
                    Channels[chnIdx].Vibrato = false;
                    Channels[chnIdx].Tremolo = false;
                    if(instIdx >= (Instruments.Length - 1)) instIdx = 0;
                    if(instIdx != 0) Channels[chnIdx].NextInstrumentIndex = (short)instIdx;
                    if(period != 0) {
                        if(Channels[chnIdx].NextInstrumentIndex != 0) {
                            Channels[chnIdx].InstrumentIndex = instIdx;
                            if(Type == 2) Channels[chnIdx].Volume = Instruments[instIdx].Volume;
                            Channels[chnIdx].Pos = 0;
                            Channels[chnIdx].Length = Instruments[instIdx].Length << MOD_PRECISION;
                            Channels[chnIdx].FineTune = Instruments[instIdx].FineTune << MOD_PRECISION;
                            Channels[chnIdx].LoopStart = Instruments[instIdx].LoopStart << MOD_PRECISION;
                            Channels[chnIdx].LoopEnd = Instruments[instIdx].LoopEnd << MOD_PRECISION;
                            Channels[chnIdx].Sample = Instruments[instIdx].Sample;
                            Channels[chnIdx].NextInstrumentIndex = 0;
                        }
                        if((command != 0x03) || (Channels[chnIdx].Period == 0)) {
                            Channels[chnIdx].Period = (int)period;
                            Channels[chnIdx].Length = Instruments[Channels[chnIdx].InstrumentIndex].Length << MOD_PRECISION;
                            Channels[chnIdx].Pos = 0;
                        }
                        Channels[chnIdx].PortamentoDest = (int)period;
                    }
                    switch((Effects)(command + 1)) {
                        // 00: Arpeggio
                        case Effects.CMD_ARPEGGIO:
                            if((param == 0) || (Channels[chnIdx].Period == 0)) break;
                            Channels[chnIdx].Count2 = 3;
                            Channels[chnIdx].Period2 = Channels[chnIdx].Period;
                            Channels[chnIdx].Count1 = 2;
                            Channels[chnIdx].Period1 = (int)(Channels[chnIdx].Period + (param & 0x0F));
                            Channels[chnIdx].Period += (int)((param >> 4) & 0x0F);
                            break;
                        // 01: Portamento Up
                        case Effects.CMD_PORTAMENTOUP:
                            if(param == 0) param = (uint)Channels[chnIdx].OldFreqSlide;
                            Channels[chnIdx].OldFreqSlide = (int)param;
                            Channels[chnIdx].FreqSlide = -(int)param;
                            break;
                        // 02: Portamento Down
                        case Effects.CMD_PORTAMENTODOWN:
                            if(param == 0) param = (uint)Channels[chnIdx].OldFreqSlide;
                            Channels[chnIdx].OldFreqSlide = (int)param;
                            Channels[chnIdx].FreqSlide = (int)param;
                            break;
                        // 03: Tone-Portamento
                        case Effects.CMD_TONEPORTAMENTO:
                            if(param == 0) param = (uint)Channels[chnIdx].PortamentoSlide;
                            Channels[chnIdx].PortamentoSlide = (int)param;
                            Channels[chnIdx].Portamento = false;
                            break;
                        // 04: Vibrato
                        case Effects.CMD_VIBRATO:
                            if(!bVib) Channels[chnIdx].VibratoPos = 0;
                            if(param == 0) Channels[chnIdx].VibratoSlide = (int)param;
                            Channels[chnIdx].Vibrato = false;
                            break;
                        // 05: Tone-Portamento + Volume Slide
                        case Effects.CMD_TONEPORTAVOL:
                            if(period != 0) {
                                Channels[chnIdx].PortamentoDest = (int)period;
                                if(Channels[chnIdx].OldPeriod != 0) Channels[chnIdx].Period = Channels[chnIdx].OldPeriod;
                            }
                            Channels[chnIdx].Portamento = false;
                            if(param != 0) {
                                if((param & 0xF0) != 0) Channels[chnIdx].VolumeSlide = (int)((param >> 4) << 2);
                                else Channels[chnIdx].VolumeSlide = -(int)((param & 0x0F) << 2);
                                Channels[chnIdx].OldVolumeSlide = Channels[chnIdx].VolumeSlide;
                            }
                            break;
                        // 06: Vibrato + Volume Slide
                        case Effects.CMD_VIBRATOVOL:
                            if(!bVib) Channels[chnIdx].VibratoPos = 0;
                            Channels[chnIdx].Vibrato = false;
                            if(param != 0) {
                                if((param & 0xF0) != 0) Channels[chnIdx].VolumeSlide = (int)((param >> 4) << 2);
                                else Channels[chnIdx].VolumeSlide = -(int)((param & 0x0F) << 2);
                                Channels[chnIdx].OldVolumeSlide = Channels[chnIdx].VolumeSlide;
                            }
                            break;
                        // 07: Tremolo
                        case Effects.CMD_TREMOLO:
                            if(!bTrem) Channels[chnIdx].TremoloPos = 0;
                            if(param == 0) Channels[chnIdx].TremoloSlide = (int)param;
                            Channels[chnIdx].Tremolo = false;
                            break;
                        // 09: Set Offset
                        case Effects.CMD_OFFSET:
                            if(param > 0) {
                                param <<= 8 + MOD_PRECISION;
                                if(param < Channels[chnIdx].Length) Channels[chnIdx].Pos = param;
                            }
                            break;
                        // 0A: Volume Slide
                        case Effects.CMD_VOLUMESLIDE:
                            if(param != 0) {
                                if((param & 0xF0) != 0) Channels[chnIdx].VolumeSlide = (int)((param >> 4) << 2);
                                else Channels[chnIdx].VolumeSlide = -(int)((param & 0x0F) << 2);
                                Channels[chnIdx].OldVolumeSlide = Channels[chnIdx].VolumeSlide;
                            }
                            break;
                        // 0B: Position Jump
                        case Effects.CMD_POSITIONJUMP:
                            param &= 0x7F;
                            NextPattern = param;
                            Row = 0x3F;
                            break;
                        // 0C: Set Volume
                        case Effects.CMD_VOLUME:
                            if(param > 0x40) param = 0x40;
                            param <<= 2;
                            Channels[chnIdx].Volume = (int)param;
                            break;
                        // 0B: Pattern Break
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
                                    if(Channels[chnIdx].Period != 0) {
                                        Channels[chnIdx].Period -= (int)param;
                                        if(Channels[chnIdx].Period < 1) Channels[chnIdx].Period = 1;
                                    }
                                    break;
                                // 0xE2: Fine Portamento Down
                                case 0x02:
                                    if(Channels[chnIdx].Period != 0) {
                                        Channels[chnIdx].Period += (int)param;
                                    }
                                    break;
                                // 0xE3: Set Glissando Control (???)
                                // 0xE4: Set Vibrato WaveForm
                                case 0x04:
                                    Channels[chnIdx].VibratoType = (int)(param & 0x03);
                                    break;
                                // 0xE5: Set Finetune
                                case 0x05:
                                    Channels[chnIdx].FineTune = FineTuneTable[param];
                                    break;
                                // 0xE6: Pattern Loop
                                // 0xE7: Set Tremolo WaveForm
                                case 0x07:
                                    Channels[chnIdx].TremoloType = (int)(param & 0x03);
                                    break;
                                // 0xE9: Retrig + Fine Volume Slide
                                // 0xEA: Fine Volume Up
                                case 0x0A:
                                    Channels[chnIdx].Volume += (int)(param << 2);
                                    break;
                                // 0xEB: Fine Volume Down
                                case 0x0B:
                                    Channels[chnIdx].Volume -= (int)(param << 2);
                                    break;
                                // 0xEC: Note Cut
                                case 0x0C:
                                    Channels[chnIdx].Count1 = (int)(param + 1);
                                    Channels[chnIdx].Period1 = 0;
                                    break;
                            }
                            break;
                        // 0F: Set Speed
                        case Effects.CMD_SPEED:
                            if((param != 0) && (param < 0x20)) MusicSpeed = param;
                            else
                                if(param >= 0x20) MusicTempo = param;
                            break;
                    }
                }
                SpeedCount = MusicSpeed;
            }

            if(Pattern >= patterns.Length) return false;

            // Update channels data
            for(uint nChn = 0; nChn < ActiveChannels; nChn++) {
                Channels[nChn].Volume += Channels[nChn].VolumeSlide;
                if(Channels[nChn].Volume < 0) Channels[nChn].Volume = 0;
                if(Channels[nChn].Volume > 0x100) Channels[nChn].Volume = 0x100;
                if(Channels[nChn].Count1 != 0) {
                    Channels[nChn].Count1--;
                    if(Channels[nChn].Count1 == 0) Channels[nChn].Period = Channels[nChn].Period1;
                }
                if(Channels[nChn].Count2 != 0) {
                    Channels[nChn].Count2--;
                    if(Channels[nChn].Count2 == 0) Channels[nChn].Period = Channels[nChn].Period2;
                }
                if(Channels[nChn].Period != 0) {
                    Channels[nChn].CurrentVolume = (short)Channels[nChn].Volume;
                    if(Channels[nChn].Tremolo) {
                        int vol = Channels[nChn].CurrentVolume;
                        switch(Channels[nChn].TremoloType) {
                            case 1:
                                vol += ModRampDownTable[Channels[nChn].TremoloPos] * (Channels[nChn].TremoloSlide & 0x0F) / 127;
                                break;
                            case 2:
                                vol += ModSquareTable[Channels[nChn].TremoloPos] * (Channels[nChn].TremoloSlide & 0x0F) / 127;
                                break;
                            case 3:
                                vol += ModRandomTable[Channels[nChn].TremoloPos] * (Channels[nChn].TremoloSlide & 0x0F) / 127;
                                break;
                            default:
                                vol += ModSinusTable[Channels[nChn].TremoloPos] * (Channels[nChn].TremoloSlide & 0x0F) / 127;
                                break;
                        }
                        if(vol < 0) vol = 0;
                        if(vol > 0x100) vol = 0x100;
                        Channels[nChn].CurrentVolume = (short)vol;
                        Channels[nChn].TremoloPos = (Channels[nChn].TremoloPos + (Channels[nChn].TremoloSlide >> 4)) & 0x3F;
                    }
                    if((Channels[nChn].Portamento) && (Channels[nChn].PortamentoDest != 0)) {
                        if(Channels[nChn].Period < Channels[nChn].PortamentoDest) {
                            Channels[nChn].Period += Channels[nChn].PortamentoSlide;
                            if(Channels[nChn].Period > Channels[nChn].PortamentoDest)
                                Channels[nChn].Period = Channels[nChn].PortamentoDest;
                        }
                        if(Channels[nChn].Period > Channels[nChn].PortamentoDest) {
                            Channels[nChn].Period -= Channels[nChn].PortamentoSlide;
                            if(Channels[nChn].Period < Channels[nChn].PortamentoDest)
                                Channels[nChn].Period = Channels[nChn].PortamentoDest;
                        }
                    }
                    Channels[nChn].Period += Channels[nChn].FreqSlide;
                    if(Channels[nChn].Period < 1) Channels[nChn].Period = 1;
                    int period = Channels[nChn].Period;
                    if(Channels[nChn].Vibrato) {
                        switch(Channels[nChn].VibratoType) {
                            case 1:
                                period += ModRampDownTable[Channels[nChn].VibratoPos] * (Channels[nChn].VibratoSlide & 0x0F) / 127;
                                break;
                            case 2:
                                period += ModSquareTable[Channels[nChn].VibratoPos] * (Channels[nChn].VibratoSlide & 0x0F) / 127;
                                break;
                            case 3:
                                period += ModRandomTable[Channels[nChn].VibratoPos] * (Channels[nChn].VibratoSlide & 0x0F) / 127;
                                break;
                            default:
                                period += ModSinusTable[Channels[nChn].VibratoPos] * (Channels[nChn].VibratoSlide & 0x0F) / 127;
                                break;
                        }
                        Channels[nChn].VibratoPos = (Channels[nChn].VibratoPos + (Channels[nChn].VibratoSlide >> 4)) & 0x3F;
                    }
                    if(period < 1) period = 1;
                    Channels[nChn].Inc = (uint)((Channels[nChn].FineTune * MOD_AMIGAC2) / (period * Rate));
                } else {
                    Channels[nChn].Inc = 0;
                    Channels[nChn].Pos = 0;
                    Channels[nChn].Length = 0;
                }
            }
            BufferCount = (Rate * 5) / (MusicTempo * 2);
            SpeedCount--;
            return true;
        }
    }
}
