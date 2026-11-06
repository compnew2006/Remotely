# Feature Specification: Camera Capture

**Feature Branch**: (none — no git branch is created; the feature ships on `master` like the previous owner-directed features)

**Created**: 2026-11-06

**Status**: Draft — implementation and local x64/AnyCPU builds plus 106 unit tests verified; camera capture has not been exercised on a device with a camera, and the feature is **not formally accepted** (no `[RELEASE-GATE]` or CI run)

**Input**: SPEC-003: Camera Capture. An administrator opens the camera of a connected device — the same way the "Screenshots" action opens the periodic screenshot monitor — and receives a short video clip recorded from one of the device's cameras, at a chosen quality level. The administrator may also start a recurring schedule. Clips are recorded on the device, sent to the server, stored with per-device settings and retention limits, and played back in the browser.

---

## User Scenarios & Testing

### User Story 1 — An administrator records a clip from a device's camera (Priority: P1)

An administrator opens the camera of a connected device. They pick one of the cameras the device reported, a quality level, and how long the clip should run. The device records the clip, sends it to the server, and the administrator can play it back in the browser within a few seconds. The clip is stored on the server like a screenshot, and it can be deleted or cleared with the rest.

**Why this priority**: It is the whole point of the feature — a recorded video from the camera of a remote device.

**Independent Test**: On a device with a camera, request one clip of 10 seconds at the medium quality. Confirm the device records a file, the server stores it, the browser lists it, and the video plays.

**Acceptance Scenarios**:

1. **Given** a device with a camera that is online, **When** the administrator requests a clip, **Then** the device records an MP4 clip of the requested length at the requested quality, and the clip appears in the list with its time stamp, quality, and duration.
2. **Given** a device with no camera, **When** a clip is requested, **Then** nothing is recorded, the failure is reported to the administrator, and no clip is stored.
3. **Given** a device that is offline, **When** a clip is requested, **Then** the request is refused with a clear message and no session is left behind.
4. **Given** a camera that is busy (in use by another application), **When** a clip is requested, **Then** the failure is reported and no clip is stored.

### User Story 2 — The quality of the clip can be chosen (Priority: P1)

The administrator chooses between three quality levels before recording. Each level has a fixed resolution, frame rate, and bitrate, so the size of the clip on the server and the load on the device are predictable. The length of the clip is bounded, so a single request can never fill the disk of the server or the device.

**Why this priority**: "Different qualities" is an explicit part of the request, and the bounds are what keep the feature safe to operate.

**Independent Test**: Record one clip per quality level on a device whose camera supports them, and compare the resolution, the frame rate, and the file size of the three results.

**Acceptance Scenarios**:

1. **Given** the quality selector, **When** it is opened, **Then** the three levels are shown with their resolution and frame rate.
2. **Given** a duration that is below or above the allowed range, **When** the request is sent, **Then** the duration is clamped to the allowed range.
3. **Given** a device whose camera does not support the requested resolution, **When** the clip is recorded, **Then** the closest supported format that is not larger than the requested one is used, and the recording still succeeds.

### User Story 3 — The clips are stored with limits, and only the people who may see the device may see the clips (Priority: P2)

The clips of a device are stored in that device's folder on the server, the newest ones are kept, and the old ones are deleted automatically. A user who has no access to the device cannot list, watch, download, or delete its clips, and a device cannot upload a clip to another device's folder.

**Why this priority**: A capture feature without storage bounds fills the disk, and a capture feature without an access check leaks the privacy of the person in front of the camera.

**Independent Test**: Record more clips than the retention limit and confirm the oldest ones are deleted from the disk and from the database. Request the clips of a device with a user who is not in its group and confirm the request is refused.

**Acceptance Scenarios**:

1. **Given** more clips than the retention limit for one device, **When** a new clip is stored, **Then** the oldest clips are deleted from the disk and from the database.
2. **Given** a user with no access to the device, **When** they request the clip list, the clip file, or the delete action, **Then** every request is refused.
3. **Given** an upload request with a session key that does not belong to the device, **Then** the upload is refused and nothing is written.
4. **Given** a file name that tries to leave the device's clip folder, **Then** it is refused and nothing outside the folder is read or deleted.

### User Story 4 — The clip is sent to the server even when the direct upload fails (Priority: P2)

The clip is recorded on the device, so it has to travel to the server. The device sends it with a single HTTP request first. When that request fails — a proxy that rejects the body, a lost connection — the device sends the same clip over the existing hub connection in small chunks, so a clip is not lost because of one transport.

**Why this priority**: The feature is useless if the clip cannot reach the server in a real network.

**Independent Test**: Block the HTTP endpoint on the device side and confirm the clip still arrives through the chunked path.

**Acceptance Scenarios**:

1. **Given** a working HTTP endpoint, **When** the clip is sent, **Then** it arrives as one request and no chunks are sent.
2. **Given** a failing HTTP endpoint, **When** the clip is sent, **Then** the clip is sent over the hub in chunks of at most 80 KB and arrives complete.
3. **Given** an upload that is interrupted, **When** the device disconnects, **Then** the partial file is not stored as a clip.

### User Story 5 — An administrator schedules recurring clips (Priority: P2)

An administrator can start or stop a recurring camera schedule for a device, choose an interval from 1 to 30 minutes, and use the selected camera, quality, and duration. The schedule observes the device's pause windows. It resumes after a desktop-client reconnect while its server session remains in memory, but is not restored after a server restart.

**Independent Test**: Start a schedule, verify that clips are stored without overlapping, stop one clip without ending the schedule, then disconnect and reconnect the desktop client and verify the schedule resumes.

**Acceptance Scenarios**:

1. **Given** a connected device and selected camera, **When** the administrator starts a schedule with an interval from 1 to 30 minutes, **Then** the desktop client records recurring clips at that interval without overlapping captures.
2. **Given** a schedule is active, **When** a manual clip is requested, **Then** the manual capture is serialized with the scheduled capture and the schedule remains active.
3. **Given** a schedule is active, **When** the administrator stops the current recording, **Then** that recording is canceled but the schedule remains active; stopping the schedule is a separate action.
4. **Given** an active schedule and a desktop client that disconnects temporarily, **When** that client reconnects, **Then** the server re-sends the active schedule and recording resumes.
5. **Given** a server restart, **When** the desktop client reconnects, **Then** no prior schedule is restored because schedule state is held in server memory.
6. **Given** one or more pause windows (including a window crossing midnight), **When** the scheduled time falls within a pause, **Then** no clip is captured for that interval.

### User Story 6 — An administrator configures camera storage per device (Priority: P2)

An administrator can choose the server folder, maximum clip count, retention hours, and multiple pause windows for each device. Defaults are 200 clips and 24 hours, with a default folder of `AppData/Camera/<DeviceId>`.

**Independent Test**: Set a custom folder and a retention count of one, save two clips, and verify that the newest clip remains in the configured folder. Set a pause window that crosses midnight and confirm the schedule observes it.

**Acceptance Scenarios**:

1. **Given** a device without custom settings, **When** its first clip is stored, **Then** it is stored under `AppData/Camera/<DeviceId>` and the defaults are 200 clips and 24 hours.
2. **Given** a device with custom storage and retention settings, **When** a clip is stored, **Then** it uses that device's configured folder and limits without changing screenshot settings.
3. **Given** multiple pause windows, **When** the administrator saves them, **Then** they are stored independently for that device and apply to its active and future camera schedules.

---

### Edge Cases

- The desktop client is built for a platform that the native capture library does not support (AnyCPU) → the feature reports that the camera capture is not available in this build, and nothing is recorded.
- The device has no camera at all → the camera list is empty, and the request fails with a clear reason.
- The camera is used by another application → the recording fails with the reason from the capture engine.
- The session that asked for the recording ends before the clip is uploaded → the upload is refused, and no clip is stored.
- The clip is larger than the accepted size → it is discarded instead of filling the disk.
- Manual and scheduled captures are requested at the same time → the desktop client serializes them, so only one capture runs at a time.
- A camera schedule is active while the desktop client reconnects → the server-held schedule is re-sent; a server restart does not restore it.
- The administrator closes the modal while the device records → the recording continues and the clip is stored; it is listed the next time the modal is opened.

---

## Requirements

### Functional Requirements

**Device capability**

- **FR-001**: The device MUST report the cameras it has to the server when it is asked to record, and the administrator MUST be able to pick one of them.
- **FR-002**: When no camera is picked, the device MUST use the first camera it has.
- **FR-003**: The recording MUST be produced by the desktop client of the device; the device service itself MUST NOT record.
- **FR-004**: The recorded clip MUST be an MP4 file that the browser can play.
- **FR-005**: The capture MUST use the capture format of the camera that is the closest to the requested resolution, and MUST NOT use a format that is larger than the requested one when a smaller supported format exists.
- **FR-006**: The capture MUST stop by itself when the requested duration has passed, and MUST stop when the recording is canceled.
- **FR-007**: The temporary file of the recording MUST be deleted from the device after it has been sent, whether the sending succeeded or not.
- **FR-008**: When the desktop client is built for a platform that the capture library does not support, the device MUST report that the camera capture is not available in this build, and MUST NOT record.

**Quality and duration**

- **FR-009**: The feature MUST offer exactly three quality levels: Low (640x480, 15 fps, 1 Mbps), Medium (1280x720, 30 fps, 2.5 Mbps), High (1920x1080, 30 fps, 5 Mbps).
- **FR-010**: The duration of a recording MUST be between 5 and 300 seconds, and a value outside that range MUST be clamped to it.
- **FR-011**: The quality level, the duration, and the camera MUST be part of the request that the server sends to the device, and MUST be recorded with the clip.

**Transfer**

- **FR-012**: The clip MUST be sent to the server with a single HTTP request to the camera upload endpoint, authenticated with the access key of the session that asked for the recording.
- **FR-013**: When the HTTP upload fails, the clip MUST be sent over the existing hub connection in chunks of at most 80 KB, which is below the maximum message size of the server.
- **FR-014**: The server MUST reject an upload whose session key does not belong to a camera session of that device.
- **FR-015**: A clip that is larger than the accepted size MUST be discarded, and the rejection MUST be logged.

**Storage**

- **FR-016**: The clips of a device MUST be stored in that device's folder on the server, and the folder MUST be derived from the device, never from the request.
- **FR-017**: By default, the server MUST keep the newest 200 clips per device and MUST delete clips older than 24 hours. Each device MUST be able to override its folder, maximum clip count (1–2000), and retention period (1–8760 hours) independently of screenshot settings. The default folder MUST be `AppData/Camera/<DeviceId>`.
- **FR-018**: Each clip MUST be recorded in the database with its file name, size, time stamp, quality, duration, and camera name.
- **FR-019**: The file name of a stored clip MUST be generated by the server, and a file name that comes from a request MUST be refused when it is not a plain file name.

**Access and playback**

- **FR-020**: Listing, watching, downloading, and deleting the clips of a device MUST require the same access to the device that the screenshot feature requires.
- **FR-021**: The clip endpoint MUST support range requests, so the browser can seek inside a clip.
- **FR-022**: A single clip MUST be deletable, and every clip of a device MUST be clearable at once.

**Observability**

- **FR-023**: The server MUST log the start of a recording, the cameras a device reported, the arrival of a clip, and the refusal of an upload.
- **FR-024**: The device MUST log the start of a recording, the result of the recording, and the result of the upload.
- **FR-025**: An administrator MUST be able to start and stop a recurring camera schedule with a selected interval from 1 to 30 minutes.
- **FR-026**: The desktop client MUST serialize manual and scheduled captures; no two camera captures may overlap. Stopping a current recording MUST NOT stop the schedule, and stopping the schedule MUST be a separate operation.
- **FR-027**: A camera schedule MUST honor multiple per-device pause windows, including windows that cross midnight. Saving updated pause windows MUST update an active schedule.
- **FR-028**: The server MUST retain the active schedule in memory and re-send it when the desktop client reconnects. A server restart is not required to restore the schedule.
- **FR-029**: The camera directory, retention count, retention hours, and pause windows MUST be stored independently per device and MUST NOT change screenshot settings.

### Key Entities

- **Camera**: a camera of a device, identified by its device path and shown by its friendly name.
- **Camera Recording Request**: the camera, the quality level, and the duration of one recording.
- **Camera Clip**: a recorded video that is stored on the server, with its file name, size, time stamp, quality, duration, and camera name.
- **Camera Session**: the server session that asks a device to record; it carries the access key that authenticates the upload.
- **Camera Schedule**: an in-memory server session with an interval, capture request, and per-device pause windows; it is re-applied after desktop-client reconnect.
- **Camera Settings**: per-device folder, retention count, retention period, and pause windows, independent of screenshot settings.
- **Quality Preset**: the resolution, frame rate, and bitrate that belong to a quality level.

### Success Criteria

- **SC-001**: In 100% of the requests on a device with a camera, a clip of the requested length and quality is stored on the server and is playable in the browser.
- **SC-002**: In 100% of the requests on a device without a usable camera, nothing is recorded, no clip is stored, and the administrator is told why.
- **SC-003**: 100% of the recorded clips are stored under the folder of their own device, and no clip is stored outside of it.
- **SC-004**: 100% of the list, playback, download, and delete requests from a user who has no access to the device are refused.
- **SC-005**: 100% of the uploads with a session key that does not belong to the device are refused, and nothing is written.
- **SC-006**: 100% of the file names that try to leave the device's clip folder are refused.
- **SC-007**: Unless overridden per device, the server never keeps more than 200 clips or clips older than 24 hours for one device.
- **SC-008**: A clip that is recorded is delivered to the server by one of the two transports, and a partial upload is never stored as a clip.
- **SC-009**: The duration of a recording is always between 5 and 300 seconds, whatever the administrator asked for.
- **SC-010**: The camera capture is unavailable, with a clear message, in a build that does not support the capture library.
- **SC-011**: A schedule accepts only intervals from 1 to 30 minutes and captures do not overlap.
- **SC-012**: Stopping a current clip leaves the recurring schedule active; stopping the schedule prevents further scheduled clips.
- **SC-013**: An active schedule resumes after the desktop client reconnects, but is not restored after a server restart.
- **SC-014**: Custom camera storage and retention settings affect only the selected device and are independent of screenshot settings.

### Assumptions

- The capture engine is ScreenRecorderLib 7.0.1 over Media Foundation (owner decision AD-18). The engine talks to the camera of the Windows session, so it needs the desktop client, and the desktop client has to be built for x64 or x86.
- The device is online and its agent can launch the desktop client, exactly like the screenshot feature does.
- The desktop client knows the address of the server and the access key of its session, so it can upload the clip without a new credential.
- The browser of the administrator can play an MP4 file that is served with range requests.
- The camera of the device is not used by another application at the same time. The capture engine reports the failure when it is.

### Out of Scope

- Live view of the camera (a stream instead of a recorded clip).
- Audio of the camera or of the device.
- Recording without an administrator explicitly starting a recurring schedule.
- Choosing the camera before the device has reported it, and a search for a camera by name.
- Restoring a camera schedule after a server process restart.
- A per-capability consent model, a local privacy pause, and a persistent indicator on the employee's screen (the consent model of SPEC-002/SPEC-003 is still to be built).
- Any change to the identity of the desktop client (the capture helper of SPEC-002 is still to be built).
- Camera support on Linux and macOS desktop clients.

### Constitution Compliance Review

Required by the constitution §Compliance. This review covers the specification text only; it records coverage of the requirement, never that a gate has passed.

| Principle | Compliance | Where |
|---|---|---|
| I — Extend, Do Not Rewrite | **Compliant.** The feature reuses the existing agent, desktop client, hub, session, API, and modal patterns; no subsystem is replaced. | FR-003, FR-012 |
| II — Strengthen Authorization, Preserve Function | **Compliant.** The clip endpoints use the same device-access check as the screenshot endpoints, and the upload endpoint is guarded by the session key. | FR-014, FR-020 |
| III — Consent First, Visible, Fail-Closed | **NOT COMPLIANT — recorded deviation (AD-18).** The owner chose the fastest path: the recording happens with no consent prompt, no per-capture notification, and no local pause. The failure paths are closed (no camera, busy camera, offline device, bad upload → nothing is stored and the administrator is told), but the consent part of the principle is not met, and gate G6 stays unsatisfied. | FR-002, FR-022, Assumptions, Gates |
| IV — Windows Session Isolation | **Partial.** The recording happens in the desktop client, inside the target session, so it can reach the camera of that session. The identity of that process is still SYSTEM (E-43), which is the open G7 finding, and this feature does not change it. | FR-003, Gates |
| V — Capture Identity and Least Privilege | **Partial.** The upload is authenticated by the session key and captures are serialized per desktop client. The identity of the capturing process is not improved. | FR-014, FR-020, FR-026 |
| VI — Reliable Transfer, Bounded Storage | **Compliant.** Two transports, a bounded clip size, per-device bounded retention, and a partial upload is never stored. | FR-012…FR-017, FR-029 |
| VII — Performance Budgets | **Partial.** Each clip is short and bounded, captures are serialized, and quality levels keep size predictable. The sustained load of a fleet recording on schedules is not measured. | FR-009, FR-010, FR-025…FR-027 |
| VIII — Minimal Dependencies | **Compliant.** One new package (ScreenRecorderLib 7.0.1, MIT), which is the engine the constitution already names, and it is referenced only for the platforms that support it. | Assumptions, Gates |
| IX — ScreenRecorderLib Compatibility | **Compliant for the screen engine.** The same library is used for the camera, and its screen path was already proven on this stack (E-49). The camera path of the library was not proven: this host has no camera. | Assumptions, Gates |
| X — Test-First and Regression Protection | **Partial.** Unit tests cover quality/duration, storage settings, file-name guards, upload, retention, and delete paths. Schedule lifecycle and physical camera capture have not been exercised here; the host has no camera. | SC-001…SC-014 |
| XI — Licence and Windows Compatibility | **Compliant.** ScreenRecorderLib is MIT, and the feature only exists on the Windows desktop client, which is where the cameras are. | Assumptions |
| XII — Production Security Release Gates | **Partial.** The feature touches G6 (consent — not met) and G7 (identity — unchanged), and it promotes no gate. It also adds a new endpoint that is authenticated by the session key. | FR-014, Gates |
| XIII — Evidence-Based Decisions | **Compliant.** The local build and test results are recorded as test evidence; the runtime capture on a camera is explicitly marked as unverified, because this host has no camera. | SC-001, Gates |

### Gates

| Gate | Applicability | Status |
|---|---|---|
| G5 — Authenticated local IPC | **Out of scope.** The feature does not change the local channel between the agent and the desktop client. The upload uses the session key that already exists. | ⛔ Unchanged — still FAIL (OD-2 / SPEC-002) |
| G6 — Consent and privacy | **In scope and NOT met.** The recording happens without a consent prompt, without a notification, and without a local pause. The owner chose this explicitly (AD-18). | ⛔ Not passed — recorded deviation, G6 stays UNKNOWN |
| G7 — Privileged process isolation | **Unchanged.** The capture runs in the desktop client, which is still SYSTEM (E-43 / AD-14). The camera makes the same gap more sensitive, but the feature does not close it. | ⛔ Still FAIL — SPEC-002 (capture helper) is still not implemented |
| G1…G4 | Independent — unaffected by this feature. | Out of scope here |
