!include "MUI2.nsh"

;--------------------------------
; General Settings
Name "CurrencyConverter ${ver} ${platform}"
OutFile ".\..\bin\CurrencyConverter-${ver}-${platform}.exe"
InstallDir "$LOCALAPPDATA\Microsoft\PowerToys\PowerToys Run\Plugins\CurrencyConverter"
RequestExecutionLevel user
Icon "icon.ico"
UninstallIcon "icon.ico"
LicenseData "..\LICENSE"
BrandingText "CurrencyConverter ${ver} ${platform}"

;--------------------------------
; Interface Settings
!define MUI_ICON "icon.ico"
!define MUI_UNICON "icon.ico"

;--------------------------------
; Pages
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "..\LICENSE"
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_WELCOME
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH

!insertmacro MUI_LANGUAGE "English"

;--------------------------------
; Version Information
VIProductVersion "${ver}.0"
VIAddVersionKey /LANG=${LANG_ENGLISH} "ProductName" "CurrencyConverter Setup"
VIAddVersionKey /LANG=${LANG_ENGLISH} "CompanyName" "advaith3600"
VIAddVersionKey /LANG=${LANG_ENGLISH} "LegalCopyright" "advaith3600"
VIAddVersionKey /LANG=${LANG_ENGLISH} "FileDescription" "Currency Converter plugin for PowerToys Run"
VIAddVersionKey /LANG=${LANG_ENGLISH} "FileVersion" "${ver}"
VIAddVersionKey /LANG=${LANG_ENGLISH} "ProductVersion" "${ver}"

;--------------------------------
; Installer Sections
Section ""
  ClearErrors
  ; Check if already installed
  ReadRegStr $0 HKCU "Software\advaith3600\CurrencyConverter" "InstallPath"
  IfFileExists "$0\uninstall.exe" 0 +7
  MessageBox MB_YESNO "CurrencyConverter is already installed. Do you want to uninstall it first?" IDYES uninst IDNO abort
  abort:
  Abort
  uninst:
  ; Execute the uninstaller and wait for it to complete
  ExecWait '"$0\uninstall.exe"'
  ; Check if the uninstaller process has finished
  Sleep 100
  IfFileExists "$0\uninstall.exe" loop
  loop:
  Sleep 100
  IfFileExists "$0\uninstall.exe" loop

  SetOutPath $INSTDIR
  File /r "${direct}\*"
  
  WriteUninstaller "$INSTDIR\uninstall.exe"
SectionEnd

;--------------------------------
; Add Registry Entries
Section "Add Registry Entries"
  ; Store the installation path
  WriteRegStr HKCU "Software\advaith3600\CurrencyConverter" "InstallPath" "$INSTDIR"
  
  ; Register for uninstallation
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\CurrencyConverter" "DisplayName" "CurrencyConverter"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\CurrencyConverter" "UninstallString" "$INSTDIR\uninstall.exe"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\CurrencyConverter" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\CurrencyConverter" "DisplayVersion" "${ver}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\CurrencyConverter" "Publisher" "advaith3600"
SectionEnd

;--------------------------------
; Uninstaller
Section "Uninstall"
  Delete "$INSTDIR\*.*"
  RMDir "$INSTDIR"
  DeleteRegKey HKCU "Software\advaith3600\CurrencyConverter"
  DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\CurrencyConverter"
SectionEnd

