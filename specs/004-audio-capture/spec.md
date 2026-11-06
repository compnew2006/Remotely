# Feature Specification: Audio Capture

**Feature Branch**: (none — no git branch is created; the feature ships on `master` like the previous owner-directed features)

**Created**: 2026-11-07

**Status**: Draft — implementation is complete and the local build is clean with 134/134 unit tests passing; the audio capture has **not** been exercised on a device with a real sound device, and the feature is **not formally accepted** (no `[RELEASE-GATE]` or CI run)

**Input**: SPEC-004: Audio Capture. An administrator opens the audio of a connected device — the same way the "Camera" action opens the camera recorder — and receives a short audio clip recorded from the device's **system audio** (everything the device plays) or from its **microphone**, at a chosen quality level. The administrator may also start a recurring schedule, and may **listen to the device live** while the modal is open. Clips are recorded on the device, sent to the server, stored with per-device settings and retention limits, and played back in the browser.

---

## User Scenarios & Testing

### User Story 1 — An administrator records a clip from a device (Priority: P1)

An administrator opens the audio of a connected device. They pick the source (system audio or microphone), the microphone when one is picked, a quality level, and how long the clip should run. The device records the clip, sends it to the server, and the administrator can play it back in the browser within a few seconds. The clip is stored on the server like a screenshot, and it can be deleted or cleared with the rest.

**Why this priority**: It is the whole point of the feature — a recorded audio clip from a remote device.

**Independent Test**: On a device with a sound device, request one clip of 10 seconds at the medium quality. Confirm the device records a file, the server stores it, the browser lists it, and the audio plays.

**Acceptance Scenarios**:

1. **Given** a device with a sound device that is online, **When** the administrator requests a clip, **Then** the device records a WAV clip of the requested length at the requested quality from the requested source, and the clip appears in the list with its time stamp, quality, duration, and source.
2. **Given** a device with no usable microphone, **When** a microphone clip is requested, **Then** nothing is recorded, the failure is reported to the administrator, and no clip is stored.
3. **Given** a device that is offline, **When** a clip is requested, **Then** the request is refused with a clear message and no session is left behind.
4. **Given** a device that has more than one microphone, **When** the microphone list is shown, **Then** every active capture endpoint of the device is listed, and the administrator can pick one.

### User Story 2 — The source can be chosen (Priority: P1)

The administrator chooses between the system audio and the microphone before recording. The system audio is captured with a loopback device, so it contains everything the device plays — meetings, calls, and media. The microphone captures what is spoken in the room. The two are never mixed in one clip.

**Why this priority**: "من صوت الجهاز او صوت المايك" is an explicit part of the request.

**Independent Test**: Record one clip of each source on a device that plays a known sound, and confirm that the system clip contains it while the microphone clip contains the room.

**Acceptance Scenarios**:

1. **Given** the source selector, **When** it is opened, **Then** "System audio" and "Microphone" are offered.
2. **Given** the microphone source, **When** no microphone was reported yet, **Then** the microphone selector shows that no microphones have been reported, and the administrator can ask the device for its list.
3. **Given** a source that is not available on the device, **When** the recording starts, **Then** the failure is reported and no clip is stored.

### User Story 3 — The quality of the clip can be chosen (Priority: P1)

The administrator chooses between three quality levels before recording. Each level has a fixed sample rate, so the size of the clip on the server and the load on the device are predictable. The length of the clip is bounded, so a single request can never fill the disk of the server or the device.

**Why this priority**: "Different qualities" is an explicit part of the request, and the bounds are what keep the feature safe to operate.

**Independent Test**: Record one clip per quality level, and compare the sample rate and the file size of the three results.

**Acceptance Scenarios**:

1. **Given** the quality selector, **When** it is opened, **Then** the three levels are shown with their sample rate.
2. **Given** a duration that is below or above the allowed range, **When** the request is sent, **Then** the duration is clamped to the allowed range.
3. **Given** a device whose capture format differs from the requested one, **Then** the clip is converted to the requested sample format before it is stored.

### User Story 4 — The clips are stored with limits, and only the people who may see the device may see the clips (Priority: P2)

The clips of a device are stored in that device's folder on the server, the newest ones are kept, and the old ones are deleted automatically. A user who has no access to the device cannot list, play, download, or delete its clips, and a device cannot upload a clip to another device's folder.

**Why this priority**: A capture feature without storage bounds fills the disk, and a capture feature without an access check leaks the privacy of the person whose voice was recorded.

**Independent Test**: Record more clips than the retention limit and confirm the oldest ones are deleted from the disk and from the database. Request the clips of a device with a user who is not in its group and confirm the request is refused.

**Acceptance Scenarios**:

1. **Given** more clips than the retention limit for one device, **When** a new clip is stored, **Then** the oldest clips are deleted from the disk and from the database.
2. **Given** a user with no access to the device, **When** they request the clip list, the clip file, or the delete action, **Then** every request is refused.
3. **Given** an upload request with a session key that does not belong to the device, **Then** the upload is refused and nothing is written.
4. **Given** a file name that tries to leave the device's clip folder, **Then** it is refused and nothing outside the folder is read or deleted.

### User Story 5 — The clip is sent to the server even when the direct upload fails (Priority: P2)

The clip is recorded on the device, so it has to travel to the server. The device sends it with a single HTTP request first. When that request fails — a proxy that rejects the body, a lost connection — the device sends the same clip over the existing hub connection in small chunks, so a clip is not lost because of one transport.

**Why this priority**: The feature is useless if the clip cannot reach the server in a real network.

**Independent Test**: Block the HTTP endpoint on the device side and confirm the clip still arrives through the chunked path.

**Acceptance Scenarios**:

1. **Given** a working HTTP endpoint, **When** the clip is sent, **Then** it arrives as one request and no chunks are sent.
2. **Given** a failing HTTP endpoint, **When** the clip is sent, **Then** the clip is sent over the hub in chunks of at most 80 KB and arrives complete.
3. **Given** an upload that is interrupted, **When** the device disconnects, **Then** the partial file is not stored as a clip.

### User Story 6 — An administrator schedules recurring clips (Priority: P2)

An administrator can start or stop a recurring audio schedule for a device, choose an interval from 1 to 30 minutes, and use the selected source, quality, and duration. The schedule observes the device's pause windows. It resumes after a desktop-client reconnect while its server session remains in memory, but is not restored after a server restart.

**Independent Test**: Start a schedule, verify that clips are stored without overlapping, stop one clip without ending the schedule, then disconnect and reconnect the desktop client and verify the schedule resumes.

**Acceptance Scenarios**:

1. **Given** a connected device and a selected source, **When** the administrator starts a schedule with an interval from 1 to 30 minutes, **Then** the desktop client records recurring clips at that interval without overlapping captures.
2. **Given** a schedule is active, **When** a manual clip is requested, **Then** the manual capture is serialized with the scheduled capture and the schedule remains active.
3. **Given** a schedule is active, **When** the administrator stops the current recording, **Then** that recording is canceled but the schedule remains active; stopping the schedule is a separate action.
4. **Given** an active schedule and a desktop client that disconnects temporarily, **When** that client reconnects, **Then** the server re-sends the active schedule and recording resumes.
5. **Given** a server restart, **When** the desktop client reconnects, **Then** no prior schedule is restored because schedule state is held in server memory.
6. **Given** one or more pause windows (including a window crossing midnight), **When** the scheduled time falls within a pause, **Then** no clip is captured for that interval.

### User Story 7 — An administrator listens to a device live (Priority: P2)

An administrator can start a live stream of the device's audio from the device card, without opening a control session. The audio arrives in the browser while it is being captured. Nothing is stored on the server, and the stream stops when the administrator stops it or closes the modal.

**Why this priority**: "إماكتية سماع الصوت ... في الوقت الفعلي" is an explicit part of the request, and it is the most privacy-sensitive part of the feature.

**Independent Test**: Start the live stream on a device that is playing a known sound and confirm the sound is audible in the browser, then stop the stream and confirm the device stops capturing.

**Acceptance Scenarios**:

1. **Given** an online device, **When** the administrator starts the live stream, **Then** the device captures the selected source and sends short audio frames to the browser, which plays them one after the other.
2. **Given** an active live stream, **When** the administrator stops it, **Then** the device stops capturing and the browser stops playing.
3. **Given** an active live stream, **When** the administrator closes the modal, **Then** the browser stops playing, and the stream is stopped by the server when the session ends.
4. **Given** a live stream, **Then** no clip is stored on the server, and the live stream never appears in the clip list.

### User Story 8 — The existing in-session audio button keeps working (Priority: P2)

The remote control viewer already has an audio button that streams the system audio of the device into the browser. That path is unchanged by this feature, and both paths can be used at the same time.

**Why this priority**: The live listening scope chosen by the owner is "both" — the existing in-session button and the new device-card stream.

**Independent Test**: Open a remote control session, press the audio button, and confirm the system audio is still audible and that the new device-card stream works at the same time.

**Acceptance Scenarios**:

1. **Given** a remote control session, **When** the audio button is pressed, **Then** the system audio of the device is streamed to the viewer exactly as before.
2. **Given** an active in-session audio stream, **When** the device-card live stream is started, **Then** both streams run, and neither one stops the other.

### User Story 9 — An administrator configures audio storage per device (Priority: P2)

An administrator can choose the server folder, maximum clip count, retention hours, and multiple pause windows for each device. Defaults are 200 clips and 24 hours, with a default folder of `AppData/Audio/<DeviceId>`.

**Independent Test**: Set a custom folder and a retention count of one, save two clips, and verify that the newest clip remains in the configured folder. Set a pause window that crosses midnight and confirm the schedule observes it.

**Acceptance Scenarios**:

1. **Given** a device without custom settings, **When** its first clip is stored, **Then** it is stored under `AppData/Audio/<DeviceId>` and the defaults are 200 clips and 24 hours.
2. **Given** a device with custom storage and retention settings, **When** a clip is stored, **Then** it uses that device's configured folder and limits without changing screenshot or camera settings.
3. **Given** multiple pause windows, **When** the administrator saves them, **Then** they are stored independently for that device and apply to its active and future audio schedules.

---

### Edge Cases

- The desktop client runs on a platform without a working sound device → the source is reported as unavailable, and nothing is recorded.
- The device has no microphone → the microphone list is empty, and a microphone request fails with a clear reason.
- The device plays nothing → the system-audio capture produces a silent clip; the clip is still stored, because silence is a valid recording.
- The administrator asks for the microphone list only (duration of zero) → no clip is recorded, only the list is sent.
- The session that asked for the recording ends before the clip is uploaded → the upload is refused, and no clip is stored.
- The clip is larger than the accepted size → it is discarded instead of filling the disk.
- Manual and scheduled captures are requested at the same time → the desktop client serializes them, so only one capture runs at a time.
- An audio schedule is active while the desktop client reconnects → the server-held schedule is re-sent; a server restart does not restore it.
- The administrator closes the modal while the device records → the recording continues and the clip is stored; it is listed the next time the modal is opened.
- The browser blocks audio until the user interacts with the page → the first live frame is dropped until the audio context is resumed, and playback starts afterwards.
- The live stream and a scheduled recording are active at the same time → they use separate capture instances, so neither one stops the other.
- The device reconnects while a live stream is running → the stream does not resume by itself, and the administrator starts it again. The live stream is interactive, so it is not restored like a schedule is.

---

## Requirements

### Functional Requirements

**Device capability**

- **FR-001**: The device MUST report the microphones it has to the server when it is asked to record, and the administrator MUST be able to pick one of them.
- **FR-002**: When no microphone is picked, the device MUST use the default capture endpoint of the system.
- **FR-003**: The recording MUST be produced by the desktop client of the device; the device service itself MUST NOT record.
- **FR-004**: The recorded clip MUST be an uncompressed WAV file that the browser can play.
- **FR-005**: The source MUST be selectable per request: system audio (loopback) or microphone, and the two MUST NOT be mixed in one clip.
- **FR-006**: The capture MUST stop by itself when the requested duration has passed, and MUST stop when the recording is canceled.
- **FR-007**: The temporary file of the recording MUST be deleted from the device after it has been sent, whether the sending succeeded or not.
- **FR-008**: The feature MUST work with the audio library that the desktop client already ships with, and MUST NOT add a new dependency.
- **FR-009**: The capture MUST use the system audio when the system audio is not available on the device, and MUST report that the source is unavailable instead of recording something else.

**Quality and duration**

- **FR-010**: The feature MUST offer exactly three quality levels: Low (16 kHz, mono, 16-bit), Medium (22.05 kHz, mono, 16-bit), High (44.1 kHz, mono, 16-bit).
- **FR-011**: The duration of a recording MUST be between 5 and 600 seconds, and a value outside that range MUST be clamped to it.
- **FR-012**: The quality level, the duration, and the source MUST be part of the request that the server sends to the device, and MUST be recorded with the clip.

**Transfer**

- **FR-013**: The clip MUST be sent to the server with a single HTTP request to the audio upload endpoint, authenticated with the access key of the session that asked for the recording.
- **FR-014**: When the HTTP upload fails, the clip MUST be sent over the existing hub connection in chunks of at most 80 KB, which is below the maximum message size of the server.
- **FR-015**: The server MUST reject an upload whose session key does not belong to an audio session of that device.
- **FR-016**: A clip that is larger than the accepted size MUST be discarded, and the rejection MUST be logged.

**Storage**

- **FR-017**: The clips of a device MUST be stored in that device's folder on the server, and the folder MUST be derived from the device, never from the request.
- **FR-018**: By default, the server MUST keep the newest 200 clips per device and MUST delete clips older than 24 hours. Each device MUST be able to override its folder, maximum clip count (1–2000), and retention period (1–8760 hours) independently of screenshot and camera settings. The default folder MUST be `AppData/Audio/<DeviceId>`.
- **FR-019**: Each clip MUST be recorded in the database with its file name, size, time stamp, quality, duration, and source.
- **FR-020**: The file name of a stored clip MUST be generated by the server, and a file name that comes from a request MUST be refused when it is not a plain file name.

**Access and playback**

- **FR-021**: Listing, playing, downloading, and deleting the clips of a device MUST require the same access to the device that the screenshot feature requires.
- **FR-022**: The clip endpoint MUST support range requests, so the browser can seek inside a clip.
- **FR-023**: A single clip MUST be deletable, and every clip of a device MUST be clearable at once.

**Live listening**

- **FR-024**: The server MUST be able to start a live audio stream for a device, and to stop it, without an open control session.
- **FR-025**: The live stream MUST carry the audio in short, self-contained frames, and the browser MUST play the frames one after the other.
- **FR-026**: A live stream MUST NOT be stored on the server, and MUST NOT appear in the clip list.
- **FR-027**: A live stream MUST be stopped by the administrator, and the device MUST stop capturing when it is stopped.
- **FR-028**: The live stream MUST require the same access to the device as the clip list.
- **FR-029**: The existing in-session audio button of the remote control viewer MUST keep working exactly as before, and the two streams MUST be able to run at the same time.

**Observability**

- **FR-030**: The server MUST log the start of a recording, the microphones a device reported, the arrival of a clip, the refusal of an upload, and the start and the stop of a live stream.
- **FR-031**: The device MUST log the start of a recording, the result of the recording, and the result of the upload.
- **FR-032**: An administrator MUST be able to start and stop a recurring audio schedule with a selected interval from 1 to 30 minutes.
- **FR-033**: The desktop client MUST serialize manual and scheduled captures; no two audio captures may overlap. Stopping a current recording MUST NOT stop the schedule, and stopping the schedule MUST be a separate operation.
- **FR-034**: An audio schedule MUST honor multiple per-device pause windows, including windows that cross midnight. Saving updated pause windows MUST update an active schedule.
- **FR-035**: The server MUST retain the active schedule in memory and re-send it when the desktop client reconnects. A server restart is not required to restore the schedule.
- **FR-036**: The audio directory, retention count, retention hours, and pause windows MUST be stored independently per device and MUST NOT change screenshot or camera settings.

### Key Entities

- **Audio Source**: the place the audio is captured from — system audio or microphone.
- **Audio Device**: a microphone of a device, identified by its device path and shown by its friendly name.
- **Audio Capture Request**: the source, the microphone, the quality level, and the duration of one recording.
- **Audio Clip**: a recorded audio file that is stored on the server, with its file name, size, time stamp, quality, duration, and source.
- **Audio Session**: the server session that asks a device to record; it carries the access key that authenticates the upload.
- **Audio Schedule**: an in-memory server session with an interval, capture request, and per-device pause windows; it is re-applied after desktop-client reconnect.
- **Audio Live Session**: an in-memory server session that carries the live frames of one device to one circuit.
- **Audio Settings**: per-device folder, retention count, retention period, and pause windows, independent of screenshot and camera settings.
- **Quality Preset**: the sample rate, channel count, and bit depth that belong to a quality level.

### Success Criteria

- **SC-001**: In 100% of the requests on a device with a sound device, a clip of the requested length and quality is stored on the server and is playable in the browser.
- **SC-002**: In 100% of the requests on a device without a usable source, nothing is recorded, no clip is stored, and the administrator is told why.
- **SC-003**: 100% of the recorded clips are stored under the folder of their own device, and no clip is stored outside of it.
- **SC-004**: 100% of the list, playback, download, and delete requests from a user who has no access to the device are refused.
- **SC-005**: 100% of the uploads with a session key that does not belong to the device are refused, and nothing is written.
- **SC-006**: 100% of the file names that try to leave the device's clip folder are refused.
- **SC-007**: Unless overridden per device, the server never keeps more than 200 clips or clips older than 24 hours for one device.
- **SC-008**: A clip that is recorded is delivered to the server by one of the two transports, and a partial upload is never stored as a clip.
- **SC-009**: The duration of a recording is always between 5 and 600 seconds, whatever the administrator asked for.
- **SC-010**: A live stream is audible in the browser within a few seconds of being started, and it stops when the administrator stops it.
- **SC-011**: A live stream never produces a stored clip.
- **SC-012**: The in-session audio button of the remote control viewer behaves exactly as before this feature.
- **SC-013**: A schedule accepts only intervals from 1 to 30 minutes and captures do not overlap.
- **SC-014**: Stopping a current clip leaves the recurring schedule active; stopping the schedule prevents further scheduled clips.
- **SC-015**: An active schedule resumes after the desktop client reconnects, but is not restored after a server restart.
- **SC-016**: Custom audio storage and retention settings affect only the selected device and are independent of screenshot and camera settings.

### Assumptions

- The audio engine is the NAudio library that the desktop client already ships with (owner decision AD-19). It talks to the sound devices of the Windows session, so it needs the desktop client, and the desktop client has to run in the session of the user.
- The device is online and its agent can launch the desktop client, exactly like the screenshot and camera features do.
- The desktop client knows the address of the server and the access key of its session, so it can upload the clip without a new credential.
- The browser of the administrator can play a WAV file that is served with range requests.
- The desktop client is built for Windows; the audio capture is not available in the Linux and macOS desktop clients.

### Out of Scope

- Audio of the camera (the camera clips stay silent, as they were before).
- Recording without an administrator explicitly starting a recurring schedule.
- Choosing the microphone before the device has reported it, and a search for a microphone by name.
- Restoring an audio schedule after a server process restart.
- Mixing the system audio and the microphone in one clip.
- A per-capability consent model, a local privacy pause, and a persistent indicator on the employee's screen (the consent model of SPEC-002/SPEC-003 is still to be built).
- Any change to the identity of the desktop client (the capture helper of SPEC-002 is still to be built).
- Audio support on the Linux and macOS desktop clients.
- Storing the live stream as a recording.

### Constitution Compliance Review

Required by the constitution §Compliance. This review covers the specification text only; it records coverage of the requirement, never that a gate has passed.

| Principle | Compliance | Where |
|---|---|---|
| I — Extend, Do Not Rewrite | **Compliant.** The feature reuses the existing agent, desktop client, hub, session, API, modal, and audio-sampling patterns; no subsystem is replaced. | FR-003, FR-013, FR-024 |
| II — Strengthen Authorization, Preserve Function | **Compliant.** The clip endpoints use the same device-access check as the screenshot endpoints, the upload endpoint is guarded by the session key, and the live stream requires the same access. | FR-015, FR-021, FR-028 |
| III — Consent First, Visible, Fail-Closed | **NOT COMPLIANT — recorded deviation (AD-19).** The owner chose the same path as the camera: the recording and the live stream happen with no consent prompt, no notification, and no local pause. The failure paths are closed (no source, offline device, bad upload → nothing is stored and the administrator is told), but the consent part of the principle is not met, and gate G6 stays unsatisfied. Audio is more sensitive than video, because it can capture conversations that never touch the screen. | FR-002, FR-023, Assumptions, Gates |
| IV — Windows Session Isolation | **Partial.** The capture happens in the desktop client, inside the target session, so it can reach the sound devices of that session. The identity of that process is still SYSTEM (E-43), which is the open G7 finding, and this feature does not change it. | FR-003, Gates |
| V — Capture Identity and Least Privilege | **Partial.** The upload is authenticated by the session key and captures are serialized per desktop client. The identity of the capturing process is not improved. | FR-015, FR-021, FR-033 |
| VI — Reliable Transfer, Bounded Storage | **Compliant.** Two transports, a bounded clip size, per-device bounded retention, a partial upload is never stored, and the live stream stores nothing. | FR-013…FR-018, FR-026, FR-036 |
| VII — Performance Budgets | **Partial.** Each clip is short and bounded, captures are serialized, and quality levels keep size predictable. The sustained load of a fleet recording on schedules is not measured, and the live stream adds a constant per-listener bandwidth cost. | FR-010, FR-011, FR-025, FR-032…FR-034 |
| VIII — Minimal Dependencies | **Compliant.** No new dependency: the capture uses NAudio, which the desktop client already referenced for the in-session audio. | FR-008, Assumptions |
| IX — ScreenRecorderLib Compatibility | **Not applicable.** The feature does not touch the screen engine; it uses the audio library instead. | Assumptions |
| X — Test-First and Regression Protection | **Partial.** Unit tests cover the quality and duration presets, the storage settings, the file-name guards, the upload, the retention, and the delete paths. The schedule lifecycle, the live stream, and a physical capture have not been exercised here; this host has no verified sound device. | SC-001…SC-016 |
| XI — Licence and Windows Compatibility | **Compliant.** NAudio is MIT, and the feature only exists on the Windows desktop client, which is where the sound devices are. | Assumptions |
| XII — Production Security Release Gates | **Partial.** The feature touches G6 (consent — not met) and G7 (identity — unchanged), and it promotes no gate. It also adds a new endpoint and a new live channel that are authenticated by the session key and the device-access check. | FR-015, FR-028, Gates |
| XIII — Evidence-Based Decisions | **Compliant.** The local build and test results are recorded as test evidence; the runtime capture on a real sound device is explicitly marked as unverified. | SC-001, Gates |

### Gates

| Gate | Applicability | Status |
|---|---|---|
| G5 — Authenticated local IPC | **Out of scope.** The feature does not change the local channel between the agent and the desktop client. The upload uses the session key that already exists. | ⛔ Unchanged — still FAIL (OD-2 / SPEC-002) |
| G6 — Consent and privacy | **In scope and NOT met.** The recording and the live stream happen without a consent prompt, without a notification, and without a local pause. The owner chose this explicitly (AD-19), as he did for the camera. | ⛔ Not passed — recorded deviation, G6 stays UNKNOWN |
| G7 — Privileged process isolation | **Unchanged.** The capture runs in the desktop client, which is still SYSTEM (E-43 / AD-14). Audio makes the same gap more sensitive than the screen does, because it captures the room and the calls of the user. | ⛔ Still FAIL — SPEC-002 (capture helper) is still not implemented |
| G1…G4 | Independent — unaffected by this feature. | Out of scope here |
