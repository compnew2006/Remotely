# Constitution

<!--
  Constitution for the Remotely Capture Extension project.
  Version v2.1.0 — **PROPOSED on 2026-11-06. NOT RATIFIED.** This file becomes the
  governing constitution only if the project owner approves it in writing; until then
  v2.0.0 remains the ratified text and governs every artifact.

  Supersedes the v1.0.0 draft. The previous draft is preserved verbatim at
  .specify/memory/constitution.v1.0.0.superseded.md and in the pre-init snapshot
  .specify/memory/constitution.v1.0.0.superseded.md and in the pre-init snapshot
  .specify.orig-20261106/. The exact diff of the ratified v2.0.0 is
  CONSTITUTION-v2.0.0.diff. The exact diff of this proposed v2.1.0 is
  P00-CONSTITUTION-v2.1.0.proposed.diff.

  Baseline evidence (exact commits inspected during reverse engineering):
    Remotely         master @ ec0d45d8f83ee512e60306ce4ddefed6509bd903 (2024-12-17), GPL-3.0
    ScreenRecorderLib master @ 7e8703c69dafc3e7c2e38b9de774c72a8e06ffca (2026-10-27), MIT
  Review basis: REVERSE-ENGINEERING-REPORT.md (478 lines) + CONSTITUTION-REVIEW.md.

  Amendment log (v1.0.0 → v2.0.0 ratified; v2.0.0 → v2.1.0 proposed):
    A1   Principle II  — view-only: preserve function, strengthen authorization (not "as today").
    A2   Principle V   — Windows execution identity: capture runs in the EMPLOYEE'S user
                         context with least privilege, never as SYSTEM; the existing
                         Remotely remote-support process identity is frozen (owner decision 2).
    A3   Principle VI  — at-least-once delivery + idempotent server-side commit
                         (replaces the "exactly once" guarantee).
    A4   Principle VI  — strict 5 GB policy: stop and alert when full; never delete
                         unacknowledged media.
    A5   Principle XII — production security release gates G1–G7.
    A6   Principle IX  — ScreenRecorderLib compatibility items are UNRESOLVED until tested.
    A7   New section   — evidence taxonomy (final wording per A14: source-verified,
                         test-evidence, runtime-verified, release-gate).
    A8   Principle XI  — licensing and deployment obligations made explicit.
    A9   New section   — architecture decisions D1–D5 (D3 open).
    A10  Governance   — VERSIONING CORRECTION: this revision is MAJOR (v2.0.0), not
                         v1.1.0, because v1.0.0 governance defines a major version as any
                         change that "adds, removes, or alters an obligation". The earlier
                         v1.1.0 label was incorrect and is withdrawn.
    A11  Owner decision 3 — SPEC-001 is the View Only authorization hardening
                         specification; the foundation/capture integration specification
                         becomes SPEC-002.
    A12  Owner decision 4 — one capture engine only; no engine is integrated before the
                         Windows proof of concept passes.
    A13  Owner correction 1 — execution-identity evidence: the winlogon-derived SYSTEM
                         finding stays [SOURCE-VERIFIED]; no executed "whoami /all" record
                         exists, and none may be implied.
    A14  Owner correction 2 — evidence taxonomy split into SOURCE-VERIFIED, TEST-EVIDENCE,
                         RUNTIME-VERIFIED, RELEASE-GATE; a passing unit test never
                         establishes production readiness on its own; performance and
                         security-runtime claims require reproducible evidence.
    A15  Owner correction 3 — privileged process isolation restated as five concrete
                         conditions (identity, no SYSTEM elevation, no unrelated tokens or
                         elevation, cannot forge or grant consent, consent independently
                         verifiable by the server) instead of "cannot impersonate".
    A16  Owner correction 4 — D3 settled: the isolated Capture Helper inside the
                         interactive session, shipped in the existing Remotely client
                         installation and update package, is the accepted architecture;
                         only launch mechanism, IPC design, and lifecycle remain open.
    A17  Evidence taxonomy — adds a distinct [SOURCE-CAPABILITY] tier: a claim about
                         what the inspected source PERMITS or is EXPECTED to do, kept
                         separate from [SOURCE-VERIFIED], which states what the code
                         DOES. Owner directive 2026-11-06 (P00) uses the term
                         SOURCE-CAPABILITY as a primary label; this amendment makes it
                         a first-class tier and states its claim limits.
    A18  Governance — binds DECISION-LOG.md and GATE-REGISTER.md as normative
                         governance artifacts; a decision that is not recorded is
                         treated as non-existent, and a gate may not be raised to PASS
                         by the agent that produced the evidence.
    A19  Header accuracy — REVERSE-ENGINEERING-REPORT.md line count corrected 471 → 478.
-->

## Purpose

This constitution governs the design, implementation, and acceptance of the Remotely
Capture Extension: scheduled, consent-based employee screen / audio / webcam capture
for approximately 50 Windows 10–11 workstations, collected by an existing Remotely
Server and Remotely Windows client.

The project is an **extension of an existing open-source product**, not a rewrite.
Remotely already provides device enrolment, identity, SignalR transport, chat,
remote control, screen viewing, scripting, scheduling, file transfer, and server-side
recording storage. These are treated as assets to be preserved and extended.

The purpose of this constitution is to make the non-negotiable engineering, security,
and ethical constraints explicit and testable, so that later specification and planning
phases are judged against a fixed baseline rather than re-argued per feature.

**Scope boundary.** This constitution does not authorize implementation of any capture
feature. It binds the specification and planning phases that follow.

## Evidence taxonomy

Every claim in every artifact of this project must carry exactly one of these labels.
The distinction is mandatory because the audited baseline contains capabilities that
exist in source but have never been executed, and code paths that exist in source but
are never enforced.

| Label | Meaning | May be used to claim |
|---|---|---|
| `[SOURCE-VERIFIED]` | The behaviour was established by reading the inspected source, with the file and symbol recorded. | Design feasibility and code-level facts only. Never readiness, never compatibility, never performance. |
| `[SOURCE-CAPABILITY]` | The inspected source **makes the behaviour possible** — the code path exists and would do this if reached — but the behaviour has **not been executed and observed**. Distinct from `[SOURCE-VERIFIED]`, which states what the code **does**. | What the implementation makes *possible*, and therefore what a design may rely on **as an explicit expectation**. Never that the behaviour occurred, never readiness, never compatibility, never performance. |
| `[TEST-EVIDENCE]` | An automated test covering the behaviour was written, executed, and observed to pass. | That the behaviour holds **under that test**. Never production readiness, never target-hardware compatibility, never performance. |
| `[RUNTIME-VERIFIED]` | The behaviour was executed on representative target hardware and runtime, outside the unit-test harness, and the observation was recorded. | Runtime compatibility, security-runtime behaviour, performance. |
| `[RELEASE-GATE]` | The pass condition of a gate in Principle XII was exercised in the deployment context and the result recorded. | Production readiness, and only for the specific gate that was exercised. |
| `[PROPOSED]` | New behaviour or design not present in the codebase. | Planning only. |
| `[UNKNOWN]` | Not determinable from available material. | Nothing. Must be carried as an open question. |

**Rules for these labels.**

1. A passing automated test establishes `[TEST-EVIDENCE]` **only**. It never, by itself, establishes production readiness, runtime compatibility on the target estate, or any performance characteristic. A suite that passes in a build agent proves nothing about a device that has never run the code.
2. Every **performance** claim and every **security-runtime** claim requires `[RUNTIME-VERIFIED]` evidence that is **reproducible**: the exact command or procedure, the environment (hardware, OS build, configuration), the observation, and the location where the record is kept.
3. `[SOURCE-VERIFIED]` and `[TEST-EVIDENCE]` never substitute for `[RUNTIME-VERIFIED]` where a gate in Principle XII demands the latter.
4. No specification may classify `[SOURCE-VERIFIED]` or `[TEST-EVIDENCE]` as `[RELEASE-GATE]`. A gate is passed only by the evidence its pass condition names.
5. If a capability is load-bearing for a decision, it must first be promoted to the label the decision needs, or the decision must be written so that it remains valid if the capability turns out to be unreliable.
6. A claim about an expected runtime behaviour that has not been observed is labelled
   `[SOURCE-CAPABILITY]`, not `[SOURCE-VERIFIED]`. `[SOURCE-VERIFIED]` states what the
   inspected code does; `[SOURCE-CAPABILITY]` states what it permits or is expected to
   do. A `[SOURCE-CAPABILITY]` claim may inform a design decision only when that decision
   remains valid if the capability turns out to be unavailable, or when the decision is
   deferred to a controlled runtime test.
## Principles

### I. Extend, Do Not Rewrite Remotely

New behaviour must be delivered as additive, isolated modules against stable
extension points — never as a fork-and-divergence of upstream subsystems.

*Rationale.* Upstream Remotely is GPL-3.0 and actively used. A rewrite forfeits
device management, authentication, and remote-control maturity that would take
years to reproduce, and multiplies the upgrade cost of every upstream release.

*Consequences.* New server behaviour lives behind new services/interfaces; new
client behaviour lives behind a capture abstraction, not inside existing capture
or transport code. Changes to existing files must be minimal and reviewable. Model
changes must be delivered for all three migration providers (Sqlite, SqlServer,
PostgreSql — 66 migration classes today), and the specification must account for
that threefold cost.

### II. Preserve Remote-Control Functionality While Strengthening Authorization

Existing remote-control functionality must remain available and behaviour-compatible
for authorized users. At the same time, **authorization for remote control must be
enforced on the server and on the device execution path** — not only in the browser.

*Rationale.* The audited baseline establishes that the "view only" concept is a
per-session UI hint, not an authorization boundary:

* `Server/Hubs/CircuitConnection.cs:266` writes `RemoteControlSession.ViewOnly`;
  no server code path ever reads it to gate an action.
* `Server/Hubs/ViewerHub.cs:187` (`SendDtoToClient`), `:162` (`InvokeCtrlAltDel`)
  and `:77` (`ChangeWindowsSession`) forward control actions with no view-only gate.
* `Shared/Interfaces/IAgentHubClient.cs:54-60` — the `RemoteControl(...)` payload
  sent to the agent carries **no** view-only parameter, so the device cannot know
  the session is view-only.
* `Desktop.Shared/Services/DtoMessageHandler.cs:63` gates input on
  `viewer.HasControl`, but `Desktop.Shared/Services/Viewer.cs:95` initialises
  `HasControl = true` and **nothing assigns it anywhere in the repository** — the
  gate is inert.
* `Server/wwwroot/src/InputEventHandlers.ts:125-128` — the viewer can toggle the
  view-only flag off in the browser.
* `Server/Models/RemoteControlSession.cs:92` — session clone resets `ViewOnly = false`;
  `Server/API/RemoteControlController.cs:138-145,176` — API-created sessions carry
  no view-only at all.

The controls that **are** enforced are: device-group membership
(`Server/Services/DataService.cs:1018-1036`), the session `accessKey` match
(`Server/Hubs/ViewerHub.cs:210-221`), the `EnforceAttendedAccess` consent gate
(`CircuitConnection.cs:268` → `ViewerHub.cs:245-262` → `PromptForAccess`), and the
OTP viewer filter (`Server/Filters/ViewerAuthorizationFilter.cs:23-40`).

*Consequences.* New authorization is built on the enforced set above, never on the
view-only flag. Remediation of view-only is scoped as its own security
specification — **SPEC-001** — and is **not** silently bundled into capture
integration. Any capture feature that touches the remote-control path must not
weaken current behaviour, and must include regression tests proving the view-only
path still refuses control input.

### III. Consent First, Visible Recording, Privacy Fail-Closed

No capture may occur without prior, informed, revocable employee consent that
distinguishes screen capture, microphone capture, and webcam capture. Consent must
be visible to the employee at the moment of capture. When privacy cannot be
assured, the system must skip the capture rather than capture-and-redact.

*Rationale.* The deployment captures employees in a workplace context, and one of
the five required capture types (microphone-only) can capture third-party
conversations. Failing open risks recording other people who never consented.
Failing closed degrades gracefully.

*Consequences.* Every capture request carries an explicit consent state and a
per-capability grant, and consent is verifiable by the server, not asserted by the
capture process. A "privacy pause" is a first-class, locally-controlled state, not a
server-side preference. Webcam permission is separate from screen and microphone
permission. Any adaptive behaviour during third-party calls is treated as
**unverified** until proven on real hardware, and defaults to skipping.

### IV. Windows Session Isolation

Capture must never execute in the Windows Service session (Session 0). Capture
executes only inside an interactive user session; the Windows Service retains
communication, queueing, file management, and upload responsibilities.

*Rationale.* The baseline already separates the two: `Agent/Program.cs:29` calls
`.UseWindowsService()`, while `Desktop.Win/Program.cs` is a `WinExe` launched into
an interactive session by `Agent/Services/Windows/AppLauncherWin.cs:113-124`.
Session 0 has no interactive desktop, so Session-0 capture would yield blank or
invalid frames and would itself be a privacy failure.

*Consequences.* The capture component must be hosted by a process running in a
logged-on user's session. After logout, capture stops; the service continues
upload and communication. Session hand-off between the service and the session
process uses an explicit, authenticated channel — not shared folders, and not
unauthenticated named pipes (see Principle XII).

### V. Capture Runs in the Employee's User Context, with Least Privilege

Scheduled capture **must** execute in the logged-on employee's own user context
with the least privilege required. It must **not** execute as
`NT AUTHORITY\SYSTEM`. The capture component is a separately isolated **Capture
Helper**, installed and updated as part of the existing Remotely client package.

*Rationale.* The audited launch path does **not** yield the employee's identity.
`Desktop.Native/Windows/Win32Interop.cs:105-193` (`CreateInteractiveSystemProcess`):

* `:121-128` locates the `winlogon` process in the target session;
* `:134` opens winlogon's token with `TOKEN_DUPLICATE`;
* `:145` `DuplicateTokenEx(..., TokenPrimary, out hUserTokenDup)` — the duplicated
  token is derived from **winlogon**, which runs as `NT AUTHORITY\SYSTEM`;
* `:158` sets `si.lpDesktop = @"winsta0\" + ResolveDesktopName(dwSessionId)`;
* `:174-185` `CreateProcessAsUser(hUserTokenDup, ...)`.

`winlogon.exe` runs as SYSTEM, so on the evidence of the source alone the existing
interactive remote-support process is **expected to run as SYSTEM**. This remains
`[SOURCE-VERIFIED]`: no execution of that process has been performed in this project,
no `whoami /all` output has been captured, and no runtime record exists. The identity
must be established by a controlled Windows runtime test before any design decision
may rely on it. `AppLauncherWin.cs:41,111` shows the alternative path in source: when
the agent is *not* SYSTEM it uses `Process.Start`, which inherits the agent's own
identity.

Two design consequences follow.

1. **The existing remote-support process identity is frozen.** Changing how the
   Remotely desktop client is launched, its token, its privileges, or its desktop
   access is out of scope for this project. It requires a separate architectural
   review and owner approval. The Capture Helper is a **new, separate process**
   with a **different, explicitly chosen identity**; it does not reuse or mutate
   the remote-support launch path.
2. **Consent cannot be asserted by a privileged process.** Because the employee
   must be the party whose consent is recorded, the consent artifact must be
   attributable to the employee's own session context, and the enforcement point
   must not be able to fabricate an indicator. A SYSTEM process can present any
   indicator it likes, so the visible indicator and the consent record must be
   verifiable against the employee's context.

`ResolveDesktopName` (`Win32Interop.cs:195-215`) returns `"Winlogon"` — the secure
desktop — while `LogonUI.exe` or `consent.exe` is visible, so during logon and UAC
the session runs on the **secure desktop**, not the user desktop.

*Consequences.*

* The Capture Helper is launched into the target session **with the logged-on
  user's token** (the session's own token, not winlogon's), at the least privilege
  that permits screen, audio, and camera capture, and without elevation.
* The specification must state the intended execution identity explicitly and
  justify any deviation.
* The following must be **verified on Windows** and recorded before the capture
  design is accepted: the process token, the user SID, the session ID, the
  elevation state, desktop accessibility (which window station and desktop the
  process lands on), and the exact launch mechanism used to place the helper in
  the employee's session.
* Capture is suspended while the session is on the secure desktop.
* The Capture Helper's authority is bounded by the five conditions of gate G7: it
  runs under the intended employee identity, holds no SYSTEM elevation, cannot acquire
  unrelated user tokens or elevated privileges, cannot forge or grant employee
  consent, and consent authorization is independently verifiable by the server.
  Where the platform allows, the employee must be able to distinguish a genuine
  capture indicator from a forged one.

### VI. Reliable Transfer, Bounded Local Storage, At-Least-Once Delivery

Every completed capture must reach the server, with at-least-once delivery and
**idempotent** server-side commits keyed by a stable capture identifier. Local
capture storage is strictly bounded per device.

*Rationale.* Interrupted transfers on flaky LAN links are the normal case, not the
exception. Unbounded local queues fill employee disks and cause silent capture
loss. Network-level "exactly once" delivery is not a property a client can
provide; only idempotent commits at the server can make repeated delivery safe.

*Consequences.*

* Every capture receives a stable `CaptureId` (UUID) and a content digest at
  creation. The server stores the digest and rejects or de-duplicates a second
  commit with the same `(DeviceId, CaptureId)`. Re-delivery is expected and
  harmless; duplicate *records* are not.
* **Local queue policy is strict and explicit.** The 5 GB per-device ceiling is a
  hard limit on *unacknowledged* media.
  * When the queue reaches the limit, the device **stops starting new captures**
    and raises an alert to the administrator. It does not silently discard work.
  * **Unacknowledged media is never deleted.** Deletion is permitted only for
    media the server has acknowledged as stored, oldest-first, with an audit
    record of what was removed, when, and why.
  * The policy, the alert, and the eviction order are specified, not implied.
* Media is stored on the server filesystem — **not** in the relational database —
  because `Shared/Entities/SharedFile.cs:13` stores file bytes in a `byte[]`
  column, which is unsuitable for large media. The filesystem pattern already
  exists in `Server/Services/SessionRecordingSink.cs`.
* SignalR message limits are respected: `Server/Program.cs:194` sets
  `MaximumReceiveMessageSize = 100_000`, so media transfer uses chunking or
  streaming, never a single large payload.

### VII. Performance Budgets Validated on Real Hardware

Any performance claim — CPU, RAM, disk, network, encoding — must be measured on
representative target hardware before it is accepted.

*Rationale.* The target estate is Intel 5th–10th generation with 8–16 GB RAM and
dual monitors, and the project priority is low idle CPU/RAM and efficient
recording on older devices. Vendor benchmarks and modern-hardware numbers do not
transfer.

*Consequences.* No performance number may be stated as fact without a recorded
measurement on a representative device; no figure may be quoted from upstream or
vendor material. Idle cost must be measured separately from active-capture cost.
Claims about Remotely's existing remote-viewing performance must not be asserted
without measurement.

### VIII. Minimal Dependency and Package Footprint

Dependencies are added only when they demonstrably reduce risk or implementation
cost. Every new dependency must be justified against its footprint, licence,
native requirements, and maintenance status. **A dependency that has not passed
the compatibility gates below is not adopted.**

*Rationale.* Remotely is deployed to 50 workstations with a small installation
footprint requirement, and a mixed-mode native dependency raises the bar for
every client.

*Consequences.*

* Any dependency that ships native binaries must have its architecture support,
  redistributable requirements, and per-device install prerequisites stated in the
  specification before adoption.
* Prefer abstractions that allow later replacement of a capture engine.

### IX. ScreenRecorderLib Compatibility Is Unresolved Until Tested

ScreenRecorderLib is the **first candidate** for the capture engine, behind the
`ICaptureEngine` abstraction. **Exactly one engine is implemented initially.** No
engine — including this one — is integrated into Remotely before the Windows proof
of concept establishes compatibility and performance on representative hardware.
Its adoption is conditional, and no compatibility or performance claim may be made
about it until the following are established and recorded — all currently
`[UNKNOWN]` or `[SOURCE-VERIFIED]`:

1. **Modern .NET loading.** The package is a C++/CLI mixed-mode assembly targeting
   `.NET Framework v4.8` (`ScreenRecorderLib.vcxproj:31`, `CLRSupport=true`);
   there is no `netstandard` or `net8.0` target. Whether and how it loads from a
   `net8.0` process is unverified; Microsoft documents `/clr:netcore` as the
   supported porting path, and `dotnet/runtime#10333` notes that .NET-Framework-
   built mixed-mode assemblies are hard-wired to initialise .NET Framework.
2. **Native architecture.** `ScreenRecorderLib.targets:4-7` rejects `AnyCPU` and
   requires an explicit `x86`/`x64`/`ARM64` platform. Remotely currently builds
   `Desktop.Win` as `AnyCPU;x86;x64` — the packaging and update path must be
   reconciled, and the client installation must be pinned to one architecture.
3. **VC++ runtime.** The library requires the matching Visual C++ Redistributable
   (`README.md:2`). Presence on the target estate is unverified and must be a
   documented install prerequisite and a preflight check.
4. **Media Foundation.** Required and **not** installed by default on Windows
   Server, and absent on Windows N/KN editions without the Media Feature Pack
   (`README.md:2`). This affects the target estate directly.
5. **Microphone-only output.** `Options.h:123-125` states that with
   `IsVideoCaptureEnabled=false` "only audio is recorded if enabled, and video
   will be black". That is a source-level claim about behaviour that has never
   been executed; the produced file's playability, duration, and container
   behaviour are `[UNKNOWN]`.
6. **Webcam behaviour.** Camera capture exists in source
   (`ScreenRecorderLibNative/CameraCapture.cpp`, `VideoCaptureRecordingSource`,
   `GetSystemVideoCaptureDevices`, `TakeSnapshot` on an active camera source),
   but the library ships **no camera tests** and its CI does not run tests at
   all. Snapshot validity from a camera source, format negotiation, and
   silent-video output are `[UNKNOWN]`.
7. **Dual-monitor and DPI.** The library performs no `SetProcessDpiAwareness`
   call, and its capture is per-display (`DisplayRecordingSource`) with no
   virtual-desktop source. Behaviour on two monitors with different DPI is
   `[UNKNOWN]` and must be tested on the target hardware.
8. **Release selection.** The published NuGet `7.0.1` (`6463832`) predates the
   fragmented-MP4 fix in `master` (`7e8703c`, issue #375). The build source must
   be pinned deliberately, and any locally built copy must be redistributed
   under the licence terms in the licensing section.

*Consequences.* If the PoC establishes that ScreenRecorderLib cannot meet the
requirements, the `ICaptureEngine` abstraction must permit a second engine, and
that engine may be built without a new third-party dependency (for example a
managed Media Foundation / Windows Graphics Capture implementation). **A second
engine is not authorised in advance**; it is authorised only by evidence.

### X. Test-First Changes and Regression Protection

New behaviour ships with tests. Changes touching remote control, authentication,
device enrolment, or the transport layer must add regression tests proving the
existing behaviour is unchanged.

*Rationale.* The audited test suite is thin — 26 `[TestMethod]` attributes in
total (18 in `Tests/Server.Tests`, 7 `[Ignore]`d manual tests in
`Tests/Desktop.Win.Tests`, 1 in `Tests/Shared.Tests`), so 19 execute in practice —
with no coverage of the SignalR hubs, file transfer, desktop capture, or the
view-only path. The project `Tests/Desktop.Win.Tests` is not referenced by
`Remotely.sln`, so it never runs in CI, and the GitHub workflows pin SDK 7 against
`net8.0` projects.

*Consequences.* A specification is not "done" without executable verification.
Tests must be executed and observed to pass; a test may never be reported as
passing on the basis of it being written. Tests that require Windows-only or
desktop-only conditions must still be wired into a CI path that actually runs.

### XI. Licence and Windows Compatibility Verification

Every dependency and every upstream artefact used must have its licence and
target compatibility verified in source before adoption, and the resulting
obligations carried into the deployment plan.

*Rationale and obligations.*

* **Remotely is GPL-3.0.** Any derivative work, integrated component, or
  distributed build inherits GPL-3.0 obligations: provide or offer the
  corresponding source of the covered work, retain the licence and copyright
  notices, permit redistribution and modification under the same terms, and do
  not add restrictions that would block the rights GPL-3.0 grants. The
  distribution channel and the source-offer mechanism are decided explicitly
  before any build is shipped.
* **ScreenRecorderLib is MIT** (`LICENSE`, Copyright (c) 2017 Sverre Skodje).
  MIT permits commercial use, modification, distribution, and sublicensing; the
  only condition is that the copyright notice and permission notice are included
  in all copies or substantial portions. If the library is adopted — whether
  consumed as a NuGet package or built from source — its `LICENSE` must ship in
  the client distribution, and the notice must remain intact in any fork.
* MIT is compatible with GPL-3.0 for this use; the incompatibility risk in this
  project is not the licence but the **deployment prerequisites** and the
  **maintenance posture** (bus factor 1; CI does not run the upstream tests).
* **Deployment obligations are explicit, not implied:** x64-only client build;
  Visual C++ Redistributable and Media Foundation present on every client; a
  reverse proxy (Caddy is the only supported one per the upstream README — the
  repository ships **no** Caddyfile, so it must be supplied and configured);
  signed and verified client updates; server storage sized for the retention
  policy; and `RemoteControlRequiresAuthentication` left enabled in production.

### XII. Production Security Release Gates

The following gates are **release blockers** for any production deployment of the
capture subsystem. A gate is passed only when the check has been executed and
recorded, not when the code exists.

| # | Gate | Pass condition |
|---|---|---|
| G1 | **Server-enforced View Only** | The view-only flag is read on the server relay path, propagated to the device, and enforced on the device before any input is applied. Remediation is delivered as SPEC-001 and verified by regression tests. |
| G2 | **Signed and verified client updates** | The update payload and its installer are signed; the client verifies the signature (and a pinned hash) before executing anything. Today `Agent/Services/Windows/UpdaterWin.cs:136-146` downloads `Install-Remotely.ps1` and a zip and executes them with **no** signature, hash, or Authenticode check (no `SignedXml`/`X509Certificate`/`Authenticode` usage exists in client code), and `Agent/Services/UpdateDownloader.cs:26` allows a 6-hour download timeout. |
| G3 | **Safe file paths and transfer boundaries** | Remote-supplied file names are canonicalised and confined to an approved directory; the traversal exposure in `Desktop.Win/Services/FileTransferServiceWin.cs:88` (a remote `fileName` is used verbatim in `Path.Combine`) is closed; the broad `BuiltinUsersSid` Modify grant in `:157-203` is replaced with least privilege; and the unbounded `_partialTransfers` map (`:22`) is bounded. |
| G4 | **Authenticated and authorized device actions** | Every action the agent or the capture helper can be asked to perform is authorized server-side against organization and device-group scope, and executed only after an authenticated, org-scoped instruction. The capture subsystem must not introduce a second, weaker trust path alongside the existing `ApiToken` + `ValidateApiKey(path, ip)` model. |
| G5 | **Authenticated local IPC with least privilege** | The service↔session channel is authenticated (mutual or signed messages), confined to the intended session, and least-privilege. The existing named pipe is **not** authenticated: `Desktop.Shared/Services/ChatHostService.cs:30` creates `NamedPipeServerStream` with a default ACL and no client verification, and the pipe name is a GUID passed on the process command line (`AppLauncherWin.cs:44,50`). |
| G6 | **Verified consent and privacy controls** | Consent is granted per capability, recorded server-side, revocable, visible during capture, and attributable to the employee. The visible recording indicator and the local privacy pause are verified in the target session. |
| **G7** | **Privileged process isolation** | The Capture Helper (1) runs under the intended employee identity; (2) holds no SYSTEM elevation; (3) cannot acquire unrelated user tokens or elevated privileges; (4) cannot forge or grant employee consent; and (5) consent authorization is independently verifiable by the server. The existing Remotely remote-support process is **not** modified by this project and remains functionally unchanged. Isolation is verified by the recorded identity facts required in Principle V: process token, user SID, session ID, elevation state, desktop accessibility, and launch mechanism — each recorded as `[RUNTIME-VERIFIED]`, not inferred from source. |

Additionally, before production:

* The unauthenticated endpoints (`Server/API/ClientDownloadsController.cs:117-121`
  and `Server/API/DevicesController.cs:124-135`) are closed or explicitly accepted
  in writing by the owner.
* `Server/Filters/ViewerAuthorizationFilter.cs:23-27` — the open remote-control
  path when `RemoteControlRequiresAuthentication` is disabled — must be closed in
  production.

### XIII. Evidence-Based Engineering Decisions

Every design decision in this project traces to inspected evidence — a file path,
a symbol, a measured result, or an explicitly labelled inference under the
taxonomy above. Unverified assumptions are carried as open questions.

*Rationale.* The specification must distinguish what Remotely does today from what
is proposed, or downstream planning will be built on fiction. The audited
baseline contains duplicated parallel trees (`Desktop.Core/` versus `Desktop.Shared/`),
a client-side-only view-only guard, a microphone path that captures system audio
rather than the microphone, an unsigned update path, and a CI configuration that
cannot build the projects it claims to test.

*Consequences.* Facts are labelled `[SOURCE-VERIFIED]`, `[TEST-EVIDENCE]`,
`[RUNTIME-VERIFIED]`, `[RELEASE-GATE]`, `[PROPOSED]`, or `[UNKNOWN]`, per the
taxonomy above. Absence of evidence is reported as absence, not as absence of the
feature.

## Architecture decisions

| ID | Decision | Status |
|---|---|---|
| D1 | View Only becomes a server-enforced authorization boundary before production deployment, with its remediation scoped as **SPEC-001** rather than bundled into capture integration. | **Accepted.** Amends Principle II; gate G1. |
| D2 | ScreenRecorderLib is the first capture-engine candidate, behind an `ICaptureEngine` abstraction. Exactly one engine is implemented initially. A second engine is authorised only if the Windows proof of concept establishes the need. | **Accepted.** Amends Principle IX. |
| D3 | The isolated **Capture Helper process** inside the interactive Windows session is the **accepted architecture**. It must be included in the existing Remotely client installation and update package — it is not a separate product. Only the exact Windows launch mechanism, the IPC design, and the lifecycle details remain open for proof-of-concept validation. | **Accepted — owner directive, 2026-11-06.** Amends Principle V; gate G7. |
| D4 | Spec Kit installation is authorised and performed with the official `codex` integration; no tool was installed or initialised before owner authorisation. Installation authorises nothing beyond tooling. | **Accepted.** See the toolchain section. |
| D5 | Capture runs in the employee's user context with least privilege, never as SYSTEM; the existing Remotely remote-support process identity is frozen and requires a separate architectural review before any change. | **Accepted.** Amends Principle V; gate G7. |

### D3 — accepted architecture; details open for proof-of-concept validation

The comparison below was the basis for the owner's decision to adopt the isolated
Capture Helper. It is retained as the decision record. The **architecture is now
settled**: a separately isolated helper process, inside the interactive session,
shipped and updated inside the existing Remotely client package. What remains open is
the **exact launch mechanism** (how the helper is placed into the employee's session
with the employee's token), the **IPC design** between the service and the helper, and
the **lifecycle** (start, stop, restart, and behaviour at logon/logout).

| Criterion | In-process `Desktop.Win` integration | Isolated Capture Helper in the session |
|---|---|---|
| Execution identity | Same as the existing desktop client — SYSTEM by the current launch path (`Win32Interop.cs:105-193`), which the owner has ruled out for capture. | The employee's own user context, least privilege, not elevated (Principle V). |
| Fault isolation | A capture-engine crash takes down remote control with it. | Engine crash leaves remote control alive. |
| Idle cost | Shares the Avalonia process already resident. | One extra process; must be proven negligible. |
| Installation / update | No new packaging path; the client install already carries the desktop binary. | Must be added to the existing client install and update payload — explicitly *not* a separate product. |
| Reuse of existing abstractions | `Desktop.Shared/Abstractions` (`IScreenCapturer`, `IAudioCapturer`, `IFileTransferService`) are directly reusable. | Same abstractions reusable, but the helper needs its own DI and lifecycle. |
| Known burden of the host | `Desktop.Win` uses a manual `BuildServiceProvider()`, a static `StaticServiceProvider`, no single-instance guard, and no Generic Host. | New, small, purpose-built host. |
| Interaction with capture and streaming | Capture and the live stream share one process ⇒ encoder contention risk (unmeasured). | Separated ⇒ contention avoided, at the cost of an extra process. |

## Specification sequence

| Spec | Title | Scope |
|---|---|---|
| **SPEC-001** | Remote Access / View Only Authorization Hardening | Server-side enforcement of the view-only flag; authorization of control messages; protection of the flag across session changes (`ChangeWindowsSession` / `RemoteControlSession.CreateNew`); agent-side defence in depth (the currently inert `HasControl` gate); negative security tests. Delivered as a separate security specification, ahead of capture work. |
| **SPEC-002** | Remotely Foundation and Capture Integration | The previously drafted foundation specification: preserved device enrolment and View Only functionality; administrator and employee interaction flows; the Service ↔ Capture Helper boundary; consent and privacy controls; one minimal end-to-end capture proof (approved screenshot request → local result → secure transfer → server acknowledgement → administrator-visible result); recorded integration and compatibility risks. |
| Later | — | Recurring capture scheduler · screen video with microphone audio · independent audio recording · webcam photo and silent video · resumable upload hardening and 5 GB queue controls · retention and media library · update management · adaptive capture policy during calls. |

## Open questions requiring owner direction

1. **Launch mechanism.** Which mechanism places the Capture Helper into the
   employee's session under the employee's own token — session-token duplication from
   the session, a scheduled task, or a service-hosted launcher — and does that
   mechanism need its own architectural review? (Principle V, D3)
2. **IPC design.** Which channel carries job hand-off between the Session-0 service
   and the session helper, and what authenticates it? (Principle XII gate G5, D3)
3. **Lifecycle.** When does the helper start and stop, what happens across logon and
   logout, and how is a second instance prevented? (Principle IV, D3)
4. Is ScreenRecorderLib adopted as a NuGet package or built from a pinned source
   commit, given the `7.0.1` versus `master` release gap? (Principle IX)
5. Is the client build pinned to x64 for the whole estate? (Principle IX.2, XI)
6. Is the Media Feature Pack / VC++ redistributable installation path under the
   owner's control for all 50 devices, and who provisions it? (Principle XI)
7. Does the owner accept that the server-side retention for capture media is a
   separate storage budget from the existing recording volume on the 1 TB disk? (Principle VI)

## Governance

### Governance artifacts

Two standing artifacts bind this constitution and are maintained alongside it:

- `DECISION-LOG.md` — the decision register. Every owner directive, architecture
  decision, agent-level decision, and open question is recorded with its approval
  state, its basis, and the condition that would reverse it. **A decision that is not
  recorded is treated as non-existent**, and a decision marked `PROPOSED` is not in
  force and may not be implemented against.
- `GATE-REGISTER.md` — the gate register. Each gate in Principle XII carries an
  explicit state — `PASS`, `FAIL`, `UNKNOWN`, or `NOT STARTED` — together with the
  evidence required, the evidence held, the related gaps, and the next action. **A gate
  may not be raised to `PASS` by the agent that produced the evidence**; the owner
  records the pass. `UNKNOWN` means the check was not run, and is a synonym for neither
  `FAIL` nor `PASS`.

The agent implementing a change may not amend this constitution in order to make that
change compliant. Amendments are proposed as a diff and take effect only on explicit
owner approval, per the Amendment Procedure below.

### Amendment Procedure

1. Amendments are proposed as a pull request against this file, quoting the
   principle(s) affected and the evidence that motivates the change.
2. Amendments require explicit approval by the project owner. An amendment may not
   be self-approved by the agent implementing it.
3. Amendments take effect on approval. Work already in flight against the
   superseded text is assessed for compliance and either reworked or explicitly
   grandfathered in the same pull request.
4. A principle may not be deleted to resolve a conflict. If a principle is
   genuinely superseded, it is marked superseded with the date and the replacing
   principle, so the history of the constraint remains visible.

### Versioning

This constitution carries a version number and a last-amended date. **Minor**
versions adjust wording without changing obligations. **Major** versions add,
remove, or alter an obligation.

**The v1.0.0 → v2.0.0 change is MAJOR.** It adds binding obligations that did not
exist in v1.0.0: Principle V (execution identity, least privilege, and the frozen
remote-support identity), Principle XII and gates G1–G7, Principle IX's conditional
adoption rule, the evidence taxonomy, the licensing and deployment obligations,
and the specification sequence that reorders SPEC-001. By the v1.0.0 rule — a major
version is any change that "adds, removes, or alters an obligation" — this must be
v2.0.0. An earlier draft labelled this revision "v1.1.0" on the reasoning that no
principle had been removed; that label is withdrawn because it contradicts the
v1.0.0 rule and because the added gates are new binding obligations.

### Compliance

Every specification produced under this constitution must include a compliance
review against each principle and each gate in Principle XII, stating how the
specification satisfies it or why it does not yet apply. A specification that
cannot be reconciled with a principle or gate must either be amended or
rejected — it may not proceed with the conflict unresolved.

### Ratification

**v2.0.0 — Ratified.** The basis is the project owner's written directive of **2026-11-06**,
which: accepted the architectural direction of Constitution v2.0.0; authorised
finalization after four precise corrections (A13–A16); authorised installation of the
official Specify CLI and initialisation of the `codex` integration only; set the final
API access-mode policy for SPEC-001 (ViewOnly default, FullControl explicitly
authorised and audited); and directed that SPEC-001 be created through the official
`$speckit-specify` workflow.

- Constitution version: **v2.0.0**
- Approved by: the project owner (directive dated 2026-11-06)
- Approval date: **2026-11-06**
- Superseded: v1.0.0 — preserved verbatim at `.specify/memory/constitution.v1.0.0.superseded.md`
- Exact diff of this ratification: `CONSTITUTION-v2.0.0.diff`
- Constitution compliance for SPEC-001: recorded in `CONSTITUTION-REVIEW.md` §12

**v2.1.0 — Proposed, not ratified.** The amendments above (A17–A19) were prepared on
2026-11-06 while closing the P00 baseline and await the project owner's written
approval. **Until that approval, v2.0.0 remains the ratified constitution and this
proposal has no force.** The exact diff is `P00-CONSTITUTION-v2.1.0.proposed.diff`.

Downstream specifications may now be drafted. **Implementation remains gated**: no
capture feature may be implemented before the gates in Principle XII that apply to it
have been satisfied, and the capture subsystem as a whole is blocked by G1 and G7.

### Enforcement

This constitution is the tie-breaker when a specification, a plan, or an
implementation decision conflicts with it. When a conflict cannot be resolved by
amendment, the conservative interpretation applies: the capture is skipped rather
than performed, and the release gate is treated as failed until proven passed.
