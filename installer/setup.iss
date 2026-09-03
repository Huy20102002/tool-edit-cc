; =====================================================================
; Inno Setup Script for Auto Video Editor (TikTok & Reels OneShot)
; =====================================================================

#define MyAppName "Auto Video Editor"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Auto Video Editor"
#define MyAppExeName "AutoVideoEditor.App.exe"
#define MySourceDir "..\bin\publish\AutoVideoEditor"

[Setup]
AppId={{5A4F79A1-21B0-4C98-9C45-8B92F2698D01}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=..\bin\dist
OutputBaseFilename=AutoVideoEditor_Setup_v{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequiredOverridesAllowed=dialog
DisableProgramGroupPage=no
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
english.CreateDesktopIcon=Tạo biểu tượng ngoài màn hình Desktop (Create Desktop Shortcut)
english.LaunchProgram=Khởi chạy Auto Video Editor ngay bây giờ (Launch Auto Video Editor)

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#MySourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram}"; Flags: nowait postinstall skipifsilent
