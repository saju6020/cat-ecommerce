GATE: CODE_REVIEW
HANDOVER: HO-POR-002
COMMITS: 4c2cd1a2 00a0e164 e2c80176 f39f0fa8 c38e6bf4 0f3497b9 f38ebba3 (b12fd39d..f38ebba3)
DATE: 2026-10-13
STATUS: PASS

FINDINGS
No BLOCKER or HIGH. Medium/low rows are proposed for docs/backlog/POR.md (highest existing row is 28).

- [MEDIUM] web/lib/hooks/useReorder.ts:33-37 — onError renews the Idempotency-Key on EVERY 4xx. The backend IdempotencyFilter stores and replays 2xx/4xx, but `409 RequestInProgress` is a filter refusal that is never stored: the original request is still running. Renewing then lets "Try again" send a new key while the first request may still complete, so the same order can be added to the basket twice. ORD-002's `shouldRenewIdempotencyKey` (ho/ORD-002-build, lib/api/errors.ts) deliberately keeps the key for RequestInProgress; this hook does not. Reachable: network error (key kept) -> retry -> 409 RequestInProgress (key renewed) -> Try again. The 409 also shows "Your basket changed while we were adding items", which is wrong for this case. Fix: reuse or port `shouldRenewIdempotencyKey` (4xx except a RequestInProgress violation code); map RequestInProgress to "Still adding your items, please wait a moment". After ORD-002 merges, the two `useIdempotencyKey` hooks (lib/hooks/billing/ and lib/hooks/) should be reconciled.
- [MEDIUM] web/tests/components/portal/reorder.test.tsx:U-B1b — the renewal rules are covered only for network error (kept) and success (renewed). Nothing proves the 4xx renewal, the 5xx keep, or the 409 RequestInProgress keep, which is the rule most likely to break. Add three cases asserting the key on the second request.
- [MEDIUM] web/tests/e2e/portal-dashboard.spec.ts:14 and portal-a11y-responsive.spec.ts:12 — `retries: 1` is set on the whole describe, overriding the config's CI-only retry. The stated cause (cold start lands on /sign-in) was not diagnosed. It is a sign-in/session-restore race that a real regression could share (a lost session on first navigation), and one retry turns any 50% flake into a green run. Fix the race instead: have `signInAsCustomer` wait for the post-sign-in URL/session before `goto('/portal')`, then drop the override. If retained, scope it to the navigation helper, not to every test.
- [LOW] web/components/portal/dashboard/OrdersTile.tsx:47 — `placedOn` is an instant formatted with `formatInstantDate`, which uses the BROWSER zone, so the day can differ from Asia/Dhaka for a customer abroad (05:00Z fixtures hide it). No test asserts any order date. Pass 'Asia/Dhaka' (or the commerce zone) to a zoned date formatter and add a test with a 20:00Z instant.
- [LOW] web/components/portal/dashboard/BalancesTile.tsx:21-48 and HO-POR-001 balances reader — the "500 or more open invoices" label is right when the count is 500. When the scan hits the 500-issued-invoice cap but fewer are open (e.g. 120 of 500 scanned), the count and the "oldest due first" list are silently understated and the DTO carries no truncation flag, so the client cannot say so. Accepted by owner (POR-001 Amended (7)); propose a `scanTruncated` field, or Billing status filter (BIL 66), as a backend row.
- [LOW] web/lib/api/errors.ts describeReorderError — a 401/403/422 (IdempotencyKeyReused) falls to the generic sentence, which is acceptable; a 400 IdempotencyKeyRequired would read "can't be ordered again". Unreachable today; note only.

VERIFIED CLEAN
- Dashboard: absent key -> "Not available yet" (no retry), Unavailable or data null -> "couldn't load" with Try again, Ready -> children; all six tiles go through one DashboardSection; greeting and next-step never treat absent/Unavailable as empty. U-A12 asserts it.
- Dates: followUpOn, earliestEndsOn, dueOn use formatDateOnly (UTC-neutral, no browser zone); appointments are formatted in branchTimeZoneId with the zone named (Dhaka and London cases tested at the hour). No toISOString or new Date() day arithmetic.
- Cache: ['cart'] matches ORD-002's CART_QUERY_KEY; success invalidates it and not the dashboard; test is non-vacuous (asserts isInvalidated both ways).
- Error mapping matches ReorderCommandHandler's reason codes with an unknown-code fallback; codes never rendered.
- Accessibility: aria-disabled keeps focus, focus returns to the opener (tested), live-region summary, headings and landmarks per tile.

PROPOSED docs/backlog/POR.md ROWS (29-33, MEDIUM/LOW, owner: POR frontend unless stated)
29 | MEDIUM | useReorder renews the key on 409 RequestInProgress (double-add risk); reuse ORD-002 shouldRenewIdempotencyKey and add a RequestInProgress message
30 | MEDIUM | Add reorder tests for 4xx renew / 5xx keep / 409 RequestInProgress keep
31 | MEDIUM | Remove describe-level `retries: 1` from the portal e2e specs; fix the sign-in race in signInAsCustomer
32 | LOW | OrdersTile placedOn in browser zone, not Asia/Dhaka; add a test
33 | LOW (backend) | Balances dashboard: expose scan truncation when the 500-invoice cap is hit with fewer open invoices (BIL 66)

SCOPE CHECK
- files in allowed scope: 34/34
- files outside allowed scope: none
