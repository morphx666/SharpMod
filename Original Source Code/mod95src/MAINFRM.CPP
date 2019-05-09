// mainfrm.cpp : implementation of the CMainFrame class
//

#include "stdafx.h"
#include "MOD95.h"
#include "mainfrm.h"

#define TIMER_SPEED		200


CAudioProperties::CAudioProperties(LPSTR lpszPathName,UINT nQuality,BOOL bLoop,CWnd *parent,UINT nID):CDialog(nID,parent)
//-----------------------------------------------------------------------------------------------------------------------
{
	strncpy(m_szPathName,lpszPathName,sizeof(m_szPathName));
	m_nQuality = nQuality;
	m_bLoop = bLoop;
}


BOOL CAudioProperties::OnInitDialog()
//-----------------------------------
{
	BOOL bOK = CDialog::OnInitDialog();
	CComboBox *combo;

	SetDlgItemText(IDC_EDIT1,m_szPathName);
	CheckDlgButton(IDC_CHECK1,m_bLoop);
	if ((combo = (CComboBox *)GetDlgItem(IDC_COMBO1)) != NULL)
	{
		combo->AddString("11025 Hz; 8 Bit; Mono");
		combo->AddString("11025 Hz; 8 Bit; Stereo");
		combo->AddString("11025 Hz; 16 Bit; Mono");
		combo->AddString("11025 Hz; 16 Bit; Stereo");
		combo->AddString("22050 Hz; 8 Bit; Mono");
		combo->AddString("22050 Hz; 8 Bit; Stereo");
		combo->AddString("22050 Hz; 16 Bit; Mono");
		combo->AddString("22050 Hz; 16 Bit; Stereo");
		combo->AddString("44100 Hz; 8 Bit; Mono");
		combo->AddString("44100 Hz; 8 Bit; Stereo");
		combo->AddString("44100 Hz; 16 Bit; Mono");
		combo->AddString("44100 Hz; 16 Bit; Stereo");
		combo->SetCurSel(m_nQuality);
	}
	return bOK;
}


void CAudioProperties::OnOK()
//---------------------------
{
	GetDlgItemText(IDC_EDIT1,m_szPathName,sizeof(m_szPathName));
	int n = ((CComboBox *)GetDlgItem(IDC_COMBO1))->GetCurSel();
	if ((n>=0) && (n<12)) m_nQuality = n;
	m_bLoop = IsDlgButtonChecked(IDC_CHECK1) ? TRUE : FALSE;
	CDialog::OnOK();
}




/////////////////////////////////////////////////////////////////////////////
// CMainFrame

IMPLEMENT_DYNCREATE(CMainFrame, CFrameWnd)
BEGIN_MESSAGE_MAP(CMainFrame, CFrameWnd)
	ON_WM_CREATE()
	ON_WM_CLOSE()
	ON_COMMAND(ID_AUDIOSETUP,	OnAudioSetup)
END_MESSAGE_MAP()

/////////////////////////////////////////////////////////////////////////////
	
// toolbar buttons - IDs are command buttons
static UINT BASED_CODE buttons[] =
{
	// same order as in the bitmap 'toolbar.bmp'
	ID_FILE_OPEN,	ID_SEPARATOR,
	ID_VIDEO_STOP,	ID_VIDEO_PLAY,	ID_VIDEO_PREV,	ID_VIDEO_NEXT,	ID_SEPARATOR,
	ID_APP_ABOUT,
};


/////////////////////////////////////////////////////////////////////////////
// CMainFrame construction/destruction

CMainFrame::CMainFrame()
//----------------------
{
	m_nAudioQuality = 4;
	m_szModPath[0] = 0;
	m_bLoop = FALSE;
}


CMainFrame::~CMainFrame()
//-----------------------
{
}


int CMainFrame::OnCreate(LPCREATESTRUCT lpCreateStruct)
//-----------------------------------------------------
{
	CRect rect;
	CString str;
	char *Section;

	if (CFrameWnd::OnCreate(lpCreateStruct) == -1) return -1;
	if (!m_wndToolBar.Create(this) ||
		!m_wndToolBar.LoadBitmap(IDR_TOOLBAR) ||
		!m_wndToolBar.SetButtons(buttons,
		  sizeof(buttons)/sizeof(UINT))) return -1;
	m_wndToolBar.EnableDocking(CBRS_ALIGN_ANY);
	EnableDocking(CBRS_ALIGN_ANY);
	DockControlBar(&m_wndToolBar);
	m_wndToolBar.SetBarStyle(m_wndToolBar.GetBarStyle() | CBRS_TOOLTIPS | CBRS_FLYBY);
	Section = "Audio";
	m_nAudioQuality = AfxGetApp()->GetProfileInt(Section,"Quality",m_nAudioQuality);
	m_bLoop = AfxGetApp()->GetProfileInt(Section,"Loop",m_bLoop);
	str = AfxGetApp()->GetProfileString(Section,"Path","");
	strncpy(m_szModPath,str,sizeof(m_szModPath));
	Section = "Window";
	if (AfxGetApp()->GetProfileInt(Section,"RunMaximized",0)) AfxGetApp()->m_nCmdShow = SW_SHOWMAXIMIZED;
	str = AfxGetApp()->GetProfileString(Section,"Position","0 0 0 0");
	sscanf(str,"%d %d %d %d",&rect.left,&rect.top,&rect.right,&rect.bottom);
	if ((rect.right > rect.left) && (rect.bottom > rect.top)
	 && (rect.right > 0) && (rect.left < ::GetSystemMetrics(SM_CXSCREEN))
	 && (rect.bottom > 0) && (rect.top < ::GetSystemMetrics(SM_CYSCREEN)) )
	{
		MoveWindow(rect);
	}
	if (!AfxGetApp()->GetProfileInt(Section,"ToolBar",1))
		m_wndToolBar.ShowWindow(SW_HIDE);
	return 0;
}


void CMainFrame::OnClose()
//------------------------
{
	CRect rect;
	char s[80],*Section = "Window";
	
	// Save Window Settings
	if (!IsZoomed() && !IsIconic())
	{
		GetWindowRect(&rect);
		sprintf(s,"%d %d %d %d",rect.left,rect.top,rect.right,rect.bottom);
		AfxGetApp()->WriteProfileString(Section,"Position",s);
	}
	AfxGetApp()->WriteProfileInt(Section,"RunMaximized",IsZoomed());
	AfxGetApp()->WriteProfileInt(Section,"ToolBar",m_wndToolBar.IsWindowVisible());
	Section = "Audio";
	AfxGetApp()->WriteProfileInt(Section,"Quality",m_nAudioQuality);
	AfxGetApp()->WriteProfileInt(Section,"Loop",m_bLoop);
	AfxGetApp()->WriteProfileString(Section,"Path",m_szModPath);
	CFrameWnd::OnClose();
}


void CMainFrame::OnAudioSetup()
//-----------------------------
{
	CAudioProperties dlg(m_szModPath,m_nAudioQuality,m_bLoop,this);
	if (dlg.DoModal()==IDOK)
	{
		m_nAudioQuality = dlg.m_nQuality;
		m_bLoop = dlg.m_bLoop;
		strncpy(m_szModPath,dlg.m_szPathName,sizeof(m_szModPath));
	}
}

