; Inno Setup script for Coral Event Explorer
; Build the Release configuration first, then compile this script with ISCC.exe.

#define MyAppName "Coral Event Explorer"
#define MyAppVersion "1.0.0"
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
