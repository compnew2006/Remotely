$scriptPath = Join-Path $PSScriptRoot 'Get-PSCommands.ps1'
if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
    throw "PowerShell command export script not found at '$scriptPath'."
}

$argumentList = "-File `"$scriptPath`""
Start-Process -FilePath 'powershell.exe' -ArgumentList $argumentList

$pwsh = Get-Command 'pwsh.exe' -ErrorAction SilentlyContinue
if ($pwsh) {
    Start-Process -FilePath $pwsh.Source -ArgumentList $argumentList
}
