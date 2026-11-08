# Linux Desktop Services

## Overview

Linux implementations for startup, screen capture, X11 input, file transfer, cursor reporting, and shutdown.

## Interface

- AppStartup implements mode-specific client startup and unattended hub registration [Desktop.Linux/Services/AppStartup.cs:AppStartup.Run].
- ScreenCapturerLinux implements IScreenCapturer using X11 monitor and image APIs [Desktop.Linux/Services/ScreenCapturerLinux.cs:ScreenCapturerLinux.GetNextFrame].
- KeyboardMouseInputLinux implements IKeyboardMouseInput using XTest [Desktop.Linux/Services/KeyboardMouseInputLinux.cs:KeyboardMouseInputLinux.SendKeyDown].
- FileTransferServiceLinux implements IFileTransferService [Desktop.Linux/Services/FileTransferServiceLinux.cs:FileTransferServiceLinux.ReceiveFile].
- AudioCapturerLinux and CursorIconWatcherLinux provide platform implementations [Desktop.Linux/Services/AudioCapturerLinux.cs:AudioCapturerLinux], [Desktop.Linux/Services/CursorIconWatcherLinux.cs:CursorIconWatcherLinux].
- ShutdownServiceLinux implements IShutdownService [Desktop.Linux/Services/ShutdownServiceLinux.cs:ShutdownServiceLinux.Shutdown].

## Invariants

- ScreenCapturerLinux.GetFullFrame delegates to GetNextFrame, which captures an X11 frame even when the display is static [Desktop.Linux/Services/ScreenCapturerLinux.cs:ScreenCapturerLinux.GetFullFrame].
- AudioCapturerLinux.ToggleAudio and KeyboardMouseInputLinux.ToggleBlockInput are explicit no-op implementations [Desktop.Linux/Services/AudioCapturerLinux.cs:AudioCapturerLinux.ToggleAudio].
- FileTransferServiceLinux serializes partial-file writes with _writeLock and removes the message entry when endOfFile is true [Desktop.Linux/Services/FileTransferServiceLinux.cs:FileTransferServiceLinux.ReceiveFile].

## Side Effects

- ScreenCapturerLinux opens and closes an X display and reads monitor pixels with XGetImage [Desktop.Linux/Services/ScreenCapturerLinux.cs:ScreenCapturerLinux.GetX11Capture].
- KeyboardMouseInputLinux injects X11 key and mouse events and synchronizes the display [Desktop.Linux/Services/KeyboardMouseInputLinux.cs:KeyboardMouseInputLinux.SendMouseMove].
- FileTransferServiceLinux writes received files to the desktop directory or a temporary RemoteControl directory [Desktop.Linux/Services/FileTransferServiceLinux.cs:FileTransferServiceLinux.GetBaseDirectory].
- ShutdownServiceLinux disconnects viewers when possible, then shuts down the UI dispatcher [Desktop.Linux/Services/ShutdownServiceLinux.cs:ShutdownServiceLinux.Shutdown].

## Verification

- Run from the repository root: & "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln
