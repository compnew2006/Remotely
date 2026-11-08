# Server Enums

## Overview
Enums that model device-card presentation, stream state, and toast severity.

## Interface
- DeviceCardState distinguishes Normal and Expanded card states. [DeviceCardState.cs:DeviceCardState]
- StreamerState is a flags enum for connection, reconnect, and Windows session transition states. [StreamerState.cs:StreamerState]
- ToastType enumerates supported toast severity and style categories. [ToastType.cs:ToastType]

## Invariants
- StreamerState values are bit flags and are intended to be combined. [StreamerState.cs:Flags]

## Side Effects
- These enums are value contracts and contain no state-changing operations. [DeviceCardState.cs:DeviceCardState]

## Verification
- Run: & "$env:USERPROFILE\.dotnet\dotnet.exe" build Server/Server.csproj