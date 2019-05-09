// MOD95.h : main header file for the MOD95 application
//
#include "resource.h"       // main symbols

/////////////////////////////////////////////////////////////////////////////
// CPlayerApp:
// See MOD95.cpp for the implementation of this class
//

//==============================
class CPlayerApp: public CWinApp
//==============================
{
public:
	CPlayerApp();
	virtual BOOL InitInstance();

protected:
	CSingleDocTemplate *m_pModDoc;

protected:
	afx_msg void OnAppAbout();
	afx_msg void OnFileOpen();
	DECLARE_MESSAGE_MAP()
};


/////////////////////////////////////////////////////////////////////////////
