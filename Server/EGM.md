# Server Component Package

## Overview
ASP.NET Core and Blazor server host for Remotely APIs, Identity, SignalR hubs, persistent data, background services, and the browser viewer.

## Interface
- Program configures the host, dependency injection, middleware, API routes, Blazor routes, and the Agent, Desktop, and Viewer hubs. [Program.cs:WebApplication.CreateBuilder] [Program.cs:MapHub]
- ApplicationOptions selects the database provider and startup migration policy. [Options/ApplicationOptions.cs:ApplicationOptions]
- Server APIs, authorization, data, hubs, components, services, and viewer assets are documented in [API](./API/EGM.md), [Auth](./Auth/EGM.md), [Data](./Data/EGM.md), [Hubs](./Hubs/EGM.md), [Components](./Components/EGM.md), [Services](./Services/EGM.md), and [wwwroot](./wwwroot/EGM.md). [Program.cs:MapControllers]

## Invariants
- Supported database providers are SQLite, SQL Server, and PostgreSQL, selected during service registration. [Program.cs:AddDbContext]
- Startup applies pending database migrations by default; disabling AllowDbAutoMigration makes startup reject pending migrations. [Program.cs:AllowDbAutoMigration] [Options/ApplicationOptions.cs:AllowDbAutoMigration]
- SignalR accepts messages up to 100,000 bytes and uses a two-minute client timeout. [Program.cs:MaximumReceiveMessageSize] [Program.cs:ClientTimeoutInterval]
- Viewer control is checked against the viewer’s own authority before control messages are forwarded. [Hubs/ViewerHub.cs:ViewerMayControl] [Hubs/ViewerHub.cs:SendDtoToClient]

## Side Effects
- Startup configures and migrates the selected database, registers background services, and maps HTTP, SignalR, and Blazor routes. [Program.cs:Database.Migrate] [Program.cs:AddHostedService] [Program.cs:MapHub]
- The host reads forwarded-header, CORS, logging, and application settings from configuration. [Program.cs:ForwardedHeadersOptions] [Program.cs:TrustedOriginPolicy]
- Audio clip, scheduled audio, live audio, and keyboard capture sessions are started without an on-device consent prompt or start notification. [Hubs/CircuitConnection.cs:StartAudioCapture] [Hubs/CircuitConnection.cs:StartAudioMonitoring] [Hubs/CircuitConnection.cs:StartAudioLive] [Services/KeyboardCaptureManager.cs:StartAsync]
- Password-field text is captured only when the persisted KeyboardShowPasswords option is true; the hub sends that option to the desktop client before keyboard logging starts. [Services/DataService.cs:UpdateKeyboardSettings] [Hubs/DesktopHub.cs:IsKeyboardPasswordVisible] [Hubs/DesktopHub.cs:SetKeyboardCaptureOptions]
- Keyboard capture starts automatically for an online device with KeyboardEnabled and is restored for enabled devices at server startup. [Hubs/AgentHub.cs:DeviceCameOnline] [Services/KeyboardCaptureManager.cs:RestoreAsync]
- Hub calls relay control, monitoring, scripting, chat, and file-transfer messages between viewers, desktop clients, and agents. [Hubs/ViewerHub.cs:SendDtoToClient] [Hubs/AgentHub.cs:CheckForPendingScriptRuns] [Hubs/DesktopHub.cs:SendDesktopStream]

## Verification
- Run: & "$env:USERPROFILE\.dotnet\dotnet.exe" build Server/Server.csproj