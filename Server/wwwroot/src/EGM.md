# Viewer Client Source

## Overview
TypeScript browser client for remote-control streams, SignalR messaging, input, file transfer, settings, session recording, and viewer UI.

## Interface
- App initializes viewer behavior and ViewerHubConnection manages the browser’s SignalR viewer connection. [App.ts:App] [ViewerHubConnection.ts:ViewerHubConnection]
- CaptureProcessor, Stream, and SessionRecorder process stream data and recording. [CaptureProcessor.ts:ProcessStream] [Stream.ts:IStreamSubscriber] [SessionRecorder.ts:SessionRecorder]
- DtoChunker, DtoMessageHandler, and MessageSender encode and dispatch viewer DTO traffic. [DtoChunker.ts:ChunkDto] [DtoMessageHandler.ts:DtoMessageHandler] [MessageSender.ts:MessageSender]
- FileTransferService, ClipboardWatcher, InputEventHandlers, SettingsService, Sound, UI, and Utilities implement browser-facing operations. [FileTransferService.ts:UploadFiles] [ClipboardWatcher.ts:ClipboardWatcher] [SettingsService.ts:GetSettings] [UI.ts:ShowToast]
- Protocol enums, wire interfaces, and view models are documented in [Enums](./Enums/EGM.md), [Interfaces](./Interfaces/EGM.md), and [Models](./Models/EGM.md). [Enums/EGM.md:Viewer Client Enums] [Interfaces/EGM.md:Viewer Client Interfaces] [Models/EGM.md:Viewer Client Models]

## Invariants
- DTO payloads are chunked and reassembled using the same DtoType and request identifier. [DtoChunker.ts:ChunkDto] [DtoChunker.ts:TryComplete]
- ViewerHubConnection reconnects and re-requests viewer state through its SignalR connection lifecycle. [ViewerHubConnection.ts:ViewerHubConnection]

## Side Effects
- Client modules interact with the DOM, browser clipboard, audio playback, settings storage, and SignalR. [UI.ts:CloseAllPopupMenus] [ClipboardWatcher.ts:ClipboardWatcher] [SettingsService.ts:SetSettings] [ViewerHubConnection.ts:ViewerHubConnection]
- FileTransferService uploads selected files and receives file payloads through the viewer connection. [FileTransferService.ts:UploadFiles] [FileTransferService.ts:ReceiveFile]

## Verification
- Run: & "$env:USERPROFILE\.dotnet\dotnet.exe" build Server/Server.csproj