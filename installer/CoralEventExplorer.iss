; Inno Setup script for Coral Event Explorer
; Build the Release configuration first, then compile this script with ISCC.exe.

#define MyAppName "Coral Event Explorer"
; Default version; the github-release workflow overrides it with ISCC /DMyAppVersion=x.y.z
#ifndef MyAppVersion
  #define MyAppVersion "1.1.4"
#endif
#define MyAppPublisher "Previse Systems"
#define MyAppURL "https://github.com/stuartb2/CoralEventExplorer"
#define MyAppExeName "CoralEventExplorer.exe"
#define ReleaseDir "..\src\ServiceBusExplorer\bin\Release"

[Setup]
AppId={{7C129CCE-B441-44A4-9D68-B113A6FA4060}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
; Detect a running instance (the app holds a mutex of this name) and close it
; before installing, so an in-place upgrade reliably replaces the executable.
AppMutex=CoralEventExplorerMutex
CloseApplications=yes
RestartApplications=no
; Per-user install: no admin rights needed and the application can update its
; own configuration file next to the executable.
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\Coral Event Explorer
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE.txt
OutputDir=Output
OutputBaseFilename=CoralEventExplorerSetup-{#MyAppVersion}
SetupIconFile=..\src\ServiceBusExplorer\NewServiceBusExplorerLogo.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; Flags: unchecked

[Files]
Source: "{#ReleaseDir}\*"; DestDir: "{app}"; Excludes: "*.pdb,*.xml"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
