param([string]$OutFile)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
Invoke-WebRequest -Uri 'https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/10.0.14/windowsdesktop-runtime-10.0.14-win-x64.exe' -OutFile $OutFile -UseBasicParsing
if ((Get-Item -LiteralPath $OutFile).Length -lt 1000000) { exit 2 }
exit 0
