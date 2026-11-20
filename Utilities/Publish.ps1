<#
.SYNOPSIS
   Publishes the Remotely clients.
.DESCRIPTION
   Publishes the Remotely clients.
   To deploy the server, supply the following arguments: -rid win-x64 -outdir path\to\dir -hostname https://mysite.mydomain.com
.COPYRIGHT
   Copyright 2023 Immense Networks.  All rights reserved.
.EXAMPLE
   Run it from the Utilities folder (located in the solution directory).
   Or run "powershell -f Publish.ps1 -rid win-x64 -outdir path\to\dir -hostname https://mysite.mydomain.com
#>

param (
    [string]$OutDir = "",
    # RIDs are described here: https://docs.microsoft.com/en-us/dotnet/core/rid-catalog
    [string]$RID = "",
    [string]$CertificatePath = "",
    [string]$CertificatePassword = "",
    [string]$CurrentVersion = ""
)



$ErrorActionPreference = "Stop"
$Root = (Get-Item -Path $PSScriptRoot).Parent.FullName
$SignAssemblies = $false

if (!$CurrentVersion) {
    Push-Location -Path $Root

    $VersionString = git show -s --format=%ci
    $VersionDate = [DateTimeOffset]::Parse($VersionString)

    $CurrentVersion = $VersionDate.ToString("yyyy.MM.dd.HHmm")

    Pop-Location
}

if ($CertificatePath.Length -gt 0 -and 
    (Test-Path -Path $CertificatePath) -eq $true -and 
    $CertificatePassword.Length -gt 0) {
    $SignAssemblies = $true
}



Set-Location -Path $Root

#region Functions

function Replace-LineInFile($FilePath, $MatchPattern, $ReplaceLineWith, $MaxCount = -1) {
    [string[]]$Content = Get-Content -Path $FilePath
    $Count = 0
    for ($i = 0; $i -lt $Content.Length; $i++) {
        if ($Content[$i] -ne $null -and $Content[$i].Contains($MatchPattern)) {
            $Content[$i] = $ReplaceLineWith
            $Count++
        }
        if ($MaxCount -gt 0 -and $Count -ge $MaxCount) {
            break
        }
    }
    ($Content | Out-String).Trim() | Out-File -FilePath $FilePath -Force -Encoding utf8
}

function Wait-ForExists($FilePath) {
    while ((Test-Path -Path $FilePath) -eq $false) {
        Write-Host "Waiting for file: $FilePath"
        Start-Sleep -Seconds 3
    }
}

# Writes the version of a published desktop client next to it.  The server
# puts the version in the signed manifest, so the agents can tell whether
# the client they have is the one the server publishes.  The file is written
# without a byte order mark, because the server reads the version as it is.
function Write-DesktopVersion($FilePath) {
    if ((Test-Path -Path $FilePath) -eq $false) {
        Write-Host "The desktop client was not published: $FilePath" -ForegroundColor DarkYellow
        return
    }

    [System.IO.File]::WriteAllText("$FilePath.version", $CurrentVersion)
}

# Builds the package of an agent.  The desktop client is left out of it, even
# when a previous publish left one behind, because the agents download it
# from the server when a session needs it.
function New-AgentPackage($PublishDir, $PackageName) {
    $Items = @(Get-ChildItem -Path $PublishDir | Where-Object {
        $_.Name -ne "Desktop" -and $_.Name -ne $PackageName
    })

    $ZipPath = Join-Path $PublishDir $PackageName

    Compress-Archive -Path $Items.FullName -DestinationPath $ZipPath -Force
    Wait-ForExists -FilePath $ZipPath
    Move-Item -Path $ZipPath -Destination "$Root\Server\wwwroot\Content\$PackageName" -Force
}
# The machine-wide dotnet can be older than the SDK that global.json
# requires, and "dotnet publish" then fails with NETSDK1045.  The SDK that
# matches global.json is looked up first, and the user-profile install is
# preferred over the machine-wide one.
function Get-InstalledSdks($DotNetPath) {
    $sdks = @()
    $lines = & $DotNetPath --list-sdks 2>$null
    foreach ($line in $lines) {
        if ($line -match '^\s*(\d+\.\d+\.\d+)\s*\[') {
            $sdks += $Matches[1]
        }
    }
    return $sdks
}

function Resolve-DotNet {
    $required = $null
    $globalJson = Join-Path $Root "global.json"
    if (Test-Path $globalJson) {
        try {
            $required = (Get-Content $globalJson -Raw | ConvertFrom-Json).sdk.version
        }
        catch {
            $required = $null
        }
    }

    $candidates = @()
    $profileDotNet = Join-Path $env:USERPROFILE ".dotnet\dotnet.exe"
    if (Test-Path $profileDotNet) {
        $candidates += $profileDotNet
    }
    $onPath = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($onPath -and $onPath.Source) {
        $candidates += $onPath.Source
    }
    $candidates = @($candidates | Select-Object -Unique)

    if ($candidates.Count -eq 0) {
        throw "No dotnet executable was found.  Install the SDK that global.json requires."
    }

    if ($required) {
        # An exact match first.
        foreach ($candidate in $candidates) {
            if ((Get-InstalledSdks $candidate) -contains $required) {
                return $candidate
            }
        }

        # global.json rolls forward to a later feature band of the same
        # major.minor, so any newer patch of the same band is accepted too.
        $parts = $required.Split('.')
        foreach ($candidate in $candidates) {
            $match = Get-InstalledSdks $candidate | Where-Object {
                $p = $_.Split('.')
                $p[0] -eq $parts[0] -and $p[1] -eq $parts[1] -and [version]$_ -ge [version]$required
            } | Select-Object -First 1
            if ($match) {
                Write-Host "Using $candidate with SDK $match (global.json asks for $required)."
                return $candidate
            }
        }

        throw "No installed SDK satisfies global.json ($required).  Installed: " + ((Get-InstalledSdks $candidates[0]) -join ', ')
    }

    return $candidates[0]
}
#endregion

$DotNet = Resolve-DotNet
Write-Host "Publishing with $DotNet ($(& $DotNet --version))."

# Publish uses dotnet publish; MSBuild is not required.

# Clear publish folders.
if ((Test-Path -Path "$Root\Agent\bin\publish\win-x64") -eq $true) {
    Get-ChildItem -Path "$Root\Agent\bin\publish\win-x64" | Remove-Item -Force -Recurse
}
if ((Test-Path -Path  "$Root\Agent\bin\publish\win-x86" ) -eq $true) {
    Get-ChildItem -Path  "$Root\Agent\bin\publish\win-x86" | Remove-Item -Force -Recurse
}
if ((Test-Path -Path "$Root\Agent\bin\publish\linux-x64") -eq $true) {
    Get-ChildItem -Path "$Root\Agent\bin\publish\linux-x64" | Remove-Item -Force -Recurse
}


# Publish agents.
& $DotNet publish /p:Version=$CurrentVersion /p:FileVersion=$CurrentVersion --runtime win-x64 --self-contained --configuration Release --output "$Root\Agent\bin\publish\win-x64" "$Root\Agent"
& $DotNet publish /p:Version=$CurrentVersion /p:FileVersion=$CurrentVersion --runtime linux-x64 --self-contained --configuration Release --output "$Root\Agent\bin\publish\linux-x64" "$Root\Agent"
& $DotNet publish /p:Version=$CurrentVersion /p:FileVersion=$CurrentVersion --runtime win-x86 --self-contained --configuration Release --output "$Root\Agent\bin\publish\win-x86" "$Root\Agent"
& $DotNet publish /p:Version=$CurrentVersion /p:FileVersion=$CurrentVersion --runtime osx-x64 --self-contained --configuration Release --output "$Root\Agent\bin\publish\osx-x64" "$Root\Agent"
& $DotNet publish /p:Version=$CurrentVersion /p:FileVersion=$CurrentVersion --runtime osx-arm64 --self-contained --configuration Release --output "$Root\Agent\bin\publish\osx-arm64" "$Root\Agent"




# The desktop client is not part of the agent package anymore.  The agents
# download it from the server when a session needs it, which keeps the agent
# package small.  The server signs the published desktop client, so an agent
# only accepts a download that matches the signed manifest.

# Publish Linux GUI App
& $DotNet publish /p:Version=$CurrentVersion /p:FileVersion=$CurrentVersion -p:PublishProfile=desktop-linux-x64 --configuration Release "$Root\Desktop.Linux\"




# Publish Windows GUI App (64-bit)
& $DotNet publish /p:Version=$CurrentVersion /p:FileVersion=$CurrentVersion -p:PublishProfile=desktop-win-x64 -r win-x64 --configuration Release "$Root\Desktop.Win"

if ($SignAssemblies) {
    &"$Root\Utilities\signtool.exe" sign /fd SHA256 /f "$CertificatePath" /p $CertificatePassword /t http://timestamp.digicert.com "$Root\Server\wwwroot\Content\Win-x64\Remotely_Desktop.exe"
}


# Publish Windows GUI App (32-bit)
& $DotNet publish /p:Version=$CurrentVersion /p:FileVersion=$CurrentVersion -p:PublishProfile=desktop-win-x86 -r win-x86 --configuration Release "$Root\Desktop.Win"

if ($SignAssemblies) {
    &"$Root\Utilities\signtool.exe" sign /fd SHA256 /f "$CertificatePath" /p $CertificatePassword /t http://timestamp.digicert.com "$Root\Server\wwwroot\Content\Win-x86\Remotely_Desktop.exe"
}

# The version of the desktop client is written next to it, because a single
# file build of a Linux executable carries no version resource.  The server
# puts it in the signed manifest.
Write-DesktopVersion "$Root\Server\wwwroot\Content\Win-x64\Remotely_Desktop.exe"
Write-DesktopVersion "$Root\Server\wwwroot\Content\Win-x86\Remotely_Desktop.exe"
Write-DesktopVersion "$Root\Server\wwwroot\Content\Linux-x64\Remotely_Desktop"

# Compress Agents.  The desktop client is not part of the package: the agents
# download it from the server when a session needs it.
New-AgentPackage "$Root\Agent\bin\publish\win-x64" "Remotely-Win-x64.zip"
New-AgentPackage "$Root\Agent\bin\publish\win-x86" "Remotely-Win-x86.zip"
New-AgentPackage "$Root\Agent\bin\publish\linux-x64" "Remotely-Linux.zip"
New-AgentPackage "$Root\Agent\bin\publish\osx-x64" "Remotely-MacOS-x64.zip"
New-AgentPackage "$Root\Agent\bin\publish\osx-arm64" "Remotely-MacOS-arm64.zip"
if ($RID.Length -gt 0 -and $OutDir.Length -gt 0) {
    if ((Test-Path -Path $OutDir) -eq $false) {
        New-Item -Path $OutDir -ItemType Directory
    }

    & $DotNet publish /p:Version=$CurrentVersion /p:FileVersion=$CurrentVersion --runtime $RID --self-contained --configuration Release --output $OutDir "$Root\Server\"
}
else {
    Write-Host "`nSkipping server deployment.  Params -outdir and -rid not specified." -ForegroundColor DarkYellow
}