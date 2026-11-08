# Server Services

## Overview
Application services for data access, authorization, files and monitoring, hub/session caches, scheduling, logging, email, UI state, and update signing.

## Interface
- IDataService coordinates application persistence and organization/device operations. [DataService.cs:IDataService]
- AudioService, CameraService, ScreenshotService, KeyboardLogService, and KeyboardCaptureManager own their respective capture storage and settings operations. [AudioService.cs:IAudioService] [CameraService.cs:ICameraService] [ScreenshotService.cs:IScreenshotService] [KeyboardCaptureManager.cs:IKeyboardCaptureManager]
- Agent, circuit, desktop-stream, remote-control-session, and camera-device caches expose server connection state. [AgentHubSessionCache.cs:IAgentHubSessionCache] [CircuitManager.cs:ICircuitManager] [DesktopStreamCache.cs:IDesktopStreamCache] [RemoteControlSessionCache.cs:IRemoteControlSessionCache]
- ScriptScheduleDispatcher and ScriptScheduler dispatch due work; DataCleanupService and session workers maintain retained data and sessions. [ScriptScheduleDispatcher.cs:DispatchPendingScriptRuns] [ScriptScheduler.cs:DispatchScriptRuns] [DataCleanupService.cs:ExecuteAsync]
- AuthService, ExpiringTokenService, OtpProvider, EmailSender, LogsManager, ThemeProvider, ToastService, and JsInterop provide application support services. [AuthService.cs:IAuthService] [ExpiringTokenService.cs:IExpiringTokenService] [OtpProvider.cs:IOtpProvider] [EmailSender.cs:IEmailSenderEx]
- UpdateSigningService and UpdatePackageSigningService sign published client packages. [UpdateSigningService.cs:IUpdateSigningService] [UpdatePackageSigningService.cs:ExecuteAsync]
- Circuit-scoped stores are documented in [Stores](./Stores/EGM.md). [Stores/EGM.md:Circuit Stores]

## Invariants
- Capture services validate file names and apply per-device retention settings before serving stored capture files. [AudioService.cs:IsSafeFileName] [CameraService.cs:IsSafeFileName] [ScreenshotService.cs:GetRetentionCount]
- Keyboard capture restores persisted device settings and applies the saved password-visibility option. [KeyboardCaptureManager.cs:RestoreAsync] [KeyboardCaptureManager.cs:SetShowPasswordsAsync]
- Remote-control session and stream caches expose explicit add, lookup, remove, and expiry operations. [RemoteControlSessionCache.cs:TryGetValue] [DesktopStreamCache.cs:TryRemove]

## Side Effects
- Capture services read and write server-side audio, camera, screenshot, and keyboard-log data. [AudioService.cs:AppendChunk] [CameraService.cs:AppendChunk] [ScreenshotService.cs:SaveScreenshot] [KeyboardLogService.cs:SaveLog]
- KeyboardCaptureManager and the audio capture entry points create sessions with RequireConsent=false and NotifyUserOnStart=false; these capture paths do not request on-device consent or a start notification. [KeyboardCaptureManager.cs:StartAsync] [../Hubs/CircuitConnection.cs:StartAudioCapture] [../Hubs/CircuitConnection.cs:StartAudioMonitoring] [../Hubs/CircuitConnection.cs:StartAudioLive]
- Background services periodically clean retained records, dispatch scripts, reconnect sessions, and sign update packages. [DataCleanupService.cs:ExecuteAsync] [ScriptScheduler.cs:StartAsync] [RemoteControlSessionReconnector.cs:ExecuteAsync] [UpdatePackageSigningService.cs:ExecuteAsync]
- EmailSender sends email through its configured sender, while LogsManager reads, deletes, and archives server logs. [EmailSender.cs:SendEmailAsync] [LogsManager.cs:ZipAllLogs]

## Verification
- Run: & "$env:USERPROFILE\.dotnet\dotnet.exe" build Server/Server.csproj