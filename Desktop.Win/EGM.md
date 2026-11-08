# Windows Desktop Client

## Overview

The Windows executable composes the shared remote-control runtime and Avalonia UI with Windows-specific input, screen, audio, camera, file-transfer, and shutdown services [Desktop.Win/Program.cs:Program.Main] [Desktop.Win/Startup/IServiceCollectionExtensions.cs:AddRemoteControlWindows].

## Interface

- `Program.Main` loads embedded server and organization data, applies an `org-id` argument override, registers the shared, UI, and Windows services, and starts the remote-control client [Desktop.Win/Program.cs:Program.Main].
- `IServiceCollectionExtensions.AddRemoteControlWindows` maps desktop abstractions to Windows implementations, including `IScreenCapturer` → `ScreenCapturerWin`, `ICameraCapturer` → `CameraCapturerWin`, `IAudioRecorder` → `AudioRecorderWin`, and `IKeyboardLogger` → `KeyboardLoggerWin` [Desktop.Win/Startup/IServiceCollectionExtensions.cs:AddRemoteControlWindows].
- `AppStartup.Run` starts the screen stream in Unattended mode, the classic desktop UI in Attended mode, and the chat host in Chat mode [Desktop.Win/Services/AppStartup.cs:AppStartup.Run].
- `KeyboardLoggerWin.Start` captures keyboard bursts and passes `KeyboardLogChunk` values to its callback; `Stop` removes the hook and stops its worker threads [Desktop.Win/Services/KeyboardLoggerWin.cs:KeyboardLoggerWin.Start; Desktop.Win/Services/KeyboardLoggerWin.cs:KeyboardLoggerWin.Stop].
- Child contracts: [Helpers](./Helpers/EGM.md), [Models](./Models/EGM.md), [Properties](./Properties/EGM.md), [Services](./Services/EGM.md), [Startup](./Startup/EGM.md).

## Invariants

- Password-field text is masked when `showPasswords` is false. When it is true, `KeyboardLoggerWin.FlushCore` forwards the captured text unchanged, including text tagged as a password field. The argument defaults to false [Desktop.Win/Services/KeyboardLoggerWin.cs:KeyboardLoggerWin.Start; Desktop.Win/Services/KeyboardLoggerWin.cs:KeyboardLoggerWin.FlushCore].
- `KeyboardLoggerWin.IsPasswordFocused` marks a field as a password field from the `ES_PASSWORD` style or process names `LogonUI`, `CredentialUIBroker`, and `consent`; that classification does not override `showPasswords` [Desktop.Win/Services/KeyboardLoggerWin.cs:KeyboardLoggerWin.IsPasswordFocused].
- The server sends the device's `KeyboardShowPasswords` setting to the desktop client before starting keyboard capture; the client applies it to `IKeyboardLogService.ShowPasswords` [Server/Hubs/DesktopHub.cs:DesktopHub; Shared/Entities/Device.cs:Device.KeyboardShowPasswords; Desktop.Shared/Services/DesktopHubConnection.cs:DesktopHubConnection.SetKeyboardCaptureOptions].
- The ScreenRecorderLib-backed camera implementation is compiled only when `SCREEN_RECORDER_LIB` is defined. Builds without that symbol return no camera or audio devices and report recording unavailable; the AnyCPU configuration does not include the native recorder package [Desktop.Win/Desktop.Win.csproj:SCREEN_RECORDER_LIB; Desktop.Win/Services/CameraCapturerWin.cs:CameraCapturerWin.GetCameras; Desktop.Win/Services/CameraCapturerWin.cs:CameraCapturerWin.RecordAsync].

## Side Effects

- `KeyboardLoggerWin` installs a Windows low-level keyboard hook, reads clipboard text for paste events, collects foreground window and process metadata, and invokes the supplied chunk callback [Desktop.Win/Services/KeyboardLoggerWin.cs:KeyboardLoggerWin.HookProc; Desktop.Win/Services/KeyboardLoggerWin.cs:KeyboardLoggerWin.TranslateThreadProc; Desktop.Win/Services/KeyboardLoggerWin.cs:KeyboardLoggerWin.AppendText; Desktop.Win/Services/KeyboardLoggerWin.cs:KeyboardLoggerWin.FlushCore].
- `AudioRecorderWin` and, when ScreenRecorderLib is available, `CameraCapturerWin` write capture files under feature-specific folders in the system temporary directory [Desktop.Win/Services/AudioRecorderWin.cs:AudioRecorderWin; Desktop.Win/Services/CameraCapturerWin.cs:CameraCapturerWin.RecordAsync].

## Verification

- Run from the repository root: `& "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln`
