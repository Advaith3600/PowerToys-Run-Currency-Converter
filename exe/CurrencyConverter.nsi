!include "MUI2.nsh"
!include "nsProcess.nsh"

;--------------------------------
; Variables and Defines
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${name}"

;--------------------------------
; General Settings
Name "${name} ${ver} ${platform}"
OutFile ".\..\bin\${name}-${ver}-${platform}.exe"
InstallDir "$LOCALAPPDATA\Microsoft\PowerToys\PowerToys Run\Plugins\${name}"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
ManifestDPIAware true
Unicode true

Icon "icon.ico"
UninstallIcon "icon.ico"
LicenseData "..\LICENSE"
BrandingText "${name} ${ver} ${platform}"

;--------------------------------
; Interface Settings
!define MUI_ICON "icon.ico"
!define MUI_UNICON "icon.ico"
!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_NOAUTOCLOSE
!define MUI_UNFINISHPAGE_NOAUTOCLOSE

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
VIAddVersionKey /LANG=${LANG_ENGLISH} "ProductName" "${name} Setup"
VIAddVersionKey /LANG=${LANG_ENGLISH} "CompanyName" "advaith3600"
VIAddVersionKey /LANG=${LANG_ENGLISH} "LegalCopyright" "advaith3600"
VIAddVersionKey /LANG=${LANG_ENGLISH} "FileDescription" "Currency Converter plugin for PowerToys Run"
VIAddVersionKey /LANG=${LANG_ENGLISH} "FileVersion" "${ver}"
VIAddVersionKey /LANG=${LANG_ENGLISH} "ProductVersion" "${ver}"

;--------------------------------
; Functions
Function .onInit
    ; Check for PowerToys Run process
    ${nsProcess::FindProcess} "PowerToys.PowerLauncher.exe" $R0
    ${If} $R0 = 0
        MessageBox MB_OK|MB_ICONEXCLAMATION "Please close PowerToys Run before continuing the installation. You can do this by going to the system tray, right-clicking the PowerToys icon, and clicking Exit."
        Abort
    ${EndIf}
    ${nsProcess::Unload}
FunctionEnd

;--------------------------------
; Installer Sections
Section "MainSection" SEC01
    SetOutPath $INSTDIR
    SetOverwrite try
    
    ; Delete existing files first
    RMDir /r "$INSTDIR"
    
    ; Copy new files
    File /r "${direct}\*"
    
    WriteUninstaller "$INSTDIR\uninstall.exe"
    
    ; Write registry entries
    WriteRegStr HKCU "Software\advaith3600\${name}" "InstallPath" "$INSTDIR"
    WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayName" "${name}"
    WriteRegStr HKLM "${UNINSTALL_KEY}" "UninstallString" "$\"$INSTDIR\uninstall.exe$\""
    WriteRegStr HKLM "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
    WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayVersion" "${ver}"
    WriteRegStr HKLM "${UNINSTALL_KEY}" "Publisher" "advaith3600"
    WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoModify" 1
    WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoRepair" 1
SectionEnd

;--------------------------------
; Uninstaller Section
Section "Uninstall"
    ; Check for PowerToys Run process
    ${nsProcess::FindProcess} "PowerToys.PowerLauncher.exe" $R0
    ${If} $R0 = 0
        MessageBox MB_OK|MB_ICONEXCLAMATION "Please close PowerToys Run before continuing the installation. You can do this by going to the system tray, right-clicking the PowerToys icon, and clicking Exit."
        Abort
    ${EndIf}
    ${nsProcess::Unload}
    
    ; Remove files and folders
    Delete "$INSTDIR\uninstall.exe"
    RMDir /r "$INSTDIR"
    
    ; Remove registry entries
    DeleteRegKey HKCU "Software\advaith3600\${name}"
    DeleteRegKey HKLM "${UNINSTALL_KEY}"
    
    ; Notify user to restart PowerToys
    MessageBox MB_OK|MB_ICONINFORMATION "Uninstallation complete. Please restart PowerToys for the changes to take effect."
SectionEnd