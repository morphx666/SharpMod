// mainfrm.h : interface of the CMainFrame class
//
/////////////////////////////////////////////////////////////////////////////


//=================================
class CMainFrame : public CFrameWnd
//=================================
{
protected:
	DECLARE_DYNCREATE(CMainFrame)
	CMainFrame();

public:
	virtual ~CMainFrame();

protected:  // control bar embedded members
	CToolBar    m_wndToolBar;
	UINT		m_nAudioQuality;
	BOOL		m_bLoop;
	char		m_szModPath[_MAX_PATH];
	
// Operations
public:
	UINT GetAudioQuality()	{ return m_nAudioQuality; }
	BOOL GetLoop()			{ return m_bLoop; }
	void GetAudioPath(LPSTR s,UINT n)	{ strncpy(s,m_szModPath,n); }

// Generated message map functions
protected:
	afx_msg int OnCreate(LPCREATESTRUCT lpCreateStruct);
	afx_msg void OnClose();
	afx_msg void OnAudioSetup();
	DECLARE_MESSAGE_MAP()
};

/////////////////////////////////////////////////////////////////////////////


//====================================
class CAudioProperties: public CDialog
//====================================
{
public:
	char m_szPathName[_MAX_PATH];
	UINT m_nQuality;
	BOOL m_bLoop;
	
public:
	CAudioProperties(LPSTR lpszPathName,UINT nQuality,BOOL bLoop,CWnd *parent=NULL,UINT nID=IDD_AUDIOSETUP);

protected:
	virtual BOOL OnInitDialog();
	virtual void OnOK();
};
