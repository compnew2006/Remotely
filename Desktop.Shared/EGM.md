# Shared Desktop Runtime

## Overview

`Desktop.Shared` contains the desktop clients' common runtime, platform service contracts, remote-control session handling, and capture orchestration. Windows and Linux projects supply operating-system implementations through the shared abstractions [Desktop.Shared/Desktop.Shared.csproj:TargetFramework; Desktop.Shared/Abstractions/IScreenCapturer.cs:IScreenCapturer; Desktop.Shared/Startup/IServiceCollectionExtensions.cs:AddRemoteControlXplat].

## Interface

- `IDesktopHubConnection` defines the SignalR client boundary for connection lifecycle, viewer/session messages, screenshots, recorded audio and camera clips, live audio, and keyboard log chunks; `DesktopHubConnection` implements it [Desktop.Shared/Services/DesktopHubConnection.cs:IDesktopHubConnection; Desktop.Shared/Services/DesktopHubConnection.cs:DesktopHubConnection].
- `IViewer` sends a desktop stream and viewer metadata, file transfers, audio samples, clipboard text, and session metrics [Desktop.Shared/Services/Viewer.cs:IViewer].
- `IScreenCaster.BeginScreenCasting` creates and tracks a viewer, sends display metadata and a desktop stream, and handles session cleanup [Desktop.Shared/Services/ScreenCaster.cs:IScreenCaster; Desktop.Shared/Services/ScreenCaster.cs:ScreenCaster.BeginScreenCasting; Desktop.Shared/Services/ScreenCaster.cs:ScreenCaster.BeginScreenCastingImpl].
- `IDtoMessageHandler.ParseMessage` decodes viewer DTOs and delegates control, clipboard, and file-transfer actions to platform abstractions [Desktop.Shared/Services/DtoMessageHandler.cs:IDtoMessageHandler; Desktop.Shared/Services/DtoMessageHandler.cs:DtoMessageHandler.ParseMessage].
- `AddRemoteControlXplat` registers the common hub, viewer, screen/audio/camera monitoring, capture orchestration, and keyboard-log services [Desktop.Shared/Startup/IServiceCollectionExtensions.cs:AddRemoteControlXplat].
- Child contracts: [Abstractions](./Abstractions/EGM.md), [Enums](./Enums/EGM.md), [Extensions](./Extensions/EGM.md), [Messages](./Messages/EGM.md), [Properties](./Properties/EGM.md), [Reactive](./Reactive/EGM.md), [Services](./Services/EGM.md), [Startup](./Startup/EGM.md), [ViewModels](./ViewModels/EGM.md).

## Invariants

- `ScreenCaster.BeginScreenCastingImpl` sets `IViewer.HasControl` to the inverse of the incoming request's `ViewOnly` flag. `DtoMessageHandler.ParseMessage` logs and drops messages classified by `DtoType.IsControlMessage` whenever that viewer does not have control [Desktop.Shared/Services/ScreenCaster.cs:ScreenCaster.BeginScreenCastingImpl; Desktop.Shared/Services/DtoMessageHandler.cs:DtoMessageHandler.ParseMessage; Shared/Models/Dtos/DtoTypeExtensions.cs:DtoTypeExtensions.IsControlMessage].
- Cross-platform registration supplies unavailable implementations for camera capture, audio recording, and keyboard logging; platform projects register their supported implementations [Desktop.Shared/Startup/IServiceCollectionExtensions.cs:AddRemoteControlXplat; Desktop.Shared/Services/UnavailableCameraCapturer.cs:UnavailableCameraCapturer.RecordAsync; Desktop.Shared/Services/UnavailableAudioRecorder.cs:UnavailableAudioRecorder.RecordAsync; Desktop.Shared/Services/UnavailableKeyboardLogger.cs:UnavailableKeyboardLogger.Start].
- After a SignalR reconnect, `DesktopHubConnection` resumes camera and audio monitoring only in Unattended mode and only when the corresponding app-state flags are set [Desktop.Shared/Services/DesktopHubConnection.cs:DesktopHubConnection.HandleReconnected].

## Side Effects

- `DesktopHubConnection` opens a SignalR connection and sends session, stream, screenshot, media, and keyboard messages to the server [Desktop.Shared/Services/DesktopHubConnection.cs:DesktopHubConnection.Connect; Desktop.Shared/Services/DesktopHubConnection.cs:DesktopHubConnection.SendScreenshot; Desktop.Shared/Services/DesktopHubConnection.cs:DesktopHubConnection.SendKeyboardLog].
- `ScreenCaster` streams frames to viewers, publishes viewer/session metadata, and asks the shutdown service to stop an Unattended client when no viewers remain [Desktop.Shared/Services/ScreenCaster.cs:ScreenCaster.BeginScreenCastingImpl].
- `AudioCaptureService.CaptureAsync` serializes clip recording, uploads the result, and deletes the temporary clip file after upload [Desktop.Shared/Services/AudioCaptureService.cs:AudioCaptureService.CaptureAsync].

## Verification

- Run from the repository root: `& "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln`
