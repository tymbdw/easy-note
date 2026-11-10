; 桌面记事 Desktop Note — per-user installer (no admin prompt).
; Build: "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" installer\DesktopNote.iss
; Output: build\dist\DesktopNote-Setup-<version>.exe

#define AppName "易便签"
#define AppNameEn "Easy Note"
#define AppVersion "1.0.0"
#define AppExe "DesktopNote.exe"
#define AppPublisher "tymbdw"

[Setup]
AppId={{7C4A9E31-2B6D-4F58-9A21-5D8E3C7B1A46}
AppName={#AppName} {#AppNameEn}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={code:GetDefaultDir}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; 让用户自己选安装位置
DisableDirPage=no
UsePreviousAppDir=no
AllowUNCPath=no
AllowRootDirectory=no
DisableWelcomePage=no
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\build\dist
OutputBaseFilename=EasyNote-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName={#AppName} {#AppNameEn}
UninstallDisplayIcon={app}\{#AppExe}
SetupIconFile=..\assets\desktopnote.ico
CloseApplications=yes
AllowNoIcons=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加选项:"; Flags: checkedonce
Name: "startup"; Description: "开机自动启动"; GroupDescription: "附加选项:"; Flags: unchecked

[Files]
Source: "..\build\publish\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\卸载 {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; \
    ValueName: "DesktopNote"; ValueData: """{app}\{#AppExe}"""; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#AppExe}"; Description: "立即运行 {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Messages]
english.WelcomeLabel2=This will install [name/ver] on your computer.%n%n便签内容保存在 %APPDATA%\DesktopNote，卸载不会被删除。
english.FinishedLabel=安装完成。易便签默认钉在桌面右上角，开机自启可在托盘菜单里开关。

[Code]
{ 有 D: 盘就装到 D:\桌面记事，否则装到当前用户目录，保证换台机器也能装 }
function GetDefaultDir(Param: string): string;
begin
  if DirExists('D:') then
    Result := 'D:\易便签'
  else
    Result := ExpandConstant('{localappdata}\Programs\易便签');
end;
