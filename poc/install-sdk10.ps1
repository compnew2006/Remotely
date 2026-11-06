$ErrorActionPreference = 'Stop'
$src = 'https://dot.net/v1/dotnet-install.ps1'
$dst = Join-Path $env:TEMP 'dotnet-install.ps1'
Invoke-WebRequest -UseBasicParsing -Uri $src -OutFile $dst
& $dst -Channel 10.0 -InstallDir 'C:\Program Files\dotnet'
& 'C:\Program Files\dotnet\dotnet.exe' --list-sdks
