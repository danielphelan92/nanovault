; NanoVault Windows installer (Inno Setup 6).
; Build on Windows with:  iscc /DPublishDir=..\..\artifacts\publish\win-x64 NanoVault.iss
; Produces artifacts\NanoVault-Setup-<version>.exe

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\..\artifacts\publish\win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\artifacts"
#endif

[Setup]
AppId={{7E1B9C40-58F1-4D46-9A6E-0A3D3F2C9B11}
AppName=NanoVault
AppVersion={#AppVersion}
AppVerName=NanoVault {#AppVersion}
AppPublisher=NanoVault
DefaultDirName={autopf}\NanoVault
DefaultGroupName=NanoVault
DisableProgramGroupPage=yes
; Installs per-user by default; no administrator rights required.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir={#OutputDir}
OutputBaseFilename=NanoVault-Setup-{#AppVersion}
SetupIconFile=..\NanoVault.App\Assets\NanoVault.ico
UninstallDisplayIcon={app}\NanoVault.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19045

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\NanoVault"; Filename: "{app}\NanoVault.exe"
Name: "{autodesktop}\NanoVault"; Filename: "{app}\NanoVault.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\NanoVault.exe"; Description: "{cm:LaunchProgram,NanoVault}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Application files only. User settings, logs, and backups are never removed.
Type: filesandordirs; Name: "{app}"
