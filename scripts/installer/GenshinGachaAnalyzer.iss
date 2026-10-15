; AppVersion 仅由 Directory.Build.props 注入。固定 AppId 使后续版本升级同一安装。
#ifndef AppVersion
  #error 请使用 scripts/Build-Installer.ps1 构建。
#endif
#define AppName "木偶祈愿分析"
#define AppExe "GenshinGachaAnalyzer.exe"
#ifndef InstallerAppId
  #define InstallerAppId "GenshinGachaAnalyzer"
  #define CleanupLegacyShortcuts
#endif

[Setup]
AppId={#InstallerAppId}
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
WizardImageFile=Assets\WizardPortrait.bmp
WizardImageStretch=yes
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

#ifdef CleanupLegacyShortcuts
[InstallDelete]
Type: files; Name: "{group}\木偶原神抽卡分析工具.lnk"
Type: files; Name: "{group}\卸载 木偶原神抽卡分析工具.lnk"
Type: files; Name: "{userdesktop}\木偶原神抽卡分析工具.lnk"
#endif

[Run]
Filename: "{app}\{#AppExe}"; Description: "启动 {#AppName}"; Flags: nowait postinstall skipifsilent

[Messages]
WelcomeLabel2=此向导将在您的电脑上安装 [name/ver]。%n%n安装包已包含运行环境，无需另装 .NET。%n%n升级和卸载会保留您的祈愿档案与设置。

[Code]
procedure NormalizeEntryIntegrity(EntryPath: String);
var
  ResultCode: Integer;
begin
  if not Exec(ExpandConstant('{sys}\icacls.exe'), '"' + EntryPath + '" /setintegritylevel Medium',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    RaiseException('无法设置程序的普通用户运行标记，请选择当前用户可写的安装目录。');
  if ResultCode <> 0 then
    RaiseException('设置程序运行标记失败（' + IntToStr(ResultCode) + '），请选择当前用户可写的安装目录。');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    { 自定义目录可能继承 Low 标签。只修正安装器维护的入口，保持 asInvoker。 }
    NormalizeEntryIntegrity(ExpandConstant('{app}\{#AppExe}'));
    NormalizeEntryIntegrity(ExpandConstant('{uninstallexe}'));
    Log('应用与卸载入口已设置 Medium 完整性；不请求管理员权限。');
  end;
end;
