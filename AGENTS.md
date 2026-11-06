# AGENTS.md

Remotely — a remote control and remote scripting solution built with .NET 10 (LTS retarget ratified — AD-16), Blazor, and SignalR Core. Upstream: https://github.com/immense/Remotely

## Setup commands

- Restore deps: `dotnet restore Remotely.sln`
- Build:        `dotnet build Remotely.sln`
- Test:         `dotnet test`   # MSTest; CI runs exactly this at repo root on push/PR to `master`. **Currently cannot execute here** — see Testing instructions.
- Run server:   `dotnet run --project Server/Server.csproj`   # ASP.NET Core + Blazor + SignalR
- Run desktop:  `dotnet run --project Desktop.Linux/Desktop.Linux.csproj` (Linux) or `Desktop.Win/Desktop.Win.csproj` (Windows)
- Publish:      `Utilities/Publish.ps1`  ·  migrations: `Utilities/Add-Migration.ps1` / `Update-Database.ps1`
- Docker:       `docker-compose/docker-compose.yml`  # single service `immybot/remotely:latest`, port 5000 (HTTP), volume `/var/www/remotely` → `/app/AppData`. **No Caddyfile exists in the repo** — Caddy is the only supported reverse proxy (per README) but must be supplied separately.
- There is no npm step: the browser client in `Server/wwwroot/src` is TypeScript compiled by `Microsoft.TypeScript.MSBuild` during `dotnet build`. Third-party browser libs come from **LibMan** (`Server/libman.json`), not npm. **The compiled `.js`/`.js.map` are committed** — regenerate and commit them with any `.ts` change, or the shipped client stays stale. **Known build blocker (2026-11-06):** `Microsoft.Web.LibraryManager.Build` 2.1.175 fails with `error LIB002` for the two `unpkg`-provider libraries (`@msgpack/msgpack@3.0.0-beta2`, `@microsoft/signalr-protocol-msgpack@8.0.7`), which breaks the `Server` build. The assets themselves resolve fine (HTTP 200) — the failure is in the LibMan client, not the provider. See `EVIDENCE-MAP.md` GAP-14.

## Project layout

- `Server/` — ASP.NET Core (net10.0): `API/`, `Hubs/` (SignalR), `Components/` + `Pages/` (Blazor), `Auth/`, `Data/` + `Migrations/`, `Services/`, `Extensions/`, `Options/`, `wwwroot/` (TS client)
- `Shared/` — cross-project entities, DTOs, primitives, shared services
- `Agent/` — background agent/service client (net10.0)
- `Desktop.Shared/`, `Desktop.UI/` (Avalonia), `Desktop.Linux/`, `Desktop.Win/` (net10.0-windows), `Desktop.Native/` (Win32/X11 P/Invoke). `Desktop.Core/` is a **duplicate of `Desktop.Shared` that is not in the solution and not referenced by any project** — dead code; the live implementation is `Desktop.Shared/`.
- `Tests/` — MSTest projects `Server.Tests/`, `Shared.Tests/`, `Desktop.Win.Tests/`; `LoadTester/` is a console tool
- `docker-compose/` — container deployment  ·  `Utilities/` — publish/migration/signing scripts
- `.github/workflows/` — `run_tests.yml` (CI test job), `build.yml` (manual RID builds)  ·  `.azure-pipelines/` — release pipeline

## Code style

- C# / .NET 10 (all solution projects retargeted to `net10.0` in `6c092ac9`; ratified as owner decision AD-16 — local SDK-10 availability tracked as OD-10), `Nullable` enabled repo-wide via `Directory.Build.props`; `RootNamespace` is `Remotely.<Project>`
- File-scoped namespaces (`namespace Remotely.Server.Services;`), 4-space indent, CRLF, braces required (`.editorconfig`)
- Global usings live in each project's `Usings.cs` — add to those instead of repeating `using` lines
- DI is registered in the composition root (`Server/Program.cs`) and `Server/Extensions/`; no service locators or statics for services
- Platform-specific desktop code is split into `Desktop.Linux/`, `Desktop.Win/`, `Desktop.Native/` behind interfaces in `Desktop.Shared/Abstractions/`
- Match the surrounding file's style; do not reformat unrelated lines

## Testing instructions

- Unit tests: `dotnet test` (MSTest + Moq). **26 test attributes exist in source, but only 19 are in the solution** (`Server.Tests` 18 + `Shared.Tests` 1); the other 7 are in `Tests/Desktop.Win.Tests`, which is **not in the solution** and whose tests are all `[Ignore]`d. **0 of them currently execute** (verified 2026-11-06). Since the net10.0 retarget (`6c092ac9`) the situation worsened (T-10): this machine has **SDK 9.0.315 only** (runtime 10.0.14 ≠ SDK), so `dotnet build Remotely.sln` fails `NETSDK1045` on all 11 projects — nothing compiles locally until OD-10 is resolved (install SDK 10 / revert / CI-only). The `Server` build additionally fails on the LibMan `LIB002` error. CI pins **SDK 7** — expect `NETSDK1045` there too. ⇒ **Do not claim a test result you did not run.** See `EVIDENCE-MAP.md` §3 and T-10.
- Test projects live under `Tests/`; follow the existing `*Tests.cs` naming and folder layout
- Add tests for new behavior; EF-backed tests use `Microsoft.EntityFrameworkCore.InMemory`
- `dotnet test` must pass before opening a PR

## Tool routing

- Architecture and call paths: `codebase-memory-mcp` (`search_graph`, `trace_path`, `get_architecture`, `search_code`); retrieve only relevant results.
- Knowledge graph / deep codebase map: `graphify` (`/graphify`, `graphify query`, `graphify path`, `graphify explain`); local deterministic tree-sitter AST (~40 langs + docs/SQL/PDFs), no vector store, every edge explained EXTRACTED/INFERRED; query scoped subgraph instead of grepping, use for god nodes, communities, and cross-file calls/imports/inherits.
- Exact symbols / refactoring: Serena; fallback to native Read/Edit if unavailable.
- Text, config, small files: native Read/Grep/Edit. Installed library API: context7, checked against the lockfile.
- Local UI: playwright; chrome-devtools for deep network/performance issues.
- Authenticated websites: browser-controller orchestrator only, with authorization.
- UI: Impeccable skill when available; check RTL/LTR, mobile, loading, empty, and error states.

### Indexed state (verified 2026-11-06)

- This folder is named `Captura` but holds the **Remotely** project — do not let the folder name mislead you.
- `codebase-memory-mcp` — project key is **`Remotely`** (9,956 nodes / 33,556 edges). Pass `project: "Remotely"`.
- `Serena` — project key is **`Captura`** (it derives the name from the folder). Call `activate_project` with this path before use.
- `graphify` — graph lives in `graphify-out/` (`graph.json`, `GRAPH_REPORT.md`, `graph.html`). Query with `graphify query "<question>"` from the repo root; rebuild with `graphify .`.
- `graphify-out/` and `.serena/` are excluded locally through `.git/info/exclude` — never commit them.
- Known gap: 47 `.razor` files and 52 others parse only partially in codebase-memory, so line ranges there are unreliable — read those files directly.

## PR & commit conventions

- Branch from `master`; never push to `master` directly. PRs target `master`.
- Commit message: short imperative sentence, e.g. `Update packages.` — upstream does not use conventional-commit prefixes.
- Keep the copyright-assignment paragraph from `.github/pull_request_template.md` (assigns copyright to Immense Networks).
- Open PR via `gh pr create` once CI is green.

## Security

- Never commit secrets; `Server.csproj` uses `UserSecretsId`, so local credentials stay in user secrets — not in committed settings.
- `Server/Options/ApplicationOptions.cs` holds DB provider, HTTPS enforcement, and known-proxy lists — do not commit credentials or widen `KnownProxies` casually.
- The server is internet-facing behind a reverse proxy: forwarded headers (`X-Forwarded-Proto`, `X-Forwarded-Host`, `X-Forwarded-For`) are trusted only for configured proxies.
- **View Only is not an authorization boundary.** `RemoteControlSession.ViewOnly` is written once (`CircuitConnection.cs:266`), never read on the relay path, never sent to the agent, and reset to `false` on session clone. The controls that *are* enforced are: device-group membership (`DataService.DoesUserHaveAccessToDevice`), the session `accessKey` match (`ViewerHub.cs:210-221`), `EnforceAttendedAccess` consent, and the OTP viewer filter. Build any new authorization on those, never on View Only.
- **The interactive desktop process runs as SYSTEM, not as the logged-on employee.** `Desktop.Native/Windows/Win32Interop.cs:121-145` duplicates **winlogon's** token (`DuplicateTokenEx` → `TokenPrimary`) and `winlogon.exe` runs as `NT AUTHORITY\SYSTEM`; `Win32Interop.cs:158` places it on `winsta0\<desktop>` of the target session. `Agent/Services/Windows/AppLauncherWin.cs:41,111` falls back to `Process.Start` only when the agent itself is not SYSTEM. ⇒ "interactive session" ≠ "employee identity". `ResolveDesktopName` returns `"Winlogon"` (secure desktop) during logon/UAC.
- **Governance — read before proposing any change:** `.specify/memory/constitution.md` = **Constitution v2.0.0, RATIFIED** (owner directive 2026-11-06). The superseded v1.0.0 is preserved verbatim at `.specify/memory/constitution.v1.0.0.superseded.md`; the exact diff is `CONSTITUTION-v2.0.0.diff`. **Never edit the constitution to make your own change fit — propose an amendment and let the owner ratify it; the constitution forbids the implementing agent from self-approving one.**
- **P00 baseline (2026-11-06):** `EVIDENCE-MAP.md` (35 labelled findings with `file:line` citations, plus what was and was not executed) · `DECISION-LOG.md` (AD / D / ADR / OD register — who approved what, and what would reverse it) · `GATE-REGISTER.md` (G1–G7 status: **0 PASS · 5 FAIL · 2 UNKNOWN**, plus PC-1/PC-2). **Read these first** — they already record what is known, and re-deriving it wastes effort or silently contradicts a ratified decision.
- Phase-1 evidence: `REVERSE-ENGINEERING-REPORT.md` (14-dimension reverse engineering) · `CONSTITUTION-REVIEW.md` (constitution review, gates G1–G7, SPEC-001 scope, ratification record in §12) · `specs/001-view-only-authorization-hardening/` (SPEC-001 spec + requirements-quality checklist).
- **Spec Kit is installed** (`specify-cli 1.3.1`, isolated venv at `C:\Users\Administrator\Documents\smacc_TEST\specify-venv`; not on PATH). Codex integration is initialised: skills live in `.agents/skills/`, invoked as `$speckit-constitution`, `$speckit-specify`, `$speckit-plan`, `$speckit-tasks`, `$speckit-implement`, `$speckit-converge`.
