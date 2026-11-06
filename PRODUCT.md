# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

The operator console (device dashboard, server/organization admin) and the remote-control viewer are web surfaces. The Avalonia desktop clients (Windows/Linux) and the background agent are a **separate product surface** with their own UI, not one adaptive design language shared with the web.

## Users

**Primary — IT technician / support engineer.** Opens the console to see which devices are online and to act on a specific machine: watch it, take control of it, or work on it. The job is "reach this machine and resolve the issue", not "browse a dashboard".

**Secondary — the owner/server administrator** (the same person in this deployment). Also configures the server: database provider, HTTPS enforcement, known proxies, retention, banned devices, unattended-access consent, branding, API keys.

**Other roles that exist in the product** (confirmed from repository/docs, not from an interview): organization admin vs server admin, and non-admin users whose access is scoped by device-group membership. Remote-support operators outside the organization may be reached through shared session links.

## Product Purpose

Remotely is a self-hosted remote control and remote scripting solution. A device runs a background agent; an operator reaches it from a browser. Success means the operator can reach a machine that is already online, see what is on it, and take a governed action on it — with consent where consent is required, and with an authorization decision made by the server.

The product covers four jobs that happen in one session: observe the screen, control the screen, move files, and run scripts/commands on the remote machine.

## Positioning

The viewer runs **in the operator's browser** — no client install, no remote-desktop client, no plugin. A session is identified by a URL carrying a session id and an access key, so sharing access to a screen is a link rather than a deployment. Screen pixels travel as JPEG frames over a SignalR WebSocket and are painted onto a canvas; input, clipboard, audio, file transfer, and script execution travel over the same relay.

The differentiator a neighbouring product cannot truthfully copy is the **combination**: self-hosted server you own, agent-only enrollment, browser viewer, unattended *and* attended sessions, and server-side authorization decisions — with the operator never installing anything.

## Operating Context

- **Deployment**: the operator's own infrastructure. The current deployment runs the published server build on Windows with the server listening on port 5000 on the LAN (`192.168.0.2:5000`), with a device group containing an online Windows domain controller (`ARKAN-DC02`, connected as Administrator). The desktop agent and the background service run on the managed machine, not on the operator's.
- **Internet exposure**: the only supported reverse proxy is Caddy, directly facing the internet. Forwarded headers (`X-Forwarded-Proto/Host/For`) are trusted only for configured proxies (`KnownProxies`).
- **Storage**: SQLite by default, provider selectable through `DBProvider`; data, logs and session recordings live under the server's data folder (`/app/AppData` in the container, mounted to a host path).
- **Tenancy**: one organization by default; self-registration closes after the first account; multi-tenancy requires raising `MaxOrganizationCount`.
- **Session types**: attended (operator present at the device) and unattended (operator absent, device reachable by the agent). Unattended attempts can be gated by `EnforceAttendedAccess`, in which case the employee is prompted to accept.
- **Views are shared**: a session link can be handed to another person to watch, without granting control.
- **Day-to-day rituals**: check which machines are online → open one → watch the screen → interact → transfer a file or run a script → close the session. Administrators additionally read server logs, adjust retention, and manage banned devices and API keys.
- **Implementation context (not a design-system decision)**: ASP.NET Core / Blazor Server with SignalR Core; the browser client is TypeScript compiled during the .NET build; server-side CSS and a dark theme live in `Server/wwwroot/css/`; the desktop clients are Avalonia.

## Capabilities and Constraints

**Confirmed capabilities**: live screen streaming; remote mouse/keyboard input; clipboard read/write both ways; audio streaming (Windows only); file upload and download; remote script execution; chat; multiple monitors and monitor switching; Windows session switching; Ctrl+Alt+Del simulation; session metrics (FPS, throughput, latency); session recording; unattended access; shared view links; device groups; API keys and API authentication; quick-support clients and an installer that pull branding from the server; a task manager that lists and ends the processes running on a device; periodic screen monitoring, where the device captures its own desktop at a fixed interval with nobody watching, with per-device storage settings, retention limits, and several exception windows (hours during which nothing is captured) plus a one-off capture on demand.

**Confirmed constraints and terminology**

- **"View Only" is a session mode whose enforcement is not settled.** The UI exposes it, and SPEC-001 landed server-side checks, but the gate that would certify it is still FAIL/unverified in `GATE-REGISTER.md`. **Design and copy must not assume remote control is actually blocked in View Only mode.**
- **Consent**: unattended access may require the employee on the device to accept before the session starts. The employee is the notified party, not the operator.
- **Capture identity**: capturing an employee's screen must run under that employee's real token and must not hold SYSTEM elevation. The capture helper is designed for that (AD-02, AD-15) and is not yet implemented in production code.
- **Single capture engine**: ScreenRecorderLib is the intended engine, one engine only (AD-04).
- **Terms in use**: device = a managed machine with the agent; session = one operator-to-device connection; viewer = the browser surface; agent = the background client; unattended vs attended = operator-absent vs operator-present.
- **Undecided product facts**: Arabic/RTL support is an **open decision** — recorded, not committed. Nothing in the product requires Arabic copy today; the UI is English.

## Brand Commitments

- Name: **Remotely**. Copyright holder: **Immense Networks** ("© 2026 - Immense Networks" appears in the viewer footer).
- Upstream: `github.com/immense/Remotely`. The fork keeps the product name and the Immense Networks attribution.
- Server-side **branding** is a feature: the Branding page changes the logo, name and links used by the quick-support clients and the Windows installer (clients must be built from source with the server URL hard-coded to pick branding up).
- Existing logo and banner assets are authoritative and must not be regenerated: `Server/wwwroot/images/Remotely_Logo.png`, `Remotely_Icon*.png`, `Remotely_Icon_Transparent.png`, `viewer/remotely-logo-light.svg`, `viewer/remotely-logo-dark.svg`, `banner1..4.svg`, `DefaultIcon*.png`.
- No invented brand claims: no testimonials, no customer counts, no performance marketing numbers beyond what is measured in-product (session metrics are a product feature, not marketing copy).

## Evidence on Hand

- **Live deployment** available for verification: `http://192.168.0.2:5000/` with an online device (`ARKAN-DC02`, Windows, Administrator). A measured session (2026-11-06): screen streamed at **74 FPS, 0.06 Mbps, 1.74 ms latency, GPU capture enabled**; the canvas reported 1920×910.
- **Real product assets** as listed under Brand Commitments, plus `Server/wwwroot/images/viewer-background-gradient-dark.svg`.
- **Real screenshots** of the remote desktop from a live session (Windows 11 desktop with Task Manager open, remote-control-active toast on the device).
- **Product documentation**: `README.md` (install, HTTPS/proxy rules, configuration reference, admin accounts), `AGENTS.md` (architecture, security posture, gate register pointers).
- **Governance and decision records**: `DECISION-LOG.md` (AD/OD register — confirmed product decisions), `GATE-REGISTER.md` (gate status), `EVIDENCE-MAP.md` (labelled findings).
- **PoC evidence**: ScreenRecorderLib 7.0.1 compatibility probe on net10 — PASS (E-49).
- **Measured feature evidence (2026-11-06)**: the task manager and the screen monitoring were run against the live device (ARKAN-DC02): a real desktop screenshot was captured and stored on the server, the storage folder was changed and the next capture landed in the new folder, two exception windows (20-21 and 13-14) blocked an on-demand capture with a clear message, and deleting one screenshot or clearing them all removed both the files and their index rows (E-51-E-59).
- **Absent, and therefore not to be fabricated**: customer testimonials, case studies, pricing or licensing terms, SLAs, benchmark numbers, Arabic-language assets, and any screenshot of a session that was not actually run.

## Product Principles

1. **The server decides, the interface only reports.** Authorization for a session is a server-side decision (device-group membership, session access key, consent, OTP filter). A toggle in the UI is never the control. *Corollary for design: never let a control's appearance imply an enforcement that has not been certified.*
2. **No action on a person's machine without a recorded reason to be allowed.** Consent is the employee's to give; the capture identity is theirs; unattended access is the exceptional path, not the default.
3. **The operator's browser is the client.** Anything that would require installing software on the operator's machine, or that would break sharing a session as a link, is a regression.
4. **Self-hosted and LAN-first.** The operator owns the server, the database, the logs, and the recordings. No dependency on a third-party cloud service is introduced by this product.
5. **One capture engine, one honest picture of the screen.** The operator sees exactly what the employee sees — no synthetic, reconstructed, or approximated rendering.

## Accessibility & Inclusion

No product-specific accessibility standard has been established for this product yet, and none should be assumed. Two facts are recorded because they constrain future work:

- **The remote desktop arrives as pixels only.** Measured on a live session (2026-11-06): the viewer's accessibility tree exposes toolbar controls only, and no text, element, or role from the remote screen is available to assistive technology or to any agent reading the page. The operator is looking at an image of a screen, not at that screen's structure. Any design that assumes the remote UI can be traversed, read, or scripted semantically is invalid today.
- **Arabic and RTL are an open decision, not a commitment.** If support is added later, RTL is a layout concern that must be settled before it is assumed by any surface.
