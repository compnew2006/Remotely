# Webhook Delivery for Remotely

* **Author:** noiemany
* **Status:** Draft
* **Date:** 2026-11-08
* **Source:** `concepts_brief.md`

---

## Overview

Remotely is a remote-control server (ASP.NET Core, Blazor, SignalR) cloned from GitHub. The author did not write this codebase. Teams use it to control devices interactively, run scripts, and capture screenshots, camera clips, audio clips, and keyboard logs.

Today, external systems cannot be informed when an event occurs. The only outbound HTTP path is the alert API request in `AlertsController.Create`, and it is fire-and-forget: no retry, no signature, no delivery log. A missed notification is a missed integration, and an operator cannot tell whether a receiver failed or the alert never left the server.

This design adds webhook delivery as a passive, side-band channel. When a listed event occurs (a device connects, a script run completes, an artifact becomes available), the server records the event in its database in the same transaction, and a background dispatcher sends an HTTP POST to a registered endpoint. The dispatcher retries with backoff, writes a delivery log, and signs each attempt.

Three constraints shape the whole design:

1. Webhooks must never block the normal remote-control path (SignalR, UI, API). They are enqueue-only from domain code; all HTTP delivery happens in a background worker.
2. Webhooks must never become a data leak. Payloads are metadata-only by construction; raw keyboard text and media bytes are structurally unreachable, and every endpoint is SSRF-guarded before any call.
3. The alert path does not stay a second system. `AlertsController` becomes the enqueue path, and the dispatcher is the single sender from that point onward.

## The Problem

Two problems need solving, and they pulled in opposite directions during brainstorming.

**Problem 1 — events are invisible outside the server.** A script run finishes, a device comes online, a keyboard log is captured — none of that reaches a ticketing system, a monitor, or an automation pipeline without manual polling. The existing alert API request is the only outbound mechanism, and it does not tell anyone whether its delivery succeeded. Teams therefore build brittle polling loops, or they capture screenshots and clips by hand.

**Problem 2 — a naive webhook is an attack surface.** The server is internet-facing and stores sensitive material (typed text, script output that can contain credentials, captured media). A straightforward "POST to user-supplied URL, retry until it works" design is a Server-Side Request Forgery channel, an exfiltration channel, and a reliability trap — slow receivers would stall the control plane, poison endpoints would retry forever, and a plaintext signing secret would let a database dump forge deliveries. The Goldfish reviews found six blockers in this category alone, and the concept is not implementable until each of them is closed.

The solution is one delivery plane with a fixed contract: transactional outbox, background dispatcher, metadata-only payloads, SSRF guard, encrypted signing secrets, bounded retries with dead-letter, and a delivery log. None of these is optional; each exists because a Goldfish finding demonstrated the failure mode on this exact codebase.

## Goals

1. **Events reach external systems reliably.** Every listed event is recorded durably before any send is attempted, and a receiver that comes back online receives deliveries within the stated retry horizon (8 attempts over 72 hours).
2. **The control path is unaffected.** No domain code path (`AgentHub`, controllers, services) awaits an HTTP call; only enqueue happens inline, in the same database transaction as the event. A slow or malicious endpoint degrades only its own subscription stream.
3. **Receivers can trust what they get.** Every delivery carries a stable event ID, an idempotency key, a timestamp, and an HMAC-SHA256 signature under the Standard Webhooks wire format. At-least-once delivery is the only promise; exactly-once is explicitly not promised.
4. **Sensitive data cannot ride along.** Payloads contain identifiers, timestamps, counts, and a signed `details_url` — never raw `KeyboardLog.Text`, never screenshot/camera/audio bytes. A generic screenshot hook cannot be turned into a file-exfiltration hook by configuration.
5. **An operator can answer "did it arrive?" in 3 AM conditions.** Every attempt is logged with event ID, subscription ID, attempt number, response code, and latency; dead letters are visible and re-enqueue-able with the same idempotency key; a test- ping exists before an endpoint is enabled.
6. **One system, not two.** The alert path and the webhook path are the same enqueue path. `AlertsController` becomes an enqueue of a delivery rather than an inline send.

## Non-Goals

1. No change to the desktop agent, the desktop client, or the SignalR/agent protocol.
2. No exactly-once delivery guarantee.
3. No raw `KeyboardLog.Text`, screenshot bytes, camera clip bytes, or audio clip bytes in any webhook payload.
4. No multi-instance/cluster deployment support; the single-process assumptions of the codebase (static caches, `SemaphoreSlim` dispatches) are inherited, not solved.
5. No message broker (RabbitMQ/Kafka) and no new persistence infrastructure beyond the existing Sqlite/SqlServer/PostgreSql providers.
6. No user-facing design beyond a subscription list plus a deliveries view with a test ping — a settings page designer is outside scope for v1.

## Open Questions

* **Q1:** Does the team accept that the `AlertsController.Create` inline send is retired entirely in favor of the outbox, or must alerts keep an immediate inline path for a transition period?
* **Q2:** Should the five catalogued production events (7 events) be reduced for v1 to a smaller set (e.g., `device.connected` + `script.execute` only) to ship sooner?

## Critical User Journeys (CUJs)

### CUJ 1: Register and enable an endpoint

* **Given**: An organization administrator in the Blazor UI, signed in with access to an organization.
* **When**: They create a webhook subscription: URL, events, and receive a one-time-displayed signing secret.
* **Then**: The endpoint is registered disabled; a "Test delivery" ping is offered; enabling requires the ping to succeed and SSRF validation to pass both at registration and at send time.

### CUJ 2: Receive a device-connection event with fallback

* **Given**: A subscriptions exists with `.local` HTTPS disabled and receiver reachable.
* **When**: A device comes online and an event row is inserted in the same `DbContext` transaction as the device upsert.
* **Then**: The dispatcher claims the row, sends the envelope with `webhook-id`, `webhook-timestamp`, `webhook-signature`, and `Idempotency-Key`; the delivery log records status, attempts, and latency; success requires a 2xx.

### CUJ 3: Receiver outage and dead-letter recovery

* **Given**: An endpoint returns 500 for every attempt.
* **When**: Backoff runs 1 min → 2 min → 5 min → 15 min → 1 h → 6 h with ±20% jitter, up to 8 attempts within 72 hours.
* **Then**: The delivery moves to the dead-letter state, remains visible in the deliveries view, and manual retry re-enqueues it with the same `EventId` (therefore the same `Idempotency-Key`) so deduplication still works.

### CUJ 4: Receiver is brought online after exhaustion

* **Given**: A dead-letter row exists.
* **When**: An operator clicks manual retry (or the endpoint is re-enabled).
* **Then**: The delivery is re-queued and delivered without a new event ID; the log shows the full history including the dead attempts.

---

## Technical Plan (Architecture)

Webhooks are implemented as a **transactional outbox** with a **pull-based background dispatcher**. Nothing in the domain path sends HTTP; everything is a database write. This is the only topology the seven Goldfish lenses agree on, and it directly reuses two existing patterns in this codebase: the background-service shape of `ScriptScheduler`/`DataCleanupService` (scoped context per tick + `SemaphoreSlim` dispatch lock) and the "producer inserts, worker drains" split used by `DataService.CleanupOldRecords`.

```text
                    EVENT (e.g. device came online)
                         |
     +-------------------------------------------+
     | Enqueue (same DbContext transaction)       |
     | Alert + WebhookDelivery (Queued)          |
     +-------------------------------------------+
                         |
     +------------------ +------------------ +-----------+
     | WebhookDispatcher (IHostedService)                  |
     |                                                    |
     |  1. Claim pending rows (Status=Queued,            |
     |     NextAttemptUtc<=Now, lease + batch size)        |
     |  2. SSRF + egress validation (per attempt)          |
     |  3. Sign (HMAC-SHA256), POST through                |
     |     IHttpClientFactory (shared, timeout+limits)     |
     |  4. Record attempt (status, code, latency)          |
     |  5. Succeeded -> retention 14d                       |
     |     Failure -> NextAttemptUtc += backoff, jitter    |
     |     Max attempts / poison -> DeadLetter             |
     +------------------ +------------------ +-----------+
                         |
           +-------------+-------------+
           | Receiver (external HTTPS) |
           |  dedupe by EventId header |
           +---------------------------+
                         |
            Delivery log + metrics + dead-letter view
```

### 1) Event Producers (enqueue only)

The domain code that already owns each event inserts a `WebhookDelivery` row in the same transaction:

| Event | Producer (today) | Enqueue site |
|---|---|---|
| `device.connected` / `device.disconnected` | `AgentHub.DeviceCameOnline` + `DataService.DeviceDisconnected` | AgentHub, status update path |
| `script.execute` | `DataService.AddScriptRun` / `ScriptResultsController.Post` | Script run completion |
| `screenshot.available` | `ScreenshotService.SaveScreenshot` | Save-success path |
| `camera.clip.available` | `CameraService.SaveClip` / `CompleteUpload` | Save-success path |
| `audio.clip.available` | `AudioService.SaveClip` / `CompleteUpload` | Save-success path |
| `keyboard.log.available` | `KeyboardLogService.SaveLog` | Save-success path (count/digest only) |

Enqueue rules enforced in `IWebhookPublisher` (a domain-facing API):

- No `await HttpClient` anywhere in the producer call chain. The producer writes one row and returns.
- Payload is built from a per-event DTO — never an EF entity. The serializer has no field that can hold `KeyboardLog.Text` or media bytes.
- A per-org subscriptions fan-out happens at enqueue time, filtered through `DataService.DoesUserHaveAccessToDevice` and `FilterUsersByDevicePermission`, so a subscription can never receive a device the registrant cannot see.

### 2) Outbox Entities

```text
WebhookEndpoint
  Id (string) | OrganizationId | DeviceGroupId? (nullable)
  Url, Events (comma list of catalog names), Enabled bool
  SigningSecretHash (bytes; never plaintext in DB — extended column)
  EventsScope: org | device_group
  RequireSigning bool (default true for non-loopback)
  CreatedBy, CreatedUtc

WebhookDelivery
  Id (string)                       // stable EventId across retries
  SubscriptionId (FK -> WebhookEndpoint)
  EventType (string), SchemaVersion (string)   // "v1"
  PayloadJson (string, indexed? no — capped at 256KB)
  Status ∈ Queued | Delivering | RetryPending | Succeeded | Dead
  Attempts int, MaxAttempts int (=8)
  NextAttemptUtc (DateTimeOffset), LeaseUntil (DateTimeOffset)?  // claim lease
  LastHttpStatusCode int?, LastLatencyMs int?, LastErrorKind string?
  ClaimedBy (string? — worker id for lease ownership)
  CreatedUtc, CompletedUtc?
```

- Indexes: `(Status, NextAttemptUtc)` for the claim query; `(SubscriptionId, CreatedUtc)` for the deliveries view; `(OrganizationId)` for fan-out.
- Claim is transactional: `UPDATE ... WHERE Status='Queued' AND NextAttemptUtc<=now, SET Status='Delivering', LeaseUntil=now+lease-minutes, ClaimedBy=workerId` — no `SELECT FOR UPDATE` (no portable cross-provider semantics; lease + short lease duration is enough for the single-process deployment, and safe if it ever scales out).
- Retention: `Succeeded`/`Dead` rows kept 14 days, pruned by `DataCleanupService` extension. Claimed rows that never complete are reclaimed when their lease expires.

### 3) Dispatcher — `WebhookDispatcher : IHostedService`

Follows `ScriptScheduler`/`DataCleanupService` shape: `BackgroundService`, periodic tick (2 s), then:

1. Create a scope, resolve `IAppDbFactory`, claim a batch (e.g. 50).
2. For each claimed delivery:
   - **SSRF guard (per attempt, before any HTTP)**: scheme allowlist (https only by default; http only with an explicit per-endpoint opt-in flag and never for a private host), DNS resolution, block any resolved IP in RFC1918 / loopback / link-local / reserved / multicast / metadata (169.254.169.254) / IPv6 ULA + zone IDs. No redirects to a different origin. A delivery that fails the guard records the failure kind and is retried like any failure (DNS rebinding is precisely why this re-runs at send time, not only at registration).
   - `/Sign`: `signature = base64(HMAC-SHA256(key = endpoint secret, msg = webhook-id + "." + unix-timestamp + "." + rawBody))`, constant-time verify by receivers, 5-minute timestamp skew tolerance.
   - `POST` through `IHttpClientFactory` (already registered at `Program.cs` for `UpgradeService`) with connect 5 s / response 10 s timeouts, buffered request/response body caps (256 KB / 64 KB), `AutoRedirectHandler` disabled.
   - Record attempt: `StatusCode`, `LatencyMs`, `ErrorKind` (nullable, never payload/secret), `LastHttpStatusCode`. Success = 2xx; 3xx follows the strict no-redirect rule.
3. Failure path: `Attempts += 1`; `NextAttemptUtc = now + backoff(attempt)` with ±20% jitter; on 4xx except 408/429, no retry (immediate `Dead`). On `Attempts >= MaxAttempts` or beyond the 72-hour horizon, `Status = Dead`.

Per-endpoint concurrency cap of 4 prevents one slow receiver from head-of-line blocking the queue. Every attempt writes the structured log fields the runbook expects (`EventID`, `SubscriptionID`, `Attempt`, `StatusCode`, `LatencyMs`, `ErrorKind`) and increments the metrics below.

### 4) Wire Contract — Standard Webhooks

Every attempt is the same shape (a receiver implements this one contract):

```http
POST {endpoint-url}
Content-Type: application/json
webhook-id: <delivery Id>
webhook-timestamp: <unix seconds>
webhook-signature: v1,<base64-hmac>
X-Remotely-Event: <event type>
X-Remotely-Delivery: <delivery Id>
Idempotency-Key: <EventId>

{ "type": "device.connected", "data": {…}, "delivery_id": "<uuid>", "occurred_at": "<rfc3339>" }
```

Per-event `data` block (metadata-only, with one complete example per event in the draft):

| Event | `data` carries |
|---|---|
| `device.connected` / `device.disconnected` | `DeviceId`, `OrganizationId`, `DeviceGroupId?`, `PublicIP` (optional per allowlist), `OccurredAt` |
| `script.execute` | `ScriptRunId`, `ScriptId`, `DeviceIds[]`, `Status` (success/failure), `StartedAt`, `CompletedAt` |
| `screenshot.available` | `ScreenshotId`, `DeviceId`, `TimeStamp`, `SizeBytes`, signed `details_url` |
| `camera.clip.available` | `ClipId`, `DeviceId`, `CameraName`, `Quality`, `DurationSeconds`, signed `details_url` |
| `audio.clip.available` | `ClipId`, `DeviceId`, `Quality`, `DurationSeconds`, `Source` |
| `keyboard.log.available` | `LogId`, `DeviceId`, `WindowTitle`, `ProcessName`, `Count`, `Window`Digest only — no `Text`, no `IsPasswordField` body |

Receivers deduplicate by `EventId` within a 24 h window; ordering is explicitly not guaranteed across deliveries.

### 5) Registration API + UI

- REST (or Blazor admin) endpoints for register/list/disable/test-ping on `WebhookEndpoint`; every operation checks `DataService` organization and device-group authorization; `details_url` requires the same `ApiAuthorizationFilter` token path the existing controllers use.
- SSRF validation at registration (scheme, DNS, private-range block, private suffix `.local/.internal/.intranet`) with actionable error text; both write-time and send-time checks are stored as `LastValidationKind` for the operator.
- Deliveries view per subscription: last N attempts with status, attempt count, response code, latency, and dead-letter list; "Test delivery" sends a `ping` event under the exact wire contract; manual retry re-enqueues with the same `EventId`.

### 6) AlertsController transition

`AlertsController.Create` (the only current sender) stops sending inline. The `Alert` row creation stays; the outbound HTTP call is replaced by a `WebhookDelivery` enqueue through `IWebhookPublisher`, so existing integrations that call `api/alerts/create` receive the outbox semantics (retry, signature, log) without a second code path. The `Alert` entity and the email path (`EmailSenderEx`) are untouched.

## Alternatives Considered

### Alternative A: Reuse the `AlertsController` HTTP path as-is, no outbox

* **Pros**: Zero new infrastructure; the system already offers generic HTTP delivery (method, URL, JSON body, arbitrary headers) with a persisted `Alert` row and an SMTP path; the position is backed by the project's own code (`Server/API/AlertsController.cs`, `Shared/Entities/Alert.cs`).
* **Cons**: That path is fire-and-forget. It does not retry, does not sign, does not record attempts, and leaves an operator unable to distinguish a dropped delivery from a successful one 3 AM triage later. It does not dedupe, does not redeliver, and carrying `ApiToken.Secret`/`ApiRequestUrl` semantics forward would perpetuate SSRF-hardening debt without closing it.
* **Reason for Rejection**: Rejected by author: the existing path's blind spots are the failure mode this feature exists to close. However, the design does not discard it; `AlertsController` becomes the enqueue entry point (Technical Plan §6), so the alternative's integration surface is preserved inside the chosen design rather than as a parallel system.

### Alternative B: Extend `AlertOptions` with payload templating (`{{DeviceId}}`/`{{Message}}` substitution)

* **Pros**: Minimal change; solves the "body shaping" problem without any subscription model or new entities; aligns with the existing success/error trigger semantics (`SavedScript.GenerateAlertOnError` / `SendEmailOnError`).
* **Cons**: It touches only body formatting. It does not fix reliability, retries, secrets, ordering, dedupe, or observability — the six blockers the Goldfish review marked as shipping gate. It also keeps the per-call SMTP/HTTP model instead of the durable-queue model, so a slow receiver still stalls the trigger path.
* **Reason for Rejection**: Rejected by author: templating is a genuine smaller increment and stays as the fallback if the team refuses a subscription model entirely. But it cannot carry the credibility load of a webhook contract (Standard Webhooks wire format, HMAC, idempotency), so it is kept as a labeled smaller increment, not as the platform.

### Alternative C: Rely on email or polling through the existing API

* **Pros**: Nothing new to build; email is already delivered via `EmailSenderEx` and polling works today through the controllers.
* **Cons**: Email lacks machine-readable delivery semantics (no status codes, no idempotency key, no dead letter, no bounded retry contract) and polling misses low-latency hooks entirely while adding load per second. Both keep consumers guessing at structure and latency.
* **Reason for Rejection**: Rejected by author: they are complementary channels for humans, not replacements for a machine event contract. The design instead gives machines the envelope, ids, and signature that email and polling structurally cannot.

### Alternative D: Message broker (RabbitMQ/Kafka)

* **Pros**: The heaviest durability and throughput story; mature DLQ and redelivery semantics out of the box.
* **Cons**: Vast operational burden for a single-process .NET deployment whose codebase already assumes in-memory caches and lock-guarded dispatches. It would introduce a new infrastructure dependency, a new failure domain, and a deployment story the project does not otherwise have. Cross-provider equivalence (SQLite default, SQL Server, PostgreSQL in `Program.cs`) would need its own connector and migration plan.
* **Reason for Rejection**: Rejected by author: the transactional outbox already gives durable queuing on the existing databases; importing a broker solves a problem — multi-instance fan-out — the codebase explicitly does not have today.

---

## Non-Functional Requirements

- **Security & privacy**
  - Signed deliveries only for non-loopback endpoints (signature defaults ON for anything that is not a loopback URL). Plain HTTP / unsigned is an explicit per-endpoint opt-in flag with a warning — disabled by default.
  - Signing secrets are encrypted at rest with ASP.NET Core Data Protection (DPAPI on Windows — same model as `UpdateSigningService`, key outside the web root in `AppData/keys/`), never logged, never returned in GET, displayed once on creation, rotated by immediate invalidation of the old secret.
  - SSRF guard runs on both the registration write path and every send attempt: https-only scheme allowlist, DNS resolve + block RFC1918/loopback/link-local/reserved/multicast/metadata ranges (incl. IPv6 ULA and `169.254.169.254`), reject numeric/local names and private suffixes, no redirects to a different origin, and request/response body caps. A failed guard never sends; it records `ErrorKind = dns-blocked`/`scheme-blocked` and retries like any failure.
  - Metadata-only payloads and a per-event field allowlist are compile-time reachable through dedicated DTOs: there is no code path that can serialize `KeyboardLog.Text` or media bytes into a webhook body.
  - No request body or secret is logged; structured log fields are attempt metadata only.
- **Performance & latency**
  - Producer-path cost is one integer row insert in the same transaction — no awaits, no HTTP. The dispatcher batch size (50) and per-endpoint concurrency cap (4) keep a slow receiver from pulling the queue down.
  - The claim query is index-driven (`(Status, NextAttemptUtc)`); poll tick 2 s; queue-lag is a first-class metric (`webhook_queue_lag_seconds`).
  - No real-time delivery SLA is promised — expected steady-state lag is well under the lowest retry step (1 minute).
- **Reliability**
  - At-least-once with a stable `EventId` carried in `webhook-id` and `Idempotency-Key`; exactly-once is not promised. Receivers dedupe within 24 h.
  - 8 attempts with exponential backoff (1 min → 2 min → 5 min → 15 min → 1 h → 6 h, jitter ±20%), 72-hour horizon, then dead letter. No retry on 4xx except 408/429.
  - Claim-with-lease protects against dispatcher crash; a claimed row whose lease expires is reclaimed and retried.
- **Rollout & rollback**
  - Feature is additive only: new tables, new endpoints, new hosted service. No existing hub/API signature changes; existing agents, desktop clients, and SignalR protocol are untouched, so rollback is: stop the dispatcher, leave rows in place (they are inert), and revert code. Nothing that was stored before the feature needs migrating backward.
  - Migrations must be scaffolded for all three providers; server must fail closed as today when auto-migration is disabled. Git history already has `AllowDbAutoMigration`-style ops gating — do not touch it.

---

### Additional Resources

* `concepts_brief.md` — the brainstorm brief this draft consumes (all eight best-practice resolutions).
* `Server/Services/README.md` — current service inventory; this design's dispatcher follows `ScriptScheduler`/`DataCleanupService` patterns.
* Standard Webhooks specification — the cross-vendor wire contract this design adopts (headers, signature input, timestamp tolerance).

### Future Improvements

* **Optional digest events** — `keyboard.digest` / hourly summary events instead of per-burst hooks, if real receiver volume demands it.
* **Template rendering for one-event payloads** — if the team later wants per-endpoint body overrides without compromising the metadata-only rule.
* **Multi-instance support** — claim-with-lease is already safe for scale-out; a horizontal deployment then only needs workers, no schema change.

---

## Detailed Implementation Plan

This is the exact file list. No implementation code is included; APIs and schemas are named so an implementer can build it without further design decisions. All paths are relative to the repository root of the Remotely checkout (the `Captura` folder in the current workspace).

### Target Files to Create

#### 1. `Shared/Entities/WebhookEndpoint.cs` ([Create]) — Entity

* **Type / Scope**: Shared EF entity.
* **Proposed Changes**: Columns per Technical Plan §2. `SigningSecretHash` is a `byte?` holding the Data Protection protected bytes; there is no `string Secret` column anywhere.

#### 2. `Shared/Entities/WebhookDelivery.cs` ([Create]) — Entity

* **Type / Scope**: Shared EF entity.
* **Proposed Changes**: Columns per Technical Plan §2, including `Status` enum (`WebhookDeliveryStatus`), `EventId` (stable across retries), `Attempts`, `MaxAttempts = 8`, `NextAttemptUtc`, `LeaseUntil`, `LastHttpStatusCode`, `LastLatencyMs`, `LastErrorKind`, `CreatedUtc`, `CompletedUtc`.

#### 3. `Shared/Dtos/WebhookPayload.cs` ([Create]) — Payload DTOs

* **Type / Scope**: Shared DTOs, one per catalog event + the envelope.
* **Proposed Changes**: `WebhookEnvelope<TData>` (`type`, `data`, `delivery_id`, `occurred_at`), one metadata-only DTO per event (`DeviceConnectedData`, `ScriptExecuteData`, `ScreenshotAvailableData`, `CameraClipAvailableData`, `AudioClipAvailableData`, `KeyboardLogAvailableData`). By construction there is no field for `KeyboardLog.Text` or media bytes. `details_url` is a signed URL carrying `ApiAuthorizationFilter`-compatible auth (expiring like `ExpiringTokenService` tokens).

#### 4. `Server/Services/IWebhookPublisher.cs` ([Create]) — Producer API

* **Type / Scope**: Server service (domain-facing).
* **Proposed Changes**: `Task EnqueueAsync(WebhookEvent evt, CancellationToken ct)` — the only entry point producers call. Builds the per-event DTO, fans out to matching subscriptions through org/device-group authz, inserts `WebhookDelivery` rows in the caller's transaction, returns. No HTTP, no awaits on delivery.

#### 5. `Server/Services/WebhookDispatcher.cs` ([Create]) — Background worker

* **Type / Scope**: `IHostedService` / `BackgroundService`.
* **Proposed Changes**: 2 s tick → claim batch (50) → per delivery: SSRF guard, sign, POST via `IHttpClientFactory` (connect 5 s / response 10 s, body caps 256 KB / 64 KB, redirects disabled), record attempt (status, code, latency, `ErrorKind`), apply backoff (1 m → 2 m → 5 m → 15 m → 1 h → 6 h, ±20% jitter), dead-letter at 8 attempts or 72 h, 4xx (except 408/429) → dead immediately. Structured logging fields per attempt; metrics: `webhook_deliveries_total`, `webhook_retry_total`, `webhook_queue_depth`, `webhook_queue_lag_seconds`.

#### 6. `Server/Services/WebhookSsrfGuard.cs` ([Create]) — Egress validator

* **Type / Scope**: Server service.
* **Proposed Changes**: `ValidateAsync(string url, bool allowHttp)` → success or a `WebhookValidationError` (`Scheme`, `DnsBlocked`, `PrivateRange`, `PrivateSuffix`, `RedirectToInternal`). Runs DNS resolution and blocks RFC1918 / loopback / link-local / reserved / multicast / metadata / IPv6 ULA / zone IDs; runs both at registration and per-attempt.

#### 7. `Server/Services/WebhookSigning.cs` ([Create]) — Signature helper

* **Type / Scope**: Server service.
* **Proposed Changes**: `CreateSecret()` (32 random bytes, Data Protection protected before write), `Sign(secret, webhook-id, timestamp, rawBody)` → `base64(HMAC-SHA256("webhook-id.unix-timestamp.rawBody"))`, `Verify(...)` (constant-time compare, 5-minute skew tolerance). Reuses the DPAPI/`AppData/keys` pattern from `UpdateSigningService`.

#### 8. `Server/API/WebhooksController.cs` ([Create]) — Management API

* **Type / Scope**: ASP.NET Core controller behind `ApiAuthorizationFilter`.
* **Proposed Changes**: `POST /api/webhooks` (register, SSRF validation, secret shown once), `GET /api/webhooks` (list, secrets never returned), `DELETE /api/webhooks/{id}` (disable or hard-delete), `POST /api/webhooks/{id}/test` (sends a `ping` event under the wire contract), `GET /api/webhooks/{id}/deliveries` (history), `POST /api/webhooks/{id}/retry` (manual retry with the same `EventId`), `POST /api/webhooks/{id}/rotate-secret`. Every operation re-checks org/device-group authorization through `DataService`.

#### 9. `Tests/Server.Tests/WebhookTests.cs` ([Create]) — Unit tests

* **Type / Scope**: MSTest + EF InMemory (existing test projects).
* **Proposed Changes**: Cover claim transition (`Queued→Delivering→RetryPending→Succeeded/Dead`), lease expiry reclaim, backoff schedule math, 4xx no-retry rule, dead-letter after max attempts, and payload construction asserting no `KeyboardLog.Text` / media bytes field exists on any DTO.

#### 10. `Tests/Server.Tests/WebhookDispatcherTests.cs` ([Create]) — Dispatcher integration tests

* **Type / Scope**: MSTest + mock `HttpMessageHandler`.
* **Proposed Changes**: Enqueue → dispatcher → success (2xx), → 500-then-success (retry + same `Idempotency-Key`), → 404 (dead immediately), → timeout (retry), → SSRF-blocked host (no HTTP call made, `ErrorKind`). Assert attempts, status transitions, and attempt logging.

#### 11. EF Migrations × 3 ([Create])

* **Type / Scope**: Server migrations.
* **Proposed Changes**: `dotnet ef migrations add AddWebhooks --context "SqliteDbContext"` (and `SqlServerDbContext`, `PostgreSqlDbContext`) from `Server/`. The SQL Server scaffold lands in the PostgreSql folder — move its files to `Server/Migrations/SqlServer/` afterwards (per AGENTS.md). Apply the `DateTimeOffset` converters for SQLite/PostgreSQL in `AppDb.OnModelCreating` the same way existing entities do. 14-day retention for `Succeeded`/`Dead` rows is enforced in code (`DataCleanupService`), not by a DB TTL.

### Target Files to Modify

#### 12. `Server/Data/AppDb.cs` ([Modify]) — DbSets

* **Proposed Changes**: Add `DbSet<WebhookEndpoint>` and `DbSet<WebhookDelivery>`; configure relationships and indexes `IX_WebhookDelivery_Status_NextAttemptUtc`, `IX_WebhookDelivery_SubscriptionId_CreatedUtc`, `IX_WebhookEndpoint_OrganizationId`; apply the `DateTimeOffset` converters the existing model uses.

#### 13. `Server/Program.cs` ([Modify]) — DI + hosted service

* **Proposed Changes**: `services.AddScoped<IWebhookPublisher, WebhookPublisher>()`; `services.AddSingleton<WebhookSsrfGuard>()` + `WebhookSigning`; `services.AddHostedService<WebhookDispatcher>()`; register the hardened `HttpClient` handler configuration (timeouts, redirect policy) via the existing `AddHttpClient()` setup at line 249. No other DI lifetime changes.

#### 14. `Server/API/AlertsController.cs` ([Modify]) — Remove inline send

* **Proposed Changes**: Delete the inline `IHttpClientFactory.SendAsync` branch; after saving the `Alert` row in the same `DbContext`, call `IWebhookPublisher.EnqueueAsync` for the alert event. The `Alert` entity and the response contract stay unchanged for existing callers.

#### 15. `Server/Hubs/AgentHub.cs` ([Modify]) — Device connect/disconnect enqueue

* **Proposed Changes**: On `DeviceCameOnline`/`DeviceDisconnected`, enqueue `device.connected`/`device.disconnected` with the same `DbContext` used for the status update. No HTTP, no await beyond the transaction. Same guard as every other producer: no hub method awaits delivery.

#### 16. `Server/Services/ScreenshotService.cs` ([Modify]) — `screenshot.available`

* **Proposed Changes**: Inject `IWebhookPublisher`; after `SaveScreenshot` commits, enqueue with metadata (`ScreenshotId`, `DeviceId`, `TimeStamp`, `SizeBytes`, signed `details_url`). File path and `MaxClipBytes` semantics unchanged.

#### 17. `Server/Services/CameraService.cs` ([Modify]) — `camera.clip.available`

* **Proposed Changes**: Same pattern at `SaveClip` and `CompleteUpload` success points; metadata only (`ClipId`, `DeviceId`, `CameraName`, `Quality`, `DurationSeconds`). The `MaxClipBytes` cap is never shipped in the body.

#### 18. `Server/Services/AudioService.cs` ([Modify]) — `audio.clip.available`

* **Proposed Changes**: Same pattern at `SaveClip` and `CompleteUpload` success points; metadata only (`ClipId`, `DeviceId`, `Quality`, `DurationSeconds`, `Source`).

#### 19. `Server/Services/KeyboardLogService.cs` ([Modify]) — `keyboard.log.available`

* **Proposed Changes**: At `SaveLog` success, enqueue a digest event (`LogId`, `DeviceId`, `WindowTitle`, `ProcessName`, `Count`, window). The DTO has no `Text` field; raw keystrokes never reach the enqueue path.

#### 20. `Server/Services/DataService.cs` ([Modify]) — `script.execute` + enqueue plumbing

* **Proposed Changes**: In `AddScriptRun` completion paths, enqueue `script.execute` (`ScriptRunId`, `ScriptId`, `DeviceIds`, `Status`, `StartedAt`, `CompletedAt`) in the same transaction. Reuse `DoesUserHaveAccessToDevice` / `FilterUsersByDevicePermission` at fan-out time.

#### 21. `Server/Services/DataCleanupService.cs` ([Modify]) — Retention

* **Proposed Changes**: Extend `RemoveExpiredDbRecords` to prune `WebhookDelivery` rows in `Succeeded`/`Dead` older than 14 days (batched; no `int.MaxValue` loads) alongside the existing script-run/result/shared-file pruning.

#### 22. `Server/Options/ApplicationOptions.cs` ([Modify]) — Webhook ops options

* **Proposed Changes**: Non-secret tunables only: `MaxAttempts` (default 8), `PerEndpointConcurrency` (default 4), `DeliveryRetentionDays` (default 14), penalized-epoch fallback for `AllowHttpOptIn`. Secrets never read from committed options.

### Verification Plan

* **Compilation/Build**: `& "$env:USERPROFILE\.dotnet\dotnet.exe" build Remotely.sln` (explicit user-profile SDK 10.0.403; `dotnet` on PATH is SDK 9.0.315 and fails `NETSDK1045` on this repo).
* **Unit Tests**: `& "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln` — MSTest; must stay green. New tests: webhook claim/retry transitions, signature and SSRF guard tests, payload-negative tests (no `Text`/bytes on DTOs).
* **Static Analysis & Linting**: `dotnet build` with warnings treated as diagnostics (0 new warnings). No new lint system is introduced by this design.
* **Test Coverage Requirement**: The dispatcher integration tests (mock `HttpMessageHandler`) are mandatory — no gate promotion without them; coverage for successful, retried, dead-letter, timeout, and SSRF paths.
* **E2E/Integration**: Manual — register an endpoint (valid URL), trigger a device event, assert envelope headers + signature are verifiable with the issued secret, deliveries log shows attempt history; register an endpoint with `http://127.0.0.1/…` and assert rejection; point an endpoint at 500 and assert dead-letter after 8 attempts.
* **Claimed evidence boundary**: This plan is a design only; no implementation code has been written in this workflow. Test results may not be claimed until commands above are actually run.

---

*Drafted with EGM*