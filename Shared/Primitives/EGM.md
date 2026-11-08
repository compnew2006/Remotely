# Primitives

## Overview

Small result and disposal abstractions plus the pure recovery policy used by desktop-process supervision.

## Interface

- Result and its generic form represent success, reason, optional exception, and optional value [Shared/Primitives/Result.cs:Result].
- CallbackDisposable and CallbackDisposableAsync invoke a callback during disposal; NoopDisposable supplies an empty IDisposable [Shared/Primitives/CallbackDisposable.cs:Dispose; Shared/Primitives/CallbackDisposableAsync.cs:DisposeAsync; Shared/Primitives/NoopDisposable.cs:Dispose].
- DesktopProcessRecoveryPolicy exposes CleanExitCode, ShouldRecover, and GetDelay [Shared/Primitives/DesktopProcessRecoveryPolicy.cs:CleanExitCode/ShouldRecover/GetDelay].

## Invariants

- Result(bool isSuccess, ...) requires a reason or exception for an unsuccessful non-generic result [Shared/Primitives/Result.cs:Result.Result(bool,string,Exception?)].
- DesktopProcessRecoveryPolicy.IsCleanExit considers exit code 0 intentional; ShouldRecover rejects clean exits and limits attempts within AttemptWindow [Shared/Primitives/DesktopProcessRecoveryPolicy.cs:IsCleanExit/ShouldRecover].
- DesktopProcessRecoveryPolicy.GetDelay treats attempt numbers below one as one and caps exponential backoff at MaxDelay [Shared/Primitives/DesktopProcessRecoveryPolicy.cs:GetDelay].

## Side Effects

- CallbackDisposable.Dispose and CallbackDisposableAsync.DisposeAsync invoke supplied callbacks; exception handlers receive callback failures [Shared/Primitives/CallbackDisposable.cs:Dispose; Shared/Primitives/CallbackDisposableAsync.cs:DisposeAsync].
- The recovery policy itself is pure and does not start, stop, or inspect processes [Shared/Primitives/DesktopProcessRecoveryPolicy.cs:DesktopProcessRecoveryPolicy].

## Verification

Run from the repository root:

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" build Shared/Shared.csproj
```

