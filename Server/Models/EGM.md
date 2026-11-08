# Server Models

## Overview
Server-side request, session, UI, and status models used between controllers, hubs, services, and Blazor components.

## Interface
- RemoteControlSession and RemoteControlRequest carry remote-control session state and request inputs. [RemoteControlSession.cs:RemoteControlSession] [RemoteControlRequest.cs:RemoteControlRequest]
- ApiLogin and SettingsModel represent API login and persisted server settings. [ApiLogin.cs:ApiLogin] [SettingsModel.cs:SettingsModel]
- AudioMonitoringStatus, CameraMonitoringStatus, DirectoryListing, RemoteProcessInfo, and StreamSignaler represent monitoring, filesystem, process, and stream state. [AudioMonitoringStatus.cs:AudioMonitoringStatus] [CameraMonitoringStatus.cs:CameraMonitoringStatus] [DirectoryListing.cs:DirectoryListing]
- Toast, ModalButton, ColorPickerModel, ViewerPageTheme, and RemoteControlMode support server UI boundaries. [Toast.cs:Toast] [ModalButton.cs:ModalButton] [RemoteControlMode.cs:RemoteControlMode]
- Message DTOs are documented in [Messages](./Messages/EGM.md). [Messages/EGM.md:Server Message Models]

## Invariants
- RemoteControlSession.CreateNew produces a session copy and the readiness wait methods expose cancellation-aware readiness. [RemoteControlSession.cs:CreateNew] [RemoteControlSession.cs:WaitForSessionReady]
- StreamSignaler owns a stream signal lifecycle and is disposable. [StreamSignaler.cs:Dispose]

## Side Effects
- Disposing RemoteControlSession and StreamSignaler releases their associated synchronization resources. [RemoteControlSession.cs:Dispose] [StreamSignaler.cs:Dispose]

## Verification
- Run: & "$env:USERPROFILE\.dotnet\dotnet.exe" build Server/Server.csproj