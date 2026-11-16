#requires -Version 7
<#
.SYNOPSIS
  Verifies a signed Remotely update package before it is released (G2 / AD-13).

.DESCRIPTION
  Checks the three things the agent checks when it updates itself, so the
  release can be proven before it reaches a device:
    1. the SHA-256 of the package matches the manifest,
    2. the ECDsa P-256 signature over the exact manifest bytes verifies with
       the trusted public key,
    3. the key id of the manifest matches the key id of that public key.

  The public key is the base64 SubjectPublicKeyInfo that the server hands to
  the agents.  It can be read from a file (-PublicKeyPath) or from the
  environment variable REMOTELY_UPDATE_PUBLIC_KEY.  It is never the private
  key.

.PARAMETER PackagePath
  The update package, for example Remotely-Win-x64.zip.

.PARAMETER ManifestPath
  The manifest that was written next to the package.  Defaults to the package
  path with ".manifest.json" appended.

.PARAMETER SignaturePath
  The signature that was written next to the manifest.  Defaults to the
  manifest path with ".sig" appended.

.PARAMETER InstallerPath
  The installer, when the manifest also carries its hash.

.PARAMETER PublicKeyPath
  A file that contains the base64 SubjectPublicKeyInfo of the trusted key.

.PARAMETER ExpectedKeyId
  The key id the release is expected to be signed with.  When it is not
  given, the key id of the manifest is checked against the public key.

.EXAMPLE
  .\Verify-Release.ps1 -PackagePath .\Remotely-Win-x64.zip -PublicKeyPath .\update-signing.pub
#>
param(
    [Parameter(Mandatory = $true)][string]$PackagePath,
    [string]$ManifestPath,
    [string]$SignaturePath,
    [string]$InstallerPath,
    [string]$PublicKeyPath,
    [string]$ExpectedKeyId
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $PackagePath -PathType Leaf)) {
    throw "Package not found: $PackagePath"
}

if (-not $ManifestPath) {
    $ManifestPath = "$PackagePath.manifest.json"
}

if (-not $SignaturePath) {
    $SignaturePath = "$ManifestPath.sig"
}

if (-not (Test-Path $ManifestPath -PathType Leaf)) {
    throw "Manifest not found: $ManifestPath"
}

if (-not (Test-Path $SignaturePath -PathType Leaf)) {
    throw "Signature not found: $SignaturePath"
}

$publicKeyBase64 = $null
if ($PublicKeyPath -and (Test-Path $PublicKeyPath -PathType Leaf)) {
    $publicKeyBase64 = (Get-Content $PublicKeyPath -Raw).Trim()
}
elseif ($env:REMOTELY_UPDATE_PUBLIC_KEY) {
    $publicKeyBase64 = $env:REMOTELY_UPDATE_PUBLIC_KEY.Trim()
}
else {
    throw "No public key provided. Use -PublicKeyPath or set REMOTELY_UPDATE_PUBLIC_KEY (base64 SubjectPublicKeyInfo)."
}

$manifestBytes = [IO.File]::ReadAllBytes($ManifestPath)
$manifest = [Text.Encoding]::UTF8.GetString($manifestBytes) | ConvertFrom-Json

# 1. The package hash.
$packageHash = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($PackagePath)))
if ($packageHash -ne $manifest.sha256) {
    throw "The package hash does not match the manifest.  Manifest: $($manifest.sha256)  Package: $packageHash"
}
Write-Host "Package hash matches the manifest: $packageHash"

# 2. The installer hash, when the manifest carries one.
if ($InstallerPath) {
    if (-not (Test-Path $InstallerPath -PathType Leaf)) {
        throw "Installer not found: $InstallerPath"
    }

    $installerHash = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($InstallerPath)))
    if ($installerHash -ne $manifest.installerSha256) {
        throw "The installer hash does not match the manifest.  Manifest: $($manifest.installerSha256)  Installer: $installerHash"
    }
    Write-Host "Installer hash matches the manifest: $installerHash"
}

# 3. The signature over the exact manifest bytes.
$publicKeyBytes = [Convert]::FromBase64String($publicKeyBase64)
$ecdsa = [System.Security.Cryptography.ECDsa]::Create()
try {
    $ecdsa.ImportSubjectPublicKeyInfo($publicKeyBytes, [ref]0) | Out-Null

    $signature = [Convert]::FromBase64String((Get-Content $SignaturePath -Raw).Trim())
    $verified = $ecdsa.VerifyData(
        $manifestBytes,
        $signature,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256)

    if (-not $verified) {
        throw "The signature of the manifest is not valid for the supplied public key."
    }
    Write-Host "The manifest signature is valid."

    # 4. The key id.
    $keyId = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($publicKeyBytes))[..16].ToLowerInvariant()

    if ($ExpectedKeyId -and $ExpectedKeyId -ne $manifest.keyId) {
        throw "The manifest was signed with key id '$($manifest.keyId)', but the release expects '$ExpectedKeyId'."
    }

    if ($manifest.keyId -ne $keyId) {
        throw "The manifest key id '$($manifest.keyId)' does not belong to the supplied public key ('$keyId')."
    }

    Write-Host "Key id matches: $($manifest.keyId)"
}
finally {
    $ecdsa.Dispose()
}

Write-Host ""
Write-Host "The package is signed and its content matches the manifest."
Write-Host "This is release evidence, not a release gate: the gate is recorded in GATE-REGISTER.md."
