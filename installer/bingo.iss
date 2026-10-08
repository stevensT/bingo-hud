; Builds Bingo-X.Y.Z-setup.exe, a per-user installer around the self-contained executable.
; The version and the executable are passed in, so this script holds no version of its own:
;
;   & "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" /DAppVersion=0.3.0 /DSourceExe=artifacts\self-contained\BingoHud.App.exe /DOutputDir=artifacts\release-0.3.0 installer\bingo.iss
;
; Run it from the repository root. Every relative path, here and on the command line, is resolved
; from there (SourceDir below), not from this script's folder.
;
; installer\check-install.ps1 checks what this script is supposed to do. The names below have to
; match it and the winget manifest exactly.

#ifndef AppVersion
  #error Pass the version: /DAppVersion=X.Y.Z
#endif
#ifndef SourceExe
  #error Pass the executable to wrap: /DSourceExe=path\to\BingoHud.App.exe
#endif
#ifndef OutputDir
  #define OutputDir "artifacts"
#endif

#define AppName "Bingo"
#define AppExe "BingoHud.App.exe"

[Setup]
; Never change this. It is how an install finds the previous version to replace, and its registry
; key, {6B65FCD1-588C-4977-8EEB-6D3A62AD659F}_is1, is the ProductCode in the winget manifest. A new
; AppId would install a second copy beside the first, and winget would stop recognizing Bingo.
; The doubled opening brace is how Inno Setup writes a literal one.
AppId={{6B65FCD1-588C-4977-8EEB-6D3A62AD659F}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=stevensT
AppPublisherURL=https://github.com/stevensT/bingo-hud
AppSupportURL=https://github.com/stevensT/bingo-hud/issues
AppUpdatesURL=https://github.com/stevensT/bingo-hud/releases
VersionInfoVersion={#AppVersion}

; Settings > Apps would otherwise say "Bingo version X.Y.Z". winget matches on this name.
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}

; Per-user, so no administrator prompt. {userpf} is %LOCALAPPDATA%\Programs.
PrivilegesRequired=lowest
DefaultDirName={userpf}\{#AppName}
DisableDirPage=yes
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Bingo has no single-instance guard, so a running copy is found by the file it holds open and
; closed through Restart Manager, even when installing silently. Restart Manager reopens it after
; an upgrade.
CloseApplications=force
RestartApplications=yes

SourceDir=..
OutputDir={#OutputDir}
OutputBaseFilename=Bingo-{#AppVersion}-setup
SetupIconFile=src\BingoHud.App\Assets\bingo.ico
WizardStyle=modern
Compression=lzma2
SolidCompression=yes

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
; Unticked, so a silent install leaves it off. Inno Setup remembers the choice across upgrades.
Name: "startatsignin"; Description: "Start Bingo when I sign in"; Flags: unchecked

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; DestName: "{#AppExe}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{userstartup}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: startatsignin

[Run]
; Ticked on the last page of an interactive install; skipped when silent.
Filename: "{app}\{#AppExe}"; Description: "Launch Bingo"; Flags: nowait postinstall skipifsilent

[Code]
// The uninstaller does not use Restart Manager: an uninstall with Bingo running opened no Restart
// Manager session, and this is what closed it. It ends a copy still running from the install
// folder before its files are removed. It matches on the full path, so a copy of
// BingoHud.App.exe running from anywhere else is left alone.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ExePath: String;
  Command: String;
  ResultCode: Integer;
begin
  if CurUninstallStep <> usUninstall then
    Exit;

  // Single quotes in PowerShell are escaped by doubling them, for a user name like O'Brien.
  ExePath := ExpandConstant('{app}\{#AppExe}');
  StringChangeEx(ExePath, '''', '''''', True);

  Command :=
    '$p = @(Get-Process -Name BingoHud.App -ErrorAction SilentlyContinue | ' +
    'Where-Object { $_.Path -eq ''' + ExePath + ''' }); ' +
    '$p | Stop-Process -Force; ' +
    '$p | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue';

  Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "' + Command + '"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;
