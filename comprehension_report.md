# Stage 6 — Comprehension Test

**What this proposes**

This design adds webhook delivery as a passive, background side-channel alongside the existing remote-control path. When a catalogued event occurs, the server records the event and a queued delivery row together in one database transaction, and a background dispatcher later signs and POSTs a metadata-only payload to registered endpoints. Deliveries are retried with backoff up to a bounded horizon, each attempt is logged, and exhausted deliveries become visible dead letters that can be manually queued with the same event identifier. The existing inline alert HTTP send is retired so alert and webhook share one enqueue path, and every send is SSRF-guarded and signed.

**How the existing system works, per this document**

Remotely is described as a cloned internet-facing ASP.NET Core / Blazor / SignalR remote-control server that the author did not write, used for interactive device control, script runs, and captured screenshots, camera clips, audio clips, and keyboard logs. Today external systems cannot be informed when an event occurs, except for the one fire-and-forget HTTP request in `AlertsController.Create`, which has no retry, signature, or delivery log. Operators therefore cannot tell whether a notification was delivered or never sent, and must poll or capture artifacts manually. Sensitive material — typed text, script output, captured media — already exists on the server.

## Findings

**Severity:** Major
**Affected Scope:** Organization / DeviceGroup / EventsScope (Technical Plan §1, §2, §5; CUJ 1-2)
**Discrepancy Description & Rationale:** The tenant/authorization model the whole fan-out depends on is never defined. Naive reader does not know what an Organization is, what a DeviceGroup is, how they relate, or what `org | device_group` scoping means in practice. Claims that fan-out is "filtered through `DataService.DoesUserHaveAccessToDevice`" assume unstated hierarchy and permission semantics, so a fresh reader cannot verify that a subscription cannot receive devices it should not see.

**Severity:** Major
**Affected Scope:** Subscription vs Endpoint vs WebhookEndpoint (Overview, Goals, CUJ 1-4, Technical Plan §2, §5)
**Discrepancy Description & Rationale:** Three related nouns are used interchangeably without a defined relationship: registered endpoint, subscription, and `WebhookEndpoint`. It is not clear whether one endpoint equals one subscription, whether one endpoint can subscribe to many events/orgs, or why deliveries are keyed by `SubscriptionId` but described as endpoint streams. A naive implementer cannot tell if these are the same entity, a 1:1, or a 1:N.

**Severity:** Major
**Affected Scope:** Standard Webhooks wire format, `details_url`, `ApiAuthorizationFilter`, `ExpiringTokenService` (Technical Plan §4-§5; Detailed Plan #3, #8)
**Discrepancy Description & Rationale:** The design adopts an external wire contract but never links its specification or defines the signed URL mechanism it depends on. `details_url` is called signed and authorization-compatible, but how it is constructed, how it expires, and how a receiver extracts the detail are not explained in this document and not linked. The test-ping, ping event, and delivery-log claims therefore assume prerequisite knowledge a fresh reader does not have.

**Severity:** Major
**Affected Scope:** Payload fields `details_url`, `PublicIP (optional per allowlist)`, `WindowDigest` (§4 table; Technical Plan §4)
**Discrepancy Description & Rationale:** Several payload fields carry the design's privacy promise but are themselves undefined. The document does not explain what a window digest contains, what the PublicIP allowlist is or who configures it, or what a `details_url` resolves to. As written, "metadata-only" cannot be reviewed by a reader who does not already know which fields are safe, because the risky fields are named but not defined.

**Severity:** Major
**Affected Scope:** Retry counts and horizon: 8 attempts / 72 hours vs listed backoff (Overview, Goals §1, CUJ 3, Technical Plan §3)
**Discrepancy Description & Rationale:** The schedule "1 min → 2 min → 5 min → 15 min → 1 h → 6 h with ±20% jitter, up to 8 attempts within 72 hours" lists six intervals for eight attempts, so the timing of attempts 7 and 8 and the total elapsed time cannot be reconstructed. A reader cannot implement or reason about the retry horizon from the text alone. Relatedly, Open Questions Q2 refers to "five catalogued production events (7 events)" without reconciling which events ship.

**Severity:** Nit
**Affected Scope:** Claim-with-lease mechanics (Technical Plan §2, §3; CUJ 2-4)
**Discrepancy Description & Rationale:** Placeholder values ("lease + batch size", "now+lease-minutes", "short lease duration") instead of concrete numbers; the anti-forget condition for reclaiming a `Delivering` row is not stated.

**Severity:** Nit
**Affected Scope:** Pattern reuse justification (`ScriptScheduler` / `DataCleanupService` / `DataService.CleanupOldRecords`)
**Discrepancy Description & Rationale:** The topology is justified as reusing background-service patterns, but none of those services are defined or linked beyond name and shape, so the reuse claim rests on assumed project knowledge.
