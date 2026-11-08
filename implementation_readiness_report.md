# Stage 9 — Implementation Readiness Test

Target: `docs/designs/webhook-delivery.md` DetailedRange 284..402
Reviewers: egm-implementation-readiness, egm-codebase-details, egm-style

Verdict: Not ready. 2 Blockers (transactional enqueue premise is not in the codebase; `script.execute` producer/fields do not exist), 8 Majors (delete semantics, options, wire transport, claim/lease, DI scoping, producer call order, authz/test coverage, phasing), 1 Nit. The file boundary is exact, but behavior decisions must be fixed before implementation without guessing.

## Blocker

- **Transaction atomicity premise:** Every producer creates its own `DbContext` via `IAppDbFactory.GetContext()` and `SaveChangesAsync()`; there is no caller's transaction to join and no ambient transaction/unit-of-work. "Inserts WebhookDelivery rows in the caller's transaction" requires a refactor not listed in the file plan. `AlertsController` owns no `DbContext` — it delegates to `DataService.AddAlert`, which fans out one `Alert` row per user. Both claims in §4/§14 are impossible as written.
- **`script.execute` producer/fields nonexistent:** `AddScriptRun` is the queueing path, not completion; `ScriptRun` has no `Status`/`StartedAt`/`CompletedAt` and no `ScriptId` (it's `SavedScriptId` `Guid?`). Enqueuing there emits "executed" at dispatch time with fabricated fields. Completion is `ScriptResult`/`AddScriptResultToScriptRun` — mapping still missing.
- **Undefined jargon (style):** "penalized-epoch fallback for AllowHttpOptIn" is undefined — no property, type, default, or fallback semantics. Cannot be built as written; either define it concretely or remove it.
- **Delete semantics undecided:** `DELETE /api/webhooks/{id}` says "disable or hard-delete" without choosing. Schema, list filtering, FK history, and authz tests all branch on it.

## Major

- **Schema not reproduced in plan:** columns delegated to "Technical Plan §2" without type/nullability/lengths/PK-FK/delete behavior in this segment.
- **Wire transport missing:** no header table (name/casing, `Idempotency-Key` construction, `Content-Type`, signature mapping to HMAC canonical string), no `ping` payload shape.
- **Claim/lease unspecified:** lease duration, atomic claim query, lease-token ownership, reclaim rule, clock source, per-endpoint concurrency enforcement, tie-break between 8 attempts and 72 h.
- **DI scoping gap:** scoped `IWebhookPublisher` vs singleton `WebhookDispatcher` — no scope-factory pattern, no named `HttpClient`, no explicit lifetime for `WebhookSigning`.
- **Producer call order undefined:** Tasks 14–20 name methods but not call order relative to `SaveChanges`/commit, failure semantics if `EnqueueAsync` throws, DTO field mapping, or which completion paths in scope.
- **Producer gaps:** no hub method named `DeviceDisconnected` (it's `OnDisconnectedAsync` → `_dataService.DeviceDisconnected`); `DeviceHeartbeat` also writes online status; `EnqueueAsync` being awaitable contradicts "no hub method awaits delivery"; screenshot service has **no** `MaxClipBytes` to preserve (zero-length check only); save paths swallow DB exceptions while still returning the object as success — no well-defined enqueue point.
- **Misdirected modify target:** retention pruning lives in `DataService.CleanupOldRecords` (`DataService.cs:668-705`); `DataCleanupService.RemoveExpiredDbRecords` is a 4-line delegation. Plan lists the wrong file; also `CleanupOldRecords` loads unbounded rather than batched as the plan perversely assumes.
- **`details_url` mechanism gap:** `ApiAuthorizationFilter` accepts only admin-cookie or `ApiKey` header (no query credential); `ExpiringTokenService` tokens are opaque in-memory secrets validated by header. A signed URL requires new scheme + filter + endpoint — none specified.
- **AuthZ principal gap:** `ApiAuthorizationFilter` is admin-or-API-key-only; `DoesUserHaveAccessToDevice` requires a full `RemotelyUser`. Which principal is checked for API-key callers and device-scoped producers is unresolved.
- **DPAPI reuse overstated:** `UpdateSigningService.Protect` returns plaintext on non-Windows; existing pattern protects a file, not a DB `byte[]` — different backup/rotation semantics, plan addresses neither.
- **Test coverage gap:** unit-test bullets omit signature/SSRF cases that the Verification Plan claims; neither covers management-API authz, "secrets never returned"/"shown once", rotation invalidation, retry preserving `EventId`, or `ping` envelope.
- **No phasing:** flat list 1–22; build order (entities/migrations → signing/SSRF → publisher → dispatcher → API → producers → cleanup/tests) with per-phase exit tests is absent.
- **Naming gaps:** no `SubscriptionId` column defined in §2 (index references an undefined name); camera/audio `ClipId` — upload GUID vs persisted clip GUID (two different values) unmapped; `AllowHttpOptIn` does not exist in `ApplicationOptions.cs`.
- **Completeness overclaim (style):** "exact file list / without further design decisions" contradicted inside the section by two open choices; calibrate to "proposed file list".
- **Signature/Do's absence is checked (pass):** Detailed Plan tone is dry and direct; no marketing hyperbole filed.

## Nit

- Bare `AddHttpClient()` at Program.cs:249 has no hardening to reuse; metrics sink for `webhook_*` metrics is never identified — the plan should name the sink or mark it `[Create]`.
- `DateTimeOffset` converters already apply to all entities by reflection (`AppDb.cs:238-276`) — the "apply … per entity" step is redundant and will mislead an implementer, worth one clarifying sentence (SQL Server loop does not run, worth a sentence too).
- "hardened" is a subjective judgment without a measurable definition already satisfied by the stated timeouts/redirect policy — delete the adjective.

## Verified accurate (no finding)

- `AppDb.OnModelCreating`, three EF contexts, migration folders, MSTest+InMemory+Moq stack all exist as cited.
- `AlertsController.Create` inline `SendAsync` + `ApiAuthorizationFilter` + `Ok()` contract — accurate.
- Producer names `SaveScreenshot`/`SaveClip`/`CompleteUpload`/`MaxClipBytes` (Camera/Audio), `SaveLog`, `KeyboardLog.Text` — exist as described.
- `DataService.DoesUserHaveAccessToDevice`, `FilterUsersByDevicePermission`, `AddScriptRun` exist; `DataCleanupService` shape exists.
- `UpdateSigningService` keys in `AppData/keys/` + DPAPI `ProtectedData` exists (file pattern).
- `ExpiringTokenService` and `ApiAuthorizationFilter` exist.
- Verification Plan build/test commands match repo instructions; design-only disclaimer is accurate.
