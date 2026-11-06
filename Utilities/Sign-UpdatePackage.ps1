#requires -Version 5.1
<#
.SYNOPSIS
  Signs a Remotely update package and produces the manifest + signature (G2 / AD-13).

.DESCRIPTION
  Builds a signed manifest for a Remotely agent update package:
    {package}.manifest.json  - the manifest (package + installer SHA-256, version, key id)
    {package}.manifest.sig   - ECDsa P-256 signature over the exact manifest bytes (base64)

  The signing key NEVER lives in this repository. Provide it as a base64
  PKCS#8/ECC private-key blob via -KeyPath or the REMOTELY_UPDATE_SIGNING_KEY
  environment variable. Production keys belong in the release pipeline's
  secret store (KMS/HSM), per owner decision AD-13.

.EXAMPLE
  .\Sign-UpdatePackage.ps1 -PackagePath .\Remotely-Win-x64.zip -InstallerPath .\Install-Remotely.ps1 -Version 1.2.3 -KeyId prod-2026-11
#>
param(
    [Parameter(Mandatory = $true)][string]$PackagePath,
    [Parameter(Mandatory = $true)][string]$InstallerPath,
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$KeyId,
    [string]$KeyPath,
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $PackagePath)) { throw "Package not found: $PackagePath" }
if (-not (Test-Path $InstallerPath)) { throw "Installer not found: $InstallerPath" }

$keyBlob = $null
if ($KeyPath -and (Test-Path $KeyPath)) {
    $keyBlob = [Convert]::FromBase64String((Get-Content $KeyPath -Raw).Trim())
}
elseif ($env:REMOTELY_UPDATE_SIGNING_KEY) {
    $keyBlob = [Convert]::FromBase64String($env:REMOTELY_UPDATE_SIGNING_KEY.Trim())
}
else {
    throw "No signing key provided. Use -KeyPath or set REMOTELY_UPDATE_SIGNING_KEY (base64 PKCS#8). Keys must never be committed."
}

$ecdsa = [System.Security.Cryptography.ECDsa]::Create()
try {
    $ecdsa.ImportPkcs8PrivateKey($keyBlob, [ref]0) | Out-Null

    $packageHash    = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($PackagePath)))
    $installerHash  = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($InstallerPath)))

    $manifest = [ordered]@{
        package         = [IO.Path]::GetFileName($PackagePath)
        version         = $Version
        sha256          = $packageHash
        installerSha256 = $installerHash
        keyId           = $KeyId
    }

    $manifestJson = $manifest | ConvertTo-Json
    $manifestBytes = [Text.Encoding]::UTF8.GetBytes($manifestJson)
    $signature = [Convert]::ToBase64String($ecdsa.SignData($manifestBytes, [System.Security.Cryptography.HashAlgorithmName]::SHA256))

    $outDir = if ($OutputDirectory) { $OutputDirectory } else { Split-Path $PackagePath -Parent }
    $manifestPath = Join-Path $outDir ([IO.Path]::GetFileName($PackagePath) + '.manifest.json')
    $sigPath = Join-Path $outDir ([IO.Path]::GetFileName($PackagePath) + '.manifest.sig')

    [IO.File]::WriteAllBytes($manifestPath, $manifestBytes)
    [IO.File]::WriteAllText($sigPath, $signature)

    Write-Host "Manifest written: $manifestPath"
    Write-Host "Signature written: $sigPath"
    Write-Host "Deploy both files next to the package on the server (Content/), named exactly as the updater expects:"
    Write-Host "  Remotely-Win-<platform>.zip.manifest.json / .sig"
}
finally {
    $ecdsa.Dispose()
}
