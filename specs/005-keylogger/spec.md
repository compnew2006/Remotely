# Feature Specification: Keyboard Capture

**Feature Branch**: (none — no git branch is created; the feature ships on `master` like the previous owner-directed features)

**Created**: 2026-11-07

**Status**: Draft — implementation and local builds plus 157 unit tests verified; the capture has **not** been exercised on a device, and the feature is **not formally accepted** (no `[RELEASE-GATE]` or CI run)

**Input**: SPEC-005: Keyboard Capture. An administrator turns the capture of the typed text on for a device. The desktop client of that device captures what is typed in the session of the user, sends it to the server while it is being typed, and the administrator reads it live in the browser and in the history that is kept on the server.

---

## User Scenarios & Testing

### User Story 1 — An administrator captures the text that is typed on a device (Priority: P1)

An administrator opens the keyboard of a connected device and turns the capture on. The device sends what is typed to the server while it is typed, and the administrator sees it appear in the browser within a second or two. The capture keeps running until it is turned off, and it survives a restart of the server.

**Why this priority**: It is the whole point of the feature — the text of a remote machine, live.

**Independent Test**: Turn the capture on for a device, type a sentence in Notepad on that device, and confirm the sentence appears in the browser and in the history.

**Acceptance Scenarios**:

1. **Given** a device that is online, **When** the administrator turns the capture on, **Then** the device starts capturing and the text appears in the browser while it is typed.
2. **Given** a device that is offline, **When** the capture is turned on, **Then** the administrator is told that the device is not online, and the setting is kept for the next time the device comes online.
3. **Given** a capture that is running, **When** the server is restarted, **Then** the capture of every device that has it enabled starts again when the devices come online.
4. **Given** a capture that is running, **When** the administrator turns it off, **Then** the device stops capturing and no more text arrives.

### User Story 2 — The text arrives with the context of where it was typed (Priority: P1)

Every burst of text carries the title of the window that had the focus and the name of the process, so the administrator knows where the text came from.

**Why this priority**: A burst of characters without a window is close to useless for support work.

**Independent Test**: Type in three different applications and confirm that each burst names its own window and process.

**Acceptance Scenarios**:

1. **Given** a burst of text, **When** it is shown, **Then** the title of the window and the name of the process are shown with it.
2. **Given** a change of the focused window, **When** more text is typed, **Then** the text before the change is sent as its own burst, and the text after it starts a new burst.

### User Story 3 — A secret is never stored (Priority: P1)

The text of a field that holds a password is never stored. The administrator sees that something was typed into a password field, and how many characters it had, but not what they were.

**Why this priority**: The captured text is the most sensitive data of the whole product, because it holds the passwords of the users of the machine.

**Independent Test**: Type a password into a password field and confirm that the stored text is a mask and that the entry is marked as a password field.

**Acceptance Scenarios**:

1. **Given** a focused control that holds a password, **When** text is typed, **Then** the text is replaced with a mask, and the entry is marked as a password field.
2. **Given** a credential prompt of Windows, **When** text is typed, **Then** the text is masked as well.
3. **Given** an ordinary field, **When** text is typed, **Then** the text is stored as it was typed.

### User Story 4 — The pasted text is captured as well (Priority: P2)

Text that is pasted with the keyboard is not typed, so it is read from the clipboard instead.

**Why this priority**: A large part of what a user writes is pasted, and a capture that misses it is incomplete.

**Independent Test**: Copy a sentence and paste it into a text field, and confirm that the sentence arrives in the capture.

**Acceptance Scenarios**:

1. **Given** a paste of text, **When** the paste happens, **Then** the text of the clipboard is captured.
2. **Given** a paste of something that is not text, **Then** nothing is captured, and the capture keeps running.

### User Story 5 — The captured text is stored with limits (Priority: P2)

The captured text of a device is stored on the server, the newest entries are kept, and the old ones are deleted automatically. A user who has no access to the device cannot read or delete its captured text.

**Why this priority**: A capture that is never deleted fills the database of the server.

**Independent Test**: Capture more entries than the retention limit and confirm that the oldest ones are deleted. Request the captured text of a device with a user who is not in its group, and confirm the request is refused.

**Acceptance Scenarios**:

1. **Given** more entries than the retention limit for one device, **When** a new entry is stored, **Then** the oldest entries are deleted.
2. **Given** a user with no access to the device, **When** they request the captured text, **Then** the request is refused.
3. **Given** a single entry, **When** it is deleted, **Then** it is gone from the list and from the database.
4. **Given** the device, **When** every entry is cleared, **Then** the device has no captured text left.

### User Story 6 — The capture is per device and can be paused (Priority: P2)

Each device has its own switch and its own retention settings, and the windows during which nothing is captured, so a capture can be limited to the working hours.

**Why this priority**: A capture that cannot be limited to a device or to a time window cannot be operated responsibly.

**Independent Test**: Turn the capture on for one device and confirm that the other devices are unaffected. Set a pause window and confirm that nothing is captured inside it.

**Acceptance Scenarios**:

1. **Given** the capture of one device, **When** it is turned on, **Then** the other devices keep their own setting.
2. **Given** a pause window, **When** the time falls inside it, **Then** nothing is captured.
3. **Given** the settings of a device, **When** they are saved, **Then** they do not change the settings of the screenshots, the camera, the audio, or the keyboard of another device.

---

### Edge Cases

- The desktop client runs on a platform without a keyboard hook → the capture is not available in that build, and the server is told so.
- The capture is turned on for a device that is offline → the setting is stored, and the capture starts when the device comes online.
- The device disconnects while the capture is running → the capture stops, and it starts again when the device comes back.
- The user types faster than the text can be sent → the bursts are sent as they are, and no text is lost, because the buffer is only flushed when it is full or when the typing stops.
- The user types into a window that has no title → the burst is stored without a title.
- The clipboard holds a very large text → the whole text is captured, and the burst is limited by the maximum burst length.
- The clipboard cannot be opened because another application holds it → the paste is skipped, and the capture keeps running.

---

## Requirements

### Functional Requirements

**Capture**

- **FR-001**: The device MUST capture the text that is typed in the session of the user, and MUST send it to the server while it is being typed.
- **FR-002**: Every burst MUST carry the title of the window that had the focus and the name of the process.
- **FR-003**: A burst MUST be sent when the typing stops for a moment, when the focused window changes, or when the burst reaches its maximum length, whichever comes first.
- **FR-004**: The text of a field that holds a password MUST NEVER be stored. It MUST be replaced with a mask, and the entry MUST be marked as a password field.
- **FR-005**: The credential prompts of Windows MUST be treated as password fields.
- **FR-006**: The text of a paste MUST be captured from the clipboard, because it is never typed.
- **FR-007**: The capture MUST keep running until it is turned off, and it MUST be restored when the server or the device restarts.
- **FR-008**: The capture MUST be a switch of a single device, and the devices MUST be independent of each other.
- **FR-009**: The capture MUST be available on the Windows desktop client only, and the other builds MUST report that it is not available.

**Transfer**

- **FR-010**: The bursts MUST be sent over the hub connection of the desktop client, and the server MUST reject a burst of a session that is not a capture session.
- **FR-011**: The server MUST forward a burst to the circuit that asked for the capture, so it appears live in the browser.

**Storage**

- **FR-012**: Each burst MUST be stored with its time stamp, its window title, its process, its text, and whether it was a password field.
- **FR-013**: By default, the server MUST keep the newest 500 entries per device and MUST delete the entries that are older than 24 hours. Each device MUST be able to override the maximum count (1–20000) and the retention period (1–8760 hours).
- **FR-014**: The captured text of one device MUST NEVER be returned for another device.
- **FR-015**: A single entry MUST be deletable, and every entry of a device MUST be clearable at once.

**Access**

- **FR-016**: Reading and deleting the captured text of a device MUST require the same access to the device that the screenshot feature requires.
- **FR-017**: The pause windows MUST be stored independently per device and MUST NOT change the settings of the other features.

### Key Entities

- **Keyboard Log**: one burst of captured text, with its time stamp, its window title, its process, its text, and whether it was a password field.
- **Keyboard Log Chunk**: the burst as it travels from the device to the server.
- **Keyboard Capture Session**: the server session that asks a device to capture; it carries the access key and the circuit that receives the live bursts.
- **Keyboard Settings**: per-device switch, maximum count, retention period, and pause windows.

### Success Criteria

- **SC-001**: In 100% of the bursts that a device sends, the text arrives in the browser of the administrator and is stored on the server.
- **SC-002**: In 100% of the bursts that are typed into a password field, the stored text is a mask and never the secret itself.
- **SC-003**: 100% of the read and delete requests from a user who has no access to the device are refused.
- **SC-004**: 100% of the captured text of one device is never returned for another device.
- **SC-005**: Unless overridden per device, the server never keeps more than 500 entries or entries older than 24 hours for one device.
- **SC-006**: The capture of a device that has it enabled is running again after the device or the server restarts.

### Assumptions

- The capture runs in the desktop client of the device, inside the session of the user, because that is the only place where the keystrokes of the user can be read (owner decision AD-21).
- The desktop client of the device is up to date, because the capture needs the client that was published with this feature.
- The browser of the administrator can show a live list of the captured bursts.

### Out of Scope

- Capturing the keys of another user's session, or of the secure desktop, apart from the credential prompts of Windows.
- Reading the clipboard outside of a paste.
- Capturing the mouse, the screenshots, or the audio of the device — those are the features of SPEC-003 and SPEC-004.
- Exporting the captured text to another system.
- A per-capability consent model, a local privacy pause, and a persistent indicator on the employee's screen (the consent model of SPEC-002/SPEC-003 is still to be built).

### Constitution Compliance Review

Required by the constitution §Compliance. This review covers the specification text only; it records coverage of the requirement, never that a gate has passed.

| Principle | Compliance | Where |
|---|---|---|
| I — Extend, Do Not Rewrite | **Compliant.** The feature reuses the existing agent, desktop client, hub, session, API, and modal patterns; no subsystem is replaced. | FR-001, FR-010 |
| II — Strengthen Authorization, Preserve Function | **Compliant.** The captured text is guarded by the same device-access check as the screenshots, and the bursts are only accepted from a capture session. | FR-010, FR-016 |
| III — Consent First, Visible, Fail-Closed | **NOT COMPLIANT — recorded deviation (AD-21).** The owner chose the capture to be silent and to run all the time, with no consent prompt and no indicator on the employee's screen. The failure paths are closed (no device, no client, refused burst → nothing is stored and the administrator is told), but the consent part of the principle is not met, and gate G6 stays unsatisfied. **This is the most sensitive deviation of the product**, because the captured text holds the passwords and the messages of the users of the machine; the only protection that is left is that the text of a password field is never stored. | FR-004, FR-005, FR-008, Assumptions, Gates |
| IV — Windows Session Isolation | **Partial.** The capture happens in the desktop client, inside the target session, so it sees the keystrokes of that session. The identity of that process is still SYSTEM (E-43), which is the open G7 finding, and this feature does not change it. | FR-001, Gates |
| V — Capture Identity and Least Privilege | **Partial.** The bursts are accepted only from a capture session of the device, and the access to the captured text is checked. The identity of the capturing process is not improved. | FR-010, FR-016 |
| VI — Reliable Transfer, Bounded Storage | **Compliant.** The bursts are small and are sent over the existing hub, and the storage is bounded per device by the retention limits. | FR-003, FR-013 |
| VII — Performance Budgets | **Partial.** A burst is sent at most every few hundred milliseconds, and the bursts are small. The load of a fleet that is captured all the time is not measured. | FR-001, FR-003 |
| VIII — Minimal Dependencies | **Compliant.** No new dependency: the capture uses the Windows keyboard hook of the operating system. | FR-009, Assumptions |
| IX — ScreenRecorderLib Compatibility | **Not applicable.** The feature does not touch the screen engine. | Assumptions |
| X — Test-First and Regression Protection | **Partial.** Unit tests cover the storage, the retention, the settings, and the independence of the devices. The capture itself has not been exercised on a device. | SC-001…SC-006 |
| XI — Licence and Windows Compatibility | **Compliant.** The feature only exists on the Windows desktop client. | FR-009 |
| XII — Production Security Release Gates | **Partial.** The feature touches G6 (consent — not met) and G7 (identity — unchanged), and it promotes no gate. It also adds a new endpoint and a new stream that are authenticated by the session and the device-access check. | FR-010, FR-016, Gates |
| XIII — Evidence-Based Decisions | **Compliant.** The local build and test results are recorded as test evidence; the runtime capture is explicitly marked as unverified. | SC-001, Gates |

### Gates

| Gate | Applicability | Status |
|---|---|---|
| G5 — Authenticated local IPC | **Out of scope.** The feature does not change the local channel between the agent and the desktop client. | ⛔ Unchanged — still FAIL (OD-2 / SPEC-002) |
| G6 — Consent and privacy | **In scope and NOT met.** The capture is silent and runs all the time, with no consent prompt, no notification, and no indicator. The owner chose this explicitly (AD-21). The passwords are never stored, but everything else that is typed is. | ⛔ Not passed — recorded deviation, G6 stays UNKNOWN |
| G7 — Privileged process isolation | **Unchanged.** The capture runs in the desktop client, which is still SYSTEM (E-43 / AD-14). | ⛔ Still FAIL — SPEC-002 (capture helper) is still not implemented |
| G1…G4 | Independent — unaffected by this feature. | Out of scope here |
