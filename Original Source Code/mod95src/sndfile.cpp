#include "stdafx.h"
#include "sndfile.h"
#include <windowsx.h>


#define MOD_PRECISION	10
#define MOD_FRACMASK	1023
#define MOD_AMIGAC2		0x1AB


// FineTune frequencies
static UINT FineTuneTable[16] =
{
	7895,7941,7985,8046,8107,8169,8232,8280,
	8363,8413,8463,8529,8581,8651,8723,8757,
};

// Sinus table
static int ModSinusTable[64] =
{
	0,12,25,37,49,60,71,81,90,98,106,112,117,122,125,126,
	127,126,125,122,117,112,106,98,90,81,71,60,49,37,25,12,
	0,-12,-25,-37,-49,-60,-71,-81,-90,-98,-106,-112,-117,-122,-125,-126,
	-127,-126,-125,-122,-117,-112,-106,-98,-90,-81,-71,-60,-49,-37,-25,-12
};

// Triangle wave table (ramp down)
static int ModRampDownTable[64] =
{
	0,-4,-8,-12,-16,-20,-24,-28,-32,-36,-40,-44,-48,-52,-56,-60,
	-64,-68,-72,-76,-80,-84,-88,-92,-96,-100,-104,-108,-112,-116,-120,-124,
	127,123,119,115,111,107,103,99,95,91,87,83,79,75,71,67,
	63,59,55,51,47,43,39,35,31,27,23,19,15,11,7,3
};

// Square wave table
static int ModSquareTable[64] =
{
	127,127,127,127,127,127,127,127,127,127,127,127,127,127,127,127,
	127,127,127,127,127,127,127,127,127,127,127,127,127,127,127,127,
	-127,-127,-127,-127,-127,-127,-127,-127,-127,-127,-127,-127,-127,-127,-127,-127,
	-127,-127,-127,-127,-127,-127,-127,-127,-127,-127,-127,-127,-127,-127,-127,-127
};

// Random wave table
static int ModRandomTable[64] =
{
	98,-127,-43,88,102,41,-65,-94,125,20,-71,-86,-70,-32,-16,-96,
	17,72,107,-5,116,-69,-62,-40,10,-61,65,109,-18,-38,-13,-76,
	-23,88,21,-94,8,106,21,-112,6,109,20,-88,-30,9,-127,118,
	42,-34,89,-4,-51,-72,21,-29,112,123,84,-101,-92,98,-54,-95
};



CSoundFile::CSoundFile(LPCSTR lpszPathName, UINT nRate, BOOL bHigh, BOOL bStereo, BOOL bLoop)
//---------------------------------------------------------------------------------------
{
	char s[1024] = "";
	int i, j, k, nbp;
	BYTE bTab[32];

	m_nType = 0;
	m_nRate = nRate;
	m_bHigh = bHigh;
	m_bStereo = bStereo;
	m_bLoop = bLoop;
	m_nChannels = 0;
	memset(Ins, 0, sizeof(Ins));
	memset(Chn, 0, sizeof(Chn));
	memset(Order, 0, sizeof(Order));
	memset(Patterns, 0, sizeof(Patterns));
	memset(m_szNames, 0, sizeof(m_szNames));
	if(!m_File.Open(lpszPathName, CFile::modeRead | CFile::typeBinary)) return;
	m_nType = 1;
	m_File.Seek(0x438, CFile::begin);
	m_File.Read(s, 4);
	s[4] = 0;
	m_nSamples = 31;
	m_nChannels = 4;
	if(!strcmp(s, "M.K.")) m_nChannels = 4; else
		if((s[0] == 'F') && (s[1] == 'L') && (s[2] == 'T') && (s[3] >= '1') && (s[3] <= '9')) m_nChannels = s[3] - '0'; else
			if((s[0] >= '1') && (s[0] <= '9') && (s[1] == 'C') && (s[2] == 'H') && (s[3] == 'N')) m_nChannels = s[0] - '0'; else
				if((s[0] == '1') && (s[1] >= '0') && (s[1] <= '6') && (s[2] == 'C') && (s[3] == 'H')) m_nChannels = s[1] - '0' + 10;
				else m_nSamples = 15;
	m_File.SeekToBegin();
	m_File.Read(m_szNames[0], 20);

	int a = m_File.GetPosition();

	for(i = 1; i <= (int)m_nSamples; i++) {
		m_File.Read(bTab, 30);
		memcpy(m_szNames[i], bTab, 22);

		if((j = (bTab[22] << 9) | (bTab[23] << 1)) < 4) j = 0;
		Ins[i].nLength = j;
		if((j = bTab[24]) > 7) j &= 7;	else j = (j & 7) + 8;
		Ins[i].nFineTune = FineTuneTable[j];
		Ins[i].nVolume = bTab[25];
		if(Ins[i].nVolume > 0x40) Ins[i].nVolume = 0x40;
		Ins[i].nVolume <<= 2;

		if((j = ((unsigned int)bTab[26] << 9) | ((unsigned int)bTab[27] << 1)) < 4) j = 0;
		if((k = ((unsigned int)bTab[28] << 9) | ((unsigned int)bTab[29] << 1)) < 4) k = 0;
		if(j + k > (int)Ins[i].nLength) {
			j >>= 1;
			k = j + ((k + 1) >> 1);
		} else k += j;
		if(Ins[i].nLength) {
			if(j >= (int)Ins[i].nLength) j = Ins[i].nLength - 1;
			if(k > (int)Ins[i].nLength) k = Ins[i].nLength;
			if((j > k) || (k < 4) || (k - j <= 4))	j = k = 0;
		}
		Ins[i].nLoopStart = j;
		Ins[i].nLoopEnd = k;
	}

	for(i = 0; i < 32; i++) {
		j = 31;
		while((j >= 0) && (m_szNames[i][j] <= ' ')) m_szNames[i][j--] = 0;
		while(j >= 0) {
			if(m_szNames[i][j] < ' ') m_szNames[i][j] = ' ';
			j--;
		}
	}
	m_File.Read(bTab, 2);
	k = bTab[0];
	if(m_File.Read(Order, 128) != 128) goto OnError;


	nbp = 0;
	for(j = 0; j < 128; j++) {
		i = Order[j];
		if((i < 64) && (nbp <= i)) nbp = i + 1;
	}
	j = 0xFF;
	if((!k) || (k > 0x7F)) k = 0x7F;
	while((j >= k) && (!Order[j])) Order[j--] = 0xFF;
	if(m_nSamples == 31) m_File.Seek(4, CFile::current);
	if(!nbp) goto OnError;
	// Reading channels
	for(i = 0; i < nbp; i++) {
		if((Patterns[i] = (LPBYTE)GlobalAllocPtr(GHND, m_nChannels * 256)) == NULL) goto OnError;
		if(!m_File.Read(Patterns[i], m_nChannels * 256)) goto OnError;
	}
	// Reading instruments
	for(i = 1; i <= (int)m_nSamples; i++) if(Ins[i].nLength) {
		if((Ins[i].pSample = (LPSTR)GlobalAllocPtr(GHND, Ins[i].nLength + 1)) == NULL) {
			Ins[i].nLength = 0;
			goto OnError;
		}
		m_File.Read(Ins[i].pSample, Ins[i].nLength);
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
OnError:;
}


CSoundFile::~CSoundFile()
//-----------------------
{
	int i;
	for(i = 0; i < 64; i++) if(Patterns[i]) {
		GlobalFreePtr(Patterns[i]);
		Patterns[i] = NULL;
	}
	for(i = 0; i < 32; i++) if(Ins[i].pSample) {
		GlobalFreePtr(Ins[i].pSample);
		Ins[i].pSample = 0;
	}
	if(m_nType == 1) m_File.Close();
}


BOOL CSoundFile::SetWaveConfig(UINT nRate, BOOL bHigh, BOOL bStereo)
//----------------------------------------------------------------
{
	m_nRate = nRate;
	m_bHigh = bHigh;
	m_bStereo = bStereo;
	return TRUE;
}


UINT CSoundFile::GetNumPatterns()
//-------------------------------
{
	for(UINT i = 0; i < 128; i++) if(Order[i] >= 64) return i;
	return 128;
}


void CSoundFile::SetCurrentPos(UINT nPos)
//---------------------------------------
{
	UINT nPattern = nPos >> 6;
	UINT nRow = nPos & 0x3F;
	if(nPattern > 127)	nPattern = 0;
	if(nRow) {
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


UINT CSoundFile::Read(LPVOID lpBuffer, UINT cbBuffer)
//---------------------------------------------------
{
	LPBYTE p = (LPBYTE)lpBuffer;
	UINT lRead, lMax, lSampleSize;
	signed short adjustvol = (signed short)m_nChannels;
	short int CurrentVol[32];
	LPSTR pSample[32];
	BOOL bTrkDest[32];
	UINT j;

	if(!m_nType) return 0;
	lSampleSize = 1;
	if(m_bHigh) lSampleSize *= 2;
	if(m_bStereo) lSampleSize *= 2;
	lMax = cbBuffer / lSampleSize;
	if((!lMax) || (!p)) return 0;
	if(m_nType == 1) return m_File.Read(lpBuffer, lMax * lSampleSize) / lSampleSize;
	// Memorize channels settings
	for(j = 0; j < m_nChannels; j++) {
		CurrentVol[j] = Chn[j].nCurrentVol;
		pSample[j] = (Chn[j].nLength) ? Chn[j].pSample : NULL;
		if(m_nChannels == 4)
			bTrkDest[j] = (((j & 3) == 1) || ((j & 3) == 2)) ? TRUE : FALSE;
		else
			bTrkDest[j] = (j & 1) ? FALSE : TRUE;
	}
	if(m_nPattern >= 64) return 0;
	// Fill audio buffer
	for(lRead = 0; lRead < lMax; lRead++, p += lSampleSize) {
		if(!m_nBufferCount--) {
			ReadNote();
			// Memorize channels settings
			for(j = 0; j < m_nChannels; j++) {
				CurrentVol[j] = Chn[j].nCurrentVol;
				pSample[j] = (Chn[j].nLength) ? Chn[j].pSample : NULL;
			}
		}
		int vRight = 0, vLeft = 0;
		for(UINT i = 0; i < m_nChannels; i++) if(pSample[i]) {
			// Read sample
			int poshi = Chn[i].nPos >> MOD_PRECISION;
			short int poslo = (signed short)(Chn[i].nPos & MOD_FRACMASK);
			short int srcvol = (signed char)pSample[i][poshi];
			short int destvol = (signed char)pSample[i][poshi + 1];
			int vol = srcvol + ((int)(poslo * (destvol - srcvol)) >> MOD_PRECISION);
			vol *= CurrentVol[i];
			if(bTrkDest[i]) vRight += vol; else vLeft += vol;
			Chn[i].nOldVol = vol;
			Chn[i].nPos += Chn[i].nInc;
			if(Chn[i].nPos >= Chn[i].nLength) {
				Chn[i].nLength = Chn[i].nLoopEnd;
				Chn[i].nPos = (Chn[i].nPos & MOD_FRACMASK) + Chn[i].nLoopStart;
				if(!Chn[i].nLength) pSample[i] = FALSE;
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
				p[0] = (BYTE)(((UINT)vRight) & 0xFF);
				p[1] = (BYTE)(((UINT)vRight) >> 8);
				p[2] = (BYTE)(((UINT)vLeft) & 0xFF);
				p[3] = (BYTE)(((UINT)vLeft) >> 8);
			} else {
				// 8-Bit
				p[0] = (BYTE)((((UINT)vRight) >> 8) + 0x80);
				p[1] = (BYTE)((((UINT)vLeft) >> 8) + 0x80);
			}
		} else {
			// Mono
			int vol = (vRight + vLeft) / adjustvol;
			if(m_bHigh) {
				// 16-Bit
				p[0] = (BYTE)(((UINT)vol) & 0xFF);
				p[1] = (BYTE)(((UINT)vol) >> 8);
			} else {
				// 8-Bit
				p[0] = (BYTE)((((UINT)vol) >> 8) + 0x80);
			}
		}
	}
	return lRead;
}


BOOL CSoundFile::ReadNote()
//-------------------------
{
	if(!m_nSpeedCount) {
		m_nRow = (m_nRow + 1) & 0x3F;
		if(!m_nRow) {
			m_nCurrentPattern = m_nNextPattern;
			m_nNextPattern++;
			m_nPattern = Order[m_nCurrentPattern];
		}
		if(m_nPattern >= 64) {
			m_nMusicSpeed = 6;
			m_nMusicTempo = 125;
			if(!m_bLoop) {
				m_nBufferCount = (m_nRate * 5) / (m_nMusicTempo * 2);
				return FALSE;
			}
			m_nCurrentPattern = 0;
			m_nNextPattern = 1;
			m_nPattern = Order[m_nCurrentPattern];
		}
		LPBYTE p = Patterns[m_nPattern] + m_nRow * m_nChannels * 4;
		for(UINT nChn = 0; nChn < m_nChannels; nChn++, p += 4) {
			BYTE A0 = p[0], A1 = p[1], A2 = p[2], A3 = p[3];
			UINT period = (((UINT)A0 & 0x0F) << 8) | (A1);
			UINT instr = ((UINT)A2 >> 4) | (A0 & 0x10);
			UINT command = A2 & 0x0F;
			UINT param = A3;
			BOOL bVib = Chn[nChn].bVibrato;
			BOOL bTrem = Chn[nChn].bTremolo;

			// Reset channels data
			Chn[nChn].nVolumeSlide = 0;
			Chn[nChn].nFreqSlide = 0;
			Chn[nChn].nOldPeriod = Chn[nChn].nPeriod;
			Chn[nChn].bPortamento = FALSE;
			Chn[nChn].bVibrato = FALSE;
			Chn[nChn].bTremolo = FALSE;
			if(instr > 31) instr = 0;
			if(instr) Chn[nChn].nNextIns = (short int)instr;
			if(period) {
				if(Chn[nChn].nNextIns) {
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
				if((command != 0x03) || (!Chn[nChn].nPeriod)) {
					Chn[nChn].nPeriod = period;
					Chn[nChn].nLength = Ins[Chn[nChn].nSample].nLength << MOD_PRECISION;
					Chn[nChn].nPos = 0;
				}
				Chn[nChn].nPortamentoDest = period;
			}
			switch(command) {
				// 00: Arpeggio
				case 0x00:
					if((!param) || (!Chn[nChn].nPeriod)) break;
					Chn[nChn].nCount2 = 3;
					Chn[nChn].nPeriod2 = Chn[nChn].nPeriod;
					Chn[nChn].nCount1 = 2;
					Chn[nChn].nPeriod1 = Chn[nChn].nPeriod + (param & 0x0F);
					Chn[nChn].nPeriod += (param >> 4) & 0x0F;
					break;
					// 01: Portamento Up
				case 0x01:
					if(!param) param = Chn[nChn].nOldFreqSlide;
					Chn[nChn].nOldFreqSlide = param;
					Chn[nChn].nFreqSlide = -(int)param;
					break;
					// 02: Portamento Down
				case 0x02:
					if(!param) param = Chn[nChn].nOldFreqSlide;
					Chn[nChn].nOldFreqSlide = param;
					Chn[nChn].nFreqSlide = param;
					break;
					// 03: Tone-Portamento
				case 0x03:
					if(!param) param = Chn[nChn].nPortamentoSlide;
					Chn[nChn].nPortamentoSlide = param;
					Chn[nChn].bPortamento = TRUE;
					break;
					// 04: Vibrato
				case 0x04:
					if(!bVib) Chn[nChn].nVibratoPos = 0;
					if(param) Chn[nChn].nVibratoSlide = param;
					Chn[nChn].bVibrato = TRUE;
					break;
					// 05: Tone-Portamento + Volume Slide
				case 0x05:
					if(period) {
						Chn[nChn].nPortamentoDest = period;
						if(Chn[nChn].nOldPeriod) Chn[nChn].nPeriod = Chn[nChn].nOldPeriod;
					}
					Chn[nChn].bPortamento = TRUE;
					if(param) {
						if(param & 0xF0) Chn[nChn].nVolumeSlide = (int)((param >> 4) << 2);
						else Chn[nChn].nVolumeSlide = -(int)((param & 0x0F) << 2);
						Chn[nChn].nOldVolumeSlide = Chn[nChn].nVolumeSlide;
					}
					break;
					// 06: Vibrato + Volume Slide
				case 0x06:
					if(!bVib) Chn[nChn].nVibratoPos = 0;
					Chn[nChn].bVibrato = TRUE;
					if(param) {
						if(param & 0xF0) Chn[nChn].nVolumeSlide = (int)((param >> 4) << 2);
						else Chn[nChn].nVolumeSlide = -(int)((param & 0x0F) << 2);
						Chn[nChn].nOldVolumeSlide = Chn[nChn].nVolumeSlide;
					}
					break;
					// 07: Tremolo
				case 0x07:
					if(!bTrem) Chn[nChn].nTremoloPos = 0;
					if(param) Chn[nChn].nTremoloSlide = param;
					Chn[nChn].bTremolo = TRUE;
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
					if(param) {
						if(param & 0xF0) Chn[nChn].nVolumeSlide = (int)((param >> 4) << 2);
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
					Chn[nChn].nVolume = param;
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
							if(Chn[nChn].nPeriod) {
								Chn[nChn].nPeriod -= param;
								if(Chn[nChn].nPeriod < 1) Chn[nChn].nPeriod = 1;
							}
							break;
							// 0xE2: Fine Portamento Down
						case 0x02:
							if(Chn[nChn].nPeriod) {
								Chn[nChn].nPeriod += param;
							}
							break;
							// 0xE3: Set Glissando Control (???)
							// 0xE4: Set Vibrato WaveForm
						case 0x04:
							Chn[nChn].nVibratoType = param & 0x03;
							break;
							// 0xE5: Set Finetune
						case 0x05:
							Chn[nChn].nFineTune = FineTuneTable[param];
							break;
							// 0xE6: Pattern Loop
							// 0xE7: Set Tremolo WaveForm
						case 0x07:
							Chn[nChn].nTremoloType = param & 0x03;
							break;
							// 0xE9: Retrig + Fine Volume Slide
							// 0xEA: Fine Volume Up
						case 0x0A:
							Chn[nChn].nVolume += param << 2;
							break;
							// 0xEB: Fine Volume Down
						case 0x0B:
							Chn[nChn].nVolume -= param << 2;
							break;
							// 0xEC: Note Cut
						case 0x0C:
							Chn[nChn].nCount1 = param + 1;
							Chn[nChn].nPeriod1 = 0;
							break;
					}
					break;
					// 0F: Set Speed
				case 0x0F:
					if((param) && (param < 0x20)) m_nMusicSpeed = param; else
						if(param >= 0x20) m_nMusicTempo = param;
					break;
			}
		}
		m_nSpeedCount = m_nMusicSpeed;
	}
	if(m_nPattern >= 64) return FALSE;
	// Update channels data
	for(UINT nChn = 0; nChn < m_nChannels; nChn++) {
		Chn[nChn].nVolume += Chn[nChn].nVolumeSlide;
		if(Chn[nChn].nVolume < 0) Chn[nChn].nVolume = 0;
		if(Chn[nChn].nVolume > 0x100) Chn[nChn].nVolume = 0x100;
		if(Chn[nChn].nCount1) {
			Chn[nChn].nCount1--;
			if(!Chn[nChn].nCount1) Chn[nChn].nPeriod = Chn[nChn].nPeriod1;
		}
		if(Chn[nChn].nCount2) {
			Chn[nChn].nCount2--;
			if(!Chn[nChn].nCount2) Chn[nChn].nPeriod = Chn[nChn].nPeriod2;
		}
		if(Chn[nChn].nPeriod) {
			Chn[nChn].nCurrentVol = (short int)Chn[nChn].nVolume;
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
				}
				if(vol < 0) vol = 0;
				if(vol > 0x100) vol = 0x100;
				Chn[nChn].nCurrentVol = (short int)vol;
				Chn[nChn].nTremoloPos = (Chn[nChn].nTremoloPos + (Chn[nChn].nTremoloSlide >> 4)) & 0x3F;
			}
			if((Chn[nChn].bPortamento) && (Chn[nChn].nPortamentoDest)) {
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
				}
				Chn[nChn].nVibratoPos = (Chn[nChn].nVibratoPos + (Chn[nChn].nVibratoSlide >> 4)) & 0x3F;
			}
			if(period < 1) period = 1;
			Chn[nChn].nInc = (Chn[nChn].nFineTune * MOD_AMIGAC2) / (period * m_nRate);
		} else {
			Chn[nChn].nInc = 0;
			Chn[nChn].nPos = 0;
			Chn[nChn].nLength = 0;
		}
	}
	m_nBufferCount = (m_nRate * 5) / (m_nMusicTempo * 2);
	m_nSpeedCount--;
	return TRUE;
}


UINT CSoundFile::GetLength()
//--------------------------
{
	UINT dwElapsedTime = 0, nRow = 0, nSpeedCount = 0, nCurrentPattern = 0, nNextPattern = 0, nPattern = 0;
	UINT nMusicSpeed = 6, nMusicTempo = 125;

	for(;;) {
		if(!nSpeedCount) {
			nRow = (nRow + 1) & 0x3F;
			if(!nRow) {
				nCurrentPattern = nNextPattern;
				nNextPattern++;
				nPattern = Order[nCurrentPattern];
			}
			if(nPattern >= 64) goto EndMod;
			LPBYTE p = Patterns[nPattern] + nRow * m_nChannels * 4;
			for(UINT nChn = 0; nChn < m_nChannels; nChn++, p += 4) {
				UINT command = p[2] & 0x0F;
				UINT param = p[3];

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
						if((param) && (param < 0x20)) nMusicSpeed = param; else
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

