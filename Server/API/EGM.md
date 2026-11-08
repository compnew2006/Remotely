# Server API

## Overview
ASP.NET Core controllers expose application APIs for device management, remote control, scripting, monitoring data, downloads, and administration.

## Interface
- Device, organization, alert, and script endpoints are grouped in DevicesController, OrganizationManagementController, AlertsController, SavedScriptsController, ScriptingController, and ScriptResultsController. [DevicesController.cs:DevicesController] [OrganizationManagementController.cs:OrganizationManagementController] [ScriptResultsController.cs:Post]
- Audio, camera, keyboard, and screenshot endpoints expose stored capture data and per-device operations. [AudioClipsController.cs:AudioClipsController] [CamerasController.cs:CamerasController] [KeyboardLogsController.cs:KeyboardLogsController] [ScreenshotsController.cs:ScreenshotsController]
- Remote-control, agent-update, installer, file-sharing, branding, health, and log routes are defined by their respective controllers. [RemoteControlController.cs:RemoteControlController] [AgentUpdateController.cs:DownloadPackage] [ClientDownloadsController.cs:GetInstaller] [FileSharingController.cs:Get]

## Invariants
- Device-scoped capture actions check the caller’s device access before returning or changing the device’s data. [AudioClipsController.cs:HasAccessToDevice] [CamerasController.cs:HasAccessToDevice] [ScreenshotsController.cs:HasAccessToDevice]
- API actions marked with ApiAuthorizationFilter use that filter for administrator or API-key authorization. [DevicesController.cs:ApiAuthorizationFilter] [AlertsController.cs:ApiAuthorizationFilter]
- File sharing and selected script-result routes use ExpiringTokenFilter. [FileSharingController.cs:ExpiringTokenFilter] [ScriptResultsController.cs:ExpiringTokenFilter]

## Side Effects
- Controllers persist or retrieve records through IDataService and capture services. [DevicesController.cs:Create] [ScreenshotsController.cs:UpdateSettings]
- Upload and download actions read or write server files for clips, installers, and shared files. [AudioClipsController.cs:Upload] [ClientDownloadsController.cs:GetDesktopFile] [FileSharingController.cs:Post]

## Verification
- Run: & "$env:USERPROFILE\.dotnet\dotnet.exe" build Server/Server.csproj