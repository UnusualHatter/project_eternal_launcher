; =============================================================================
; Eternal TF2 Launcher — Inno Setup 6 Script
; =============================================================================
;
; PREREQUISITES
;   Inno Setup 6.3+  https://jrsoftware.org/isinfo.php
;
; HOW TO BUILD
;   1. Publish the app first:
;        scripts\publish.ps1          (PowerShell helper included in repo)
;      or manually:
;        dotnet publish src/LauncherTF2/LauncherTF2.csproj ^
;          -c Release -r win-x64 --self-contained false ^
;          -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true ^
;          -o installer\publish
;
;   2. (Recommended) Sign the output binaries before building the installer:
;        signtool sign /tr http://timestamp.digicert.com /td sha256 /fd sha256 ^
;          /a installer\publish\LauncherTF2.exe ^
;             installer\publish\steam_patcher.exe ^
;             installer\publish\pure_patcher.exe
;
;   3. Open this file in Inno Setup Compiler and press Ctrl+F9,
;      or run:  iscc installer\installer.iss
;
;   4. (Recommended) Sign the output installer:
;        signtool sign /tr http://timestamp.digicert.com /td sha256 /fd sha256 ^
;          /a installer\Output\EternalTF2Launcher-Setup-{version}.exe
;
; OUTPUT
;   installer\Output\EternalTF2Launcher-Setup-{#AppVersion}.exe
; =============================================================================

; -- Version — bump this before every release release -----------------------
#define AppVersion     "1.0.0"

; -- Identity ----------------------------------------------------------------
#define AppName        "Eternal TF2 Launcher"
#define AppExeName     "LauncherTF2.exe"
#define AppPublisher   "Project Eternal"
#define AppURL         "https://github.com/your-org/project-eternal-launcher"
#define AppMutexName   "ProjectEternalLauncher_Mutex"

; IMPORTANT: Never change AppId after the first public release.
; Windows uses this GUID to match installers to existing installations
; in Add/Remove Programs. Changing it creates duplicate entries.
#define AppId          "{{B9C3D5E2-7F1A-4B6C-A8D0-3E5F2C9D7B84}"

; -- Paths (relative to this .iss file) -------------------------------------
#define SourceDir      "publish"
#define IconFile       "..\resources\Assets\logo64_classic.ico"


; =============================================================================
[Setup]
; Identity
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
AppCopyright=Copyright (C) 2025 Project Eternal

; Installation location
; User-space install so no UAC prompt is needed and the launcher (which is
; asInvoker) can write settings.json / launcher_config.json next to the exe.
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
PrivilegesRequired=lowest
; Allow the user to elevate to admin (for a system-wide install) if they want.
PrivilegesRequiredOverridesAllowed=dialog commandline

; Architecture — the launcher only ships x64 binaries.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Windows 10 build 19041 (version 2004) is the minimum .NET 8 supports.
MinVersion=10.0.19041

; Appearance
WizardStyle=modern
SetupIconFile={#IconFile}
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}

; Automatically close the running launcher before upgrading.
; The tray-icon state is handled by the [Code] mutex check below.
CloseApplications=yes
CloseApplicationsFilter={#AppExeName}
RestartApplications=no

; Output
OutputDir=Output
OutputBaseFilename=EternalTF2Launcher-Setup-{#AppVersion}

; Compression — lzma2 gives the best ratio for .NET single-file bundles.
Compression=lzma2/ultra64
SolidCompression=yes

; Misc
DisableProgramGroupPage=yes
DisableReadyPage=no
DisableWelcomePage=no
ShowLanguageDialog=no


; =============================================================================
[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"


; =============================================================================
[Tasks]
Name: "desktopicon";  \
  Description: "Create a &desktop shortcut";  \
  GroupDescription: "Additional shortcuts:";  \
  Flags: unchecked


; =============================================================================
[Files]
; Everything produced by dotnet publish.
; PublishSingleFile bundles all managed DLLs into LauncherTF2.exe.
; The only loose files are steam_patcher.exe, pure_patcher.exe, and any
; content items with CopyToOutputDirectory=PreserveNewest.
Source: "{#SourceDir}\*"; \
  DestDir: "{app}"; \
  Flags: ignoreversion recursesubdirs createallsubdirs


; =============================================================================
[Icons]
; Start Menu
Name: "{group}\{#AppName}";             Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}";   Filename: "{uninstallexe}"

; Optional desktop shortcut
Name: "{autodesktop}\{#AppName}"; \
  Filename: "{app}\{#AppExeName}"; \
  Tasks: desktopicon


; =============================================================================
[Run]
; Offer to launch the app at the end of the installer wizard.
Filename: "{app}\{#AppExeName}"; \
  Description: "&Launch {#AppName} now"; \
  Flags: nowait postinstall skipifsilent


; =============================================================================
; Remove runtime-written files that live next to the exe.
; These are not installed by us, but Inno Setup won't delete the install
; directory on uninstall unless it is empty — listing them here ensures a
; clean removal. User profiles in %APPDATA% are handled in [Code] below.
[UninstallDelete]
Type: files;          Name: "{app}\app_debug.log"
Type: files;          Name: "{app}\crash_log.txt"
Type: files;          Name: "{app}\settings.json"
Type: files;          Name: "{app}\launcher_config.json"
Type: files;          Name: "{app}\price_cache.json"
Type: files;          Name: "{app}\mod_metadata_cache.json"
Type: files;          Name: "{app}\mod_state.json"
Type: filesandordirs; Name: "{app}\mod_thumbnails"
Type: filesandordirs; Name: "{app}\logs"
Type: dirifempty;     Name: "{app}"


; =============================================================================
[Code]

// ---------------------------------------------------------------------------
// .NET 8 Desktop Runtime — required for WPF on .NET 8
// ---------------------------------------------------------------------------
const
  DotNetDownloadUrl =
    'https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe';

// Detect .NET 8 x64 Desktop Runtime via two independent strategies so the
// check works regardless of how the user installed .NET:
//
//   1. Registry value names  — the .NET installer writes a string value named
//      after the version (e.g. "8.0.10") under the sharedfx key. Earlier
//      versions of the check used RegGetSubkeyNames, which is wrong — the
//      versions are VALUES, not sub-keys.
//
//   2. File system fallback  — check for the well-known installation directory
//      C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App\8.*\
//      This works even when the registry key is absent (e.g. xcopy deploys).
//
// Either strategy returning True is sufficient.
function IsDotNet8DesktopInstalled(): Boolean;
var
  KeyPath:    String;
  ValueNames: TArrayOfString;
  I:          Integer;
  FindRec:    TFindRec;
  BaseDir:    String;
begin
  Result  := False;

  // Strategy 1: registry VALUE names (not sub-keys)
  KeyPath := 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\'
           + 'Microsoft.WindowsDesktop.App';

  if RegGetValueNames(HKLM, KeyPath, ValueNames) then
    for I := 0 to GetArrayLength(ValueNames) - 1 do
      if (Length(ValueNames[I]) >= 2) and (Copy(ValueNames[I], 1, 2) = '8.') then
      begin
        Result := True;
        Exit;
      end;

  // Strategy 2: file system — covers xcopy/custom-path installs
  BaseDir := GetEnv('ProgramFiles')
           + '\dotnet\shared\Microsoft.WindowsDesktop.App\';

  if FindFirst(BaseDir + '8.*', FindRec) then
  try
    repeat
      if FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY <> 0 then
      begin
        Result := True;
        Exit;
      end;
    until not FindNext(FindRec);
  finally
    FindClose(FindRec);
  end;
end;

// ---------------------------------------------------------------------------
// Mutex check — detect a running launcher instance (including tray-only state)
// ---------------------------------------------------------------------------
function IsLauncherRunning(): Boolean;
begin
  // CreateMutex returns False if another process already holds the mutex.
  Result := CheckForMutexes('{#AppMutexName}');
end;

// ---------------------------------------------------------------------------
// InitializeSetup — called before the wizard appears
// ---------------------------------------------------------------------------
function InitializeSetup(): Boolean;
var
  Answer: Integer;
begin
  Result := True;

  // 1. .NET 8 check
  if not IsDotNet8DesktopInstalled() then
  begin
    Answer := MsgBox(
      '.NET 8 Desktop Runtime is required but was not found on your PC.'
      + #13#10#13#10
      + 'Click Yes to open the Microsoft download page.'
      + #13#10
      + 'Install it, then run this installer again.'
      + #13#10#13#10
      + 'Click No to cancel.',
      mbError, MB_YESNO or MB_DEFBUTTON1);

    if Answer = IDYES then
      ShellExec('open', DotNetDownloadUrl, '', '', SW_SHOWNORMAL, ewNoWait, Answer);

    Result := False;
    Exit;
  end;

  // 2. Running instance check (handles the tray-minimised case that
  //    CloseApplications= might miss when no window is visible)
  if IsLauncherRunning() then
  begin
    Answer := MsgBox(
      '{#AppName} is currently running.'
      + #13#10#13#10
      + 'Please close it before continuing (check the system tray),'
      + #13#10
      + 'or click OK to force-close it and continue.',
      mbConfirmation, MB_OKCANCEL or MB_DEFBUTTON1);

    if Answer = IDCANCEL then
    begin
      Result := False;
      Exit;
    end;

    // Force-close: terminate the process by exe name.
    Exec('taskkill.exe', '/F /IM {#AppExeName}', '', SW_HIDE,
         ewWaitUntilTerminated, Answer);

    // Brief pause to let the process exit and release its resources.
    Sleep(800);
  end;
end;

// ---------------------------------------------------------------------------
// CurUninstallStepChanged — optional AppData cleanup on uninstall
// ---------------------------------------------------------------------------
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  AppDataPath: String;
  Answer:      Integer;
begin
  if CurUninstallStep <> usPostUninstall then Exit;

  AppDataPath := ExpandConstant('{userappdata}\Eternal TF2 Launcher');

  if not DirExists(AppDataPath) then Exit;

  Answer := MsgBox(
    'Do you want to delete your saved profiles and launcher preferences?'
    + #13#10#13#10
    + AppDataPath
    + #13#10#13#10
    + 'Yes  — delete everything (profiles, caches, thumbnails).'
    + #13#10
    + 'No   — keep your data (useful if you plan to reinstall).',
    mbConfirmation, MB_YESNO or MB_DEFBUTTON2);   // Default: No

  if Answer = IDYES then
    DelTree(AppDataPath, True, True, True);
end;
