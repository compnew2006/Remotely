# Shared

## Overview

Shared is the net10.0 library for Remotely's shared entities, wire contracts,
primitives, and cross-platform helpers. The project enables nullable reference
types and declares AnyCPU, x64, and x86 platforms in Shared.csproj.

## Interface

- AppConstants exposes product defaults, upload limits, and API/session header names, including AppConstants.SessionKeyHeaderName [Shared/AppConstants.cs:AppConstants.SessionKeyHeaderName].
Shared contract folders:

- [Attributes](./Attributes/EGM.md) [Shared/Attributes/SortableAttribute.cs:SortableAttribute]
- [DtoEntityBases](./DtoEntityBases/EGM.md) [Shared/DtoEntityBases/ScriptResultBase.cs:ScriptResultBase]
- [Dtos](./Dtos/EGM.md) [Shared/Dtos/DeviceClientDto.cs:DeviceClientDto]
- [Entities](./Entities/EGM.md) [Shared/Entities/Device.cs:Device]
- [Enums](./Enums/EGM.md) [Shared/Enums/RemoteAccessMode.cs:RemoteAccessMode]
- [Extensions](./Extensions/EGM.md) [Shared/Extensions/DeviceExtensions.cs:DeviceExtensions]
- [Helpers](./Helpers/EGM.md) [Shared/Helpers/PathSanitizer.cs:PathSanitizer]
- [Interfaces](./Interfaces/EGM.md) [Shared/Interfaces/IDesktopHubClient.cs:IDesktopHubClient]
- [Models](./Models/EGM.md) [Shared/Models/CameraCaptureRequest.cs:CameraCaptureRequest]
- [Primitives](./Primitives/EGM.md) [Shared/Primitives/Result.cs:Result]
- [Services](./Services/EGM.md) [Shared/Services/UpdatePackageVerifier.cs:UpdatePackageVerifier]
- [Utilities](./Utilities/EGM.md) [Shared/Utilities/ScreenshotPause.cs:ScreenshotPause]
- [ViewModels](./ViewModels/EGM.md) [Shared/ViewModels/ViewModelBase.cs:ViewModelBase]

## Invariants

- Shared.csproj targets net10.0, enables Nullable, and declares AnyCPU, x64, and x86 platforms [Shared/Shared.csproj:TargetFramework/Nullable/Platforms].
- DtoTypeExtensions.ControlTypes is the shared set used by IsControlMessage to classify DTOs that modify device input. It classifies messages; callers still own authorization [Shared/Models/Dtos/DtoTypeExtensions.cs:ControlTypes/IsControlMessage].
- PathSanitizer.TrySanitizeFileName returns a bare filename or false. Callers must still authorize the destination directory and only combine a successful sanitized name with that destination [Shared/Helpers/PathSanitizer.cs:PathSanitizer.TrySanitizeFileName]. G3 remains FAIL pending the separate live transfer check and review [GATE-REGISTER.md:G3].
- UpdatePackageVerifier.Verify rejects malformed manifests, unknown keys, invalid signatures, package or installer hash mismatches, and version rollback [Shared/Services/UpdatePackageVerifier.cs:UpdatePackageVerifier.Verify]. G2 remains FAIL because signing key management and a release gate remain open [GATE-REGISTER.md:G2].
- DesktopProcessRecoveryPolicy.ShouldRecover does not recover a clean exit (code 0) and caps recent attempts at MaxAttempts; GetDelay applies a capped exponential delay [Shared/Primitives/DesktopProcessRecoveryPolicy.cs:ShouldRecover/GetDelay].

## Side Effects

- FileLogger.Log writes synchronously under the path returned by FileLoggerDefaults.GetLogPath; FileLogger.CleanupLogs deletes log files older than seven days [Shared/Services/FileLogger.cs:Log/CleanupLogs; Shared/Services/FileLoggerDefaults.cs:GetLogPath].
- DtoChunker.TryComplete and RateLimiter use process-local MemoryCache state; both Debouncer implementations keep process-local timers [Shared/Helpers/DtoChunker.cs:TryComplete; Shared/Helpers/RateLImiter.cs:Throttle; Shared/Helpers/Debouncer.cs:Debounce; Shared/Utilities/Debouncer.cs:Debounce].
- ProcessInvoker.InvokeProcessOutput starts an external process and reads its standard output [Shared/Services/ProcessInvoker.cs:InvokeProcessOutput].
- UpdatePackageVerifier.LoadTrustedKeysFile reads a JSON trust-key file; Verify hashes package bytes and performs ECDSA signature verification without writing files [Shared/Services/UpdatePackageVerifier.cs:LoadTrustedKeysFile/Verify].
- AppendableStream delegates reads, writes, and disposal to its wrapped stream [Shared/Utilities/AppendableStream.cs:AppendableStream].

## Verification

Run from the repository root:

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln
```

This is the repository test command in AGENTS.md; it was not run for this documentation task.
