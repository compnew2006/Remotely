# Desktop Platform Abstractions

## Overview

Interfaces in this directory separate the shared remote-control runtime from operating-system implementations [Desktop.Shared/Abstractions/IScreenCapturer.cs:IScreenCapturer; Desktop.Shared/Abstractions/IKeyboardMouseInput.cs:IKeyboardMouseInput].

## Interface

- Startup, display, input, file-transfer, and shutdown contracts are `IAppStartup`, `IScreenCapturer`, `IKeyboardMouseInput`, `IFileTransferService`, and `IShutdownService` [Desktop.Shared/Abstractions/IAppStartup.cs:IAppStartup; Desktop.Shared/Abstractions/IScreenCapturer.cs:IScreenCapturer; Desktop.Shared/Abstractions/IKeyboardMouseInput.cs:IKeyboardMouseInput; Desktop.Shared/Abstractions/IFileTransferService.cs:IFileTransferService; Desktop.Shared/Abstractions/IShutdownService.cs:IShutdownService].
- Capture contracts are `IAudioCapturer`, `IAudioRecorder`, `ICameraCapturer`, and `IKeyboardLogger` [Desktop.Shared/Abstractions/IAudioCapturer.cs:IAudioCapturer; Desktop.Shared/Abstractions/IAudioRecorder.cs:IAudioRecorder; Desktop.Shared/Abstractions/ICameraCapturer.cs:ICameraCapturer; Desktop.Shared/Abstractions/IKeyboardLogger.cs:IKeyboardLogger].
- UI-facing contracts cover clipboard, chat, cursor changes, session indicators, and attended-access prompts [Desktop.Shared/Abstractions/IClipboardService.cs:IClipboardService; Desktop.Shared/Abstractions/IChatUiService.cs:IChatUiService; Desktop.Shared/Abstractions/ICursorIconWatcher.cs:ICursorIconWatcher; Desktop.Shared/Abstractions/ISessionIndicator.cs:ISessionIndicator; Desktop.Shared/Abstractions/IRemoteControlAccessService.cs:IRemoteControlAccessService].

## Invariants

- `IKeyboardLogger.Start` defaults `showPasswords` to false; false requests masking of password-field text, while true permits the implementation to capture that text [Desktop.Shared/Abstractions/IKeyboardLogger.cs:IKeyboardLogger.Start].
- These declarations define method and event shapes; platform-specific behavior belongs to their implementations [Desktop.Shared/Abstractions/IScreenCapturer.cs:IScreenCapturer; Desktop.Shared/Abstractions/IAudioRecorder.cs:IAudioRecorder].

## Side Effects

- None in this directory's interfaces; implementations perform capture, input, UI, file, and operating-system operations [Desktop.Shared/Abstractions/IKeyboardMouseInput.cs:IKeyboardMouseInput; Desktop.Shared/Abstractions/IFileTransferService.cs:IFileTransferService].

## Verification

- Run from the repository root: `& "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln`
