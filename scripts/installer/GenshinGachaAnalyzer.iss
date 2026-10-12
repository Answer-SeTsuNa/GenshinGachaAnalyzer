; AppVersion 仅由 Directory.Build.props 注入。固定 AppId 使后续版本升级同一安装。
#ifndef AppVersion
  #error 请使用 scripts/Build-Installer.ps1 构建。
#endif
#define AppName "木偶原神抽卡分析工具"
#define AppExe "GenshinGachaAnalyzer.exe"

[Setup]
AppId=GenshinGachaAnalyzer
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=木偶
AppPublisherURL=https://github.com/Answer-SeTsuNa/GenshinGachaAnalyzer
AppSupportURL=https://github.com/Answer-SeTsuNa/GenshinGachaAnalyzer/issues
AppUpdatesURL=https://github.com/Answer-SeTsuNa/GenshinGachaAnalyzer/releases
DefaultDirName={localappdata}\Programs\GenshinGachaAnalyzer
DefaultGroupName={#AppName}
DisableProgramGroupPage=no
DisableDirPage=no
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir={#ProjectRoot}\artifacts
OutputBaseFilename=GenshinGachaAnalyzer-Setup-v{#AppVersion}-win-x64
SetupIconFile={#ProjectRoot}\GenshinGachaAnalyzer.WPF\Resources\App.ico
UninstallDisplayIcon={app}\{#AppExe}
VersionInfoVersion={#AppVersion}
VersionInfoCompany=木偶
VersionInfoDescription={#AppName} 安装程序
LicenseFile={#ProjectRoot}\LICENSE
Compression=lzma2
SolidCompression=yes
WizardStyle=modern dynamic
DisableWelcomePage=no
CloseApplications=yes
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "chinesesimplified"; MessagesFile: "ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "快捷方式："

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,*.db,*.db-wal,*.db-shm,*.log,*.partial,unins*.exe,unins*.dat,unins*.msg"

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"
Name: "{group}\卸载 {#AppName}"; Filename: "{uninstallexe}"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "启动 {#AppName}"; Flags: nowait postinstall skipifsilent

[Messages]
WelcomeLabel2=此向导将在您的电脑上安装 [name/ver]。%n%n安装包已包含运行环境，无需另装 .NET。%n%n升级和卸载会保留您的祈愿档案与设置。
