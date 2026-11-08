# Shared Desktop Services

## Overview

This directory contains common desktop state, SignalR orchestration, viewer streaming, DTO handling, and screenshot, audio, camera, and keyboard capture services [Desktop.Shared/Services/AppState.cs:AppState; Desktop.Shared/Services/DesktopHubConnection.cs:DesktopHubConnection; Desktop.Shared/Services/ScreenCaster.cs:ScreenCaster].

## Interface

- `IAppState` exposes client mode, session configuration, capture flags, and active viewers [Desktop.Shared/Services/AppState.cs:IAppState; Desktop.Shared/Services/AppState.cs:AppState].
- `IDesktopHubConnection` defines the desktop SignalR boundary; `DesktopHubConnection` registers incoming callbacks and sends session and capture messages [Desktop.Shared/Services/DesktopHubConnection.cs:IDesktopHubConnection; Desktop.Shared/Services/DesktopHubConnection.cs:DesktopHubConnection.ApplyConnectionHandlers].
- `IViewer` and `IScreenCaster` expose per-viewer screen, audio, clipboard, metrics, and stream operations [Desktop.Shared/Services/Viewer.cs:IViewer; Desktop.Shared/Services/ScreenCaster.cs:IScreenCaster].
- `IDtoMessageHandler.ParseMessage` processes MessagePack viewer messages; `IScreenMonitor`, `ICameraMonitor`, `IAudioMonitor`, `IAudioCaptureService`, `ICameraCaptureService`, `IAudioLiveStreamer`, and `IKeyboardLogService` expose capture lifecycles [Desktop.Shared/Services/DtoMessageHandler.cs:IDtoMessageHandler.ParseMessage; Desktop.Shared/Services/ScreenMonitor.cs:IScreenMonitor; Desktop.Shared/Services/CameraMonitor.cs:ICameraMonitor; Desktop.Shared/Services/AudioMonitor.cs:IAudioMonitor; Desktop.Shared/Services/AudioCaptureService.cs:IAudioCaptureService; Desktop.Shared/Services/CameraCaptureService.cs:ICameraCaptureService; Desktop.Shared/Services/AudioLiveStreamer.cs:IAudioLiveStreamer; Desktop.Shared/Services/KeyboardLogService.cs:IKeyboardLogService].

## Invariants

- `ScreenCaster.BeginScreenCastingImpl` derives `IViewer.HasControl` from the request's `ViewOnly` value. `DtoMessageHandler.ParseMessage` rejects classified control messages when `HasControl` is false [Desktop.Shared/Services/ScreenCaster.cs:ScreenCaster.BeginScreenCastingImpl; Desktop.Shared/Services/DtoMessageHandler.cs:DtoMessageHandler.ParseMessage; Shared/Models/Dtos/DtoTypeExtensions.cs:DtoTypeExtensions.IsControlMessage].
- Audio and camera clip capture requests are serialized and delete their temporary files after upload [Desktop.Shared/Services/AudioCaptureService.cs:AudioCaptureService.CaptureAsync; Desktop.Shared/Services/CameraCaptureService.cs:CameraCaptureService.CaptureAsync].
- `KeyboardLogService` passes its `ShowPasswords` option to the configured `IKeyboardLogger`; the concrete logger decides how that option affects password-field text [Desktop.Shared/Services/KeyboardLogService.cs:KeyboardLogService.Start; Desktop.Shared/Abstractions/IKeyboardLogger.cs:IKeyboardLogger.Start].
- After reconnect, camera and audio monitoring resume only in Unattended mode and only when their corresponding app-state flags are set [Desktop.Shared/Services/DesktopHubConnection.cs:DesktopHubConnection.HandleReconnected].
- Default cross-platform registrations use unavailable camera, audio-recording, and keyboard-logging implementations; these return empty device lists or failed results, or do no capture [Desktop.Shared/Services/UnavailableCameraCapturer.cs:UnavailableCameraCapturer; Desktop.Shared/Services/UnavailableAudioRecorder.cs:UnavailableAudioRecorder; Desktop.Shared/Services/UnavailableKeyboardLogger.cs:UnavailableKeyboardLogger].

## Side Effects

- `DesktopHubConnection` connects to the server and sends session, screenshot, audio/video capture, and keyboard messages over SignalR [Desktop.Shared/Services/DesktopHubConnection.cs:DesktopHubConnection.Connect; Desktop.Shared/Services/DesktopHubConnection.cs:DesktopHubConnection.SendScreenshot; Desktop.Shared/Services/DesktopHubConnection.cs:DesktopHubConnection.SendAudioClipChunk; Desktop.Shared/Services/DesktopHubConnection.cs:DesktopHubConnection.SendCameraClipChunk; Desktop.Shared/Services/DesktopHubConnection.cs:DesktopHubConnection.SendKeyboardLog].
- `ScreenCaster` streams frames to viewers, publishes viewer metadata, and shuts down an Unattended client when no viewers remain [Desktop.Shared/Services/ScreenCaster.cs:ScreenCaster.BeginScreenCastingImpl].
- `ScreenMonitor.CaptureNow` starts a one-shot capture or wakes the periodic loop; `AudioCaptureService.CaptureAsync` and `CameraCaptureService.CaptureAsync` record, upload, and remove clip files [Desktop.Shared/Services/ScreenMonitor.cs:ScreenMonitor.CaptureNow; Desktop.Shared/Services/AudioCaptureService.cs:AudioCaptureService.CaptureAsync; Desktop.Shared/Services/CameraCaptureService.cs:CameraCaptureService.CaptureAsync].
- `DtoMessageHandler` delegates authorized input, clipboard, audio, and file-transfer operations to injected platform services [Desktop.Shared/Services/DtoMessageHandler.cs:DtoMessageHandler.ParseMessage].

## Verification

- Run from the repository root: `& "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln`
