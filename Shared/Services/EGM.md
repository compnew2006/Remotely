# Services

## Overview

Shared service interfaces and implementations for elevation checks, process invocation, embedded server data, logging, and update verification.

## Interface

- IElevationDetector.IsElevated has Linux, macOS, and Windows implementations [Shared/Services/IElevationDetector.cs:IsElevated; Shared/Services/ElevationDetectorLinux.cs:IsElevated; Shared/Services/ElevationDetectorMac.cs:IsElevated; Shared/Services/ElevationDetectorWin.cs:IsElevated].
- IProcessInvoker.InvokeProcessOutput runs a command and returns captured standard output [Shared/Services/ProcessInvoker.cs:IProcessInvoker.InvokeProcessOutput].
- ISystemTime exposes Now, Offset, Restore, and Set for adjustable time [Shared/Services/SystemTime.cs:ISystemTime].
- IEmbeddedServerDataProvider encodes EmbeddedServerData into a filename and returns a Result when reading it back [Shared/Services/EmbeddedServerDataProvider.cs:IEmbeddedServerDataProvider].
- UpdatePackageVerifier.Verify validates UpdateManifest signatures, package and optional installer hashes, trusted key IDs, and version rollback [Shared/Services/UpdatePackageVerifier.cs:UpdatePackageVerifier.Verify].
- FileLoggerProvider creates ILogger instances backed by FileLogger [Shared/Services/FileLoggerProvider.cs:CreateLogger].

## Invariants

- UpdatePackageVerifier.Verify fails closed for malformed manifests, unknown keys, invalid signatures, hash mismatches, and versions older than currentVersion [Shared/Services/UpdatePackageVerifier.cs:UpdatePackageVerifier.Verify].
- UpdatePackageVerifier.LoadTrustedKeysFile merges a JSON key file with BuiltInTrustedKeys; private signing keys are not handled by this shared verifier [Shared/Services/UpdatePackageVerifier.cs:LoadTrustedKeysFile/BuiltInTrustedKeys].
- ElevationDetectorLinux and ElevationDetectorMac consider effective user ID 0 elevated; ElevationDetectorWin checks the current Windows identity's Administrator role [Shared/Services/ElevationDetectorLinux.cs:IsElevated; Shared/Services/ElevationDetectorMac.cs:IsElevated; Shared/Services/ElevationDetectorWin.cs:IsElevated].

## Side Effects

- FileLogger.Log writes synchronously to disk; FileLogger.CleanupLogs attempts to delete files older than seven days [Shared/Services/FileLogger.cs:Log/CleanupLogs].
- FileLoggerDefaults.LogsFolderPath selects a platform-specific log path and creates it through FileLogger.CheckLogFileExists [Shared/Services/FileLoggerDefaults.cs:LogsFolderPath; Shared/Services/FileLogger.cs:CheckLogFileExists].
- ProcessInvoker.InvokeProcessOutput starts an operating-system process and logs launch failures [Shared/Services/ProcessInvoker.cs:InvokeProcessOutput].
- UpdatePackageVerifier.LoadTrustedKeysFile reads the supplied key file; Verify computes SHA-256 hashes and performs ECDSA verification [Shared/Services/UpdatePackageVerifier.cs:LoadTrustedKeysFile/Verify].
- EmbeddedServerDataProvider serializes and parses filename content but does not itself write the containing executable [Shared/Services/EmbeddedServerDataProvider.cs:GetEncodedFileName/TryGetEmbeddedData].

## Verification

Run from the repository root:

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" build Shared/Shared.csproj
```

