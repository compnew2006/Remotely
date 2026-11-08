# Webhook Delivery for Remotely

* **Author:** noiemany
* **Status:** Draft
* **Date:** 2026-11-08
* **Source:** `concepts_brief.md`

---

## Overview

Remotely is a remote-control server (ASP.NET Core, Blazor, SignalR) cloned from GitHub. The author did not write this codebase. Teams use it to control devices interactively, run scripts, and capture screenshots, camera clips, audio clips, and keyboard logs.

Today, the alert callback in `AlertsController.Create` is awaited inline: it has no retry or durable per-attempt history, and the controller logs the response status or exception before returning. The server also makes unrelated outbound requests (for example, `UpgradeService` checks GitHub releases), but there is no durable outbound event-delivery path. A missed alert callback is hard to distinguish from a successful request after the fact.

This design adds webhook delivery as a passive, side-band channel. When a v1 event occurs (a device transitions online or a script result is accepted), the producer writes one event row in the same database transaction as the domain change. A background dispatcher later fans the event out to matching endpoints, sends signed HTTP POSTs, retries transient failures, and records each attempt. Future file-capture events remain out of scope until their producers can meet the same atomicity requirement.

Three constraints shape the whole design:

1. Webhook HTTP must never run on the normal remote-control path (SignalR, UI, API). Producers append one outbox row in the domain transaction; subscription fan-out and all HTTP delivery happen in a background worker. The outbox commit still uses the shared database and can affect producer latency or fail with the domain transaction.
2. Minimize webhook data exposure. V1 payloads use DTO allowlists and payload-negative tests; they exclude raw keyboard text and media bytes, and each endpoint is SSRF-checked before delivery.
3. Alert delivery remains a separate compatibility path in v1. Webhooks use their own outbox and background dispatcher; consolidating the existing alert HTTP request is deferred to a later release decision.

## The Problem

Two problems need solving, and they pulled in opposite directions during brainstorming.

**Problem 1 — events are invisible outside the server.** A script result is accepted or a device comes online — neither reaches a ticketing system, a monitor, or an automation pipeline without manual polling. The existing alert API callback can make an outbound request, but it is not a durable event-delivery channel. Capture-related events are possible future additions, not part of v1.

**Problem 2 — a naive webhook is an attack surface.** The server is internet-facing and stores sensitive material (typed text, script output that can contain credentials, captured media). A straightforward "POST to user-supplied URL, retry until it works" design creates SSRF and data-exposure risks; a slow receiver can consume worker capacity, repeated failures can grow the queue, and a plaintext signing secret in a database dump could be used to forge deliveries. The design addresses these failure modes with explicit egress, payload, secret-storage, and retry rules.

The v1 design uses a database outbox, background dispatcher, allowlisted metadata payloads, SSRF checks, protected signing secrets, bounded retries, dead letters, and an append-only attempt history. The alert API behavior remains unchanged in this release.

## Goals

1. **V1 events reach external systems with bounded retries.** A `device.connected` event or accepted script result is durably recorded before delivery is attempted. A recoverable receiver outage receives up to 8 attempts within a 72-hour event horizon; expiry or permanent failure becomes a dead letter.
2. **Webhook HTTP stays off the control path.** Producers append one event row in the domain transaction and never await network I/O. Subscription fan-out and delivery happen in the background. Per-endpoint lanes limit one receiver's effect, while the database and worker remain shared resources.
3. **Receivers can identify and verify deliveries.** The envelope carries a stable logical `EventId`; each endpoint delivery has a stable `webhook-id` across its retries, a timestamp, an idempotency key, and an HMAC-SHA256 signature using the documented Standard Webhooks input. Delivery is at-least-once; exactly-once is not promised.
4. **Payloads are constrained, not assumed harmless.** V1 carries event IDs, device/script identifiers, status, and timestamps through dedicated DTO allowlists. It carries no `details_url`, script output/input, keyboard text, or media bytes. Identifiers and timestamps are still treated as sensitive operational data.
5. **Operators can inspect and recover delivery failures.** Each attempt is retained with its event and endpoint IDs, number, response status, latency, and error kind; dead letters remain visible, and manual replay keeps the same logical `EventId`. A test ping uses the same delivery path.
6. **Additive rollout.** Webhooks use their own enqueue path. `AlertsController.Create` keeps its current inline behavior in v1; alert callers and the email path are unchanged.

## Non-Goals

1. No change to the desktop agent, the desktop client, or the SignalR/agent protocol.
2. No exactly-once delivery guarantee.
3. No raw `KeyboardLog.Text`, screenshot bytes, camera clip bytes, or audio clip bytes in any webhook payload.
4. No multi-instance/cluster deployment support; the dispatcher is designed for the existing single-process deployment only.
5. No message broker (RabbitMQ/Kafka) and no new persistence infrastructure beyond the existing Sqlite/SqlServer/PostgreSql providers.
6. No Blazor administration page in v1. Endpoint registration, management, test ping, and delivery history are exposed through the authenticated REST controller API.

## Resolved Design Decisions

* **D1 — Alerts:** `AlertsController.Create` remains unchanged in v1. Webhooks are additive; removing or migrating the existing inline alert request requires a separate, documented release decision.
* **D2 — Event scope:** V1 contains exactly `device.connected` and `script.execute`. `device.disconnected` and the four capture events are future work, gated on a named receiver and a separate privacy review.

## Critical User Journeys (CUJs)

### CUJ 1: Register and enable an endpoint

* **Given**: An authenticated organization administrator using the management REST API and authorized for the target organization.
* **When**: They create a webhook endpoint with URL and event filters and receive a one-time-displayed signing secret.
* **Then**: The endpoint is registered disabled; a "Test delivery" ping is offered; enabling requires the ping to succeed and SSRF validation to pass both at registration and at send time.

### CUJ 2: Receive a device-connection event

* **Given**: An enabled webhook endpoint uses HTTPS and has a reachable receiver.
* **When**: A device comes online and the producer writes one `WebhookEvent` row; matching endpoint deliveries are created later by the dispatcher.
* **Then**: The dispatcher claims the row, validates the endpoint egress, signs the envelope, sends it with `webhook-id`, `webhook-timestamp`, `webhook-signature`, and `Idempotency-Key`; the delivery log records status, attempts, and latency; success requires a 2xx.

### CUJ 3: Receiver outage and dead-letter recovery

* **Given**: An endpoint returns 500 for every attempt.
* **When**: Attempt 1 is sent immediately. The seven retry gaps before attempts 2–8 are 1 min → 2 min → 5 min → 15 min → 1 h → 6 h → 48 h, each with ±20% jitter (nominal cumulative delay ≈ 55 h 23 min; maximum with jitter < 67 h), within the 72-hour horizon measured from event creation.
* **Then**: After 8 total attempts or the 72-hour horizon, the delivery moves to the dead-letter state and remains visible. A manual replay creates a separate one-attempt delivery with the same `EventId` and `Idempotency-Key`, preserving the original attempt history.

### CUJ 4: Receiver is brought online after exhaustion

* **Given**: A dead-letter row exists.
* **When**: Within 96 hours of event creation, an operator explicitly requests manual replay for an enabled endpoint.
* **Then**: A new one-attempt delivery cycle is queued with the same `EventId` and a new `webhook-id`; the prior attempt history remains visible. Manual replay is allowed only through 96 hours after the event was created.

---

## Technical Plan (Architecture)

Webhooks use a **database outbox** and a **pull-based background dispatcher**. All v1 producers write one event row in the same database transaction as the domain change and never send HTTP. Future producers that cannot share the transaction are not enabled until their atomicity gap is closed. The dispatcher creates per-endpoint delivery rows in a transaction, then sends them in the background. Existing patterns to consult are `ScriptScheduler` (hosted timer), `DataCleanupService` (hosted periodic worker), and `DataService.CleanupOldRecords` (cleanup delegated from the hosted service).

```text
                    EVENT (device connected / script result accepted)
                         |
     +-------------------------------------------+
     | Enqueue one WebhookEvent row               |
     | Same transaction as each v1 domain change |
     +-------------------------------------------+
                         |
     +------------------ +------------------ +-----------+
     | WebhookDispatcher (IHostedService)                  |
     |                                                    |
     |  1. Claim event; fan out atomically to matching     |
     |     WebhookDelivery rows (unique per event/endpoint)|
     |  2. Claim due deliveries with expiring leases       |
     |  3. Pin validated egress IP, sign, and POST         |
     |  4. Append an attempt record                        |
     |  5. Retry transient errors; dead-letter permanent   |
     +------------------ +------------------ +-----------+
                         |
            +-------------+-------------+
            | Receiver (external HTTPS) |
            |  dedupe by EventId        |
            +---------------------------+
                          |
             Attempt history + metrics + dead-letter view
```

### 1) Event Producers (enqueue only)

The domain code that already owns each v1 event writes **one `WebhookEvent` row** (no per-endpoint fan-out). The dispatcher matches enabled endpoints by event type and organization; an organization-scoped endpoint (`DeviceGroupId=null`) matches every device in that organization, while a group-scoped endpoint matches only the same `DeviceGroupId`. It rechecks the endpoint creator's current authorization during fan-out and immediately before network I/O. If access is revoked, the delivery is dead-lettered with `AuthorizationRevoked` and is not sent. The event stores the organization and device-group identifiers from the time of occurrence; permissions are not frozen into a historical snapshot.

| Event | Producer (today) | Enqueue site |
|---|---|---|
| `device.connected` (v1) | `AgentHub.DeviceCameOnline` / `DeviceHeartbeat` → `DataService.AddOrUpdateDevice` | Only when the persisted device transitions from offline/new to online; repeated heartbeats do not emit events |
| `device.disconnected` (future) | `AgentHub.OnDisconnectedAsync` → `DataService.DeviceDisconnected` | Device-offline status path |
| `script.execute` (v1) | `ScriptResultsController.Post` → `DataService.AddScriptResult` | One event when one device's result is accepted; not an aggregate "all devices completed" event |
| `screenshot.available` (future) | `ScreenshotService.SaveScreenshot` | After successful save |
| `camera.clip.available` (future) | `CameraService.SaveClip` / `CompleteUpload` | After successful save |
| `audio.clip.available` (future) | `AudioService.SaveClip` / `CompleteUpload` | After successful save |
| `keyboard.log.available` (future) | `KeyboardLogService.SaveLog` | After successful save; requires a separate privacy decision |

The five future event types above are catalogued only; none is emitted in v1. `script.execute` means an accepted per-device result, not a promise that an entire multi-device run has completed.

Enqueue rules enforced in `IWebhookPublisher` (a domain-facing API):

- No producer awaits HTTP. It appends one event row in the domain transaction and returns after that transaction commits; later delivery failures cannot roll back the committed domain/event transaction.
- Payloads are built from per-event DTO allowlists, never EF entities. V1 `script.execute` excludes `ScriptInput`, `StandardOutput`, and `ErrorOutput`; no v1 DTO includes keyboard text or media bytes. `WebhookTests.cs` and `WebhookDispatcherTests.cs` must assert the serialized body excludes those fields.
- Fan-out is a background operation. Match `OrganizationId`, optional `DeviceGroupId`, and event type; before delivery, verify the endpoint is still enabled and its `CreatedBy` administrator remains authorized for that scope. Do not treat an API key alone as proof of organization/device-group membership.

**Producer transaction boundary:** Every v1 producer adds the `WebhookEvent` to the same `AppDb` context as its domain write. `AddOrUpdateDevice` appends only on an offline/new-to-online transition; `DeviceDisconnected` appends only on an online-to-offline transition; both save the status and event together. `DataService.AddScriptResult` uses an explicit transaction: save the `ScriptResult` once to obtain its generated string `ID`, append the event, save it, then commit. If either save fails, roll back and do not return success. Event timestamps use server UTC from the injected `TimeProvider`, not client-provided timestamps. Future file-capture producers must not be enabled until they can share a transaction or provide a reconciliation design. Enqueue failure is logged and counted; it is never reported as queued.

### 2) Outbox Entities

| Entity | Columns (types / nullability) | Notes |
|---|---|---|
| `WebhookEndpoint` | `Id` varchar(36) PK · `OrganizationId` varchar(36) not null · `DeviceGroupId` varchar(36) nullable · `Url` varchar(2048) not null · `EventsJson` varchar(4096) not null (nonempty subset of v1 allowlist) · `Enabled` bool not null · `AllowHttpOptIn` bool not null (default false) · `CurrentProtectedSecret` varbinary not null · `CurrentSecretVersion` int not null · `PreviousProtectedSecret` varbinary nullable · `PreviousSecretVersion` int nullable · `PreviousSecretValidUntilUtc` DateTimeOffset nullable · `LastValidationKind` varchar(64) nullable · `LastValidatedUtc` DateTimeOffset nullable · `CreatedBy` varchar(450) not null · `CreatedUtc` DateTimeOffset not null · `DisabledUtc` DateTimeOffset nullable | Organization/group IDs are validated against current membership at create time; no parent FK, so retained history does not block tenant deletion. `DeviceGroupId = null` means organization scope; otherwise it narrows scope to that group. Set `DisabledUtc` at creation and on disable; clear it on enable. Index `(OrganizationId)`. API `DELETE` soft-disables; cleanup hard-purges the endpoint after 14 days disabled and after its delivery history expires. Cap: 100 enabled endpoints per organization. |
| `WebhookEvent` | `EventId` varchar(36) PK · `OrganizationId` varchar(36) not null · `DeviceGroupId` varchar(36) nullable · `EventType` varchar(100) not null · `SchemaVersion` varchar(16) not null (`v1`) · `PayloadDataJson` text not null (UTF-8 JSON data object ≤ 240,000 bytes) · `OccurredUtc` DateTimeOffset not null · `CreatedUtc` DateTimeOffset not null · `FanoutStatus` varchar(16) not null (`Pending`, `Dispatching`, `FannedOut`, `Failed`) · `FanoutAttempts` int not null · `FanoutNextAttemptUtc` DateTimeOffset not null · `FanoutLeaseUntilUtc` DateTimeOffset nullable · `FanoutCompletedUtc` DateTimeOffset nullable · `FanoutLastErrorKind` varchar(64) nullable | Index `(FanoutStatus, FanoutNextAttemptUtc)`. Only the allowlisted `data` object is stored once; the per-delivery envelope is serialized at send time. Full signed body is capped at 262,144 UTF-8 bytes. Organization/group scope is historical values, not cascading FKs. |
| `WebhookDelivery` | `Id` varchar(36) PK (stable `webhook-id` for this delivery and its automatic retries) · `EventId` varchar(36) FK not null · `WebhookEndpointId` varchar(36) FK not null · `DeliveryKind` varchar(16) not null (`Event`, `TestPing`, `ManualReplay`) · `ReplayNumber` int not null (0 initial/test; 1 manual replay) · `SecretVersion` int not null · `Status` varchar(16) not null (`Queued`, `Delivering`, `RetryPending`, `Succeeded`, `Dead`) · `Attempts` int not null · `MaxAttempts` int not null (= 8 for event/test; = 1 for manual replay) · `NextAttemptUtc` DateTimeOffset not null · `LeaseUntilUtc` DateTimeOffset nullable · `ClaimedBy` varchar(100) nullable · `CreatedUtc` DateTimeOffset not null · `CompletedUtc` DateTimeOffset nullable | Unique `(EventId, WebhookEndpointId, ReplayNumber)`; indexes `(Status, NextAttemptUtc)` and `(WebhookEndpointId, CreatedUtc)`. FKs to event and endpoint do not cascade-delete history. Permit at most one manual replay per event/endpoint; a repeat request returns conflict. Test pings are direct deliveries and bypass event-filter matching. |
| `WebhookDeliveryAttempt` | `Id` varchar(36) PK · `WebhookDeliveryId` varchar(36) FK not null · `AttemptNumber` int not null · `StartedUtc` DateTimeOffset not null · `CompletedUtc` DateTimeOffset nullable · `HttpStatusCode` int nullable · `LatencyMs` int nullable · `ErrorKind` varchar(64) nullable | One retained record per started delivery attempt; insert at start, then fill completion fields once. History is never deleted before event retention expires. Unique `(WebhookDeliveryId, AttemptNumber)`. Request/response bodies, URLs, secrets, and payload data are not copied here. |

Logical provider mappings are: text `varchar(n)` → SQLite `TEXT`, PostgreSQL `varchar(n)`, SQL Server `varchar(n)`; unbounded JSON `text` → SQLite/PostgreSQL `TEXT`, SQL Server `nvarchar(max)`; protected bytes → SQLite `BLOB`, PostgreSQL `bytea`, SQL Server `varbinary(max)`; bool → SQLite integer, PostgreSQL `boolean`, SQL Server `bit`. `AppDb` applies its existing `DateTimeOffset` converters where required by SQLite/PostgreSQL. Entity IDs are UUID strings. Endpoint and event rows preserve organization/group IDs as scope values without parent FKs, so retention does not block tenant/group deletion; fan-out suppresses events for a missing or unauthorized scope and disables orphaned endpoints. Delivery FKs never cascade-delete history.

- **Fan-out state:** `Pending → Dispatching → FannedOut` or `Failed`. Claim `Pending` when `FanoutNextAttemptUtc <= now`, or reclaim `Dispatching` when `FanoutLeaseUntilUtc <= now`; the fan-out lease is 2 minutes. Select events in `(FanoutNextAttemptUtc, CreatedUtc, EventId)` order. The worker selects matching enabled endpoints, rechecks their creator's current authorization, inserts deliveries, and marks the event `FannedOut` in one transaction. A unique `(EventId, WebhookEndpointId, ReplayNumber)` constraint makes a re-run idempotent. An event with no matching authorized endpoint is marked `FannedOut` with no delivery rows; registering an endpoint later does not replay historical events. On transient database failure, retry fan-out at 1, 2, 5, 15, and 60 minutes, then every 6 hours until the 72-hour event horizon; after that mark fan-out `Failed` and expose it in the metrics response/logs.
- **Delivery state transitions:** `Queued → Delivering`; `RetryPending → Delivering`; expired-lease `Delivering → Delivering` reclaim; `Delivering → Succeeded` for 2xx; `Delivering → RetryPending` for retryable failure while below 8 attempts and within the horizon; and `Delivering → Dead` for terminal failure, exhaustion, revoked authorization, or expired horizon. If an endpoint is disabled after claim but before network I/O, release the claim to `Queued` without incrementing attempts or adding an attempt record. Claim only rows joined to an enabled endpoint, except `DeliveryKind=TestPing`, which is explicitly allowed while disabled. A disabled endpoint leaves ordinary queued work untouched; re-enabling makes still-valid rows eligible, but does not resurrect dead letters.
- **Delivery claim:** select candidates in `(NextAttemptUtc, CreatedUtc, Id)` order, then conditionally update each candidate using a compare-and-set predicate: `Id == candidate.Id AND ((Status IN {Queued, RetryPending} AND NextAttemptUtc <= now) OR (Status == Delivering AND LeaseUntilUtc <= now)) AND Attempts < MaxAttempts AND Event.CreatedUtc > now - 72h AND (Endpoint.Enabled OR DeliveryKind == TestPing)`. Only an update affecting one row wins the claim; set `Status=Delivering`, `LeaseUntilUtc=now+10 minutes`, and `ClaimedBy=workerId`. Implement provider-specific atomic SQL where needed; never use an unguarded read-then-update. Verify the same state contract on SQLite, SQL Server, and PostgreSQL. Use injected `TimeProvider` for UTC and deterministic tests. Independently mark rows `Dead` when attempt or horizon limits expire, including while an endpoint is disabled. After the endpoint-enabled check, increment `Attempts` and insert an attempt row when delivery processing starts; record pre-network authorization or SSRF policy rejection as a terminal attempt with null HTTP status. An endpoint-disable race before network I/O is not an attempt and releases the delivery as described above. An expired lease can cause at-least-once redelivery if the receiver got the request before the worker crashed.
- **Manual replay:** preserve the original `EventId` and prior attempt history; create one new delivery per event/endpoint (`ReplayNumber=1`), set `MaxAttempts=1`, use the endpoint's current secret version, and send immediately. Reject subsequent replay requests for the same event/endpoint. The receiver sees the same `Idempotency-Key` and a new `webhook-id`. Allow replay only until event age reaches 96 hours; after that return a conflict explaining that the deduplication window has expired. Manual replay does not extend the automatic 72-hour retry horizon.
- **Endpoint disable:** stop new fan-out and make ordinary queued/retry-pending deliveries ineligible; the dispatcher rechecks endpoint status and creator authorization immediately before network I/O. Test pings are the sole disabled-endpoint exception. Re-enable resumes only ordinary deliveries still within the 72-hour automatic horizon; older work becomes dead-lettered. A disabled endpoint's dead letters are not automatically replayed on re-enable.
- **Retention:** retain event and delivery/attempt history for 14 days after `WebhookEvent.CreatedUtc`, then prune in bounded batches in FK order: attempts, deliveries, then events. Keep endpoint configuration until explicitly disabled; then hard-purge it after 14 days and only after its delivery history is gone. The cleanup logic belongs in `DataService.CleanupOldRecords`; expired in-flight deliveries become dead letters before pruning.
- The wire contract, signature input, timestamp units, and `ping` envelope are defined in §4.

### 3) Dispatcher — `WebhookDispatcher : BackgroundService`

The hosted worker polls every 2 seconds and uses `IServiceScopeFactory` to create a DI scope for each database unit of work. It runs two bounded loops: event fan-out, then due-delivery dispatch. `IWebhookPublisher` is scoped; the hosted singleton does not capture a scoped service. A scoped `IWebhookHttpTransport` creates and disposes a request-specific `SocketsHttpHandler` for each delivery attempt so its `ConnectCallback` can be bound to that attempt's validated IP without leaking callback state or pooled connections across deliveries.

1. Claim up to 50 pending events, then atomically create the matching delivery rows and mark each event `FannedOut` as described in §2. The unique event/endpoint/replay key prevents duplicate fan-out after a restart.
2. Claim up to 50 due deliveries. Send them in parallel with per-endpoint semaphores (maximum 4 in flight for one endpoint) and a global ceiling of 32. A slow receiver can occupy its own lane up to the configured timeout; it does not make the rest of a claimed batch run sequentially.
3. Before **each** HTTP request, validate and pin the destination:
   - Permit HTTPS by default. Permit plain HTTP only when `AllowHttpOptIn=true` on that endpoint; signing remains mandatory for all schemes. Reject credentials in URLs, numeric/local host aliases, `.local`/`.internal`/`.intranet`, and any normalized IP that is loopback, private, link-local, shared, reserved/special-use, multicast, IPv6 ULA, or metadata (including `169.254.169.254`). Resolve all A/AAAA answers and reject the host if any answer is blocked; normalize IPv4-mapped IPv6 and zone identifiers before comparison.
   - The validated address is request-specific. A fresh `SocketsHttpHandler` per delivery attempt binds `ConnectCallback` to that validated IP and connects to it; the request URL retains the original hostname, which the handler uses for TLS SNI and normal certificate validation. Dispose the handler after the response so a connection validated for an earlier attempt cannot be reused. Do not follow redirects. Dedicated transport tests verify the dialed IP and hostname/certificate behavior; a mock `HttpMessageHandler` alone does not prove IP pinning.
   - Policy failures (`DnsBlocked`, `PrivateRange`, `SchemeBlocked`, `PrivateSuffix`, `RedirectBlocked`) are terminal and go directly to `Dead`; a DNS/connect timeout is transient. Re-checking at send time plus the pinned connection closes the validation/connect DNS gap for that request.
 4. Serialize the envelope, including `schema_version`, once to the exact UTF-8 `rawBody` that will be sent. Enforce the 262,144-byte cap on this complete envelope. Decrypt the endpoint's `SecretVersion`, then compute `base64(HMAC-SHA256(secret, webhook-id + "." + unixTimestamp + "." + rawBody))`; send `webhook-id`, `webhook-timestamp` (Unix seconds), `webhook-signature` (`v1,<base64>`), and `Idempotency-Key`. Receivers allow at most 5 minutes of timestamp skew.
 5. For each attempt, use a fresh `SocketsHttpHandler` with `AllowAutoRedirect=false`, a 5-second connect timeout and 10-second overall request timeout. Dispose it after the response. Limit request bodies to 262,144 bytes. Send with `HttpCompletionOption.ResponseHeadersRead` and read at most 65,536 response bytes; dispose without unbounded buffering. No response body is persisted or logged.
 6. Append one `WebhookDeliveryAttempt` for each started delivery attempt and update the delivery state. A 2xx is success. Network failures, 408, 429, and 5xx are retriable. 3xx, other 4xx, and policy failures are terminal; redirects are never followed.
7. Attempt 1 is immediate. Retry gaps before attempts 2–8 are 1 min → 2 min → 5 min → 15 min → 1 h → 6 h → 48 h, with ±20% jitter. Seven nominal gaps total 55 h 23 min; the maximum with jitter is under 67 hours. No next attempt is scheduled past `WebhookEvent.CreatedUtc + 72 h`; the 8-attempt cap is checked before the horizon when both limits coincide. After exhaustion, mark the delivery `Dead`.

Expose organization-scoped operational metrics through authenticated `GET /api/webhooks/metrics`; v1 assumes no external metrics exporter. The response includes `webhook_deliveries_total` and `webhook_retries_total` by bounded event-type/outcome values, `webhook_fanout_failures_total` by event type, and `webhook_queue_oldest_age_seconds`, calculated as `max(0, now - CreatedUtc)` for the oldest eligible due delivery whose endpoint is enabled or whose kind is `TestPing`. The operator view/runbook flags queue age above 60 seconds; no automatic alerting service is included. Endpoint IDs, organization IDs, URLs, and user IDs are not metric dimensions. Structured attempt logs include `EventId`, `WebhookDeliveryId`, `WebhookEndpointId`, attempt number, status code, latency, and error kind; they exclude payload, URL, headers, and secrets.

### 4) Wire Contract — Standard Webhooks signing headers

Every attempt is the same shape (a receiver implements this one contract):

```http
POST {endpoint-url}
Content-Type: application/json
webhook-id: <delivery Id>
webhook-timestamp: <unix seconds>
webhook-signature: v1,<base64-hmac>
Idempotency-Key: <EventId>

{ "type": "device.connected", "schema_version": "v1", "event_id": "<EventId>", "data": {…}, "delivery_id": "<WebhookDeliveryId>", "occurred_at": "<rfc3339>" }
```

Wire-contract table (`EventId` is stable across deliveries and retries; `WebhookDelivery.Id` is stable across retries of one delivery):

| Header | Value source | Notes |
|---|---|---|
| `webhook-id` | `WebhookDelivery.Id` | Identifies one endpoint delivery/replay cycle and stays unchanged across its automatic retries. |
| `webhook-timestamp` | send time, Unix seconds | Receivers must reject skew > 5 minutes. |
| `webhook-signature` | `v1,` + `base64(HMAC-SHA256(secret, webhook-id + "." + timestamp + "." + rawBody))` | Secret is decrypted from the endpoint's protected secret version recorded on the delivery. |
| `Idempotency-Key` | `WebhookEvent.EventId` | Stable across automatic retries, manual replays, and deliveries to different endpoints for the same logical event. |
| `Content-Type` | `application/json` | The signed `rawBody` is the exact UTF-8 JSON bytes sent in the request. |

The envelope always contains `type`, `schema_version`, `event_id`, `data`, `delivery_id`, and `occurred_at`. `schema_version` is `"v1"` for every event in this release; it is stored on `WebhookEvent` and copied into each envelope. The table's `PayloadDataJson` stores only `data`; the dispatcher adds the delivery-specific envelope fields before signing.

The payload `ping` (test delivery) is a normal `schema_version: "v1"` envelope with `"type": "ping"`, a generated `event_id`, `"data": {}`, and a normal `delivery_id`. In one transaction, the controller creates the `WebhookEvent` and a direct `DeliveryKind=TestPing` row, marks the event fan-out complete, and returns `202 Accepted`; it does not synchronously call the receiver or run subscription matching. Test pings may be sent while the endpoint is disabled; they still require authorization, SSRF validation, signing, and the normal dispatcher.

Per-event `data` block. V1 fields are allowlisted and exclude script input/output, secrets, URLs, and media bytes. Future-event fields below are illustrative and require a named receiver and privacy review before implementation.

| Event | `data` carries |
|---|---|
| `device.connected` (v1) | `DeviceId`, `OrganizationId`, `DeviceGroupId?` |
| `device.disconnected` (future) | `DeviceId`, `OrganizationId`, `DeviceGroupId?` |
| `script.execute` (v1; one accepted per-device result) | `ScriptResultId` (the generated string `ScriptResult.ID`), `ScriptRunId?`, `SavedScriptId?`, `DeviceId`, `HadErrors`, `RunTimeMs`; never `ScriptInput`, `StandardOutput`, or `ErrorOutput`. `occurred_at` is the server receipt time. |
| `screenshot.available` (future) | `ScreenshotId`, `DeviceId`, `CapturedAt`, `SizeBytes`; no `details_url` |
| `camera.clip.available` (future) | `ClipId`, `DeviceId`, `CameraName`, `Quality`, `DurationSeconds`; no `details_url` |
| `audio.clip.available` (future) | `ClipId`, `DeviceId`, `Quality`, `DurationSeconds`, `Source` |
| `keyboard.log.available` (future) | `LogId`, `DeviceId`, `WindowTitle`, `ProcessName`, `Count`, `WindowStartUtc`; title/process values are sensitive and require explicit approval before this event can ship |

Receivers deduplicate by `EventId` in `Idempotency-Key` for at least 96 hours from first receipt (72-hour automatic retry horizon plus a 24-hour manual-replay grace period). The receiver must verify that `Idempotency-Key` equals the signed body `event_id`. `webhook-id` is not the logical event ID: it is stable only for one endpoint delivery/replay cycle and is reused across that delivery's automatic retries. Manual replay is a single attempt with a new `webhook-id`. Ordering is not guaranteed. V1 has no resource-download API; consumers that need details must use a separately authorized integration, outside this contract.

One minimal example per event:

```jsonc
// device.connected (v1)
{ "type": "device.connected", "schema_version": "v1", "event_id": "e001", "data": { "DeviceId": "d1", "OrganizationId": "o1", "DeviceGroupId": null }, "delivery_id": "d001", "occurred_at": "2026-11-08T04:00:00Z" }
// device.disconnected (future)
{ "type": "device.disconnected", "schema_version": "v1", "event_id": "e002", "data": { "DeviceId": "d1", "OrganizationId": "o1", "DeviceGroupId": null }, "delivery_id": "d002", "occurred_at": "2026-11-08T04:02:00Z" }
// script.execute (v1; one accepted result)
{ "type": "script.execute", "schema_version": "v1", "event_id": "e003", "data": { "ScriptResultId": "result-id", "ScriptRunId": 412, "SavedScriptId": "a91c", "DeviceId": "d1", "HadErrors": false, "RunTimeMs": 850 }, "delivery_id": "d003", "occurred_at": "2026-11-08T04:01:00Z" }
// screenshot.available (future)
{ "type": "screenshot.available", "schema_version": "v1", "event_id": "e004", "data": { "ScreenshotId": "s81", "DeviceId": "d1", "CapturedAt": "2026-11-08T04:00:00Z", "SizeBytes": 40960 }, "delivery_id": "d004", "occurred_at": "2026-11-08T04:00:00Z" }
// camera.clip.available (future)
{ "type": "camera.clip.available", "schema_version": "v1", "event_id": "e005", "data": { "ClipId": "c55", "DeviceId": "d1", "CameraName": "front", "Quality": "720p", "DurationSeconds": 60 }, "delivery_id": "d005", "occurred_at": "2026-11-08T04:00:00Z" }
// audio.clip.available (future)
{ "type": "audio.clip.available", "schema_version": "v1", "event_id": "e006", "data": { "ClipId": "a12", "DeviceId": "d1", "Quality": "medium", "DurationSeconds": 30, "Source": "mic" }, "delivery_id": "d006", "occurred_at": "2026-11-08T04:00:00Z" }
// keyboard.log.available (future; sensitive values not approved for v1)
{ "type": "keyboard.log.available", "schema_version": "v1", "event_id": "e007", "data": { "LogId": "k99", "DeviceId": "d1", "WindowTitle": "Editor", "ProcessName": "notepad", "Count": 12, "WindowStartUtc": "2026-11-08T04:00:00Z" }, "delivery_id": "d007", "occurred_at": "2026-11-08T04:00:00Z" }
```

### 5) Registration API

- V1 exposes an authenticated controller API only; a Blazor administration view is future work. `POST` creates a disabled `WebhookEndpoint` and shows its signing secret once; `GET` never returns either protected secret; URL, organization/group scope, event filters, and HTTP opt-in are immutable after creation (replace by creating/testing a new endpoint, then disabling the old one); `DELETE` only disables the endpoint and retains history; `POST /test` queues a ping directly for that endpoint (even while disabled) and returns `202 Accepted`; delivery history and manual replay use separate endpoints. Ping bypasses event-subscription matching, but not authorization, SSRF validation, signing, or the dispatcher.
- Each operation requires an organization-administrator identity and checks organization/device-group scope. The API principal must resolve to a `RemotelyUser` with the required access; an API key alone is not evidence of organization membership unless the server explicitly maps it to such an identity.
- A ping must succeed and the endpoint must pass SSRF checks before it can be enabled. Registration validates the scheme, DNS answers, and host policy; each actual send repeats validation and pins the IP. Rejected policy checks return an actionable error kind; attempt logs retain the last validation result without storing the URL in metric labels.
- Disabling stops new fan-out and makes ordinary queued/retry-pending deliveries ineligible. Re-enabling resumes eligible rows only inside the 72-hour horizon; the independent expiry sweep marks older rows dead, and existing dead letters are not automatically replayed. Manual replay requires an enabled endpoint, is limited to one per event/endpoint, creates a new one-attempt delivery with the original `EventId`, and preserves the original attempt history.
- V1 does not issue signed resource URLs or tokens. The webhook consumer receives only allowlisted event metadata and must use a separately authorized integration for any additional information.

### 6) AlertsController transition

`AlertsController.Create` (the current alert callback sender) is **not changed in this release**. Webhooks are additive on their own enqueue path; the alert inline send keeps its current behavior, and the `Alert` entity and email path (`EmailSenderEx`) are untouched. Compatibility: existing callers of `api/alerts/create` see no behavior change; the alert inline branch may be removed in a future release only after a documented migration boundary.

## Key Definitions and References

- **Organization** is the tenant scope used by this design. **DeviceGroup** is an optional narrower scope within an organization. Endpoint creation and event fan-out must use the server's current organization/device-group authorization rules; this document does not grant new access to device data.
- **WebhookEndpoint** is the one registration record: callback URL + organization/group scope + event filter + signing-secret versions. “Subscription” is a user-facing synonym only, not another entity. **WebhookEvent** is one logical event with one stable `EventId`; **WebhookDelivery** is one endpoint-specific delivery/replay cycle with its own stable `webhook-id`; **WebhookDeliveryAttempt** is one HTTP attempt.
- **WindowStartUtc** (future keyboard event only) is the start of the aggregation window; **Count** is the number of captured entries in that window. Neither field contains captured text. The event itself is not approved for v1.
- **Standard Webhooks** (https://www.standardwebhooks.com/) supplies the signing headers and `{webhook-id}.{timestamp}.{rawBody}` signature input used here. The Remotely envelope and `Idempotency-Key` behavior are Remotely-specific; this design does not claim full specification conformance.
- Existing implementation references: `Server/Program.cs` (`AddHttpClient`); `Server/Services/ScriptScheduler.cs` (hosted timer); `Server/Services/DataCleanupService.cs` (hosted periodic worker); `Server/Services/DataService.cs` (`AddScriptResult`, `CleanupOldRecords`); `Server/API/ScriptResultsController.cs` (`Post`); `Server/Services/UpdateSigningService.cs` (signing-key storage outside the web root). `UpdateSigningService` is not a drop-in secret-storage implementation: its platform behavior must not be copied for webhook secrets.

## Alternatives Considered

### Alternative A: Reuse the `AlertsController` HTTP path as-is, no outbox

* **Pros**: Zero new infrastructure; the system already offers generic HTTP delivery (method, URL, JSON body, arbitrary headers) with a persisted `Alert` row and an SMTP path; the position is backed by the project's own code (`Server/API/AlertsController.cs`, `Shared/Entities/Alert.cs`).
* **Cons**: The request is awaited inline, but it does not retry, sign, or retain durable attempt history; an operator cannot reliably distinguish a dropped delivery from a successful one during incident response. It does not deduplicate or redeliver, and carrying `ApiToken.Secret`/`ApiRequestUrl` semantics forward would retain the existing SSRF-hardening gap.
* **Reason for Rejection**: This path remains unchanged for existing alert callers, but it does not meet the webhook delivery contract. The new webhook API is additive and does not route alert requests through the outbox in v1.

### Alternative B: Extend `AlertOptions` with payload templating (`{{DeviceId}}`/`{{Message}}` substitution)

* **Pros**: Minimal change; solves the "body shaping" problem without any subscription model or new entities; aligns with the existing success/error trigger semantics (`SavedScript.GenerateAlertOnError` / `SendEmailOnError`).
* **Cons**: It touches only body formatting. It does not provide bounded retries, per-attempt history, endpoint-specific signing, or the deduplication contract. It also retains the current inline request behavior for alert callers.
* **Reason for Rejection**: Templating is a smaller alternative, but it does not provide the reliability and verification requirements selected for webhooks. The alert route remains unchanged for compatibility.

### Alternative C: Rely on email or polling through the existing API

* **Pros**: Nothing new to build; email is already delivered via `EmailSenderEx` and polling works today through the controllers.
* **Cons**: Email lacks machine-readable delivery semantics (no status codes, no idempotency key, no dead letter, no bounded retry contract) and polling misses low-latency hooks entirely while adding load per second. Both keep consumers guessing at structure and latency.
* **Reason for Rejection**: Email and polling remain available, but neither provides the selected webhook delivery envelope, signature, and per-attempt history.

### Alternative D: Message broker (RabbitMQ/Kafka)

* **Pros**: Durable broker queues with built-in dead-letter and redelivery features.
* **Cons**: It adds broker deployment, credentials, monitoring, a new failure domain, and connector/migration work for SQLite, SQL Server, and PostgreSQL.
* **Reason for Rejection**: The database outbox meets the v1 single-instance deployment requirement without adding a broker dependency. Multi-instance deployment is not supported by this design and would require a separate evaluation of claims, concurrency, and storage behavior.

---

## Non-Functional Requirements

- **Security & privacy**
  - HMAC signing is mandatory for every endpoint. Plain HTTP requires explicit per-endpoint opt-in and is still blocked for any non-public destination.
  - Store the current and previous endpoint secrets only as ASP.NET Core Data-Protection-protected bytes; never store a one-way hash as the HMAC key or plaintext secret. A database dump still exposes endpoint URLs, scope, event metadata, delivery history, and protected-secret ciphertext, but not the plaintext signing secrets. Persist the Data Protection key ring outside the web root in durable storage; Docker deployments mount a named volume and include key backup/restore in operations. If keys are unavailable, secret decryption and delivery fail closed until the key ring is restored; unrecoverable key loss requires endpoint replacement and secret rotation. Do not copy `UpdateSigningService`'s non-Windows plaintext fallback.
  - Rotation increments `SecretVersion`, stores the old protected secret as the previous version for 72 hours, and issues the new secret once. Each automatic delivery records its secret version; the dispatcher signs retries with that version during the grace period. The receiver must accept both versions during the same period. Reject another normal rotation while a previous version is still in its grace period. A forced emergency rotation disables the endpoint, invalidates both old versions, dead-letters their queued deliveries with `SecretRevoked`, and returns a new secret once; re-enable requires a successful test ping.
  - SSRF guard runs at registration and before each send. HTTPS is default; opted-in HTTP is permitted only for public destinations. Normalize addresses and reject loopback, private, link-local, shared, reserved/special-use, multicast, metadata, IPv6 ULA, numeric/local aliases, and `.local`/`.internal`/`.intranet`; reject the whole host if any A/AAAA answer is blocked. Pin the validated IP to the individual connection, retain the original hostname for TLS SNI/certificate validation, disable redirects, and cap request/response bodies. Policy failures are terminal; network timeouts are retriable.
  - Dedicated DTO allowlists exclude script input/output, keyboard text, URLs, and media bytes from v1. `WebhookTests.cs` and `WebhookDispatcherTests.cs` must assert this against serialized bodies. Identifiers and timestamps may still be sensitive and require access controls and retention limits.
  - Attempt logs contain only event/delivery/endpoint IDs, attempt number, response status, latency, and error kind; they exclude payload, URL, headers, and secret material.
- **Performance & latency**
  - Producer-path work is one event-row insert (the script-result path may perform two `SaveChanges` calls inside one explicit transaction to obtain the generated ID); subscription fan-out runs later. This is not zero-cost: the insert is part of the domain transaction, and database latency/failure is shared. Fan-out is bounded by at most 100 enabled endpoints per organization. Stored `data` JSON is capped at 240,000 UTF-8 bytes and the full signed envelope at 262,144 bytes. Delivery rows reference the event instead of duplicating its payload.
  - The worker polls every 2 seconds, claims batches of up to 50, and uses per-endpoint concurrency 4 with a global limit of 32. The named claim indexes are `(FanoutStatus, FanoutNextAttemptUtc)` and `(Status, NextAttemptUtc)`.
  - Surface bounded-cardinality delivery/retry counters, fan-out failure counts, and an oldest-due-queue-age gauge through authenticated `GET /api/webhooks/metrics`. The response groups by event type/outcome only; endpoint/org/user IDs are excluded from metric dimensions. V1 does not include an external exporter or automatic alerting service.
  - No real-time delivery SLA is promised. Actual queue delay depends on database load, worker capacity, and receiver timeouts; the poll interval is not an end-to-end latency guarantee.
- **Reliability**
  - At-least-once delivery. `EventId` and `Idempotency-Key` identify the logical event; `webhook-id` identifies an endpoint delivery cycle and remains constant across its automatic retries. Receivers dedupe for at least 96 hours from first receipt; exactly-once is not promised. At most one manual replay per event/endpoint is allowed, as one immediate attempt within the 24-hour grace after the automatic horizon.
  - Attempt 1 is immediate; seven retry gaps (1 min → 2 min → 5 min → 15 min → 1 h → 6 h → 48 h, each ±20%) precede attempts 2–8. No attempt is scheduled beyond 72 hours after event creation; the delivery then becomes a dead letter. Retry 408/429, 5xx, and transient network failures; do not retry policy failures, 3xx, or other 4xx.
  - Expiring leases allow work to be reclaimed after a worker crash. If the remote receiver accepted a request before the server lost its result, redelivery can occur; consumers must dedupe by `EventId`.
- **Rollout & rollback**
  - Feature is additive only: new tables, new endpoints, new hosted service. No existing hub/API signature changes; existing agents, desktop clients, and SignalR protocol are untouched, so rollback is: stop the dispatcher, leave rows in place (they are inert), and revert code. Nothing that was stored before the feature needs migrating backward.
  - Migrations must be scaffolded and verified for SQLite, SQL Server, and PostgreSQL. The server follows the existing behavior when automatic migration is disabled; this design does not change that setting.

## Risks & Limits

| Risk / limit | Mitigation or explicit boundary |
|---|---|
| A worker can crash after the receiver accepted a request but before success was recorded. | Lease expiry retries the delivery; it is at-least-once, so receivers dedupe by `EventId`. |
| A database write can fail; a non-transactional future producer could also stop between its domain save and event insert. | V1 outbox inserts share the domain transaction, so a failed event insert rolls back that domain write and the caller receives failure. Do not enable future file-capture events until each has a shared transaction or a reconciliation plan; an enqueue failure is logged and counted, not reported as queued. |
| Event payloads, delivery rows, attempts, or disabled endpoint configuration can consume database capacity. | Cap stored `data` JSON at 240,000 UTF-8 bytes and the full envelope at 262,144 bytes, cap enabled endpoints at 100 per organization, retain event/delivery history for 14 days, purge disabled endpoint configuration after its history expires, and prune in bounded batches. SQLite remains a shared writer and has no throughput SLA. |
| DNS answers can change between validation and connection, or redirects can target internal addresses. | Pin the request's validated public IP, use the URL hostname for TLS validation, reject any mixed public/private answer set, disable redirects, and test that blocked targets receive no request. |
| Data Protection key-ring loss makes stored endpoint secrets undecryptable. | Persist the key ring outside the web root, use a durable deployment volume, back it up, and fail closed until restored; secrets cannot be recovered from a database dump alone. |
| Secret rotation can strand in-flight deliveries if the receiver drops the old secret too early. | Keep the previous protected secret for 72 hours, version deliveries, and require the receiver to accept both secret versions during that grace period. |
| Slow or malicious receivers consume worker capacity. | 5-second connect and 10-second request timeouts, per-endpoint limit 4, global limit 32, bounded response reads, and no redirects. The database and worker are still shared resources. |
| Endpoint-specific metric labels create unbounded cardinality. | Metrics use only bounded event/outcome dimensions; endpoint IDs appear in access-controlled structured logs. |
| `device.connected` and `script.execute` include identifiers and operational metadata. | Authorize endpoint scope, minimize v1 fields, avoid output/text/media, restrict log access, and apply 14-day retention. No legal conclusion is made by the phrase “metadata-only.” |
| Multi-instance deployment can race claims or duplicate work. | It is outside v1. Provider-specific claim compare-and-set behavior must pass tests on all three supported databases before release; a future scale-out requires a separate design review. |

---

### Additional Resources

* `concepts_brief.md` — the brainstorm brief this draft consumes (all eight best-practice resolutions).
* `Server/Services/README.md` — current service inventory; this design's dispatcher follows `ScriptScheduler`/`DataCleanupService` patterns.
* Standard Webhooks specification — the cross-vendor wire contract this design adopts (headers, signature input, timestamp tolerance).

### Future Improvements

* **Optional aggregate events** — consider a keyboard count-only digest only after a separate privacy review and a named receiver request.
* **Template rendering for one-event payloads** — consider only if per-endpoint body customization is required and can preserve the v1 field allowlist.
* **Multi-instance support** — requires a separate review of provider-specific claims, concurrent fan-out, lease ownership, and database contention; v1 makes no scale-out guarantee.

---

## Detailed Implementation Plan

This is the proposed file map for implementation, based on the design decisions above. It is not implementation code or evidence that the current code already provides these behaviors. Operational limits below are proposed defaults and must remain consistent with the contract and tests. All paths are relative to the Remotely repository root (the `Captura` folder in this workspace).

### Target Files to Create

#### 1. `Shared/Entities/WebhookEndpoint.cs` ([Create]) — Endpoint entity

* **Type / Scope**: Shared EF entity.
* **Proposed Changes**: Implement the `WebhookEndpoint` schema in Technical Plan §2, including current/previous protected secrets and secret versions, endpoint state, URL/event filters, scope, creator, and validation metadata. There is no plaintext secret column and no hash column.

#### 2. `Shared/Entities/WebhookEvent.cs` ([Create]) — Event outbox

* **Type / Scope**: Shared EF entity.
* **Proposed Changes**: Persist one logical event with stable `EventId`, event type/schema, organization and optional group IDs, allowlisted payload JSON, occurrence/creation times, and fan-out status/lease fields.

#### 3. `Shared/Entities/WebhookDelivery.cs` ([Create]) — Endpoint delivery

* **Type / Scope**: Shared EF entity.
* **Proposed Changes**: A per-endpoint delivery/replay row referencing `WebhookEvent` and `WebhookEndpoint`; includes stable delivery ID, replay number, signing-secret version, status, attempts, retry time, and lease fields. Fields and indexes are defined in Technical Plan §2.

#### 4. `Shared/Entities/WebhookDeliveryAttempt.cs` ([Create]) — Attempt history

* **Type / Scope**: Shared EF entity.
* **Proposed Changes**: Append-only row per started HTTP attempt with start/completion times, attempt number, response status, latency, and bounded error kind. It contains no URL, secret, request body, or response body.

#### 5. `Shared/Dtos/WebhookPayload.cs` ([Create]) — Payload DTOs

* **Type / Scope**: Shared DTOs, one per catalog event + the envelope.
* **Proposed Changes**: `WebhookEnvelope<TData>` contains `type`, `schema_version`, `event_id`, `data`, `delivery_id`, and `occurred_at`. V1 DTOs are `DeviceConnectedData` and `ScriptExecuteData`. Types for future events may be documented but are not wired to producers in v1. No DTO includes `details_url`, script input/output, keyboard text, or media bytes.

#### 6. `Server/Services/IWebhookPublisher.cs` + `Server/Services/WebhookPublisher.cs` ([Create]) — Producer API

* **Type / Scope**: Server service (domain-facing).
* **Proposed Changes**: `AppendEvent(AppDb dbContext, WebhookEvent evt)` only adds the row to the caller's context; it performs no save, fan-out, or HTTP. All v1 producers add the event to the same context as their domain write. Do not hide an independent context/transaction inside the producer API. Future producers that cannot share a transaction remain out of scope until their loss window is addressed.

#### 7. `Server/Services/WebhookDispatcher.cs` + `WebhookHttpTransport.cs` ([Create]) — Background worker and pinned transport

* **Type / Scope**: `IHostedService` / `BackgroundService`.
* **Proposed Changes**: Implement the two loops, fan-out transaction, provider-aware claim contract, scoped DI access, bounded parallel lanes, request-pinned per-attempt transport, secret-version lookup, append-only attempts, retry classification, and metrics defined in §§2–3. Preserve stable `EventId` and `webhook-id` semantics; the transport uses a fresh handler per attempt, not a pooled `IHttpClientFactory` handler.

#### 8. `Server/Services/WebhookSsrfGuard.cs` ([Create]) — Egress validator

* **Type / Scope**: Server service.
* **Proposed Changes**: Return a validated hostname plus the allowed resolved address set, or a typed policy error. Normalize all address forms; block private/special-use destinations and private suffixes; run at registration and per attempt. The transport connects to the validated address for that request.

#### 9. `Server/Services/WebhookSigning.cs` ([Create]) — Signature helper

* **Type / Scope**: Server service.
* **Proposed Changes**: Create a cryptographically random secret, protect/unprotect it with ASP.NET Core Data Protection, and compute the documented HMAC over `webhook-id.timestamp.rawBody`. Persist the Data Protection key ring as specified in NFR Security; do not reuse the non-Windows plaintext fallback in `UpdateSigningService`.

#### 10. `Server/API/WebhooksController.cs` ([Create]) — Management API

* **Type / Scope**: ASP.NET Core controller behind `ApiAuthorizationFilter`.
* **Proposed Changes**: `POST /api/webhooks` (create disabled; return secret once), `GET /api/webhooks` (never return secrets), `DELETE /api/webhooks/{id}` (disable only), `POST /api/webhooks/{id}/enable` (requires successful ping and valid destination), `POST /api/webhooks/{id}/test` (create a direct `TestPing` delivery even while disabled; return 202), `GET /api/webhooks/{id}/deliveries`, `POST /api/webhooks/{id}/retry` (new one-attempt delivery row using the current secret version, same `EventId`, allowed through 96-hour event age), `POST /api/webhooks/{id}/rotate-secret` (normal rotation retains previous version for grace period; `force=true` emergency rotation invalidates old versions, disables endpoint, and dead-letters old-version deliveries; return new secret once), and `GET /api/webhooks/metrics` (organization-scoped delivery/retry/fan-out counts and oldest due age, no endpoint labels). Check organization and device-group authorization on every operation.

#### 11. `Tests/Server.Tests/WebhookTests.cs` ([Create]) — Unit tests

* **Type / Scope**: MSTest; EF InMemory only for pure state/payload logic, relational SQLite for transaction and atomic-claim tests.
* **Proposed Changes**: Cover event fan-out idempotency, all state transitions, expired leases, exact seven-gap retry arithmetic, retry classification, manual replay identity/current-secret selection, ping while disabled, and serialized-payload negative assertions for script input/output, keyboard text, URLs, and media bytes. Do not use EF InMemory to claim relational transaction or claim correctness.

#### 12. `Tests/Server.Tests/WebhookDispatcherTests.cs` ([Create]) — Dispatcher integration tests

* **Type / Scope**: MSTest + mock `HttpMessageHandler` for HTTP outcomes, plus dedicated pinned-transport and database-provider integration tests.
* **Proposed Changes**: Verify 2xx, 500-then-success with stable `EventId`/`webhook-id`, 404 and 3xx terminal handling, timeout/429 retry, rotation grace, ping queueing, authorization filtering, and no HTTP call for blocked DNS results. A separate `WebhookPinnedTransportTests` fixture exercises the real `SocketsHttpHandler.ConnectCallback` with controlled DNS and a local TLS endpoint to assert the dialed IP and hostname/certificate validation; an `HttpMessageHandler` mock is not accepted as evidence for those properties. A `WebhookDbProviderTests` suite verifies migrations, uniqueness, transactions, claims, and lease expiry on SQLite, SQL Server, and PostgreSQL. Assert append-only attempt records and state transitions.

#### 13. EF Migrations × 3 ([Create])

* **Type / Scope**: Server migrations.
* **Proposed Changes**: Scaffold the four tables and indexes for SQLite, SQL Server, and PostgreSQL. Follow existing migration-folder conventions and validate migrations plus claim transactions against all three providers. Reuse `AppDb`'s existing `DateTimeOffset` conversion; do not add per-entity converters. Retention is implemented in application code, not a database TTL.

### Target Files to Modify

#### 14. `Server/Data/AppDb.cs` ([Modify]) — DbSets and model configuration

* **Proposed Changes**: Add DbSets for `WebhookEndpoint`, `WebhookEvent`, `WebhookDelivery`, and `WebhookDeliveryAttempt`; configure the FKs, maximum lengths, unique event/endpoint/replay key, and claim/history indexes from §2. Reuse the existing `DateTimeOffset` conversion behavior.

#### 15. `Server/Program.cs` ([Modify]) — DI, key persistence, and hosted service

* **Proposed Changes**: Register the scoped producer/outbox and `IWebhookHttpTransport` services, dispatcher hosted service, request-pinned SSRF guard, signing helper, and durable Data Protection key-ring location with a stable application name. The hosted dispatcher uses `IServiceScopeFactory`; it must not capture scoped services. The transport creates a fresh `SocketsHttpHandler` per attempt with `ConnectCallback`, `AllowAutoRedirect=false`, connection timeout, and bounded request/response handling.

#### 16. `Server/Services/DataService.cs` ([Modify]) — V1 event producers and retention

* **Proposed Changes**: Append `device.connected` in `AddOrUpdateDevice` only on an offline/new-to-online transition and `device.disconnected` in `DeviceDisconnected` only on an online-to-offline transition. In `AddScriptResult`, use one explicit database transaction: save the `ScriptResult` once to obtain its generated ID, append the `script.execute` event with result IDs, `HadErrors`, runtime, and server receipt time, save the event, then commit; if event persistence fails, roll back and do not return success. In `CleanupOldRecords`, prune expired events, deliveries, attempts, and eligible disabled endpoints in bounded batches.

#### 17. `Server/Options/ApplicationOptions.cs` ([Modify]) — Operational defaults

* **Proposed Changes**: Bind non-secret operational defaults for poll interval (2 seconds), batch size (50), per-endpoint concurrency (4), global concurrency (32), and retention (14 days). Keep the 8-attempt/72-hour delivery contract fixed in v1 rather than independently configurable. `AllowHttpOptIn` is stored per endpoint, not in global options. Do not add signing secrets to configuration files.

The following files are **not** changed in v1: `Server/API/AlertsController.cs` (alert compatibility), `Server/Hubs/AgentHub.cs` (event persistence is inside `DataService`), `ScreenshotService.cs`, `CameraService.cs`, `AudioService.cs`, and `KeyboardLogService.cs` (their events are future work). Retention belongs in `DataService.CleanupOldRecords`, not the four-line `DataCleanupService` delegation method.

### Verification Plan

#### Five-phase implementation order and exit checks

1. **Persistence foundation:** add the four entities, mappings, indexes, migrations, and provider-specific atomic claim implementation. Exit only when schema/migration and claim/lease integration tests pass for SQLite, SQL Server, and PostgreSQL.
2. **Producer and authorization path:** add the event append operation, device transition events, per-result script event, and background fan-out with current-scope authorization and idempotent uniqueness. Exit when producer transaction tests, permission-change tests, and fan-out crash/replay tests pass.
3. **Wire and endpoint management:** add DTOs, HMAC/Data Protection versioning, SSRF registration/send-time validation with IP pinning, and management REST API. Exit when signature vectors, rotation grace, secret-not-returned, disabled-endpoint, and blocked-address tests pass.
4. **Delivery worker:** add bounded claims, per-endpoint/global concurrency, timeout/body limits, retry classification, append-only attempts, dead letters, ping, and manual replay. Exit when the dispatcher suite verifies every state transition, the seven retry gaps, and replay history.
5. **Operations and release:** add bounded-batch cleanup, metrics/logging, deployment key-ring instructions, and rollback verification. Exit when all three provider migrations apply, full solution build/tests pass, and the E2E checklist below passes. If any provider cannot be exercised, provider-claim readiness remains unverified and release is blocked.

* **Compilation/Build**: `& "$env:USERPROFILE\.dotnet\dotnet.exe" build Remotely.sln` (explicit user-profile SDK 10.0.403; `dotnet` on PATH is SDK 9.0.315 and fails `NETSDK1045` on this repo).
* **Unit Tests**: `& "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln` — MSTest; must stay green. New tests: webhook claim/retry transitions, signature and SSRF guard tests, payload-negative tests (no `Text`/bytes on DTOs).
* **Static Analysis & Linting**: `dotnet build` with warnings treated as diagnostics (0 new warnings). No new lint system is introduced by this design.
* **Test Coverage Requirement**: The dispatcher integration tests (mock `HttpMessageHandler`) are mandatory — no gate promotion without them; coverage for successful, retried, dead-letter, timeout, and SSRF paths.
* **E2E/Integration**: Register an endpoint (valid URL), trigger a device event, and assert envelope headers + signature are verifiable with the issued secret and attempt history is visible. Register `http://127.0.0.1/…` and assert rejection without a connection. Exercise a 500 response through all 8 attempts using an injected `TimeProvider` to advance the fixed retry schedule; do not wait 55+ hours in a wall-clock test.
* **Claimed evidence boundary**: This plan is a design only; no implementation code has been written in this workflow. Test results may not be claimed until commands above are actually run.

---

*Drafted with EGM*
