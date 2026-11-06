# Constitution

<!--
  Constitution for the Remotely Capture Extension project.
  This project extends the upstream Remotely codebase (github.com/immense/Remotely)
  with a consent-based, scheduled capture subsystem.

  Baseline evidence:
    Remotely  master @ ec0d45d8f83ee512e60306ce4ddefed6509bd903 (2024-12-17), GPL-3.0
    ScreenRecorderLib master @ 7e8703c69dafc3e7c2e38b9de774c72a8e06ffca (2026-10-27), MIT
  Both hashes are the exact commits inspected during reverse engineering.
  Where this document states a behaviour, it is backed by inspected source.
-->

## Purpose

This constitution governs the design, implementation, and acceptance of the Remotely
Capture Extension: scheduled, consent-based employee screen / audio / webcam capture
for approximately 50 Windows 10–11 workstations, collected by an existing Remotely
Server and Remotely Windows client.

The project is an **extension of an existing open-source product**, not a rewrite.
Remotely already provides device enrolment, identity, SignalR transport, chat,
remote control, a screen-viewing feature, scripting, scheduling, file transfer,
and server-side recording storage. These are treated as assets to be preserved and
extended.

The purpose of this constitution is to make the non-negotiable engineering and
ethical constraints explicit and testable, so that later specification and planning
phases can be judged against a fixed baseline rather than re-argued per feature.

**Scope boundary.** This constitution does not authorize implementation of any
capture feature. It binds the specification and planning phases that follow.

## Principles

### I. Extend, Do Not Rewrite Remotely

New behaviour must be delivered as additive, isolated modules against stable
extension points — never as a fork-and-divergence of upstream subsystems.

*Rationale.* Upstream Remotely is GPL-3.0 and actively used. A rewrite forfeits
device management, authentication, and remote-control maturity that would take
years to reproduce, and multiplies the upgrade cost of every upstream release.

*Consequences.* New server behaviour lives behind new services/interfaces; new
client behaviour lives behind a capture abstraction, not inside existing capture
or transport code. Changes to existing files must be minimal and reviewable.

### II. Preserve Remote-Access Authorization; Never Weaken View Only

Existing remote-control authorization semantics must remain intact and must not be
relaxed. The "view only" capability must continue to function exactly as it does
today for existing users.

*Rationale.* Inspected source establishes that the view-only flag is carried by
the server session model (`Server/Models/RemoteControlSession.cs`,
`Server/Hubs/CircuitConnection.cs:218,266`) but is **not** consulted anywhere on
the server relay path, and is not propagated to the agent at all
(`Server/Hubs/DesktopHub.cs` session payload carries the access key only). The
enforcement today lives in the browser client (`Server/wwwroot/src/InputEventHandlers.ts`).
This means view-only is currently a **client-side UI guard, not an authorization
boundary**.

*Consequences.* No feature may depend on the existing view-only mechanism as a
security control. The capture subsystem must implement its own server-side,
authenticated authorization, and new work that touches the remote-control path must
not make the current situation weaker. Whether to harden view-only server-side is
a separate, explicitly-scoped decision — it may not be bundled silently into a
capture feature.

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
per-capability grant. A "privacy pause" is a first-class, locally-controlled
state, not a server-side preference. Webcam permission is separate from screen and
microphone permission. Any adaptive behaviour during third-party calls is treated
as **unverified** until proven on real hardware, and defaults to skipping.

### IV. Windows Session Isolation

Capture must never execute in the Windows Service session (Session 0). Capture
executes only inside the interactive user session; the Windows Service retains
communication, queueing, file management, and upload responsibilities.

*Rationale.* Inspected source shows the split already exists in Remotely:
`Agent/Program.cs:29` calls `.UseWindowsService()` (Session 0), while
`Desktop.Win/Program.cs` is a `WinExe` running in the interactive session, launched
into the target session by `Agent/Services/Windows/AppLauncherWin.cs` via
`Win32Interop.CreateInteractiveSystemProcess`. Session 0 has no interactive
desktop, so a Session-0 capture would produce blank or invalid frames and would
be a privacy problem in its own right.

*Consequences.* The capture component must be hosted by a process that runs in the
logged-on user's session. After logout, capture stops; the service continues
upload and communication. The service and the interactive process need an explicit,
authenticated channel for job hand-off — not shared folders.

### V. Least Privilege and Authenticated Device Communication

Device-to-server communication must use the existing authenticated Remotely
channels. The capture subsystem must not introduce a second, weaker trust path.

*Rationale.* Remotely already has device identity (`Shared/Entities/ApiToken.cs`),
SignalR transport with MessagePack (`Shared/Shared.csproj`), and API-key
authorization scoped by path and remote IP
(`Server/Auth/ApiAuthorizationFilter.cs`, `DataService.ValidateApiKey`). A
parallel anonymous upload endpoint would be a new attack surface on an
internet-facing server.

*Consequences.* Capture commands travel over the authenticated agent hub; uploads
are authorized server-side. New endpoints require explicit authorization
attributes and organization scoping. No capture artefact is readable without an
organization-scoped authorization check.

### VI. Reliable Transfer and Bounded Local Storage

Every completed capture must reach the server exactly once, with resumable transfer
that does not duplicate files. Local capture storage is bounded per device.

*Rationale.* Interrupted transfers on flaky LAN links are the normal case, not the
exception. Unbounded local queues fill employee disks and cause silent capture loss.

*Consequences.* The local queue is capped (5 GB per device as the confirmed
requirement) with deterministic eviction policy. Uploads are content-addressed or
otherwise idempotent so a retry cannot create a second copy. Media is stored on the
server filesystem — **not** in the relational database — because the existing
`Shared/Entities/SharedFile.cs` stores file bytes as a `byte[]` column, which is
unsuitable for large media.

### VII. Performance Budgets Validated on Real Hardware

Any performance claim — CPU, RAM, disk, network, encoding — must be measured on
representative target hardware before it is accepted.

*Rationale.* The target estate is Intel 5th–10th generation with 8–16 GB RAM and
dual monitors, and the project priority is low idle CPU/RAM and efficient
recording on older devices. Vendor benchmarks and modern-hardware numbers do not
transfer.

*Consequences.* No performance number may be stated as fact without a recorded
measurement on a representative device. Idle cost must be measured separately from
active-capture cost. Claims about Remotely's existing remote-viewing performance
must not be asserted without measurement.

### VIII. Minimal Dependency and Package Footprint

Dependencies are added only when they demonstrably reduce risk or implementation
cost. Every new dependency must be justified against its footprint, licence, native
requirements, and maintenance status.

*Rationale.* Remotely is deployed to 50 workstations with a small installation
footprint requirement, and a mixed-mode native dependency raises the bar for
every client (architecture, runtime, redistributables).

*Consequences.* Any dependency that ships native binaries must have its
architecture support and redistributable requirements stated in the specification
before adoption. Prefer abstractions that allow later replacement of a capture
engine.

### IX. Test-First Changes and Regression Protection

New behaviour ships with tests. Changes touching remote control, authentication,
device enrolment, or the transport layer must add regression tests proving the
existing behaviour is unchanged.

*Rationale.* The inspected test suite is thin — 26 test methods in total
(18 in `Tests/Server.Tests`, 7 manual in `Tests/Desktop.Win.Tests`, 1 in
`Tests/Shared.Tests`) — with no coverage of the SignalR hubs, file transfer,
desktop capture, or view-only behaviour. Untested regressions in a remote-control
product are a security event, not a bug.

*Consequences.* A specification is not "done" without executable verification.
Tests must be executed and observed to pass; a test may never be reported as
passing on the basis of it being written.

### X. Licence and Windows Compatibility Verification

Every dependency and every upstream artefact used must have its licence and target
compatibility verified in source before adoption.

*Rationale.* Remotely is GPL-3.0; ScreenRecorderLib is MIT (verified in
`LICENSE`). Compatibility with the exact Windows versions in the target estate,
and with the exact .NET/architecture combination actually shipped, is not
self-evident for native or mixed-mode dependencies.

*Consequences.* Licence compatibility is checked and recorded, not assumed. A
dependency that cannot be verified as working on the target configuration is
marked unverified and gated behind a proof of concept, never presented as
production-ready.

### XI. Evidence-Based Engineering Decisions

Every design decision in this project traces to inspected evidence — a file path,
a symbol, a measured result, or an explicitly labelled inference. Unverified
assumptions are labelled as such and carried as open questions.

*Rationale.* The specification must distinguish what Remotely does today from what
is proposed, or downstream planning will be built on fiction. The upstream codebase
contains duplicated parallel trees (`Desktop.Core/` versus `Desktop.Shared/`) and a
client-side-only view-only guard — both are easy to misread as stronger than they
are.

*Consequences.* Facts are labelled `[VERIFIED]`, `[INFERRED]`, `[UNKNOWN]`, or
`[PROPOSED]`. Absence of evidence is reported as absence, not as absence of the
feature. Specifications may not classify unverified functionality as implemented.

## Governance

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

This constitution carries a version number (`v1.0.0`) and a last-amended date.
Minor versions adjust wording without changing obligations. Major versions add,
remove, or alter an obligation.

### Compliance

Every specification produced under this constitution must include a compliance
review against each principle, stating how the specification satisfies it or why
the principle does not yet apply. A specification that cannot be reconciled with a
principle must either be amended or rejected — it may not proceed with the
conflict unresolved.

### Ratification

This constitution becomes binding on the project owner and the engineering
contributors when approved by the owner. The initial approval record is:

- Constitution version: v1.0.0
- Approved by: *pending owner approval*
- Approval date: *pending*

Until that approval is recorded, downstream specifications may be drafted but not
implemented.

### Enforcement

This constitution is the tie-breaker when a specification, a plan, or an
implementation decision conflicts with it. When a conflict cannot be resolved by
amendment, the conservative interpretation applies: the capture is skipped rather
than performed.
