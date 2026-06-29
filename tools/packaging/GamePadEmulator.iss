; ============================================================================
;  GamePad Emulator — Inno Setup 安装包定义
;  由 build-installer.ps1 调用，可通过 /DVersion /DAppExeName 覆盖默认值。
;  产物：GamePadEmulator-<Version>-x64-Setup.exe（自包含 .NET 10 运行时，x64）。
; ============================================================================

#ifndef Version
  #define Version "1.0.0"
#endif

#ifndef AppExeName
  #define AppExeName "GamePadEmulator.exe"
#endif

#define AppName      "GamePad Emulator"
#define AppPublisher "ZCode"
#define AppURL       "https://github.com/ViGEm/ViGEmBus/releases/latest"
#define AppExeBase   StringChange(AppExeName, ".exe", "")

; 自包含运行时目录由脚本预先 publish 到 ./publish（相对本 .iss 文件）。
#define PublishDir   "publish"

[Setup]
; 固定 AppId（写死 GUID），保证后续升级时被识别为同一程序而非新装。
AppId={{8B1C2D3E-4F5A-6B7C-8D9E-0A1B2C3D4E5F}
AppName={#AppName}
AppVersion={#Version}
AppVerName={#AppName} {#Version}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}

; 默认安装到 Program Files\GamePad Emulator；开始菜单组同名。
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}

; 仅 64 位：禁止在 32 位系统上安装，并在 64 位系统上以 64 位模式安装。
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

; 自包含运行时文件多，用最高压缩 + solid 压缩把 ~75MB 压到 ~30-35MB。
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

; 卸载时显示在控制面板的图标指向主程序（若有自定义 app.ico 也会覆盖快捷方式图标）。
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}

; 产物文件名带版本号，输出到本 .iss 同级的 output\。
OutputDir=output
OutputBaseFilename=GamePadEmulator-{#Version}-x64-Setup

; 可选：若存在 assets\app.ico，则用作安装器与快捷方式图标；否则 Inno 用默认图标。
#ifndef SetupIconFile
  #if FileExists(AddBackslash(SourcePath) + "assets\app.ico")
    #define SetupIconFile "assets\app.ico"
  #endif
#endif
#ifdef SetupIconFile
  SetupIconFile={#SetupIconFile}
#endif

; 不要求管理员（与应用 app.manifest 的 asInvoker 保持一致）。
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
; 仅使用 Inno Setup 内置随附的英文语言，避免依赖需单独下载的中文语言包。
; 安装器界面为英文；应用程序本身的 UI 不受影响（仍为中文）。
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; 递归打包自包含发布目录的全部文件。
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\卸载 {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
; 安装结束时可选启动主程序。
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[Code]
// 安装结束后检测 ViGEmBus 驱动是否已安装（系统级内核驱动，应用运行前提）。
// 未安装则非阻塞提示用户去下载安装并重启，与应用 README 的运行前提保持一致。
function ViGEmBusInstalled: Boolean;
begin
  // HKLM64: 检查驱动服务注册键（64 位系统视图）。
  Result := RegKeyExists(HKLM64, 'SYSTEM\CurrentControlSet\Services\ViGEmBus');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    if not ViGEmBusInstalled then
    begin
      MsgBox(
        '尚未检测到 ViGEmBus 驱动。' #13#10 #13#10 +
        '该驱动是创建虚拟手柄的内核驱动，是本程序正常工作的前提（与 DS4Windows 等同类软件共用）。' #13#10 #13#10 +
        '请前往下载并安装后重启电脑：' #13#10 +
        'https://github.com/ViGEm/ViGEmBus/releases/latest' #13#10 #13#10 +
        '驱动安装前，本程序的界面可正常预览，但无法向游戏注入输入。',
        mbInformation, MB_OK);
    end;
  end;
end;
