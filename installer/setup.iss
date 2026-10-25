#define AppVersion "0.4.0-preview"
[Setup]
AppId={{3B9C29C1-CC53-49D4-9B35-EA0D4835CB08}
AppName=CraftHarbor
AppVersion={#AppVersion}
VersionInfoVersion=0.4.0.0
AppPublisher=g-kari
AppPublisherURL=https://github.com/g-kari/craftharbor
AppSupportURL=https://github.com/g-kari/craftharbor/issues
DefaultDirName={localappdata}\Programs\CraftHarbor
DefaultGroupName=CraftHarbor
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern dark
SetupIconFile=..\dist\icons\craftharbor.ico
UninstallDisplayIcon={app}\CraftHarbor.exe
LicenseFile=..\LICENSE
OutputDir=..\dist
OutputBaseFilename=CraftHarbor-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
CloseApplications=no
RestartApplications=no
AppMutex=Local\CraftHarborInstallerGuard,Global\CraftHarborInstallerGuard
DisableProgramGroupPage=yes
UninstallDisplayName=CraftHarbor

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[LangOptions]
DialogFontName=Yu Gothic UI
DialogFontSize=12

[Tasks]
Name: "desktopicon"; Description: "デスクトップにショートカットを作成する"; Flags: unchecked

[InstallDelete]
; Remove only the obsolete launcher owned by older CraftHarbor installations.
Type: files; Name: "{app}\uninstall.exe"

[Files]
Source: "..\dist\CraftHarbor.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\CraftHarbor.Host.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\CraftHarbor-Uninstall-{#AppVersion}.exe"; DestDir: "{app}"; DestName: "CraftHarbor-Uninstall.exe"; Flags: ignoreversion
Source: "..\dist\icons\craftharbor.ico"; DestDir: "{app}\icons"; Flags: ignoreversion
Source: "..\dist\recipes\server-packs.json"; DestDir: "{app}\recipes"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\PRIVACY.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\CODE_SIGNING_POLICY.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\docs\PERFORMANCE.md"; DestDir: "{app}\docs"; Flags: ignoreversion
Source: "..\dist\docs\PROFILES.md"; DestDir: "{app}\docs"; Flags: ignoreversion
Source: "..\dist\docs\DESIGN.md"; DestDir: "{app}\docs"; Flags: ignoreversion
Source: "..\dist\docs\RELEASE_NOTES.md"; DestDir: "{app}\docs"; Flags: ignoreversion
Source: "..\dist\docs\SIGNPATH.md"; DestDir: "{app}\docs"; Flags: ignoreversion
Source: "..\dist\docs\BOOT_RECOVERY.md"; DestDir: "{app}\docs"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\CraftHarbor"; Filename: "{app}\CraftHarbor.exe"
Name: "{userdesktop}\CraftHarbor"; Filename: "{app}\CraftHarbor.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\CraftHarbor.exe"; Description: "CraftHarborを開く"; Flags: nowait postinstall skipifsilent unchecked

[Messages]
japanese.WelcomeLabel2=CraftHarborをこのWindowsユーザー用にインストールします。%n%nアプリ本体とショートカットを追加します。Minecraft・Java・MODパックは含みません。%n%nアンインストールしてもワールド・設定・バックアップは残ります。%n%n未署名の開発プレビューです。

; No recursive UninstallDelete entries: operator workspaces and private settings are retained.


[Code]
function InitializeUninstall(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  if FileExists(ExpandConstant('{app}\CraftHarbor.exe')) then
  begin
    Result := Exec(ExpandConstant('{app}\CraftHarbor.exe'), '--remove-boot-tasks', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    if Result then Result := ResultCode = 0;
    if not Result then
      MsgBox('CraftHarborのWindows起動タスクを解除できませんでした。運用画面から登録を解除してから、もう一度アンインストールしてください。', mbError, MB_OK);
  end;
end;
