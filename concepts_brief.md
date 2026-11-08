# Webhook Delivery for Remotely

* **Author:** noiemany
* **Date:** 2026-11-08

## Overview

Remotely is a remote-control server (ASP.NET Core, Blazor, SignalR) cloned from GitHub; the author did not write it. Today, when an event happens (device coming online, script finishing, screenshot saved, alerts created), the only outbound HTTP path is the alert API request in `AlertsController.Create`, and it is fire-and-forget: no retry, no signature, no delivery log. A team that wants to integrate Remotely with external systems (ticketing, monitoring, automation) needs to be told when events happen, and a missed notification is a failed integration. This feature adds webhook delivery as a passive, side-band notification channel: when a chosen event occurs, the server records it in the database and a background worker sends an HTTP POST to a registered endpoint, with retries, a delivery log, and an optional signature. It must never block the remote-control flow, and it must never be a new way to steal sensitive data.

## Analysis

Seven Goldfish lenses reviewed the anchored hypothesis independently. The findings cluster into simple agreements.

**The shape is agreed: transactional outbox, not inline delivers.** Every lens converges on the same mechanism: the domain code that produces the event inserts a delivery row in the same database transaction, and an isolated `IHostedService` (`WebhookDispatcher`) pulls rows and sends them. Putting `await HttpClient.PostAsync` inside `AgentHub.DeviceCameOnline` or a controller would stall the SignalR heartbeat path and the session caches, and a second outage of a receiver could drag down remote control. The dispatcher is the single pattern from `ScriptScheduler` and `DataCleanupService` (background service + scoped context per tick + `SemaphoreSlim` dispatch lock), just with claim rows.

**The topology is where the lenses split.** The `egm-zoom-out` lens argues hard against adding any of this: `AlertsController.Create` already does a generic HTTP POST with method, URL, JSON body, and arbitrary headers, plus persisted `Alert` rows and an SMTP path — so a second durable outbox is a second delivery plane for the same job, plus a new SSRF surface. This is the biggest objection in the set, and it is supported by code. What moves it from fatal to answerable is the existing alert path's own limits: it has no retry, no signature, no attempt log, and a 3 AM operator cannot tell a delivered alert from a dropped one. So the honest fork is: either accept the outbox as the upgrade of the alert path (not a parallel system), or stay with alerts. Either way, "webhook" must mean one system, not two.

**Security dominates the blocker list — six of the first findings are blockers here.** Remotely is internet-facing, and URL-driven outbound HTTP has a concrete attack list:

- SSRF. A naive `PostAsync(url)` allows private/loopback addresses, the cloud metadata address, and redirects into the network. The alert path already accepts arbitrary URLs and headers with no allowlist — repeating that mistake is not acceptable. Scheme allowlist (`https` only), DNS resolution plus private/loopback/link-local blocking, no following redirects to internal addresses, and strict timeouts are required at write time and at send time.
- Sensitive payloads. Keyboard logs store actual typed text; `ScriptResult.StandardOutput` can hold credentials; clips are files up to 250 MB. Defaulting to metadata-only (IDs, timestamps, counts) and forbidding raw `KeyboardLog.Text` is required; bytes must be sent through signed links that go through the API authorization filter, never as embedded webhook bodies.
- Secret handling. "Optional signature" plus plaintext storage (the `SettingsModel.SmtpPassword` precedent) produces forgeable webhooks from a DB dump. Signing secrets must be hashed or encrypted at rest (DPAPI pattern from the update-signing key), displayed once, rotatable, and never logged. HMAC-SHA256 over body + timestamp + delivery ID with a timestamp window is the minimum bar.

**The concept must become a contract before any code exists.** The naive-reader, implementation-readiness, and structural lenses all arrive at the same verdict: the anchored hypothesis is not actionable yet. A receiver cannot be built today because nothing is defined: the closed event catalogue (the current list uses "..." instead of names), each event's payload schema and example, the registration actor and permission scope (global vs device vs org vs device-group), the delivery guarantee (at-least-once with a stable `EventId` is the only sane option, and ordering is explicitly not promised), the retry schedule (max attempts, backoff, timeouts, dead letter), the wire contract (method, content type, headers, signature algorithm), and the delivery log a registrant sees (status, attempts, response code). Without those, any two implementers produce incompatible systems, and the unit of work is still a conversation, not an implementation.

**The invented middle path removes two of the three hard trade-offs.** The TRIZ lens names them plainly:

- Reliability vs. minimalism: the durable queue already exists — the database. A transactional outbox turns the database into the queue; no message broker is needed and a crash never loses an event.
- Isolation vs. latency: pull, not push. Domain code only enqueues; the dispatcher pulls, so a slow receiver degrades only its own stream.
- Once vs. once: exactly-once is abolished as a server promise. With a stable `EventId`, `Idempotency-Key`, and signed body, retries are harmless and receivers dedupe within a window.

**Scale and ops numbers are missing and will decide whether hooks hurt.** Keyboard bursts and heartbeat-adjacent events are high-frequency: a per-burst webhook is a database insert plus an HTTP POST per burst per device. Budgets are required before any event is allowed: per-event cardinality caps (keyboard hooks are digest/count only, never raw), per-org deliveries/sec and subscriptions, per-endpoint concurrency limits, claim-with-lease so scale-out never double-sends, poison handling (circuit-break, dead letter, TTL), and a delivery log with retention enforced by `DataCleanupService`. Reads of the codebase show a single-process deployment with in-memory caches, so scale-out duplicate delivery must be handled by design if it ever ships.

## Best-Practice Resolutions

Each Goldfish objection, resolved with how production webhook systems (GitHub, Stripe, Twilio, the Standard Webhooks specification) solve the same problem:

### 1. One delivery plane (answers the zoom-out objection)

The alert API path in `AlertsController.Create` becomes the enqueue path — the event producer inserts the `Alert` plus a `WebhookDelivery` row in the same transaction; the dispatcher is the **only** sender. No second system is created: existing scripts and operators that call `api/alerts/create` get the outbox for free. An `Alert` becomes an *enqueue* of a delivery rather than an inline call, which is also how Polymarket and Twilio migrated from synchronous hooks to delivery queues.

### 2. Standard wire contract (answers the naive-reader objection)

Adopt the cross-vendor Standard Webhooks shape rather than inventing one:

- `POST` to the registered URL, `Content-Type: application/json`.
- Headers (exact spec, stable across vendors): `webhook-id` (delivery ID), `webhook-timestamp` (Unix seconds), `webhook-signature` (versioned list, `v1,` prefix), plus `X-Remotely-Event` and `X-Remotely-Delivery`. These are renamed aliases of the Standard Webhooks names if we keep the `X-Remotely-*` branding, but the names, semantics, and signature input are identical to the spec: `signature = base64(HMAC-SHA256(key=secret, msg="{webhook-id}.{webhook-timestamp}.{payload}"))`.
- Payload envelope: `{"type": "<event>", "data": {…}, "delivery_id": "<uuid>", "occurred_at": "<rfc3339>"}`. Every event has one example in the brief before any endpoint is built.
- `Idempotency-Key: <EventId>` on every attempt (same key across retries) so receivers dedupe by it. At-least-once is the only guaranteed delivery semantics; exactly-once is explicitly not promised.

### 3. Closed event catalogue — with default metadata-only payloads (answers security + readiness)

Event names are closed and versioned (`v1`) from the start: `device.connected`, `device.disconnected`, `script.execute`, `screenshot.available`, `keyboard.log.available`, `camera.clip.available`, `audio.clip.available`. Naming rule: `<entity>.<verb>`; additive-only evolution within a version; a new event always gets a new type string, never a renamed field. Per event, the payload carries **metadata only** (device ID, event ID, timestamps, counts, a short preview, and a signed `details_url` that requires API authorization to fetch full content). Raw `KeyboardLog.Text` and media bytes are structurally unreachable — the serializer simply has no field for them. This matches the best-practice "envelope, not content" rule used by Stripe (events carry IDs and objects only) and GitHub (payload is a self-contained object, never a file).

### 4. SSRF and egress control (answers the SSRF blocker)

Follow the production-evaluator standard that GitHub and Stripe document:

- Scheme allowlist: `https` only by default (plain `http` only when the registrant opts-in explicitly and the host is not private).
- DNS resolution at write time **and** at send time, then block any resolved IP in private/loopback/link-local/reserved/multicast ranges (RFC 1918, 127.0.0.0/8, ::1, fe80::/10, fc00::/7, 169.254.0.0/16 including the cloud metadata address 169.254.169.254, and IPv6 zone IDs). This blocks DNS-rebinding, which is why both checks are required — a host that resolves public on registration can resolve private on retry.
- No auto-redirect (or: redirect budget of zero to a different origin; redirects only within the same host), connect timeout 5 s, response timeout 10 s, request and response body caps (e.g. request ≤ 256 KB, response read capped at 64 KB).
- Private DNS (names ending `.local`, `.internal`, `.intranet`) rejected. These checks run in the dispatcher before any `HttpClient` call, with the result written to the delivery log.

### 5. Secret storage and signature discipline (answers the secret-storage blocker)

Use the crypto pattern already proven in this repo for the update-signing key, and the storage pattern the `ApiToken` feature already follows:

- Encrypt signing secrets at rest with Data Protection (DPAPI on Windows, the `AppData/keys` model from `UpdateSigningService`); never log them, never return them in GET, display once on creation.
- Signature = HMAC-SHA256 keyed by that secret over `{webhook-id}.{webhook-timestamp}.{raw_payload}`; receivers verify with constant-time comparison and reject timestamps > 5 minutes skew (Standard Webhooks tolerance).
- Rotation: creating a new secret invalidates the old one immediately; per-endpoint enable/disable; "signature required" defaults to on for any non-loopback endpoint. A plain-HTTP or unsigned endpoint is possible only behind an explicit per-endpoint flag with a warning, and is disabled by default.

### 6. Reliability schedule and poison handling (answers the reliability/ops blocker)

Take the numbers from production receivers and from the repo's own retention idioms:

- Max 8 attempts, exponential backoff (1 min → 2 min → 5 min → 15 min → 1 h → 6 h, jitter ±20%), hard horizon of 72 h, then dead letter. No retry on 4xx except 408/429. Success = any 2xx; 3xx that the dispatcher will not follow counts as success-follow for logging but does not continue the chain.
- Claim-with-lease: the dispatcher claims rows with `NextAttemptUtc + lease`, per-endpoint concurrency cap of 4, per-org deliveries/sec budget, and a per-event cardinality cap (keyboard hooks are digest-only; heartbeat is never an event source).
- Retention: `Succeeded` / `Dead` deliveries kept 14 days, enforced by `DataCleanupService` pattern with indexed `(Status, NextAttemptUtc)` claim query, batched in pages rather than `int.MaxValue` loads.
- Dead-letter state is visible in the delivery log UI/API; "manual retry" re-enqueues with the same `EventId` (therefore same `Idempotency-Key`) so dedupe still works.

### 7. Observability (answers the 3 AM blocker)

- Atomic per-attempt structured log fields: `EventID`, `SubscriptionID`, `Attempt`, `StatusCode`, `LatencyMs`, `ErrorKind` (timeout, dns-blocked, 4xx...), never the payload or secret.
- Delivery history view per subscription (last N, filter by status) plus a "Test delivery" ping that sends a `ping` event with the same contract, so admins validate a receiver before enabling hot events.
- Metrics: `webhook_deliveries_total`, `webhook_retry_total`, `webhook_queue_depth`, `webhook_queue_lag_seconds` — the lag metric is how the operator knows the pump is behind before users do.

### 8. Authorization and multi-tenancy (answers the cross-org leak)

- Subscriptions are org-scoped and optional device-group-scoped. Enqueue-time evaluation reuses `DataService.DoesUserHaveAccessToDevice` and `FilterUsersByDevicePermission` (the same tools `AgentHub.DeviceCameOnline` uses), so a user cannot subscribe to a device they cannot see.
- Delivery fan-out respects the same scoping; denied deliveries are logged in the metric, never silently dropped. Payload fields are an explicit allowlist per event; `Device` EF entities are never serialized directly (dedicated DTOs).

## How these resolutions close the Goldfish findings

- The zoom-out objection ("second delivery plane") is answered by making the alert path the enqueue path (Resolution 1).
- All six security blockers are answered by Resolutions 2–5 (wire contract, metadata-only payloads, SSRF, secrets).
- The readiness blockers (undefined catalogue, schema, pump, tests) are answered by Resolutions 2, 3, 6, 7: they name the concrete catalog, schema shape (`WebhookEndpoint`, `WebhookDelivery` narrowing to the transactional outbox), pump, and acceptance numbers that the draft must freeze.
- The delivery/ops blockers (claiming, single-instance duplicates, clobbering) are answered by Resolutions 1, 6, and 7 (outbox atomicity, claim-with-lease, per-endpoint concurrency, dead letter, metrics).

## Goals vs. Non-Goals

* **In-Scope Goals:** A durable outbox: enqueue-only in the domain path, pull-based background dispatcher. A small, closed event catalogue with a versioned payload schema and example per event. Org- and device-group-scoped subscriptions, evaluated at enqueue time. Standard Webhooks wire contract (HMAC signature, stable IDs, idempotency key) with metadata-only default payloads. SSRF guard, encrypted secret storage, retry schedule with dead letter, and a delivery/observability surface.
* **Out-of-Scope Non-Goals:** Any change to the desktop/agent client or the SignalR/V2 protocol. An exactly-once guarantee. Sending raw `KeyboardLog.Text` or screenshot/camera bytes inside the webhook body. Supporting Remotely multi-instance deployment (single-process assumption remains). A message broker. A full settings UI designer — a registration list plus a deliveries/test-ping view is the ceiling for v1.

## Alternatives Weighed

- **Reuse `AlertsController` HTTP path as-is, no outbox.** Rejected by author because the alert path has no retry, signature, or attempt log; a delivery that fails is invisible to the operator, and "webhook" on top of it inherits that blind spot. The zoom-out lens still advocates this or a minimal alert-payload template, which remains the cheap baseline.
- **Extend `AlertOptions` with payload templating (`{{DeviceId}}`/`{{Message}}`).** Rejected as a stopgap only: it solves body shaping but not reliability, secrets, or ordering. Kept as the smaller increment if the team prefers alerts over a platform.
- **SMS/email-only or polling via the existing API.** Rejected: email lacks machine-readable delivery semantics and polling misses low-latency hooks; both remain valid channels, not replacements for an event contract.
- **Message broker (RabbitMQ/Kafka).** Rejected: ops burden far beyond a single-server .NET deployment; the transactional outbox already gives durability on existing database providers (Sqlite/SqlServer/PostgreSql).

## Key Decisions & Staging for Next Steps

- **Decision 1 — one delivery plane.** The alert path becomes the enqueue path. Staging: no parallel sender is created.
- **Decision 2 — event catalogue first.** Freeze the closed event list (`v1`) with one payload example per event before any schema or API work; raw keyboard text and file bytes are structurally unreachable.
- **Decision 3 — Standard Webhooks contract.** Headers, signature input, timestamp tolerance, and idempotency key follow the cross-vendor specification exactly; receiver docs link the verification example.
- **Decision 4 — security floor and reliability numbers.** SSRF guard (both DNS checks, HTTPS only), DPAPI-encrypted secrets (one-time reveal, rotation), per-org/endpoint budgets, 8-attempt backoff with dead letter, claim-with-lease, and 14-day delivery retention are locked before the first real webhook.
- **Decision 5 — observability.** Per-delivery attempt log (event, subscription, attempt, status, response code, latency), dead-letter view, manual retry with the same `EventId`, and a "Test delivery" ping before enabling an endpoint.
- **Next step:** `/egm-draft` — it consumes this brief as Priority 1 intake and drafts the full design under `docs/designs/`, pausing after every section for your approval. The draft should carry the five decisions above as constraints, and name `TransactionalOutbox`, `ClaimWithLease`, and the dispatcher worker as the first implementation steps.
