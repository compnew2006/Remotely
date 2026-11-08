# Modal Content Components

## Overview
Device action dialogs for audio, camera, screenshots, keyboard logs, task management, folders, and script selection.

## Interface
- AudioModal, CameraModal, ScreenshotModal, KeyboardModal, and TaskManagerModal expose the device monitoring and capture controls. [AudioModal.razor.cs:LoadDeviceSettings] [CameraModal.razor.cs:BuildRequest] [ScreenshotModal.razor.cs:CaptureNow] [KeyboardModal.razor.cs:ToggleCapture] [TaskManagerModal.razor.cs:RefreshAsync]
- EditDeviceGroup, FolderPickerModal, and QuickScriptsSelector support group editing, folder selection, and script selection. [EditDeviceGroup.razor.cs:GroupCheckChanged] [FolderPickerModal.razor:Browse] [QuickScriptsSelector.razor:RunScriptClicked]

## Invariants
- Device-specific settings are loaded before controls are presented for the selected device. [AudioModal.razor.cs:LoadDeviceSettings] [CameraModal.razor.cs:LoadDeviceSettings]
- Modal components unsubscribe from messenger events when disposed. [AudioModal.razor.cs:Dispose] [ScreenshotModal.razor.cs:Dispose]

## Side Effects
- Capture controls invoke the circuit connection and server APIs to start, stop, and update device actions. [KeyboardModal.razor.cs:SaveSettings] [ScreenshotModal.razor.cs:StartMonitoring]
- TaskManagerModal sends a script request and parses its result into process rows. [TaskManagerModal.razor.cs:RefreshAsync] [TaskManagerModal.razor.cs:ParseProcesses]
- ScreenshotModal refreshes or deletes screenshots through the screenshots API. [ScreenshotModal.razor.cs:LoadScreenshots]

## Verification
- Run: & "$env:USERPROFILE\.dotnet\dotnet.exe" build Server/Server.csproj