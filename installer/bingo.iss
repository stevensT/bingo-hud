; Builds Bingo-X.Y.Z-setup.exe, a per-user installer around the self-contained executable.
; The version and the executable are passed in, so this script holds no version of its own:
;
;   & "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" /DAppVersion=X.Y.Z /DSourceExe=artifacts\self-contained\BingoHud.App.exe /DOutputDir=artifacts\release-X.Y.Z installer\bingo.iss
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

; Settings > Apps would otherwise say "Bingo version X.Y.Z". winget finds the install by its
; ProductCode (the AppId key above) and lists it by this name, which must equal its PackageName.
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
; closed through Restart Manager, even when installing silently. Restart Manager only reopens apps
; that register themselves for restart, which Bingo does not, so the code below reopens it instead.
; Restart Manager's own restart stays off so that registering one day cannot start a second copy.
CloseApplications=force
RestartApplications=no

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
; Unticked, so a first silent install leaves it off. Inno Setup remembers the choice, so an upgrade
; keeps whatever was chosen before.
Name: "startatsignin"; Description: "Start Bingo when I sign in"; Flags: unchecked

[InstallDelete]
; Inno Setup does not remove a task's shortcut when the task is unticked on a later run, so without
; this, unticking the box would leave Bingo starting at sign-in.
Type: files; Name: "{userstartup}\{#AppName}.lnk"; Tasks: not startatsignin

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; DestName: "{#AppExe}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{userstartup}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: startatsignin

[Run]
; Ticked on the last page of an interactive install; skipped when silent. Hidden when Bingo was
; already running, because the code below starts it again and two copies would run.
Filename: "{app}\{#AppExe}"; Description: "Launch Bingo"; Flags: nowait postinstall skipifsilent; Check: BingoWasNotRunning

[Code]
const
  BingoRunningExitCode = 10;

var
  BingoWasRunning: Boolean;

// Runs a PowerShell command in which $p holds every Bingo running from ExePath, and returns its
// exit code, or -1 if PowerShell could not be started. Matching on the full path leaves alone a
// copy of BingoHud.App.exe running from anywhere else.
function RunAgainstBingo(ExePath, Action: String): Integer;
var
  Command: String;
  ResultCode: Integer;
begin
  // Single quotes in PowerShell are escaped by doubling them, for a user name like O'Brien.
  StringChangeEx(ExePath, '''', '''''', True);

  Command :=
    '$p = @(Get-Process -Name BingoHud.App -ErrorAction SilentlyContinue | ' +
    'Where-Object { $_.Path -eq ''' + ExePath + ''' }); ' + Action;

  if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "' + Command + '"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    ResultCode := -1;
  Result := ResultCode;
end;

// Runs first, before Restart Manager closes anything. The wizard offers no folder choice, so the
// executable's path is known before {app} is set.
// deferred: an install moved elsewhere with /DIR= is not found running, so an upgrade closes it
// and does not start it again. Reading the previous install folder from the registry would lift it.
function InitializeSetup(): Boolean;
begin
  BingoWasRunning :=
    RunAgainstBingo(ExpandConstant('{userpf}\{#AppName}\{#AppExe}'),
      'if ($p.Count -gt 0) { exit ' + IntToStr(BingoRunningExitCode) + ' } else { exit 0 }')
    = BingoRunningExitCode;
  Result := True;
end;

function BingoWasNotRunning(): Boolean;
begin
  Result := not BingoWasRunning;
end;

// An upgrade closes a running Bingo to replace it; this starts it again, silent or not.
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if (CurStep = ssPostInstall) and BingoWasRunning then
    ShellExec('', ExpandConstant('{app}\{#AppExe}'), '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
end;

// Inno Setup's uninstaller does not use Restart Manager (an uninstall with Bingo running opened no
// session), so this ends a copy still running from the install folder before its files are removed.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep <> usUninstall then
    Exit;

  RunAgainstBingo(ExpandConstant('{app}\{#AppExe}'),
    '$p | Stop-Process -Force; $p | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue');
end;
