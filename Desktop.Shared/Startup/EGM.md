# Desktop Shared Startup

## Overview

This directory defines common dependency registration and command-line startup for desktop clients [Desktop.Shared/Startup/IServiceCollectionExtensions.cs:IServiceCollectionExtensions; Desktop.Shared/Startup/IServiceProviderExtensions.cs:IServiceProviderExtensions].

## Interface

- `AddRemoteControlXplat` registers common session, SignalR, viewer, monitor, and capture services, including unavailable camera, audio-recorder, and keyboard-logger implementations [Desktop.Shared/Startup/IServiceCollectionExtensions.cs:AddRemoteControlXplat].
- `CommandProvider.CreateRemoteControlCommand` defines host, mode, pipe, session, access-key, requester, organization, relaunch, viewer, and elevate options [Desktop.Shared/Startup/CommandProvider.cs:CommandProvider.CreateRemoteControlCommand].
- `IServiceProviderExtensions.UseRemoteControlClient` parses command-line options or accepts explicit startup values, configures `IAppState`, and runs the registered `IAppStartup` [Desktop.Shared/Startup/IServiceProviderExtensions.cs:IServiceProviderExtensions.UseRemoteControlClient].

## Invariants

- The command defaults to `AppMode.Attended`; Chat mode requires a nonblank pipe name [Desktop.Shared/Startup/CommandProvider.cs:CommandProvider.CreateRemoteControlCommand].
- Windows elevation handling runs before app-state configuration; the elevated relaunch returns without continuing normal startup in that process [Desktop.Shared/Startup/IServiceProviderExtensions.cs:IServiceProviderExtensions.UseRemoteControlClient].

## Side Effects

- Service registration mutates the supplied `IServiceCollection`; client startup sets `StaticServiceProvider.Instance` and invokes `IAppStartup.Run` [Desktop.Shared/Startup/IServiceCollectionExtensions.cs:AddRemoteControlXplat; Desktop.Shared/Startup/IServiceProviderExtensions.cs:IServiceProviderExtensions.UseRemoteControlClient].
- Startup subscribes to `TaskScheduler.UnobservedTaskException`; on Windows, the elevate option can relaunch the client process [Desktop.Shared/Startup/IServiceProviderExtensions.cs:IServiceProviderExtensions.UseRemoteControlClient; Desktop.Shared/Startup/IServiceProviderExtensions.cs:RelaunchElevated].

## Verification

- Run from the repository root: `& "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln`
