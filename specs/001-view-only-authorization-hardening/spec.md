# Feature Specification: Remote Access / View Only Authorization Hardening

**Feature Branch**: (none — no git branch is created; the `git` extension is not installed in this project. The feature is located through `.specify/feature.json`.)

**Created**: 2026-11-06

**Status**: Draft

**Input**: User description: "SPEC-001: Remote Access / View Only Authorization Hardening. Server-enforced session access modes; authorization for all remote-control messages; server-owned immutable session permission boundaries; View Only preservation during session switching and cloning; agent-side defense in depth; secure API session creation; authentication and authorization fail-closed behavior; explicit API compatibility and migration requirements; negative security tests; FullControl regression tests; working CI verification. Screen capture scheduling, webcam recording, ScreenRecorderLib implementation, and media upload are excluded."

## User Scenarios & Testing *(mandatory)*

<!--
  User stories are prioritized as independent journeys. Each one is independently
  testable and delivers a viable slice on its own.
-->

### User Story 1 - A viewer connects with the least privilege by default (Priority: P1)

A support administrator opens a device to assist an employee. The session that is
created grants **ViewOnly** access unless the administrator explicitly requests
**FullControl** and that request is independently authorized. The viewer can see the
employee's screen and cannot type, click, move the pointer, press control keys, or
change the target Windows session. The employee's device behaves exactly as if no
keyboard or mouse input had been sent.

**Why this priority**: This is the whole point of the feature. Today the access mode
is decided by whoever opens the browser tab, and the device cannot tell the
difference between a viewer who was trusted and one who was not. Without this story,
every other story is hardening a mechanism that does not hold.

**Independent Test**: Start one remote-support session against a test device, let the
viewer see the screen, then attempt every remote-control action available in the
interface. The device must apply none of them, and the attempt must be recorded.

**Acceptance Scenarios**:

1. **Given** an administrator opens a device for a viewer and does not request any
   specific access mode, **When** the session is created, **Then** the session access
   mode is ViewOnly and the viewer interface reflects a non-controlling session.
2. **Given** a ViewOnly session, **When** the viewer attempts keyboard, pointer, or
   control-key input, **Then** the device applies no input and the session stays
   usable for viewing.
3. **Given** a ViewOnly session, **When** the viewer requests to change the target
   Windows session, **Then** the request is refused and the session target is unchanged.
4. **Given** a ViewOnly session, **When** the browser page is reloaded or the viewer
   reconnects, **Then** the session access mode remains ViewOnly.

---

### User Story 2 - Full control is granted only by an explicit, authorized, audited decision (Priority: P1)

A support administrator who needs to operate the employee's device explicitly
requests FullControl. The request is evaluated against the administrator's
authenticated identity, the employee's organization, the device's membership in the
administrator's device groups, and any attended-access consent requirement. Only when
every check passes does the session carry control authority — and the decision is
recorded with who decided, what was decided, and when.

**Why this priority**: Control authority over someone else's machine is the highest
consequence this product grants. Today a single boolean carried in a link decides it,
and the decision leaves no trace worth auditing.

**Independent Test**: Attempt to obtain FullControl through each available route
(administrator interface, API, and viewer link) without satisfying every precondition,
and confirm each attempt is refused and recorded; then satisfy every precondition and
confirm control works and is recorded.

**Acceptance Scenarios**:

1. **Given** an authenticated administrator with device-group access and a satisfied
   attended-access consent, **When** they explicitly request FullControl, **Then** the
   session access mode becomes FullControl and the privilege decision is recorded.
2. **Given** a request for FullControl without an explicit request, **When** the
   session is created through any route, **Then** the session is ViewOnly.
3. **Given** a request for FullControl from a caller without device-group or
   organization scope, **When** the request is evaluated, **Then** it is refused and
   recorded as refused.
4. **Given** a request for FullControl where attended-access consent is required and
   has not been given, **When** the request is evaluated, **Then** it is not granted
   until consent is obtained, and the consent outcome is recorded.
5. **Given** any session, **When** a viewer presents a self-declared FullControl
   request or modifies the link that was given to them, **Then** the session access
   mode is unchanged.

---

### User Story 3 - The session's permission boundary is owned by the server (Priority: P2)

An administrator switches the remote session from one Windows session to another, or
the session is cloned, or the viewer reconnects, or a viewer link is generated through
the API. In every one of those cases the session's access authority is re-derived
from server-owned state. A session that was ViewOnly never becomes FullControl as a
result of any of these events, and no event leaves the device holding an access mode
that the server has not authorized.

**Why this priority**: Session lifecycle events are the natural places for privilege
to drift. Today cloning a session resets the access hint, and switching the target
session re-derives it from the client side.

**Independent Test**: Run each lifecycle event against a ViewOnly session and a
FullControl session and confirm the resulting access mode in each case.

**Acceptance Scenarios**:

1. **Given** a ViewOnly session, **When** the target Windows session is changed,
   **Then** the resulting session remains ViewOnly.
2. **Given** a ViewOnly session, **When** the session is cloned, **Then** the clone
   remains ViewOnly and carries its own recorded authorization decision.
3. **Given** a viewer reconnects using a previously issued link, **When** the
   connection is established, **Then** the access mode comes from the server-owned
   session record and not from the link.
4. **Given** a session, **When** the device's organization scope or the viewer's
   device-group membership is revoked, **Then** control authority is withdrawn at
   the next server evaluation and no further control message is accepted.

---

### User Story 4 - The device refuses control the server did not authorize (Priority: P2)

The employee's device independently verifies every remote-control message against the
authority the server granted for that session. A control message that arrives without
a matching server authorization — including a replayed, stale, or tampered message — is
rejected on the device and recorded locally, without disturbing the viewing session.

**Why this priority**: The server is one enforcement point; the device is the other.
Defence in depth is what makes a compromised or misconfigured server unable to turn
into silent control of an employee workstation.

**Independent Test**: Deliver control messages to a test device for a session the
server never authorized as FullControl, and confirm the device applies none of them
while the viewing session continues.

**Acceptance Scenarios**:

1. **Given** a ViewOnly session, **When** a control message is delivered to the device
   anyway, **Then** the device applies no input and records the rejection.
2. **Given** a FullControl session, **When** a control message arrives that the
   server did not issue for that session, **Then** the device rejects it.
3. **Given** a session whose server authorization has been withdrawn, **When** any
   further control message arrives, **Then** the device applies none of them.
4. **Given** a rejected control message, **When** the administrator reviews the device
   status, **Then** the rejection is visible without exposing session secrets.

---

### User Story 5 - Existing API integrations get a safe, explicit path (Priority: P3)

An integrator who uses the product's API to open remote sessions is told, in a documented
and dated way, what changes: sessions created without an explicit access mode are now
ViewOnly. Integrators who need control are told how to request it and what they must
satisfy. Existing integrations are not silently broken by a default that changes
underneath them, and they are not silently left with an unsafe default either.

**Why this priority**: A secure default that silently changes behaviour breaks
integrations; preserving the old default silently keeps the vulnerability. Both are
unacceptable, and the resolution has to be explicit and dated.

**Independent Test**: Run an integration that creates a session through the API without
requesting an access mode, and confirm it receives a ViewOnly session plus a clear,
machine-readable indication of the change rather than control or a silent failure.

**Acceptance Scenarios**:

1. **Given** an API client creates a session without specifying an access mode,
   **When** the session is returned, **Then** the session is ViewOnly and the response
   states the access mode that was applied.
2. **Given** an API client that needs control, **When** it requests FullControl with a
   valid authenticated identity, **Then** control is granted only if the documented
   preconditions are satisfied.
3. **Given** a deployment upgrading from a version where the default was different,
   **When** the operator reads the migration guidance, **Then** the behaviour change,
   its date, the affected request shape, and the remediation steps are all stated.

---

### Edge Cases

- A viewer holds a valid link issued while the session was FullControl, then the
  session is downgraded to ViewOnly — the stale link must not carry control authority.
- An administrator's device-group membership is removed while a FullControl session is
  open — control authority must not survive the removal.
- The same device is reachable through two different sessions with different access
  modes — each session's authority must be evaluated separately.
- A session is switched to a Windows session where no employee is logged on.
- A control message arrives after the session ended, or after the device restarted the
  session helper.
- A control message arrives for a session id the device has never seen.
- The server cannot evaluate the request because the organization scope, the device
  record, or the consent state cannot be read — the request must fail closed.
- Two administrators request FullControl for the same device at the same time.
- A viewer-supplied access mode is present but malformed or unrecognised.
- An API client sends an access mode value that exists but is not permitted for its
  identity — refused, and recorded as refused.

## Requirements *(mandatory)*

### Functional Requirements

**Access mode model**

- **FR-001**: The system MUST represent a remote session's access authority as an
  explicit, server-controlled access mode with exactly two supported values: ViewOnly
  and FullControl.
- **FR-002**: A session's access mode MUST be owned by server-side session state. The
  browser, the viewer, the link, and the device MUST NOT be able to set, widen, or
  influence it.
- **FR-003**: When a session is created without an explicitly requested access mode,
  the system MUST assign ViewOnly.
- **FR-004**: The system MUST distinguish an explicitly requested access mode from an
  absent one; an absent mode MUST never be interpreted as FullControl.

**FullControl authorization**

- **FR-005**: FullControl MUST be granted only when all of the following are
  satisfied and independently evaluated at the time of the request: an explicit
  request; an authenticated caller identity; a server-side authorization decision
  granting control; device-membership scope for the caller's organization; and any
  attended-access consent that applies to the session. [NEEDS CLARIFICATION: is the
  existing attended-access consent sufficient as-is for a FullControl request, or
  must FullControl require a distinct, elevated consent step that cannot be satisfied
  by the same consent that permits viewing?]
- **FR-006**: Possession of an access key, a viewer link, or a URL parameter MUST NOT
  by itself grant FullControl.
- **FR-007**: A user-interface control that appears to offer control MUST NOT grant
  control unless the server has independently authorized FullControl for the session.
- **FR-008**: The system MUST record each privilege decision — the session, the
  requested mode, the decision, the authenticated identity that made the request, the
  time, and the outcome of each precondition check.
- **FR-009**: The system MUST refuse, and record as refused, any request whose
  preconditions cannot all be evaluated or satisfied. Authorization failures MUST fail
  closed.

**Message authorization**

- **FR-010**: Every remote-control message MUST be authorized against the server-owned
  access mode of the session it belongs to before it is forwarded.
- **FR-011**: Control messages for a ViewOnly session MUST be refused at the server,
  and the refusal MUST be observable by the administrator.
- **FR-012**: The server MUST forward a control message only to the session that the
  message is bound to; a message bound to one session MUST NOT be applied to another.

**Session lifecycle**

- **FR-013**: Changing the target Windows session, cloning a session, reconnecting a
  viewer, and generating a viewer link through the API MUST NOT upgrade a session from
  ViewOnly to FullControl.
- **FR-014**: A clone or a new session MUST carry its own authorization decision and
  its own audit record.
- **FR-015**: When the authorization basis for a session changes (organization scope,
  device-group membership, or consent state), the system MUST re-evaluate control
  authority and withdraw it when the basis no longer holds.
- **FR-016**: A session's access mode MUST survive the viewer reconnecting with an
  earlier-issued link without inheriting any authority that the server record no
  longer grants.

**Device-side defence in depth**

- **FR-017**: The device MUST independently verify each remote-control message against
  the authority the server granted for that session, and MUST apply input only when
  that verification succeeds.
- **FR-018**: The device MUST reject control messages that are unauthorised, replayed,
  stale, or bound to an unknown session, and MUST record the rejection.
- **FR-019**: A rejection on the device MUST NOT terminate or degrade the viewing
  session.
- **FR-020**: The device MUST NOT apply control on the basis of a locally held or
  client-supplied value; its decision MUST derive from the authority the server
  granted for the session.

**Secure API session creation**

- **FR-021**: The API MUST allow a caller to request an explicit access mode, and
  MUST record the requested mode, the authenticated identity, and the decision.
- **FR-022**: The API MUST return the access mode that was actually applied in the
  session response.
- **FR-023**: The API MUST NOT offer a mode that the caller's identity or scope is
  not permitted to hold.
- **FR-024**: API-generated viewer links MUST NOT carry or imply control authority, and
  a link MUST NOT be sufficient to obtain it.

**Compatibility and migration**

- **FR-025**: The system MUST document the behaviour change for existing API
  integrations whose default session mode changes from control-capable to ViewOnly,
  including the effective date, the affected request shape, and the remediation steps.
  [NEEDS CLARIFICATION: what migration shape is required — must the product ship a
  versioned endpoint or request field so existing integrations keep their previous
  behaviour for an explicit transition window, or is a single dated behaviour change
  with an opt-in upgrade flag sufficient? The answer determines whether the migration
  is backward-compatible by construction or only by documented procedure.]
- **FR-026**: The migration guidance MUST NOT preserve the previous unsafe default
  silently; integrations that need control MUST be required to request it explicitly
  and satisfy the documented preconditions.
- **FR-027**: The system MUST provide a machine-readable indication of the applied
  access mode on session creation responses so that integrations can detect the change
  without inspecting logs.

**Auditability**

- **FR-028**: The system MUST make granted and refused privilege decisions
  retrievable per session and per device by an authorized administrator.
  [NEEDS CLARIFICATION: what is the retention window and the sink for
  privilege-decision records — does the existing operational log store satisfy the
  retrieval requirement, or must the decisions be persisted in a queryable
  per-session record with a stated minimum retention period? The answer determines
  whether SC-009 is answerable from the product itself or only from exported logs.]
- **FR-029**: Audit records MUST NOT expose session secrets such as access keys.

**Out of Scope**

- Screen capture scheduling, screen recording, webcam recording, and media upload.
- Any integration or implementation of the ScreenRecorderLib capture engine.
- The capture consent model, which is specified separately under the constitution's
  consent principle.
- The capture helper process, its launch mechanism, its IPC design, and its lifecycle.
- Hardening the client update path, file-transfer path boundaries, and the local IPC
  channel, which are separate constitution release gates.

### Key Entities

- **Remote Session**: a single remote-support engagement between one or more viewers
  and one device. Carries the server-owned access mode, the authorizing identity, the
  device and organization scope it was granted under, its lifecycle state, and the
  current target Windows session.
- **Access Mode**: the explicit, server-controlled authorization attribute of a remote
  session, with exactly two values, ViewOnly and FullControl. Owned by the server and
  never supplied by a viewer.
- **Privilege Decision**: the recorded outcome of evaluating a request for access
  authority. Records the session, the requested mode, the authenticated identity, the
  time, each precondition's outcome, and the final decision.
- **Device**: the managed endpoint, which holds the device-side authorization state
  used to refuse control the server did not authorize.
- **Device Group**: the membership set that establishes whether an administrator may
  act on a given device.
- **Organization**: the tenant boundary that scopes both devices and administrators.
- **Attended-Access Consent**: the record that the employee present at the device
  agreed to the session, required when the deployment enables attended access.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of remote sessions created without an explicit access-mode request
  are ViewOnly, verified by an automated test over every session-creation route.
- **SC-002**: Zero control actions reach the device from a ViewOnly session in the
  negative test suite: for every distinct remote-control message type, the device
  applies no input.
- **SC-003**: 100% of privilege decisions — granted and refused — are recorded with
  identity, time, and precondition outcomes, verified by an automated test that
  reconciles the decision log against the set of requests made.
- **SC-004**: Every session lifecycle event — Windows-session switch, clone,
  reconnect, and API-generated link — leaves a ViewOnly session ViewOnly in 100% of
  automated cases.
- **SC-005**: FullControl is granted in 0% of automated attempts that omit any one of
  its preconditions, and in 100% of attempts that satisfy all of them.
- **SC-006**: A viewer-side modification — altered link, modified page state, or
  self-declared mode — changes the session's access mode in 0% of automated cases.
- **SC-007**: The security test suite for this feature runs on every pull request in
  continuous integration, with results visible, and the build fails when any security
  test fails.
- **SC-008**: Existing integrations that relied on implicit control are identified and
  documented with their migration step before the changed default is released.
- **SC-009**: An administrator can answer, for any session in the retention window,
  "who granted control, when, and on what basis" without consulting source code or
  raw logs.
- **SC-010**: No device-side state is required to reconstruct the authority of a
  control message other than the authority the server granted for that session.

## Assumptions

- The existing authentication, organization, and device-group model remains in place
  and is reused rather than replaced.
- The existing attended-access consent mechanism is reused for sessions where the
  deployment requires it.
- The device is capable of receiving an authorization statement from the server for a
  session, and of refusing control that statement does not cover.
- Administrator identity, organization scope, and device-group membership are already
  available to the server at the moment a session is created or a privilege is
  requested.
- Sessions are short-lived enough that withdrawing authority at the next server
  evaluation is an acceptable enforcement latency.
- The deployment's administrators are authenticated users of the product, so an
  explicit, authorized request for control is an available interaction.
- No behaviour of the capture subsystem is assumed by this specification; it is
  specified separately.

### Constitution Compliance Review

Required by the Constitution v2.0.0 §Compliance: every specification produced under
this constitution must include a compliance review against each principle and each
gate in Principle XII. This review covers the specification text only. It records
**coverage of the requirement**, never that a gate has passed — under Principle XIII,
a passing test would establish `[TEST-EVIDENCE]` only, and no gate in this feature
has been exercised.

**Principles**

| Principle | Compliance | Where |
|---|---|---|
| I — Extend, Do Not Rewrite | **Compliant.** This specification adds a new authorization concept and changes no existing subsystem's responsibility. No fork, no divergence. | FR-001, FR-002, FR-021…FR-024 |
| II — Strengthen Authorization, Preserve Function | **Compliant.** Remote-control functionality stays intact; the view-only flag stops being a client-side hint and becomes a server-enforced authorization boundary. | FR-001…FR-012, SC-001, SC-002, SC-005 |
| III — Consent First, Visible, Fail-Closed | **Partial.** The existing attended-access consent is reused rather than redesigned. A distinct elevated consent for FullControl is **not yet decided** (see FR-005). Fail-closed behaviour is explicit. | FR-005, FR-009, FR-019, Edge Cases |
| IV — Windows Session Isolation | **Not applicable.** This specification performs no capture. It does require that changing the target Windows session never widens authority. | FR-013 |
| V — Capture Identity and Least Privilege | **Not applicable to the capture helper, which is out of scope.** The frozen remote-support process identity is respected: this specification adds no privilege to the existing interactive process. | Out of Scope; D5 |
| VI — Reliable Transfer, Bounded Storage | **Not applicable.** No media transfer, no local capture queue, no storage budget in this feature. | Out of Scope |
| VII — Performance Budgets | **Not applicable.** No capture pipeline. The performance-sensitive behaviour here is the cost of refusing a control message, which is bounded by SC-002's outcome rather than by a throughput target. | SC-002 |
| VIII — Minimal Dependencies | **Compliant.** No new dependency is introduced by this specification. | FR-001…FR-029 (no dependency) |
| IX — ScreenRecorderLib Compatibility | **Not applicable.** ScreenRecorderLib and all capture-engine work are excluded. | Out of Scope |
| X — Test-First and Regression Protection | **Committed, not yet executed.** The specification mandates a negative security suite and a FullControl regression suite that must run in continuous integration. No test has been written or executed under this specification. | SC-002, SC-005, SC-007 |
| XI — Licence and Windows Compatibility | **Compliant.** No new code, no new packaging, no new deployment obligation arises from this specification. | Out of Scope |
| XII — Production Security Release Gates | **Partial.** Gate G1 is this specification's scope, and the device-action half of G4 is as well. G2, G3, G5, G6 and G7 are independent gates outside this scope. | §12 of `CONSTITUTION-REVIEW.md` |
| XIII — Evidence-Based Decisions | **Compliant.** This specification asserts no verified fact about the current product's behaviour. All historical findings live in `REVERSE-ENGINEERING-REPORT.md` with their evidence labels; this document states requirements only. | FR-001…FR-029, Success Criteria |

**Gates**

| Gate | Applicability | Status |
|---|---|---|
| G1 — Server-enforced View Only | **In scope.** This specification is the remediation for G1. | ⛔ Not passed — specification only; no implementation, no test evidence |
| G2 — Signed and verified client updates | Independent gate. | ⛔ Open — not addressed here |
| G3 — Safe file paths and transfer boundaries | Independent gate. | ⛔ Open — not addressed here |
| G4 — Authenticated and authorized device actions | **In scope, partially.** Authorizing remote-control actions server-side against organization and device-group scope is this specification's core. The capture-helper half of the gate belongs to SPEC-002. | ⛔ Not passed — specification only |
| G5 — Authenticated local IPC with least privilege | Independent gate; the service↔session channel is a SPEC-002 concern. | ⛔ Open — not addressed here |
| G6 — Verified consent and privacy controls | Independent gate; per-capability consent belongs to the capture work. Reusing the existing consent mechanism does not close it. | ⛔ Open — not addressed here |
| G7 — Privileged process isolation | Independent gate; belongs to the capture work. | ⛔ Open — not addressed here |

**Additional pre-production conditions named in Principle XII.** Two conditions sit
outside the seven numbered gates: the unauthenticated endpoints
(`ClientDownloadsController`, `DevicesController`) must be closed or explicitly
accepted in writing by the owner, and the open remote-control path left when
`RemoteControlRequiresAuthentication` is disabled must be closed in production.
Both bear directly on this specification's subject matter — FR-021…FR-024 and
FR-009 — and both remain open until the specification is implemented and the
behaviour is verified.

**Reconciliation.** No principle or gate conflicts with this specification. The two
partial entries (Principle III, Principle XII) are pending decisions and independent
gates respectively — not unresolved conflicts. Per the Constitution's §Compliance, a
specification that cannot be reconciled is rejected; this one can, with the two
partial entries carried as explicit obligations.
