# DTOs

## Overview

Small client and response DTOs exchanged by the shared library and server.

## Interface

- DeviceClientDto is a DataContract whose DataMember properties carry agent version, device identity, platform, online state, resource usage, network addresses, drives, and organization ID [Shared/Dtos/DeviceClientDto.cs:DeviceClientDto].
- ScriptResultDto derives from ScriptResultBase without adding fields [Shared/Dtos/ScriptResultDto.cs:ScriptResultDto].
- ScriptResultResponse exposes a required, init-only Id [Shared/Dtos/ScriptResultResponse.cs:ScriptResultResponse.Id].

## Invariants

- DeviceClientDto's serialized members are explicitly marked with DataMember [Shared/Dtos/DeviceClientDto.cs:DataMember].
- ScriptResultDto inherits the ScriptResultBase contract rather than defining a separate result shape [Shared/Dtos/ScriptResultDto.cs:ScriptResultDto].

## Side Effects

- None. These types hold or serialize data [Shared/Dtos/DeviceClientDto.cs:DeviceClientDto; Shared/Dtos/ScriptResultDto.cs:ScriptResultDto; Shared/Dtos/ScriptResultResponse.cs:ScriptResultResponse].

## Verification

Run from the repository root:

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" build Shared/Shared.csproj
```

