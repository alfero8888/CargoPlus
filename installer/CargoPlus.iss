#ifndef SourceExe
  #define SourceExe "CargoPlus.exe"
#endif

[Setup]
AppId={{A7C3E1B4-9F2D-4C6A-8B15-6E4D2F0A91C3}
AppName=Cargo Plus
AppVersion=1.0.0
AppVerName=Cargo Plus 1.0.0
AppPublisher=Baraka Logistic Cargo
DefaultDirName={localappdata}\Programs\CargoPlus
DefaultGroupName=Cargo Plus
DisableProgramGroupPage=yes
OutputBaseFilename=CargoPlus-Setup
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\CargoPlus.exe
VersionInfoVersion=1.0.0.0
VersionInfoCompany=Baraka Logistic Cargo
VersionInfoProductName=Cargo Plus
VersionInfoDescription=Установщик Cargo Plus
CloseApplications=yes
RestartApplications=no
WizardStyle=modern
SetupLogging=no

[Languages]
Name: "ru"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; DestName: "CargoPlus.exe"; Flags: ignoreversion

[Icons]
Name: "{group}\Cargo Plus"; Filename: "{app}\CargoPlus.exe"
Name: "{autodesktop}\Cargo Plus"; Filename: "{app}\CargoPlus.exe"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "CargoPlus"; ValueData: """{app}\CargoPlus.exe"""; Flags: uninsdeletevalue

[Run]
Filename: "{app}\CargoPlus.exe"; Description: "Запустить Cargo Plus"; Flags: nowait postinstall skipifsilent
