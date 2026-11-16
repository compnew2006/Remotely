# Running the Remotely server

How to build, run, and verify the ASP.NET Core server in this repository.

The server is the operator console: it serves the Blazor UI, the REST API, the
SignalR hubs, and the client packages that agents download.

---

## 1. Prerequisites

| Requirement | Notes |
|---|---|
| .NET SDK | `10.0.403` or a compatible 10.0 feature band. `global.json` pins the version and `rollForward` is `latestFeature`. |
| HTTPS dev certificate | Only needed for the `https://localhost:5001` endpoint. Created and trusted with `dotnet dev-certs https --trust`. |
| Git | Clone with `--recurse-submodules`. |

### Use the right `dotnet` on this machine

The machine-level `dotnet` on this host is SDK **9.0.315**, and `global.json`
asks for **10.0.403**. The machine-level command therefore fails:

```
A compatible .NET SDK was not found.
Requested SDK version: 10.0.403
Installed SDKs: 9.0.315
```

The SDK 10.0.403 install lives in the user profile. Every command below uses
the explicit path so it works regardless of `PATH`:

```powershell
$DOTNET = "$env:USERPROFILE\.dotnet\dotnet.exe"
```

Check it before anything else:

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" --version
# 10.0.403
```

CI does not have this problem: it uses `actions/setup-dotnet` with
`global-json-file: global.json`.

---

## 2. Build

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" build Server\Server.csproj
```

Expected output ends with `0 Warning(s)` and `0 Error(s)`.

Build the whole solution when you also changed the agent or the desktop clients:

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" build Remotely.sln
```

---

## 3. Run for development

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" run --project Server\Server.csproj
```

The launch profile in `Server/Properties/launchSettings.json` binds:

| URL | Purpose |
|---|---|
| `http://localhost:5000` | The console. Use this one. |
| `https://localhost:5001` | Same console over TLS. Needed by the debug agent, which connects to `https://localhost:5001`. |

Open <http://localhost:5000>, register the first account, and the console
loads.

If the HTTPS endpoint fails with a trust error, trust the certificate once:

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" dev-certs https --trust
```

### Keep the port free

**Port 5000 is the production port on this network.** The deployed server
listens on `0.0.0.0:5000`, and the address `192.168.0.2` is this machine. A
development server on port 5000 and the production server cannot both hold
the port, and stopping the wrong one takes production down.

When production is running, start the development server on other ports:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://localhost:5010;https://localhost:5011"
& "$env:USERPROFILE\.dotnet\dotnet.exe" run --no-restore --no-launch-profile --project Server\Server.csproj
```

Then open <http://localhost:5010>.

### Run against a separate database

By default the server uses SQLite and creates `Server/Remotely.db`. Override
the connection string with environment variables when you want to keep the
checked-out tree clean:

```powershell
$env:Remotely_ConnectionStrings__SQLite = "Data Source=C:\temp\remotely-dev.db"
$env:Remotely_ApplicationOptions__DbProvider = "SQLite"
& "$env:USERPROFILE\.dotnet\dotnet.exe" run --project Server\Server.csproj
```

`Server/Remotely.db`, `-shm`, and `-wal` are already in `.gitignore`, so the
default location is safe for normal development.

### Start the background shell

To keep the server running while you work on something else, start it in the
background and redirect its output:

```powershell
$log = "$env:TEMP\remotely-dev.log"
& "$env:USERPROFILE\.dotnet\dotnet.exe" run --project Server\Server.csproj *> $log
```

Stop it again by stopping the `Remotely_Server.exe` process:

```powershell
Get-Process Remotely_Server -ErrorAction SilentlyContinue | Stop-Process -Force
```

---

## 4. Verify it works

After the log line `Now listening on: http://localhost:5010`, check the
endpoints:

```powershell
foreach ($url in @(
    'http://localhost:5010/',
    'http://localhost:5010/Account/Login',
    'http://localhost:5010/css/console.css',
    'http://localhost:5010/_framework/blazor.web.js',
    'http://localhost:5010/Remotely_Server.styles.css')) {
    (Invoke-WebRequest -Uri $url -UseBasicParsing).StatusCode
}
```

All five must return `200`. The last two are the ones that break when the
server runs in Production mode from a checkout. `/api/devices` returns `401`
when you are not signed in, which is also correct.

### Do not run the server from source in Production mode

**Use the Development environment when you run the server from a checkout.**
Production mode from a checkout serves the framework and scoped-CSS assets as
`500`, because those files are generated during build and only exist in a
published output:

```
System.IO.FileNotFoundException: Could not find file
'...\Server\wwwroot\_framework\blazor.web.js'.
  at Microsoft.AspNetCore.Builder.StaticAssetDevelopmentRuntimeHandler...
```

The affected routes are the ones the build generates rather than the ones
checked into the repository:

| Route | In `Server/wwwroot`? | Published? |
|---|---|---|
| `/_framework/blazor.web.js` | no | yes |
| `/Remotely_Server.styles.css` | no | yes |
| `/Components/ModalHarness.razor.js` | no | yes |
| `/css/console.css` | yes | yes |

Everything else, including the pages themselves, still returns `200`, which
makes this look like a styling bug rather than a hosting problem. The console
renders without its stylesheet and without the Blazor script.

Two ways out:

- Run with `ASPNETCORE_ENVIRONMENT=Development` (the launch profile does
  this), which resolves those assets from the build output.
- Or publish and run the published output, as section 5 describes.

### Rebuilding invalidates a running server

**Stop the server before you rebuild.** Two things break:

1. The build fails outright while the server runs, because the executable is
   locked:

   ```
   error MSB3021: Unable to copy file "obj\Debug\net10.0\apphost.exe" to
   "bin\Debug\net10.0\Remotely_Server.exe". The process cannot access the
   file ... because it is being used by another process.
   ```

2. A build also rewrites the static web assets manifest, which records the
   length and the integrity hash of every file under `Server/wwwroot`. A
   server that keeps running holds the old manifest while the files on disk
   are the new ones, and it then answers `500` for those assets.

The symptom is easy to mistake for a broken deployment:

```
System.ArgumentOutOfRangeException:  (Parameter 'count')
  at Microsoft.AspNetCore.StaticAssets.StaticAssetsInvoker.SendAsync
  RequestPath="/css/Themes/darkly.custom.css"
```

If pages return `500` while `/css/site.css` still works, stop the server,
build, and start it again.

---

## 5. Run in production

### Publish

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" publish Server\Server.csproj `
    -c Release -r win-x64 --self-contained -o C:\Remotely\ServerApp
```

Publishing the whole solution — agents, desktop clients, and the server —
uses the helper script, which also stamps the version:

```powershell
.\Utilities\Publish.ps1 -RID win-x64 -OutDir C:\Remotely\ServerApp
```

`Publish.ps1` stamps `AssemblyVersion` and `FileVersion` with a build
timestamp such as `2026.11.07.2400`. **Publish every component with the same
version.** Replacing a DLL that was built with a different `/p:Version` fails at
runtime with `Could not load file or assembly 'Shared, Version=…'`.

### Start it

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:ASPNETCORE_URLS = "http://0.0.0.0:5000"
$env:Remotely_ApplicationOptions__DbProvider = "SQLite"
$env:Remotely_ConnectionStrings__SQLite = "Data Source=C:\Remotely\Remotely.db"
Set-Location C:\Remotely\ServerApp
& .\Remotely_Server.exe
```

The server applies its EF Core migrations on startup, so the database file is
created and brought up to date on the first run.

### Keep it running

Register it as a Windows service, or use a scheduled task that runs the
executable on boot. The `Remotely_Service` entry you see in Task Manager is the
**agent** that runs on managed machines, not the server.

---

## 6. Containers

```bash
docker compose -f docker-compose/docker-compose.yml up -d
```

The compose file maps a host port to container port `5000` and keeps the
server data in the `/var/www/remotely` volume. Change the host side of the
mapping to move the console to a different port.

---

## 7. Tests

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln
```

The solution holds two test projects: `Tests/Server.Tests` and
`Tests/Shared.Tests`.

---

## 8. Database migrations

`dotnet-ef` 8.0.33 is installed globally. The scripts in `Utilities/` select
one provider per run and require the matching connection string as an
environment variable, so set only the provider you are working with:

```powershell
$env:Remotely_ConnectionStrings__SQLite = "Data Source=C:\temp\migrations.db"
.\Utilities\Add-Migration.ps1 -Provider SQLite -MigrationName AddSomething
.\Utilities\Update-Database.ps1 -Provider SQLite -MigrationName AddSomething
```

The provider names are `SQLite`, `SQLServer`, and `PostgreSQL`. `Update-Database.ps1` supports `-WhatIf`. `Remove-Migration.ps1` asks for
confirmation before it deletes anything.

---

## 9. Client packages and update signing

The server signs the client packages it publishes so the agents can verify them
before installing. On startup it writes, next to each package in
`Server/wwwroot/Content/`:

- `Remotely-Win-<platform>.zip.manifest.json` — package hash, installer hash, version, key id
- `Remotely-Win-<platform>.zip.manifest.sig` — the ECDSA signature over those bytes

The signing key lives **outside** the web root, in `Server/AppData/keys/update-signing.key`, and is
protected with DPAPI on Windows. It is created on the first run.

Two consequences when you run from a checkout:

- The signing key is generated per checkout, so agents only trust a server
  whose key they have already received over the authenticated hub connection.
- The manifest files are written into `Server/wwwroot/Content/` and are **not**
  covered by `.gitignore`. They show up as untracked files after a run. Delete
  them before committing, or add them to `.gitignore`.

---

## 10. Troubleshooting

| Symptom | Cause and fix |
|---|---|
| `dotnet` fails with "A compatible .NET SDK was not found" | The machine-level SDK is 9. Use `"$env:USERPROFILE\.dotnet\dotnet.exe"` instead of `dotnet`. |
| Every page returns `500`, assets included | The server is running against a stale build. Stop it, rebuild, start it again. See section 4. |
| `/_framework/blazor.web.js`, `/Remotely_Server.styles.css`, or `/Components/*.razor.js` return `500` while the pages return `200` | The server runs in Production mode from a checkout, where those build-generated assets do not exist. Use the Development environment, or publish and run the published output. See section 4. |
| The build fails with `MSB3021` / "file is being used by another process" | The server is still running and holds the executable. Stop it, then build. |
| The console renders without styling | The theme stylesheet failed to load. Check `/css/Themes/darkly.custom.css` and `/css/console.css` return `200`. |
| `https://localhost:5001` fails with a trust error | Run `dotnet dev-certs https --trust`. |
| The agent does not appear in the device list | The agent connects to `https://localhost:5001` in debug mode. Check that port is up and the certificate is trusted. |
| `Could not load file or assembly 'Shared, Version=…'` | A DLL was replaced with one built under a different version stamp. Republish everything with the same `Publish.ps1` run. |
| Agent reports `Key ID '…' is not trusted` | The server generated a new signing key. The agent must fetch the new public key over the hub connection. |
| The development server refuses to start, or production stopped | Both want port 5000. See "Keep the port free" in section 3. |
