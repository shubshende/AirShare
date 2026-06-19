#define MyAppName "AirReceiver"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "AirReceiver"
#define MyAppExeName "AirReceiver.exe"
#define SourceDir "..\publish\AirReceiver-win-x64"

[Setup]
AppId={{0B9D4C23-5F6E-49B3-9B68-78A9765A59B1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=AirReceiver-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{cmd}"; Parameters: "/C netsh advfirewall firewall add rule name=""AirReceiver AirPlay TCP 5000"" dir=in action=allow protocol=TCP localport=5000"; Flags: runhidden
Filename: "{cmd}"; Parameters: "/C netsh advfirewall firewall add rule name=""AirReceiver AirPlay TCP 7000"" dir=in action=allow protocol=TCP localport=7000"; Flags: runhidden
Filename: "{cmd}"; Parameters: "/C netsh advfirewall firewall add rule name=""AirReceiver mDNS UDP 5353"" dir=in action=allow protocol=UDP localport=5353"; Flags: runhidden
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{cmd}"; Parameters: "/C netsh advfirewall firewall delete rule name=""AirReceiver AirPlay TCP 5000"""; Flags: runhidden
Filename: "{cmd}"; Parameters: "/C netsh advfirewall firewall delete rule name=""AirReceiver AirPlay TCP 7000"""; Flags: runhidden
Filename: "{cmd}"; Parameters: "/C netsh advfirewall firewall delete rule name=""AirReceiver mDNS UDP 5353"""; Flags: runhidden
