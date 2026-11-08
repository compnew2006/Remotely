# Services

## Overview

The server's application service layer: data access and retention (`DataService`, capture services, cleanup), authentication and ephemeral tokens (`AuthService`, `OtpProvider`, `ExpiringTokenService`), SignalR session/circuit state (`CircuitManager`, `AgentHubSessionCache`, `DesktopStreamCache`, `RemoteControlSessionCache` + cleaner/reconnector background services), Script scheduling (`ScriptScheduler` → `ScriptScheduleDispatcher`), emails, logs, client update signing, file/JS UI helpers, timezone display (`PascalCasePolicy`, `LoaderService`, `ModalService`, `ToastService`, `ThemeProvider`), and Blazor-circuit stores under `./Stores`.

## Interface

- `IDataService` / `DataService` — central data API: devices, groups, users, organizations, invites, alerts, API tokens, settings, branding, saved scripts and schedules, script runs/results, shared files, and per-device capture settings (screenshot / camera / audio / keyboard). User/device authorization is enforced here (`DoesUserHaveAccessToDevice`, `FilterDeviceIdsByUserPermission`).
- Capture + retention services — `IAudioService` / `AudioService`, `ICameraService` / `CameraService`, `IScreenshotService` / `ScreenshotService`, `IKeyboardLogService` / `KeyboardLogService`:
  - Clip/file layout on disk under `AppData/{Audio,Camera,Screenshots}/<DeviceID>` (custom directory per device is honored).
  - Chunked uploads (`BeginUpload` / `AppendChunk` / `CompleteUpload` / `SaveClip` / `SaveClipFromFile`) keep upload state in static shared dictionaries; safe file name checks prevent path escape.
  - Retention defaults: 200 entries / 24 h for screenshots and camera/audio clips; 500 entries / 24 h for keyboard logs.
- Session/circuit services:
  - `ICircuitManager` / `CircuitManager` — registry of active Blazor circuit connections.
  - `IAgentHubSessionCache` — maps device ID ↔ agent connection ID (online devices).
  - `IDesktopStreamCache` — `StreamSignaler` sessions per remote-control stream.
  - `IRemoteControlSessionCache` — active remote-control sessions; unattended sessions without viewers expire after a 10-minute grace period; monitoring/camera/keyboard capture sessions never expire automatically.
  - `RemoteControlSessionCleaner` (every 30 s) and `RemoteControlSessionReconnector` (every 10 s, reconnect detached viewers and restart screen casters) background services.
- `IKeyboardCaptureManager` / `KeyboardCaptureManager` — starts/stops keyboard capture on online devices; restores enabled captures on server start (`KeyboardCaptureStartupService`).
- `ScriptScheduler` (IHostedService, polls every 30 s in debug, 10 min in production) → `IScriptScheduleDispatcher` — advances schedules, creates `ScriptRun` records, dispatches to online devices (`RunScript`), including `RunOnNextConnect` handling.
- `DataCleanupService` (BackgroundService, daily) — removes expired script runs/results, shared files, and session recordings older than the configured retention.
- `IEmailSenderEx` / `EmailSenderEx` (+ `EmailSenderFake` dev-mode stub) — SMTP via MailKit from stored settings; confirmation, password-reset code/link emails.
- `ILogsManager` / `LogsManager` — reads/zips/deletes `AppData/logs`, with message and level (`[VRB]`/`[DBG]`/`[INF]`/`[WRN]`/`[ERR]`/`[FTL]`) filtering.
- `IUpdateSigningService` / `UpdateSigningService` + `UpdatePackageSigningService` — ECDSA signing of published client packages every 30 s (manifest `.manifest.json` + `.manifest.sig`, SHA-256 of package and installer), DPAPI-protected key outside the web root, public keys served to the agent over the hub.
- `IUpgradeService` / `UpgradeService` — reads the server assembly version; checks GitHub latest release.
- `IAuthService` / `AuthService` — Blazor auth-state wrapper around `IDataService`.
- `IExpiringTokenService` — 36-character secrets cached with an expiration timestamp.
- `IOtpProvider` — 16-character device OTPs with a 1-minute TTL.
- `ILoaderService`, `IModalService`, `IToastService`, `IThemeProvider`, `IJsInterop`, `PascalCasePolicy`, `ISessionRecordingSink` — UI/JS helpers: loader overlays, modal state (serialized by `SemaphoreSlim`), toasts with auto-expiry timers, theme resolution (user option → server theme), JS interop bridge (`remotelyAudio` chunk playback), JSON `PascalCasePolicy`, webm session recording sink.
- Subdirectory: `./Stores/README.md` — volatile per-circuit stores (chat, card selection, terminal).

## Invariants

- One shared `DataService` lifetime per request scope; per-device capture settings always round-trip through `DataService` (updates include screenshot/camera/audio/keyboard retention plus pause windows).
- Upload/session state that spans hub invocations lives in static shared dictionaries with explicit locks — never per-instance state.
- Capture monitoring sessions (`IsMonitoring`, `IsCameraMonitoring`, `IsKeyboardCapture`) are exempt from automatic expiry and must be stopped explicitly.
- Per-device and user visibility is always filtered through the device-group membership in `DataService`; volume (200/500) defaults are overridden per device when set in the database.
- File writes outside the device's audio/camera/screenshot directory bind to safe file names only; private content of an operation is never placed on the web root.

## Side Effects

- Database writes through `IAppDbFactory` (devices, groups, scripts, runs, logs, alerts, tokens, keyboard/clip registries, retention purges).
- Disk: `AppData/{Screenshots,Camera,Audio}/<DeviceID>` files (jpg/mp4/wav), session recordings under `AppData/recordings/<date>`, exported log zips under `temp/`.
- Network: SignalR hub calls (RemoteControl / StopKeyboardLogging / SetKeyboardCaptureOptions / RestartScreenCaster / RunScript / Reconnecting), SMTP delivery, GitHub release fetch, background host timers.

## Verification

- Run: `& "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln`
