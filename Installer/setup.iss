#define MyAppVersion "1.0.0"

[Setup]
AppId={{B7E2A5D1-8F4C-4D3A-9E6B-1C5F8A2D7E9B}
AppName=Shadow Explorer
AppVersion={#MyAppVersion}
AppPublisher=Shadow Explorer
AppPublisherURL=https://github.com/ianwitherow/ShadowExplorer
DefaultDirName={autopf}\ShadowExplorer
DefaultGroupName=Shadow Explorer
OutputDir=..\dist
OutputBaseFilename=ShadowExplorer-Setup-{#MyAppVersion}
SetupIconFile=..\ShadowExplorer\shadow.ico
UninstallDisplayIcon={app}\ShadowExplorer.exe
Compression=lzma2/ultra64
SolidCompression=yes
PrivilegesRequired=admin
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
; Allow upgrading over existing install without asking to uninstall first
UsePreviousAppDir=yes
CloseApplications=yes
RestartApplications=no

[Files]
Source: "..\ShadowExplorer\bin\Release\net9.0-windows\win-x64\publish\ShadowExplorer.exe"; DestDir: "{app}"; Flags: ignoreversion

[Registry]
; File context menu
Root: HKCR; Subkey: "*\shell\ViewShadowCopies"; ValueType: string; ValueName: ""; ValueData: "View Shadow Copies"; Flags: uninsdeletekey
Root: HKCR; Subkey: "*\shell\ViewShadowCopies"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\ShadowExplorer.exe"
Root: HKCR; Subkey: "*\shell\ViewShadowCopies\command"; ValueType: string; ValueName: ""; ValueData: """{app}\ShadowExplorer.exe"" ""%1"""

; Folder context menu
Root: HKCR; Subkey: "Directory\shell\ViewShadowCopies"; ValueType: string; ValueName: ""; ValueData: "View Shadow Copies"; Flags: uninsdeletekey
Root: HKCR; Subkey: "Directory\shell\ViewShadowCopies"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\ShadowExplorer.exe"
Root: HKCR; Subkey: "Directory\shell\ViewShadowCopies\command"; ValueType: string; ValueName: ""; ValueData: """{app}\ShadowExplorer.exe"" ""%1"""

; Folder background context menu
Root: HKCR; Subkey: "Directory\Background\shell\ViewShadowCopies"; ValueType: string; ValueName: ""; ValueData: "View Shadow Copies"; Flags: uninsdeletekey
Root: HKCR; Subkey: "Directory\Background\shell\ViewShadowCopies"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\ShadowExplorer.exe"
Root: HKCR; Subkey: "Directory\Background\shell\ViewShadowCopies\command"; ValueType: string; ValueName: ""; ValueData: """{app}\ShadowExplorer.exe"" ""%V"""
