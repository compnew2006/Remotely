# Viewer Client Interfaces

## Overview
TypeScript wire DTO, MessagePack, settings, and file-transfer shapes used by the browser client.

## Interface
- Dtos exports DTO classes and interfaces including DtoWrapper, FileDto, input events, and audio payloads. [Dtos.ts:DtoWrapper] [Dtos.ts:FileDto] [Dtos.ts:AudioSampleDto]
- MessagePack describes the browser serialization codec contract. [MessagePack.ts:MessagePack]
- Settings describes viewer settings persisted by SettingsService. [Settings.ts:Settings]

## Invariants
- DTO properties form the client/server wire contract and must preserve the expected names and shapes. [Dtos.ts:DtoWrapper] [MessagePack.ts:MessagePack]

## Side Effects
- These declarations define data shapes only; serialization and browser storage are performed by client services. [Dtos.ts:DtoWrapper] [Settings.ts:Settings]

## Verification
- Run: & "$env:USERPROFILE\.dotnet\dotnet.exe" build Server/Server.csproj