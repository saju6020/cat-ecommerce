GATE: ARCHITECTURE
HANDOVER: HO-XCT-002
COMMITS: 3e52c323 f9cf3644 6fb9566a 61765229 a574b268 68814f65 66c58c4a (branch ho/XCT-002, `git diff 5a187b6b..66c58c4a`, 100 files)
DATE: 2026-10-14
STATUS: FAIL

The owner's gate policy (2026-09-27) applies: only BLOCKER and HIGH findings fail a gate. MEDIUM and LOW findings are proposed below as rows
61 onwards for docs/backlog/XCT.md, whose highest row is 60 on both master and this branch. The SECURITY and CODE_REVIEW gates ran in
parallel and proposed rows without numbers, so the integrator renumbers when combining them. This gate did not append any rows.

The gate fails on **two HIGH findings, both small fixes**. There is no BLOCKER. The layering, the service worker, the IndexedDB boundary,
the read-only offline guard and the scoped edits to other slices' files all comply.

What this pass reviewed:
- HO-XCT-002 in full: §1 Amended (the 2026-10-13 integrator rows and the 2026-10-14 build row, items 1-21), §3-§14 and §20.
- HO-ACC-002 §1 Amended and §4.3. HO-CAT-002 §8.2 (layering). The HO-XCT-001 §12.4 `DrillOutput` contract, and the backend validator it
  is enforced by.
- `web/README`-level precedent from earlier gates: the BIL-002, NTF-002 and EMR-002 rulings on `adminNavItems.ts`; the ADM-002, APT-002,
  FIL-002, POR-002 and SHP-002 rulings on a slice's own handover Amended row; the XCT-001 ruling (row 33) on files outside the literal
  §13 list.
- Every changed source file in the diff. Tests and mocks were read for scope only.

HO-XCT-002 §6 says the `.ai/` documents govern the backend only. That is the HO-CAT-002 position, so this frontend diff was judged against
the handovers rather than `.ai/`. Nothing was built and no suite was run. One scratch simulation of the monitor's timer logic was run
outside the repository (finding H1); every runtime claim is marked for TEST.

FINDINGS

- [HIGH] H1. web/lib/connectivity/monitor.ts:84, :163-166, :172-177 — **the ping loop duplicates itself on every failed check, so the
  ADR-FE-142 / OQ-3 cadence does not hold.**
  - The rule: one ping per staff tab every 15 s. After a failure, one ping every 3 s for the first minute offline (Amended 20), then 15 s,
    then 60 s after five minutes.
  - What the code does:
    1. `recordOutcome('unreachable')` calls `rescheduleCheck()`, which clears `timer` and schedules a new timer, A.
    2. The caller then schedules again: `await forceCheck(); if (mounts > 0) schedule();` in the timer callback, or `.finally(schedule)`
       in `checkNow`. That creates timer B, which overwrites the only handle, so A is never cleared.
    3. The two chains fire back to back with both pings in flight. Each failure adds one more, so concurrent chains grow by about one per
       check cycle for as long as the outage lasts.
    4. On recovery, every chain survives at 15 s, for the life of the `(staff)` layout, which is the whole staff session.
  - Rough scale: a 10-minute outage leaves about 40 pings per 15 s per tab, about 2.7 requests a second, on every front-desk PC, aimed at
    a server that has just come back. A client-reported failure that arrives while a ping is in flight duplicates the loop the same way.
  - Evidence: a scratch Node simulation of exactly this control flow (in the scratchpad, not the repo). Over equal windows, pings per
    window rose steadily: 3, 3, 4, 5, 6, 7, 8, 9, 10; peak concurrency was 11. `tests/components/offline/connectivity.test.tsx` asserts
    status only and never counts pings.
  - CODE_REVIEW filed the same defect as a MEDIUM ("rate doubled after one outage"). This gate rates it HIGH because the duplication grows
    with the length of the outage rather than doubling once, and it contradicts an approved ADR.
  - **TEST should confirm with fake timers:** count `pingServer` calls over 10 simulated minutes offline, then 2 minutes online.

- [HIGH] H2. web/components/operations/RecordRestoreTestForm.tsx:98 — **`drillOutput: pasted || JSON.stringify(values)` makes up the
  drill evidence when nothing was pasted.**
  - The contract: HO-XCT-001 §12.4 defines `DrillOutput` as "the drill script's JSON, pasted". `RestoreTestCommandValidators.cs:17`
    enforces `NotEmpty()`.
  - What the code does: for a hand-entered test it serialises the form's own values (notes, supersedes id and the instants before UTC
    conversion) and sends that as the drill output. The validator passes, and the detail page later shows that text in its `<pre>` as if
    `restore-drill.sh` had printed it.
  - Why it fails: it is a client-side workaround for a server rule (§20.4, "do not shim"), and it does so on an audit-grade evidence record
    (US-XCT-05 AC2).
  - §1 Amended (6) says only "the form sends the pasted JSON text itself as `drillOutput`" and does not disclose the fallback.
  - Compliant behaviour is either (a) refuse to submit without a paste, with a visible reason, or (b) send what the person entered and let
    the server's `400` show. Which one is a product choice; this gate does not pick it.

- [MEDIUM] Merge condition, not a code defect. **The §5.3 amendments are recorded in none of their target handovers.** Ruling on the
  HO-ACC-002 IndexedDB carve-out (§20.2):
  - **Yes, a ratifying §1 Amended row must be added to HO-ACC-002 before merge.** It should cite ADR-FE-141 and the U-X4 / E-X3 extension
    ("no session token, password, CAPTCHA answer or draft in IndexedDB either").
  - Who writes it: the integrator or README owner, on master or in the merge commit. This branch may not touch HO-ACC-002 (§14
    `docs/handover/**`). The precedent is c70180d6, where the integrator ratified HO-XCT-001's §5.3 edits at merge.
  - Why it does not fail this gate:
    - The code stays inside the carve-out exactly as ADR-FE-141 bounds it. The only `indexedDB` references are in `web/lib/offline/**`,
      and the two stores are `snapshot` and `device`.
    - Taken literally, HO-ACC-002 §4.3 forbids storing "a session, a reset token, a password, a CAPTCHA answer or a draft", and the
      snapshot is none of these.
  - Why the row is still needed: HO-XCT-002 §20.2 makes it the licence the SECURITY gate reads. The coordinator's instruction to build
    authorised the build; it is not the record.
  - The same merge-time ratification is owed for the rest of §5.3:
    - HO-ACC-002 §11: the erase panel on the staff customer page.
    - HO-ACC-002 §8.4: the store is wiped on sign-out, `session_invalid` and a person change.
    - HO-APT-002 §11.5 and HO-BRD-002: the banner, and read-only state taken from `useConnectivity()`.
    - HO-CAT-002 §12.1: the `next.config.ts` headers and the root-layout reporter.
  - Proposed row 61. It is marked as a merge precondition, so it must not be left in the backlog past the merge.

- [MEDIUM] **Files outside the literal §13 list, with no Amended record.**
  - Three new files: `web/lib/operations/drill.ts`, `web/lib/retention/labels.ts` and
    `web/tests/components/observability/web-vitals.test.tsx` (§13 names `tests/components/{offline,retention,operations}` only).
  - One line: `<OverdueRestoreBanner />` in `web/app/(staff)/layout.tsx:43`. §13 limits that file to "ConnectivityProvider,
    OfflineBanner, OfflineSnapshotSync, ServiceWorkerRegistrar, admin nav entries only".
  - None of these is another slice's file. The banner is required by §11.5 on "any admin route", and the admin layout belongs to
    HO-ADM-002 and is not in §13, so the `(staff)` layout was the only in-scope place for it. Under the XCT-001 row 33 precedent this is
    cured by an integrator Amended row naming them, not by a code change. Proposed row 62.

- [MEDIUM] web/components/offline/ServiceWorkerRegistrar.tsx:15-23, web/app/(staff)/layout.tsx:38-40, web/lib/offline/snapshot.ts:58-63 —
  **the service-worker clean-up rules of ADR-FE-140 / §12.1 are only partly met.**
  1. In mock or development mode, a previously registered worker is removed only from inside `StaffSessionGuard`, that is, only for a
     signed-in member of staff. ADR-FE-140 says "unregistered on load".
     - This is exactly the §20.9 collision. A stale `/staff/` worker controls `/staff/sign-in` and passes `/api/` straight to the network,
       so MSW never sees those requests and sign-in fails.
     - Because sign-in fails, the clean-up never runs.
     - Affects a developer who once ran a production build with the device opted in.
  2. §12.1 lists "`unregister()` plus `purge`" on sign-out. Sign-out only purges the caches (`wipeOfflineSnapshots` ->
     `purgeOfflineServiceWorker`); the worker stays registered until the opt-in goes off.
  - Neither deviation is in §1 Amended. U-A17 tests `reconcileServiceWorker` in isolation, not where it is mounted. TEST to confirm.
    Proposed row 63.

- [MEDIUM] web/lib/connectivity/monitor.ts:27-32, web/docs/decisions.md (ADR-FE-142) — **the 3 s fast recheck (§1 Amended 20) changes
  the default of OQ-3, which belongs to the owner and ops.**
  - The builder's reasoning holds up: TanStack pauses queries while the monitor says offline, and the 15 s-only default left staff pages
    on their loading screen. Even so, a builder's Amended row is not the owner's answer to an open question.
  - ADR-FE-142 still says "every 15 s (60 s after five minutes)" and does not mention 3 s.
  - Owner to confirm, and the ADR text to be updated to match. Proposed row 64. This is separate from H1, which is a defect in how the
    recheck is scheduled, not a question about its interval.

- [MEDIUM] **Ruling on WriteButton, ConfirmDialog and the front desk's own buttons (§1 Amended 13).**
  - What is compliant:
    - `components/common/WriteButton.tsx` (new) and the `ConfirmDialog.tsx` edit are exactly what §13 allows for `components/common/**`:
      they consume `useWriteEnabled()`, show the visible reason and set `aria-describedby`.
    - The front desk's ConfirmDialog-based actions are therefore disabled offline.
    - Leaving HO-APT-002's own check-in and walk-in buttons unedited is correct; editing them would have been a §14 BLOCKER. They still
      fail fast through the `client.ts` guard, with nothing queued and nothing lost.
  - What follows from it: §18 A3 and E-A1 ("every action is disabled with a reason") are not fully met on the front desk. Closing that gap
    needs the HO-APT-002 §5.3 amendment and an APT follow-up, or the owner accepting fail-fast. Owner: HO-APT-002. Proposed row 65.

- [MEDIUM] Backend, HO-XCT-001 (for the integrator's XCT backlog). **Ruling on Amended (19): restore tests answer `500` for an instant with
  a non-UTC offset.**
  - The frontend's conversion is compliant and is not a shim. `RecordRestoreTestForm.tsx:51-53` sends the same instant written in UTC,
    which is a valid value under the unchanged contract. `restore-drill.sh` already prints UTC.
  - The `500` is an HO-XCT-001 defect: Npgsql accepts only offset 0 for `timestamptz`. The fix is to normalise with
    `ToUniversalTime()` in the handler or aggregate, or to reject in the validator with a `400`. Any other client would still hit it.
  - Proposed row 66. It is shared with SECURITY's LOW; count it once.

- [MEDIUM] Backend and host, HO-XCT-001 (§1 Amended 1). `WithExposedHeaders("Date")` hides `ETag`, so the client can never send
  `If-None-Match`. Every snapshot refresh is therefore a full `200`, and every `200` writes an audit row (C10). CODE_REVIEW reached the
  same conclusion. The client degrades correctly: it reads the header only when the API exposes it. Expose `ETag` and `Cache-Control`.
  Proposed row 67.

- [MEDIUM] Other owners. **Ruling on the ADR-FE-148 performance budget findings.**
  - **Reporting rather than fixing is correct.** ADR-FE-148, §20.7 and §14 all require it, and fixing them was out of scope.
  - `perf-budgets.json` is not relaxed (2 500 ms; 170 kB). No sweep excludes a route.
  - Findings and owners:
    - First-load JS over budget: `/shop/products/[slug]` 203 kB (HO-SHP-002); `/cart` 207 kB and `/checkout` 241 kB (HO-ORD-002);
      `/portal` 194 kB (HO-POR-002).
    - `/` is at the edge of the LCP budget, with a median of 2 412 ms from one run at 2 564 ms (home page owner).
  - The integrator should file these in SHP.md, ORD.md and POR.md; they are not XCT rows.
  - Note for the owner: `npm run perf:js` exits 1 on this head. If it is wired into CI, master goes red on merge, which is ADR-FE-147
    working as designed.
  - Proposed row 68 (pointer only).

- [LOW] Backend, HO-XCT-001 (§1 Amended 2). **Ruling on `GET /api/admin/retention/runs` having no `status` filter.** The page-local
  filter, which says it covers only the page shown, is the honest degradation, and no query parameter was invented. UI-4 is only partly
  met until the API gains the filter. Proposed row 69.

- [LOW] **Ruling on Amended (7): the snapshot's active-branch refusal is `403 { error }`.** Only `ActionNotPermitted` turns the device
  copy off. A branch refusal leaves the copy and the flag alone, which is the conservative reading of §11.2: a branch problem is not a
  statement about the person. No shim. The difference from the `errors[]` shape is a backend consistency point. Proposed row 70.

- [LOW] web/lib/api/client.ts:274-279, :344-347 — `client.ts` gained a fourth change beyond §13's three (offline guard, failure reporting,
  `sendTelemetry`): conditional-GET support (`onResponseHeaders`, and a `304` returning `undefined`). §8.2 and §11.2 require it, and
  Amended (1) mentions it in passing. It should be named explicitly in the Amended row. Proposed row 71.

- [LOW] web/components/offline/ConnectivityProvider.tsx:23-33 — the provider changes the app-wide `QueryClient` mutation defaults and
  `onlineManager`, and never restores them on unmount. A person who leaves the staff routes while the monitor says offline can leave
  TanStack's `onlineManager` set to offline for the rest of that SPA session. TEST to confirm. Proposed row 72.

- [LOW] web/tests/perf/lcp.spec.ts:49 — a budget route missing from the inventory is skipped, although §11.6 says a missing named route
  fails. `check-js-budget.mjs` does fail on it (line 65), so a rotted budget is still caught. Proposed row 73.

- [LOW] web/public/staff/sw.js:110-121 — a navigation that is slow but online (more than 4 s, per §12.1) is redirected to
  `/staff/offline?from=…`. The board leaves only on an offline-to-online transition (`recoveredAt`), so with a fresh monitor it can keep a
  person on the saved copy while the server is up. CODE_REVIEW filed the same as a MEDIUM. TEST to confirm. Proposed row 74.

VERIFIED (no finding)
- **StaffSessionGuard is unchanged.** It is not in the diff. Neither are `SignOutButton`, `IdleLock`, `app/providers.tsx` or any HO-APT-002
  front-desk file. Sign-out is detected from the session query being removed, not hooked (Amended 14).
- **ADR-FE-139…148: no collision.**
  - Each is a heading exactly once in `web/docs/decisions.md`.
  - `ho/RPT-002` uses ADR-FE-129…138 and `ho/SRG-002` uses ADR-FE-069…078. Neither branch, master nor any other handover uses 139-148.
  - The README allocation table assigns 139-148 to HO-XCT-002.
  - Expect an append-on-append text conflict in `decisions.md` at merge; the integrator should recheck the ADR text after resolving it.
- **Offline-board architecture.**
  - `(offline)` is its own route group, with no session guard and no server data.
  - `force-static` is set on both offline pages.
  - The board reads IndexedDB once and mounts no fetching query. `useKnownStaffAccountId` is a disabled observer in `lib/hooks`.
  - **OfflineIdleLock as a hook** (`useIdleTimeout` in `lib/hooks`, called by `OfflineBoard`) is compliant. §11.4 lists the behaviour, not
    a file. The hook keeps the layering rule (hooks live in `lib/hooks`) and is recorded in Amended (15).
  - The reload bypass of the lock and the person binding is SECURITY's HIGH; it is not repeated here.
- **Service worker (ADR-FE-139/140).**
  - Scope is `/staff/`, set by its location. No `Service-Worker-Allowed` header.
  - `/api/` and other origins return without `respondWith`. Only `/_next/static/**`, `/staff/icons/**` and the `/staff/offline` shell are
    cached. `/staff/sign-in` is never intercepted.
  - Registration requires production, mocks off and the device opt-in.
  - The `302` to `/staff/offline?from=` is a recorded deviation (Amended 16, ADR-FE-140 Status) that avoids putting the shell's markup
    under another page's URL. Accepted.
- **Connectivity store (ADR-FE-142).**
  - A module-level `subscribe`/`getSnapshot` store read through `useSyncExternalStore`: a device fact, not an app store.
  - `client.ts` imports nothing from the monitor. The monitor registers through `setConnectivityReporter`, the same direction as
    `setSessionErrorHandler`.
  - The ping skips reporting and the unsafe-method guard.
- **Offline guard (ADR-FE-143).** `OfflineError` is thrown before the request for an unsafe method while the monitor says offline; nothing
  is queued. Mutations use `networkMode: 'always'`. The page stays mounted.
- **Layering (HO-CAT-002 §8.2).**
  - `fetch` is called only in `client.ts`; `sendTelemetry` lives there too.
  - Only `lib/hooks/` declares `useQuery`/`useMutation`. Components use only `useQueryClient`, which matches master's `SignOutButton`.
  - No `localStorage`, `sessionStorage` or `dangerouslySetInnerHTML` in the diff.
  - Component imports from `lib/api` are type imports, except the reporter's `sendWebVitals`, which §11.6 specifies.
- **No new dependency.** `package.json` gained four scripts only. No lockfile, `tsconfig` or `eslint` change. No workbox, `idb`,
  `web-vitals` or Lighthouse package.
- **Scoped edits to other slices' files.**
  - `app/layout.tsx`: the reporter only.
  - `next.config.ts`: the `/staff/sw.js` headers only.
  - `customers/[customerId]/page.tsx`: the erase panel only, gated for presentation.
  - `mocks/handlers/index.ts`: append only.
  - `errors.ts`: an `OfflineError` branch added in three existing mappers, which §11.7 requires, plus appended operations codes.
  - `adminNavItems.ts`: three entries, recorded in Amended (18). This follows the BIL-002, NTF-002 and EMR-002 precedent for nav entries.
- **Erasure contract (2026-10-13 Amended).** `{ channel, identityCheck }`, with the four fixed codes as a select. No free text and no
  reason box.
- **The handover edit.** `docs/handover/HO-XCT-002.md` changed by appending to its §1 Amended cell only. Checked: the old cell is an exact
  prefix of the new one. This follows the ADM/APT/FIL/POR/SHP-002 precedent.

PROPOSED ROWS FOR docs/backlog/XCT.md (highest existing row: 60)
| 61 | MEDIUM / MERGE PRECONDITION | Integrator: before merging HO-XCT-002, record its §5.3 amendments in the target handovers' §1 Amended rows. HO-ACC-002 §4.3/ADR-FE-002: the IndexedDB carve-out (ADR-FE-141), with U-X4/E-X3 extended to IndexedDB. HO-ACC-002 §11: the erase panel. HO-ACC-002 §8.4: the store is wiped on sign-out, `session_invalid` and a person change. HO-APT-002 §11.5 and HO-BRD-002: the banner and `useConnectivity()`. HO-CAT-002 §12.1: the headers and the reporter. HO-XCT-002 §20.2 makes the first of these the feature's licence. | ARCHITECTURE |
| 62 | MEDIUM | Integrator: name in HO-XCT-002 §1 Amended the files outside literal §13: `web/lib/operations/drill.ts`, `web/lib/retention/labels.ts`, `web/tests/components/observability/web-vitals.test.tsx`, and the `<OverdueRestoreBanner />` line in `app/(staff)/layout.tsx` (§11.5 needs it on every admin route; the admin layout is HO-ADM-002's). | ARCHITECTURE |
| 63 | MEDIUM | ADR-FE-140 / §12.1 clean-up. `ServiceWorkerRegistrar` runs only inside `StaffSessionGuard`, so in mock or dev mode a stale `/staff/` worker (which breaks MSW on `/staff/sign-in`, §20.9) is never removed. Sign-out purges the caches but does not `unregister()`. Meet "unregistered on load" and the sign-out unregister, or record the deviation. | ARCHITECTURE |
| 64 | MEDIUM / OWNER | OQ-3 changed by the build (Amended 20): 3 s recheck after a failure and for the first minute offline. Owner/ops to confirm. Update ADR-FE-142's text, which still says 15 s / 60 s. | ARCHITECTURE |
| 65 | MEDIUM / APT | Front desk check-in and walk-in buttons (HO-APT-002 files) are refused fast offline but not disabled with a reason; §18 A3 and E-A1 are partly met. Needs the HO-APT-002 §5.3 amendment and an APT follow-up (consume `useWriteEnabled()` or `WriteButton`), or owner acceptance. | ARCHITECTURE |
| 66 | MEDIUM / HO-XCT-001 | `POST /api/admin/operations/restore-tests` answers `500` for a `DateTimeOffset` with a non-zero offset (Npgsql `timestamptz` accepts UTC only). Normalise with `ToUniversalTime()` or reject in the validator with a `400`. The frontend sends UTC. Same as SECURITY's LOW. | ARCHITECTURE |
| 67 | MEDIUM / HO-XCT-001 | CORS exposes only `Date`. Add `ETag` (and `Cache-Control`) to `WithExposedHeaders`, so the snapshot's `If-None-Match` and `304` path works and each refresh stops writing a C10 audit row. Same as CODE_REVIEW's MEDIUM. | ARCHITECTURE |
| 68 | MEDIUM / SHP, ORD, POR | Pointer only: the first-load JS breaches (`/shop/products/[slug]` 203 kB, `/cart` 207 kB, `/checkout` 241 kB, `/portal` 194 kB, against 170 kB) and `/` at the edge of the LCP budget go to SHP.md, ORD.md and POR.md (ADR-FE-148). `npm run perf:js` exits 1 until they are fixed. | ARCHITECTURE |
| 69 | LOW / HO-XCT-001 | `GET /api/admin/retention/runs` has no `status` filter (Amended 2); the UI filters the current page only. Add the filter for UI-4. | ARCHITECTURE |
| 70 | LOW / HO-XCT-001 | The snapshot's active-branch refusal is `403 { error }`, not the `errors[]` shape (Amended 7). Align it with the other refusals. | ARCHITECTURE |
| 71 | LOW | Name `client.ts`'s conditional-GET plumbing (`onResponseHeaders`, and a `304` returning `undefined`) in HO-XCT-002 §1 Amended as a fourth change beyond §13's three. | ARCHITECTURE |
| 72 | LOW | `ConnectivityProvider` changes app-wide `QueryClient` mutation defaults and `onlineManager`, and never restores them on unmount. Leaving staff routes while offline may leave TanStack offline for the rest of the SPA session. TEST to verify. | ARCHITECTURE |
| 73 | LOW | `tests/perf/lcp.spec.ts:49` skips a budget route missing from the inventory; §11.6 says a missing route fails. Make it fail, as `check-js-budget.mjs` already does. | ARCHITECTURE |
| 74 | LOW | `sw.js` redirects a slow-but-online staff navigation (more than 4 s) to the board, and the board leaves only on an offline-to-online transition. Same as CODE_REVIEW's MEDIUM; count it once at the higher severity. | ARCHITECTURE |

H1 and H2 are not proposed as rows; they must be fixed on this branch. H1 overlaps CODE_REVIEW's MEDIUM "monitor duplicate ping loops",
which should be counted once, at HIGH.

SCOPE CHECK
- files in allowed scope: 97/100
  - Accepted under precedent: `docs/handover/HO-XCT-002.md` (append-only §1 Amended) and `web/components/admin/adminNavItems.ts` (Amended
    18; BIL/NTF/EMR-002 precedent).
  - Within the §13 line limits: `web/app/(staff)/layout.tsx`, except the one `OverdueRestoreBanner` line covered by row 62.
- files outside allowed scope: `web/lib/operations/drill.ts`, `web/lib/retention/labels.ts`,
  `web/tests/components/observability/web-vitals.test.tsx` (new files with no other owner; none is in §14), plus the
  `OverdueRestoreBanner` line in `web/app/(staff)/layout.tsx`. This gate's rule makes any file outside scope a FAIL on its own; under the
  XCT-001 row 33 precedent it is cured by the integrator's Amended row (proposed row 62), not by a code change. No §14 file was edited:
  nothing under `src/**`, `tests/**`, `deploy/**`, `.ai/**` or `.claude/**`, no package dependency, and no storefront, portal or checkout
  route file.


---

## Combined re-check — 2026-10-14 (architecture + security + code review, fix round)

GATE: ARCHITECTURE (combined re-check covering SECURITY and CODE_REVIEW findings)
HANDOVER: HO-XCT-002
COMMITS: 77d4d418 (range 39ea78ec..77d4d418, branch ho/XCT-002, 39 files)
DATE: 2026-10-14
STATUS: FAIL

One new HIGH. It is a residual of the SECURITY HIGH that was introduced by this fix round. The CODE_REVIEW HIGH and ARCHITECTURE H1 and
H2 are fixed. Owner decisions applied: phone numbers "Remove them"; recheck "Keep 3 s after a failure".

Evidence: `vitest run tests/components/{offline,operations,observability}` gave 9 files and 104 tests, all passing. The worktree's own
node_modules was used, so no junction was needed. Mutation runs: each mutation was applied, tested and restored, and the tree is clean.
The full suites and Playwright were not run.

FINDINGS
- [HIGH] web/components/offline/OfflineSnapshotSync.tsx:48-52 (with web/lib/offline/activity.ts) — **the online HO-ACC-002 idle lock
  still leads to an unlocked offline board.** The SECURITY HIGH named this bypass, and it remains in this narrower form.
  - What the code does: `recording = loaded && settings.offlineEnabled && staffAccountId !== null`. It never consults
    `useStaffLockState().locked`.
  - When `IdleLock` locks in place, it keeps the children mounted. `useStaffSession()` keeps its last `data` when the query errors with
    `StaffSessionIdleTimeoutError`. So `staffAccountId` stays non-null, and every `pointermove` or `keydown` on the lock dialog writes
    `lastActivityAt = now`.
  - Nothing writes `lockedForStaffAccountId` when the online lock fires. Only the board's own timer does that.
  - Exploit, with no DevTools:
    1. A receptionist walks away and the online idle lock fires.
    2. A passer-by moves the mouse over the lock dialog, which marks the device active.
    3. They switch off Wi-Fi or pull the cable, then open `/staff/offline` by typing it, from history, or through any `/staff/` link
       that `sw.js` redirects.
    4. The ping fails. `isBoardLocked` sees a fresh `lastActivityAt` and no saved lock, so it shows today's list without a sign-in:
       customer names, high-risk handling notes and boarding instructions.
  - When it works:
    - Within roughly one or two minutes of the lock. After that, a snapshot refresh that gets `session_invalid` wipes the copy.
    - At any time if no such refresh reached the server. Examples: the tab was hidden, so `refetchInterval` does not run in the
      background, or the network was already down. In those cases the attacker cuts the network before the next refresh.
  - Remediation, inside XCT files:
    1. Gate `recording` on `!useStaffLockState().locked`. Read the HO-ACC-002 context only; do not edit IdleLock.
    2. When `locked` turns true with a known `staffAccountId`, call `lockOfflineBoard(staffAccountId)`. The board then opens locked, and
       the next successful sync after re-authentication lifts it, through the existing path.
    3. Add a test: lock the staff shell, fire `pointermove`, and assert that nothing is written to `lastActivityAt` and that
       `lockedForStaffAccountId` is set.

- [MEDIUM] web/lib/offline/device.ts:89-95 + web/components/offline/OfflineBoard.tsx:115-116 — a lock saved for person A blocks person B.
  - `locked` holds for any non-null `lockedForStaffAccountId`. `clearOfflineBoardLock(B)` lifts only a lock saved for B.
  - Result: when B signs in on a device that A left locked, B's copy is synced, but B's board opens locked during B's first outage. It
    only heals after that, once the board re-saves the lock for B and B syncs again.
  - This fails closed. Fix: any successful sync lifts the lock. `removeOtherPeoplesSnapshots` has already made the copy the syncing
    person's.
- [LOW] Test gap for ARCH H1, not a defect. The code is correct.
  - These mutations are NOT caught:
    - removing the shared in-flight check (`if (inFlight) return inFlight`);
    - removing both `!running` guards in `scheduleNext` and `checkAndContinue`;
    - removing `!inFlight` from `recordOutcome`. This last one is close to an equivalent mutant, because `scheduleNext` clears first.
  - The "none after stop()" assertion never stops while a ping is in flight.
  - Add two tests: two concurrent `forceCheck()` calls cause one ping; `stop()` during an in-flight ping leaves zero pings afterwards.
- [LOW] web/components/offline/OfflineBoard.tsx:84-88 — while the board is open, its 5 s `forceCheck` resets the monitor's timer through
  `recordOutcome`. The ADR-FE-142 back-off (15 s after one minute, 60 s after five) therefore never applies on the board page; the rate
  is about one ping every 5 s. This is acceptable, but Amended (24)/(25) and ADR-FE-142 should say that the board page is an exception.
- [LOW] web/components/offline/OfflineDeviceSettings.tsx:69,118 — the opt-in text still says the copy contains customers' "names and
  phone numbers" and lists "customer name and phone". That has been inaccurate since the owner's "Remove them".
  - Also: a record written before this build still holds `customerPhone` until its next sync or until `validUntil`. The phone is not
    shown, and the schema version is unchanged.
  - Fix: reword the text, and/or run `withoutCustomerPhones` over stored records on read.
- [LOW] web/lib/offline/device.ts:105-108 — `isBoardLocked` treats a `lastActivityAt` in the future as recent. Winding the OS clock back
  therefore unlocks the board, and keeps an expired copy valid. That needs OS clock rights, which a DevTools-level attacker has anyway.
  Fix: treat a future `lastActivityAt` as locked.
- [LOW] docs/backlog/XCT.md row 61 — the HO-CAT-002 §12.1 ratification still lists "the headers and the reporter". It should also name
  `<ServiceWorkerCleanup />` in `app/layout.tsx`, because §13 limits that file to the reporter and next/font. The edit itself is
  recorded in Amended (22) and row 62.

VERIFIED
1. SECURITY HIGH (the original three bypasses):
   - A fresh load while online shows "Checking the connection" and no data, then `replace`. A 5xx counts as an answer: `server-error`
     is not `unreachable`, so the board leaves, and a test covers it.
   - A fresh load offline with no `lastActivityAt`, or one older than `idleTimeoutMinutes`, opens locked.
   - A reload while locked stays locked, because `lockedForStaffAccountId` is persisted. The window between the idle timer firing and
     the lock being written is covered, because a reload in that window is idle-stale anyway.
   - The 5 s loop is correct: it shares the in-flight ping, is cancelled and cleared on unmount, and only `unreachable` keeps the board.
   - Another person's copy on a cold load: there is still no `knownStaffId`. The copy can only be shown within the idle window of the
     person who saved it, and the next sign-in by anyone else wipes it. That is equivalent to an unlocked session left open, apart from
     the HIGH above.
   - DevTools ruling: the lock is a walk-up deterrent only. Anyone with DevTools, or with file access to the profile, can read
     IndexedDB, or set `lockedForStaffAccountId: null` and a fresh `lastActivityAt`. The lock cannot protect against that. What bounds
     the exposure is the wipe triggers and `validUntil`. Nothing in the `device` store is trusted by the server.
2. CODE_REVIEW HIGH: `seenRecovery = useRef(recoveredAt)`. Mutating it back to `useRef(null)` fails the second-outage test.
3. ARCH H1: `scheduleNext()` always clears the handle first. Mutating out `clearTimer()` fails two ping-count tests. Correctness of the
   in-flight sharing and of the stop guards was checked by reading the code (see the LOW on tests).
4. ARCH H2:
   - `drillOutput: pasted`, with no fallback.
   - Submitting is refused before validation when the paste is blank, with a visible reason.
   - The original text, whitespace included, is sent even after fields are edited.
   - `hasUtcOffset` refuses instants with no offset in both the form and the parser. All four cases have tests.
5. Phones: `withoutCustomerPhones` runs inside `writeSnapshot` before `dbPut`, so the sync and direct writes are both covered. The `tel:`
   link and the "No phone on file" text are gone. The worker caches only the static shell and assets, never a snapshot body. Only the
   LOW wording above remains.
6. MEDIUM and LOW fixes:
   - `sw.js` falls back only on a rejected fetch. Eviction runs only after the shell fetch has succeeded.
   - The cross-tab guard uses 80 %, applies to the same person only, and always fetches on the first sync of a page load.
   - `touchSnapshot` is one readwrite transaction and ETag-matched.
   - `onclose` and `onversionchange` drop the cached connection.
   - Web-vitals uses the route at load time for load metrics and the current route for INP. Matching by position from the right is
     correct for the measured routes.
   - `ConnectivityProvider` restores the defaults and the `onlineManager` on unmount.
   - `AbortError` is no longer reported.
   - `ServiceWorkerCleanup` acts only when a worker may not exist (development or mock mode) and unregisters only the `/staff/` scope.
     In production nothing changes. It is out of the literal §13 line but recorded (Amended 22, row 62), and the precedent cures it.
7. Scope: the handover edit is append-only. The old cell is an exact prefix of the new one. Amended (22)-(27) match the code, apart from
   the two LOW wording points above.
   - Backlog rows 1-90 are one contiguous numbering with no duplicates.
   - Rows 61-90 are honest about what is resolved and what is open, with two caveats. Row 88's "person binding" is met by the lock
     rather than by an identity check. Row 88 is not fully resolved while the HIGH above stands.
8. No other area's behaviour changed:
   - `client.ts`'s abort rule is inert without the staff reporter.
   - The root-layout clean-up does nothing in production.
   - StaffSessionGuard, IdleLock and the front-desk files are untouched.

SCOPE CHECK
- files in allowed scope: 38/39 (accepted under precedent: the append-only HO-XCT-002 §1 Amended, and the docs/backlog and test-case
  docs)
- files outside allowed scope: the `<ServiceWorkerCleanup />` line in `web/app/layout.tsx`, beyond the §13 "reporter and next/font
  only". It is recorded in Amended (22) and row 62, and the HO-CAT-002 ratification is owed in row 61 (LOW above). No §14 file was
  touched.


---

## Re-check 2 — 2026-10-14 (combined, fix round 2)

GATE: ARCHITECTURE (combined re-check covering SECURITY and CODE_REVIEW findings)
HANDOVER: HO-XCT-002
COMMITS: a9956a12 (range 1c8ee5be..a9956a12, branch ho/XCT-002, 12 files)
DATE: 2026-10-14
STATUS: PASS

The HIGH from the combined re-check is closed. No BLOCKER or HIGH remains. The MEDIUM and LOW findings below are test-quality and
recording points, and they go to the backlog.

Evidence:
- `vitest run tests/components/{offline,operations,retention,observability}`: 11 files and 126 tests, all passing.
- Seven mutations were run against `tests/components/offline`. Each was applied, tested and restored, and the tree is clean:

| Mutation | Caught? |
|---|---|
| G1: remove `&& !locked` from `recording` | NOT caught (78/78 pass) |
| G2: remove the `lockOfflineBoard` call when the online lock fires | caught (2 fail) |
| G3: a sync never lifts another person's lock | caught |
| G4: remove the future-`lastActivityAt` check in `isBoardLocked` | NOT caught |
| G5: remove the shared in-flight check | caught (the new concurrent test) |
| G6: remove both `!running` guards | NOT caught |
| G7: no clean-up of stored records on read | caught |

Bypass retried (fire the online lock, move the mouse or press a key, cut the network, open the board): **closed.**
- When `locked` turns true, `OfflineSnapshotSync` calls `lockOfflineBoard(staffAccountId)`. If the device settings have not loaded yet,
  the call is re-run when they load. The write is local to IndexedDB, so it also works when the lock fires while offline.
- The board then opens locked whatever `lastActivityAt` says. A reload while online-locked keeps the saved lock.
- The activity gate (`!locked`) is correct code and a second layer of defence. G2 is the control that closes the bypass on its own, and
  the tests catch it.
- Residual, acceptable: a sync that answers 200 while the lock dialog shows lifts the board lock. That needs a live session, for example
  one kept alive by the same person's activity in another tab, so it is not a bypass.

FINDINGS
- [MEDIUM] web/tests/components/offline/online-lock.test.tsx:84-90 — the "no activity while locked" assertion cannot fail.
  - Why: `startActivityRecording` throttles writes to one a minute, and it already wrote on mount. The events after `lock()` fall inside
    that minute and are throttled whether or not the gate exists (mutation G1 survives).
  - Fix: advance fake timers (or `Date.now`) by more than 60 s before dispatching the events, or inject a 0 ms interval. Then assert that
    `lastActivityAt` is unchanged.
- [LOW] web/tests/components/offline/connectivity.test.tsx ("stop() while a ping is in flight") — fake timers are installed only after
  the ping completes.
  - Why it matters: an orphan `setTimeout` scheduled after `stop()` is a real-timer handle, which `advanceTimersByTimeAsync` never fires.
    So G6 survives.
  - Fix: call `vi.useFakeTimers({ shouldAdvanceTime: true })` before `startMonitor()`.
  - The code itself is correct.
- [LOW] web/tests/components/offline/board-security.test.tsx ("a failed sync leaves the lock in place") — the assertion
  `not.toBeUndefined()` also passes for `null`.
  - Also, a 403 runs `disableOfflineCopy`, which resets the record to its defaults, so the test proves nothing.
  - Fix: use a network failure (`snapshotScenario` offline) and `toBe(staffAccountId)`.
- [LOW] web/tests/components/offline/board-security.test.tsx ("future lastActivityAt") — the test passes through `idleElapsedMs`, which
  returns `Infinity` and makes the idle timer fire at once. It does not pass through `isBoardLocked`, so G4 survives.
  - The behaviour holds through two paths.
  - Fix: add a unit assertion on `isBoardLocked` with a future instant.
- [LOW] docs/handover/HO-XCT-002.md:18 — this round edits Amended (24) and (25) in place instead of appending.
  - What changed: (24) gains the 5 s exception. In (25), "only a sync for the same person" becomes "a sync by anyone", and the lock and
    clock text is added. A new (28) is appended.
  - The old cell is no longer a prefix of the new one. The text is accurate and these are the builder's own unmerged rows from today, but
    the append-only convention (ADM/APT/FIL/POR/SHP-002 precedent) prefers a correcting item.
  - The integrator should accept it as is or restate it as an appended (29) at merge.

VERIFIED
- HIGH: the gate on `!locked` and the lock call are both present. The lock context is only read; IdleLock and StaffSessionGuard are not in
  the diff.
- MEDIUM, any sync lifts the lock: `clearOfflineBoardLock(now)` clears any non-null lock after a 200 written, or after a 304. A sync that
  fails or is skipped leaves the lock alone.
- Pings: the concurrent `forceCheck` test catches G5. The 5 s board exception is documented in Amended (24), ADR-FE-142 and row 94.
- Phones: the opt-in text is reworded. `cleaned()` strips `customerPhone` on read, both in memory and once in the store, inside one
  readwrite transaction.
- Clock: a `lastActivityAt` more than 60 s in the future, or unparsable, counts as no activity in both `isBoardLocked` and
  `idleElapsedMs`.
- Row 61 now names `<ServiceWorkerCleanup />`. Backlog rows 1-97 are one contiguous numbering with no duplicates, and rows 91-97 are
  accurate apart from the test caveats above.
- StaffLockProvider wrapper in `renderWithClient`:
  - It is inert unless `lock()` is called: no effect on mount, and it restores the managers on unmount.
  - Only XCT's own test files import the helper: offline/*, operations, retention.
  - The only production consumer added, `OfflineSnapshotSync`, is mounted only in `app/(staff)/layout.tsx`, under the real provider. So
    the wrapper cannot hide a missing-provider throw in production, and no other area's tests use it.
- No other area's behaviour changed.

SCOPE CHECK
- files in allowed scope: 12/12. This counts the HO-XCT-002 Amended cell, with the in-place edit noted as LOW above, and the backlog
  and ADR docs.
- files outside allowed scope: none new. No §14 file was touched.
