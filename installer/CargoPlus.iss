#ifndef SourceExe
  #define SourceExe "CargoPlus.exe"
#endif
#define MyVersion "1.0.41"

[Setup]
AppId={{A7C3E1B4-9F2D-4C6A-8B15-6E4D2F0A91C3}
AppName=Cargo Plus
AppVersion={#MyVersion}
AppVerName=Cargo Plus {#MyVersion}
AppPublisher=Baraka Logistic Cargo
DefaultDirName={autopf}\Cargo Plus
DisableDirPage=yes
UsePreviousAppDir=no
DefaultGroupName=Cargo Plus
DisableProgramGroupPage=yes
OutputBaseFilename=CargoPlus-Setup
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\brand\logo.ico
UninstallDisplayIcon={app}\logo.ico
VersionInfoVersion=1.0.41.0
VersionInfoCompany=Baraka Logistic Cargo
VersionInfoProductName=Cargo Plus
VersionInfoDescription=Cargo Plus setup
CloseApplications=yes
RestartApplications=no
WizardStyle=modern
SetupLogging=no
ShowLanguageDialog=yes
LanguageDetectionMethod=none

[Languages]
Name: "tr"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "ru"; MessagesFile: "compiler:Languages\Russian.isl"

[CustomMessages]
tr.LaunchApp=Cargo Plus'ı başlat
ru.LaunchApp=Запустить Cargo Plus
tr.AutoStart=Bilgisayar açılınca Cargo Plus başlasın
ru.AutoStart=Запускать Cargo Plus при включении компьютера
tr.Need64=Cargo Plus yalnızca 64 bit Windows'a kurulur.
ru.Need64=Cargo Plus ставится только на 64-битную Windows.
tr.OsTooOld=Bu Windows çok eski. Cargo Plus için Windows 10 (1607) veya Windows 11 gerekir. .NET Framework 3 gerekmez.
ru.OsTooOld=Эта Windows слишком старая. Для Cargo Plus нужна Windows 10 (1607) или Windows 11. .NET Framework 3 не нужен.
tr.NeedRuntime=Bu bilgisayarda .NET 10 masaüstü ortamı yok. Cargo Plus onu ister. İndirip kurulsun mu? İndirme bitince kurulum yeniden kontrol eder.
ru.NeedRuntime=На этом компьютере нет среды .NET 10 для программ Windows. Cargo Plus её требует. Скачать и установить? После загрузки установка проверит ещё раз.
tr.DownloadingRuntime=.NET 10 indiriliyor...
ru.DownloadingRuntime=Скачиваю .NET 10...
tr.InstallingRuntime=.NET 10 kuruluyor...
ru.InstallingRuntime=Устанавливаю .NET 10...
tr.RuntimeBundled=Ayrı .NET 10 kurulamadı. Cargo Plus kendi içinde .NET 10 taşıyor, kurulum devam ediyor.
ru.RuntimeBundled=Отдельную среду .NET 10 поставить не удалось. Внутри Cargo Plus она уже есть, установка продолжается.
tr.RuntimeDeclined=Ayrı .NET 10 kurulmayacak. Cargo Plus kendi içinde .NET 10 taşıyor, kurulum devam ediyor.
ru.RuntimeDeclined=Отдельную среду .NET 10 ставить не будем. Внутри Cargo Plus она уже есть, установка продолжается.
tr.CheckingUpdate=Sunucudaki son sürüm kontrol ediliyor...
ru.CheckingUpdate=Проверяю последнюю версию на сервере...
tr.FinishedOk=Cargo Plus kuruldu. Sunucuda daha yeni sürüm yok.
ru.FinishedOk=Cargo Plus установлена. На сервере нет версии новее.
tr.FinishedUpdated=Cargo Plus kuruldu ve sunucudaki son sürüme güncellendi.
ru.FinishedUpdated=Cargo Plus установлена и обновлена до последней версии с сервера.
tr.FinishedNoCheck=Cargo Plus kuruldu. Sunucuya bakılamadı, bu sürüm kaldı.
ru.FinishedNoCheck=Cargo Plus установлена. Сервер не ответил, осталась эта версия.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce
Name: "autostart"; Description: "{cm:AutoStart}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; DestName: "CargoPlus.exe"; Flags: ignoreversion
Source: "..\brand\logo.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\brand\logo.png"; DestDir: "{app}"; Flags: ignoreversion
Source: "download-runtime.ps1"; Flags: dontcopy
Source: "github-update.ps1"; Flags: dontcopy
Source: "install-user.ps1"; Flags: dontcopy

[Icons]
Name: "{group}\Cargo Plus"; Filename: "{app}\CargoPlus.exe"; IconFilename: "{app}\logo.ico";
Name: "{autodesktop}\Cargo Plus"; Filename: "{app}\CargoPlus.exe"; IconFilename: "{app}\logo.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\CargoPlus.exe"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent

[Code]
function WindowsOk: Boolean;
var
  Version: TWindowsVersion;
begin
  GetWindowsVersionEx(Version);
  Result := (Version.Major > 10) or ((Version.Major = 10) and (Version.Build >= 14393));
end;

function FolderHasDotNet10(const Path: String): Boolean;
var
  Found: TFindRec;
begin
  Result := False;
  if not DirExists(Path) then
    Exit;
  if FindFirst(Path + '\10.*', Found) then
  begin
    Result := True;
    FindClose(Found);
  end;
end;

function HasDotNet10: Boolean;
var
  Names: TArrayOfString;
  I: Integer;
begin
  Result := FolderHasDotNet10(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App'))
    or FolderHasDotNet10(ExpandConstant('{commonpf}\dotnet\shared\Microsoft.WindowsDesktop.App'))
    or FolderHasDotNet10(ExpandConstant('{commonpf32}\dotnet\shared\Microsoft.WindowsDesktop.App'))
    or FolderHasDotNet10(ExpandConstant('{localappdata}\Microsoft\dotnet\shared\Microsoft.WindowsDesktop.App'));
  if Result then
    Exit;
  if not RegGetValueNames(HKLM64, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App', Names) then
    Exit;
  for I := 0 to GetArrayLength(Names) - 1 do
    if (Length(Names[I]) >= 3) and (Copy(Names[I], 1, 3) = '10.') then
    begin
      Result := True;
      Exit;
    end;
end;

function RunPowerShell(const Script, Args: String; var Code: Integer): Boolean;
begin
  Result := Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoProfile -ExecutionPolicy Bypass -File "' + Script + '" ' + Args,
    '', SW_HIDE, ewWaitUntilTerminated, Code);
end;

function EnsureRuntime: String;
var
  Script, SetupFile: String;
  Code: Integer;
begin
  Result := '';
  if HasDotNet10 then
    Exit;
  if MsgBox(ExpandConstant('{cm:NeedRuntime}'), mbConfirmation, MB_YESNO) <> IDYES then
  begin
    MsgBox(ExpandConstant('{cm:RuntimeDeclined}'), mbInformation, MB_OK);
    Exit;
  end;

  WizardForm.StatusLabel.Caption := ExpandConstant('{cm:DownloadingRuntime}');
  WizardForm.Update;
  ExtractTemporaryFile('download-runtime.ps1');
  Script := ExpandConstant('{tmp}\download-runtime.ps1');
  SetupFile := ExpandConstant('{tmp}\dotnet10-desktop.exe');
  if (not RunPowerShell(Script, '-OutFile "' + SetupFile + '"', Code)) or (Code <> 0) then
  begin
    MsgBox(ExpandConstant('{cm:RuntimeBundled}'), mbInformation, MB_OK);
    Exit;
  end;

  WizardForm.StatusLabel.Caption := ExpandConstant('{cm:InstallingRuntime}');
  WizardForm.Update;
  ShellExec('runas', SetupFile, '/install /quiet /norestart', ExpandConstant('{tmp}'), SW_SHOW, ewWaitUntilTerminated, Code);
  Sleep(1500);
  if not HasDotNet10 then
    MsgBox(ExpandConstant('{cm:RuntimeBundled}'), mbInformation, MB_OK);
end;

function InitializeSetup: Boolean;
begin
  Result := True;
  if not IsWin64 then
  begin
    MsgBox(ExpandConstant('{cm:Need64}'), mbError, MB_OK);
    Result := False;
    Exit;
  end;
  if not WindowsOk then
  begin
    MsgBox(ExpandConstant('{cm:OsTooOld}'), mbError, MB_OK);
    Result := False;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  NeedsRestart := False;
  Result := EnsureRuntime;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Script, Installed: String;
  Code: Integer;
begin
  if CurStep <> ssPostInstall then
    Exit;
  ExtractTemporaryFile('install-user.ps1');
  Script := ExpandConstant('{tmp}\install-user.ps1');
  Installed := '-AppDir "' + ExpandConstant('{app}') + '"';
  if WizardIsTaskSelected('desktopicon') then
    Installed := Installed + ' -Desktop';
  if WizardIsTaskSelected('autostart') then
    Installed := Installed + ' -Autostart';
  ExecAsOriginalUser(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoProfile -ExecutionPolicy Bypass -File "' + Script + '" ' + Installed,
    '', SW_HIDE, ewWaitUntilTerminated, Code);
  WizardForm.StatusLabel.Caption := ExpandConstant('{cm:CheckingUpdate}');
  WizardForm.Update;
  ExtractTemporaryFile('github-update.ps1');
  Script := ExpandConstant('{tmp}\github-update.ps1');
  Installed := ExpandConstant('{app}\CargoPlus.exe');
  if (not RunPowerShell(Script, '-Installed "' + Installed + '" -Current "{#MyVersion}"', Code)) or (Code = 13) or (Code = 11) or (Code = 12) then
    WizardForm.FinishedLabel.Caption := ExpandConstant('{cm:FinishedNoCheck}')
  else if Code = 0 then
    WizardForm.FinishedLabel.Caption := ExpandConstant('{cm:FinishedUpdated}')
  else
    WizardForm.FinishedLabel.Caption := ExpandConstant('{cm:FinishedOk}');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Script: String;
  Code: Integer;
begin
  if CurUninstallStep <> usUninstall then
    Exit;
  ExtractTemporaryFile('install-user.ps1');
  Script := ExpandConstant('{tmp}\install-user.ps1');
  ExecAsOriginalUser(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoProfile -ExecutionPolicy Bypass -File "' + Script + '" -AppDir "' + ExpandConstant('{app}') + '" -Remove',
    '', SW_HIDE, ewWaitUntilTerminated, Code);
end;
