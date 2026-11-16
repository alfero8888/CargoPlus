param(
    [Parameter(Mandatory = $true)][string]$AppDir,
    [switch]$Desktop,
    [switch]$Autostart,
    [switch]$Remove
)

$ErrorActionPreference = 'Stop'
$exe = Join-Path $AppDir 'CargoPlus.exe'
$ico = Join-Path $AppDir 'logo.ico'

function Remove-Link([string]$Folder) {
    if ([string]::IsNullOrWhiteSpace($Folder)) { return }
    $path = Join-Path $Folder 'Cargo Plus.lnk'
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
}

if ($Remove) {
    Remove-Link ([Environment]::GetFolderPath('Desktop'))
    Remove-Link ([Environment]::GetFolderPath('Programs'))
    Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'CargoPlus' -ErrorAction SilentlyContinue
    exit 0
}

if (-not (Test-Path -LiteralPath $exe)) { exit 2 }
if (-not (Test-Path -LiteralPath $ico)) { exit 3 }

$shell = New-Object -ComObject WScript.Shell

function Set-Link([string]$Folder) {
    if ([string]::IsNullOrWhiteSpace($Folder)) { return }
    New-Item -ItemType Directory -Force -Path $Folder | Out-Null
    $link = $shell.CreateShortcut((Join-Path $Folder 'Cargo Plus.lnk'))
    $link.TargetPath = $exe
    $link.WorkingDirectory = $AppDir
    $link.WindowStyle = 1
    $link.Description = 'Cargo Plus'
    $link.IconLocation = "$ico,0"
    $link.Save()
}

if ($Desktop) {
    Set-Link ([Environment]::GetFolderPath('Desktop'))
}

Set-Link ([Environment]::GetFolderPath('Programs'))

if ($Autostart) {
    New-Item -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Force | Out-Null
    Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'CargoPlus' -Value ('"{0}"' -f $exe)
}

exit 0
