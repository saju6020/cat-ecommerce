GATE: CODE_REVIEW (combined: architecture, security, code; owner rule 2026-10-07 for backlog fixes)
HANDOVER: BUGS-NTF (NTF.md N2, N4, N9, N10, N12, N13, N15, NTF-001 #7, #16; FIL.md row 4)
COMMITS: e9e6bf4 9b81476 6bcfe60 76b640b 0af43e9 f9f3cce 67bc607 72598fd (range 7a742ec...HEAD)
DATE: 2026-10-07
STATUS: PASS

FINDINGS
- [MEDIUM] web/components/notifications/SandboxedEmailPreview.tsx:20 — N9 CSP meta stops every subresource load (img, css, font, frame) and a later CSP meta or `<base>` cannot loosen it, but `default-src` does not cover navigation. A previewed `<meta http-equiv="refresh" content="0;url=https://tracker/...">` (or a clicked link) still navigates the frame to a remote page, which sandbox="" does not block, so the admin's IP is still revealed. Fix: strip or neutralise `meta[http-equiv=refresh]` and `a[href]` from the preview HTML, or preview via a parsed DOM rather than raw srcDoc.
- [LOW] web/components/notifications/SandboxedEmailPreview.tsx:20 — the two metas sit before any `<!DOCTYPE>` in the srcdoc, so the preview renders in quirks mode and differs from real mail clients. Fix: put the DOCTYPE first (`<!DOCTYPE html>` + metas) when the body has none, or insert the metas after the body's `<head>`.
- [MEDIUM] src/Application/.../DeliverNotificationCommandHandler.cs:83 and tests/.../QuietHoursEndpointTests.cs:70 — #16 redaction on the new expire path is a copy of the existing block (same `Expire` + `Redact(Secret)` as line 44), so behaviour matches, but the test uses VaccinationDue, which has no secret. Nothing proves a secret-bearing notification is redacted on this path. Fix: add a case with a ContainsSecret template (password reset is Urgent, so use a Normal-urgency secret template or a unit test of the handler) and extract the expire-and-redact into one private method.
- [LOW] src/Api/.../CatProfilesModule.cs (PlaceholderMarkers) — #7 the markers "todo" and "{" / "<" are substring matches, so a real Production host such as `https://todos.example.org` or `mastodon...` containing "todo" makes Production refuse to start. Fix: match "todo" only as a whole host label, or drop it.
- [LOW] src/Api/.../CatProfilesModule.cs — #7 verified safe for test hosts: empty or placeholder outside Production only logs a warning, so Testing and Development still start; HostCompositionTests (Production) sets https://portal.example.com. A Staging deployment with an empty value warns but does not fail; that is the intended trade-off.
- [LOW] web/app/(staff)/staff/notifications/page.tsx — N10 the term is out of the browser URL, history and Referer as intended, but it is still sent as `?destination=` on the API GET, so it appears in server and proxy access logs; Back/reload also no longer restores it. Fix: accept, or move the search to POST; document the choice. Also the eslint-disable on the [destination] effect hides a deps list that omits `pageNumber`, `values` and `router`.
- [LOW] web/components/notifications/NotificationLogFilters.tsx:33-37 — the doc comment is garbled by the edit ("Every value is / but the destination is owned by..."). Fix the sentence.
- [LOW] web/components/inbox/InboxBadge.tsx:52 — N15 the live region is correct (visually hidden, role=status, polite, count text, not colour only), but the first poll result after page load announces e.g. "3 unread messages" on every portal page load. Fix: skip announcing until the count has been seen once, or only announce increases.
- [LOW] web/components/notifications/NotificationLogTable.tsx:86 — N13 the "Updating…" role=status paragraph and aria-busy are accessible, and the role=alert refetch banner has a keyboard-reachable Retry. The log polls, so a screen reader hears "Updating…" on every background refetch. Fix: announce only for user-initiated page or filter changes.
- [LOW] tests/.../FilesEndpointTests.cs — FIL 4 `GroupBy(isEligible)` keeps the single shared predicate and held count is the sum of the non-eligible groups. The test counts COUNT statements (would fail on the old two-count code). It was not run here (needs PostgreSQL). The eligible-id read is still a second statement, so held and purge-batch are not one snapshot, which is fine for the stated row.

NOTES ON THE CHECKS
- N9 test would fail without the fix (srcdoc startsWith CSP meta); it asserts the string, not browser behaviour (jsdom cannot).
- N2, N4, N10, N12, N13, N15 each have a test that fails on the old code (read from the diff). N4 withholds the session and asserts no editor; N12 pages inside the debounce window.
- No superseded-architecture items, no controller, handler or DI changes. #16 and FIL 4 keep the aggregate and repository patterns.
- tsc --noEmit: exit 0. eslint --max-warnings 0: exit 0. vitest --maxWorkers=3: 140 files, 730 tests, all passed. dotnet build CatEcommerce.sln: 0 warnings, 0 errors. Domain tests: 683 passed. Integration tests (new guard, quiet-hours, FIL tests) were not run here.
- web/public/mockServiceWorker.js left untouched and uncommitted.

SCOPE CHECK
- files in allowed scope: 18/18
- files outside allowed scope: none
