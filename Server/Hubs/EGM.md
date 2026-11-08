# SignalR Hubs

## Overview
Three SignalR hubs coordinate agent registration and work, desktop-client monitoring and streams, and browser viewer sessions.

## Interface
- AgentHub handles agent online state, heartbeats, scripts, logs, downloads, and update-key retrieval. [AgentHub.cs:DeviceCameOnline] [AgentHub.cs:DeviceHeartbeat] [AgentHub.cs:GetUpdatePublicKey]
- DesktopHub receives desktop session, stream, screenshot, audio, camera, and keyboard events. [DesktopHub.cs:ReceiveUnattendedSessionInfoV2] [DesktopHub.cs:SendScreenshot] [DesktopHub.cs:SendAudioLiveChunk]
- ViewerHub attaches viewers, streams desktop data, authorizes control messages, and stores session recordings. [ViewerHub.cs:SendScreenCastRequestToDevice] [ViewerHub.cs:SendDtoToClient] [ViewerHub.cs:StoreSessionRecording]
- CircuitConnection is the Blazor circuit adapter used by UI components for remote-control actions. [CircuitConnection.cs:RemoteControl]

## Invariants
- Viewer control authority is evaluated for the viewer connection, and malformed control DTOs fail closed. [ViewerHub.cs:ViewerMayControl] [ViewerHub.cs:TryGetDtoType]
- Desktop schedules are restored only from matching server-owned monitoring sessions after reconnect. [DesktopHub.cs:ResumeCameraMonitoring] [DesktopHub.cs:ResumeAudioMonitoring]
- Device connection and online state are maintained through AgentHubSessionCache. [AgentHub.cs:DeviceCameOnline] [AgentHub.cs:OnDisconnectedAsync]

## Side Effects
- Hubs send messages across SignalR hub contexts and update circuit/session caches. [AgentHub.cs:CheckForPendingRemoteControlSessions] [DesktopHub.cs:SendDesktopStream] [ViewerHub.cs:SendDtoToClient]
- DesktopHub stores received monitoring uploads through capture services and publishes typed messenger events. [DesktopHub.cs:CameraClipUploadCompleted] [DesktopHub.cs:AudioClipUploadCompleted] [DesktopHub.cs:SendKeyboardLog]
- ViewerHub records refused control decisions and can forward WebM data to the recording sink. [ViewerHub.cs:LogPrivilegeRefused] [ViewerHub.cs:StoreSessionRecording]

## Verification
- Run: & "$env:USERPROFILE\.dotnet\dotnet.exe" build Server/Server.csproj