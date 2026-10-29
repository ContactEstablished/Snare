; Inno Setup script for Snare. Built by tools/build-installer.ps1, which passes
; AppVersion, SourceDir (the published app) and OutputDir.
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef SourceDir
  #error SourceDir is required
#endif
#ifndef OutputDir
  #define OutputDir "."
#endif

[Setup]
AppId={{6F0B7C0E-2D6B-4A53-9A57-5B5E0D3C7A11}
AppName=Snare
AppVersion={#AppVersion}
AppPublisher=Matt Wilson
; Per-user install, no admin prompt. Start with Windows only registers itself from this folder.
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\Snare
DefaultGroupName=Snare
DisableProgramGroupPage=yes
DisableDirPage=auto
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=Snare-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName=Snare
UninstallDisplayIcon={app}\Snare.exe
CloseApplications=no

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\Snare"; Filename: "{app}\Snare.exe"
Name: "{group}\Uninstall Snare"; Filename: "{uninstallexe}"

[Registry]
; Snare writes this value itself (Settings > Start with Windows). Remove it on uninstall.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "Snare"; Flags: dontcreatekey uninsdeletevalue

[Run]
Filename: "{app}\Snare.exe"; Description: "Start Snare now"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; The library and settings in %LOCALAPPDATA%\Snare are left alone.
Type: filesandordirs; Name: "{app}"

[Code]
// Snare is a tray app with no window, so Setup can't ask it to close. Stop the copy in {app}.
procedure StopSnare;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM Snare.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopSnare;
  Result := '';
end;

function InitializeUninstall: Boolean;
begin
  StopSnare;
  Result := True;
end;
