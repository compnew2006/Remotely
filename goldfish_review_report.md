# Goldfish Annotated Review Report

**Target:** `docs/designs/webhook-delivery.md` (`full file`)

**Status:** resolved

## Resolution Summary

- Applied the proposed fixes for Issues 1–7, 9–22, and 24 (22 suggestions): secret protection and rotation, atomic outbox boundaries, claim/retry behavior, pinned SSRF transport, v1 event scope and payloads, wire contract/examples, authorization, operations, tests, and calibrated language.
- Issue 8 remains **CONTEST**: retained the implementation-plan section required by the drafting workflow, but framed it as a proposed file map rather than a frozen implementation contract.
- Issue 23 remains **DECLINE** per the nit-handling guidance. The separate `Risks & Limits` content was added under Issue 11; cosmetic template/title changes were not made.
- Some proposed wording was resolved with a more precise alternative: retry arithmetic is seven gaps for eight total attempts; metrics avoid endpoint labels; producer authorization is rechecked against current access rather than a persisted permission snapshot.
- Verification: `git diff --check` passed after the final document edits. No feature code, build, or tests were run; the webhook feature remains unimplemented.

## Prioritized Findings & AI Suggestions

### Issue 1 [`egm-design`] - Severity: Blocker
* **Affected Scope:** `webhook-delivery.md` L144, L173, L252 (Secret storage)
* **Discrepancy Description:** Contradictory secret handling: line 144 stores `SigningSecretHash` (bytes; never plaintext in DB) but HMAC signing requires the plaintext secret (line 173), and line 252 requires DPAPI-encrypted reversible storage. A hash cannot be an HMAC key — the spec cannot be implemented as written.
* **Rationale:** Resolve before implementation: choose one model (encrypted reversible with envelope key, not hashed) and document key recovery, persistence across restarts, and what a DB dump leaks.
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** The HMAC-verify direction is a design requirement of the wire contract; the column name `SigningSecretHash` is the contradiction the reviewer cites (readable in the document). Rename the column to `ProtectedSigningSecret` and clarify "DPAPI-/Data-Protection protected bytes".
* ***Proposed Edit:***
    *   Location: Technical Plan §2 (WebhookEndpoint fields), NFR Security, Implementation file 1
    *   Change: Rename `SigningSecretHash` → `ProtectedSigningSecret (byte[], Data-Protection-protected, reversible)`; add a sentence: "The secret is stored only as protected bytes; a hash is never kept. Receiver-side verification uses HMAC over the same secret."

### Issue 2 [`egm-design`] - Severity: Blocker
* **Affected Scope:** `webhook-delivery.md` L154, L163-164, L170 (Claim state machine)
* **Discrepancy Description:** Claim predicate `UPDATE ... WHERE Status='Queued'` never re-selects `RetryPending`; lease-reclaim (expired `Delivering`) has no predicate at all. Recoverable deliveries leak permanently.
* **Rationale:** Specify a full transition table including `Delivering→Delivering` reclaim and `RetryPending→Delivering` re-claim with exact WHERE clauses, and prove atomicity across Sqlite/SqlServer/Postgres without `SELECT FOR UPDATE`.
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** Reviewer is right that the stated claim only covers `Queued`. Add the missing predicates to Section 2 text.
* ***Proposed Edit:***
    *   Location: Technical Plan §2
    *   Change: Append after the claim paragraph: "Re-claims cover both `Queued` and `RetryPending` where `NextAttemptUtc <= now`, and `Delivering` where `LeaseUntil <= now`; both set `Status='Delivering'`, `LeaseUntil=now+lease-minutes`, `ClaimedBy=workerId`."

### Issue 3 [`egm-design`] - Severity: Blocker
* **Affected Scope:** `webhook-delivery.md` L172, L174 (SSRF guard + `IHttpClientFactory` POST)
* **Discrepancy Description:** TOCTOU — the guard resolves DNS, then the POST resolves again; `SocketsHttpHandler` also caches DNS across the 2 s tick. Attacker rebinding wins despite send-time validation.
* **Rationale:** Require IP-pinned connection (validate IP, connect to IP with TLS SNI/cert verification against hostname, disable pooled connection reuse for webhook client) or acknowledge the residual risk explicitly.
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** The guard claim is true but incomplete as designed. The fix that matches the design's existing intent (no redirects, validated host) is IP-pinned binding via `SocketsHttpHandler.ConnectCallback` with SNI host header and cert verification against the original hostname, plus `PooledConnectionLifetime` set to a short value.
* ***Proposed Edit:***
    *   Location: Technical Plan §3, step 2; Implementation Plan item 6
    *   Change: Add: "The validated IP is pinned: the dispatcher uses `SocketsHttpHandler.ConnectCallback` to connect to the validated IP, sends TLS SNI and certificate verification against the original hostname, and disables connection reuse across deliveries (`PooledConnectionLifetime` ≤ attempt"). Add pinning verification to `WebhookSsrfGuard` tests."

### Issue 4 [`egm-design`] - Severity: Blocker
* **Affected Scope:** `webhook-delivery.md` L63, L67, L146, L172, L251-253 (Loopback/HTTP/signing triangle)
* **Discrepancy Description:** loopback is simultaneously (a) disabled by default per CUJ 2, (b) blocked as private range, (c) exempt from signing ("default true for non-loopback"), and (d) not required — an unsigned plaintext opt-in still possible. Private-host definition flips between RFC1918 and suffix list.
* **Rationale:** Freeze one policy: which destinations may ever be plain HTTP/unsigned, and remove signing exemptions that counter SSRF hardening.
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** Incoherence is visible in the document itself. Freezing the policy is a reviewer-visible text correction with system consequences the user should still approve.
* ***Proposed Edit:***
    *   Location: CUJ 2 When; NFR Security bullet 1; Technical Plan §5
    *   Change: Replace the loopback exemption with a uniform rule: "Signing is required for every endpoint regardless of destination. Plain HTTP is allowed only when the registrant opts in explicitly per endpoint, and never to a private/loopback/link-local address. Private-host means: private ranges, metadata, reserved, multicast, and suffixes `.local`/`.internal`/`.intranet` — any one blocks."

### Issue 5 [`egm-design`/`egm-codebase-details`] - Severity: Blocker
* **Affected Scope:** `webhook-delivery.md` L20-22, L134-135, L254 (Data-leak absolutism + DTO unreachable claim)
* **Discrepancy Description:** "never become a data leak", "structurally unreachable", "no code path that can serialize `KeyboardLog.Text`" — the document asserts a dischargeable negative without tests; DTO allowlists are per-file text claims.
* **Rationale:** Calibrate to a stated DTO allowlist and required field whitelist, plus the payload-negative tests already promised in the Verification Plan.
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** The negative is unverifiable as written; the document already plans payload-negative tests, so aligning the claim with the test mechanism is a low-cost calibration.
* ***Proposed Edit:***
    *   Location: Overview constraint 2; Technical Plan §1; NFR Security bullet 4
    *   Change: Replace "structurally unreachable" with "kept out by DTO allowlists and verified by payload-negative tests (no `Text`/bytes field exists on any webhook DTO)"; replace "never become a data leak" with "payloads are metadata-only by construction and subject to payload-negative tests".

### Issue 6 [`egm-style`] - Severity: Blocker
* **Affected Scope:** `webhook-delivery.md` §4 L197 (Wire Contract examples)
* **Discrepancy Description:** Claims "one complete example per event in the draft", but no per-event JSON bodies are present.
* **Rationale:** Supply examples or drop the claim.
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** Straightforward verifiability failure; adding one example body per event (7 small JSON blocks) makes the contract implementable.
* ***Proposed Edit:***
    *   Location: Technical Plan §4, after the per-event data table
    *   Change: Append one minimal JSON example per event (`device.connected`, `device.disconnected`, `script.execute`, `screenshot.available`, `camera.clip.available`, `audio.clip.available`, `keyboard.log.available`) per the envelope + data fields in the table.

### Issue 7 [`egm-zoom-out`/`egm-comprehension`] - Severity: Blocker
* **Affected Scope:** Event catalogue scope (7 events in v1; Q2 unresolved at L54-55)
* **Discrepancy Description:** YAGNI: six of seven producers are high-sensitivity capture hooks with no named day-1 consumer; the document itself notes in Future Improvements that digest events may be wanted. Q2 already proposes shipping `device.connected` + `script.execute` only.
* **Rationale:** Ship 2 events in v1; list five as future events.
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** This is a scope reduction, not a wording tweak; the user must confirm it at the gate. If accepted, Q2 resolves to "v1 = 2 events" and the remaining 5 become explicit future events in the catalogue.
* ***Proposed Edit:***
    *   Location: Goals §1; Open Questions Q2; Technical Plan §1 producer table and §4 data table; Non-Goals
    *   Change: Mark the 5 capture events as "future events (not v1)" in §1/§4 tables; Goals §1 becomes "Two listed events."; close Q2 with "v1 ships `device.connected` + `script.execute`; remaining five follow when a receiver commits."

### Issue 8 [`egm-zoom-out`/`egm-design`] - Severity: Blocker
* **Affected Scope:** Detailed Implementation Plan (L284–398)
* **Discrepancy Description:** A task list, not a design: exact file paths, method signatures, DI lines, EF migration commands, test class names, metric names hardcoded in five places; freezes Q1/Q2 answers before they are made; altitude drop.
* **Rationale:** Cut §Detailed Implementation Plan down to component boundaries, invariants, and reuse points; move signatures and file checklist to the tasks stage.
* ***AI Suggestion:*** CONTEST
* ***Justification:*** The plan's existence is the format the `egm-draft` skill mandates ("must enumerate every file touched … NO CODE"). Removing it would violate the drafting contract — but the findings داخل The Zoom Out standard are right that the detail level must be "component boundaries, invariants, and reuse points" rather than signatures. CONTEST keeps the section; the required repair is the wording calibration in Issue 9.
* ***Proposed Edit:***
    *   Location: §Detailed Implementation Plan header
    *   Change: Add a framing line at the top: "This section is a proposed file list for the implementation phase; signatures, constants (batch size, timeouts, caps, metrics) are tunable implementation, not design decisions; no item is a frozen commitment until Q1/Q2 are resolved in the Implementation Readiness phase."

### Issue 9 [`egm-style`] - Severity: Major
* **Affected Scope:** `webhook-delivery.md` L286 (Completeness overclaim)
* **Discrepancy Description:** "exact file list" / "without further design decisions" overstates completeness and is contradicted inside the same section by two open choices (L328, L389: "disable or hard-delete", "penalized-epoch fallback").
* **Rationale:** Calibrate to "proposed file list" unless the ambiguities are resolved.
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** Calibration is negligible cost and required for the user's own honesty tests downstream (Stage 6 comprehension).
* ***Proposed Edit:***
    *   Location: §Detailed Implementation Plan header (L286)
    *   Change: Replace "This is the exact file list." with "This is the proposed file list for the implementation phase; two implementation choices remain open and are flagged under Items 8 and 22 (endpoint delete behavior, HTTP opt-in fallback)."

### Issue 10 [`egm-design`] - Severity: Major
* **Affected Scope:** `webhook-delivery.md` L36, L74, L176, L262 (Retry arithmetic)
* **Discrepancy Description:** Delays sum to 443 min against promised "8 attempts over 72 hours"; two attempts and ~64 h unaccounted; "well under 1 minute" steady-state lag unbacked.
* **Rationale:** Add missing steps or revise the 72 h claim; give QPS/storage/lag reasoning.
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** The numbers are the contract — they must be arithmetically consistent and consistent across sections (Issue from Stage 6 as well).
* ***Proposed Edit:***
    *   Location: Goals §1; CUJ 3; Technical Plan §3; NFR Performance; NFR Reliability
    *   Change: Replace the backoff list with a schedule that sums to the horizon: "1 min → 2 min → 5 min → 15 min → 1 h → 6 h → 24 h → 32 h (7 retry gaps, 8 total attempts, cumulative ≈ 63 h < 72 h horizon)." Remove "1 m → 2 m → 5 m → 15 m → 1 h → 6 h (±20% jitter)" in each location; drop "well under 1 minute" lag claim and replace with "steady-state lag is bounded by poll tick (2 s) + claim batch processing; no real-time SLA is promised."

### Issue 11 [`egm-design`/`egm-structural`] - Severity: Major
* **Affected Scope:** Whole document vs Standard §Risks & Mitigations
* **Discrepancy Description:** No dedicated Risks & Limits section; failure modes (dispatcher crash + lease expiry, DB full/oversized `PayloadJson`, leaked `details_url`, DPAPI key loss, secret rotation breaking in-flight verification, malicious slow receiver, metric cardinality, observability gaps) are scattered in NFR prose.
* **Rationale:** The feature-spec standard requires enumerated risks + mitigations regardless of scattered coverage.
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** Isolated text addition that also closes the Stage 6/8 transparency critiques; the content exists in scattered form in the document already.
* ***Proposed Edit:***
    *   Location: New section after `## Non-Functional Requirements`
    *   Change: Insert "## Risks & Limits" enumerating at minimum: dispatcher crash + lease expiry; DB full / oversized payload; leaked `details_url`; DPAPI key loss (Linux non-DPAPI fallback); secret rotation breaking in-flight verification; malicious slow receiver; per-`SubscriptionId` metric cardinality; and 3 AM observability gaps — each with its mitigation as already stated in the NFR.

### Issue 12 [`egm-design`] - Severity: Major
* **Affected Scope:** `webhook-delivery.md` L54, L218 (AlertsController transition / Q1)
* **Discrepancy Description:** Breaking change decided in §6 but left open as Open Question Q1; no migration, dual-write, deprecation window, or compatibility behavior for existing callers sending arbitrary method/URL/headers/body.
* **Rationale:** Decide retire vs. transition period and document compatibility story — blocks handoff as-is.
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** This is the correct reading of Q1: it is a migration decision the architecture depends on. The simplest settlement (consistent with Zoom Out's simplest rollout): webhooks additive, alerts untouched during a dual-run period, with removal only after a stated release boundary.
* ***Proposed Edit:***
    *   Location: Open Questions Q1; Technical Plan §6; Non-Goals
    *   Change: Document the compatibility story: "AlertsController keeps its current inline send; webhooks are additive and register separately. After consumers migrate (documented release boundary), the alert HTTP branch is removed and alerts keep their persisted rows + email path. No caller breaks at any point."

### Issue 13 [`egm-inventive-catalyst`/`egm-design`] - Severity: Major
* **Affected Scope:** `webhook-delivery.md` L39, L203-205, L212, L303 (details_url)
* **Discrepancy Description:** `details_url` is a bearer URL stored 14 days in `PayloadJson` — byte exfiltration by reference, surrendering the metadata-only invariant at the payload boundary; no host, construction, expiry, or revocation is defined.
* **Rationale:** Extract `details_url` out of the webhook body: carry resource IDs only; receivers fetch details through the authenticated API with a short-lived token at fetch time. "Metadata-only" is then true in the payload itself.
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** A signed URL stored in the payload-to-be-logged-for-14-days is an exfiltration primitive regardless of the auth story; IDs-only + authenticated fetch matches how Stripe/GitHub patterns re-invent the guarantee. The Stage 8 zoom-out had this as a Blocker for the same reason; the fix also removes the `ExpiringTokenService` mismatch entirely.
* ***Proposed Edit:***
    *   Location: Goals §4; Technical Plan §4 (payload table + text); Non-Goals; "Additional Resources"
    *   Change: Remove `details_url` from all payload rows; replace with: "The payload carries only the resource ID; the receiver fetches details through the authenticated API (`ApiAuthorizationFilter` path), which issues a short-lived token at fetch time. No URL is stored in the payload."

### Issue 14 [`egm-zoom-out`/`egm-inventive-catalyst`] - Severity: Major
* **Affected Scope:** `webhook-delivery.md` L136, L257 (Fan-out inside producer transaction)
* **Discrepancy Description:** "One row insert, control path unaffected" vs. per-subscription fan-out + authz filter in the event's own transaction — N inserts + authorization queries on every hot trigger; a webhook authz failure can roll back a device save.
* **Rationale:** Producers enqueue one raw event row; the dispatcher performs subscription fan-out and authorization at delivery time, evaluated against a persisted permission snapshot.
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** The document's own Goal 2 already promises the control path unaffected — a coupling-free hot path is the requirement; the fan-out location conflicts with it, so the design must move the work.
* ***Proposed Edit:***
    *   Location: Technical Plan §1; Goals §2
    *   Change: "Producers enqueue one raw `WebhookEvent` row in the transaction. Subscription fan-out and authorization filtering happen at delivery time in the dispatcher, evaluated against a permission snapshot persisted with the event (OrganizationId, DeviceGroupId?, occurred-at)."

### Issue 15 [`egm-inventive-catalyst`] - Severity: Major
* **Affected Scope:** `webhook-delivery.md` §3 (Concurrency cap 4 cannot cap)
* **Discrepancy Description:** A serial loop claimed no head-of-line blocking: claim 50, then per delivery SSRF+DNS+sign+POST at 10 s each — one slow endpoint's batch occupies the tick ~500 s.
* **Rationale:** Segment execution into per-endpoint lanes with per-endpoint semaphore (4 in-flight) and a global ceiling; seconds-scale DNS TTL inside a lane.
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** The cap's promised isolation is not deliverable by a serial loop; either make execution per-endpoint parallel or retract the head-of-line-blocking claim.
* ***Proposed Edit:***
    *   Location: Technical Plan §3; NFR Performance
    *   Change: "Dispatcher claims a batch, then dispatches per-endpoint in parallel lanes (per-endpoint semaphore 4, global ceiling e.g. 32 in-flight). One slow receiver stalls only its own lane." Update the batch processing description accordingly.

### Issue 16 [`egm-design`/`egm-inventive-catalyst`] - Severity: Major
* **Affected Scope:** `webhook-delivery.md` L150, L187-194, L208, L261 (EventId identity / dedupe)
* **Discrepancy Description:** Per-subscription rows mean N different `webhook-id`/`Idempotency-Key` values for one logical event — receivers cannot dedupe across subscriptions; 24 h dedupe < 72 h retry horizon.
* **Rationale:** Keep one root `EventId` plus per-delivery attempt IDs; window must be ≥ retry horizon (96 h) or state replay window explicitly.
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** The dedup contract as written is undeliverable by the receiver; this is scope/identity arithmetic, not style.
* ***Proposed Edit:***
    *   Location: Technical Plan §2 entities; §4 wire contract; Goals §3
    *   Change: Split identity: `EventId` (one per logical event, stable) on the envelope and `Idempotency-Key`; `webhook-id` = per-delivery + attempt ID. Dedupe window: 96 h (full 72 h retry horizon + 24 h manual-replay grace). Update the entity sketch and contract accordingly.

### Issue 17 [`egm-design`/`egm-structural`] - Severity: Major
* **Affected Scope:** `webhook-delivery.md` L252, L265 (DPAPI portability vs Linux Docker)
* **Discrepancy Description:** Key durability story is Windows-centric; DPAPI key-ring loss on Linux Docker invalidates every stored secret and all in-flight HMAC verification. Rotation via immediate invalidation breaks queued deliveries.
* **Rationale:** Specify portable key-ring persistence (volume/backup/restore), grace-period dual-verify, and fail-closed behavior when keys are absent.
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** `UpdateSigningService`'s own comment acknowledges the non-Windows plaintext fallback; the webhook design cannot silently inherit that gap for production secrets.
* ***Proposed Edit:***
    *   Location: NFR Security bullet 2; §6 future concerns
    *   Change: Add: "On non-Windows, Data Protection keys are persisted on a named volume; a lost key ring invalidates every stored webhook secret (one-time reveal cannot be reconstructed). Key backup/restore is part of the rollout checklist, and dispatcher fails closed when keys are absent (no unsigned sends). Rotation keeps the previous secret valid for one retry horizon (72 h) to verify in-flight deliveries."

### Issue 18 [`egm-design`] - Severity: Major
* **Affected Scope:** `webhook-delivery.md` L87, L121-136 (Producer transaction boundary)
* **Discrepancy Description:** Hand-waved atomicity: file-write-then-DB flows, per-producer ordering, atomicity limits, and `IAppDbFactory`/`IWebhookPublisher` lifetime/failure semantics are unspecified.
* **Rationale:** Document per-producer write ordering and atomicity limits.
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** The codebase-details reviewer proved producers own their contexts (`IAppDbFactory.GetContext()` per method) and that "in the caller's transaction" has no referent at most producer sites. The design can state one rule and its exception table in a few lines.
* ***Proposed Edit:***
    *   Location: Technical Plan §1 (after enqueue rules)
    *   Change: Add: "The enqueue happens on the producer's context where the producer already holds one (DataService and the same-transaction saves); where the producer does not hold one (capture services), the publisher opens its own short-lived context after the producer's commit and enqueues immediately (at-least-once on the event itself is preserved because the domain write committed first — a crash between commit and enqueue is a known, small loss window, stated here, not hidden)."

### Issue 19 [`egm-design`/`egm-codebase-details`] - Severity: Major
* **Affected Scope:** `webhook-delivery.md` L201, L206 (PublicIP, WindowTitle, ProcessName sensitivity)
* **Discrepancy Description:** "Metadata-only = not sensitive" asserted, not justified: PublicIP is personal data in many jurisdictions; window titles/process names during keyboard-log events can reveal sensitive contexts (banking, medical). No legal basis or per-event rationale.
* **Rationale:** Remove `PublicIP` from v1 payloads or document the legal basis and per-event field rationale; count/digest filtering alone is not sufficient privacy reasoning.
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** The privacy promise in Goal 4 collapses if the payload still carries personal data without justification. Removing `PublicIP` is the cheapest reconciliation; window/process names stay only because the keyboard event now requires a named receiver justification before it can ship at all (coupled to Issue 7).
* ***Proposed Edit:***
    *   Location: Technical Plan §4 payload table; Goals §4
    *   Change: Delete `PublicIP` from the `device.connected`/`device.disconnected` `data` fields; add a per-event privacy note: "No personal data is shipped in v1 payloads. Window/process names ship only for events with a named receiver justification (see Issue 7); keep field-level rationale in the per-event table."

### Issue 20 [`egm-comprehension`/`egm-implementation-readiness`] - Severity: Major
* **Affected Scope:** §1 producer sites, Technical Plan §2 (schema), §4 (wire transport), claim/lease, DI, test coverage, phasing, naming (subscription vs endpoint, SubscriptionId column, `AllowHttpOptIn`)
* **Discrepancy Description:** The draft delegates columns to "Technical Plan §2" without reproducing type/nullability/lengths/PK-FK/delete behavior inline; wire transport (header table, `Idempotency-Key` construction, `Content-Type`, signature header, `ping` body) is unspecified; claim/lease occupancy details (lease duration, atomic claim query, clock source, tie-break) absent; no phasing/exit tests; `SubscriptionId` references a column the entity spec never introduces; `AllowHttpOptIn` does not exist in `ApplicationOptions.cs`; serializer claim about KeyboardLog.Text reachability unverifiable without tests cited.
* **Rationale:** An unfamiliar implementer must not have to guess: inline the schema, add the wire-contract table, specify claim/lease constants and tie-breaks, state DI scope-factory pattern and named HttpClient lifetime, add 4–6 phases with entry files/build command/exit test, fix the FK name, define or drop `AllowHttpOptIn`, and cite the payload-negative tests' file names inline.
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** These are the "build without further decisions" constraints of the implementation-readiness standard; the draft's own claimed evidence boundary (line ~398) commits it to them.
* ***Proposed Edit:***
    *   Location: Technical Plan §2 (entity sketch), §3 (dispatcher), §4 (wire contract); Detailed Implementation Plan items 1-2, 4-6, 8-11, 13, 22
    *   Change: Inline each entity's columns with types/nullability/lengths/PK-FK; add a wire-contract table (header → value source, timestamp units, signature header, idempotency derivation, `ping` body); specify lease duration (10 min), claim query with tie-break (attempts cap before horizon), dispatcher DI scope-factory pattern and named HttpClient lifetime; add a 5-phase build order with exit tests; unify naming (`WebhookEndpointId` everywhere); define `AllowHttpOptIn` as a non-secret `ApplicationOptions` boolean (default false, per-endpoint override requires explicit flag) or remove it; cite `WebhookTests.cs`/`WebhookDispatcherTests.cs` by name in the producer/Dynamic sections.

### Issue 21 [`egm-comprehension`] - Severity: Major
* **Affected Scope:** Throughout (definitions churning under reader use)
* **Discrepancy Description:** Organization/DeviceGroup hierarchy unexplained; "endpoint" vs "subscription" vs `WebhookEndpoint` used interchangeably while deliveries are keyed by `SubscriptionId`; `details_url`, PublicIP allowlist, `WindowDigest` named but not defined; existing-system pattern reuse claimed without definitions. A fresh reader cannot verify the authorization model, dedupe semantics, or re-use claims.
* **Rationale:** Define the named entities and their relationships, link the Standard Webhooks spec, define each risky payload field, and cite existing-pattern files with symbols.
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** Comprehension gaps are doc defects by rule ("Do not contest a comprehension gap"). The fixes are localized prose additions.
* ***Proposed Edit:***
    *   Location: Overview (new paragraph), Technical Plan §1, §5; Glossary-equivalent "Key definitions" box
    *   Change: Insert a "Key definitions" box after the Overview: entity glossary (`Organization` = tenant unit, `DeviceGroup` = sub-slice of devices by membership, `WebhookEndpoint` = one registered URL + scope + events, `Subscription` = the endpoint's registration record — 1:1 with `WebhookEndpoint`, deliveries reference `WebhookEndpointId`). Link the Standard Webhooks specification URL. Define each payload-only field used by the privacy/payload claims (`details_url` removed per Issue 13, `WindowDigest` = format `window_start + count`, `PublicIP` removed per Issue 19). Cite `ScriptScheduler`, `DataCleanupService`, `UpdateSigningService`, `DataService` by file symbol on first mention.

### Issue 22 [`egm-design`/`egm-style`] - Severity: Major
* **Affected Scope:** `webhook-delivery.md` L170, L178, L257-258 (dispatcher throughput / metric definitions)
* **Discrepancy Description:** Sequential vs parallel fan-out unstated; named metric `webhook_queue_lag_seconds` undefined (aggregation, alert threshold); per-endpoint isolation not mechanically deliverable as written.
* **Rationale:** Define the worker threading model, per-endpoint isolation, and the exact metric/log schema the runbook consumes.
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** Issue 15 covers the execution model; this issue covers naming the metrics/log contract. Fields already exist in the attempt-log sentence; they need names the dispatcher tests can assert against.
* ***Proposed Edit:***
    *   Location: Technical Plan §3; NFR Performance and Observability
    *   Change: "Metrics are per-endpoint aggregates: `webhook_deliveries_total{endpoint}`, `webhook_queue_lag_seconds` (p50/p95 of `now − CreatedUtc` for queued rows, computed per tick, alert at > 60 s), `webhook_retry_total{endpoint}`; attempt log fields: `EventID`, `SubscriptionID`, `Attempt`, `StatusCode`, `LatencyMs`, `ErrorKind`."

### Issue 23 [`egm-design`/`egm-structural`] - Severity: Nit
* **Affected Scope:** Structure/template compliance
* **Discrepancy Description:** Missing mandatory `Risks & Mitigations` section (see Issue 11); `Additional Resources`/`Future Improvements` mis-nested as H3 under Non-Functional Requirements; `The Problem`/`Technical Plan (Architecture)` titles deviate from standard verbatim labels; `Window`Digest` formatting glitch.
* **Rationale:** Template compliance for a rigid validator.
* ***AI Suggestion:*** DECLINE
* ***Justification:*** Templates quirks are a validator concern; the user's content pipeline treats these as cosmetic labels. Issue 11 (Risks) is already PROPOSE FIX'd as a content addition.
* ***Proposed Edit:***
    *   Location: N/A
    *   Change: Recorded without edits per egm-resolve guidance (Nits are DECLINE by default).

### Issue 24 [`egm-style`/`egm-design`] - Severity: Nit
* **Affected Scope:** Uncalibrated absolutes, subjective goal phrasing, additive boilerplate ("goldfish reviews found six blockers", "structurally unreachable", "safe brackets")
* **Discrepancy Description:** Marketing/absolute phrasing in normative sections; "exponential backoff" used for a non-exponential sequence; "precisely why"; "REST (or Blazor admin)" alternative left open.
* **Rationale:** Calibrate normative language to numbers and mechanisms already in the document; resolve the surface alternative.
* ***AI Suggestion:*** PROPOSE FIX
* ***Justification:*** Issue 10 already fixes the schedule naming ("scheduled backoff with stated intervals"); this issue sweeps the remaining absolutes in one vocabulary pass. The API contract pin ("REST via Blazor admin is out; controller API only in v1") is a real ambiguity a fresh engineer would trip on (also an Issue 20 item).
* ***Proposed Edit:***
    *   Location: Overview, Goals, NFR, Alternatives Considered, Problem, Technical Plan §5
    *   Change: Replace absolutes with mechanism words already in the design ("structurally unreachable" → "no code path… verified by payload-negative tests"); replace "REST (or Blazor admin)" with "REST (controller API only in v1; Blazor admin view is future work)"; remove filler adverbs. Values unchanged.
