#define AppVersion "0.3.1-preview"
[Setup]
AppId={{3B9C29C1-CC53-49D4-9B35-EA0D4835CB08}
AppName=CraftHarbor
AppVersion={#AppVersion}
VersionInfoVersion=0.3.1.0
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
OutputBaseFilename=setup
Compression=lzma2
SolidCompression=yes
CloseApplications=no
RestartApplications=no
AppMutex=Local\CraftHarborInstallerGuard
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

[Files]
Source: "..\dist\CraftHarbor.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\CraftHarbor.Host.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\uninstall.exe"; DestDir: "{app}"; Flags: ignoreversion
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

[Icons]
Name: "{userprograms}\CraftHarbor"; Filename: "{app}\CraftHarbor.exe"
Name: "{userdesktop}\CraftHarbor"; Filename: "{app}\CraftHarbor.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\CraftHarbor.exe"; Description: "CraftHarborを開く"; Flags: nowait postinstall skipifsilent unchecked

[Messages]
japanese.WelcomeLabel2=CraftHarborをこのWindowsユーザー用にインストールします。%n%nアプリ本体とショートカットを追加します。Minecraft・Java・MODパックは含みません。%n%nアンインストールしてもワールド・設定・バックアップは残ります。%n%n未署名の開発プレビューです。

; No recursive UninstallDelete entries: operator workspaces and private settings are retained.
