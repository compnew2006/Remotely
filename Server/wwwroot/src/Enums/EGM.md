# Viewer Client Enums

## Overview
TypeScript enums that define DTO message types, hub state, remote-control mode, and Windows session type.

## Interface
- DtoType identifies browser-to-server DTO payload kinds. [DtoType.ts:DtoType]
- HubConnectionState and RemoteControlMode represent connection and access modes. [HubConnectionState.ts:HubConnectionState] [RemoteControlMode.ts:RemoteControlMode]
- WindowsSessionType labels target Windows session categories. [WindowsSessionType.ts:WindowsSessionType]

## Invariants
- These enum values are protocol-facing and must stay aligned with their server/shared counterparts. [DtoType.ts:DtoType] [RemoteControlMode.ts:RemoteControlMode]

## Side Effects
- Enum values are serialized in browser-client messages; the enum declarations themselves have no external side effects. [DtoType.ts:DtoType]

## Verification
- Run: & "$env:USERPROFILE\.dotnet\dotnet.exe" build Server/Server.csproj