// moddoc.h : interface of the CModuleDoc class
//
/////////////////////////////////////////////////////////////////////////////
#include "sndfile.h"

#define NUM_AUDIO_BUFFERS	16


//================================
class CModuleDoc: public CDocument
//================================
{
protected:
	DECLARE_DYNCREATE(CModuleDoc)
	CModuleDoc();
	
protected:
	// Audio data
	CSoundFile *m_pSndFile;
	HWAVEOUT shWaveOut;
	UINT swBuffers, swBuffersOut, swNextBuffer;
	LONG slSampleSize,sdwSamplesPerSec, sdwAudioBufferSize;
	LPWAVEHDR salpAudioBuf[NUM_AUDIO_BUFFERS];
	LONG m_nVideoStart,m_nVideoEnd,m_nVideoPos;
	BOOL m_bPlay;
	WAVEFORMATEX m_WaveFormat;

public:
	LONG GetVideoStart()	{ return m_nVideoStart; }
	LONG GetVideoEnd()		{ return m_nVideoEnd; }
	LONG GetVideoPos()		{ return m_nVideoPos; }
	CSoundFile *GetSoundFile()	{ return m_pSndFile; }
	void SetVideoPos(int nPos);
	void audioCloseDevice();
	BOOL audioOpenDevice();
	BOOL audioiFillBuffers();
	BOOL OnDocIdle();
	void OnWOMDone()		{ --swBuffersOut; audioiFillBuffers(); }
	virtual void OnIdle()	{ OnDocIdle(); }
	virtual BOOL OnOpenDocument(LPCTSTR lpszPathName);
	virtual BOOL OnSaveDocument(LPCTSTR) { return TRUE; }
	virtual BOOL OnNewDocument();
	virtual void OnCloseDocument();
	virtual ~CModuleDoc();
	
public:
	afx_msg void OnVideoPlay();
	afx_msg void OnVideoStop();
	afx_msg void OnVideoNext();
	afx_msg void OnVideoPrev();
	afx_msg void OnUpdateVideoStop(CCmdUI *pCmdUI);
	afx_msg void OnUpdateVideoPlay(CCmdUI *pCmdUI);
	DECLARE_MESSAGE_MAP()
};
