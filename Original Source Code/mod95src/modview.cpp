// modview.cpp : implementation of the CModView class
//

#include "stdafx.h"
#include "MOD95.h"
#include "moddoc.h"
#include "modview.h"


/////////////////////////////////////////////////////////////////////////////
// CModView
#define SLIDER_HEIGHT	20
#define IDC_SLIDER1		IDC_COMBO1

IMPLEMENT_DYNCREATE(CModView, CView)
BEGIN_MESSAGE_MAP(CModView, CView)
	ON_WM_CREATE()
	ON_WM_SIZE()
	ON_MESSAGE(MM_WOM_DONE, OnWOMDone)
	ON_MESSAGE(MM_WOM_OPEN, OnWOMIgnore)
	ON_MESSAGE(MM_WOM_CLOSE, OnWOMIgnore)
	ON_MESSAGE(WM_HSCROLL, OnMoveSlider)
	ON_COMMAND(ID_VIDEO_PLAY, OnVideoPlay)
END_MESSAGE_MAP()

/////////////////////////////////////////////////////////////////////////////
// CModView construction/destruction

CModView::CModView()
//------------------
{}


CModView::~CModView()
//-------------------
{}


void CModView::OnDraw(CDC*)
//--------------------------
{}


/////////////////////////////////////////////////////////////////////////////
// CModView message handlers

int CModView::OnCreate(LPCREATESTRUCT lpCreateStruct) 
//---------------------------------------------------
{
	int n = CView::OnCreate(lpCreateStruct);
	if(n != -1) {
		Slider.Create(WS_CHILD | WS_VISIBLE | TBS_HORZ | TBS_NOTICKS, CRect(0, 0, 256, SLIDER_HEIGHT), this, IDC_SLIDER1);
		Edit.Create(WS_CHILD | WS_VISIBLE | WS_VSCROLL | ES_LEFT | ES_MULTILINE | ES_READONLY | ES_AUTOVSCROLL, CRect(0, SLIDER_HEIGHT, 256, SLIDER_HEIGHT * 2), this, IDC_EDIT1);
		Edit.SetFont(CFont::FromHandle((HFONT)GetStockObject(DEFAULT_GUI_FONT)));
	}
	return n;
}


void CModView::OnSize(UINT nType, int cx, int cy)
//---------------------------------------------
{
	CView::OnSize(nType, cx, cy);
	if((nType == SIZE_MAXIMIZED) || (nType == SIZE_RESTORED)) {
		CRect rect;
		GetClientRect(rect);
		rect.top = rect.bottom - SLIDER_HEIGHT;
		Slider.MoveWindow(rect);
		rect.bottom = rect.top;
		rect.top = 0;
		Edit.MoveWindow(rect);
	}
}


LONG CModView::OnWOMDone(WPARAM, LPARAM)
//--------------------------------------
{
	CModuleDoc* pDoc = GetDocument();
	if(pDoc) pDoc->OnWOMDone();
	return 0;
}


LONG CModView::OnWOMIgnore(WPARAM, LPARAM)
//----------------------------------------
{
	return 0;
}


void CModView::OnInitialUpdate()
//------------------------------
{
	CView::OnInitialUpdate();
	CModuleDoc* pDoc = GetDocument();
	if(pDoc) {
		SetRange(pDoc->GetVideoStart(), pDoc->GetVideoEnd());
		SetPos(pDoc->GetVideoPos());
		CSoundFile* pMod = pDoc->GetSoundFile();
		if(pMod) {
			char s[1024], text[80];
			pMod->GetTitle(text);
			UINT nsec = pMod->GetLength();
			UINT nmin = nsec / 60;
			nsec %= 60;
			sprintf(s, "\"%s\", %umn%us", text, nmin, nsec);
			for(int i = 1; i < 32; i++) {
				pMod->GetSampleName(i, text);
				if(text[0]) {
					strcat(s, "\x0D\x0A");
					strcat(s, text);
				}
			}
			SetDlgItemText(IDC_EDIT1, s);
		}
	}
}


LONG CModView::OnMoveSlider(WPARAM wParam, LPARAM)
//------------------------------------------------
{
	CModuleDoc* pDoc = GetDocument();
	LONG nType = LOWORD(wParam), nPos = HIWORD(wParam);

	if(!pDoc) return 0;
	switch(nType) {
		case TB_LINEDOWN:
			Slider.SetPos(Slider.GetPos() + 1);
			pDoc->SetVideoPos(Slider.GetPos());
			break;

		case TB_PAGEDOWN:
			Slider.SetPos(Slider.GetPos() + 16);
			pDoc->SetVideoPos(Slider.GetPos());
			break;

		case TB_LINEUP:
			Slider.SetPos(Slider.GetPos() - 1);
			pDoc->SetVideoPos(Slider.GetPos());
			break;

		case TB_PAGEUP:
			Slider.SetPos(Slider.GetPos() - 16);
			pDoc->SetVideoPos(Slider.GetPos());
			break;

		case TB_THUMBPOSITION:
		case TB_THUMBTRACK:
			Slider.SetPos(nPos);
			pDoc->SetVideoPos(nPos);
			break;
	}
	return 0;
}


void CModView::OnVideoPlay()
//--------------------------
{
	CModuleDoc* pDoc = GetDocument();
	if(pDoc) pDoc->OnVideoPlay();
}

