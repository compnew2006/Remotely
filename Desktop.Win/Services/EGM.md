# Windows Desktop Services

## Overview

This directory implements the Windows adapters used by the shared desktop runtime, including desktop startup, screen capture, input, file transfer, audio, camera, keyboard, cursor, message-loop, and shutdown services [Desktop.Win/Startup/IServiceCollectionExtensions.cs:AddRemoteControlWindows].

## Interface

- `AppStartup.Run` selects startup behavior for Unattended, Attended, and Chat modes [Desktop.Win/Services/AppStartup.cs:AppStartup.Run].
- `ScreenCapturerWin` implements `IScreenCapturer`; `KeyboardMouseInputWin` implements `IKeyboardMouseInput`; `FileTransferServiceWin` implements `IFileTransferService` [Desktop.Win/Services/ScreenCapturerWin.cs:ScreenCapturerWin; Desktop.Win/Services/KeyboardMouseInputWin.cs:KeyboardMouseInputWin; Desktop.Win/Services/FileTransferServiceWin.cs:FileTransferServiceWin].
- `AudioCapturerWin` and `AudioRecorderWin` implement `IAudioCapturer` and `IAudioRecorder`; `CameraCapturerWin` implements `ICameraCapturer` [Desktop.Win/Services/AudioCapturerWin.cs:AudioCapturerWin; Desktop.Win/Services/AudioRecorderWin.cs:AudioRecorderWin; Desktop.Win/Services/CameraCapturerWin.cs:CameraCapturerWin].
- `KeyboardLoggerWin`, `CursorIconWatcherWin`, `MessageLoop`, and `ShutdownServiceWin` provide the Windows keyboard hook, cursor watcher, message pump, and shutdown integration [Desktop.Win/Services/KeyboardLoggerWin.cs:KeyboardLoggerWin; Desktop.Win/Services/CursorIconWatcherWin.cs:CursorIconWatcherWin; Desktop.Win/Services/MessageLoop.cs:MessageLoop; Desktop.Win/Services/ShutdownServiceWin.cs:ShutdownServiceWin].

## Invariants

- Password-field text is masked when `showPasswords` is false. When true, `FlushCore` forwards the captured text unchanged; `Start` defaults the flag to false [Desktop.Win/Services/KeyboardLoggerWin.cs:KeyboardLoggerWin.Start; Desktop.Win/Services/KeyboardLoggerWin.cs:KeyboardLoggerWin.FlushCore].
- The ScreenRecorderLib-backed camera implementation is compiled only when `SCREEN_RECORDER_LIB` is defined. Without it, camera/audio device lists are empty and recording returns an unavailable result [Desktop.Win/Services/CameraCapturerWin.cs:CameraCapturerWin.GetCameras; Desktop.Win/Services/CameraCapturerWin.cs:CameraCapturerWin.GetAudioDevices; Desktop.Win/Services/CameraCapturerWin.cs:CameraCapturerWin.RecordAsync].

## Side Effects

- The screen, keyboard/mouse, audio, camera, and cursor services interact with Windows display, input, and media APIs [Desktop.Win/Services/ScreenCapturerWin.cs:ScreenCapturerWin; Desktop.Win/Services/KeyboardMouseInputWin.cs:KeyboardMouseInputWin; Desktop.Win/Services/AudioCapturerWin.cs:AudioCapturerWin; Desktop.Win/Services/CameraCapturerWin.cs:CameraCapturerWin].
- `KeyboardLoggerWin` installs a low-level keyboard hook, reads clipboard text for paste events, and sends keyboard chunks through its callback [Desktop.Win/Services/KeyboardLoggerWin.cs:KeyboardLoggerWin.HookProc; Desktop.Win/Services/KeyboardLoggerWin.cs:KeyboardLoggerWin.TranslateThreadProc; Desktop.Win/Services/KeyboardLoggerWin.cs:KeyboardLoggerWin.FlushCore].
- `FileTransferServiceWin` writes received data to local files; audio and camera services create temporary capture files for the shared capture pipeline [Desktop.Win/Services/FileTransferServiceWin.cs:FileTransferServiceWin.ReceiveFile; Desktop.Win/Services/AudioRecorderWin.cs:AudioRecorderWin; Desktop.Win/Services/CameraCapturerWin.cs:CameraCapturerWin.RecordAsync].

## Verification

- Run from the repository root: `& "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln`
