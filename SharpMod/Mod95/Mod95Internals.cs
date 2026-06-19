using System;

namespace SharpMod {
    public partial class SoundFile {
        private uint GetNumPatterns() {
            for(uint i = 0; i < 128; i++) if(order[i] >= 64) return i;
            return 128;
        }

        private uint GetLength() {
            uint dwElapsedTime = 0, nRow = 0, nSpeedCount = 0, nCurrentPattern = 0, nNextPattern = 0, nPattern = 0;
            uint nMusicSpeed = 6, nMusicTempo = 125;

            for(; ;) {
                if(nSpeedCount == 0) {
                    nRow = (nRow + 1) & 0x3F;
                    if(nRow == 0) {
                        nCurrentPattern = nNextPattern;
                        nNextPattern++;
                        nPattern = order[nCurrentPattern];
                    }
                    if(nPattern >= patterns.Length) goto EndMod;

                    int inc = Type == Types.MOD ? 4 : 6;
                    int pIndex = (int)(nRow * ActiveChannels * inc);
                    byte[] p = patterns[nPattern];
                    for(uint nChn = 0; nChn < ActiveChannels; nChn++, pIndex += inc) {
                        uint command = 0xFF;
                        uint param = 0;

                        switch(Type) {
                            case Types.MOD:
                                command = (uint)(p[2] & 0x0F);
                                param = p[3];
                                break;
                            case Types.S3M:
                            case Types.XM:
                            case Types.STM:
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
                                else if(param >= 0x20) nMusicTempo = param;
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

        private uint GetTotalPos() {
            uint nPos = 0;
            for(int i = 0; i < order.Length; i++) {
                if(order[i] != 0xFF) nPos += 64;
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
            // Per-mix attenuation follows OpenMPT's PreAmpTable curve (Sndmix.cpp) rather
            // than a linear /(channels*8): the linear form over-attenuates dense voicings
            // in many-channel formats, while clamping at 4 (the previous workaround) leaves
            // no headroom and lets sums wrap through the byte cast as harsh crackling.
            // Calibrated so chCount=4 yields the original divisor of 32 (0x60/3).
            int chCount = (int)ActiveChannels;
            if(chCount < 1) chCount = 1; else if(chCount > 31) chCount = 31;
            int attenuation = PreAmpTable[chCount >> 1];
            short[] CurrentVol = new short[32];
            short[] CurrentPan = new short[32];
            byte[][] pSample = new byte[32][];
            uint j;

            if(Type == Types.INVALID) return 0;
            lSampleSize = 1;
            if(Is16Bit) lSampleSize *= 2;
            if(IsStereo) lSampleSize *= 2;
            lMax = cbBuffer / lSampleSize;
            if((lMax == 0) || (p == null)) return 0;
            if(Type == Types.WAV) return (uint)(file.Read(lpBuffer, 0, (int)(lMax * lSampleSize)) / lSampleSize);

            // Memorize channels settings
            for(j = 0; j < ActiveChannels; j++) {
                CurrentVol[j] = channels[j].Muted ? (short)0 : channels[j].CurrentVolume;
                CurrentPan[j] = channels[j].Pan;
                if(channels[j].Length != 0) {
                    pSample[j] = new byte[channels[j].Sample.Length];
                    Array.Copy(channels[j].Sample, pSample[j], channels[j].Sample.Length);
                }
            }
            if(Pattern >= patterns.Length) return 0;

            // Fill audio buffer
            int pIndex = 0;
            for(lRead = 0; lRead < lMax; lRead++, pIndex += (int)lSampleSize) {
                if(BufferCount == 0) {
                    ReadNote();
                    // Memorize channels settings
                    for(j = 0; j < ActiveChannels; j++) {
                        CurrentVol[j] = channels[j].Muted ? (short)0 : channels[j].CurrentVolume;
                        CurrentPan[j] = channels[j].Pan;
                        if(channels[j].Length != 0) {
                            pSample[j] = new byte[channels[j].Sample.Length];
                            Array.Copy(channels[j].Sample, pSample[j], channels[j].Sample.Length);
                        } else {
                            pSample[j] = null;
                        }
                    }
                }
                BufferCount--;

                int vRight = 0, vLeft = 0;
                for(uint i = 0; i < ActiveChannels; i++) {
                    if(pSample[i] != null) {
                        // Read sample (8/16-bit, mono/stereo)
                        int poshi = (int)(channels[i].Pos >> MOD_PRECISION);
                        if((poshi + 1) < channels[i].SampleCount) {
                            short poslo = (short)(channels[i].Pos & MOD_FRACMASK);

                            int volL, volR;
                            if(channels[i].Is16Bit) {
                                int idxL = poshi * 2;
                                int sL = (short)(pSample[i][idxL] | (pSample[i][idxL + 1] << 8)) >> 8;
                                int dL = (short)(pSample[i][idxL + 2] | (pSample[i][idxL + 3] << 8)) >> 8;
                                volL = sL + ((poslo * (dL - sL)) >> MOD_PRECISION);
                                if(channels[i].IsStereo) {
                                    int idxR = ((int)channels[i].SampleCount + poshi) * 2;
                                    int sR = (short)(pSample[i][idxR] | (pSample[i][idxR + 1] << 8)) >> 8;
                                    int dR = (short)(pSample[i][idxR + 2] | (pSample[i][idxR + 3] << 8)) >> 8;
                                    volR = sR + ((poslo * (dR - sR)) >> MOD_PRECISION);
                                } else volR = volL;
                            } else {
                                int sL = (sbyte)pSample[i][poshi];
                                int dL = (sbyte)pSample[i][poshi + 1];
                                volL = sL + ((poslo * (dL - sL)) >> MOD_PRECISION);
                                if(channels[i].IsStereo) {
                                    int idxR = (int)channels[i].SampleCount + poshi;
                                    int sR = (sbyte)pSample[i][idxR];
                                    int dR = (sbyte)pSample[i][idxR + 1];
                                    volR = sR + ((poslo * (dR - sR)) >> MOD_PRECISION);
                                } else volR = volL;
                            }
                            volL *= CurrentVol[i];
                            volR *= CurrentVol[i];

                            int pan = CurrentPan[i];
                            if(channels[i].IsStereo) {
                                vLeft += volL;
                                vRight += volR;
                                channels[i].OldVol = (volL + volR) >> 1;
                            } else {
                                vLeft += (volL * (256 - pan)) >> 8;
                                vRight += (volL * pan) >> 8;
                                channels[i].OldVol = volL;
                            }
                        }

                        // Always advance the position and check for end-of-sample so non-looping
                        // samples terminate cleanly; otherwise the bounds-check above would skip
                        // this block forever and Length would never reach 0.
                        channels[i].Pos += channels[i].Inc;
                        if(channels[i].Pos >= channels[i].Length) {
                            channels[i].Length = channels[i].LoopEnd;
                            channels[i].Pos = (channels[i].Pos & MOD_FRACMASK) + channels[i].LoopStart;
                            if(channels[i].Length == 0) pSample[i] = null;
                        }
                    } else {
                        // No active sample on this channel. The original Mod95 mixer keeps
                        // adding the last interpolated value (OldVol) every frame as a click-
                        // removal trick, but with the sample-termination fix this branch fires
                        // continuously after a non-looping sample ends, producing a held DC
                        // offset (audible as a hard cut/pop). Ramp OldVol toward 0 so the
                        // tail fades out smoothly. `vol - (vol >> 4)` decays by ~6.25% per
                        // frame for both positive and negative integers in C# arithmetic.
                        int vol = channels[i].OldVol;
                        int pan = CurrentPan[i];
                        vLeft += (vol * (256 - pan)) >> 8;
                        vRight += (vol * pan) >> 8;
                        channels[i].OldVol = vol - (vol >> 4);
                    }
                }

                // Sample ready. Saturate the post-mix value to int16 range so any residual
                // peak hard-clips at the rails instead of wrapping through the byte cast
                // below (which would otherwise turn small overflows into harsh crackling).
                if(IsStereo) {
                    // Stereo - Surround
                    int vol = vRight;
                    vRight = (vRight * 13 + vLeft * 3) * 3 / attenuation;
                    vLeft = (vLeft * 13 + vol * 3) * 3 / attenuation;
                    if(vLeft > 32767) vLeft = 32767; else if(vLeft < -32768) vLeft = -32768;
                    if(vRight > 32767) vRight = 32767; else if(vRight < -32768) vRight = -32768;
                    if(Is16Bit) {
                        // 16-Bit: interleaved L, R (matches ALFormat.Stereo16 / WAV)
                        p[pIndex + 0] = (byte)(((uint)vLeft) & 0xFF);
                        p[pIndex + 1] = (byte)(((uint)vLeft) >> 8);
                        p[pIndex + 2] = (byte)(((uint)vRight) & 0xFF);
                        p[pIndex + 3] = (byte)(((uint)vRight) >> 8);
                    } else {
                        // 8-Bit: interleaved L, R (matches ALFormat.Stereo8 / WAV)
                        p[pIndex + 0] = (byte)((((uint)vLeft) >> 8) + 0x80);
                        p[pIndex + 1] = (byte)((((uint)vRight) >> 8) + 0x80);
                    }
                } else {
                    // Mono
                    int vol = (vRight + vLeft) * 24 / attenuation;
                    if(vol > 32767) vol = 32767; else if(vol < -32768) vol = -32768;
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
                    Pattern = order[CurrentPattern];
                }

                int inc = Type == Types.MOD ? 4 : 6;
                int pIndex = (int)(Row * ActiveChannels * inc);
                byte[] p = patterns[Pattern];
                for(int i = 0; (i < ActiveChannels) && (pIndex < p.Length); i++, pIndex += inc) {
                    uint period;
                    uint instIdx;
                    uint command;
                    uint param;
                    int chnIdx;

                    if(Type != Types.MOD) { // S3M / XM
                        int mode = p[pIndex + 0];
                        if(mode == 0) continue;

                        period = 0;
                        instIdx = 0;
                        command = 0xFF;
                        param = 0;
                        chnIdx = mode & 0x1F;

                        bool noteCut = false;
                        if((mode & 0x20) != 0) {
                            instIdx = p[pIndex + 2];

                            int note = p[pIndex + 1];
                            if((note >= 0xFE) && (note <= 0xFF)) {
                                // Note cut (0xFE) / note off (0xFF): silence channel, don't retrigger
                                noteCut = true;
                                instIdx = 0;
                            } else if(note < 0xF0) {
                                int octave = note >> 4;
                                int semitone = note & 0x0F;
                                note = semitone + 12 * octave + 12;// + 1;

                                // Amiga-style period calibrated so that S3M/XM/STM C-5 (note 60)
                                // yields MOD_AMIGAC2. Combined with FineTune = c5speed from the
                                // loader, the mixer's Inc = FineTune * MOD_AMIGAC2 / (period * Rate)
                                // then plays the sample at its natural rate on C-5 and tracks
                                // pitch by octave on either side.
                                period = (uint)(MOD_AMIGAC2 * Math.Pow(2.0, (60.0 - note) / 12.0));
                            }
                        };

                        //if((mode & 0x40) != 0) mChannels[chnIdx].Volume = p[pIndex + 3] << 2;

                        if((mode & 0x40) != 0) {
                            channels[chnIdx].Volume = p[pIndex + 3] << 2;
                        } else {
                            if(instIdx < instruments.Length) channels[chnIdx].Volume = instruments[instIdx].Volume;
                        }

                        if((mode & 0x80) != 0) {
                            command = p[pIndex + 4];
                            param = p[pIndex + 5];
                            command = (uint)S3MTools.ConvertEffect((Effects)command, (int)param);
                        }

                        if(noteCut) {
                            // Silence the channel by clearing Volume and Period (the slides loop
                            // will then zero Length/Inc/Pos and the Read loop will null out
                            // pSample[i]). Intentionally do NOT zero OldVol here: the else
                            // branch in the mixer uses it to ramp the channel down smoothly
                            // and avoid a hard click on ^^ / == cuts.
                            channels[chnIdx].Volume = 0;
                            channels[chnIdx].Period = 0;
                        }
                    } else { // MOD
                        chnIdx = i;
                        byte A0 = p[pIndex + 0], A1 = p[pIndex + 1], A2 = p[pIndex + 2], A3 = p[pIndex + 3];
                        period = (((uint)A0 & 0x0F) << 8) | (A1);
                        instIdx = ((uint)A2 >> 4) | (uint)(A0 & 0x10);
                        command = (uint)(A2 & 0x0F) + 1; // The +1 maps the MOD effects to the S3M effects
                        param = A3;
                    }
                    bool bVib = channels[chnIdx].Vibrato;
                    bool bTrem = channels[chnIdx].Tremolo;

                    // Reset channels data
                    channels[chnIdx].VolumeSlide = 0;
                    channels[chnIdx].FreqSlide = 0;
                    channels[chnIdx].OldPeriod = channels[chnIdx].Period;
                    channels[chnIdx].Portamento = false;
                    channels[chnIdx].Vibrato = false;
                    channels[chnIdx].Tremolo = false;
                    if(instIdx >= instruments.Length) instIdx = 0;
                    if(instIdx != 0) channels[chnIdx].NextInstrumentIndex = (short)instIdx;
                    if(period != 0) {
                        if(channels[chnIdx].NextInstrumentIndex != 0) {
                            channels[chnIdx].InstrumentIndex = instIdx;
                            if(Type == Types.MOD) channels[chnIdx].Volume = instruments[instIdx].Volume;
                            channels[chnIdx].Pos = 0;
                            // For looping samples, terminate the very first pass at LoopEnd: data past
                            // LoopEnd (the "tail") is never meant to be played and is often residual
                            // garbage that produces audible noise on every retrigger.
                            uint firstPassLen = instruments[instIdx].LoopEnd > 0 ? instruments[instIdx].LoopEnd : instruments[instIdx].Length;
                            channels[chnIdx].Length = firstPassLen << MOD_PRECISION;
                            channels[chnIdx].SampleCount = instruments[instIdx].Length;
                            channels[chnIdx].FineTune = instruments[instIdx].FineTune << MOD_PRECISION;
                            channels[chnIdx].LoopStart = instruments[instIdx].LoopStart << MOD_PRECISION;
                            channels[chnIdx].LoopEnd = instruments[instIdx].LoopEnd << MOD_PRECISION;
                            channels[chnIdx].Sample = instruments[instIdx].Sample;
                            channels[chnIdx].Is16Bit = instruments[instIdx].Is16Bit;
                            channels[chnIdx].IsStereo = instruments[instIdx].IsStereo;
                            channels[chnIdx].NextInstrumentIndex = 0;
                        }
                        if(((Effects)command != Effects.CMD_TONEPORTAMENTO) || (channels[chnIdx].Period == 0)) {
                            channels[chnIdx].Period = (int)period;
                            uint instLen = instruments[channels[chnIdx].InstrumentIndex].Length;
                            uint instLoopEnd = instruments[channels[chnIdx].InstrumentIndex].LoopEnd;
                            uint firstPassLen = instLoopEnd > 0 ? instLoopEnd : instLen;
                            channels[chnIdx].Length = firstPassLen << MOD_PRECISION;
                            channels[chnIdx].SampleCount = instLen;
                            channels[chnIdx].Pos = 0;
                        }
                        channels[chnIdx].PortamentoDest = (int)period;
                    }
                    switch((Effects)command) {
                        case Effects.CMD_ARPEGGIO:
                            if((param == 0) || (channels[chnIdx].Period == 0)) break;
                            channels[chnIdx].Count2 = 3;
                            channels[chnIdx].Period2 = channels[chnIdx].Period;
                            channels[chnIdx].Count1 = 2;
                            channels[chnIdx].Period1 = (int)(channels[chnIdx].Period + (param & 0x0F));
                            channels[chnIdx].Period += (int)((param >> 4) & 0x0F);
                            break;
                        case Effects.CMD_PORTAMENTOUP:
                            if(param == 0) param = (uint)channels[chnIdx].OldFreqSlide;
                            channels[chnIdx].OldFreqSlide = (int)param;
                            channels[chnIdx].FreqSlide = -(int)param;
                            break;
                        case Effects.CMD_PORTAMENTODOWN:
                            if(param == 0) param = (uint)channels[chnIdx].OldFreqSlide;
                            channels[chnIdx].OldFreqSlide = (int)param;
                            channels[chnIdx].FreqSlide = (int)param;
                            break;
                        case Effects.CMD_TONEPORTAMENTO:
                            if(param == 0) param = (uint)channels[chnIdx].PortamentoSlide;
                            channels[chnIdx].PortamentoSlide = (int)param;
                            channels[chnIdx].Portamento = true;
                            break;
                        case Effects.CMD_VIBRATO:
                            if(!bVib) channels[chnIdx].VibratoPos = 0;
                            if(param != 0) channels[chnIdx].VibratoSlide = (int)param;
                            channels[chnIdx].Vibrato = true;
                            break;
                        case Effects.CMD_TONEPORTAVOL:
                            if(period != 0) {
                                channels[chnIdx].PortamentoDest = (int)period;
                                if(channels[chnIdx].OldPeriod != 0) channels[chnIdx].Period = channels[chnIdx].OldPeriod;
                            }
                            channels[chnIdx].Portamento = true;
                            if(param != 0) {
                                if((param & 0xF0) != 0) channels[chnIdx].VolumeSlide = (int)((param >> 4) << 2);
                                else channels[chnIdx].VolumeSlide = -(int)((param & 0x0F) << 2);
                                channels[chnIdx].OldVolumeSlide = channels[chnIdx].VolumeSlide;
                            }
                            break;
                        case Effects.CMD_VIBRATOVOL:
                            if(!bVib) channels[chnIdx].VibratoPos = 0;
                            channels[chnIdx].Vibrato = true;
                            if(param != 0) {
                                if((param & 0xF0) != 0) channels[chnIdx].VolumeSlide = (int)((param >> 4) << 2);
                                else channels[chnIdx].VolumeSlide = -(int)((param & 0x0F) << 2);
                                channels[chnIdx].OldVolumeSlide = channels[chnIdx].VolumeSlide;
                            }
                            break;
                        case Effects.CMD_TREMOLO:
                            if(!bTrem) channels[chnIdx].TremoloPos = 0;
                            if(param != 0) channels[chnIdx].TremoloSlide = (int)param;
                            channels[chnIdx].Tremolo = true;
                            break;
                        case Effects.CMD_PANNING8:
                            // Not Implemented
                            break;
                        case Effects.CMD_OFFSET:
                            if(param > 0) {
                                param <<= 8 + MOD_PRECISION;
                                if(param < channels[chnIdx].Length) channels[chnIdx].Pos = param;
                            }
                            break;
                        case Effects.CMD_VOLUMESLIDE:
                            if(param != 0) {
                                if((param & 0xF0) != 0) channels[chnIdx].VolumeSlide = (int)((param >> 4) << 2);
                                else channels[chnIdx].VolumeSlide = -(int)((param & 0x0F) << 2);
                                channels[chnIdx].OldVolumeSlide = channels[chnIdx].VolumeSlide;
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
                            channels[chnIdx].Volume = (int)param;
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
                                    channels[chnIdx].FineTune = FineTuneTable[param] << MOD_PRECISION;
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
                    channels[nChn].CurrentVolume = (short)channels[nChn].Volume;
                    if(channels[nChn].Tremolo) {
                        int vol = channels[nChn].CurrentVolume;
                        switch(channels[nChn].TremoloType) {
                            case 1: // Ramp Down
                                vol += ModRampDownTable[channels[nChn].TremoloPos] * (channels[nChn].TremoloSlide & 0x0F) / 127;
                                break;
                            case 2: // Square
                                vol += ModSquareTable[channels[nChn].TremoloPos] * (channels[nChn].TremoloSlide & 0x0F) / 127;
                                break;
                            case 3: // Random
                                vol += ModRandomTable[channels[nChn].TremoloPos] * (channels[nChn].TremoloSlide & 0x0F) / 127;
                                break;
                            default: // Sinus
                                vol += ModSinusTable[channels[nChn].TremoloPos] * (channels[nChn].TremoloSlide & 0x0F) / 127;
                                break;
                        }
                        if(vol < 0) vol = 0;
                        if(vol > 0x100) vol = 0x100;
                        channels[nChn].CurrentVolume = (short)vol;
                        channels[nChn].TremoloPos = (channels[nChn].TremoloPos + (channels[nChn].TremoloSlide >> 4)) & 0x3F;
                    }
                    if(channels[nChn].Portamento && (channels[nChn].PortamentoDest != 0)) {
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
                    // 64-bit intermediate: FineTune is c5speed << MOD_PRECISION, so for c5speed > ~9824
                    // (common in S3M/XM/STM samples) FineTune * MOD_AMIGAC2 overflows uint and Inc collapses
                    // to a fraction of its true value, audibly dropping the affected voices by an octave or more.
                    channels[nChn].Inc = (uint)(((long)channels[nChn].FineTune * MOD_AMIGAC2) / (period * Rate));
                } else {
                    channels[nChn].Inc = 0;
                    channels[nChn].Pos = 0;
                    channels[nChn].Length = 0;
                }
            }
            BufferCount = (Rate * 5) / (MusicTempo * 2);
            SpeedCount--;
            return true;
        }
    }
}