GATE: CODE_REVIEW
HANDOVER: HO-XCT-002
COMMITS: 3e52c323 f9cf3644 6fb9566a 61765229 a574b268 68814f65 66c58c4a (range 5a187b6b..66c58c4a)
DATE: 2026-10-14
STATUS: FAIL

FINDINGS
- [HIGH] web/components/offline/OfflineBoard.tsx:77-88 — stale `recoveredAt` bounces the board away while still offline. The monitor keeps `recoveredAt` from any earlier outage for the life of the page (monitor.ts emit({...state}) never clears it) and `seenRecovery` starts at null, so the recovery effect fires on mount whenever the staff tab has recovered once before. The banner's "Open today's offline list" is a client-side `Link`, so the module state survives: second outage in one page load -> click the link -> the board mounts, `router.replace(from)` runs at once, and the person is sent back to a page whose queries are paused. Reproduced with a scratch test (outage, recovery, outage, render board: `replace` called once; scratch file removed). The e2e and U-A15 tests only cover the first outage or a full-load redirect, so they miss it. Fix: initialise `seenRecovery` from the `recoveredAt` at mount (`useRef(recoveredAt)`), or only act when `recoveredAt` changes after mount. Add a unit test for second-outage-then-open-board.
- [MEDIUM] web/lib/connectivity/monitor.ts:96-170 — duplicate ping loops. A failed check calls `rescheduleCheck()` (schedules timer A) and then the caller (`schedule` callback or `checkNow().finally`) schedules timer B; only the last handle is kept, so A is never cleared. After one outage the healthy ping rate doubled (scratch test: 8 pings per 60 s, expected 4) and one ping fired after `stop()`. Fix: a single `scheduleNext()` that always clears the current handle first, and have `rescheduleCheck` skip when a check is in flight. Existing tests only assert status, not ping counts.
- [MEDIUM] web/public/staff/sw.js:109-123 — `handleNavigation` treats a navigation slower than 4 s as offline and answers 302 to `/staff/offline`, even though the server is reachable (cold start, slow 3G). The person lands on the offline board while online. Fix: only fall back when `fetch` rejects (a real network failure), or raise the timeout and abort the fetch with an AbortController; do not redirect on timeout alone.
- [MEDIUM] web/lib/api/offline.ts:49-60 + sync.ts — with `ETag` unreadable cross-origin every refresh is a full 200 and (handover note C10) writes an audit row. Each open tab runs its own 60 s `refetchInterval`, so N tabs write N rows a minute per person. Known and documented as a backend gap; add a cross-tab guard (a `fetchedAt` check in IndexedDB, or a BroadcastChannel leader) and a backend row to expose `ETag`.
- [LOW] web/lib/offline/snapshot.ts touchSnapshot — read-modify-write across tabs can overwrite a newer body written by another tab with an older body plus a fresh `fetchedAt`. Fix: re-read inside one `readwrite` transaction, or skip when the stored etag differs.
- [LOW] web/lib/offline/db.ts openDatabase — the cached connection has no `onclose`/`onversionchange` handler; if the browser closes it, every later read silently returns empty (swallowed by `dbGet*`) until reload. Fix: null `databasePromise` on `onclose` and close on `versionchange`.
- [LOW] web/components/operations/RecordRestoreTestForm.tsx:14,70-78 — an instant typed without an offset is parsed in the browser's local zone and sent as UTC with no hint to the person; after the paste is edited, `drillOutput` still holds the original paste. Fix: require an offset (or label the zone), and send `JSON.stringify(values)` when fields were edited. The UTC conversion itself is correct (`toISOString`).
- [LOW] web/components/observability/WebVitalsReporter.tsx:34-58 — CLS/INP/LCP reported late are attributed to the route current at report time (`context.current`), not the route measured. Fix: capture the route at page load for load-time metrics.
- [LOW] web/lib/api/client.ts apiFetch catch — an `AbortError` from a caller-supplied signal is reported as `unreachable`; two aborted requests can flip the app offline. Fix: ignore `AbortError` when reporting.
- [LOW] web/public/staff/sw.js:67-77 — the assets cache never evicts hashed chunks of earlier builds until `VERSION` is bumped, so it only grows. Fix: drop asset entries not referenced by the freshly cached shell in `cacheShell`.
- [LOW] web/tests/components/offline/store-and-sync.test.tsx:94-122 — `rejects.toBeTruthy()` does not pin the error type. Fix: `rejects.toBeInstanceOf(...)`.

CHECKED, NO FINDING
- Connectivity: 15 s healthy, 3 s after a failure, then 15 s and 60 s after 1 and 5 min offline; listeners removed on unmount (apart from the duplicate-loop finding). Going offline needs two consecutive failures.
- Snapshot sync: an unreadable ETag falls back to an unconditional fetch (a null etag is never sent); `If-None-Match` only for the same person's record; 401/403 branches match the contract (a branch refusal does not switch the copy off). DB v1, two stores, `onupgradeneeded` creates both. A failed `put` (quota) returns false and leaves the old same-person record; not reported as success.
- Idle lock: starts only for a usable record, stays locked, nothing rendered while locked; expiry re-evaluated on the 60 s tick.
- Retention: reapply form shows processed/alreadyErased/notFound/merged, plus `failed` when non-zero; ids validated and capped at 500 before sending. The runs status filter narrows the current page only and says so.
- Backup page: no client-side overdue date maths (the server sends `isRestoreTestOverdue`); date-only fields go through `formatDateOnly`; instants leave the form as UTC.
- Web vitals: fetch+keepalive, credentials omitted, route template only, batches of at most 10, samples dropped when the route would be refused.
- perf-budgets.json: all six routes exist (`/`, `/shop`, `/shop/products/[slug]`, `/cart`, `/checkout`, `/portal`); the script fails on a missing route. Four routes are over 170 kB (documented; owned by SHP/ORD/POR).
- Existing code: `ConfirmDialog` reads connectivity, which stays `online` off the staff layout, so storefront and portal behaviour is unchanged. `client.ts` hooks are inert until the monitor registers. adminNavItems adds three entries. The 3 s recheck is the "Loading..." fix. No other area's behaviour changed.
- Tests: targeted run `vitest run tests/components/{offline,retention,operations,observability}`: 8 files, 90 tests pass. Fake timers use `shouldAdvanceTime` and are restored in `afterEach`.

NOT RUN
- the full 235-route 360 px and axe sweeps
- E-A9, E-C4, E-X2 against the real API
- full vitest and Playwright suites

PROPOSED BACKLOG ROWS (docs/backlog/XCT.md)
- MEDIUM monitor duplicate ping loops
- MEDIUM SW 4 s navigation timeout redirects online users
- MEDIUM per-tab 60 s snapshot refresh writes an audit row per tab (needs ETag exposed and a cross-tab guard)
- LOW touchSnapshot cross-tab race; IndexedDB connection close handling; restore form offset-less instants and drillOutput after edit; web-vitals route attribution; AbortError counted as unreachable; SW asset cache eviction; weak `rejects.toBeTruthy()` assertions

SCOPE CHECK
- files in allowed scope: 99/100 (the 100th is the docs/handover/HO-XCT-002.md Amended row, expected)
- files outside allowed scope: none found in app/lib/components; adminNavItems.ts and ConfirmDialog.tsx are listed in the handover Amended rows
