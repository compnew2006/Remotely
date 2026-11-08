# Linux Desktop Startup

## Overview

Registers Linux, shared desktop, and Avalonia UI services for the Linux client.

## Interface

- AddRemoteControlLinux is the platform registration entry point [Desktop.Linux/Startup/IServiceCollectionExtensions.cs:IServiceCollectionExtensions.AddRemoteControlLinux].
- Child package: [Services](../Services/EGM.md).

## Invariants

- AddRemoteControlLinux registers platform-specific screen, input, file-transfer, audio, cursor, session-indicator, and shutdown services after shared and UI registrations [Desktop.Linux/Startup/IServiceCollectionExtensions.cs:IServiceCollectionExtensions.AddRemoteControlLinux].

## Side Effects

- Registration mutates the supplied IServiceCollection [Desktop.Linux/Startup/IServiceCollectionExtensions.cs:IServiceCollectionExtensions.AddRemoteControlLinux].

## Verification

- Run from the repository root: & "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln
