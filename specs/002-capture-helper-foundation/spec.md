# Feature Specification: Capture Helper Foundation, Employee Token & Authenticated IPC

**Feature Branch**: (none — no git branch is created; the feature is located through `.specify/feature.json`.)

**Created**: 2026-11-06

**Status**: Draft

**Input**: SPEC-002: Capture Helper Foundation, Employee Token & Authenticated IPC. The smallest end-to-end path: the server instructs a device, a helper process runs inside the employee's Windows session under the employee's own identity (never SYSTEM), captures exactly one screenshot after a clear, visible consent interaction with the employee, and returns the result to the server. This establishes the helper's packaging inside the existing client installer, its launch mechanism, the authenticated session-scoped IPC channel, and the employee-identity proof. Scheduling, video/audio/webcam capture, resumable upload, and the full consent model are excluded.

## User Scenarios & Testing *(mandatory)*

<!-- User stories are prioritized as independent journeys. Each one is independently
testable and delivers a viable slice on its own. -->

### User Story 1 - A screenshot is captured under the employee's identity, never SYSTEM (Priority: P1)

An administrator requests a single screenshot of a device. The device service launches a helper process inside the employee's logged-on Windows session, and that helper runs with the employee's own user token — with a real logon SID, in the employee's session, on the employee's desktop. The helper captures one screenshot and returns it. An identity probe recorded at capture time proves the process token: user SID, logon session, session ID, and non-elevation. If the employee's session token cannot be obtained, the capture is refused — the device never falls back to running the helper as SYSTEM.

**Why this priority**: This is the correction of the single most serious ratified finding (G7: the current interactive support process runs as SYSTEM with no logon SID). Every capture feature afterwards stands on this identity boundary.

**Independent Test**: Trigger one capture on a test device, then read the identity facts recorded with the capture: token user SID equals the logged-on employee's SID, a logon SID exists, session ID matches the interactive session, and the process is not elevated. Repeat while the device has no logged-on employee and confirm the request is refused.

**Acceptance Scenarios**:

1. **Given** a device with an employee logged on, **When** a single capture is requested and consented, **Then** the helper process token carries the employee's user SID, a non-empty logon SID, the interactive session ID, and is not elevated.
2. **Given** a device with no employee logged on (or at the secure desktop), **When** a capture is requested, **Then** the request is refused with a recorded reason and no capture occurs.
3. **Given** any failure to obtain the employee's token (token duplication failure, profile load failure), **When** the launch path evaluates, **Then** the capture is refused and the failure is recorded; no SYSTEM-launched fallback runs.

### User Story 2 - The employee grants once, sees every capture, and can revoke anytime (Priority: P1)

The first time a capture is requested on a device, the employee sees a clear, blocking request on their own screen — who is asking, and what capability is being granted (screen capture). They can accept or refuse. If they accept, the grant is recorded once for that employee on that device and later captures do not re-ask. **Every actual capture still produces a visible, non-blocking notification on the employee's screen while it happens**, and the employee can revoke the grant locally at any time; from that moment captures are refused until a new grant is given. A refused grant, an expired unanswered grant prompt, or an inability to show the prompt means no capture, and the requester is told.

**Why this priority**: Consent-first is a constitution principle (III): permission must be prior, specific, visible in effect, and revocable. The owner's decision (one-time grant, open requester policy) is implemented exactly in this envelope: grant once, notify always, revoke instantly.

**Independent Test**: Trigger a first capture; confirm the blocking grant prompt appears, accept it, and confirm the capture proceeds with a visible on-screen notification. Trigger again; confirm no re-prompt but the notification still appears. Revoke locally; confirm the next request is refused. Repeat the first-capture flow and refuse or ignore the prompt; confirm no capture and no grant stored.

**Acceptance Scenarios**:

1. **Given** a device with no stored grant, **When** a capture is requested, **Then** a blocking grant prompt appears on the employee's desktop; on acceptance the grant is stored once and exactly one capture is taken with a visible notification.
2. **Given** a stored grant, **When** a capture is requested, **Then** no grant prompt appears, the capture proceeds, and a visible non-blocking notification appears on the employee's screen during the capture.
3. **Given** a stored grant, **When** the employee revokes it locally, **Then** the next capture request is refused and the revocation is recorded.
4. **Given** a pending grant prompt, **When** the employee refuses or does not respond within the stated timeout, **Then** no grant is stored and no capture occurs; the requester is told it was refused.
5. **Given** the employee's desktop is locked or at the secure desktop, **When** a capture is requested, **Then** it is refused (a grant cannot be honestly given there, and a stored grant cannot be exercised from a session that cannot show the notification).

### User Story 3 - The server-to-helper channel is authenticated and session-scoped (Priority: P2)

The device service and the in-session helper exchange messages only over a channel where both ends verify the other. The channel is bound to the specific employee session and the specific capture request: a message meant for one session or one request is not accepted by another. The channel's name and credentials are not passed in a way that other local processes can harvest (for example, a bare secret on a command line). A message that fails authentication or binding is rejected and recorded, without disturbing anything else.

**Why this priority**: The existing local channel is the known weakest point (broad default pipe ACL, unauthenticated client, connection identifier on a command line). If the foundation copies that weakness, the SYSTEM-identity fix is undermined from the inside.

**Independent Test**: From a second local process (simulating a hostile local peer), attempt to connect to the channel and to replay a captured message; confirm both are rejected and recorded. Confirm a request bound to session A is rejected if delivered to session B.

**Acceptance Scenarios**:

1. **Given** the channel is listening, **When** an unauthenticated local process connects, **Then** the connection is rejected and the attempt is recorded.
2. **Given** a valid message was observed once, **When** it is replayed later or on a different request, **Then** it is rejected as stale or unbound.
3. **Given** two employee sessions on one device, **When** a request bound to session A is delivered to session B's helper, **Then** it is rejected.

### User Story 4 - The helper ships and runs without changing what the administrator installs (Priority: P3)

The helper is delivered inside the existing client installer/update package. An administrator updates the device once and gets the new capability; no second installer, no separate service registration. The helper starts on demand, cleans up after itself, and leaves no orphan processes or stale artifacts behind after a capture completes, fails, or the session ends.

**Why this priority**: The product baseline forbids installing two programs on managed devices. The foundation must prove the delivery model before any capture feature scales.

**Independent Test**: Install the updated package on a clean test device, run one capture, then inspect process list and disk: no helper process remains, exactly one installed program entry exists, and the update succeeded through the existing signed-update path.

**Acceptance Scenarios**:

1. **Given** a device running the existing client, **When** the updated package (signed, per the existing update verification) is applied, **Then** the helper capability exists with no additional installer or service entry.
2. **Given** a completed, failed, or refused capture, **When** the helper finishes, **Then** no helper process remains and temporary artifacts are removed.
3. **Given** the employee logs off while the helper is alive, **Then** the helper terminates with the session and the device service records the session end.

---

### Edge Cases

- A capture is requested while the employee is at a UAC/secure desktop — refused, never answered silently.
- Two capture requests arrive for the same device at the same moment — serialized or second refused; never two simultaneous helpers per session (single-instance rule).
- The employee accepts but the capture itself fails (graphics stack error) — the failure is recorded and reported as a capture failure, not as a success without data.
- The device service restarts mid-request — the pending request is invalidated; the helper must not honor a stale request after restart.
- The helper is started but the requesting server connection drops before the capture completes — the helper aborts and cleans up.
- A capture result is delivered but the server never confirms receipt — the device records non-receipt (delivery guarantees are specified for later media features; this spec only requires the result not be silently lost).
- An employee with multiple concurrent Windows sessions — the request targets one session; the others are untouched.
- The channel endpoint name/credential is discovered by another local process — the credential must be single-use and session-bound so discovery alone is insufficient.

## Requirements *(mandatory)*

### Functional Requirements

**Helper identity and launch**

- **FR-001**: The capture helper MUST run inside the employee's interactive Windows session under the employee's own user token, with a resolvable logon SID, and MUST NOT run as SYSTEM or with elevation.
- **FR-002**: The launch path MUST obtain the employee's session token through the ratified identity mechanism (employee-session token acquisition with profile load; see Assumptions) and MUST refuse the capture when that acquisition fails, with the refusal recorded.
- **FR-003**: Every capture MUST be accompanied by a locally recorded identity record: token user SID, logon SID, session ID, elevation state, and launch mechanism. The record MUST be retrievable by an authorized administrator and MUST NOT contain session secrets.
- **FR-004**: When no employee session is available, or the target is the secure desktop, the device MUST refuse the capture and record the reason.
- **FR-005**: The helper MUST be delivered inside the existing client installer and updated through the existing signed-update path; it MUST NOT require a separate installer, service registration, or manual step.

**Consent model (owner decisions D2/D4/D5, 2026-11-06)**

- **FR-006**: The first capture request on a device for a given employee MUST present a blocking grant prompt on the employee's own desktop stating the requester identity and the capability being granted (screen capture), with accept and refuse controls. A stored grant eliminates the blocking prompt for later captures.
- **FR-007**: A refusal, an expiry after a bounded non-response timeout, or an inability to show the grant prompt MUST each result in no capture and no stored grant, with the outcome recorded and reported to the requester.
- **FR-008**: A grant prompt MUST NOT be answerable from the secure desktop, a locked session, or by any process other than the employee's own interaction; programmatic auto-acceptance MUST NOT be possible.
- **FR-009**: Every actual capture MUST produce a visible, non-blocking notification on the employee's screen during the capture, regardless of any stored grant. A capture that cannot show the notification in the employee's session MUST NOT proceed.
- **FR-010**: The employee MUST be able to revoke the stored grant locally at any time; revocation takes effect for the next capture request, is recorded, and requires a new blocking grant for captures to resume.
- **FR-011**: The stored grant is scoped to one employee account on one device, survives restarts, and is never transferrable between employees or devices.
- **FR-012**: The full consent and privacy model — per-capability grants, persistent tray indicator, and the local privacy pause that overrides any stored grant — is specified separately (SPEC-003) and MUST NOT be assumed by this spec; until SPEC-003 lands, the notification and revocation requirements above are the operative privacy controls.

**Authenticated, session-scoped IPC (owner decision D1: named-pipe channel with per-session ACL and a challenge authenticated by a service-issued single-use token)**

- **FR-013**: All messages between the device service and the helper MUST travel over a local channel that authenticates both endpoints before accepting any message, and that enforces the least-privilege access rules of the local platform.
- **FR-014**: Every message MUST be bound to the specific employee session and the specific capture request; a message replayed later, delivered to a different session, or not matching the outstanding request MUST be rejected and recorded.
- **FR-015**: The channel endpoint name and any channel credential MUST NOT be disclosed in a form harvestable by other local processes (for example, a bare secret on a process command line). The identifier passed at helper launch MUST be single-use and insufficient on its own to authenticate; mutual authentication is completed by the challenge over the channel itself.
- **FR-016**: A message that fails authentication or binding MUST be rejected without degrading or terminating any ongoing capture for other sessions; the rejection MUST be recorded.

**Single-capture path**

- **FR-017**: A capture request MUST produce exactly one screenshot for the target session's primary display, or a recorded failure — never partial or duplicated results.
- **FR-018**: The device MUST enforce at most one outstanding capture request per employee session; concurrent requests are refused or serialized by a recorded policy, and never run as parallel helper instances in the same session.
- **FR-019**: On completion, refusal, failure, or session end, the helper MUST terminate and remove its temporary artifacts; no helper process or capture artifact may outlive the request. The helper is launched on demand per request; any persistent resident component (tray, indicator) belongs to SPEC-003.
- **FR-020**: The capture result MUST reach the server and the device MUST record the delivery outcome (delivered / not confirmed). Silent loss is prohibited; retry semantics for bulk media are out of scope (later media specs).

**Observability, auditability, and requester policy**

- **FR-021**: Every request, grant outcome, notification delivery, capture result, failure, revocation, and channel rejection MUST be recorded locally with timestamp, session binding, requester identity, and outcome — without capture content and without session secrets.
- **FR-022**: An authorized administrator MUST be able to answer, for any recorded capture, "who requested it, what did the employee grant, and under what identity did it run" without reading source code or raw memory.
- **FR-023**: Per the owner's requester policy (D5: any connected session may request), the server MUST record the requester's identity for every request and MUST refuse a request when the requester's identity cannot be established or when the configured requester policy cannot be evaluated (fail-closed).
- **FR-024**: A stored grant MUST be revocable by the employee locally at any time (see FR-010), and the revocation MUST be visible to an authorized administrator through the recorded audit trail.

### Key Entities

- **Capture Helper**: the in-session process performing exactly one capture per invocation of its duty; runs under the employee token; dies with the request or the session.
- **Employee Session Context**: the identity facts of the target session — user SID, logon SID, session ID, elevation state, desktop state — recorded per capture.
- **Consent Grant**: the stored, once-given permission for one employee account on one device; carries grant timestamp, granter identity, and revocation state; revocable locally at any time.
- **Capture Notification**: the visible, non-blocking on-screen indication shown during every actual capture, independent of the stored grant.
- **Capture Request**: a server-originated instruction bound to one device, one employee session, and one capture; carries requester identity and a single-use binding credential.
- **Capture Result**: the single screenshot payload plus its delivery status as seen by the device.
- **IPC Channel**: the authenticated, session-scoped local channel between the device service and the helper, with per-message binding, replay rejection, and challenge-based mutual authentication.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In 100% of automated capture attempts on a logged-in test device, the helper's recorded identity shows the employee's user SID, a non-empty logon SID, the interactive session ID, and non-elevation.
- **SC-002**: In 100% of automated attempts with no logged-on employee or with a secure-desktop target, the capture is refused and the reason recorded; zero captures occur.
- **SC-003**: In 100% of automated refusal, expiry, and unpromptable cases, no capture content is produced and the outcome is recorded and reported to the requester.
- **SC-004**: In 100% of automated hostile-local-peer tests (unauthenticated connect, replay, cross-session delivery), the channel rejects and records the attempt; zero accepted messages.
- **SC-005**: In 100% of automated update runs, the helper capability arrives through the existing signed-update path with exactly one installed program entry and no separate service registration.
- **SC-006**: In 100% of completed, failed, refused, and logoff cases, zero helper processes and zero capture artifacts remain on the device afterwards.
- **SC-007**: The security test suite for this feature runs on every pull request in continuous integration, with results visible, and the build fails when any security test fails.
- **SC-008**: An authorized administrator can retrieve the recorded identity, request, grant outcome, and delivery outcome for any capture in the retention window without consulting source code or raw logs.
- **SC-009**: In 100% of automated captures under a stored grant, a visible notification is shown in the employee's session during the capture; in 100% of post-revocation requests, the capture is refused.

## Resolved Owner Decisions (2026-11-06)

| # | القرار | القرار المُعتمد | أثره في هذه المواصفة |
|---|---|---|---|
| **D1** (يحسم OD-2 جزئيًا) | آلية IPC | **(أ) Named pipe + ACL لكل جلسة + تحدي مصادقة بـtoken أحادي الاستخدام صادر من الخدمة** | FR-013…FR-016 — يغلق ثغرة `--pipe-name` على سطر الأوامر (SEC-05) |
| **D2 + D4** | دورة الحياة + نموذج الموافقة | **الصلاحية مرة واحدة في العمر**: منحة حاجبة واحدة لكل موظف/جهاز تُخزَّن، والإشعار المرئي عند كل التقاط، والإلغاء المحلي الفوري؛ عملية المساعد تنطلق عند الطلب وتنتهي بعده | FR-006…FR-012، FR-019، SC-009 — المقيم الدائم (Tray) مؤجل لـSPEC-003 |
| **D3** (يحسم OD-4) | مصدر محرك الالتقاط | **(أ) NuGet 7.0.1** — الدليل الحي E-49 يدعمه، مع تثبيت الإصدار | Assumptions؛ الـADR الرسمي يصدر بهذا القرار |
| **D5** | سياسة الطالب | **(ب) أي متصل بالجلسة قد يطلب** — بلا اشتراط مصادقة مدير | FR-023 — **مع ملاحظة اعتراض مدقق مسجلة**: القرار نُفِّذ داخل الحد الدستوري الإلزامي (إشعار لكل التقاط FR-009 + إلغاء فوري FR-010 + audit FR-021)؛ دون هذا الغلاف تصطدم المواصفة بـPrinciple III (إذن محدد وقابل للإلغاء ومؤشر ظاهر) |
| **D-defaults** | ما حسمه المهندس بسلطته | لقطة واحدة للشاشة الرئيسية · fail-closed في كل حالات الفشل · لا SYSTEM fallback · مساعد واحد كحد أقصى لكل جلسة · التسليم داخل الـinstaller عبر التحديث الموقّع | Assumptions + FR-001…FR-005 |

## Assumptions

- The ratified identity decision (AD-15) holds: the helper is launched by acquiring the employee's session token directly (session-token acquisition plus user-profile load), and the secure desktop is treated as a refusal condition, not as a SYSTEM-execution opportunity.
- The existing signed-update chain (update signature verification before execution) is live and trusted by the time this feature ships; the helper MUST NOT be delivered through an unverified path.
- The capture engine itself is replaceable behind an abstraction; this specification depends on a working capture capability (single screenshot), not on a specific engine product. The compatibility probe of the candidate engine has passed on the reference environment (EVIDENCE-MAP E-49).
- The device service remains a Session-0 service responsible for server connectivity, request intake, and recording; it performs no capture itself.
- "One screenshot" means the primary display of the target session at the moment of capture; multi-display selection belongs to later media specifications.
- The employee prompt is presented by the helper (or its session-side agent) on the employee's desktop; the device service does not answer prompts on the employee's behalf.
- Sessions are human-attended for this spec: attended-access consent flows already exist for remote control and are reused conceptually; SPEC-003 owns the full consent and privacy model.
- The local platform's named-pipe access-control mechanism (already hardened for the existing chat channel) is the floor for channel access rules; this spec requires endpoint mutual authentication on top of that floor.

## Out of Scope

- Scheduled or recurring capture of any kind (later scheduling specification).
- Screen video, microphone audio, system audio, webcam photos or video (later media specifications).
- Live View source toggles during remote-control sessions (later Live View specification).
- Resumable upload, device-side media queue, storage budgets, and retry semantics for bulk media (later upload specification).
- The full consent model: per-capability permissions, persistent tray indicator, local privacy pause, and revocation flows (SPEC-003).
- Multi-display selection, display choice, and DPI-specific handling beyond the primary display.
- Server-side media library, retention, and purge (later library specification).

## Constitution Compliance Review

Required by Constitution v2.0.0 §Compliance. This review covers the specification text only; it records coverage of the requirement, never that a gate has passed.

| Principle | Compliance | Where |
|---|---|---|
| I — Extend, Do Not Rewrite | **Compliant.** Adds a helper capability inside the existing client package and service; no subsystem replacement. | FR-005, FR-014 |
| II — Strengthen Authorization, Preserve Function | **Compliant.** Adds server-side scope authorization and authenticated IPC; the remote-support function is untouched. | FR-010…FR-013, FR-020 |
| III — Consent First, Visible, Fail-Closed | **Compliant — داخل غلاف إلزامي.** قرار المالك (منحة مرة واحدة + سياسة طالب مفتوحة) نُفِّذ مع الإشعار المرئي عند كل التقاط والإلغاء المحلي الفوري (FR-009/FR-010/FR-024)؛ بغيرهما لكانت المواصفة مخالفة لهذا المبدأ. الفشل = رفض مسجّل في كل الحالات. | FR-006…FR-012, SC-003, SC-009 |
| IV — Windows Session Isolation | **Compliant — this is the spec's core.** Helper runs only in the employee session under the employee token; secure desktop refused; per-session binding of all messages. | FR-001…FR-004, FR-011, SC-001/SC-002 |
| V — Capture Identity and Least Privilege | **Compliant.** Employee token identity with recorded proof; no elevation; least-privilege channel access. | FR-001, FR-003, FR-010 |
| VI — Reliable Transfer, Bounded Storage | **Partial.** Single-frame result with recorded delivery outcome; bulk transfer semantics explicitly deferred. | FR-017, Out of Scope |
| VII — Performance Budgets | **Partial.** Single short-lived helper per request with cleanup; sustained-load budgets belong to the scheduling/media specs. | FR-015, FR-016 |
| VIII — Minimal Dependencies | **Compliant.** No new dependency is mandated by this specification; the engine sits behind an abstraction and the probe result (E-49) is evidence, not a mandate. | Assumptions |
| IX — ScreenRecorderLib Compatibility | **Not decided here.** The PoC passed (E-49); adoption/deferral is an owner decision (OD-4) recorded separately. | Assumptions |
| X — Test-First and Regression Protection | **Committed, not yet executed.** Negative suites for identity, consent, and channel are mandated (SC-001…SC-004, SC-007); no test exists under this spec yet. | SC-001…SC-007 |
| XI — Licence and Windows Compatibility | **Compliant.** Helper ships inside the existing package; engine licence decision rides on OD-4. | FR-005 |
| XII — Production Security Release Gates | **Partial.** Directly serves G7 (privileged process isolation) and G5 (authenticated local IPC); G6's full consent model remains SPEC-003. | G5, G7 |
| XIII — Evidence-Based Decisions | **Compliant.** Identity and channel claims above are marked as requirements, not as verified behavior; the only runtime facts cited (E-49 PoC, G7 PoC) are labeled with their evidence class. | throughout |

### Gates

| Gate | Applicability | Status |
|---|---|---|
| G5 — Authenticated local IPC | **In scope.** The channel requirements here are the G5 remediation for the capture path. | ⛔ Not passed — specification only |
| G7 — Privileged process isolation | **In scope (remediation).** FR-001…FR-004 + identity records are the G7 correction for the capture helper (the existing remote-support path remains a separate recorded failure). | ⛔ Not passed — specification only; the current-state failure is already measured (G7 PoC) |
| G6 — Consent and privacy | **Partial overlap.** One-time grant + per-capture notification + revocation here; the gate's full scope (per-capability consent, tray indicator, privacy pause) is SPEC-003. | ⛔ Partially addressed — FR-006…FR-012 |
| G1…G4 | Independent — remediated or pending elsewhere. | Out of scope here |
