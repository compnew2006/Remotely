# Server Message Models

## Overview
Typed messages passed between Blazor components, hubs, and messenger subscribers.

## Interface
- Capture messages report audio, camera, screenshot, and keyboard events. [AudioClipReceivedMessage.cs:AudioClipReceivedMessage] [CameraClipReceivedMessage.cs:CameraClipReceivedMessage] [ScreenshotReceivedMessage.cs:ScreenshotReceivedMessage] [KeyboardLogReceivedMessage.cs:KeyboardLogReceivedMessage]
- Device, chat, and script messages report state changes and results. [DeviceStateChangedMessage.cs:DeviceStateChangedMessage] [ChatReceivedMessage.cs:ChatReceivedMessage] [ScriptResultMessage.cs:ScriptResultMessage]
- Transfer, logs, notifications, loader, and completion messages carry UI and transport status. [TransferCompleteMessage.cs:TransferCompleteMessage] [ReceiveLogsMessage.cs:ReceiveLogsMessage] [ShowLoaderMessage.cs:ShowLoaderMessage]

## Invariants
- These records are immutable message payloads with constructor-defined fields. [DeviceStateChangedMessage.cs:DeviceStateChangedMessage] [ScriptResultMessage.cs:ScriptResultMessage]

## Side Effects
- Publishing or handling these payloads is performed by consumers outside this model directory. [ChatReceivedMessage.cs:ChatReceivedMessage]

## Verification
- Run: & "$env:USERPROFILE\.dotnet\dotnet.exe" build Server/Server.csproj