# Remotely Blueprint

## System Overview

Remotely is a self-hosted remote control, remote scripting, and device monitoring system forked from immense/Remotely [README.md:Remotely]. The solution targets .NET 10 [global.json:version] [Server/Server.csproj:TargetFramework]. The ASP.NET Core host provides the Blazor console, REST endpoints, persistence, and SignalR hubs [Server/Program.cs:MapHub]. The background agent connects to the server and runs as a Windows service or systemd service [Agent/Program.cs:Main] [Agent/Program.cs:UseWindowsService]. Windows and Linux desktop applications share the Avalonia UI and desktop runtime, then register operating-system adapters [Desktop.Win/Program.cs:Program.Main] [Desktop.Linux/Program.cs:Program.Main].

The Shared project contains the domain entities, DTOs, and cross-project contracts [Shared/Entities/Device.cs:Device] [Shared/Models/KeyboardLogChunk.cs:KeyboardLogChunk] [Shared/Interfaces/IAgentHubClient.cs:IAgentHubClient]. The browser viewer source is TypeScript under Server/wwwroot/src [Server/wwwroot/src/ViewerHubConnection.ts:ViewerHubConnection]. EF Core database providers and startup migrations are selected in Server/Program.cs [Server/Program.cs:dbProvider] [Server/Program.cs:AllowDbAutoMigration]. Docker Compose and Utilities provide deployment and publishing entry points.

## Subdirectories & System Boundaries

- [Agent](./Agent/EGM.md) — background service host, SignalR connection, scripting, device registration, and update coordination.
- [Desktop.Linux](./Desktop.Linux/EGM.md) — Linux desktop entry point and platform adapters.
- [Desktop.Native](./Desktop.Native/EGM.md) — native Windows and Linux interop bindings.
- [Desktop.Shared](./Desktop.Shared/EGM.md) — cross-platform desktop runtime, abstractions, message handling, monitoring, and remote-control services.
- [Desktop.UI](./Desktop.UI/EGM.md) — Avalonia application views, view models, and UI services.
- [Desktop.Win](./Desktop.Win/EGM.md) — Windows desktop entry point and platform adapters.
- [Server](./Server/EGM.md) — ASP.NET Core host, API, hubs, Blazor components, persistence, and browser client.
- [Shared](./Shared/EGM.md) — cross-project domain entities, messages, DTOs, and services.
- [Utilities](./Utilities/EGM.md) — publishing, migration, and signing scripts.
- [Proof-of-concept projects](./poc/EGM.md) — isolated experiments and runtime probes.
- [Docker Compose configuration](./docker-compose/docker-compose.yml) — single-service server deployment and persistent AppData volume [docker-compose/docker-compose.yml:remotely].

## Global Invariants

- The repository SDK pin is 10.0.403; use the explicit user-profile SDK executable on this host because machine-level dotnet selects SDK 9 [global.json:version] [AGENTS.md:Testing instructions].
- Server startup requires ApplicationOptions:DbProvider to select SQLite, SQL Server, or PostgreSQL. Automatic migration is enabled by default; disabling it while migrations are pending stops startup [Server/Program.cs:dbProvider] [Server/Program.cs:AllowDbAutoMigration].
- Forwarded headers are accepted only from configured proxy addresses. Keep the reverse proxy list aligned with ApplicationOptions.KnownProxies [Server/Program.cs:ForwardedHeadersOptions] [Server/Models/SettingsModel.cs:KnownProxies].
- SignalR caps incoming messages at 100,000 bytes. Transfer paths must preserve their chunking limits [Server/Program.cs:MaximumReceiveMessageSize] [Shared/Helpers/DtoChunker.cs:ChunkDto].
- TypeScript under Server/wwwroot/src is the browser source of truth; keep its checked-in JavaScript and source maps in sync as described by AGENTS.md [Server/wwwroot/src/App.ts:App] [AGENTS.md:There is no npm build step].
- A successful unit-test run does not pass a release gate. GATE-REGISTER.md requires recorded [RELEASE-GATE] evidence for a PASS state [GATE-REGISTER.md:0.2].
- View-only behavior spans the server relay and desktop message handling. G1 is not release-gate verified; re-read the current viewer authorization paths before changing them [Server/Hubs/ViewerHub.cs:SendDtoToClient] [Desktop.Shared/Services/DtoMessageHandler.cs:ParseMessage] [GATE-REGISTER.md:G1].
- Keyboard password-field handling is conditional in current source. KeyboardLoggerWin masks field text when ShowPasswords is false, and the device setting can enable showing it [Desktop.Win/Services/KeyboardLoggerWin.cs:ShowPasswords] [Server/Components/ModalContents/KeyboardModal.razor.cs:ToggleShowPasswords] [Shared/Entities/Device.cs:KeyboardShowPasswords]. This differs from the unconditional-masking statement in AGENTS.md; follow current source when documenting or changing the behavior.
- Screenshot, camera/display, audio, and keyboard capture are owner-directed fork features. Audio and keyboard capture operate without employee consent or notification; keep their recorded privacy and gate status visible when changing these paths [Server/Services/KeyboardCaptureManager.cs:StartAsync] [Server/Services/AudioService.cs:AudioService] [GATE-REGISTER.md:G6].

## Global Side Effects

- Server startup reads and migrates its configured database, and server services persist operational data and captured media [Server/Program.cs:Database.MigrateAsync] [Server/Services/ScreenshotService.cs:SaveScreenshot] [Server/Services/KeyboardLogService.cs:SaveLog].
- Server and device clients exchange control, scripting, telemetry, file-transfer, capture, and update messages over HTTP and SignalR [Agent/Services/AgentHubConnection.cs:Connect] [Server/Hubs/AgentHub.cs:AgentHub].
- The agent can launch scripts and system operations on managed devices. Windows desktop integration creates interactive processes using native Windows APIs [Agent/Services/AgentHubConnection.cs:RunScript] [Desktop.Native/Windows/Win32Interop.cs:CreateInteractiveSystemProcess].
- Desktop clients access screen, keyboard, clipboard, audio, and camera/display capture sources [Desktop.Win/Services/ScreenCapturerWin.cs:ScreenCapturerWin] [Desktop.Win/Services/KeyboardLoggerWin.cs:KeyboardLoggerWin] [Desktop.Win/Services/AudioRecorderWin.cs:RecordAsync] [Desktop.Win/Services/CameraCapturerWin.cs:RecordAsync].
- Client package signing stores a private key under server AppData outside the web root; the agent retrieves trusted public keys and writes TrustedUpdateKeys.json beside its executable [Server/Services/UpdateSigningService.cs:LoadOrCreateKey] [Server/Hubs/AgentHub.cs:GetUpdatePublicKey].

## System Verification

Run from the repository root:

~~~powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" build Remotely.sln
& "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln
~~~

These commands were not run during this documentation bootstrap. See AGENTS.md and GATE-REGISTER.md for the current environment limitations and release-gate requirements.



