GATE: CODE_REVIEW
HANDOVER: HO-ADM-002
COMMITS: ce14242 8498378 ec00965 fa80c47 (delta pass, third pass, over b7065fc..HEAD; HEAD = fa80c47; web/ only)
DATE: 2026-09-29
STATUS: PASS

Commands (web/): npx tsc --noEmit -> clean; npm run lint (eslint --max-warnings 0) -> clean;
npx vitest run -> 84 files, 311 tests, all passed. No Playwright (per instruction).

FINDINGS
- [MEDIUM] web/app/(staff)/staff/admin/audit-log/page.tsx:57-70 and components/audit/AuditLogSearch.tsx:106,118 — clearing a date (or Chrome's transient '' while typing a partial date) now sets local `from`/`to` to ''; own-write landing never resyncs, so `buildAuditQuery` sends `from=`/`to=` (admin-audit.ts:40-41, unconditional) and the export URL too. Before the delta an absent param fell back to the default range. Reload shows defaults again, so screen and URL disagree. Fix: fall back to the default (or skip the write) when a date is cleared.
- [MEDIUM] web/app/(staff)/staff/admin/audit-log/page.tsx:64-69 — the query uses local `filters`, so the 300 ms debounce only delays the URL, not the request: one audit request per keystroke, each with the stale `pageNumber` (read from the URL, dropped only when the write lands), so a user on page 3 fires page-3 requests with new filters first. Fix: derive the query filters from a debounced/committed copy, and reset page with the local edit.
- [MEDIUM] web/lib/hooks/useFilterUrlUpdate.ts:97-101 and web/lib/hooks/useUrlFilterState.ts:223-225 — a write that produces the same query as the current URL (type a char then delete it inside 300 ms, so the final write equals the URL) never changes `searchParamsQuery`, so its `pendingWrites` entry is never drained; it lingers until the next own write lands or an external nav clears the list. A later external nav to a URL equal to that entry is mistaken for own write (no resync, stale local field). Narrow; fix: skip the write (and the record) when the new query equals the current one.
- [MEDIUM] web/app/(staff)/staff/admin/tax-classes/[taxClassId]/page.tsx:40-44, services/[serviceId]/page.tsx:45-48 — a 403 on the dependent rates/prices query replaces the already-loaded parent with PermissionDeniedState (defensible, but untested and unstated in the handover). Also, per §11.9 `BranchNotAssigned`/`BranchAssignmentRequired` 403s now render PermissionDeniedState on every changed page, the exact outcome BranchAssignmentBanner's own comment says is wrong; the banner and `getBranchAssignmentErrorCode` are referenced nowhere in app/ or components/ (pre-existing dead wiring, now more visible). Form/action paths do distinguish the two codes via describeForbidden.
- [LOW] web/lib/hooks/useUrlFilterState.ts:244-250 — `flush` reads `values[name]` from the render closure rather than the timer's captured value; equal in practice after a discrete event, but the timer value is the safer source.
- [LOW] web/tests/components/admin/detail-pages-403.test.tsx — covers tax-class/service/roster 403 and 500 only. No test for 404 (ApiNotFoundError branch on both detail pages), a rates/prices 403 with the parent loaded, or the system/settings/closures/rooms/leave pages changed in fa80c47; the 500 tests pass via retry+`findByText` 5 s timeout (slow, not flaky).
- [LOW] web/tests/components/audit/*.test.tsx, content/content-filter-url-state.test.tsx — no test for unmount with a pending debounce timer (the cleanup at useUrlFilterState.ts:214-221 is unasserted), no StrictMode test, no test for the same-query-write leak above. Tests that exist assert real behaviour (URL args, field values, push/replace counts) and can fail.
- [LOW] web/components/audit/AuditLogSearch.tsx:81-90 — free-text inputs have no `type`/`aria` additions; fine, but the new PermissionDeniedState/ErrorState paths reuse `role="alert"` (PermissionDeniedState) so are announced; no a11y defect found.
- Known, not re-raised: U-G7 / backlog row 22 (debounce timer not cancelled by Back).

Checked and found correct: unmount clears all timers (no setState/router call after unmount); StrictMode double-effect only causes a harmless same-value resync on mount; the mount guard and pathname captured in callbacks are safe because a pathname change unmounts the page; 403 check order on detail pages (403 -> 404 -> loading -> generic error with retry) is right, no endless spinner, `noRetryOn403` used so 403 is immediate; roster page 403 check sits after all hooks; describeApiError/describeUnexpectedError keep the two §11.9 codes distinct from each other and from the generic text, no existing caller shows a wrong message (a 403 previously got the generic retry text); lint/tsc clean.

SCOPE CHECK
- files in allowed scope: 31/31 (all under web/ per handover area; docs change web/docs/decisions.md only)
- files outside allowed scope: none
