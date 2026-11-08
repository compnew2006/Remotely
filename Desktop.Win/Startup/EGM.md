# Windows Desktop Startup

## Overview

This directory contains the Windows dependency-registration extension [Desktop.Win/Startup/IServiceCollectionExtensions.cs:IServiceCollectionExtensions].

## Interface

- `AddRemoteControlWindows` adds the shared runtime and UI services, then registers Windows implementations for cursor watching, keyboard/mouse input, audio and camera capture, screen capture, keyboard logging, file transfer, startup, message pumping, and shutdown [Desktop.Win/Startup/IServiceCollectionExtensions.cs:IServiceCollectionExtensions.AddRemoteControlWindows].

## Invariants

- The Windows-specific implementations are registered after `AddRemoteControlXplat` and `AddRemoteControlUi`; this registers them after the cross-platform fallback services [Desktop.Win/Startup/IServiceCollectionExtensions.cs:IServiceCollectionExtensions.AddRemoteControlWindows].

## Side Effects

- `AddRemoteControlWindows` mutates the supplied `IServiceCollection` [Desktop.Win/Startup/IServiceCollectionExtensions.cs:IServiceCollectionExtensions.AddRemoteControlWindows].

## Verification

- Run from the repository root: `& "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln`
