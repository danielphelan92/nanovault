; NanoVault Windows installer (NSIS 3.x) — functionally equivalent to
; NanoVault.iss. This variant exists because makensis cross-compiles Windows
; installers from macOS/Linux, so a release can be packaged anywhere.
;
; Build:  makensis -DPUBLISH_DIR=<publish folder> -DOUT_FILE=<output exe> NanoVault.nsi

!ifndef APP_VERSION
  !define APP_VERSION "1.0.0"
!endif
!ifndef PUBLISH_DIR
  !define PUBLISH_DIR "..\..\artifacts\publish\win-x64"
!endif
!ifndef OUT_FILE
  !define OUT_FILE "..\..\artifacts\NanoVault-Setup-${APP_VERSION}.exe"
!endif

!define APP_NAME "NanoVault"
!define PUBLISHER "NanoVault"
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\NanoVault"

Unicode true
SetCompressor /SOLID lzma
Name "${APP_NAME}"
OutFile "${OUT_FILE}"
RequestExecutionLevel user                      ; per-user install, no admin
InstallDir "$LOCALAPPDATA\Programs\${APP_NAME}"
InstallDirRegKey HKCU "Software\${APP_NAME}" "InstallDir"

VIProductVersion "${APP_VERSION}.0"
VIAddVersionKey "ProductName" "${APP_NAME}"
VIAddVersionKey "ProductVersion" "${APP_VERSION}"
VIAddVersionKey "FileVersion" "${APP_VERSION}"
VIAddVersionKey "FileDescription" "${APP_NAME} Setup"
VIAddVersionKey "CompanyName" "${PUBLISHER}"
VIAddVersionKey "LegalCopyright" "© 2026 ${PUBLISHER}"

!include "MUI2.nsh"
!include "FileFunc.nsh"

!define MUI_ICON "..\NanoVault.App\Assets\NanoVault.ico"
!define MUI_UNICON "..\NanoVault.App\Assets\NanoVault.ico"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\NanoVault.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Start NanoVault"
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

Section "NanoVault (required)" SecApp
  SectionIn RO
  SetShellVarContext current
  SetOutPath "$INSTDIR"
  File /r "${PUBLISH_DIR}\*"

  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "Software\${APP_NAME}" "InstallDir" "$INSTDIR"

  ; Start menu shortcut.
  CreateShortCut "$SMPROGRAMS\${APP_NAME}.lnk" "$INSTDIR\NanoVault.exe"

  ; Clean uninstall entry in Windows Settings / Add-remove programs.
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "Publisher" "${PUBLISHER}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\NanoVault.exe"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKCU "${UNINSTALL_KEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoRepair" 1

  ; EstimatedSize (KB) for the Settings app.
  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  IntFmt $0 "0x%08X" $0
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "EstimatedSize" "$0"
SectionEnd

Section "Desktop shortcut" SecDesktop
  SetShellVarContext current
  CreateShortCut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\NanoVault.exe"
SectionEnd

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SecApp} "The NanoVault application (required)."
  !insertmacro MUI_DESCRIPTION_TEXT ${SecDesktop} "Optional shortcut on your desktop."
!insertmacro MUI_FUNCTION_DESCRIPTION_END

Section "Uninstall"
  SetShellVarContext current
  ; Application files and shortcuts only. User settings, logs, and any music
  ; backups are never touched.
  RMDir /r "$INSTDIR"
  Delete "$SMPROGRAMS\${APP_NAME}.lnk"
  Delete "$DESKTOP\${APP_NAME}.lnk"
  DeleteRegKey HKCU "${UNINSTALL_KEY}"
  DeleteRegKey HKCU "Software\${APP_NAME}"
SectionEnd
