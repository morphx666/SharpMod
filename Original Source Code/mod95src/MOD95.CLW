; CLW file contains information for the MFC ClassWizard

[General Info]
Version=1
LastClass=CModView
LastTemplate=CDialog
NewFileInclude1=#include "stdafx.h"
NewFileInclude2=#include "MOD95.h"
LastPage=0

ClassCount=8
Class1=CPlayerApp
Class2=CModuleDoc
Class3=CModView
Class4=CMainFrame
Class7=CAboutDlg

ResourceCount=5
Resource1=IDD_ABOUTBOX
Resource2=IDR_MAINFRAME
Resource5=IDD_AUDIOSETUP

[CLS:CPlayerApp]
Type=0
HeaderFile=MOD95.h
ImplementationFile=MOD95.cpp
Filter=N

[CLS:CModuleDoc]
Type=0
HeaderFile=moddoc.h
ImplementationFile=moddoc.cpp
Filter=N

[CLS:CModView]
Type=0
HeaderFile=modview.h
ImplementationFile=modview.cpp
Filter=C

[CLS:CMainFrame]
Type=0
HeaderFile=mainfrm.h
ImplementationFile=mainfrm.cpp
Filter=T



[CLS:CAboutDlg]
Type=0
HeaderFile=MOD95.cpp
ImplementationFile=MOD95.cpp
Filter=D

[DLG:IDD_ABOUTBOX]
Type=1
Class=CAboutDlg
ControlCount=4
Control1=IDC_STATIC,static,1342177283
Control2=IDC_STATIC,static,1342308352
Control3=IDC_STATIC,static,1342308352
Control4=IDOK,button,1342373889

[MNU:IDR_MAINFRAME]
Type=1
Class=CMainFrame
Command1=ID_FILE_OPEN
Command2=ID_FILE_MRU_FILE1
Command3=ID_APP_EXIT
Command4=ID_VIDEO_PLAY
Command5=ID_VIDEO_STOP
Command6=ID_AUDIOSETUP
Command7=ID_VIEW_TOOLBAR
Command8=ID_APP_ABOUT
CommandCount=8

[ACL:IDR_MAINFRAME]
Type=1
Class=CMainFrame
Command1=ID_EDIT_COPY
Command2=ID_FILE_NEW
Command3=ID_FILE_OPEN
Command4=ID_EDIT_PASTE
Command5=ID_EDIT_UNDO
Command6=ID_EDIT_CUT
Command7=ID_NEXT_PANE
Command8=ID_PREV_PANE
Command9=ID_EDIT_COPY
Command10=ID_EDIT_PASTE
Command11=ID_EDIT_CUT
Command12=ID_EDIT_UNDO
CommandCount=12

[DLG:IDD_AUDIOSETUP]
Type=1
ControlCount=8
Control1=IDOK,button,1342373889
Control2=IDCANCEL,button,1342373888
Control3=IDC_COMBO1,combobox,1344339971
Control4=IDC_STATIC,static,1342308352
Control5=IDC_STATIC,button,1342177287
Control6=IDC_STATIC,static,1342308352
Control7=IDC_EDIT1,edit,1350631552
Control8=IDC_CHECK1,button,1342242819

