// moddoc.cpp : implementation of the CModuleDoc class
//

#include "stdafx.h"
#include "MOD95.h"
#include "mainfrm.h"
#include "moddoc.h"
#include "modview.h"
#include <windowsx.h>

/////////////////////////////////////////////////////////////////////////////
// CModuleDoc

IMPLEMENT_DYNCREATE(CModuleDoc, CDocument)
BEGIN_MESSAGE_MAP(CModuleDoc, CDocument)
	ON_COMMAND(ID_VIDEO_STOP, OnVideoStop)
	ON_COMMAND(ID_VIDEO_PLAY, OnVideoPlay)
	ON_COMMAND(ID_VIDEO_PREV, OnVideoPrev)
	ON_COMMAND(ID_VIDEO_NEXT, OnVideoNext)
	ON_UPDATE_COMMAND_UI(ID_VIDEO_STOP, OnUpdateVideoStop)
	ON_UPDATE_COMMAND_UI(ID_VIDEO_PLAY, OnUpdateVideoPlay)
	ON_UPDATE_COMMAND_UI(ID_VIDEO_PREV, OnUpdateVideoStop)
	ON_UPDATE_COMMAND_UI(ID_VIDEO_NEXT, OnUpdateVideoStop)
END_MESSAGE_MAP()

/////////////////////////////////////////////////////////////////////////////
// CModuleDoc construction/destruction

CModuleDoc::CModuleDoc()
//----------------------
{
	m_pSndFile = NULL;
	m_bPlay = FALSE;
	sdwSamplesPerSec = 0;
	slSampleSize = 0;
	shWaveOut = NULL;
	swBuffersOut = 0;
	swBuffers = 0;
	m_nVideoStart = 0;
	m_nVideoPos = 0;
	m_nVideoEnd = 0;
}


CModuleDoc::~CModuleDoc()
//-----------------------
{}


BOOL CModuleDoc::OnNewDocument()
//------------------------------
{
	return CDocument::OnNewDocument();
}


BOOL CModuleDoc::OnOpenDocument(LPCTSTR lpszPathName)
//---------------------------------------------------
{
	CMainFrame* pMainFrm = (CMainFrame*)AfxGetApp()->m_pMainWnd;

	if((!pMainFrm) || (!lpszPathName))	return FALSE;
	BeginWaitCursor();
	if(m_bPlay) OnVideoStop();
	if(m_pSndFile) {
		delete m_pSndFile;
		m_pSndFile = NULL;
	}
	LONG lTemp = pMainFrm->GetAudioQuality();
	if((lTemp < 0) || (lTemp >= 12)) lTemp = 0;
	UINT nRate = (11025 << (lTemp >> 2));
	BOOL bHigh = (lTemp & 2) ? TRUE : FALSE;
	BOOL bStereo = (lTemp & 1) ? TRUE : FALSE;
	sdwSamplesPerSec = nRate;
	slSampleSize = 1;
	swBuffersOut = 0;
	if(bStereo) slSampleSize <<= 1;
	if(bHigh) slSampleSize <<= 1;
	sdwAudioBufferSize = (sdwSamplesPerSec / 10) * slSampleSize;
	m_pSndFile = new CSoundFile(lpszPathName, nRate, bHigh, bStereo, pMainFrm->GetLoop());
	if((m_pSndFile) && (m_pSndFile->GetType() > 1)) {
		m_nVideoEnd = m_pSndFile->GetNumPatterns() * 64;
		m_WaveFormat.wFormatTag = WAVE_FORMAT_PCM;
		m_WaveFormat.nChannels = (unsigned short)((bStereo) ? 2 : 1);
		m_WaveFormat.nSamplesPerSec = nRate;
		m_WaveFormat.nAvgBytesPerSec = nRate * slSampleSize;
		m_WaveFormat.nBlockAlign = (unsigned short)slSampleSize;
		m_WaveFormat.wBitsPerSample = (unsigned short)((bHigh) ? 16 : 8);
		m_nVideoStart = 0;
		m_nVideoPos = 0;
		m_nVideoEnd = m_pSndFile->GetMaxPosition();
	} else {
		if(m_pSndFile) {
			delete m_pSndFile;
			m_pSndFile = NULL;
		}
		EndWaitCursor();
		return FALSE;
	}
	// Auto-Play
	POSITION pos = GetFirstViewPosition();
	CModView* pView = (CModView*)GetNextView(pos);
	if(pView) pView->PostMessage(WM_COMMAND, ID_VIDEO_PLAY);
	return TRUE;
}


void CModuleDoc::OnCloseDocument()
//--------------------------------
{
	if(m_bPlay) OnVideoStop();
	if(m_pSndFile) {
		delete m_pSndFile;
		m_pSndFile = NULL;
	}
	CDocument::OnCloseDocument();
}


/////////////////////////////////////////////////////////////////////////////
// CModuleDoc commands

void CModuleDoc::OnVideoStop()
//----------------------------
{
	if(!m_bPlay) return;
	m_bPlay = FALSE;
	BeginWaitCursor();
	if(shWaveOut != 0) {
		waveOutReset(shWaveOut);
		audioCloseDevice();
	}
	EndWaitCursor();
}


void CModuleDoc::OnVideoPlay()
//----------------------------
{
	CMainFrame* pMainFrm = (CMainFrame*)AfxGetApp()->m_pMainWnd;
	if((m_bPlay) || (!pMainFrm) || (!m_pSndFile)) return;
	if(m_nVideoPos >= m_nVideoEnd - 1) m_nVideoPos = m_nVideoStart;
	m_pSndFile->SetCurrentPos(m_nVideoPos);
	if(shWaveOut != 0) return;
	if(!audioOpenDevice() || (shWaveOut == 0)) return;
	// We're beginning play, so pause until we've filled the buffers for a seamless start
	m_bPlay = TRUE;
	BeginWaitCursor();
	waveOutPause(shWaveOut);
	audioiFillBuffers();
	waveOutRestart(shWaveOut);
	EndWaitCursor();
}


void CModuleDoc::OnVideoPrev()
//----------------------------
{
	if(!m_pSndFile) return;
	int nPos = m_pSndFile->GetCurrentPos();
	if(nPos >= 64) nPos -= 64; else nPos = 0;
	m_pSndFile->SetCurrentPos(nPos & 0xFFC0);
}


void CModuleDoc::OnVideoNext()
//----------------------------
{
	if(!m_pSndFile) return;
	int nPos = m_pSndFile->GetCurrentPos() + 64;
	if(nPos >= m_nVideoEnd) nPos = m_nVideoEnd - 1;
	m_pSndFile->SetCurrentPos(nPos & 0xFFC0);
}


void CModuleDoc::audioCloseDevice()
//---------------------------------
{
	if(shWaveOut) {
		while(swBuffers > 0) {
			--swBuffers;
			waveOutUnprepareHeader(shWaveOut, salpAudioBuf[swBuffers], sizeof(WAVEHDR));
			GlobalFreePtr((LPBYTE)salpAudioBuf[swBuffers]);
			salpAudioBuf[swBuffers] = NULL;
		}
		waveOutClose(shWaveOut);
		shWaveOut = NULL;
	}
}


BOOL CModuleDoc::audioOpenDevice()
//--------------------------------
{
	if(!m_pSndFile) return FALSE;
	if(shWaveOut) return TRUE;

	POSITION pos = GetFirstViewPosition();
	CView* pView = GetNextView(pos);
	if(!pView) return FALSE;
	if((slSampleSize <= 0) || (slSampleSize > sdwAudioBufferSize)) return FALSE;
	// Maybe we failed because someone is playing sound already.
	// Shut any sound off, and try once more before giving up.
	if(waveOutOpen(&shWaveOut, WAVE_MAPPER, &m_WaveFormat, (DWORD)pView->m_hWnd, 0L, CALLBACK_WINDOW)) {
		sndPlaySound(NULL, 0);
		if(waveOutOpen(&shWaveOut, WAVE_MAPPER, &m_WaveFormat, (DWORD)pView->m_hWnd, 0L, CALLBACK_WINDOW)) return FALSE;
	}

	for(swBuffers = 0; swBuffers < NUM_AUDIO_BUFFERS; swBuffers++) {
		if(!(salpAudioBuf[swBuffers] = (LPWAVEHDR)GlobalAllocPtr(GMEM_MOVEABLE | GMEM_SHARE, (DWORD)(sizeof(WAVEHDR) + sdwAudioBufferSize)))) break;
		salpAudioBuf[swBuffers]->dwFlags = WHDR_DONE;
		salpAudioBuf[swBuffers]->lpData = ((char*)salpAudioBuf[swBuffers]) + sizeof(WAVEHDR);
		salpAudioBuf[swBuffers]->dwBufferLength = sdwAudioBufferSize;
		if(!waveOutPrepareHeader(shWaveOut, salpAudioBuf[swBuffers], sizeof(WAVEHDR))) continue;
		GlobalFreePtr((LPBYTE)salpAudioBuf[swBuffers]);
		break;
	}

	if(swBuffers < 2) {
		audioCloseDevice();
		return FALSE;
	}
	swBuffersOut = 0;
	swNextBuffer = 0;
	return TRUE;
}


BOOL CModuleDoc::audioiFillBuffers()
//----------------------------------
{
	LONG lRead = 0;

	if((!shWaveOut) || (!m_pSndFile) || (!m_bPlay)) return FALSE;
	while(swBuffersOut < swBuffers) {
		lRead = m_pSndFile->Read(salpAudioBuf[swNextBuffer]->lpData, sdwAudioBufferSize);
		salpAudioBuf[swNextBuffer]->dwBufferLength = lRead * slSampleSize;
		if(!lRead) {
			OnVideoStop();
			break;
		}
		if(waveOutWrite(shWaveOut, salpAudioBuf[swNextBuffer], sizeof(WAVEHDR)) != MMSYSERR_NOERROR) return FALSE;
		++swBuffersOut;
		++swNextBuffer;
		if(swNextBuffer >= swBuffers) swNextBuffer = 0;
	}
	return TRUE;
}


BOOL CModuleDoc::OnDocIdle()
//--------------------------
{
	if(!m_bPlay) return FALSE;
	POSITION pos = GetFirstViewPosition();
	CModView* pView = (CModView*)GetNextView(pos);
	if((pView) && (m_pSndFile)) {
		m_nVideoPos = m_pSndFile->GetCurrentPos();
		pView->SetPos(m_nVideoPos);
	}
	return TRUE;
}


void CModuleDoc::SetVideoPos(int nPos)
//------------------------------------
{
	if(nPos >= m_nVideoEnd) nPos = m_nVideoEnd - 1;
	if(nPos < m_nVideoStart) nPos = m_nVideoStart;
	m_nVideoPos = nPos;
	if(m_pSndFile) m_pSndFile->SetCurrentPos(m_nVideoPos);
}


void CModuleDoc::OnUpdateVideoStop(CCmdUI * pCmdUI)
//------------------------------------------------
{
	pCmdUI->Enable((m_bPlay) ? TRUE : FALSE);
}


void CModuleDoc::OnUpdateVideoPlay(CCmdUI * pCmdUI)
//------------------------------------------------
{
	pCmdUI->Enable((m_bPlay) ? FALSE : TRUE);
}
