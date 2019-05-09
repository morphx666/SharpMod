#ifndef __SNDFILE_H
#define __SNDFILE_H


typedef struct tagMODINSTRUMENT {
	UINT nLength, nLoopStart, nLoopEnd;
	UINT nFineTune;
	int nVolume;
	LPSTR pSample;
} MODINSTRUMENT;


typedef struct tagMODCHANNEL {
	UINT nSample, nFineTune;
	UINT nPos, nInc;
	UINT nLength, nLoopStart, nLoopEnd;
	int nVolume, nVolumeSlide, nOldVolumeSlide;
	int nPeriod, nOldPeriod, nFreqSlide, nOldFreqSlide;
	int nPortamentoDest, nPortamentoSlide;
	int nVibratoPos, nVibratoSlide, nVibratoType;
	int nTremoloPos, nTremoloSlide, nTremoloType;
	int nCount1, nCount2;
	int nPeriod1, nPeriod2;
	BOOL bPortamento, bVibrato, bTremolo;
	LPSTR pSample;
	int nOldVol;
	short int nCurrentVol, nNextIns;
} MODCHANNEL;



//==============
class CSoundFile
	//==============
{
protected:
	CFile m_File;
	MODINSTRUMENT Ins[32];
	MODCHANNEL Chn[32];
	char m_szNames[32][32];
	BYTE Order[256];
	LPBYTE Patterns[64];
	UINT m_nType, m_nRate, m_nChannels, m_nSamples;
	UINT m_nMusicSpeed, m_nMusicTempo, m_nSpeedCount, m_nBufferCount;
	UINT m_nPattern, m_nCurrentPattern, m_nNextPattern, m_nRow;
	BOOL m_bHigh, m_bStereo, m_bLoop;

public:
	CSoundFile(LPCSTR lpszPathName, UINT nRate, BOOL bHigh, BOOL bStereo, BOOL bLoop = FALSE);
	~CSoundFile();
	UINT Read(LPVOID lpBuffer, UINT cbBuffer);
	BOOL ReadNote();
	BOOL SetWaveConfig(UINT nRate, BOOL bHigh, BOOL bStereo);
	UINT GetNumPatterns();
	UINT GetType() { return m_nType; }
	UINT GetCurrentPos() { return (m_nCurrentPattern * 64) + m_nRow; }
	UINT GetMaxPosition() { return GetNumPatterns() * 64; }
	void SetCurrentPos(UINT nPos);
	void GetTitle(LPSTR s) { strcpy(s, m_szNames[0]); }
	void GetSampleName(UINT nSample, LPSTR s) { strcpy(s, m_szNames[nSample]); }
	UINT GetLength();
};


#endif
