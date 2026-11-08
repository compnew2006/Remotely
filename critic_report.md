# Stage 7 — Critic Review

Target: `docs/designs/webhook-delivery.md` FeatureRange 1..283
Reviewers: egm-design, egm-structural, egm-style

## Blocker

- **B1 — Secret storage model contradictory:** `SigningSecretHash` cannot be an HMAC key; line 252 requires DPAPI-encrypted reversible secret. Choose one model (encrypted reversible with envelope key).
- **B2 — Claim predicate incomplete:** claim `UPDATE ... WHERE Status='Queued'` never re-selects `RetryPending`, and reclaim of expired `Delivering` has no predicate. Rows leak permanently. Specify full transition table.
- **B3 — TOCTOU / DNS cache bypass:** guard does DNS then `HttpClient` re-resolves; SocketsHttpHandler caches records across ticks. Require IP-pinned connection with SNI/cert verification, or acknowledge residual risk.
- **B4 — Loopback/HTTP/signing incoherence:** loopback both blocked and signing-exempt simultaneously; private-host definition flips between RFC1918 list and suffix list. Freeze one policy.
- **B5 — Data-leak absolutism (style):** "never become a data leak," "structurally unreachable," "no code path that can serialize `KeyboardLog.Text`" — design intent cannot verify a negative code-path claim. Calibrate to a stated DTO allowlist and required field whitelist.
- **B6 — Per-event example claim not backed:** §4 claims "one complete example per event in the draft", but no JSON bodies follow. Supply examples or drop the claim.

## Major

- **M1 — Reliability arithmetic doesn't sum to SLA:** delays sum to 443 min (~7.4 h) against promised 8 attempts / 72 h. Add missing steps or revise the claim; unbacked latency assertions ("well under 1 minute") need load justification.
- **M2 — No Risks & Limits section:** standard requires enumerated risks + mitigations; draft scatters them across NFR prose. Add the section regardless of scattered mitigations.
- **M3 — AlertsController breaking change deferred as Q1 while §6 decides it:** no migration, dual-write, deprecation, or compatibility behavior. Decide retire vs transition period.
- **M4 — `details_url` undefined and load-bearing:** no host, auth mapping, lifetime, revocation, or audit logging — a leaked signed URL is itself an exfiltration primitive. Define it or remove it from v1.
- **M5 — No cost/quota model:** fan-out rows × 256 KB × 7 events × fleet × 14-day retention unbounded; no size/latency budget for SQLite writer pressure.
- **M6 — EventId identity model contradicts dedup promise:** per-subscription rows mean N `webhook-id` values per logical event; 24 h dedupe < 72 h retry horizon. Keep one root EventId + per-delivery attempt IDs; window must be ≥ horizon.
- **M7 — Key durability is Windows-centric:** DPAPI key ring loss on Linux Docker invalidates all secrets and in-flight verification; immediate rotation invalidation breaks queued deliveries. Specify portable key-ring persistence + grace-period dual-verify.
- **M8 — Throughput model underspecified:** 2 s tick × batch 50 × 10 s timeout sequential fan-out = 500 s batch stall against the "no head-of-line blocking" claim; metric `webhook_queue_lag_seconds` undefined without aggregation/threshold.
- **M9 — "Metadata-only = not sensitive" asserted, not justified:** PublicIP is personal data; window titles/process names can reveal sensitive contexts. No legal basis or per-event rationale.
- **M10 — Producer transaction boundary hand-waved:** file-write-then-DB flows, per-producer ordering, atomicity limits, and `IAppDbFactory`/`IWebhookPublisher` lifetime/failure semantics unspecified.
- **M11 — Structural: Risks & Mitigations absent** as a header (mandatory blueprint section).
- **M12 — Style: uncalibrated absolutes and jargon:** "only topology the seven Goldfish lenses agree on", "degrades only its own stream", "prevents head-of-line blocking", "well under" — replace with the mechanisms and numbers already in the document.

## Nit

- **N1 — Draft hygiene:** "five (7 events)" contradiction, CUJ 2 garbled ".local HTTPS disabled", `Dead` vs `DeadLetter` terminology, narrowing SSRF encodings (decimal/hex IPs, trailing dots, 100.64/10), `Window`Digest` formatting glitch.
- **N2 — API surface left as "REST (or Blazor admin)"** and test-ping execution model (synchronous vs enqueued) unpinned.
- **N3 — Structure titles deviate from standard verbatim labels** (`The Problem`, `Technical Plan (Architecture)`), Additional Resources / Future Improvements nested as H3s under Non-Functional Requirements.
