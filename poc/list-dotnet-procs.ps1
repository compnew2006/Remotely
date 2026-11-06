$ErrorActionPreference = 'Continue'
Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" |
    Where-Object { $_.ExecutablePath -like 'C:\Program Files\dotnet*' } |
    ForEach-Object { '{0} :: {1}' -f $_.ProcessId, $_.CommandLine }
