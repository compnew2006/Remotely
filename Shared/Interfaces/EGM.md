# Interfaces

## Overview

Typed SignalR client callback contracts shared by the server and connected viewers, agents, and desktop clients.

## Interface

- IAgentHubClient declares agent-directed callbacks such as RemoteControl, RunScript, ExecuteCommand, TransferFileFromBrowserToAgent, and WakeDevice [Shared/Interfaces/IAgentHubClient.cs:RemoteControl/RunScript/ExecuteCommand/TransferFileFromBrowserToAgent/WakeDevice].
- IDesktopHubClient declares desktop-directed callbacks for screen casts, DTO transport, monitoring, camera/audio capture, live audio, and keyboard capture [Shared/Interfaces/IDesktopHubClient.cs:RequestScreenCast/SendDtoToClient/StartMonitoring/StartCameraCapture/StartAudioCapture/StartAudioLive/StartKeyboardLogging].
- IViewerHubClient declares viewer callbacks for reconnect state, DTO delivery, messages, and screen-caster state [Shared/Interfaces/IViewerHubClient.cs:Reconnecting/SendDtoToViewer/ShowMessage/ScreenCasterDisconnected].

## Invariants

- Interface declarations define method names and argument shapes, not server authorization or device-side enforcement [Shared/Interfaces/IAgentHubClient.cs:IAgentHubClient; Shared/Interfaces/IDesktopHubClient.cs:IDesktopHubClient; Shared/Interfaces/IViewerHubClient.cs:IViewerHubClient].
- IDesktopHubClient.RequestScreenCast and GetScreenCast carry a ViewOnly argument; the type does not establish how a receiver enforces it [Shared/Interfaces/IDesktopHubClient.cs:RequestScreenCast/GetScreenCast].
- Monitoring and capture callbacks use separate start/stop methods for screenshots, camera, recorded audio, live audio, and keyboard logging [Shared/Interfaces/IDesktopHubClient.cs:StartMonitoring/StopMonitoring/StartCameraMonitoring/StopCameraMonitoring/StartAudioMonitoring/StopAudioMonitoring/StartAudioLive/StopAudioLive/StartKeyboardLogging/StopKeyboardLogging].

## Side Effects

- None from the interfaces themselves. IAgentHubClient, IDesktopHubClient, and IViewerHubClient declare methods without implementing operations [Shared/Interfaces/IAgentHubClient.cs:IAgentHubClient; Shared/Interfaces/IDesktopHubClient.cs:IDesktopHubClient; Shared/Interfaces/IViewerHubClient.cs:IViewerHubClient].

## Verification

Run from the repository root:

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" build Shared/Shared.csproj
```

