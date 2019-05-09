// MOD95.cpp : Defines the class behaviors for the application.
//
#include "stdafx.h"
#include "MOD95.h"
#include "mainfrm.h"
#include "moddoc.h"
#include "modview.h"

/////////////////////////////////////////////////////////////////////////////
// CPlayerApp

BEGIN_MESSAGE_MAP(CPlayerApp, CWinApp)
	ON_COMMAND(ID_APP_ABOUT,	OnAppAbout)
	ON_COMMAND(ID_FILE_NEW,		OnFileNew)
	ON_COMMAND(ID_FILE_OPEN,	OnFileOpen)
END_MESSAGE_MAP()

/////////////////////////////////////////////////////////////////////////////
// CPlayerApp construction

CPlayerApp::CPlayerApp()
//----------------------
{
	m_pModDoc = NULL;
}

/////////////////////////////////////////////////////////////////////////////
// The one and only CPlayerApp object

CPlayerApp theApp;

/////////////////////////////////////////////////////////////////////////////
// CPlayerApp initialization

BOOL CPlayerApp::InitInstance()
//-----------------------------
{
	// Standard initialization
	Enable3dControls();
	LoadStdProfileSettings();  // Load standard INI file options (including MRU)
	// Register document templates
	m_pModDoc = new CSingleDocTemplate(
		IDR_MAINFRAME,
		RUNTIME_CLASS(CModuleDoc),
		RUNTIME_CLASS(CMainFrame),
		RUNTIME_CLASS(CModView));
	AddDocTemplate(m_pModDoc);
	// Enable DDE Execute open
	EnableShellOpen();
	RegisterShellFileTypes();
	// simple command line parsing
	OnFileNew();
	if (m_lpCmdLine[0]) OpenDocumentFile(m_lpCmdLine);
	if (!m_pMainWnd) return FALSE;
	// Enable drag/drop open
	m_pMainWnd->DragAcceptFiles();
	SetThreadPriority(THREAD_PRIORITY_HIGHEST);
	return TRUE;
}


static BOOL bAutoChangeDir = TRUE;

void CPlayerApp::OnFileOpen()
//---------------------------
{
	char s[_MAX_PATH];
	CFileDialog dlg(TRUE,NULL,NULL,OFN_FILEMUSTEXIST|OFN_HIDEREADONLY|OFN_PATHMUSTEXIST|OFN_OVERWRITEPROMPT,
		"Modules Amiga (*.mod)|*.mod|Tous les fichiers (*.*)|*.*||",m_pMainWnd);
	if ((bAutoChangeDir) && (m_pMainWnd))
	{
		((CMainFrame *)m_pMainWnd)->GetAudioPath(s,sizeof(s));
		if (s[0]) dlg.m_ofn.lpstrInitialDir = s;
	}
	if (dlg.DoModal()==IDOK) OpenDocumentFile(dlg.GetPathName());
}


/////////////////////////////////////////////////////////////////////////////
// CAboutDlg dialog used for App About

class CAboutDlg: public CDialog
//-----------------------------
{
public:
	CAboutDlg():CDialog(IDD_ABOUTBOX) {}

// Implementation
protected:
	DECLARE_MESSAGE_MAP()
};


BEGIN_MESSAGE_MAP(CAboutDlg, CDialog)
END_MESSAGE_MAP()


// App command to run the dialog
void CPlayerApp::OnAppAbout()
//---------------------------
{
	CAboutDlg aboutDlg;
	aboutDlg.DoModal();
}

/////////////////////////////////////////////////////////////////////////////
// CPlayerApp commands
