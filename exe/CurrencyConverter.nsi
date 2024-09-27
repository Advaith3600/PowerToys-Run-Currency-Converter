!define CC "CurrencyConverter"

LoadLanguageFile "${NSISDIR}\Contrib\Language files\English.nlf"

;--------------------------------
;Version Information
VIProductVersion "${ver}.0"
VIAddVersionKey /LANG=${LANG_ENGLISH} "ProductName" "${CC} Setup"
VIAddVersionKey /LANG=${LANG_ENGLISH} "CompanyName" "advaith3600"
VIAddVersionKey /LANG=${LANG_ENGLISH} "LegalCopyright" "advaith3600"
VIAddVersionKey /LANG=${LANG_ENGLISH} "FileDescription" "Currency Converter plugin for PowerToys Run"
VIAddVersionKey /LANG=${LANG_ENGLISH} "FileVersion" "${ver}"
VIAddVersionKey /LANG=${LANG_ENGLISH} "ProductVersion" "${ver}"
;--------------------------------

BrandingText "${CC} v${ver}"
CRCCheck force
FileErrorText "Can't write: $\r$\n$\r$\n$0$\r$\n$\r$\nPowerToys is probably still running, please close it and retry."
Icon icon.ico
InstallDir "$LOCALAPPDATA\Microsoft\PowerToys\PowerToys Run\Plugins\CurrencyConverter"
Name "${CC}"
OutFile ".\..\bin\${CC}-${ver}-${platform}.exe"
RequestExecutionLevel user
SetCompressor /SOLID /FINAL lzma
LicenseData "..\LICENSE"

;--------------------------------

Page license
Page instfiles

;--------------------------------

Section ""
  ClearErrors
  SetOutPath $INSTDIR
  GetFullPathName $0 "$EXEDIR\"
  GetFullPathName $0 $0
  File /r "${direct}\*"
  
  WriteUninstaller "$INSTDIR\uninstall.exe"

  IfErrors 0 +5
  SetErrorlevel 1
  IfSilent +2
  MessageBox MB_ICONEXCLAMATION "Unable to install, PowerToys is probably still running, please close it manually before install."
  Abort
SectionEnd

;--------------------------------
; Add Registry Entries
Section "Add Registry Entries"
  ; Store the installation path
  WriteRegStr HKCU "Software\advaith3600\CurrencyConverter" "InstallPath" "$INSTDIR"
  
  ; Register for uninstallation
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${CC}" "DisplayName" "${CC}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${CC}" "UninstallString" "$INSTDIR\uninstall.exe"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${CC}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${CC}" "DisplayVersion" "${ver}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${CC}" "Publisher" "advaith3600"
SectionEnd

;--------------------------------
; Uninstaller
Section "Uninstall"
  Delete "$INSTDIR\*.*"
  RMDir "$INSTDIR"
  DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${CC}"
SectionEnd

UninstallIcon icon.ico
