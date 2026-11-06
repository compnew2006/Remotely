<div align="center">
  <img src="Assets/Remotely_Icon.png" alt="Remotely" width="80" height="80">

  <h1>Remotely</h1>

  <p><strong>Self-hosted remote control, remote scripting, and screen monitoring.</strong><br>
  The operator works in a browser. The device runs a background agent.</p>

  <p>
    <a href="https://github.com/compnew2006/Remotely/actions/workflows/ci.yml"><img alt="CI" src="https://github.com/compnew2006/Remotely/actions/workflows/ci.yml/badge.svg?branch=master"></a>
    <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white">
    <img alt="Windows and Linux" src="https://img.shields.io/badge/platforms-Windows%20%7C%20Linux-0078D6">
    <a href="LICENSE"><img alt="License: GPL-3.0" src="https://img.shields.io/badge/license-GPL--3.0-blue.svg"></a>
  </p>

  <p>
    <a href="#quick-start">Quick start</a> ·
    <a href="#fork-additions">Fork additions</a> ·
    <a href="#configuration">Configuration</a> ·
    <a href="#api-and-integrations">API</a> ·
    <a href="#build-from-source">Build from source</a>
  </p>
</div>

> [!IMPORTANT]
> **This is the Arkan LAN deployment fork of [immense/Remotely](https://github.com/immense/Remotely).** It tracks upstream `master` and adds the work described in [Fork additions](#fork-additions). Everything else in this document describes the upstream product and applies here unchanged. Upstream support channels, Docker images, and tutorials still target the original project.

## Why Remotely

Remotely gives an IT team one place to reach the machines it manages. Install the agent once. From then on, any permitted operator can open a browser and work on that device without installing a remote desktop client.

The device connects out to your server over SignalR, so you do not open inbound ports on managed machines. The server is yours: its database, logs, recordings, users, and device inventory stay on your infrastructure.

| Job | What you get |
|---|---|
| Observe | Live screen, multiple monitors with monitor switching, session metrics (FPS, throughput, latency), and optional session recording |
| Control | Mouse and keyboard input, `Ctrl+Alt+Del`, Windows session switching, and clipboard transfer in both directions |
| Hear | Audio streaming from the device (Windows) |
| Transfer | File upload and download between operator and device |
| Automate | Remote script execution in the browser console, saved scripts, and a remote task manager |
| Talk | Per-device chat and shareable view links for staff who only need to watch |
| Govern | Attended and unattended sessions, device groups, organization scoping, API keys, 2FA, device bans, retention limits, and server-side branding |
| Integrate | A REST API with Swagger and an alerts endpoint for basic RMM-style monitoring |

The server is ASP.NET Core with a Blazor Server console. Screen frames travel as JPEG over a SignalR WebSocket and are painted onto a canvas in the browser. Input, clipboard, audio, file transfer, and script execution use the same relay. Desktop clients are Avalonia applications for Windows and Linux.

## Fork additions

These changes were added for the Arkan LAN deployment (2026-11-06). The device-side actions appear in the **Actions** menu on a device card in the dashboard.

| Addition | What it does | Status |
|---|---|---|
| **Remote task manager** | Lists the processes on a device with name, PID, session, memory, CPU, and start time, and ends a selected process. It uses the existing script channel, so already-installed agents work without an update. | Available |
| **Scheduled screenshots** | Captures the device desktop at a fixed interval (1 to 30 minutes) with nobody watching. Per-device settings cover the server storage folder, how many captures are kept, how long they are kept, and exception windows (several per device, including windows that cross midnight). Includes capture now, delete one, and clear all. | Available |
| **Camera capture** | Records an MP4 clip from a chosen device camera at one of three quality levels, with a bounded duration, an optional recurring schedule, retention limits, and browser playback. | Preview |
| **Audio capture and live listening** | Records a WAV clip from system audio (loopback) or a chosen microphone at one of three quality levels, with an optional recurring schedule, retention limits, playback, and live listening while the dialog is open. | Preview |
| **Security hardening** | Server-enforced View Only authorization with a per-viewer control gate, safe file path handling, signed and verified client updates, authenticated local IPC, protected API endpoints, and remediation of the CVE-2026-39959 dependency advisory. | Available |
| **.NET 10 LTS** | The solution targets `net10.0`; the SDK is pinned to `10.0.403` in `global.json`; LibMan was replaced with npm and a lockfile. | Available |

> [!NOTE]
> **Preview** means the feature is implemented and covered by unit tests, but it has not yet been exercised on hardware. Camera capture has not run on a device with a real camera, and audio capture has not run on a device with a real sound device. Each feature records its own acceptance status in [specs/003-camera-capture/spec.md](specs/003-camera-capture/spec.md) and [specs/004-audio-capture/spec.md](specs/004-audio-capture/spec.md).

> [!WARNING]
> The desktop client assembly version is stamped with the build time. When you publish a client update, publish it with the same version number that is already installed on the device.

## Quick start

### Option 1: Run the upstream container

The compose file deploys the upstream `immybot/remotely:latest` image. It gives you the core product, not the fork additions.

```bash
mkdir -p /var/www/remotely
cd /var/www/remotely
curl -fsSL -O https://raw.githubusercontent.com/compnew2006/Remotely/master/docker-compose/docker-compose.yml
docker compose up -d
```

On installations that still use the standalone binary, run `docker-compose up -d` instead.

Open `http://<server>:5000` and click **Register** to create the first account. That account is both the server administrator and the organization administrator, and an organization is created for it automatically. Registration closes after the first account; raise `MaxOrganizationCount` or set it to `-1` to allow more.

### Option 2: Build this fork

Use this path when you need the fork additions. You need the .NET 10 SDK, Node.js LTS, and Git. The full solution, including the Windows desktop client, builds on Windows; the server builds on Linux and macOS.

```bash
git clone --recurse-submodules https://github.com/compnew2006/Remotely.git
cd Remotely
dotnet build Server/Server.csproj -c Release
dotnet run --project Server/Server.csproj
```

The development profile listens on `https://localhost:5001` and `http://localhost:5000`. In development, connecting agents are assigned to the first organization, and the debug agent uses a predefined device ID against `https://localhost:5001`, so you can run both sides and see the device in the list.

To publish a server you can run without the SDK:

```bash
dotnet publish Server/Server.csproj -c Release -r linux-x64 --self-contained -o Server/bin/publish
cd Server/bin/publish
ASPNETCORE_HTTP_PORTS=5000 dotnet Remotely_Server.dll
```

> [!NOTE]
> `Server/Dockerfile` still pins the .NET 8 ASP.NET runtime image. Update its `FROM` line to `mcr.microsoft.com/dotnet/aspnet:10.0` before building a container from this fork. The upstream published image is unaffected.

## Configuration

Database provider, connection strings, and the ASP.NET Core port come from environment variables in `docker-compose.yml`. Everything else is set on the **Server Config** page after you sign in.

| Setting | What it controls |
|---|---|
| `AllowApiLogin` | Whether the API controller accepts a username and password. API access tokens are preferred. |
| `BannedDevices` | Device IDs, names, or IP addresses that are refused. A banned device receives an uninstall command when it connects. |
| `DataRetentionInDays` | How long logs and other server data are kept. `-1` keeps data indefinitely and is not recommended. |
| `DBProvider` | Selects SQLite, SQLServer, or PostgreSQL, and with it the matching connection string. |
| `EnableWindowsEventLog` | Mirrors server log entries into the Windows Event Log. |
| `EnforceAttendedAccess` | Prompts the person at the device before an unattended remote control session starts. |
| `EnableRemoteControlRecording` | Saves remote control session recordings under `/app/AppData/recordings`, subject to `DataRetentionInDays`. |
| `ForceClientHTTPS` | Makes installers and desktop clients use HTTPS even when forwarded headers are misconfigured. |
| `KnownProxies` | Reverse proxy and firewall addresses whose forwarded headers are trusted. |
| `MaxOrganizationCount` | How many organizations may exist. The default is one; `-1` removes the limit. |
| `RedirectToHttps` | Redirects HTTP traffic to HTTPS inside ASP.NET Core, independently of Caddy or IIS. |
| `RemoteControlNotifyUsers` | Shows a notification at the device when an unattended session starts. |
| `RemoteControlRequiresAuthentication` | Requires an authenticated user before the remote control page can connect. |
| `Require2FA` | Requires users to configure two-factor authentication before using the app. |
| SMTP settings | Host, port, credentials, and sender used for registration and password reset mail. |
| `Theme` | Site theme, `Light` or `Dark`. Users can override it in **Account > Options**. |
| `TrustedCorsOrigins` | Origins allowed to call the API from browser JavaScript. Authentication is still required. |
| `UseHsts` | Sends HTTP Strict Transport Security headers. |
| `UseHttpLogging` | Logs every request and response, including headers. It also enables extra client download log entries for the effective scheme, host, and remote IP. Set a log level for `Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware` for the output to appear. |

### Database

SQLite is the default and needs no server. On first run the server creates the database file named in the SQLite connection string. Set `DBProvider` to `SQLServer` or `PostgreSQL` to use a different database, then supply the matching connection string.

### Data, logs, and retention

In the container, application data lives under `/app/AppData`, mounted at `/var/www/remotely` on the host. That directory holds `Remotely.db`, server logs, and session recordings, and it survives container teardown and recreation. If you migrate from a non-container installation, copy your existing database file into that directory.

Client logs are written to `%ProgramData%\Remotely\Logs` on Windows. Server logs go to `/app/AppData/logs` in the container and to stdout. On IIS you can set `stdoutLogEnabled` in `web.config` to capture stdout in a file. Log levels and sinks are configured in `appsettings.json`.

## HTTPS and reverse proxies

Caddy is the only supported reverse proxy, and only when it faces the internet directly. The default Caddy configuration provides what ASP.NET Core and SignalR need.

If you use anything else, including an extra firewall or Nginx, the upstream maintainers cannot support every possible topology. Ask in the [upstream Discussions](https://github.com/immense/Remotely/discussions) or the [Remotely subreddit](https://www.reddit.com/r/remotely_app/).

When Remotely runs behind a proxy, ASP.NET Core needs three headers:

- `X-Forwarded-Proto`: the original scheme
- `X-Forwarded-Host`: the original host
- `X-Forwarded-For`: the original client IP

The scheme and host are injected into installers and desktop clients so they know where to connect. The client IP appears in device information. Remotely does not parse these headers itself; ASP.NET Core's forwarded headers middleware does. If they do not appear as expected, the header is missing, malformed, or arrived from a proxy that is not in `KnownProxies`.

ASP.NET Core accepts forwarded headers only from loopback addresses by default. The compose file adds the Docker gateway `172.28.0.1`. For any other topology, add every firewall and reverse proxy address to `KnownProxies`. If you cannot get the proxy configuration right, set **Force Client HTTPS** in Server Config to at least force the HTTPS scheme.

See [Microsoft's forwarded headers documentation](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer) for the underlying behavior.

## API and integrations

The API is browsable at `https://<your-server>/swagger`. Create a token in **Account > API Access** and send its ID and secret in the `X-Api-Key` header, separated by a colon:

```http
X-Api-Key: <api-key-id>:<api-key-secret>
```

Cross-origin browser requests require the calling origin in `TrustedCorsOrigins`. Listing an origin does not grant authentication.

The alerts endpoint supports basic monitoring: a script on the device posts an alert, and Remotely can show a notification, send email, or call another API. A PowerShell scheduled job can then watch disk space, services, or anything else the device can report.

<details>
<summary>Example: daily disk space alert with a PowerShell scheduled job</summary>

```powershell
$Trigger = New-JobTrigger -Daily -At "5 AM"
$Option = New-ScheduledJobOption -RequireNetwork

Register-ScheduledJob -ScriptBlock {
    $OsDrive = Get-PSDrive -Name C
    $FreeSpace = $OsDrive.Free / ($OsDrive.Used + $OsDrive.Free)
    if ($FreeSpace -lt .1) {
        Invoke-WebRequest -Uri "https://localhost:5001/api/Alerts/Create/" -Method Post -Headers @{
            "X-Api-Key" = "<api-key-id>:<api-key-secret>"
        } -Body @"
        {
            "AlertDeviceID": "<device-id>",
            "AlertMessage": "Low hard drive space. Free space: $([Math]::Round($FreeSpace * 100))%",
            "EmailBody": "Low hard drive space on this device.",
            "EmailSubject": "Hard drive space alert",
            "EmailTo": "ops@example.com",
            "ShouldAlert": true,
            "ShouldEmail": true,
            "ShouldSendApiRequest": false
        }
"@ -ContentType "application/json"
    }
} -Name "Check OS drive space" -Trigger $Trigger -ScheduledJobOption $Option
```
</details>

<details>
<summary>Example: cookie-based login and viewer launch</summary>

```javascript
// Log in with one request, then launch the viewer with another.
fetch("https://localhost:5001/api/Login/", {
  method: "post",
  credentials: "include",
  mode: "cors",
  body: JSON.stringify({ email: "email@example.com", password: "P@ssword1" }),
  headers: { "Content-Type": "application/json" },
}).then((response) => {
  if (!response.ok) return;
  fetch("https://localhost:5001/api/RemoteControl/Viewer/<device-id>", {
    method: "get",
    credentials: "include",
    mode: "cors",
  }).then((viewer) => {
    if (viewer.ok) viewer.text().then((url) => window.open(url));
  });
});
```
</details>

## Build from source

### Prerequisites

| Requirement | Details |
|---|---|
| .NET SDK | 10.0.403 or a compatible 10.0 feature band; `global.json` pins the version |
| Node.js | LTS release, used to build the browser assets |
| Git | Clone with `--recurse-submodules` |
| Windows client | Windows 11 with the .NET desktop development workload |
| Linux client | Ubuntu LTS with `libx11-dev`, `libxrandr-dev`, `libc6-dev`, `libxtst-dev`, and `xclip` to build the quick support client |

### Full solution on Windows

Install Visual Studio 2026, or use the .NET 10 SDK with the CLI, and select the **ASP.NET and web development** and **.NET desktop development** workloads.

```bash
git clone --recurse-submodules https://github.com/compnew2006/Remotely.git
cd Remotely
dotnet build Remotely.sln -c Release
dotnet test Remotely.sln -c Release
```

### Deployment models

Framework-dependent deployments require the matching .NET runtime on each target machine and produce smaller artifacts. Self-contained deployments bundle the runtime and need nothing installed, at the cost of size. Choose a runtime identifier with the [.NET RID catalog](https://learn.microsoft.com/en-us/dotnet/core/rid-catalog).

### Repository layout

| Path | Purpose |
|---|---|
| `Agent/` | Background agent that connects a device to the server |
| `Server/` | ASP.NET Core host: Blazor console, browser viewer, API, SignalR hubs |
| `Shared/` | Models and services shared by server, agent, and clients |
| `Desktop.Native/`, `Desktop.Win/`, `Desktop.Linux/`, `Desktop.Shared/`, `Desktop.UI/` | Avalonia desktop clients and their shared UI |
| `Tests/` | Unit and integration tests |
| `Utilities/` | Build, packaging, and installer helpers |
| `specs/` | Feature specifications for the fork additions |
| `docker-compose/` | Container deployment |
| `AGENTS.md`, `DECISION-LOG.md`, `DESIGN.md`, `PRODUCT.md`, `EVIDENCE-MAP.md`, `GATE-REGISTER.md` | Project rules, decisions, design context, and evidence records |

## Operating notes

- **Mobile remote control**: single tap for left click, long press then release for right click, long press then drag for click and drag.
- **Console shortcuts**: `/` switches shells (names are set in Options), Up and Down cycle input history, `Ctrl+Q` clears the output.
- **End user support page**: `/get-support` lets a user request help, which raises an alert above the device grid. The installer's `-supportshortcut` switch places a shortcut on the desktop.
- **Branding**: **Account > Branding** changes the logo, name, and links used by quick support clients and the Windows installer. Clients built from source retrieve branding only when the server URL is compiled into them.
- **Port configuration**: change the host port on the left side of the mapping in `docker-compose.yml`. Leave the container port unchanged.

## Security

Remotely is designed to sit on infrastructure you control, not on a shared service. Treat the server as an administrative plane.

- Terminate TLS at Caddy and keep the console off the public internet unless you need it there.
- Create the first account immediately after deployment, then enable `Require2FA` and keep self-registration closed.
- Use API tokens instead of `AllowApiLogin`, scope tokens to the work they do, and rotate them.
- Keep `KnownProxies` limited to the proxies you actually run.
- Treat script execution, file transfer, and unattended access as privileged operations. Grant dashboard access accordingly.
- Client updates in this fork are signed and verified, and local IPC requires authentication. Keep devices on current client builds.

Report a suspected vulnerability to the maintainer privately. Do not open a public pull request or issue with exploit details.

## Contributing

This repository is maintained for a specific deployment. General product changes belong upstream in [immense/Remotely](https://github.com/immense/Remotely). Fork changes follow the process recorded in [AGENTS.md](AGENTS.md): a specification in `specs/`, a decision in [DECISION-LOG.md](DECISION-LOG.md), evidence in [EVIDENCE-MAP.md](EVIDENCE-MAP.md), and gate status in [GATE-REGISTER.md](GATE-REGISTER.md).

Before you open a pull request, build the affected projects and run the test suite. Keep tests with the change that needs them, and update this README when behavior, configuration, or build steps change.

## License and credits

Remotely is licensed under the [GNU General Public License v3.0](LICENSE).

It is a fork of [immense/Remotely](https://github.com/immense/Remotely), created by Immense Networks. The product name, logo, and attribution remain with the upstream project.
