# Stage 8 — Intermediate Zoom Out Review

Target: `docs/designs/webhook-delivery.md` (402 lines, includes prior Stage 5 report)
Focus: What Stage 5 missed; whether Stage 5 findings were mitigated since.

Verdict: No Stage 5 Blocker or Major was mitigated. The only material addition — the Detailed Implementation Plan (§284-398) — worsened the largest Stage 5 finding. As drafted, v1 remains over-scoped, over-detailed, and under-contextualized. Shrink and de-weeds before implementation; do not build the 22-file plan as written.

## Mitigation check — Stage 5 findings vs. current text

All Stage 5 items persist verbatim: B1 (7 events YAGNI), M1 (task list), M2 (premature scale-out), M3 (header duplication), M4 (flag proliferation), M5 (producer cost), M6 (TOCTOU), M7 (outbox flooding), N3 (backoff arithmetic).

## New findings Stage 5 missed

- **Blocker — Anti-Weeds failure:** The Detailed Implementation Plan (§284-398) is an implementation task list (file paths, DI, migration commands, test inventories) that freezes decisions Q1/Q2 have not made. Delete it; design stops at outbox + dispatcher + contract + authz.
- **Blocker — `keyboard.log.available`:** highest-volume, most-sensitive event with no named day-1 consumer; metadata-only digest still leaks `WindowTitle`/`ProcessName`/`PublicIP`. Cut from v1 entirely.
- **Blocker — `details_url`:** a second, undefined auth system carrying exfiltration risk; no host, construction, expiry, or revocation. Remove from v1 (IDs only), or justify against `ExpiringTokenService` reuse first.
- **Major — Tenant model undefined:** Organization/DeviceGroup/Subscription/Endpoint relationships are never defined; core authorization claim cannot be stated by a naive reader. Define the 1:1/1:N relationships or collapse to one noun and one scope.
- **Major — EventId identity contradiction:** per-subscription rows mint N `webhook-id` values per logical event; 24 h dedupe < 72 h retry horizon. One root event ID + per-delivery attempt IDs is simpler and makes dedupe true.
- **Major — Throughput/reliability numbers:** 2 s tick × batch 50 × 10 s timeout cannot sustain the "no head-of-line blocking" claim; frozen constants without load evidence are weeds in NFR clothes.
- **Major — Alerts transition undecided while §6 decides a breaking change:** simplest rollout (webhooks additive, alerts untouched) never considered; Q1 must be decided before implementation.
- **Major — New services inventory unverified:** no code-search evidence that an existing SSRF validator/HMAC helper/DataProtection pattern already covers the new guard/signing types; `SigningSecretHash` name vs reversible-secret contradiction persists (Critic B1). Risks & Limits section still absent.

## Passes kept (unchanged since Stage 5)

- Transactional outbox pull (not inline `await PostAsync`).
- Single delivery plane — alert path becomes enqueue.
- Metadata-only DTO direction.
- SSRF guard + HMAC + encrypted secrets as a security floor.

These survive any shrink; the shrink does not require revisiting them.
