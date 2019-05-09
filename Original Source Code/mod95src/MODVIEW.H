// modview.h : interface of the CModView class
//
/////////////////////////////////////////////////////////////////////////////

//===========================
class CModView : public CView
//===========================
{
protected: // create from serialization only
	DECLARE_DYNCREATE(CModView)
	CModView();
	
protected:
	CSliderCtrl Slider;
	CEdit Edit;

// Operations
public:
	CModuleDoc* GetDocument()	{ return (CModuleDoc*)m_pDocument; }

// Implementation
public:
	virtual ~CModView();
	virtual void OnDraw(CDC *pDC);
	virtual void OnInitialUpdate();
	void SetRange(int nMin,int nMax)	{ Slider.SetRange(nMin,nMax); }
	void SetPos(int nPos)				{ Slider.SetPos(nPos); }

protected:

// Generated message map functions
protected:
	afx_msg int OnCreate(LPCREATESTRUCT lpCreateStruct);
	afx_msg LONG OnWOMDone(WPARAM wParam, LPARAM lParam);
	afx_msg LONG OnWOMIgnore(WPARAM wParam, LPARAM lParam);
	afx_msg void OnSize(UINT nType,int cx,int cy);
	afx_msg LONG OnMoveSlider(WPARAM wParam, LPARAM lParam);
	afx_msg void OnVideoPlay();
	DECLARE_MESSAGE_MAP()
};


/////////////////////////////////////////////////////////////////////////////
