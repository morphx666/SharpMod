# Microsoft Developer Studio Generated NMAKE File, Format Version 4.00
# ** DO NOT EDIT **

# TARGTYPE "Win32 (x86) Application" 0x0101

!IF "$(CFG)" == ""
CFG=Mod95 - Win32 Debug
!MESSAGE No configuration specified.  Defaulting to Mod95 - Win32 Debug.
!ENDIF 

!IF "$(CFG)" != "Mod95 - Win32 Debug" && "$(CFG)" != "Mod95 - Win32 Release"
!MESSAGE Invalid configuration "$(CFG)" specified.
!MESSAGE You can specify a configuration when running NMAKE on this makefile
!MESSAGE by defining the macro CFG on the command line.  For example:
!MESSAGE 
!MESSAGE NMAKE /f "Mod95.mak" CFG="Mod95 - Win32 Debug"
!MESSAGE 
!MESSAGE Possible choices for configuration are:
!MESSAGE 
!MESSAGE "Mod95 - Win32 Debug" (based on "Win32 (x86) Application")
!MESSAGE "Mod95 - Win32 Release" (based on "Win32 (x86) Application")
!MESSAGE 
!ERROR An invalid configuration is specified.
!ENDIF 

!IF "$(OS)" == "Windows_NT"
NULL=
!ELSE 
NULL=nul
!ENDIF 
################################################################################
# Begin Project
CPP=cl.exe
RSC=rc.exe
MTL=mktyplib.exe

!IF  "$(CFG)" == "Mod95 - Win32 Debug"

# PROP BASE Use_MFC 5
# PROP BASE Use_Debug_Libraries 1
# PROP BASE Output_Dir "WinDebug"
# PROP BASE Intermediate_Dir "WinDebug"
# PROP Use_MFC 5
# PROP Use_Debug_Libraries 1
# PROP Output_Dir "BIN"
# PROP Intermediate_Dir "OBJ"
OUTDIR=.\BIN
INTDIR=.\OBJ

ALL : "$(OUTDIR)\Mod95.exe"

CLEAN : 
	-@erase ".\BIN\Mod95.exe"
	-@erase ".\OBJ\stdafx.obj"
	-@erase ".\OBJ\MOD95.obj"
	-@erase ".\OBJ\Mod95.pch"
	-@erase ".\OBJ\mainfrm.obj"
	-@erase ".\OBJ\MOD95.res"
	-@erase ".\OBJ\sndfile.obj"
	-@erase ".\OBJ\modview.obj"
	-@erase ".\OBJ\moddoc.obj"
	-@erase ".\OBJ\vc40.pdb"
	-@erase ".\OBJ\vc40.idb"

"$(OUTDIR)" :
    if not exist "$(OUTDIR)/$(NULL)" mkdir "$(OUTDIR)"

"$(INTDIR)" :
    if not exist "$(INTDIR)/$(NULL)" mkdir "$(INTDIR)"

# ADD BASE CPP /nologo /MT /W3 /GX /Zi /Od /D "_DEBUG" /D "WIN32" /D "_WINDOWS" /D "_MBCS" /FR /Yu"stdafx.h" /c
# ADD CPP /nologo /G4 /Zp4 /MTd /W4 /Gm /GX /Zi /Ox /Ot /Oa /Og /Oi /Gf /Gy /D "_DEBUG" /D "WIN32" /D "_WINDOWS" /D "_MBCS" /Yu"stdafx.h" /c
# SUBTRACT CPP /Fr
CPP_PROJ=/nologo /G4 /Zp4 /MTd /W4 /Gm /GX /Zi /Ox /Ot /Oa /Og /Oi /Gf /Gy /D\
 "_DEBUG" /D "WIN32" /D "_WINDOWS" /D "_MBCS" /Fp"$(INTDIR)/Mod95.pch"\
 /Yu"stdafx.h" /Fo"$(INTDIR)/" /Fd"$(INTDIR)/" /c 
CPP_OBJS=.\OBJ/
CPP_SBRS=
# ADD BASE RSC /l 0x409 /d "_DEBUG"
# ADD RSC /l 0x409 /d "_DEBUG"
RSC_PROJ=/l 0x409 /fo"$(INTDIR)/MOD95.res" /d "_DEBUG" 
BSC32=bscmake.exe
# ADD BASE BSC32 /nologo
# ADD BSC32 /nologo
BSC32_FLAGS=/nologo /o"$(OUTDIR)/Mod95.bsc" 
BSC32_SBRS=
LINK32=link.exe
# ADD BASE LINK32 /nologo /debug /machine:I386 /SUBSYSTEM:windows,4.0
# SUBTRACT BASE LINK32 /pdb:none
# ADD LINK32 winmm.lib vfw32.lib /nologo /pdb:none /machine:I386 /VERSION:1,0 /SUBSYSTEM:windows,4.0
# SUBTRACT LINK32 /debug
LINK32_FLAGS=winmm.lib vfw32.lib /nologo /pdb:none /machine:I386\
 /out:"$(OUTDIR)/Mod95.exe" /VERSION:1,0 /SUBSYSTEM:windows,4.0 
LINK32_OBJS= \
	"$(INTDIR)/stdafx.obj" \
	"$(INTDIR)/MOD95.obj" \
	"$(INTDIR)/mainfrm.obj" \
	"$(INTDIR)/sndfile.obj" \
	"$(INTDIR)/modview.obj" \
	"$(INTDIR)/moddoc.obj" \
	"$(INTDIR)/MOD95.res"

"$(OUTDIR)\Mod95.exe" : "$(OUTDIR)" $(DEF_FILE) $(LINK32_OBJS)
    $(LINK32) @<<
  $(LINK32_FLAGS) $(LINK32_OBJS)
<<

!ELSEIF  "$(CFG)" == "Mod95 - Win32 Release"

# PROP BASE Use_MFC 5
# PROP BASE Use_Debug_Libraries 0
# PROP BASE Output_Dir "WinRel"
# PROP BASE Intermediate_Dir "WinRel"
# PROP Use_MFC 5
# PROP Use_Debug_Libraries 0
# PROP Output_Dir "BIN"
# PROP Intermediate_Dir "OBJ"
OUTDIR=.\BIN
INTDIR=.\OBJ

ALL : "$(OUTDIR)\Mod95.exe"

CLEAN : 
	-@erase ".\BIN\Mod95.exe"
	-@erase ".\OBJ\stdafx.obj"
	-@erase ".\OBJ\MOD95.obj"
	-@erase ".\OBJ\Mod95.pch"
	-@erase ".\OBJ\mainfrm.obj"
	-@erase ".\OBJ\MOD95.res"
	-@erase ".\OBJ\sndfile.obj"
	-@erase ".\OBJ\modview.obj"
	-@erase ".\OBJ\moddoc.obj"

"$(OUTDIR)" :
    if not exist "$(OUTDIR)/$(NULL)" mkdir "$(OUTDIR)"

"$(INTDIR)" :
    if not exist "$(INTDIR)/$(NULL)" mkdir "$(INTDIR)"

# ADD BASE CPP /nologo /MT /W3 /GX /O2 /D "NDEBUG" /D "WIN32" /D "_WINDOWS" /D "_MBCS" /FR /Yu"stdafx.h" /c
# ADD CPP /nologo /G4 /Zp4 /MT /W4 /GX /Ox /Ot /Oa /Og /Oi /Gf /Gy /D "NDEBUG" /D "WIN32" /D "_WINDOWS" /D "_MBCS" /Yu"stdafx.h" /c
# SUBTRACT CPP /Fr
CPP_PROJ=/nologo /G4 /Zp4 /MT /W4 /GX /Ox /Ot /Oa /Og /Oi /Gf /Gy /D "NDEBUG"\
 /D "WIN32" /D "_WINDOWS" /D "_MBCS" /Fp"$(INTDIR)/Mod95.pch" /Yu"stdafx.h"\
 /Fo"$(INTDIR)/" /c 
CPP_OBJS=.\OBJ/
CPP_SBRS=
# ADD BASE RSC /l 0x409 /d "NDEBUG"
# ADD RSC /l 0x409 /d "NDEBUG"
RSC_PROJ=/l 0x409 /fo"$(INTDIR)/MOD95.res" /d "NDEBUG" 
BSC32=bscmake.exe
# ADD BASE BSC32 /nologo
# ADD BSC32 /nologo
BSC32_FLAGS=/nologo /o"$(OUTDIR)/Mod95.bsc" 
BSC32_SBRS=
LINK32=link.exe
# ADD BASE LINK32 /nologo /machine:I386 /SUBSYSTEM:windows,4.0
# SUBTRACT BASE LINK32 /pdb:none
# ADD LINK32 winmm.lib vfw32.lib /nologo /pdb:none /machine:I386 /VERSION:1,0 /SUBSYSTEM:windows,4.0
# SUBTRACT LINK32 /debug
LINK32_FLAGS=winmm.lib vfw32.lib /nologo /pdb:none /machine:I386\
 /out:"$(OUTDIR)/Mod95.exe" /VERSION:1,0 /SUBSYSTEM:windows,4.0 
LINK32_OBJS= \
	"$(INTDIR)/stdafx.obj" \
	"$(INTDIR)/MOD95.obj" \
	"$(INTDIR)/mainfrm.obj" \
	"$(INTDIR)/sndfile.obj" \
	"$(INTDIR)/modview.obj" \
	"$(INTDIR)/moddoc.obj" \
	"$(INTDIR)/MOD95.res"

"$(OUTDIR)\Mod95.exe" : "$(OUTDIR)" $(DEF_FILE) $(LINK32_OBJS)
    $(LINK32) @<<
  $(LINK32_FLAGS) $(LINK32_OBJS)
<<

!ENDIF 

.c{$(CPP_OBJS)}.obj:
   $(CPP) $(CPP_PROJ) $<  

.cpp{$(CPP_OBJS)}.obj:
   $(CPP) $(CPP_PROJ) $<  

.cxx{$(CPP_OBJS)}.obj:
   $(CPP) $(CPP_PROJ) $<  

.c{$(CPP_SBRS)}.sbr:
   $(CPP) $(CPP_PROJ) $<  

.cpp{$(CPP_SBRS)}.sbr:
   $(CPP) $(CPP_PROJ) $<  

.cxx{$(CPP_SBRS)}.sbr:
   $(CPP) $(CPP_PROJ) $<  

MTL_PROJ=
################################################################################
# Begin Target

# Name "Mod95 - Win32 Debug"
# Name "Mod95 - Win32 Release"

!IF  "$(CFG)" == "Mod95 - Win32 Debug"

!ELSEIF  "$(CFG)" == "Mod95 - Win32 Release"

!ENDIF 

################################################################################
# Begin Source File

SOURCE=.\stdafx.cpp
DEP_CPP_STDAF=\
	".\stdafx.h"\
	

!IF  "$(CFG)" == "Mod95 - Win32 Debug"

# ADD BASE CPP /Yc"stdafx.h"
# ADD CPP /Yc"stdafx.h"

BuildCmds= \
	$(CPP) /nologo /G4 /Zp4 /MTd /W4 /Gm /GX /Zi /Ox /Ot /Oa /Og /Oi /Gf /Gy /D\
 "_DEBUG" /D "WIN32" /D "_WINDOWS" /D "_MBCS" /Fp"$(INTDIR)/Mod95.pch"\
 /Yc"stdafx.h" /Fo"$(INTDIR)/" /Fd"$(INTDIR)/" /c $(SOURCE) \
	

"$(INTDIR)\stdafx.obj" : $(SOURCE) $(DEP_CPP_STDAF) "$(INTDIR)"
   $(BuildCmds)

"$(INTDIR)\Mod95.pch" : $(SOURCE) $(DEP_CPP_STDAF) "$(INTDIR)"
   $(BuildCmds)

!ELSEIF  "$(CFG)" == "Mod95 - Win32 Release"

# ADD BASE CPP /Yc"stdafx.h"
# ADD CPP /Yc"stdafx.h"

BuildCmds= \
	$(CPP) /nologo /G4 /Zp4 /MT /W4 /GX /Ox /Ot /Oa /Og /Oi /Gf /Gy /D "NDEBUG" /D\
 "WIN32" /D "_WINDOWS" /D "_MBCS" /Fp"$(INTDIR)/Mod95.pch" /Yc"stdafx.h"\
 /Fo"$(INTDIR)/" /c $(SOURCE) \
	

"$(INTDIR)\stdafx.obj" : $(SOURCE) $(DEP_CPP_STDAF) "$(INTDIR)"
   $(BuildCmds)

"$(INTDIR)\Mod95.pch" : $(SOURCE) $(DEP_CPP_STDAF) "$(INTDIR)"
   $(BuildCmds)

!ENDIF 

# End Source File
################################################################################
# Begin Source File

SOURCE=.\MOD95.cpp
DEP_CPP_MOD95=\
	".\stdafx.h"\
	".\MOD95.h"\
	".\mainfrm.h"\
	".\moddoc.h"\
	".\modview.h"\
	".\sndfile.h"\
	

"$(INTDIR)\MOD95.obj" : $(SOURCE) $(DEP_CPP_MOD95) "$(INTDIR)"\
 "$(INTDIR)\Mod95.pch"


# End Source File
################################################################################
# Begin Source File

SOURCE=.\mainfrm.cpp
DEP_CPP_MAINF=\
	".\stdafx.h"\
	".\MOD95.h"\
	".\mainfrm.h"\
	

"$(INTDIR)\mainfrm.obj" : $(SOURCE) $(DEP_CPP_MAINF) "$(INTDIR)"\
 "$(INTDIR)\Mod95.pch"


# End Source File
################################################################################
# Begin Source File

SOURCE=.\MOD95.rc
DEP_RSC_MOD95_=\
	".\res\mod95.ico"\
	".\res\toolbar.bmp"\
	".\res\MOD95.rc2"\
	

"$(INTDIR)\MOD95.res" : $(SOURCE) $(DEP_RSC_MOD95_) "$(INTDIR)"
   $(RSC) $(RSC_PROJ) $(SOURCE)


# End Source File
################################################################################
# Begin Source File

SOURCE=.\sndfile.cpp
DEP_CPP_SNDFI=\
	".\stdafx.h"\
	".\sndfile.h"\
	

"$(INTDIR)\sndfile.obj" : $(SOURCE) $(DEP_CPP_SNDFI) "$(INTDIR)"\
 "$(INTDIR)\Mod95.pch"


# End Source File
################################################################################
# Begin Source File

SOURCE=.\modview.cpp
DEP_CPP_MODVI=\
	".\stdafx.h"\
	".\MOD95.h"\
	".\moddoc.h"\
	".\modview.h"\
	".\sndfile.h"\
	

"$(INTDIR)\modview.obj" : $(SOURCE) $(DEP_CPP_MODVI) "$(INTDIR)"\
 "$(INTDIR)\Mod95.pch"


# End Source File
################################################################################
# Begin Source File

SOURCE=.\moddoc.cpp
DEP_CPP_MODDO=\
	".\stdafx.h"\
	".\MOD95.h"\
	".\mainfrm.h"\
	".\moddoc.h"\
	".\modview.h"\
	".\sndfile.h"\
	

"$(INTDIR)\moddoc.obj" : $(SOURCE) $(DEP_CPP_MODDO) "$(INTDIR)"\
 "$(INTDIR)\Mod95.pch"


# End Source File
# End Target
# End Project
################################################################################
