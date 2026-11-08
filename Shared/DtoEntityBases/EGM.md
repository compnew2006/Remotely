# DTO Entity Bases

## Overview

Base contracts for DTOs that also represent persisted script results.

## Interface

- ScriptResultBase defines the shared script-result fields inherited by ScriptResult and ScriptResultDto: device, script input, shell, output/error data, run time, schedule IDs, sender metadata, and timestamp [Shared/DtoEntityBases/ScriptResultBase.cs:ScriptResultBase].

## Invariants

- DeviceID is required [Shared/DtoEntityBases/ScriptResultBase.cs:DeviceID].
- ScriptInput defaults to an empty string; TimeStamp defaults to DateTimeOffset.Now [Shared/DtoEntityBases/ScriptResultBase.cs:ScriptInput/TimeStamp].
- RunTime uses TimeSpanJsonConverter for JSON serialization [Shared/DtoEntityBases/ScriptResultBase.cs:RunTime].

## Side Effects

- None. ScriptResultBase only declares fields and serialization metadata [Shared/DtoEntityBases/ScriptResultBase.cs:ScriptResultBase].

## Verification

Run from the repository root:

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" build Shared/Shared.csproj
```

