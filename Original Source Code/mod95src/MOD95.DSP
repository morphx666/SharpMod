# Microsoft Developer Studio Project File - Name="Mod95" - Package Owner=<4>
# Microsoft Developer Studio Generated Build File, Format Version 6.00
# ** DO NOT EDIT **

# TARGTYPE "Win32 (x86) Application" 0x0101

CFG=Mod95 - Win32 Debug
!MESSAGE This is not a valid makefile. To build this project using NMAKE,
!MESSAGE use the Export Makefile command and run
!MESSAGE 
!MESSAGE NMAKE /f "MOD95.MAK".
!MESSAGE 
!MESSAGE You can specify a configuration when running NMAKE
!MESSAGE by defining the macro CFG on the command line. For example:
!MESSAGE 
!MESSAGE NMAKE /f "MOD95.MAK" CFG="Mod95 - Win32 Debug"
!MESSAGE 
!MESSAGE Possible choices for configuration are:
!MESSAGE 
!MESSAGE "Mod95 - Win32 Debug" (based on "Win32 (x86) Application")
!MESSAGE "Mod95 - Win32 Release" (based on "Win32 (x86) Application")
!MESSAGE 

# Begin Project
# PROP AllowPerConfigDependencies 0
# PROP Scc_ProjName ""
# PROP Scc_LocalPath ""
CPP=cl.exe
MTL=midl.exe
RSC=rc.exe

!IF  "$(CFG)" == "Mod95 - Win32 Debug"

# PROP BASE Use_MFC 5
# PROP BASE Use_Debug_Libraries 1
# PROP BASE Output_Dir ".\WinDebug"
# PROP BASE Intermediate_Dir ".\WinDebug"
# PROP Use_MFC 5
# PROP Use_Debug_Libraries 1
# PROP Output_Dir ".\BIN"
# PROP Intermediate_Dir ".\OBJ"
# ADD BASE CPP /nologo /MT /W3 /GX /Zi /Od /D "_DEBUG" /D "WIN32" /D "_WINDOWS" /D "_MBCS" /FR /Yu"stdafx.h" /c
# ADD CPP /nologo /G4 /Zp4 /MTd /W4 /Gm /GX /Zi /Ox /Ot /Oa /Og /Oi /Gf /Gy /D "_DEBUG" /D "WIN32" /D "_WINDOWS" /D "_MBCS" /Yu"stdafx.h" /FD /c
# SUBTRACT CPP /Fr
# ADD MTL /mktyplib203
# ADD BASE RSC /l 0x409 /d "_DEBUG"
# ADD RSC /l 0x409 /d "_DEBUG"
BSC32=bscmake.exe
# ADD BASE BSC32 /nologo
# ADD BSC32 /nologo
LINK32=link.exe
# ADD BASE LINK32 /nologo /debug /machine:I386 /SUBSYSTEM:windows,4.0
# SUBTRACT BASE LINK32 /pdb:none
# ADD LINK32 winmm.lib vfw32.lib /nologo /pdb:none /machine:I386 /VERSION:1,0 /SUBSYSTEM:windows,4.0
# SUBTRACT LINK32 /debug

!ELSEIF  "$(CFG)" == "Mod95 - Win32 Release"

# PROP BASE Use_MFC 5
# PROP BASE Use_Debug_Libraries 0
# PROP BASE Output_Dir ".\WinRel"
# PROP BASE Intermediate_Dir ".\WinRel"
# PROP Use_MFC 5
# PROP Use_Debug_Libraries 0
# PROP Output_Dir ".\BIN"
# PROP Intermediate_Dir ".\OBJ"
# ADD BASE CPP /nologo /MT /W3 /GX /O2 /D "NDEBUG" /D "WIN32" /D "_WINDOWS" /D "_MBCS" /FR /Yu"stdafx.h" /c
# ADD CPP /nologo /G4 /Zp4 /MT /W4 /GX /Ox /Ot /Oa /Og /Oi /Gf /Gy /D "NDEBUG" /D "WIN32" /D "_WINDOWS" /D "_MBCS" /Yu"stdafx.h" /FD /c
# SUBTRACT CPP /Fr
# ADD MTL /mktyplib203
# ADD BASE RSC /l 0x409 /d "NDEBUG"
# ADD RSC /l 0x409 /d "NDEBUG"
BSC32=bscmake.exe
# ADD BASE BSC32 /nologo
# ADD BSC32 /nologo
LINK32=link.exe
# ADD BASE LINK32 /nologo /machine:I386 /SUBSYSTEM:windows,4.0
# SUBTRACT BASE LINK32 /pdb:none
# ADD LINK32 winmm.lib vfw32.lib /nologo /pdb:none /machine:I386 /VERSION:1,0 /SUBSYSTEM:windows,4.0
# SUBTRACT LINK32 /debug

!ENDIF 

# Begin Target

# Name "Mod95 - Win32 Debug"
# Name "Mod95 - Win32 Release"
# Begin Group "Source Files"

# PROP Default_Filter "cpp;c;cxx;rc;def;r;odl;idl;hpj;bat;for;f90"
# Begin Source File

SOURCE=.\mainfrm.cpp
# End Source File
# Begin Source File

SOURCE=.\MOD95.cpp
# End Source File
# Begin Source File

SOURCE=.\MOD95.rc
# End Source File
# Begin Source File

SOURCE=.\moddoc.cpp
# End Source File
# Begin Source File

SOURCE=.\modview.cpp
# End Source File
# Begin Source File

SOURCE=.\sndfile.cpp
# End Source File
# Begin Source File

SOURCE=.\stdafx.cpp
# ADD BASE CPP /Yc"stdafx.h"
# ADD CPP /Yc"stdafx.h"
# End Source File
# End Group
# Begin Group "Header Files"

# PROP Default_Filter "h;hpp;hxx;hm;inl;fi;fd"
# Begin Source File

SOURCE=.\mainfrm.h
# End Source File
# Begin Source File

SOURCE=.\MOD95.h
# End Source File
# Begin Source File

SOURCE=.\moddoc.h
# End Source File
# Begin Source File

SOURCE=.\modview.h
# End Source File
# Begin Source File

SOURCE=.\RESOURCE.H
# End Source File
# Begin Source File

SOURCE=.\sndfile.h
# End Source File
# Begin Source File

SOURCE=.\stdafx.h
# End Source File
# End Group
# Begin Group "Resource Files"

# PROP Default_Filter "ico;cur;bmp;dlg;rc2;rct;bin;cnt;rtf;gif;jpg;jpeg;jpe"
# Begin Source File

SOURCE=.\res\mod95.ico
# End Source File
# Begin Source File

SOURCE=.\res\MOD95.rc2
# End Source File
# Begin Source File

SOURCE=.\res\toolbar.bmp
# End Source File
# End Group
# End Target
# End Project
