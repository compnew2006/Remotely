# Models

## Overview

Shared request, response, and UI-facing models for device setup, remote control, screen and media capture, and user preferences.

## Interface

- RemoteControlAccessRequest carries viewer connection, requester display name, and organization name [Shared/Models/RemoteControlAccessRequest.cs:RemoteControlAccessRequest].
- RemoteControlViewerOptions and ScreenCastRequest carry the ViewOnly flag alongside session/viewer display data [Shared/Models/RemoteControlViewerOptions.cs:ViewOnly; Shared/Models/ScreenCastRequest.cs:ViewOnly].
- AudioCaptureRequest, CameraCaptureRequest, CameraDeviceInfo, CameraAudioDeviceInfo, and quality/source enums describe recording requests and available devices [Shared/Models/AudioCaptureRequest.cs:AudioCaptureRequest; Shared/Models/CameraCaptureRequest.cs:CameraCaptureRequest; Shared/Models/CameraDeviceInfo.cs:CameraDeviceInfo; Shared/Models/CameraAudioDeviceInfo.cs:CameraAudioDeviceInfo].
- ScreenshotPauseWindow describes start/end hours; ScreenshotPause parses, serializes, and evaluates these windows [Shared/Models/ScreenshotPauseWindow.cs:ScreenshotPauseWindow; Shared/Utilities/ScreenshotPause.cs:ScreenshotPause].
- DeviceClientDto is defined in Shared/Dtos [Shared/Dtos/DeviceClientDto.cs:DeviceClientDto].
- Input and event DTOs live in [Models/Dtos](./Dtos/EGM.md) [Shared/Models/Dtos/DtoType.cs:DtoType].
- Other models include DisplayInfo, Drive, EmbeddedServerData, KeyboardLogChunk, PwshCommandCompletion, SentFrame, WindowsSession, user options, and alert options [Shared/Models/DisplayInfo.cs:DisplayInfo; Shared/Models/Drive.cs:Drive; Shared/Models/EmbeddedServerData.cs:EmbeddedServerData; Shared/Models/KeyboardLogChunk.cs:KeyboardLogChunk; Shared/Models/PwshCommandCompletion.cs:PwshCommandCompletion; Shared/Models/SentFrame.cs:SentFrame; Shared/Models/WindowsSession.cs:WindowsSession; Shared/Models/RemotelyUserOptions.cs:RemotelyUserOptions; Shared/Models/AlertOptions.cs:AlertOptions].
- RemotelyUserOptions has a generated TypeScript declaration with display name, shortcut, and theme fields [Shared/Models/RemotelyUserOptions.cs.d.ts:remotelyUserOptions].

## Invariants

- AudioCaptureRequest defaults to system audio, medium quality, and 30 seconds; CameraCaptureRequest defaults to camera video and no audio [Shared/Models/AudioCaptureRequest.cs:Source/Quality/DurationSeconds; Shared/Models/CameraCaptureRequest.cs:VideoSource/AudioSource].
- ScreenshotPause.IsPaused treats equal start/end hours as disabled and supports windows that wrap midnight; malformed JSON in ScreenshotPause.Deserialize yields an empty list [Shared/Utilities/ScreenshotPause.cs:IsPaused/Deserialize].
- CameraSourceSelection serializes video and audio selectors with a vertical-bar separator and falls back to Camera or None for invalid selector kinds [Shared/Utilities/CameraSourceSelection.cs:Separator/ParseSource/ParseAudio].
- RemoteControlViewerOptions.ViewOnly and ScreenCastRequest.ViewOnly are fields in shared messages; they do not independently authorize or block a control operation [Shared/Models/RemoteControlViewerOptions.cs:ViewOnly; Shared/Models/ScreenCastRequest.cs:ViewOnly].

## Side Effects

- ScreenshotPause.Serialize and Deserialize operate on JSON strings; they do not persist them [Shared/Utilities/ScreenshotPause.cs:Serialize/Deserialize].
- Model declarations hold values and have no direct external side effects [Shared/Models/AudioCaptureRequest.cs:AudioCaptureRequest; Shared/Models/RemoteControlAccessRequest.cs:RemoteControlAccessRequest].

## Verification

Run from the repository root:

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" build Shared/Shared.csproj
```

