# Stage 5 — Initial Zoom Out Review

Target: `docs/designs/webhook-delivery.md` (full file)
Reviewers: `egm-zoom-out`, `egm-inventive-catalyst`

Verdict: Form correctly framed, context sufficient, but v1 as drafted fails Simplicity and Altitude Control. Ship a shrunken outbox with 2 events, 1 service, stock Standard Webhooks headers, or do not ship.

## Blocker

- **B1 — Scope (Goals §, Event Producers, Wire Contract, Implementation files):** Seven production events in v1 is YAGNI. Only 2 have a day-1 external consumer; keyboard/screenshot burst hooks carry cardinality and ops cost the design itself admits, and the draft's future-improvements section proposes digest events to undo them. v1 should ship `device.connected` + `script.execute` only; Q2 already concedes this — close it before implementation.

## Major

- **M1 — Detailed Implementation Plan is a task list, not a design:** File paths, method signatures (`EnqueueAsync`, `ValidateAsync`, `CreateSecret/Verify`), DI lines, batch size 50, tick 2 s, 5 s/10 s timeouts, 256 KB/64 KB caps frozen in 5 places. Design stops at outbox + dispatcher + contract + authz.
- **M2 — Premature scale-out optimization:** Claim-with-lease + `ClaimedBy` + lease-reclaim + per-endpoint cap 4 + per-org budgets + 8 attempts/72 h + jitter + 3 metrics pays for multi-instance safety while Non-Goal 4 explicitly disclaims multi-instance. Single-process `SemaphoreSlim`-guarded pull suffices; lease logic when a second instance exists.
- **M3 — Wire Contract header duplication:** `webhook-id`, `X-Remotely-Delivery`, `Idempotency-Key` all carry the same value plus `X-Remotely-Event`. Adopt spec headers only; dedupe is one key, not three.
- **M4 — Flag proliferation before demand:** `EventsScope`, `RequireSigning`, `AllowHttpOptIn`, `LastValidationKind`, `MaxAttempts`, `PerEndpointConcurrency`, `DeliveryRetentionDays`. v1 needs org-scoped endpoint, always signed, https-only, fixed retries/retention, one `WebhookDeliveryService` — not four public types + risk features.
- **M5 — False compromise (producer cost):** Design claims producers cost "one row insert", but per-org fan-out + authz filtering runs synchronously in the event's own transaction. At hot frequencies that re-couples webhook load to latency / SQLite writer contention. Proposed: write one immutable event row in-domain, defer fan-out to dispatcher.
- **M6 — TOCTOU in SSRF guard:** "Validate DNS then POST" re-resolves DNS at connect time — rebinding gap remains. `AutoRedirectHandler` + guard misses the resolution the socket actually uses. Pin the validated IP via `SocketsHttpHandler.ConnectCallback` and classify guard failures (`DnsBlocked`, `PrivateRange`, `SchemeBlocked`) as non-retryable `Dead`.
- **M7 — Outbox flooding:** Per-burst keyboard events accepted despite "high-frequency" concession; `Count`/Digest reduces payload, not enqueue volume. Propose coalescing per-device (digest event 1–5 min) + per-organization budget/backpressure + opt-in bursts.

## Nit

- **N1 — Invocation naming drift:** Prose cites `FilterUsersByDevicePermission` where the normative `Server/Services/README.md` records `FilterDeviceIdsByUserPermission`; cleanup flows through `DataService.CleanupOldRecords`, not a direct `RemoveExpiredDbRecords`. Freezing names in the plan now repeats these drifts.
- **N2 — Scale-out claim contradicted:** Claim-with-lease asserted "safe if it ever scales out" while multi-instance is an explicit Non-Goal; per-endpoint cap relies on single-process `SemaphoreSlim`. Make concurrency DB-state-driven or mark scale-out as an unsupported invariant.
- **N3 — Backoff table arithmetic:** 8 attempts vs 6 interval steps + conflicting 3xx rules ("success-follow" vs no-redirect). Freeze the table so attempt count, intervals, and horizon sum agree; define 3xx explicitly and test it.

## Passes kept

- Transactional outbox pull, not inline `await PostAsync` in `AgentHub`/controllers.
- Single delivery plane — alert path becomes enqueue, not a parallel system.
- Metadata-only DTOs — `KeyboardLog.Text` / media bytes structurally unreachable.
- SSRF guard + HMAC + DPAPI secrets as a security floor, not over-engineering.

## Goldfish context

Reviewed with `concepts_brief.md` and `Server/Services/README.md` as the baseline reading list.
