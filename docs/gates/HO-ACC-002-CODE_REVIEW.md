GATE: CODE_REVIEW
HANDOVER: HO-ACC-002
COMMITS: d14d591 ddbae6d aae12d4 (delta ee5e907..aae12d4; F1 idle-lock sign-out race fix, narrow
seventh pass per instruction — TEST gate F1 fix only)
DATE: 2026-09-27
STATUS: PASS

## Eighth pass (F1/seventh-pass HIGH close-out, 6ce5f02..5b01d22)

Scope: `13d46ae` (`IdleLock.tsx`'s `resolveBystanderSessionInvalid` — explicit `staleTime: 0` on the
authoritative `fetchQuery` call; `next` captured from `window.location.pathname` before the `await`,
not after; both doc comments corrected), `410b93a` (one new `idle-lock.test.tsx` regression case),
`5b01d22` (HO-ACC-002.md correction row on the seventh-pass F1 Amended entry). `tsc --noEmit`,
`eslint . --max-warnings 0`, `prettier --check .` all clean; `vitest run` — 60 files / 239 tests, all
pass (up from 238 at the seventh pass — the one new case).

### 1. Is `staleTime: 0` correct against the installed `@tanstack/query-core` 5.103.2?

Read the actual installed source, not only the comment's claims.

- `QueryClient.fetchQuery` (`build/modern/queryClient.js:379-384`):
  `query.isStaleByTime(resolveQueryValue(defaultedOptions.staleTime, query)) ? query.fetch(defaultedOptions) : Promise.resolve(query.state.data)`.
- `Query.isStaleByTime(staleTime = 0)` (`query.js:214-219`): `true` if `state.data === undefined`, or
  `staleTime === 'static'` → `false`, or `state.isInvalidated` → `true`, otherwise
  `!timeUntilStale(state.dataUpdatedAt, staleTime)`.
- `timeUntilStale(updatedAt, staleTime)` (`utils.js:14-16`): `Math.max(updatedAt + (staleTime || 0) -
  Date.now(), 0)`. With `staleTime: 0`, this is `Math.max(updatedAt - Date.now(), 0)`, which for any
  `updatedAt` in the past is `0` — a falsy value — so `isStaleByTime` returns `!0 → true`
  **unconditionally**, regardless of how recently the cached data was written. This is exactly what
  the fix needs: the previous bug was the global `staleTime: 30_000` default (`app/providers.tsx:29`)
  making `isStaleByTime` return `false` for up to 30s after a successful read, routing `fetchQuery`
  into the `Promise.resolve(query.state.data)` branch — no network call at all. `staleTime: 0` closes
  that branch completely for this call site. Confirmed correct.

**Dedupe still holds.** `Query.fetch()` (`query.js:297-304`) checks
`this.state.fetchStatus !== "idle" && this.#retryer?.status() !== "rejected"` and returns the existing
retryer's promise (`this.#retryer.continueRetry(); return this.#retryer.promise;`) — this check runs
*inside* `fetch()`, before any option is consulted, and is reached identically whether `fetch()` was
entered because `isStaleByTime` was true due to elapsed time or, as now, unconditionally due to
`staleTime: 0`. Traced further: when a call is *not* deduped (fetch genuinely starts), `fetch()` also
calls `this.setOptions(options)` (`query.js:305`), which — per `Query.setOptions`
(`query.js:67-71`) — fully replaces `this.options` (merged with `#defaultOptions`) with whatever was
passed to this ad hoc `fetchQuery` call, including `staleTime: 0`. This means a genuine (non-deduped)
authoritative check briefly overwrites the *shared* `STAFF_SESSION_QUERY_KEY` query's persisted
`options.staleTime` from `useStaffSession()`'s own `10_000` (`useSession.ts:45`) down to `0`. This is
not new to this pass (the seventh-pass code already did this, just with the global `30_000` default
instead of `0`), and it self-heals essentially immediately: `useBaseQuery`'s own effect
(`useBaseQuery.js:35-37`, `observer.setOptions(defaultedOptions)`, deps `[defaultedOptions, observer]`)
re-forwards `useStaffSession()`'s own options into the same `Query.setOptions` on every render of any
component still calling that hook, and `resolveBystanderSessionInvalid` itself always triggers a
re-render shortly after (`setStatus`, `lockNow`, or `queryClient.clear()`) — so the corrupted value
lives for, at most, a fraction of a render cycle in practice. **LOW, not blocking**: worth a one-line
note if this pattern is copied elsewhere, since a component that doesn't re-render promptly after an
ad hoc `fetchQuery`/`fetch()` call on a shared key would carry a wrong `staleTime` for longer.

**No flood risk from several concurrent bystanders.** Re-derived the seventh pass's own "reviewed, not
a finding" note under the new code: each bystander calls `queryClient.fetchQuery` independently;
`QueryCache.build()` (`queryCache.js:46-61`) returns the *same* `Query` instance for the same
`queryHash` without touching its options when the query already exists, so every caller operates on
one shared query object. The dedupe branch above depends only on `fetchStatus`, which is set to
`'fetching'` synchronously (`query.js:354-357`, before any `await` in `fetch()`) and stays `'fetching'`
for the entire real network round trip — so any bystander call arriving after the first, whether in the
same tick or a later one, sees `fetchStatus !== 'idle'` and reuses the one in-flight promise. Only one
real HTTP request is ever made per authoritative-check episode, no matter how many bystanders fire.
Not a regression, and not a new risk.

### 2. Does the regression test (`410b93a`) prove what it claims?

Traced the new case by hand against both the pre-fix and post-fix code.

- Setup: a fresh `QueryClient({ defaultOptions: { queries: { staleTime: 30_000, retry: false } } })`
  (matching `app/providers.tsx`'s real default), pre-seeded via `setQueryData(STAFF_SESSION_QUERY_KEY,
  statusBody())` — so `dataUpdatedAt` is "now," well inside the 30s window, and the query is not
  in-flight (no observer has fetched it yet at seed time).
- `StaffSessionGuard`'s own `useStaffSession()` mount sees fresh, non-stale cached data (staleTime
  30_000 > 0 elapsed) and does **not** refetch on mount — so `GET /api/staff/session` (mocked to return
  `session_idle_timeout`) is never called by the session hook itself, isolating the authoritative check
  as the only path that can reach it.
- A direct `apiFetch('/api/some-unrelated-staff-endpoint')` (bypassing React Query, a genuine bystander)
  returns `401 session_invalid`, invoking the registered handler → `resolveBystanderSessionInvalid`.
- **Without `staleTime: 0`**: `fetchQuery` inherits the client's `30_000` default;
  `isStaleByTime(30_000)` is `false` (data is ~0ms old); `fetchQuery` resolves immediately to the
  seeded `statusBody()` with **no network call**; `resolveBystanderSessionInvalid` takes the
  `if (authoritative) { setStatus(authoritative); return; }` branch — no lock, no dialog. The test's
  `await screen.findByRole('dialog')` would time out and fail.
- **With `staleTime: 0`**: `isStaleByTime` is unconditionally `true`; `fetchQuery` calls `query.fetch()`,
  which hits the mocked `GET /api/staff/session` and receives `session_idle_timeout`;
  `getStaffSessionStatus` throws `StaffSessionIdleTimeoutError`; the `catch` branch runs
  `setIdleTimeoutMinutesFromError(20)` + `lockNow()`; the dialog renders with "signed out after 20
  minutes."
- Confirmed by hand-reasoning through the library source (not only trusting the commit message) that
  the test **would fail without the fix and passes with it** — it is a genuine regression test for the
  seventh-pass HIGH, not a tautology. The commit message's own "confirmed failing/passing" claim is
  accurate.
- Draft-preservation and `replace` (`expect(replace).not.toHaveBeenCalled()`) assertions in the same
  test are consistent with a lock-in-place outcome, not a redirect — correct expectations for this
  scenario.

### 3. Anything else new?

- `git diff --stat 6ce5f02..5b01d22` — exactly the three files named in the task
  (`docs/handover/HO-ACC-002.md`, `web/components/staff/IdleLock.tsx`,
  `web/tests/components/staff/idle-lock.test.tsx`), 79 insertions / 3 deletions. No file outside the
  three named commits was touched.
- `IdleLock.tsx`'s `next`-capture reorder (the seventh-pass LOW) is fixed as described: `const next =
  ...` now runs once, before the `try`, on every entry to `resolveBystanderSessionInvalid`, used
  identically in both the `if (authoritative)`-else path and the `catch` block's non-idle-timeout path.
  No test added specifically for this LOW (a losing-bystander redirect racing a winning one is timing-
  dependent and awkward to assert deterministically); acceptable given its own severity, per the
  seventh pass's own finding. Still LOW, not blocking.
- `docs/handover/HO-ACC-002.md`'s new correction row (`5b01d22`) was read in full against the actual
  shipped code: it accurately describes the original claim's scope error, the `staleTime: 0` fix, the
  dedupe-is-unaffected reasoning, the new regression test, the `next`-capture fix, and links back to
  this file's own "Seventh pass" section. No drift found between the doc's description and the code.

### Static checks (`web/`)

- `npx tsc --noEmit` — clean.
- `npx eslint . --max-warnings 0` — clean, no output.
- `npx prettier --check .` — "All matched files use Prettier code style!".
- `npx vitest run` — 60 files, **239/239 tests passed** (up from 238 at the seventh pass: the one new
  regression case). No Playwright run, no servers started, per the caller's brief.

### Verdict

**PASS.** The seventh-pass HIGH is closed correctly and at its root: `staleTime: 0` is verified by
reading the installed `@tanstack/query-core` 5.103.2 source line by line, not only by trusting the
fix's own comment, and it does not weaken the pre-existing in-flight dedupe (a separate code path,
unaffected by `staleTime`) or introduce a request-flooding risk under several concurrent bystanders
(re-derived independently of the seventh pass's own note, which still holds under the new code). The
new regression test is genuine — traced by hand to fail against the pre-fix code and pass against the
post-fix code, for the reason it claims. The `next`-capture LOW is also fixed. One new LOW is recorded
below, not blocking: a brief (sub-render-cycle) window where a genuine (non-deduped) authoritative
check overwrites the shared session query's persisted `staleTime` down to `0` before `useStaffSession`
's own next render restores it to `10_000` — pre-existing in kind since the seventh pass (which did
the same with `30_000` instead), self-healing, and not observed to cause any incorrect behaviour in
this codebase today.

FINDINGS (this pass only)
- [LOW] `web/components/staff/IdleLock.tsx:147-151` (`resolveBystanderSessionInvalid`) — a genuine
  (non-deduped) call to `queryClient.fetchQuery({ queryKey: STAFF_SESSION_QUERY_KEY, ..., staleTime:
  0 })` reaches `Query.fetch()`'s `this.setOptions(options)` (`query-core/query.js:305`), which
  overwrites the *shared* `STAFF_SESSION_QUERY_KEY` query's persisted `options.staleTime` from
  `useStaffSession()`'s own `10_000` down to `0` until that hook's own observer next re-renders and
  restores it (`useBaseQuery.js:35-37`, on every render). Self-healing in this codebase today (the
  authoritative check itself always triggers a re-render shortly after), and not new to this pass (the
  seventh-pass code had the same property with `30_000`), but worth a one-line doc-comment note if this
  ad hoc `fetchQuery`-on-a-shared-key pattern is ever reused somewhere less render-active. Not required
  to fix before merge.

SCOPE CHECK
- Files in scope for this pass: `web/components/staff/IdleLock.tsx`,
  `web/tests/components/staff/idle-lock.test.tsx`, `docs/handover/HO-ACC-002.md` — 3/3, matches the
  three commits named in the task (`13d46ae`, `410b93a`, `5b01d22`).
- Nothing outside the `6ce5f02..5b01d22` range was reviewed or touched.

---


## Seventh pass (F1 idle-lock sign-out race fix, ee5e907..aae12d4)

Scope: `d14d591` (`IdleLock.tsx`'s `resolveBystanderSessionInvalid`, routing a non-idle 401 through
`queryClient.fetchQuery(STAFF_SESSION_QUERY_KEY, getStaffSessionStatus)` instead of acting on the
bystander's own response), `ddbae6d` (two new `idle-lock.test.tsx` cases), `aae12d4` (HO-ACC-002.md
Amended row). `tsc --noEmit`, `eslint . --max-warnings 0`, `prettier --check .` all clean;
`vitest run` — 60 files / 238 tests, all pass.

### HIGH — `resolveBystanderSessionInvalid`'s "authoritative" check can silently skip the network
entirely and trust up-to-30-second-stale cached data, missing a genuinely ended session

`web/components/staff/IdleLock.tsx:127-154`. The fix's own comment (lines 114-126) and the
handover's Amended row both claim `queryClient.fetchQuery` "dedupes onto the session query's own
in-flight fetch when one is racing the bystander" and "performs no extra network round trip and
resolves to the one true outcome." That is only true in the branch where TanStack decides a fetch
is actually needed. Traced against the installed `@tanstack/query-core` 5.103.2
(`build/modern/queryClient.js:379-384`):

    fetchQuery(options) {
      const defaultedOptions = this.defaultQueryOptions(options);
      if (defaultedOptions.retry === void 0) defaultedOptions.retry = false;
      const query = this.#queryCache.build(this, defaultedOptions);
      return query.isStaleByTime(resolveQueryValue(defaultedOptions.staleTime, query))
        ? query.fetch(defaultedOptions)
        : Promise.resolve(query.state.data);
    }

`query.fetch()` — where the dedupe-onto-an-in-flight-retryer behaviour the comment describes
actually lives (`query.js:297-302`) — is only reached when `isStaleByTime()` says the cached value
is stale. `isStaleByTime` (`query.js:214-218`) is `true` if `state.data === undefined`, or
`state.isInvalidated`, or the elapsed time since `dataUpdatedAt` exceeds the **caller's own**
`staleTime` argument. The call at `IdleLock.tsx:129-132` passes no `staleTime`, so
`defaultQueryOptions` (`queryClient.js:597-604`) resolves it from the **QueryClient's global
default** — `staleTime: 30_000` in production (`web/app/providers.tsx:29`) — not from
`useStaffSession()`'s own per-hook `staleTime: 10_000` (`web/lib/hooks/useSession.ts:45`), which is
never consulted here at all.

Consequence: once the session query has ever completed one successful fetch (true for the entire
rest of a staff session, since an error never clears prior `data`), if that success was **less than
30 seconds ago** — plausible any time a staff member's tab has recently regained focus (the session
query itself refetches on focus once its own 10s staleTime has elapsed) or right after initial
mount — a bystander `session_invalid` landing in that window causes `fetchQuery` to skip the
network entirely and resolve immediately to the **old, cached "still signed in" data**, not
whatever the real, current server state is. `resolveBystanderSessionInvalid` then takes
`if (authoritative) { setStatus(authoritative); return; }` and does nothing: no lock, no sign-out —
even if the session was genuinely ended (idle timeout on this same event, or revoked by an admin,
or any other real sign-out) at that exact moment. This is not the case the comment does address
("the effect below wins and `lockedRef.current` is already true") — it is a distinct path where
**no authoritative check happens at all**, silently defeating the very control this fix exists to
add. The gap self-heals only once the cached value's age exceeds 30s on some later
`session_invalid`.

Not caught by any test in this diff, for two independent reasons:
- Every `QueryClient` constructed in `web/tests/components/staff/idle-lock.test.tsx` (lines 22, 237,
  283, 417, 576, 630 — including both new F1 tests) uses
  `new QueryClient({ defaultOptions: { queries: { retry: false } } })`, with no `staleTime`
  override. An unset `staleTime` resolves to the parameter default `0` inside `isStaleByTime`, which
  makes it always evaluate stale — the exact branch that does dedupe correctly. None of these tests
  can ever observe the `staleTime: 30_000` production behaviour, because none of them use it.
- The one E2E case that does run against the real `Providers` QueryClient (`staff-auth.spec.ts`'s
  E-G2, and the parallel staff-account-detail case above it) triggers the race via
  `window.__invalidateQueries`, i.e. `queryClient.invalidateQueries(...)`, which sets
  `state.isInvalidated = true` on the query directly. `isStaleByTime` treats `isInvalidated` as
  stale unconditionally (`query.js:217`), regardless of the 30s default — so this shortcut, useful
  for making the *other* race deterministic, happens to route around this exact gap. A genuine
  `visibilitychange`/window-focus event (the real trigger, and what E-G3 uses to test the pause
  mechanism itself) does not call `invalidateQueries` and would not set that flag.

Fix: pass `staleTime: 0` explicitly in the `fetchQuery` call (`IdleLock.tsx:129-132`) so this check
is never at the mercy of whatever the client's global default happens to be — matching the
"authoritative, serialized check" the surrounding comment already claims to perform. Add a
regression test using a `QueryClient` configured with the same `staleTime: 30_000` global default
as `app/providers.tsx`, with `STAFF_SESSION_QUERY_KEY` pre-seeded with fresh (<30s old) success
data, proving a subsequent genuine `session_invalid` is still caught rather than silently
swallowed. The `IdleLock.tsx:114-126` comment and the HO-ACC-002.md Amended row (2026-09-27, TEST
gate F1) should also be corrected once fixed — both currently state the dedupe as an unqualified
guarantee.

### Reviewed, not a finding

- **Pause-mechanism interaction (`IdleLockContext.tsx`).** `resolveBystanderSessionInvalid` only
  runs when `!lockedRef.current` (`IdleLock.tsx:181`), i.e. before `focusManager`/`onlineManager`
  are ever paused for this event — the fetch cannot get stuck behind the lock's own pause. A 401
  having just arrived implies the browser was online enough to get a server response, so the
  "paused while genuinely offline" deadlock class (`networkMode: 'online'` default on this ad hoc
  call) is not realistically reachable here the way it was for the re-sign-in mutation.
- **Several concurrent bystanders.** Each independently calls `resolveBystanderSessionInvalid`;
  `queryClient.fetchQuery`/`query.fetch()`'s own dedupe means only one real request is made once
  `isStaleByTime` is true, and every awaiter resolves off the same promise. Multiple callers
  reaching `queryClient.clear()`/`router.replace()` afterwards is redundant but harmless — both are
  idempotent, and Next's router does not error on a repeated `replace` to the same target. Not a
  finding.
- **Error-state session query vs. this handler double-locking.** Both `lockNow()` call sites are
  idempotent by the design already recorded in `IdleLockContext.tsx`'s own doc comment (`lock()`
  guarded by `pausedRef`). No double-lock risk.
- **`window.__apiFetch` (`app/providers.tsx:83-87`).** Gated `NODE_ENV !== 'production'`,
  identically to the `__apiBaseUrl`/`__invalidateQueries` hooks immediately above it. Correct,
  matches the existing pattern.
- **Unmount race.** `resolveBystanderSessionInvalid`'s `await` widens (slightly) the pre-existing
  window in which `IdleLock` could unmount before a `router.replace`/`setStatus` call lands — the
  original handler had the same property (no unmount guard) synchronously. Not a regression this
  fix introduces on its own; LOW, not blocking.

### LOW

- A losing bystander that reads `window.location.pathname` after a winning bystander has already
  called `router.replace('/staff/sign-in?next=...')` computes `next` against the already-navigated
  URL, producing a harmless but odd `?next=%2Fstaff%2Fsign-in` on the redundant second call
  (`IdleLock.tsx:141,151`). Cosmetic only.

FINDINGS
- [HIGH] `web/components/staff/IdleLock.tsx:127-154` (`resolveBystanderSessionInvalid`) —
  `queryClient.fetchQuery(STAFF_SESSION_QUERY_KEY, getStaffSessionStatus)` is called with no
  `staleTime`, so it inherits the QueryClient's global default (`30_000` in production,
  `web/app/providers.tsx:29`) rather than performing a genuine check. When the session query's
  cached data is less than 30s old and not `isInvalidated`, this returns stale cached "still signed
  in" data with **no network request at all**, silently failing to detect a genuinely ended session
  on that bystander event — the opposite failure mode from F1, and unqualified by the surrounding
  comment's "authoritative, serialized check" claim. Not exercised by any test in this diff (see
  above for why both the unit tests and the one relevant E2E case each happen to route around it).
  Fix: pass `staleTime: 0` explicitly.
- [LOW] `web/components/staff/IdleLock.tsx:141,151` — a losing concurrent bystander's redundant
  redirect can carry a self-referential `?next=` once the winner has already navigated. Cosmetic.

SCOPE CHECK
- Files in scope for this pass: `web/components/staff/IdleLock.tsx`,
  `web/tests/components/staff/idle-lock.test.tsx`, `docs/handover/HO-ACC-002.md` — 3/3, matches the
  three commits named in the task.
- Nothing outside the F1 fix range (`ee5e907..aae12d4`) was reviewed or touched.

---

# Sixth pass (combined CODE_REVIEW/SECURITY delta, 061aed3..9e8d33a)

Line-by-line review of six commits: `7eea158` (offline-at-lock deadlock fix, `networkMode: 'always'`
on `useSignInStaff()` + `restoreManagers()` resync), `a0771b5` (focus-pause unit test made real,
plus real-browser E-G3), `b20e01f` (`run-real-api-e2e.mjs` spawn hardening + HO-ACC-002 Sec1 Amended
records), `8b884e4` (`UnsavedChangesGuard` moved off `history.state` marking onto
`sentinelActiveRef`), `f9c326f` (bracketed `[::1]` test case), `9e8d33a` (handover-doc naming fix,
no code). These close, respectively: the ARCHITECTURE fifth-pass MEDIUM (`onlineManager` state
freeze deadlock), the ARCHITECTURE/CODE_REVIEW fifth-pass LOW (vacuous focus-pause test) and its own
CODE_REVIEW fifth-pass LOW (E-G2 not exercising the pause mechanism), the ARCHITECTURE fifth-pass
MEDIUM (unrecorded `run-real-api-e2e.mjs` scope) and its own CODE_REVIEW note (`shell: true`
argument-injection risk), the CODE_REVIEW fifth-pass MEDIUM (`UnsavedChangesGuard`'s marker
strippable by Next's router), the CODE_REVIEW fifth-pass LOW suggestion (bracketed IPv6 test), and
the ARCHITECTURE fifth-pass LOW (naming two files in a handover row). All six are verified closed
below.

## `7eea158` — offline-at-lock deadlock: `networkMode: 'always'` + `restoreManagers()`

- **Root cause correctly diagnosed and fixed.** `onlineManager` never reads `navigator.onLine`
  itself, only a real `online`/`offline` event — freezing its listener while locked also freezes its
  *reported* value at whatever it was the instant `lock()` ran. If already offline at that instant,
  `isOnline()` stays `false` for the whole lock, and `useSignInStaff()`'s default `networkMode:
  'online'` would then refuse to even start the one mutation that can end the lock — a genuine
  deadlock, reachable without any test manipulation (Wi-Fi drop, lid close). `networkMode: 'always'`
  exempts exactly this one mutation from `onlineManager`'s reported state, matching the doc comment's
  own claim of running "exactly as it would with no lock in play." `restoreManagers()` additionally
  resyncs `onlineManager.setOnline(navigator.onLine)` on both `unlock()` and the unmount-while-locked
  cleanup, correcting the frozen value in either direction (stuck `false` after a real reconnect, or
  stuck `true` after a real disconnect) the instant the real listener is reattached, not only on the
  next real event.
- **"Fails fast with a clear message rather than hang" when truly offline: yes, via pre-existing,
  unmodified code.** `networkMode: 'always'` still requires an actual network round-trip — a
  genuinely offline attempt now reaches the browser's own `fetch`, which rejects with a network
  `TypeError`. Traced the display path: `IdleLockDialogContent.tsx:65` passes `reSignInMutation.error`
  straight to `SignInForm`, whose existing branch (`SignInForm.tsx:100-105`, untouched by this round)
  renders "Something went wrong. Please try again." for any error that is neither
  `ApiRateLimitedError` nor `ApiUnauthorizedError` — a raw network error falls into that branch. The
  message is generic (indistinguishable from a 500) rather than "you appear to be offline," but it is
  immediate, visible, and not a silent hang — acceptable, and out of this round's own scope to
  improve (pre-existing `SignInForm` behaviour, not touched here).
- **New test is real, not a tautology.** `idle-lock.test.tsx`'s new case forces `onlineManager` to
  `false` *before* locking (so the real listener detachment genuinely can never correct it), locks via
  a real 401, then completes the re-sign-in through the actual `SignInForm`/`apiFetch` stack while
  `onlineManager.isOnline()` is still observably `false`. Without `networkMode: 'always'`, TanStack's
  default `networkMode: 'online'` would pause the mutation indefinitely and `await waitFor(() =>
  ...not.toBeInTheDocument())` would time out — verified this reasoning against the library's own
  documented `networkMode` semantics, not only the test's own comment. The final
  `expect(onlineManager.isOnline()).toBe(true)` genuinely exercises the resync, since nothing else in
  the test ever calls `setOnline(true)`.
- No regression to the deliberate design goal: `focusManager`/`onlineManager` stay paused as a pair
  for every *other* query while locked (only the one exempted mutation can reach the network), so the
  HIGH this mechanism exists to fix (a stale draft being clobbered by a focus/reconnect refetch while
  locked) is untouched by this change.

## `a0771b5` — focus-pause unit test made real; real-browser E-G3

- **The unit-test fix is correct and independently verifiable.** `defaultFocusEventSetup`
  (`IdleLockContext.tsx`) attaches to `window`; the old `fireVisibilityChange` dispatched a
  non-bubbling `Event` on `document` — a different `EventTarget`, so the listener never received it,
  locked or not. Confirmed by reading `@tanstack/query-core`'s own `focusManager.js` default setup
  (`window.addEventListener(...)`) directly, not only trusting the commit message. Dispatching on
  `window` now reaches the real listener.
- **The two new "unlocked control" tests are the right fix for the "would each fail without the
  pause?" question, and they do answer it.** Before this commit, "stays at 1 while locked" could not
  be distinguished from "the dispatch never reached anything, locked or not" — both produce identical
  output. The new unlocked-control tests (`fetchCount` goes from 1 to 2 on the identical dispatch,
  never locked) prove the dispatch technique itself causes a real refetch when nothing is paused,
  which is exactly what was missing to make the locked assertion meaningful. Traced by hand: revert
  `a0771b5`'s `window.dispatchEvent` fix back to `document.dispatchEvent` (the pre-fix state) and both
  the locked assertion *and* the new unlocked control would now read `fetchCount === 1` — the control
  would itself fail, correctly flagging the regression, which is the property a meaningful control
  needs.
- **E-G3 (`staff-auth.spec.ts`) is a genuine gap-closer for the fifth pass's own LOW.** It dispatches
  a real `visibilitychange` on `window` in an actual browser, after the lock has engaged, and asserts
  no `ErrorState` appears even with the cat query's next `GET` armed to 401 — i.e., no refetch was
  even attempted, not merely "a refetch happened but was caught." This is a different, stronger claim
  than E-G2's (which proves the `isError && !data` fallback survives a *forced* refetch via
  `__invalidateQueries`, bypassing `focusManager` entirely). Read the full test: the deterministic lock
  trigger (`__forceStaffIdleExpired` + `__invalidateQueries(['session','staff'])`) is isolated from the
  one non-deterministic step (the real `visibilitychange` dispatch), so a failure would point at the
  pause mechanism specifically, not at lock-entry machinery. The `page.waitForTimeout(500)` used to
  let a wrongly-triggered refetch settle is an inherent limitation of proving a negative in
  Playwright — not a flakiness risk in the failing direction (a regression would still produce a
  stable, visible `ErrorState` well within 500ms), only a (bounded, acceptable) risk of the test
  passing a hair later than strictly necessary. Not a finding.

## `b20e01f` — `run-real-api-e2e.mjs` spawn hardening + scope records

- **The spawn change is sound.** Resolves `@playwright/test/package.json`'s own declared
  `bin.playwright` via `createRequire(import.meta.url)` (works from this ESM script) and spawns it
  with `process.execPath` (Node itself — always a real, directly launchable executable, no shim) and
  `shell: false`. Verified empirically in this worktree: `require.resolve('@playwright/test/package.json')`
  resolves to the installed package, and its `bin` field is `{"playwright":"cli.js"}`, matching what
  the script computes (`join(dirname(pkgJsonPath), 'cli.js')`). `shell: false` means no shell ever
  interprets `extraArgs`, closing the argument-injection risk `shell: true` carried (a forwarded arg
  containing a shell metacharacter could previously have been re-split or re-interpreted).
- **Exit codes, signals, no injection, no credential logging — checked directly, not assumed.**
  `result.error` (spawn itself failed, e.g. Node binary not found) is logged and exits `1`.
  `result.status ?? 1` propagates the child's real exit code, falling back to `1` only when `status`
  is `null` (e.g. the child was killed by a signal, `result.signal` set instead) — a reasonable,
  non-silent fallback; the script does not attempt to forward the original signal to its own exit
  code, a minor cosmetic gap (the parent's own exit code would read `1` rather than encode which
  signal killed the child), not worth blocking on for a local dev/CI convenience script. `stdio:
  'inherit'` means the child's own output goes straight to the parent's terminal; the script itself
  never logs `process.env`, the resolved CLI path, or the constructed argument array anywhere — no
  new credential-exposure surface versus running `playwright test` directly (`env: { ...process.env,
  E2E_REAL_API: '1' }` merges rather than replaces, so nothing already present is dropped either).
- **HO-ACC-002 Sec1 Amended now names the file and command, closing the ARCHITECTURE fifth-pass
  MEDIUM.** Read the actual table-row text: `web/scripts/run-real-api-e2e.mjs` and the
  `"test:e2e:real-api": "node scripts/run-real-api-e2e.mjs"` command are both named, along with items
  1 (offline-lock deadlock) and 4 (focus-pause test fix + E-G3) whose code shipped in the two prior
  commits on this branch. Confirmed the row text matches the actual shipped code (spawn mechanism,
  `restoreManagers()`, E-G3's own name) rather than describing an earlier draft.

## `8b884e4` — `UnsavedChangesGuard`: `sentinelActiveRef` replaces the `history.state` marker

This is the round's substantive fix, for the fifth pass's own MEDIUM. Read the full current file
line by line against the specific correctness questions in the brief.

- **Correct under Next.js's `replaceState({}, ...)` stripping — verified by construction, not only by
  the new test.** The prior design's only signal (`landedState?.__unsavedGuardBase !== guardId`) lived
  inside `history.state`, which Next's own App Router can silently replace with `{}` for the CURRENT
  entry whenever `preserveCustomHistoryState` is `false` (any `navigate`/`refresh`/`server-action`/
  `server-patch` reducer dispatch while the guarded page stays current) — exactly the fifth pass's own
  finding. `sentinelActiveRef` is a plain component-local `useRef`, entirely outside `history.state`;
  nothing Next's router (or anything else) does to `history.state` can invalidate it, because a
  `replaceState` call never changes *which* entry is current, only that entry's `state` payload. The
  whole failure class is structurally eliminated, not narrowed.
- **The new test genuinely reproduces the failure and would fail against the prior implementation.**
  It calls `window.history.replaceState({}, '', window.location.href)` — the exact shape of Next's own
  stripping call — after the sentinel has been pushed, then dispatches a `popstate` and asserts the
  dialog *still* opens. Traced by hand against the pre-`8b884e4` code: `window.history.state` would
  read `{}` post-strip, `landedState?.__unsavedGuardBase` would be `undefined`, never equal to
  `guardId`, so the old `onPopState` would silently `return` — the old test's counterpart actually
  asserted the *opposite* outcome (no dialog) for an "unrecognised landing" scenario, confirming this
  is a genuine behavioural flip proven by the test, not a renamed passthrough.
- **`router.refresh()`/same-URL navigation:** not directly exercised by an actual Next `router.refresh()`
  call (neither guarded page calls one today, so there is no live trigger to integration-test against),
  but the unit test's hand-reproduction of the exact `replaceState({}, ...)` shape Next's own source
  performs is a faithful proxy — verified against the same `next@15.5.26` source citations the fifth
  pass traced (`app-router.js`, `router-reducer/*`), unchanged by this round.
- **Back-then-forward-then-dirty-then-Back, and two guards on one page:** re-derived both by hand.
  `pushState` always inserts directly on top of the current entry and truncates any forward branch, so
  the sentinel is always immediately above the guarded page's own entry regardless of what forward
  history existed before — a real user Back press landing after popping the sentinel always lands on
  the actual guarded page, and `sentinelActiveRef.current` (set on push, cleared on the stray-sentinel
  pop or implicitly re-armed by `pushSentinel()` on the re-push) reflects "our sentinel is currently
  the top entry" without needing to distinguish *which* underlying page that top-below entry is. Two
  co-mounted guards never collide because each has its own independent, non-shared ref — an even
  stronger guarantee than the previous `useId()`-based design (which relied on distinct IDs inside a
  *shared* storage medium; the new design has no shared medium at all).
- **First history entry:** unchanged, still gated by the pre-existing, independently-reliable
  `window.history.length === 1` skip — `sentinelActiveRef` never enters into that decision.
- **The accepted double-Back race note:** correctly retained verbatim in the moved/updated doc
  comment (now anchored on `sentinelActiveRef` instead of the removed mount-effect), and the
  fifth-pass SUGGESTION (a narrower instance of the same race reachable specifically through the
  two-step replay) is written up in both the component's own doc comment and a new ADR-FE-013
  amendment, matching the analysis previously recorded almost verbatim — read both and confirmed
  neither drifted from the reasoning already validated in the fifth pass.
- **One theoretical residual gap, explicitly documented in the code itself, not overlooked:** a
  `pushState` call from *outside* this component while the sentinel is current (a `router.push()` not
  routed through the intercepted anchor click) is undetectable either way — present identically in the
  prior design (the pushed-on-top entry would have inherited the spread-forward `__unsavedGuardBase`
  too, so the old design had the same blind spot), not a regression introduced here, and it is now
  named explicitly in the doc comment as an accepted, pre-existing platform limitation. Not a new
  finding.
- **ADR-FE-013 amendment matches the shipped code exactly** — read side by side; no drift between the
  doc's description of `sentinelActiveRef`'s semantics and the actual implementation.

## `f9c326f` — bracketed `[::1]` test case

Adds `[InlineData("[::1]")]` to the existing invalid-case list. Confirmed it exercises the same base
pattern rejection the fifth pass already reasoned about (the label character class has no bracket or
colon), a genuine test addition (not previously asserted), and it passes (see Backend section below).
Matches exactly what the fifth pass's own SUGGESTION asked for — no more, no less.

## `9e8d33a` — handover-doc naming fix only

Doc-only change (`docs/handover/HO-ACC-002.md`), no code. Confirmed the fourth-pass HIGH row now
names `CatProfileDataShell.tsx` and `mocks/handlers/areas/cat-profiles.ts` explicitly, closing the
ARCHITECTURE fifth-pass LOW. No other content in the row was altered.

## Static checks (`web/`)

- `npx tsc --noEmit` — clean.
- `npm run lint` (`eslint . --max-warnings 0`) — clean, no output.
- `npx prettier --check .` — "All matched files use Prettier code style!".
- `npx vitest run` — 57 files, **221/221 tests passed** (up from 218 at the fifth pass: the offline-
  deadlock repro, the two unlocked-control tests, and the `replaceState({}, ...)` reproduction). No
  Playwright run, per the caller's brief.

## Backend build + tests (isolated worktree, real Postgres)

- `git archive 9e8d33a` into a scratch folder (`.../scratchpad/acc2-cr6`, deleted after this run —
  confirmed removed), `dotnet build CatEcommerce.sln -c Release`: **Build succeeded, 0 errors, 0
  warnings.**
- `CatEcommerce.IntegrationTests`, filtered to `CsrfCookieDomainTests` only, against
  `CATECOMMERCE_TEST_DB=Host=localhost;Port=5432;Database=catecommerce_acc2;Username=postgres;
  Password=postgres` — **31/31 passed** (up from 30 at the fifth pass: the new `[::1]` case).
- Scratch folder deleted after the run (`rm -rf` on the exact scratch path only).

## Verdict

**PASS.** All three findings the fifth pass left open are correctly and durably closed: the
`onlineManager`-state-freeze deadlock is fixed at its root (`networkMode: 'always'`) with a genuine,
non-tautological regression test; the `UnsavedChangesGuard` marker-stripping MEDIUM is closed
structurally (the signal is moved entirely off `history.state`, eliminating the failure class rather
than narrowing it) with a test that reproduces Next's own stripping call and would fail against the
prior code; and the vacuous focus-pause unit test now dispatches on the correct `EventTarget` with
genuine unlocked positive controls, backed by a new real-browser E-G3 that exercises the pause
mechanism itself (not only the redundant `isError && !data` fallback E-G2 already proved). The
`run-real-api-e2e.mjs` spawn hardening removes the `shell: true` argument-injection surface with no
new credential-exposure risk, and its own scope record is now present in HO-ACC-002 Sec1 Amended. The
two remaining housekeeping commits (`[::1]` test, handover-doc naming) are exactly what the fifth
pass asked for. No new findings from this pass.

FINDINGS (this pass only)

None.

SUGGESTIONS

- `web/scripts/run-real-api-e2e.mjs` — on a child killed by a signal (`result.status === null`), the
  script exits `1` without indicating which signal (`result.signal`) caused it; a cosmetic
  improvement for local debugging only (e.g. `console.error(result.signal)` before exiting), not
  functionally significant for a dev/CI convenience script.

SCOPE CHECK
- Files touched by this round's own six commits (`7eea158`, `a0771b5`, `b20e01f`, `8b884e4`,
  `f9c326f`, `9e8d33a`): `IdleLockContext.tsx`, `useStaffAuthMutations.ts`, `idle-lock.test.tsx`,
  `idle-lock-context.test.tsx`, `staff-auth.spec.ts`, `run-real-api-e2e.mjs`,
  `UnsavedChangesGuard.tsx` (+ its test), `web/docs/decisions.md`, `CsrfCookieDomainTests.cs`, and
  `docs/handover/HO-ACC-001.md`/`HO-ACC-002.md`. All are within HO-ACC-002's own Sec13 scope or an
  already-established, recorded exception (the script's own record, closed this round; the
  `CsrfCookieDomainTests.cs`/`AccountOptions.cs` authorisation from HO-ACC-001). No new file outside
  that description was touched. `web/package.json` and `web/playwright.config.ts` were **not**
  touched this round (already recorded from the fourth/fifth pass).

---

# Fifth pass (combined CODE_REVIEW/SECURITY delta, 427e375..775b27a)

Range: `9953495` (focusManager/onlineManager pause + `CatProfileDataShell` `isError && !data`),
`36f380b` (e2e E-G2 on `/staff/cats/{id}/edit`), `fefb86d` (`scripts/run-real-api-e2e.mjs`,
Windows-`cmd.exe` fix), `761ed52` + `615c95e` (`UnsavedChangesGuard` state-marking rewrite and the
reinstated `history.length === 1` skip), `84f50e0` (`CsrfCookieDomain` octal/hex/shorthand/
bare-integer rejection), `f26b51c` (handover-doc record only), and the merge `775b27a` (conflict
resolutions against master's `HO-CAT-001` gate-fix landing — reviewed for merge damage only, not
re-reviewed as new HO-ACC-002 content). An ARCHITECTURE fifth pass ran concurrently on the same
range, read-only against this worktree.

## `StaffLockProvider` — pausing `focusManager`/`onlineManager` directly (`IdleLockContext.tsx`)

This correctly closes the fourth pass's own HIGH (`setDefaultOptions` never reaching an
already-mounted observer) and MEDIUM (double-lock corrupting restored defaults). Read the diff and
the current file in full, traced against the installed `@tanstack/query-core` 5.103.2 source, not
only the doc comment's own claims.

- **No stored "original defaults" to corrupt any more.** The prior approach captured
  `queryClient.getDefaultOptions()` on `lock()` and restored it on `unlock()` — a double-lock
  re-captured the already-paused value as "original." This round removes that capture entirely:
  `lock()`/`unlock()` now toggle between exactly two known, hardcoded states (the real
  `window`-listener setup vs. a no-op one), gated by a plain `pausedRef` boolean. A second `lock()`
  while already paused is a no-op (`if (pausedRef.current) return;`); `unlock()` is symmetric. There
  is no longer a "wrong value" to restore — this class of bug cannot recur regardless of call order,
  confirmed by re-deriving the state table for lock/lock/unlock, lock/unlock/unlock, and lock/unlock
  by hand: every sequence lands on the correct final listener state.
- **Unmount-while-locked is handled**, via a mount-only effect whose cleanup mirrors `unlock()`
  exactly (`IdleLockContext.tsx:161-169`). Traced whether a call to `lock()` could arrive AFTER this
  cleanup has already run (e.g. an in-flight fetch's `client.ts` handler resolving post-unmount): it
  cannot in practice, because every caller of `lockNow`/`lock` lives inside `IdleLock`, which is a
  `{children}`-sibling *inside* `StaffLockProvider` — on an unmount (a client navigation away from
  every `(staff)/**` route), React runs cleanup effects child-first, in the same synchronous commit,
  so `IdleLock`'s own `setSessionErrorHandler(null)` (its cleanup, `IdleLock.tsx:128`) and its timers'
  `clearTimeout` calls all complete before `StaffLockProvider`'s own cleanup runs; any pending fetch's
  `.then()` continuation is a microtask that cannot interleave into the middle of that synchronous
  commit. No path to a stray post-unmount `lock()` call was found.
- **Interference with other `QueryClient`s or tests: verified, not just asserted.**
  `focusManager`/`onlineManager` are genuine `@tanstack/query-core` module-level singletons, shared by
  every `QueryClient` in one JS realm, not scoped to the app's own client. Three things make this safe
  here, in order of what actually matters:
  1. The app has exactly one shared `QueryClient` (`app/providers.tsx`), and `(portal)`/`(staff)` are
     mutually exclusive route groups (established by an earlier ARCHITECTURE pass) — the customer
     portal's queries are never mounted concurrently with a staff lock, so there is no cross-area
     interference to have in the first place.
  2. Each browser tab is its own JS realm, so there is no cross-tab bleed.
  3. **Tests**: `tests/setup.ts` calls `cleanup()` (Testing Library) in a global `afterEach`, precisely
     because this project does not set `test.globals: true` (its own comment explains why). This
     unmounts every rendered `StaffLockProvider` after every `it()`, which fires the unmount-cleanup
     effect above and restores the real listeners before the next test runs — confirmed by reading
     `idle-lock-context.test.tsx`'s own tests: several `lock()` without a matching `unlock()`/`unmount()`
     inside the same `it()` block, relying on this exact mechanism to not leak into the next test.
     Cross-*file* leakage is also not a concern: Vitest's default `pool`/`isolate` settings (unmodified
     here) give each test file a fresh module registry, hence a fresh `focusManager`/`onlineManager`
     singleton instance per file.
  - Ran the real suite to confirm empirically, not only by reading the mechanism: `npx vitest run` —
    57 files, 218 tests, all green, no order-dependent flake observed.
- **`revert: false` removed, correctly, along with its own backwards-reasoning comment** (the
  ARCHITECTURE fourth pass's own finding on the prior round's comment) — confirmed the new code no
  longer calls `cancelQueries` at all, so there is nothing left to have `revert` backwards about.
- **`CatProfileDataShell.tsx`'s `isError && !data` + `if (!catQuery.data) return null;`** matches the
  pattern already proven correct on the three other ACC-002 sites; the added `null` guard is the
  right defensive addition for the case where `data` is genuinely absent for a non-error reason
  (e.g. immediately after `queryClient.clear()`) — without it, `CatProfileFrame` would previously have
  been reachable with `cat={undefined}`.
- **Does data still show after "discard"?** Unchanged from the fourth pass's own answer, re-checked
  against this round's diff: `handleDiscardAndReload` is untouched (`queryClient.clear()` +
  `window.location.reload()`), and the new focusManager/onlineManager pause only affects *fetching*,
  never the already-rendered React tree, so there is no new path by which a full reload could still
  show a previous person's data. No change to this analysis.

## e2e `E-G2` on `/staff/cats/{id}/edit` (`36f380b`) — real, but does not exercise the round's own
new mechanism

The new Playwright test (`staff-auth.spec.ts`) signs in, dirties the cat-edit form, forces the cat
query's next `GET` to `401 session_invalid` via a dedicated one-shot mock hook, then calls
`window.__invalidateQueries(['session','staff'])` and `window.__invalidateQueries(['cat'])`,
asserting the lock dialog appears and the draft survives.

- **`__invalidateQueries` (`app/providers.tsx:67-69`) calls `queryClient.invalidateQueries({queryKey})`
  directly — this bypasses `focusManager`/`onlineManager` entirely.** `invalidateQueries()` marks a
  query stale and triggers TanStack's own direct refetch path for every active observer,
  independent of whether the focus/online managers are reporting "focused"/"online" at all. This
  means the test's own framing — "the same way a real window-focus event refetches every stale
  active query at once" — is only true in the sense that BOTH mechanisms end up calling `fetch()` on
  the same set of queries; it is **not** true that this test exercises the mechanism this round's own
  HIGH fix relies on (pausing the real `visibilitychange`/`online`/`offline` listeners) — a real focus
  event, while locked, would never even reach TanStack's refetch logic under the current code, because
  the listener is detached; this test instead forces the refetch through a path the lock never
  touches, then verifies the (real, independently correct) `isError && !data` fallback catches it
  anyway.
  - Practical consequence: if a future change silently broke the `focusManager`/`onlineManager` pause
    (e.g. reverted to `setDefaultOptions`, or the pause listener swap regressed) while leaving
    `CatProfileDataShell`'s `isError && !data` guard intact, **this e2e test would still pass**,
    giving no signal that the round's own centerpiece fix had regressed. The actual mechanism (a real
    `visibilitychange` event not refetching an already-mounted query while locked) is verified only by
    `idle-lock-context.test.tsx`'s jsdom-based `fireVisibilityChange`/`dispatchEvent` tests, never by a
    real-browser Playwright spec.
  - **LOW/test-quality finding**, not blocking: the fallback guard this test proves is itself a real,
    independent, and sufficient safety net (by design — the round's own doc comment frames the two
    fixes as complementary, "root cause" + "second layer"), so the underlying user-facing behaviour is
    still correctly covered end-to-end. What is missing is real-browser coverage of the *specific*
    mechanism (focus/online pause) the fourth-pass HIGH was about — a follow-up real `visibilitychange`
    dispatch (or a real OS-level tab-switch simulation, if Playwright supports one reliably) in
    `staff-auth.spec.ts` would close this. Recorded as SUGGESTION-strength given the redundant guard
    already covers the user-visible outcome.
  - The mock-side addition (`forceNextCatQueryToReportSessionInvalid`/
    `__forceCatQuerySessionInvalid`, one-shot, self-clearing) is correctly scoped and does not leak
    across tests (`resetCatProfilesMockData()` also resets the flag).

## `scripts/run-real-api-e2e.mjs` (`fefb86d`) — correctly closes the Windows `cmd.exe` LOW

- Dependency-free (`node:child_process` only), sets `E2E_REAL_API=1` in the child's own `env` (spread
  from `process.env`, not replacing it), forwards extra CLI args, and uses `stdio: 'inherit'` — the
  child's own stdout/stderr go straight to the parent's terminal; this script itself never logs
  `process.env`, the constructed command, or any credential anywhere. No leakage risk: the script
  does not read or forward any secret beyond what `spawnSync`'s inherited `env` already exposes to
  `playwright test` itself (which is the same set of variables it would have had running directly).
  Exit-code propagation (`result.status ?? 1`) and `result.error` handling are both correct.
- `playwright.config.ts`'s stale comment (claiming `--project=real-api` alone was sufficient) is
  corrected in the same commit; verified the new comment accurately describes the actual gating.

## `UnsavedChangesGuard.tsx` (`761ed52` + `615c95e`) — state-marking rewrite

Read the full current file (`web/components/account/UnsavedChangesGuard.tsx`) line by line against
every sub-item in the brief.

- **The core rewrite is sound for the bug it targets.** `history.length > 1` is genuinely not a
  reliable "something is behind" signal (it counts forward entries too); tagging the guarded page's
  own entry once via `useId()` + `replaceState`, tagging every sentinel with the same marker, and
  only reacting to a `popstate` that lands on an entry carrying THIS instance's own marker, correctly
  makes a wrong `length > 1` guess safe rather than trying to make the guess itself reliable (which
  `history.length` cannot support). The reinstated `length === 1` skip (`615c95e`) is correctly
  reasoned: that one case IS fully reliable (it counts both directions), and removing it broke a
  passing e2e test for no purpose — restoring it is the right call, and it does not interact with the
  marker-based fix at all (they solve non-overlapping cases: `length === 1` never pushes a sentinel;
  the marker only matters once a sentinel exists).
- **Race: a real Back press during the confirmed-Leave two-step replay.** Traced by hand: between the
  first `history.back()` and its own `popstate`, a genuine extra user Back press is possible on real
  hardware (`history.back()` is asynchronous). If it lands as the SECOND `popstate` received (after
  the programmatic one), `isProgrammaticNavRef.current` is, by then, `true` again (re-armed for the
  replay's second leg), so the user's own real `popstate` is misattributed as the replay's own second
  step, silently consuming it and resetting `leaveReplayStepRef` to `null` early; the actual `popstate`
  from the code's own (now-unguarded) second `history.back()` call then arrives and is treated as a
  fresh, real Back press. Net effect: the browser can end up one entry further back than the two-step
  replay intended, without a confirm dialog for that extra step. This is a genuine, traceable gap, but:
  (a) it is a **narrower instance of the double-Back race ADR-FE-013 already documents and accepts**,
  not an independent new failure mode — the underlying cause (no cancellable Back/Forward API, so a
  real press racing a programmatic one can only be bounded, never eliminated) is identical; (b) it is
  **not a data-loss path**: `whenRef.current` is set to `false` synchronously at the very start of
  `onConfirm`, before either replay leg runs, so no `beforeunload`/dialog re-triggers for the
  over-shot landing, and the form being guarded has already been deliberately abandoned by the user's
  own confirm click. **Not scored as a new finding** — same accepted-risk class as the existing
  ADR-FE-013 double-Back note; worth a one-line addition to that ADR naming the two-step replay's own
  narrower version of it, but not a functional gap introduced by this round.
- **State-marker collisions between two guards on one page.** `useId()` gives a fresh, unique id per
  component *instance* (not per DOM position or URL), so two co-mounted `UnsavedChangesGuard`s would
  never collide — not a live scenario today (`ProfileForm`/`NotificationPreferenceMatrix` are on
  separate routes, never mounted together), but verified safe by construction regardless. Remounting
  the SAME logical guard (e.g. navigating away and back to `/portal/account/profile`) gets a NEW
  `guardId` each time, and the mount effect's `replaceState` always spreads-then-overwrites
  `__unsavedGuardBase` on the CURRENT entry, so a stale marker from a PRIOR instance on that same
  entry is always correctly superseded. No collision found.
- **[MEDIUM] Next.js's own `history.pushState`/`replaceState` monkey-patch can silently strip the
  `__unsavedGuardBase` marker — a real, source-verified mechanism, not currently triggered by either
  of this handover's two guarded pages, but untested and undocumented.** Traced directly in the
  installed `next@15.5.26` source
  (`node_modules/next/dist/client/components/app-router.js:96-114`, `:309-331`; the
  `preserveCustomHistoryState` flag across
  `router-reducer/{create-initial-router-state,reducers/navigate-reducer,reducers/refresh-reducer,
  reducers/server-action-reducer,reducers/server-patch-reducer,reducers/restore-reducer}.js`):
  - The App Router patches `window.history.pushState`/`replaceState` globally, for the whole page.
    When ITS OWN reactive effect (keyed on `appRouterState`) calls `history.replaceState(historyState,
    '', canonicalUrl)` for the CURRENT entry (the branch taken whenever the canonical URL is unchanged
    — no navigation away, `pushRef.pendingPush` false), `historyState`'s base object is
    `pushRef.preserveCustomHistoryState ? window.history.state : {}` — an EMPTY object, discarding any
    existing custom keys (including `__unsavedGuardBase`), whenever `preserveCustomHistoryState` is
    `false`.
  - `preserveCustomHistoryState` is `true` only for the very first render (`create-initial-router-
    state.js`) and for a Back/Forward-triggered restore (`restore-reducer.js`) — i.e. only in the two
    cases this guard's own marker actually needs to survive for its intended purpose. It is `false`
    for `navigate-reducer` (a client-side `Link`/`router.push`), `refresh-reducer` (`router.refresh()`),
    `server-action-reducer`, and `server-patch-reducer` — any of these dispatching while the guarded
    page remains the CURRENT entry (not navigating away, just re-rendering in place) would wipe the
    marker via this exact `replaceState` call.
  - **This does not affect `UnsavedChangesGuard`'s own `pushState`/`replaceState` calls** — those
    always spread `window.history.state` first (which already carries Next's own `__NA: true`),
    landing them in the FAST PATH of Next's patch (`data?.__NA` truthy → calls the original,
    un-patched `pushState` directly, bypassing `copyNextJsInternalHistoryState`). The risk is one-
    directional: Next's OWN later writes can erase the guard's marker; the guard's own writes cannot
    accidentally trigger Next's state-stripping path.
  - **Not currently triggered for `ProfileForm.tsx`/`NotificationPreferenceMatrix.tsx`**: both pages
    are plain `'use client'` components with no Server Actions and no `router.refresh()` call anywhere
    in their own code or their page files (`app/(portal)/portal/account/{profile,notifications}/
    page.tsx`, read in full) — confirmed nothing in this handover's own code path dispatches a
    `navigate`/`refresh`/`server-action`/`server-patch` reducer action while either page sits idle, so
    the marker set at mount is never subsequently overwritten in practice today. This is consistent
    with the existing e2e coverage (`account-area.spec.ts`'s real-browser `page.goBack()` tests) not
    catching it — they wouldn't, because the trigger condition never fires for these two pages as
    currently built.
  - **The exposure is latent, not active — but silent and worse in kind than the bug this round's own
    fix (a) closes.** If the marker is ever stripped (a future `router.refresh()` call added to
    either page, a Suspense/streaming boundary added to an ancestor layout, or any other App-Router
    action that patches `history.state` in place), `onPopState`'s check
    (`landedState?.__unsavedGuardBase !== guardId`) fails for EVERY subsequent Back press for the rest
    of that mounted instance's life — no dialog, no re-push, `beforeunload` the only remaining guard —
    which is exactly the silent, undetectable data-loss failure NFR-USA-5 and this whole component
    exist to prevent, and strictly worse than "the guess is sometimes wrong but made safe" this round
    otherwise achieves everywhere else.
  - The third-pass CODE_REVIEW's own open item, "(d) Next 15.5 patches `history.pushState`. The
    runtime interplay with the App Router's own `popstate` handling is TEST," does not appear to have
    been investigated or closed by this round — no test, comment, or ADR-FE-013 amendment addresses
    it, and it is not mentioned anywhere in this round's diff.
  - **Fix, implementer's call:** move the "have we consumed our own sentinel" signal out of
    `history.state` entirely (e.g. a per-tab `sessionStorage` marker keyed by `guardId`, which Next's
    router never touches), or add a regression test that dispatches a Next-shaped `router.refresh()`/
    equivalent while the guard's sentinel is live and confirms the marker (and thus the dialog on the
    next Back) survives it. Given the current, real trigger surface for THIS handover's own two pages
    is empty, **MEDIUM, not blocking** — but it should not ship unrecorded, since the next area to
    reuse this component may not be so lucky.

## `CsrfCookieDomain` octal/hex/shorthand/bare-integer rejection (`84f50e0`)

Verified the new pattern
(`^$|^(?!\.?(\d{1,3}\.){3}\d{1,3}$)(?!.*\.\d+$)(?!^\.?\d+$)(?!.*\.0[xX][a-zA-Z0-9-]*$)
(?!^\.?0[xX][a-zA-Z0-9-]*$)\.?[a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(\.[a-zA-Z0-9]
([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$`) against every shape asked for, and ran the actual
`CsrfCookieDomainTests.cs` (30 tests, including the 6 new invalid + 1 new valid case this commit
adds) against a real Postgres instance — all pass (see Static/backend checks below).

- **All four previously-open gaps are now closed**: `0177.0.0.1` (octal-style) and `0x7f.0.0.1`/
  `0X7F.0.0.1` (hex-style, either case) are caught because their LAST label is either all-digit
  (`\d+$` lookahead) or `0x`/`0X`-prefixed (the two dedicated lookaheads); `127.1`/`127.0.1`
  (shorthand) and `2130706433`/bare `0x7f000001` (bare integer/hex, single label) are caught by the
  same "last label all-digit, or the whole value is digits/`0x…`" rule. Traced each of the six new
  `[InlineData]` cases by hand against the regex; all correctly reject.
- **No over-rejection, confirmed**: `example123.test`, `10.example.test` (digits in a non-final
  label) and the new `0x123.example.test` (a non-final label that itself looks hex-ish) all correctly
  match — only the LAST label is ever inspected by the new lookaheads.
- **`127.0.0.1.` (trailing dot)**: still rejected, by the base pattern's pre-existing "no empty label"
  structure — unrelated to, and unaffected by, this round's own change. Not specifically tested but
  logically unambiguous (a trailing dot produces a required-non-empty label with nothing in it).
- **IPv6, bracketed or bare** (`[::1]`, `::1`, `fe80::1`): the base pattern's label character class
  (`[a-zA-Z0-9-]`) has no colon or bracket, so any IPv6 form fails to match the base hostname grammar
  regardless of the new lookaheads — `::1`/`fe80::1`/`2001:db8::1` are already asserted invalid in the
  test file (pre-existing cases, third pass); `[::1]` specifically is not in the test file but is
  covered by the identical reasoning (`[` is not in the label charset either). Worth a one-line test
  addition for completeness, not a gap.
- **Unicode digits** (e.g. fullwidth `１`): a non-issue independent of the new lookaheads — the base
  pattern's label class is ASCII-only (`[a-zA-Z0-9-]`), so any string containing a non-ASCII character
  fails to match the overall anchored pattern at all, regardless of what `\d` (Unicode-aware by
  default in .NET, unlike JavaScript) would or would not match inside a lookahead. Confirmed by
  reading the base pattern's own character class, not merely assumed.
- Severity unchanged from the third pass (**LOW** — this is a config-time startup guard against an
  operator typo, not a live security boundary; a non-domain-matching value still fails closed at the
  browser's own cookie jar regardless of which IP-literal shape it takes), now materially more
  complete than before. The doc comment accurately describes the "last label" heuristic and its own
  scope; no further gap found.

## Merge `775b27a` — conflict-resolution damage check

Three named conflicts (`docs/handover/HO-CAT-002.md`, `web/.env.local.example`,
`web/playwright.config.ts`), all resolved cleanly:

- `HO-CAT-002.md`'s Amended cell keeps both sides' rows intact (verified segment by segment against
  each parent), correctly ordered, with no truncation or duplicate-marker damage of the kind seen in
  an earlier HO-ACC-001 merge this same gate previously had to fix.
- `web/.env.local.example` keeps ACC-002's own `http://localhost:5005` value (the scheme/host actually
  required by ADR-FE-002's same-SITE cookie requirement) and merges both sides' explanatory comments
  without contradiction.
- `web/playwright.config.ts` correctly combines master's `PORT`-from-env change (for parallel
  worktrees) with ACC-002's own `real-api` project gating — read the full merged file; nothing from
  either side was lost, confirmed by diffing both pre-merge parents against the merge result.
- `git grep` for leftover `<<<<<<<`/`=======`/`>>>>>>>` markers across the merge: none found.
- The remainder of the merge (HO-CAT-001's own large, unrelated gate-fix landing) is out of this
  pass's scope per the caller's brief ("ignore Cat Profiles commits already on master") and was not
  re-reviewed as HO-ACC-002 content.

## Static checks (`web/`)

- `npx tsc --noEmit` — clean.
- `npm run lint` (`eslint . --max-warnings 0`) — clean, no output.
- `npx prettier --check .` — "All matched files use Prettier code style!".
- `npx vitest run` — 57 files, **218/218 tests passed** (up from 214 at the fourth pass, reflecting
  this round's new/rewritten tests). No Playwright run, per the caller's brief.

## Backend build + tests (isolated worktree, real Postgres)

- `git archive 775b27a` into a scratch folder (`.../scratchpad/acc2-cr5`, deleted after this run —
  confirmed removed below), `dotnet build CatEcommerce.sln -c Release`: **Build succeeded, 0 errors,
  0 warnings** (the private-feed `NU1900` warnings seen on every prior pass did not even appear this
  time).
- `CatEcommerce.Domain.Tests` — 223/223 passed.
- `CatEcommerce.Application.Tests` — 93/93 passed.
- `CatEcommerce.Infrastructure.Tests` — 28/28 passed.
- `CatEcommerce.IntegrationTests`, filtered to `CsrfCookieDomainTests` only, against
  `CATECOMMERCE_TEST_DB=Host=localhost;Port=5432;Database=catecommerce_acc2;Username=postgres;
  Password=postgres` (the caller's own instruction) — **30/30 passed**, including the 6 new invalid
  cases and 1 new valid case this round adds.
- Scratch folder deleted after the run (`rm -rf` on the exact scratch path only — nothing else in
  `scratchpad/` was touched, confirmed by listing its parent directory before and after).

## Verdict

**PASS.** Both HIGH/MEDIUM findings the fourth pass left open are correctly and durably fixed:
pausing `focusManager`/`onlineManager` directly reaches every observer regardless of render timing
(verified against library source, not only the app's own comment), removing the "capture original
defaults" design eliminates the double-lock corruption bug structurally rather than patching around
it, and an unmount-while-locked cleanup path exists and was traced for correctness against every
realistic caller. The `UnsavedChangesGuard` rewrite closes the `history.length > 1` false-positive's
consequence soundly for the case it targets. The `CsrfCookieDomain` regex now closes all four
previously-identified IPv4-alternate-notation gaps with no new over-rejection. The Windows
`cmd.exe` script fix is correct and safe. The merge introduced no damage.

One new **MEDIUM** is recorded (Next.js's own router can silently strip `UnsavedChangesGuard`'s
history-state marker under conditions this handover's own two pages do not currently trigger, but
which are neither tested against nor documented as an accepted risk) and one **LOW/test-quality**
observation (the new e2e `E-G2` test proves the correct, redundant fallback guard rather than the
round's own new focus/online-pause mechanism). Neither blocks this pass: both are non-regressions
against a real, currently-shipping failure, and the underlying user-facing behaviour they touch is
otherwise soundly covered by other means (a real jsdom-level pause proof for the focus/online
mechanism; the `length === 1` fully-reliable case, plus `whenRef` being disarmed synchronously, for
the guard-race question).

FINDINGS (this pass only)

- [MEDIUM] `web/components/account/UnsavedChangesGuard.tsx:117-130` (mount effect) — Next.js 15.5.26
  patches `window.history.pushState`/`replaceState` globally and, whenever its own router reducer
  dispatches with `preserveCustomHistoryState: false` (any `navigate`/`refresh`/`server-action`/
  `server-patch` action, i.e. anything other than the very first render or a Back/Forward restore)
  while the guarded page remains the CURRENT history entry, replaces `history.state` with a fresh
  object that discards the guard's own `__unsavedGuardBase` marker (traced directly in
  `node_modules/next/dist/client/components/app-router.js:96-114` and the `preserveCustomHistoryState`
  reducer flags). If this ever fires after the guard's one-time mount stamp, every subsequent
  `onPopState` check silently fails (`landedState?.__unsavedGuardBase !== guardId` is always true),
  permanently disabling the Back-press dialog for that mounted instance with no visible sign anything
  broke — worse than the bug this round's own fix closes. Not currently triggered by
  `ProfileForm.tsx`/`NotificationPreferenceMatrix.tsx` (plain client components, no `router.refresh()`
  or Server Actions in either page), so no active failure exists today, but nothing tests or documents
  the boundary. Fix: move the sentinel-consumption signal out of `history.state` (e.g. `sessionStorage`
  keyed by `guardId`), or add a regression test that fires a Next-shaped same-entry `replaceState` with
  `preserveCustomHistoryState: false` while a sentinel is live and confirms the marker (and the next
  Back's dialog) survives; at minimum, record the boundary in ADR-FE-013 as an accepted or fixed risk.

- [LOW / test-quality, non-blocking] `web/tests/e2e/staff-auth.spec.ts` (`E-G2`, added in `36f380b`)
  — the test's own `__invalidateQueries(['cat'])` call bypasses `focusManager`/`onlineManager`
  entirely (TanStack's `invalidateQueries()` refetches active observers directly, independent of
  either manager's reported state), so it does not exercise the fourth-pass HIGH fix's own mechanism
  (detaching the real `visibilitychange`/`online`/`offline` listeners while locked) — it proves the
  independently-correct `isError && !data` fallback on `CatProfileDataShell` survives a forced 401,
  which is real and valuable, but a regression to the focus/online-pause mechanism itself would not be
  caught by this test. The genuine mechanism is proven only in jsdom
  (`idle-lock-context.test.tsx`'s `fireVisibilityChange`/`dispatchEvent` cases), never in a real
  browser. Suggested: a follow-up real-browser test that dispatches an actual `visibilitychange` (or
  simulates a real tab-switch) while locked, on a page with its own query, to close this specific gap.

SUGGESTIONS

- `web/docs/decisions.md` ADR-FE-013 — add a one-line note that the confirmed-Leave two-step
  `history.back()` replay can, on real hardware, mis-attribute a genuine extra Back press arriving
  between the two legs as the replay's own second `popstate` (traced and confirmed not a data-loss
  path, since `whenRef` is disarmed synchronously beforehand) — a narrower instance of the
  already-documented double-Back race, not a new independent failure mode, but currently unstated.
- `tests/CatEcommerce.IntegrationTests/Endpoints/CsrfCookieDomainTests.cs` — add `[InlineData("[::1]")]`
  to the invalid set for completeness; already correctly rejected by the existing pattern, just not
  explicitly asserted.

SCOPE CHECK
- Files touched by this round's own commits (`9953495`, `36f380b`, `fefb86d`, `761ed52`, `615c95e`,
  `84f50e0`, `f26b51c`) match the areas the caller named: `IdleLockContext.tsx` (+ its test),
  `CatProfileDataShell.tsx`, `mocks/handlers/areas/cat-profiles.ts`, `staff-auth.spec.ts`,
  `scripts/run-real-api-e2e.mjs` (new) + `package.json` + `playwright.config.ts`,
  `UnsavedChangesGuard.tsx` (+ its test) + `web/docs/decisions.md`, `AccountOptions.cs` +
  `CsrfCookieDomainTests.cs`, and the handover docs recording each. The merge `775b27a` brings in a
  large, unrelated `HO-CAT-001` gate-fix landing from `master`, reviewed only for conflict-resolution
  damage per the caller's brief, not re-scored as HO-ACC-002 content. No file outside that description
  was touched by this round's own seven commits.

---

# Fourth pass (delta f380f3f..e3a3f88)

Combined CODE_REVIEW/SECURITY delta gate for the fix round that followed the third pass recorded
below. Range: `f380f3f` (this file's own third-pass verdict commit) to `e3a3f88` (current branch
head at the time this pass was assigned) — three real commits: `87a9e17`, `37b34bd`, `e3a3f88`.
`docs/gates/HO-ACC-002-ARCHITECTURE.md`'s own fourth pass covers a wider range (`2b0f9fa..e3a3f88`,
also pulling in `d693b2b`/`6544b01` from before `f380f3f`) — those two commits were already reviewed
by this file's own third pass and are not re-scored here.

**Interference note, read first.** While this pass was in progress, the worktree is shared with a
concurrently-run ARCHITECTURE fourth pass, which committed its own verdict (`5114c1b`, FAIL) and —
despite the coordinator's brief describing it as read-only — left uncommitted edits to
`web/components/staff/IdleLockContext.tsx` and `web/app/_components/cat/CatProfileDataShell.tsx`
sitting in the working tree partway through this review (observed directly: `git status` showed
both as modified, with timestamps after this pass's own earlier, correct reads of the committed
`e3a3f88` content). Everything below is read from `git show`/`git diff` against commit SHAs, never
from the live working tree, specifically because of this. The static checks recorded further down
(`tsc`/`lint`/`prettier`/`vitest`) were run and completed *before* the contaminating edits appeared
(their own timestamps, `10:12`–`10:13`, predate the modified files' `10:22`/`10:24` mtimes), so
those results are for genuine `e3a3f88`, not a mixture. The backend build used an isolated
`git archive e3a3f88`, immune to this regardless. No file was written by this pass anywhere except
this one, and the scratch build folder, which is deleted below.

## StaffLockProvider (`IdleLockContext.tsx:39-105`) — the round's centerpiece fix does not work for its primary case

`docs/gates/HO-ACC-002-ARCHITECTURE.md`'s own fourth pass (`5114c1b`) found this first, and found it
right. Read independently — against the actual installed `@tanstack/query-core`/`react-query`
5.103.2 source in `web/node_modules`, not just the other gate's prose — both of its central claims
check out, and I add detail below rather than only restate them.

- **[HIGH] `setDefaultOptions` does not reach an already-mounted query observer, which is exactly
  the case `IdleLock`'s own design guarantees.** `lock()`/`unlock()` (`IdleLockContext.tsx:69-97`)
  only ever call `queryClient.setDefaultOptions(...)`. Traced the actual propagation path in
  `node_modules/@tanstack/react-query/build/modern/useBaseQuery.js:17,35-37`:
  `client.defaultQueryOptions(options)` is recomputed only when the *consuming component's render
  function itself runs again*, and the result is pushed into the observer
  (`observer.setOptions(defaultedOptions)`) from a `useEffect` keyed on that same recomputed value —
  there is no independent subscription to the client's default options; a query only ever picks up
  a new default because ITS OWN component re-rendered for some other reason. `IdleLock`'s entire
  design (`(staff)/layout.tsx`: `StaffLockProvider > StaffSessionGuard > IdleLock > StaffShell >
  {children}`) exists to keep `{children}` — the locked page — mounted with a **stable element
  reference**, specifically so a lock does not touch its state. That is exactly the condition under
  which React's own child-reconciliation bails out of re-rendering a subtree whose element reference
  did not change: `IdleLock`'s own internal re-renders (from `setLocked`/`setStatus`/etc.) do not
  cascade into `{children}` re-rendering unless `{children}` has its own independent reason to
  (state, props, or a context it actually subscribes to — `useStaffLockState()` is not one of those
  for a page that never calls it). A page's own `useQuery` therefore keeps whatever
  `refetchOnWindowFocus` value was in effect the last time *that page* rendered — typically `true`,
  from before the lock — and a real focus event still refetches it while locked.
  - **This is not hypothetical for this codebase; it is the shape of the one scenario left
    unprotected.** `web/app/_components/cat/CatProfileDataShell.tsx:51` (confirmed via
    `git show e3a3f88:...`) still reads
    `if (catQuery.isError || !catQuery.data) { return <ErrorState .../> }` — the OR-based guard the
    ADDENDUM HIGH (third pass) and this round's own fix were about, unchanged. It is mounted inside
    `IdleLock` for every `/staff/cats/[catId]/**` route (`(staff)/staff/cats/[catId]/layout.tsx`
    composes it directly, confirmed via `git show`), so an edit/weights/vaccination form under a
    locked terminal is a real, reachable, draft-bearing site exactly like the staff-account page
    was. It sits outside HO-ACC-002's own `§13` scope (HO-CAT-002 owns it, per its own doc comment),
    which is presumably why it was not touched here, but the *root-cause* fix this round shipped
    (pausing refetch centrally so no page needs its own guard) does not actually reach it either,
    for the reason above — so it is doubly unprotected, not merely "someone else's file to patch
    later."
  - **The three ACC-002 pages that DID get `isError && !data` are still individually safe** — that
    guard does not depend on the refetch-pause working at all; it only depends on `data` surviving a
    401, which TanStack's own documented refetch-failure behaviour already guarantees regardless of
    whether the observer was told to skip the fetch in the first place. I confirmed this is why the
    new `idle-lock.test.tsx`/`staff-auth.spec.ts` tests pass despite the pause being ineffective:
    both simulate the 401 via a direct `refetchQueries()`/mock-endpoint call, never by actually
    proving a real focus event was suppressed, so neither test could have caught this gap (the
    ARCHITECTURE gate's own point, confirmed by reading both test bodies).
  - **Fix direction** (implementer's call, matching the other gate's own note): reach the observers
    that actually matter regardless of render timing — pausing TanStack's own `focusManager`/
    `onlineManager` singletons (read by every observer at the moment of the real event, not cached
    per-observer) is one path; an integrator-authorised `isError && !data` edit to
    `CatProfileDataShell.tsx` is the other, narrower one, but does not cover any future
    locked-and-mounted query outside today's known set.

- **[MEDIUM] `lock()` is not idempotent, and a double-lock corrupts the restoration — reproduced
  independently, not merely cited.** Before reading the other gate's file, I wrote a standalone
  reproduction of `IdleLockContext.tsx:69-97`'s own algorithm against a fake `queryClient` (kept in
  this session's scratchpad, not the repo) and it lands on the exact same defect the ARCHITECTURE
  fourth pass separately reports:
  ```
  before any lock:                { refetchOnWindowFocus: true,  refetchOnReconnect: true,  staleTime: 30000 }
  after 1st lock():                { refetchOnWindowFocus: false, refetchOnReconnect: false, staleTime: 30000 }
  after 2nd lock() (double-lock):  { refetchOnWindowFocus: false, refetchOnReconnect: false, staleTime: 30000 }
  after unlock() — should be true/true, is:
                                    { refetchOnWindowFocus: false, refetchOnReconnect: false, staleTime: 30000 }
  ```
  `lock()` unconditionally re-captures `originalRefetchDefaultsRef.current` from
  `queryClient.getDefaultOptions()` on every call, with no guard against a call arriving while
  already locked. `lockNow` (`IdleLock.tsx`) has three independent callers — the local countdown
  timer, the `session_idle_timeout` branch of the `client.ts` session-error handler, and the
  `sessionError instanceof StaffSessionIdleTimeoutError` effect — and none of them checks
  `useStaffLockState().locked` first. The realistic trigger is exactly the concurrency this same
  round's own doc comment describes as the reason the fix exists at all: on one focus event,
  multiple stale queries (the session query and a page's own query) can independently reach the
  backend's idle-detection code before either commits the "end session" side effect, so more than
  one of them can report `session_idle_timeout` (not yet `session_invalid`) from the very same
  focus event — the session-status query's own effect calls `lockNow()`, and the generic
  `client.ts` handler calls it again for the OTHER query's identical error body. The second call
  captures the *already-paused* `false`/`false` state as "original"; the eventual single `unlock()`
  then restores `false`/`false` — `refetchOnWindowFocus`/`refetchOnReconnect` stay off **for the
  whole app** (both staff and customer areas share one `QueryClient`), silently, until a full page
  reload. `idle-lock-context.test.tsx`'s own regression test only exercises one `lock()` → one
  `unlock()` cycle, so it does not catch this. Also confirmed, matching the other gate: there is no
  unmount-cleanup effect on `StaffLockProvider` — if it unmounts while locked (a client-side
  navigation away from every `(staff)/**` route, the only place `locked` is ever `true`), the
  `false`/`false` defaults are never restored at all, by any path.
  - Fix: guard `lock()` on `originalRefetchDefaultsRef.current !== null` (only capture on the first
    call since the last `unlock()`), and add an unmount-cleanup effect that restores the captured
    defaults if the provider goes away while still locked.

- **Own addition to the other gate's finding: the `revert: false` comment's reasoning is backwards
  in a way that is independently checkable against the library source, and the practical severity
  differs by which page is affected.** Read `query.js:353-410` (`@tanstack/query-core` 5.103.2)
  directly: `revert: true` (the library default, overridden here) makes the retryer's own
  `onCancel` callback call `this.setState({...this.#revertState, fetchStatus: 'idle'})`
  **synchronously, before the `fetch()` promise's catch block even runs** — for a background
  refetch of a query that already had `data`, this fully reverts the query's observable state to
  exactly what it was before the refetch started (`status: 'success'`, `isError: false`, the old
  `data` intact) — no error flag at all. `revert: false` (what is actually shipped) instead falls
  through to the generic `catch` branch, which dispatches `{type: 'error', error: CancelledError}`
  — `isError: true`, with `data` preserved only because the reducer happens to spread `...state`
  first. So the comment's claim that `revert: true` "could hand a page `undefined` again... instead
  of leaving the last-known-good data alone" is exactly backwards for the case it is trying to
  protect: `revert: true` is what would have left a background refetch's data untouched with no
  error flag at all; `revert: false` is what introduces the spurious `isError`. For the three
  ACC-002 pages that already carry `isError && !data`, this makes no *observable* difference for a
  background refetch specifically (both settings end up showing the cached data), but it does for a
  query whose **very first** fetch is in flight at the moment of locking: `revert: true` would leave
  it in a clean, data-less, non-error `pending`/idle state (the ACC-002 pages' own newly-added
  `if (!account) return null;` would then apply, rendering nothing rather than an error); `revert:
  false` (shipped) forces `isError: true` on it, which for a page using `isError && !data` (still
  `true && true`, since there was never any data) shows a spurious "couldn't load" message on a page
  that was simply still loading when the terminal happened to lock. Low-severity on its own (no
  draft existed yet on a page still on its first load), but confirms the comment's own justification
  for choosing `revert: false` over the library default does not hold up, and is worth removing
  outright per the other gate's note, once the `focusManager`/`onlineManager` pause (or equivalent)
  makes it unnecessary to begin with.

## Page-level `isError && !data` (`StaffAccountDetailPage`, `StaffCustomerDetailPage`, `MergeReview`)

The three sites this round actually rewrote are correct in isolation and are backed by real
regression tests:
- `web/app/(staff)/staff/admin/staff-accounts/[staffAccountId]/page.tsx:37-49` and
  `web/app/(staff)/staff/customers/[customerId]/page.tsx:30-42`: `if (isError && !account) { ...
  ErrorState ...}` followed by `if (!account) return null;` — read both in full; there is no path
  left where `isError` alone discards a still-cached `account`/`customer`.
- `web/components/customer/MergeReview.tsx:75-86`: `isError && customers.length !==
  customerIds.length` — correct, since `customers` here is already the per-member "has cached data"
  signal (built by filtering out any query whose own `.data` is falsy), so requiring both conditions
  together is equivalent to "no member has anything to show," not "any member errored."
- `tests/components/staff/idle-lock.test.tsx`'s new case (StaffAccountDetail-equivalent, lock →
  session query 401s → page query independently 401s → draft survives → unlock → draft intact) and
  `tests/components/customer/merge-review.test.tsx`'s two new cases (isError with full data → no
  blank; isError with genuinely missing data → still blanks) are real: each would fail against the
  pre-fix `isError || !data`/`isError` code, not a tautology re-asserting the new branch in
  isolation. `staff-auth.spec.ts`'s matching real-browser case exercises the identical sequence
  through the real `client.ts`/React Query stack, not a shortcut.

**Verified the specific question asked: does showing stale cached data ever show the WRONG
person's data after a different user signs in via "discard"?** No. `handleDiscardAndReload`
(`IdleLock.tsx`) does `queryClient.clear(); window.location.reload();` — the cache clear removes
every cached query (not only the session key), and the immediately-following full browser reload
discards the entire JS heap, including any component holding onto a stale `account`/`customer`
object in memory. There is no code path between "person changed" being detected
(`handleReSignInSuccess`'s `else` branch, `setPersonChanged(true)`) and the user actually clicking
Discard where the underlying page is shown to the new person — the lock `Dialog` stays open and
opaque throughout that whole window, and the only exit from it (short of unlocking as the SAME
person) is Discard itself, which reloads. A re-authentication as the SAME person
(`handleReSignInSuccess`'s `if` branch) only ever refetches the session query, not the page's own
query, so a slightly-stale (but same-person, non-sensitive-to-identity) `account`/`customer` value
can persist briefly post-unlock — that is a freshness nuance, not a cross-person data leak.

## UnsavedChangesGuard (`web/components/account/UnsavedChangesGuard.tsx`)

Read the full file at `e3a3f88` line by line against every sub-item the caller listed.

- **`sentinelActiveRef`, the 500 ms `isProgrammaticNavRef` safety timeout, and the guarded
  `history.back()` on going clean are each correctly implemented for what they claim.**
  `beginProgrammaticHistoryNav()` (:95-101) is the single call site for arming
  `isProgrammaticNavRef`, used identically by both the confirmed-Leave replay (`history.go(-2)`,
  :214-216) and the stray-sentinel pop (`history.back()`, :129-131) — both are symmetric, both clear
  the timeout in `onPopState` on the expected event (:166-171). Traced whether the 500 ms timeout
  could fire during a **legitimate slow navigation** and swallow a **later, real** Back press: it
  cannot, because in both call sites `whenRef.current` is already `false` by the time
  `beginProgrammaticHistoryNav()` runs (explicitly disarmed at :210 for the confirm case; naturally
  `false` for the clean-save case, since that branch only runs when `when` just became `false`) —
  `onPopState`'s second check (`if (!whenRef.current) return;`, :172) means a late-arriving
  programmatic `popstate`, or a real Back that lands in the same window, is silently absorbed either
  way without opening a spurious dialog or re-pushing a sentinel. The only residual risk is a
  narrower **extension** of the already-accepted "double-Back" race (ADR-FE-013), not a new,
  independent failure mode: the browser's history position can end up one step further than
  intended if a genuine Back press races a slow, still-pending programmatic one. Not tested, but not
  a data-loss path either, since `whenRef` is false throughout.
- **[MEDIUM, not fully closed by this round] The `window.history.length > 1` heuristic (:117) is
  not a reliable proxy for "is there an entry behind the current position," because
  `history.length` counts the tab's ENTIRE session history — including entries AHEAD of the current
  position — not just entries behind it.** This is a real, reproducible gap, not the same one this
  round already fixed (ARCHITECTURE gate third pass LOW (a), "the tab's very first entry ever" —
  that specific case, `history.length === 1`, is correctly handled). The gap is a second flavour of
  the same underlying problem: reach the guarded page's own history entry at **index 0 of a
  longer-than-one-entry stack**, and the check still (wrongly) reads `history.length > 1` as "safe
  to push a sentinel." Concretely: a tab is opened directly on `/portal/account/profile` (index 0,
  length 1); the user clicks an in-app link to `/portal/account/notifications` (index 1, length 2);
  the user presses Back, landing on `/portal/account/profile` again — now at index 0, but
  `history.length` is still 2 (the forward entry to `/notifications` still exists and is reachable
  via Forward). If the profile form is dirty at this point, `history.length > 1` is `true`, so a
  sentinel IS pushed — but there is genuinely nothing at index −1. `pushState` from index 0 also
  silently discards the forward entry to `/notifications` (a real side effect worth knowing about on
  its own, though outside what was asked). The eventual confirmed-Leave replay (`history.go(-2)`)
  then targets an entry that does not exist, reproducing the exact "stuck `isProgrammaticNavRef`,
  next real Back/Forward silently swallowed" failure this same round's own 500 ms timeout was built
  to bound — the timeout does bound it (the ref does not stay stuck forever), but the dialog still
  never appeared for that one Back press, and a dirty form is lost with zero warning. Not tested:
  `unsaved-changes-guard.test.tsx`'s own first-entry test (`ARCHITECTURE gate third pass, LOW (a)`)
  only ever pins `window.history.length` to a fixed value for the whole test, never simulates
  "back-then-forward-then-dirty-again," so this shape is not exercised. `history.length` genuinely
  has no way to distinguish these two situations from JS — the platform provides no "current index"
  API — so a full fix likely means tracking navigation depth in-app (e.g., a per-tab counter written
  to `sessionStorage` on every `pushState`/`popstate` this guard itself causes or observes) rather
  than trusting `history.length` at all. Recording as MEDIUM to match the severity of the gap this
  round set out to close, since the failure mode and its consequence (silent data loss on the next
  Back press) are identical — only the trigger shape is narrower now.
- The stray-sentinel pop on a clean save (:121-132) and the "exactly one live sentinel" invariant
  are correctly implemented and tested (`unsaved-changes-guard.test.tsx`'s "exactly one live
  sentinel at a time" and "isProgrammaticNavRef can never stick" cases) — both are real regression
  tests: the first would fail against the pre-fix code (no `history.back()` call existed for the
  clean transition), the second uses fake timers to prove the ref genuinely resets rather than
  asserting a tautology about the timeout firing.

## `IdleLock.tsx` `lockedRef` synchronous set (:83-97, :225)

Correct as a narrowing fix for the specific race it targets (a `session_invalid` processed in the
gap between `lock()` being called and a passive effect's next run). `lockedRef.current = true`/
`= false` now sit directly in `lockNow`/`handleReSignInSuccess`, the only two call sites, so there
is no window where the ref lags the state transition it mirrors. Not itself sufficient against the
double-lock defaults-corruption bug above (that bug is about `IdleLockContext.tsx`'s own
`originalRefetchDefaultsRef`, a different piece of state with no equivalent idempotency guard) —
the two are adjacent but independent.

## `CsrfCookieDomain` — the new IP-literal rejection has real, checkable residual bypasses

Verified the actual regex (`AccountOptions.cs`, current pattern
`^$|^(?!\.?(\d{1,3}\.){3}\d{1,3}$)\.?[a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(\.[a-zA-Z0-9]
([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$`) against every shape in the caller's own list, with a
standalone regex run (not only by reading the pattern):

| Input | Accepted? | Note |
|---|---|---|
| `127.0.0.1`, `192.168.1.1`, `.127.0.0.1` | Rejected | the case this fix targets |
| `999.999.999.999` (out-of-range octets, ≤3 digits) | Rejected | matches the doc comment's own claim — value range is deliberately not checked, only shape |
| `010.0.0.1` (3-digit leading zero) | Rejected | still within `\d{1,3}` |
| `127.0.0.1.` (trailing dot) | Rejected | independently, by the base pattern's existing "no empty label" rule |
| **`0177.0.0.1`** (4-digit, leading-zero/octal-style octet) | **Accepted** | `\d{1,3}` caps each octet at 3 digits; a 4+-digit group falls outside the IP-shape lookahead entirely and is treated as an ordinary hostname label |
| **`0x7f.0.0.1`** (hex-style octet) | **Accepted** | a letter breaks the digit-only lookahead; the base pattern's alphanumeric label class accepts it as a normal-looking hostname |
| **`127.1`, `127.0.1`** (2-/3-part shorthand IPv4) | **Accepted** | the lookahead requires exactly four dot-separated groups |
| **`2130706433`** (bare decimal-integer IPv4) | **Accepted** | not four groups at all |
| `example123.test`, `10.example.test` (hostnames with digits) | Accepted | no over-rejection — confirmed, matches the new test cases added this round |

All four **Accepted** rows marked above are real gaps in the specific class of value this fix's own
doc comment and commit message describe as closed ("an IP literal is also rejected"): Chrome,
Firefox and Safari's shared WHATWG URL host-parsing algorithm treats every one of `0177.0.0.1`,
`0x7f.0.0.1`, `127.1`, `127.0.1` and `2130706433` as a valid, resolvable form of `127.0.0.1` — these
are not exotic or invalid inputs, they are documented alternate IPv4 notations every mainstream
browser accepts today. Practical impact stays LOW for the same reason the prior two passes already
established: a `Domain` value that does not domain-match the API's real host is simply dropped by
the browser's cookie jar, failing closed to a `403`, regardless of which of these forms an operator
typos in — this is a config-time misconfiguration guard, not a live security boundary. But the fix
is materially less complete than its own description claims. **LOW**, unchanged severity from the
prior pass's finding on the same option, since the underlying risk profile (an operator typo,
caught by *some* but not *all* IP-literal shapes) is the same size as before this round's own fix,
just narrower. Suggested fix, if worth closing fully: canonicalise/parse the value with a real IPv4
parser (accepting the same non-dotted-decimal forms a browser would) rather than pattern-matching
one specific shape, or simply document that only the dotted-decimal form is caught.

## Playwright real-api gating

Confirmed functionally sound, independent of the ARCHITECTURE gate's own read: `web/package.json`'s
`test:e2e:real-api` script string is `"E2E_REAL_API=1 playwright test --project=real-api"`
(confirmed via `git show e3a3f88:web/package.json`) — genuine POSIX-only inline env-var syntax,
which fails under `cmd.exe` (the default npm script shell on a Windows dev host with no
`script-shell` override). `web/playwright.config.ts`'s gating itself, though, is sound at the
level that actually matters: the `real-api` project entry is only ever pushed into the `projects`
array (`if (process.env.E2E_REAL_API === '1') { projects.push({name: 'real-api', ...}); }`) —
confirmed by reading the array construction directly, not merely the comment — so a bare
`npx playwright test` with no `--project` filter genuinely cannot run it (the project does not
exist in the array Playwright sees at all, not merely "is excluded by testIgnore"), closing the
prior pass's own MEDIUM about default execution. The comment immediately above the `REAL_API_SPEC`
constant (:20-21, unchanged this round) is now stale — it still says `--project=real-api` alone
runs the file, which is no longer true now that the project's own existence is gated — but the
NEWER comment two paragraphs below it (added by the third pass) already states the corrected
behaviour accurately, so this is a redundant/contradictory pair of comments, not a functional gap.
**LOW/suggestion**, matching the other gate's own finding on the same lines — worth deleting the
stale half of the comment pair.

## `docs/handover/HO-ACC-001.md` — confirmed no legitimate text lost vs `master`

This file is unchanged across `f380f3f..e3a3f88` itself (the fix happened in `d693b2b`, before this
pass's own range), but the caller asked for it to be confirmed at HEAD regardless, and the
ARCHITECTURE third pass had flagged real damage at the join. Did this the hard way, not by trusting
either gate's own prose: extracted the `| Amended | ...` cell from both `master`'s
`HO-ACC-001.md` and this branch's `e3a3f88` version and diffed them as plain strings. Result:
**`master`'s entire Amended cell (25,060 characters) is a byte-for-byte exact prefix of this
branch's cell (28,933 characters)** — the two are identical, character for character, right up to
"...unique-constraint violations too. See HO-ADM-001 §1 Amended (50). ", after which this branch's
own three appended items ((22)–(24), each under a correctly bolded, properly closed dated heading)
follow. Nothing from `master` was altered, reordered or dropped; the join is a clean append. A
naive raw `**`-occurrence count on the cell comes out odd (89) at first glance, which looks like an
unbalanced-bold defect — traced this fully and it is a false positive: the cell's own pre-existing,
`master`-inherited text contains a literal `tests/*/**.csproj` glob reference (item (11)/(18)(c)),
whose `**` is a wildcard, not markdown emphasis, and that single literal occurrence is the entire
explanation for the odd count. Every semantic bold heading in the cell, traced pair by pair, opens
and closes correctly. This independently confirms the ARCHITECTURE fourth pass's own "three-way
reconstruction" conclusion on the same file ("nothing legitimate was lost... a verbatim second copy
of items (10)–(20)" was what got removed) via a different method (byte-prefix comparison against
`master` rather than reconstructing from the merge base) — both arrive at the same, clean verdict.
The item-number collision the third pass had found is also confirmed fixed: items are (22)/(23)/(24)
in the current cell, with (24)'s own internal cross-reference reading "`AccountOptions.CsrfCookieDomain`
((22) above)" — correctly pointing at the item that actually introduces it. One genuine, minor,
pre-existing cosmetic leftover: the cell carries two `*(append further rows here)*` markers (one
mid-cell, one at the true end) — confirmed via `grep -c`; the mid-cell one is a leftover from before
this branch's own items moved to the end and should be deleted, but it is inert prose, not a content
or ordering defect. **LOW**, matching the other gate's own finding on the same file.

## Tests: `getByRole` → `getByLabel` switch — reasoning confirmed sound

Two new tests (`web/tests/e2e/staff-auth.spec.ts`'s ARCHITECTURE-gate-third-pass-HIGH case, and its
matching `idle-lock.test.tsx` unit test) assert a checkbox's checked state via
`getByLabel`/`getByLabelText` instead of `getByRole('checkbox', {name: ...})`, specifically *while
the lock `Dialog` is open*, with an explicit comment reasoning that `getByRole` "respects
`aria-hidden`, which Radix's own modal Dialog correctly applies to the rest of the page while open."
Confirmed this is accurate, not just plausible-sounding, on both testing surfaces the two new tests
actually use:
- **Playwright (`getByRole`)**: Playwright's role locators compute the same accessibility tree a
  screen reader would, which explicitly excludes any element hidden via an `aria-hidden="true"`
  ancestor unless `{ includeHidden: true }` is passed (not done here); `getByLabel` is a plain
  DOM/label-association query with no such filtering.
- **Testing Library (`getByLabelText`, used in the `vitest`/jsdom unit test)**: `getByRole` filters
  out elements inaccessible per ARIA semantics by default (the `hidden` option defaults to
  excluding them), while `getByLabelText` performs a `<label>`/`aria-label`/`aria-labelledby`
  association lookup with no accessibility-tree filtering at all.
- **Radix's own behaviour**: a modal `Dialog` (the default mode; `IdleLock`'s own `Dialog` carries
  no `modal={false}` override, confirmed by reading `IdleLock.tsx` directly) hides everything
  outside the dialog's own portal from the accessibility tree while open — this was already
  established and cross-checked against `components/ui/dialog.tsx` by an earlier pass of this same
  gate file, and nothing in this round changes that behaviour.

Given all three hold, the underlying checkbox genuinely would not be found by `getByRole` while the
dialog is open, and the switch to `getByLabel`/`getByLabelText` is the correct fix for a real query
limitation, not a workaround chosen for convenience. Both new tests are real regressions, not
tautologies: `staff-auth.spec.ts`'s case would fail against the pre-`e3a3f88` `IdleLockContext.tsx`
(no root-level pause existed for it to prove) and, per the HIGH finding above, is not actually
proving the pause works either — it proves the page-level `isError && !data` guard survives the
scenario, which it correctly does. `idle-lock.test.tsx`'s matching unit test is the same property,
isolated from a real browser.

## Static checks (`web/`), run at `e3a3f88` before the working-tree interference described above

- `npx tsc --noEmit` — clean.
- `npm run lint` (`eslint . --max-warnings 0`) — clean, no output.
- `npx prettier --check .` — "All matched files use Prettier code style!".
- `npx vitest run` — 57 files, 210 tests passed. One transient failure
  (`tests/components/auth/sign-in-form.test.tsx`, a rate-limit-countdown-text assertion) appeared
  under full-suite parallel load on the first run; re-ran that file alone (5/5 passed) and the full
  suite again (57/57 files, 210/210 tests passed) to confirm it is a timing flake unrelated to this
  diff — that file is untouched by `f380f3f..e3a3f88`.

## Backend build + unit tests (isolated worktree)

- `git archive e3a3f88` into a scratch folder
  (`.../0093ba3a-84aa-4812-bc4c-5ccd9a4d0372/scratchpad/acc2-cr4`, deleted after this run — confirmed
  removed below), `dotnet build CatEcommerce.sln -c Release`: **Build succeeded, 0 errors** (20
  `NU1900` warnings — the same unreachable internal GitLab NuGet feed as every prior pass; unrelated
  to this diff).
- Unit-test projects only, Release, `--no-build`:
  - `CatEcommerce.Domain.Tests` — 221/221 passed.
  - `CatEcommerce.Application.Tests` — 73/73 passed.
  - `CatEcommerce.Infrastructure.Tests` — 28/28 passed.
  - `CatEcommerce.IntegrationTests` not run (needs Postgres; out of scope per the caller's brief; the
    new `CsrfCookieDomainTests.cs` IP-literal cases live here and were read, not executed).

## Verdict

**FAIL.** The HIGH above (`StaffLockProvider`'s refetch-pause mechanism does not reach an
already-mounted query observer, so `CatProfileDataShell.tsx` — a real, reachable, draft-bearing
staff page — remains exactly as vulnerable to the ADDENDUM HIGH/third-pass draft-loss bug as before
this round's own centerpiece fix) is confirmed independently, via direct `@tanstack/query-core`
source inspection and the app's own layout composition, not merely cited from the concurrently-run
ARCHITECTURE fourth pass. The MEDIUM (double-lock corrupts the restored defaults, disabling
focus/reconnect refetch app-wide until a reload) is independently reproduced with a standalone
script against the shipped algorithm, matching the other gate's own finding exactly. Either one
alone would fail this pass; both are confirmed real. The `UnsavedChangesGuard`/`CsrfCookieDomain`/
`HO-ACC-001.md`/Playwright-gating items are real but non-blocking on their own (LOW/MEDIUM,
consistent with or narrower than prior-pass severities on the same code), and the page-level
`isError && !data` fix, the `lockedRef` synchronous-set fix, and the `getByRole`→`getByLabel` test
changes are all correct and soundly reasoned.

FINDINGS (this pass only; see ARCHITECTURE gate's own fourth pass for its full, overlapping set)

- [HIGH] `web/components/staff/IdleLockContext.tsx:69-97` + `web/app/_components/cat/
  CatProfileDataShell.tsx:51` — the round's own refetch-pause fix (toggling
  `queryClient.setDefaultOptions`) does not propagate to an already-mounted query observer (verified
  against `@tanstack/react-query`/`query-core` 5.103.2 source: `observer.setOptions()` only runs
  from an effect keyed on a value recomputed at the CONSUMING component's own render, and `IdleLock`
  deliberately keeps `{children}` mounted with a stable reference so it does not re-render on lock).
  `CatProfileDataShell.tsx` (HO-CAT-002, mounted inside `IdleLock` for every `/staff/cats/[catId]/**`
  route) still uses the unguarded `isError || !data` pattern, so a locked terminal on a cat
  edit/weights/vaccination form remains exactly as vulnerable to losing its draft as the
  staff-account page was before this round. Fix: pause `focusManager`/`onlineManager` directly (read
  by every observer at the moment of the real event, independent of render timing), or get an
  integrator-authorised `isError && !data` edit into `CatProfileDataShell.tsx` (narrower, and does
  not cover any future mounted-and-locked query).

- [MEDIUM] `web/components/staff/IdleLockContext.tsx:69-88` — `lock()` is not idempotent; a second
  call before an intervening `unlock()` (realistic: two concurrently-refetched queries on the same
  focus event both independently detect `session_idle_timeout` before the backend's "end session"
  side effect commits, one via `IdleLock`'s own `sessionError` effect, one via the generic
  `client.ts` handler) re-captures the ALREADY-paused `{refetchOnWindowFocus:false,
  refetchOnReconnect:false}` as the "original" state; the next `unlock()` then restores those `false`
  values instead of the true pre-lock ones, disabling focus/reconnect refetch **app-wide** (one
  shared `QueryClient` across staff and customer areas) until a full page reload. Reproduced with a
  standalone script against the shipped algorithm (see body above). Also confirmed: no
  unmount-cleanup effect exists, so a provider unmount while locked leaves the `false`/`false`
  defaults in place permanently too. Fix: guard `lock()` on `originalRefetchDefaultsRef.current ===
  null` before re-capturing, and add an unmount-cleanup effect mirroring `unlock()`.

- [MEDIUM] `web/components/account/UnsavedChangesGuard.tsx:117` — `window.history.length > 1` is not
  a reliable signal for "an entry exists behind the current position," since `history.length`
  counts forward entries too. A tab that reaches the guarded page via back-then-forward-then-dirty
  (a real, ordinary in-app navigation sequence, not a contrived one) can be at index 0 of a
  longer-than-one-entry stack, still reads `history.length > 1` as `true`, pushes a sentinel, and
  the eventual confirmed-Leave `history.go(-2)` then targets a non-existent index — reproducing the
  same "next real Back/Forward silently swallowed, dirty form lost with no dialog" failure this
  round's own 500 ms `isProgrammaticNavRef` timeout was built to bound (the ref itself is bounded;
  the missed dialog for that one Back press is not). Not tested. Fix: track navigation depth
  in-app (e.g. a per-tab `sessionStorage` counter maintained by this guard's own `pushState`/
  `popstate` handling) rather than trusting `history.length`.

- [LOW] `src/Infrastructure/CatEcommerce.Infrastructure/Configuration/AccountOptions.cs` —
  `CsrfCookieDomain`'s new IP-literal negative lookahead (`(?!\.?(\d{1,3}\.){3}\d{1,3}$)`) rejects
  the plain four-octet decimal-dotted form but not four browser-recognised alternate IPv4 notations,
  confirmed with a standalone regex run: a 4+-digit leading-zero/octal-style octet (`0177.0.0.1`), a
  hex-style octet (`0x7f.0.0.1`), 2-/3-part shorthand (`127.1`, `127.0.1`), and a bare decimal
  integer (`2130706433`) all still pass as an "ordinary hostname." No over-rejection of hostnames
  containing digits. Impact stays low (a non-domain-matching value fails closed at the browser's
  cookie jar regardless), but the fix is narrower than its own doc comment claims. Fix: parse with a
  real IPv4-form-aware check, or document that only the plain dotted-decimal shape is caught.

- [LOW] `web/package.json:18` — `test:e2e:real-api`'s `"E2E_REAL_API=1 playwright test
  --project=real-api"` is POSIX-only inline env-var syntax; fails under `cmd.exe`, this repo's own
  Windows dev hosts' default npm script shell. It is a manual-only script (§5.1), so low severity.
  Fix: a cross-platform form (e.g. `cross-env`) or a documented "run from bash" note.

- [LOW] `web/playwright.config.ts:20-21` — the comment above `REAL_API_SPEC` is now stale (claims
  `--project=real-api` alone runs the file; the project's own existence is now gated on
  `E2E_REAL_API=1`, confirmed correct by reading the array-construction code directly), contradicted
  by the newer, correct comment two paragraphs below it. Functionally harmless — delete the stale
  half.

- [LOW] `docs/handover/HO-ACC-001.md` — the Amended cell carries two `*(append further rows here)*`
  markers (one stray, mid-cell, left over from before this branch's own items moved to the cell's
  end); confirmed the join itself lost no `master` content (byte-exact prefix match, see body
  above). Cosmetic; delete the mid-cell marker.

SUGGESTIONS

- (carried, still open) `web/tests/e2e/real-api-verification.spec.ts`'s module-scope `Date.now()`
  seed remains shared across concurrent workers in principle; structurally excluded from every
  default-invoked project, so the practical risk stays closed.

SCOPE CHECK
- Files touched in this round (`87a9e17`, `37b34bd`, `e3a3f88`) match the areas the caller named:
  `IdleLockContext.tsx`/`IdleLock.tsx` (the refetch-pause mechanism and the `lockedRef` race),
  `UnsavedChangesGuard.tsx` + `web/docs/decisions.md` (the four named sub-fixes), the three
  page-level `isError && !data` sites, `AccountOptions.cs` + `CsrfCookieDomainTests.cs` (IP-literal
  rejection), `eslint.config.mjs` (comment correction only), and the matching new/updated test
  files. No file outside that description was touched by this round's own three commits.

---

Third pass (delta 49b4d50..2e20e73). Combined CODE_REVIEW/SECURITY delta gate for the fix round
that followed the second pass recorded below (`49b4d50` is that pass's own verdict commit; the
range therefore starts at `fccdef8`, the first real code commit after it, and ends at `2e20e73`,
the current branch head). `108e0db` merges `master` at `eae3d7b` (bringing in HO-ADM-001 and
`integrator-host`) into `ho/ACC-002` — reviewed for merge damage only, not re-reviewed as new
HO-ACC-002 content; everything else in the range is a real HO-ACC-002 gate-fix commit. SECURITY and
CODE_REVIEW second passes both PASSED on the prior base (`docs/gates/HO-ACC-002-{SECURITY,
CODE_REVIEW}.md`, both dated 2026-09-26) and are not re-scored here except where this round's own
commits touch the same code.

**Verdict changed from PASS to FAIL while finalising this file — see ADDENDUM below.** Everything
in "FIX ROUND" and the original FINDINGS/SUGGESTIONS was drafted first, with a PASS verdict, before
I discovered (a) the worktree had moved on to a further, concurrently-run ARCHITECTURE third pass
on this identical commit range, and (b) that gate's own HIGH finding is real, independently
confirmed, and squarely inside what this pass was asked to check. Both the original material and
the addendum are kept, in the order they were actually produced, rather than silently smoothed over.

MERGE COMMIT (`108e0db`) — damage check only

- Two real conflicts, both resolved cleanly: `docs/handover/HO-ACC-001.md` (prose-only, both sides'
  wording combined) and `tests/CatEcommerce.IntegrationTests/Endpoints/
  DuplicateCustomerGroupsEndpointTests.cs` (a comment-only conflict; the executable line —
  `pageSize=100` — was already identical on both sides, since `0f12fc9` on this branch and the
  integrator's own paging-cap commit landed on the same value independently). Confirmed no leftover
  `<<<<<<<`/`=======`/`>>>>>>>` marker anywhere in the tree (`grep -rn` across `.cs`/`.md`/`.ts`/
  `.tsx`, repo-wide — none). Read the merged `GetDuplicateCustomerGroupsQueryHandler.cs` in full:
  the integrator's deterministic `ORDER BY` (audit-timestamp descending, tie-broken on the group's
  smallest member id) and HO-ACC-002's own `RecordStatus`/`RegistrationChannel`/`Cats` projection
  additions from the second-pass round coexist correctly in the merged file — no logic lost from
  either side.

FIX ROUND — read commit by commit

- `fccdef8` (idle lock survives the real API ending the session) — real, correctly targeted fix for
  the exact ARCH HIGH the caller named. `StaffLockProvider` (`IdleLockContext.tsx`) is lifted to
  wrap `StaffSessionGuard` itself in `(staff)/layout.tsx`, not placed inside `IdleLock` as before —
  the only placement that lets the guard see `locked` (a context `IdleLock` provides can never
  reach its own parent). `StaffSessionGuard.tsx`'s `authenticated`/`unauthenticated` booleans both
  now gate on `locked` the same way they already gated on `StaffSessionIdleTimeoutError`, so the
  redirect effect never fires and `children` (`IdleLock` and everything under it) stays mounted
  regardless of what the session query reports while locked. `IdleLock.tsx`'s own `client.ts`
  session-error handler reads `lockedRef` (a ref, not `locked` in the closure, so the handler is
  not re-registered on every lock/unlock) and makes a `session_invalid` a no-op while already
  locked — no cache clear, no redirect; only `handleReSignInSuccess` (matching
  `lockedStaffIdRef.current`) unlocks. Verified the specific properties the task asked for:
  - **The page stays mounted, hidden behind an opaque overlay.** `children` renders unconditionally
    before the `Dialog`; the overlay passes `overlayClassName="bg-surface-page"` (opaque in both
    themes, `tailwind-merge` genuinely replaces `bg-ink/40` rather than stacking it — confirmed
    against `components/ui/dialog.tsx`'s `cn()` call and the SECURITY second pass's own node-level
    check of the merged class). The `Dialog` gets no `modal={false}` override anywhere, so Radix's
    default modal behaviour applies: focus-trapping (`FocusScope`) and hiding the rest of the DOM
    from the accessibility tree while open are both Radix's own default `Root` behaviour for this
    primitive, not something this component has to (or does) implement itself — `onEscapeKeyDown`/
    `onPointerDownOutside`/`onInteractOutside` are all separately `preventDefault()`-ed, and
    `onOpenChange={() => {}}` means nothing Radix does internally can close it either. There is no
    code path by which a keyboard `Tab` or a pointer click can reach the mounted-but-hidden page
    while `locked` is true.
  - **No request can succeed with an ended session.** This is inherent to the real API's own
    behaviour (a request against an ended session 401s regardless of what the client believes), and
    the fix's job is only to make sure the client never *misreads* that 401 as a fresh sign-out
    while locked — confirmed it does not: `session_invalid` while locked clears nothing and
    redirects nowhere, and the activity-heartbeat effect explicitly skips entirely while `locked`
    (`if (locked) return;`), so no heartbeat is even attempted once the terminal is locked.
  - **A real `session_invalid` while NOT locked still clears the cache and redirects.** The same
    handler's `else` branch (`if (lockedRef.current) return; queryClient.clear(); ... router.replace
    (...)`) is unchanged for the not-locked case, and `CustomerSessionGuard`'s own separate handler
    (a different route group) is untouched by this commit — confirmed by reading both handlers, not
    only `IdleLock`'s.
  - Tests are real, not tautologies: `idle-lock.test.tsx`'s new "ARCH gate HIGH (second pass)" case
    seeds a focus-triggered `session_idle_timeout` (locks), then overrides the session endpoint to
    `session_invalid` and refetches again (stays locked, no redirect), then fires a *page* query's
    own `session_invalid` through the real `client.ts` handler (still locked), then re-authenticates
    as the same `staffAccountId` (unlocks, draft intact throughout) — each assertion
    (`replace).not.toHaveBeenCalled()`, the draft's literal value, `dialog` presence) would fail
    against the pre-fix code, since the pre-fix guard had no `locked` signal to consult at all.
    `staff-auth.spec.ts` exercises the same sequence in a real browser via
    `window.__forceStaffIdleExpired` (a dev-only hook, see `ed412e9`'s review below) and a real
    `input` event for the activity-heartbeat path. **See the ADDENDUM below: this test's own
    `TestForm` fixture has no query of its own, which is exactly why it could not have caught the
    HIGH finding recorded there.**
  - `mocks/handlers/areas/accounts.ts`'s `endStaffSessionForIdleTimeout` now deletes the session and
    expires the cookie on the FIRST idle-timeout response, matching the real API's
    `SessionAuthenticationMiddleware.EndForIdleTimeout`/`ExpireStaffSession` — correctly makes every
    later mock call report `session_invalid`, not `session_idle_timeout` again, which is what
    actually exercises the new code path (before this, the mock's own `forcedIdleExpired` flag never
    cleared, so the gate this whole commit fixes was never actually reachable through MSW).
  - The commit message discloses a real-API-only verification step: "seeded a throwaway staff
    administrator in `catecommerce_dev` ... (not committed)". Checked specifically, per the caller's
    instruction: `git status --porcelain --ignored` on the worktree shows only build output and two
    already-`.gitignore`d local files (`src/Api/CatEcommerce.Api/appsettings.Development.json`,
    `web/.env.local`); read both in full. Neither contains the seeded account's identifier or
    password, or any credential-shaped string — `appsettings.Development.json` has only the local
    Postgres connection string (`postgres`/`postgres`, the default dev-only credential already in
    every other worktree, not this account) and unrelated dev settings; `web/.env.local` has only
    the two `NEXT_PUBLIC_*` URLs. `git log --all` on `appsettings.Development.json` (tracked only as
    of the very first two commits, then deleted in favour of a `.example` file) shows nothing
    matching either. No committed or untracked file in the worktree carries the throwaway account's
    credentials.

- `a784670`/`6b3ac06` (eslint config severity + scope recording) — correct and minimal. `warn` →
  `error` on both `no-restricted-imports` rules now that `--max-warnings 0` is on `npm run lint`;
  the hooks-import ban's `patterns` now also catches a relative import
  (`**/lib/hooks/*`/`**/lib/hooks`, not only the `@/lib/hooks/*` alias); `SignOutButton.tsx`'s
  override no longer turns the whole rule off (which had silently re-permitted a raw
  `useQuery`/`useMutation` import there too) — it now repeats the `useQuery`/`useMutation` `paths`
  ban explicitly and only lifts the hooks-folder `patterns` ban. `6b3ac06` is documentation-only
  (records the eslint edit and several out-of-§13 files in HO-ACC-002 §1 Amended, fixes a stale
  `web/README.md:176` line) — no code risk.

- `3cd8c67` (`AccountOptions.CsrfCookieDomain` startup validation) — real hardening, correctly wired
  through the same `ValidateDataAnnotations().ValidateOnStart()` pipeline `StaffIdleTimeoutMinutes`
  already uses (confirmed: `WithCsrfCookieDomain(...)` + accessing `.Services` is what actually
  triggers validation in a `WebApplicationFactory`, so `Assert.ThrowsAny<Exception>(() => _ =
  invalidFactory.Services)` is a real assertion, not a no-op). Checked the regex itself
  (`^$|^\.?[a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(\.[a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$`)
  against every shape the caller asked about:
  - Scheme (`https://.example.test`), port (`.example.test:5005`), path (`.example.test/path`),
    leading/trailing/internal whitespace, an empty label (`..example.test`), and a wildcard
    (`*.example.com`) are all correctly **rejected** (verified independently with a standalone regex
    run, not only by reading the pattern).
    - Overlong labels are correctly rejected at the DNS 63-octet limit: a 64-character first label
    fails (the fixed-length `{0,61}` bounds each label at 1–63 chars); a 63-character label passes.
  - Empty (`""`), `.example.com`, `example.com` all correctly **accepted**.
  - **Not rejected: an IP literal** (`192.168.1.1`, `127.0.0.1` both pass — confirmed with the same
    standalone regex run). The caller's own worked list of shapes to check named this one
    specifically. Digits are within the allowed label character set, so a dotted-decimal string is
    syntactically indistinguishable from a hostname to this pattern. Practical impact is small — the
    SECURITY second pass's own analysis already established that any value which doesn't
    domain-match the API's real host gets silently dropped by the browser's cookie jar, failing
    closed as a `403`, and that holds for an IP literal too — but it is a real, one-line gap in a
    check whose own doc comment and commit message describe it as closing exactly this class of
    operator-typo mistake. **LOW.**
  - Test count: 15 total in `CsrfCookieDomainTests.cs` (4 pre-existing `Domain=` wire-shape tests
    from the second pass + 11 new: 7 invalid-value cases, each asserting host startup itself throws,
    and 4 valid-value cases asserting it does not). All 11 new ones are real: none of them would
    pass against the pre-fix code (no `[RegularExpression]` attribute existed, so nothing would have
    thrown for any of the 7 invalid cases).

- `7a24271`/`01e0db3` (`UnsavedChangesGuard` intercepts Back/Forward; same-origin-only link replay)
  — the same-origin-only replay half is correct and adequately tested (`anchor.origin !==
  window.location.origin` bails before `preventDefault`, verified with a dedicated cross-origin-link
  test). The Back/Forward half is a genuine improvement over the prior "not handled at all" gap this
  round set out to fix, but the sentinel-history-entry implementation has two real, undisclosed
  edge-case bugs beyond the two the component's own comment and ADR-FE-013 do disclose (the
  double-Back race and "Forward after a confirmed Leave is not specially handled"). Read
  `UnsavedChangesGuard.tsx` line by line against every edge case the caller listed:
  - **First page in history — not handled, and this is not merely theoretical.** `onConfirm`'s
    `history` branch always calls `window.history.go(-2)` unconditionally. If the guarded page (in
    practice, `/portal/account/profile` or `/portal/account/notifications`) is the first entry in
    the tab's own session history — a direct load, a bookmark, a refresh, or a deep link from an
    SMS/email, all realistic ways to land on these two pages — then by the time `onPopState` has
    re-pushed the sentinel and the user confirms, the current position is index 1 with nothing
    before index 0; `go(-2)` targets index −1, which does not exist. The browser does nothing: no
    navigation, and critically, no `popstate` fires. `isProgrammaticNavRef.current` was just set to
    `true` in anticipation of that `popstate`, and nothing ever resets it. **The very next real
    Back or Forward press — on this page or, since the flag is a plain module-level `useRef` with
    no reset path, any later navigation this same mounted instance sees — is silently swallowed**:
    `onPopState`'s first check (`if (isProgrammaticNavRef.current) { ...; return; }`) treats it as
    the guard's own leftover echo, resets the flag, and returns without re-pushing a sentinel or
    opening the dialog. If the form is still dirty at that point, this is exactly the data-loss
    failure NFR-USA-5 and this whole feature exist to prevent, reached silently. Not tested anywhere
    (the unit test mocks `window.history.go` so it never observes the no-op case; the e2e tests
    always visit an intermediate page first, so a prior entry always exists).
  - **Dirty → clean without navigating leaves a permanent stray history entry — and this is the
    ordinary happy path, not an edge case.** Both call sites pass a prop that goes false on a
    successful save (`ProfileForm.tsx`: `when={isDirty && !isSubmitSuccessful}`;
    `NotificationPreferenceMatrix.tsx`: `when={isDirty}`, and `isDirty` clears on a react-hook-form
    `reset()` after save). The effect that reacts to `when` going false only does
    `sentinelPushedRef.current = false` — it never calls `history.back()` (or any equivalent) to pop
    the duplicate entry `pushState` added while dirty. The entry stays in the browser's session
    history permanently. Concretely: a customer opens Profile, edits their name, saves successfully
    (the ordinary, most common flow this screen exists for) — one extra, invisible, same-URL history
    entry now sits between them and wherever they came from. The first real Back press they make
    afterwards lands on that duplicate entry; since the URL doesn't change, this will generally look
    like nothing happened, requiring a second Back press to actually leave. Editing and saving
    again in the same session stacks up a second such entry, and so on. Not disclosed in
    ADR-FE-013's "Consequence, explicitly accepted" paragraph (which names only the double-Back race
    and unhandled Forward-after-Leave). Not tested anywhere — no unit or e2e test saves the form and
    then presses Back.
  - **Double popstate (rapid double Back) and confirm-then-navigate:** the double-Back race is the
    one edge case that genuinely is disclosed, accurately, in both the component's own comment and
    ADR-FE-013 ("can be defeated by pressing Back twice in rapid succession before React has a
    chance to re-push the sentinel"). Confirm-then-navigate-again in the ordinary case (a prior
    entry does exist) is correct: `go(-2)`'s resulting `popstate` is consumed by the
    `isProgrammaticNavRef` guard exactly once, the dialog does not reappear, and the browser lands
    on the real prior page.
  - **MEDIUM** (combining the two undisclosed, reproducible gaps above — same root cause: the
    sentinel's lifecycle is only ever pushed, never correctly torn down or defended against a
    shorter-than-expected history). Suggested fix: on `when` transitioning to `false` without the
    guard's own replay having run, pop the sentinel for real (`isProgrammaticNavRef.current = true;
    window.history.back();`, guarded the same way the confirm path already guards its own
    `popstate`) instead of only resetting the tracking ref; and bound the "stuck true" window after
    a `go(-2)` that might not fire a `popstate` — for example resetting
    `isProgrammaticNavRef.current` from a short `setTimeout` fallback as well as from `onPopState`,
    so a failed `go(-2)` cannot permanently disable the guard for the rest of the component's
    mounted lifetime.

- `ed412e9` (Playwright project split; `window.__invalidateQueries`/`__apiBaseUrl` dev-only hooks) —
  structural exclusion confirmed correctly, not just re-worded. `playwright.config.ts`'s `chromium`
  and `mobile-360` projects both carry `testIgnore` for `real-api-verification.spec.ts`; a third
  `real-api` project `testMatch`es only that file. `package.json`'s `test:e2e` script passes
  `--project=chromium --project=mobile-360` explicitly, so that script can never run it; only the
  separate, never-invoked-by-default `test:e2e:real-api` script (`--project=real-api`) does. **See
  the ADDENDUM below: the ARCHITECTURE third pass independently flags that a bare `npx playwright
  test`, with no `--project` filter at all, still runs every project including `real-api` — true,
  and a real gap in the comment's own "not run at all unless" claim, though not one this pass's own
  brief asked to check (only "never in production bundles" was asked, and that part is correct).**
  The `window.__apiBaseUrl`/`window.__invalidateQueries` hooks (`app/providers.tsx`, actually
  committed in `fccdef8` since it lives in the same effect) are gated on `process.env.NODE_ENV !==
  'production'`. **Verified the gate directly, not only by reading the condition**: ran a real
  production build (`npx next build` with `NEXT_PUBLIC_USE_MOCKS=false`) from this worktree and
  grepped every emitted client chunk (`.next/static/chunks/*.js`) for `__invalidateQueries`,
  `__apiBaseUrl`, `__forceStaffIdleExpired` and `mockServiceWorker`/`areas/accounts` — zero matches
  in any shipped chunk (the strings exist only inside `.next/cache/webpack/*-production/*.pack`,
  which is Next's own build cache, never served to a browser). Next inlines `process.env.NODE_ENV`
  as a literal at build time, so `"production" !== "production"` is dead code eliminated entirely,
  consistent with what the grep shows. Confirmed correct.

- `0f12fc9` (client-side pagination clamps; MSW-side 400s) — defensive, harmless, correctly scoped.
  `getStaffAccounts`/`getDuplicateCustomerGroups` clamp with `Math.min(param ?? default, MAX)`
  before building the query string; no caller in the app sends a value near either cap today, so
  this is pure future-proofing, not a workaround for a live bug. The MSW `validatePagingParams`
  helper mirrors the real API's forthcoming 400 shape and is explicitly marked "not yet visible
  from this worktree... not invented in place of the real contract" — an honest placeholder, not a
  fabricated contract.

- `2e20e73` (Back/Forward e2e test locator fix) — read as a real bug fix, not a flakiness patch.
  `getByLabel(/^full name$/i)` (anchored regex) cannot match `ProfileForm.tsx`'s "Full name" label,
  whose accessible name carries trailing content from its own `<span aria-hidden="true">*</span>`
  required-field marker — confirmed by reading `ProfileForm.tsx`'s label markup directly; this would
  time out deterministically regardless of load, matching the commit's own three-separate-runs
  diagnosis. The fix (`getByLabel('Full name')`, a plain-string substring match) matches the exact
  form `accounts-auth.spec.ts`'s E-C2/E-C3 already use successfully against this same field — not a
  new, unverified pattern. Not executed here (Playwright is out of this gate's scope per the
  caller's brief), but the fix is legible and consistent with working precedent elsewhere in the
  same file set.

STATIC CHECKS (web/), re-run at `2e20e73`
- `npx tsc --noEmit` — clean.
- `npx eslint .` — clean, no output at all.
- `npx prettier --check .` — "All matched files use Prettier code style!".
- `npx vitest run` — 56 files, 198 tests passed (up from 55/186 at the second pass, consistent with
  the new idle-lock/unsaved-changes-guard/pagination-caps test additions in this round).
- `npx next build` (production) — succeeded; see `ed412e9`'s review above for what this run was
  actually used to verify.

BACKEND BUILD (isolated worktree)
- `git archive 2e20e73` into a scratch folder (deleted after this run — confirmed removed),
  `dotnet build CatEcommerce.sln -c Release`: **Build succeeded, 0 errors** (20 `NU1900` warnings —
  the same unreachable internal GitLab NuGet vulnerability feed as every prior pass; unrelated to
  this diff).
- Unit-test projects only, Release:
  - `CatEcommerce.Domain.Tests` — 221/221 passed.
  - `CatEcommerce.Application.Tests` — 73/73 passed.
  - `CatEcommerce.Infrastructure.Tests` — 28/28 passed.
  - `CatEcommerce.IntegrationTests` not run (needs Postgres; out of scope per the caller's brief; no
    integration or Playwright run this pass, per the caller's explicit instruction).
  - (The Application/Infrastructure test projects needed a clean `bin`/`obj` rebuild after the
    solution-wide build — a stale-artifact issue in this run's own scratch folder, not a code
    defect; both passed cleanly once rebuilt.)

ADDENDUM — found while finalising this file; changes the verdict

While preparing to write this file I found the worktree had moved on:
`docs/gates/HO-ACC-002-ARCHITECTURE.md` now carries a third pass (commit `2b0f9fa`, STATUS FAIL)
reviewing this exact same range (`49b4d50..2e20e73`), run by a separate agent instance concurrently
with this review (a shared, live worktree, not one isolated per gate run — worth the coordinator's
attention on its own), and a further, still-uncommitted change to `web/playwright.config.ts` was
sitting in the working tree at the moment I finished (not mine — I never wrote to that file; left
it untouched, per this gate's read-only-on-source rule). That gate's own HIGH finding is squarely
inside the scope this pass was asked to cover ("idle lock rework ... correctness: while locked, the
page's data stays mounted"), and I did not independently catch it while drafting the material above.
Rather than let two gate files on the same commits silently disagree, I read the cited file directly
and confirmed it myself:

- [HIGH] `web/app/(staff)/staff/admin/staff-accounts/[staffAccountId]/page.tsx:34-38` (confirmed by
  direct inspection, not merely citing the other gate). The relevant lines:

  ```
  if (isError || !account) {
    return (
      <ErrorState description="We couldn't load this account just now." onRetry={() => refetch()} />
    );
  }
  ```

  This unconditionally swaps `<StaffAccountDetail>` — and whatever `selectedRoleIds` draft it holds
  — for an error page whenever `useStaffAccount`'s query enters an error state, with no check on
  whether the terminal is currently locked. This is a second, real trigger for the same US-ACC-08
  AC2 / ADR-FE-010 draft-loss bug `fccdef8` was written to fix, one layer below where that commit's
  own changes (`StaffLockProvider`/`StaffSessionGuard`/`IdleLock`) operate: once the real API ends
  the session on the first idle-timeout detection (exactly the scenario `fccdef8` targets), a
  `refetchOnWindowFocus` refetch of this page's own `['staff-admin','staff-account',id]` query gets
  `401 session_invalid` (one retry allowed, then `isError: true`), and this page-level branch
  discards the form regardless of `locked` — the modal overlay hides this from the user until they
  unlock, at which point the edit is simply gone. `IdleLock` itself stays correctly mounted
  throughout (the review of that component above is accurate as far as it went); the gap is that
  nothing shields a locked page's OWN error-rendering branches from a session-ended 401, only the
  guard/lock composition around it — a genuine layering gap, not a mistake in the lines this pass
  read most closely.
  - Why this pass's own testing did not catch it: `idle-lock.test.tsx`'s `TestForm` fixture (used
    by every case reviewed above) is a plain `<input>` with no query of its own, and
    `staff-auth.spec.ts`'s scenario runs on `/staff/customers/new`, which has no page query to error
    out either. Neither exercises a locked page that also owns a data-fetching query with a draft
    depending on that query's result — which is exactly `StaffAccountDetail`'s own shape.
  - Fix direction (implementer's call, matching the ARCHITECTURE gate's own note): render cached
    `data` when present even if `isError` (`isError && !data`, not `isError || !account`), or gate
    focus-refetch/rendering of staff page queries on `useStaffLockState()` while locked.
  - This is a real, unmitigated data-loss bug in the exact property this pass's own brief asked to
    verify ("while locked, the page's data stays mounted... hidden behind the opaque overlay") —
    the data does stay mounted for a page with no query of its own, but not for one that has to
    refetch and can fail. Per this gate's own rubric, a confirmed data-loss risk is Critical-tier,
    not merely a should-fix. Recorded here as HIGH, matching the ARCHITECTURE gate's own severity
    label for the identical finding, since I have no basis to score the same, once-confirmed bug
    differently between two gate files reviewing the same commits.

This changes the verdict. The material above this addendum was drafted to a PASS conclusion; that
conclusion no longer holds once this finding is counted. STATUS for this file is **FAIL**.

FINDINGS

- [HIGH] see ADDENDUM above — `web/app/(staff)/staff/admin/staff-accounts/[staffAccountId]/page.tsx`
  loses an in-progress role-selection draft when the terminal is locked and this page's own
  `useStaffAccount` query 401s (`session_invalid`) after the real API has already ended the session,
  because its `isError || !account` branch unconditionally unmounts `StaffAccountDetail` with no
  regard for `useStaffLockState()`. Not part of this round's own diff (the page is unchanged by
  `fccdef8`..`2e20e73`), but a real, confirmed gap in the property this pass was specifically asked
  to verify for the idle-lock rework, and — per this gate's own rubric — a data-loss risk is
  Critical-tier, so it blocks this gate rather than merely being noted.

- [MEDIUM] `web/components/account/UnsavedChangesGuard.tsx` — the sentinel-history-entry technique
  added this round for Back/Forward has two real, undisclosed gaps beyond the two ADR-FE-013 does
  name: (1) `history.go(-2)` on confirm assumes at least two prior entries exist; when the guarded
  page is the first entry in the tab's session history (a realistic way to reach
  `/portal/account/profile` or `/portal/account/notifications` — direct load, bookmark, deep link),
  the call is a silent no-op, no `popstate` ever fires to reset `isProgrammaticNavRef`, and the
  *next* real Back/Forward press on this mounted instance is then silently swallowed with no
  confirm dialog and no re-pushed sentinel, so a dirty form is lost with zero warning — the exact
  outcome this feature exists to prevent. (2) The sentinel entry pushed while dirty is never popped
  when the form becomes clean without navigating (`sentinelPushedRef.current = false` only resets
  internal bookkeeping); since both `ProfileForm.tsx` (`isDirty && !isSubmitSuccessful`) and
  `NotificationPreferenceMatrix.tsx` (`isDirty`) go clean on an ordinary successful save, every
  normal edit-and-save leaves one permanent, invisible duplicate entry in browser history, and
  repeated edits stack up more of them — each one absorbing one Back press as an apparent no-op
  before the user actually leaves. Neither gap is tested (the unit test mocks `history.go` so it
  never observes the no-op case; no test saves the form and then presses Back) or disclosed in
  ADR-FE-013's "Consequence, explicitly accepted" paragraph, which names only the double-Back race
  and unhandled Forward-after-Leave. Fix: pop the sentinel for real when `when` goes false without
  the guard's own replay (`isProgrammaticNavRef.current = true; window.history.back();`, guarded the
  same way the confirm path already is), and bound how long a failed `go(-2)` can leave
  `isProgrammaticNavRef` stuck `true` (e.g. also reset it from a short `setTimeout` fallback, not
  only from `onPopState`).

- [LOW] `src/Infrastructure/CatEcommerce.Infrastructure/Configuration/AccountOptions.cs` —
  `CsrfCookieDomain`'s new `[RegularExpression]` (added this round to close the SECURITY gate's own
  "optional hardening" item) rejects every malformed shape it set out to catch (scheme, port, path,
  whitespace, an empty label, a wildcard, an overlong label) but does not reject an IP literal
  (`192.168.1.1`, `127.0.0.1` both pass) — digits are within the allowed hostname-label character
  set, so a dotted-decimal string is syntactically valid under this pattern. Impact is limited (the
  SECURITY second pass already established that a non-domain-matching value just gets dropped by
  the browser, failing closed), but the commit's own framing describes this as closing the
  operator-typo class generally. Fix: add a negative lookahead for an all-numeric-labelled value
  (e.g. `(?!(\d{1,3}\.){3}\d{1,3}$)`) if IP-literal values should be treated the same as the other
  rejected shapes.

SUGGESTIONS

- (carried from the second pass, still open) `web/tests/e2e/real-api-verification.spec.ts` —
  module-scope `Date.now()`-derived `unique` is still shared across concurrent workers; now
  structurally excluded from the default parallel run for every project this repo's own npm scripts
  invoke (`ed412e9`), which removes the practical risk, but the seed itself is unchanged. (The
  ARCHITECTURE third pass separately notes a bare `npx playwright test` with no `--project` filter
  still runs the `real-api` project too — a real comment inaccuracy, not something this pass's own
  brief asked to verify.)

NOTES

- No superseded-architecture symbol reintroduced, no controller holding a repository/DbContext, no
  `202` on a command, no `AddServiceBusProvider`, no client-side authorization decision, no
  token/secret in a committed or untracked file (including the specifically-checked throwaway staff
  account), no disabled CSRF path, no dev/test hook reachable from a production bundle (verified by
  building one) — none of those apply here.
- The reason STATUS is FAIL is the single HIGH finding in the ADDENDUM, which is a confirmed
  data-loss bug directly on point for what this pass was asked to verify, corroborated independently
  by the concurrently-run ARCHITECTURE third pass on the identical commit range (which also FAILED,
  for this reason plus its own separate BLOCKER over an unrecorded `package.json` scope change — see
  that gate file; not re-litigated here since it is outside this gate's own remit). The MEDIUM and
  LOW findings above are real and should also be fixed, but neither would independently have failed
  this gate — they are both genuine partial improvements over the second pass's own findings, not
  regressions.
- This file's own history is itself worth noting: the "FIX ROUND" material above was drafted to a
  PASS conclusion before the addendum's discovery. It is kept rather than rewritten, both because it
  remains an accurate, independently-useful line-by-line review of the six commits it covers, and
  because silently editing a already-reasoned PASS into a FAIL without showing the work would be
  exactly the kind of unverifiable gate record the caller's own brief warns against.

SCOPE CHECK
- Files touched in this round match the areas the caller named: `StaffLockProvider`/
  `StaffSessionGuard`/`IdleLock` (idle lock rework), the MSW mock, `eslint.config.mjs`,
  `CsrfCookieDomain` validation + its tests, `UnsavedChangesGuard` + `web/docs/decisions.md`,
  `playwright.config.ts` + the dev-only hooks in `app/providers.tsx`, `web/lib/api/{staff-admin,
  staff-customers}.ts` pagination clamps, and the one e2e test locator fix — plus the `master` merge
  (reviewed for damage only, per the caller's brief) and the handover/doc records for each fix. No
  file outside that description was touched by this pass's own review activity (the ADDENDUM's
  finding concerns a file this round did NOT touch — see that section for why it is still recorded
  here).

---- SECOND PASS (superseded, kept for the record) ----
GATE: CODE_REVIEW
HANDOVER: HO-ACC-002
COMMITS: f90d465 13612b1 c39d491 a18e58d 4453214 381475c a548509 20fad47 ad7120f 7865ee5 aa47843 d04a509 c852ebd 55a4223 8aa2eeb 3dd9114 6e3e46a 88c2591 26fab00 ac8c519
DATE: 2026-09-26
STATUS: PASS

This is the second pass. It covers branch `ho/ACC-002` at `ac8c519`, reviewed as
`git diff 0465912...ac8c519` (0465912 is the clean master merge base; f90d465 is gate-fix commit
1/N and is included, since it carries the ARCH gate HIGH 1/HIGH 2 rework the caller asked to be
checked) — 20 fix commits, 118 files. Note: the branch head has since moved (integrator verdict
commits `b84bf5a`, `2b7ec40`); this review's range stays `f90d465..ac8c519` as instructed and does
not cover anything past `ac8c519`. The prior CODE_REVIEW pass (this file, overwritten) reviewed
`5e1f120` and PASSED WITH FINDINGS (2 MEDIUM, 3 LOW, 2 suggestions); the ARCHITECTURE and SECURITY
first passes (`docs/gates/HO-ACC-002-ARCHITECTURE.md`, `-SECURITY.md`) FAILED on the same base and
are re-read here for context, not re-scored (ARCHITECTURE/SECURITY re-verification is those gates'
own job on their own second pass).

FIRST-PASS CODE_REVIEW FINDINGS — STATUS

- [MEDIUM] `web/docs/decisions.md` missing — CLOSED (6e3e46a). ADR-FE-001…013 now recorded,
  including the corrected ADR-FE-002 (cross-origin fetch, not a same-origin proxy) and a new
  ADR-FE-013 for the `UnsavedChangesGuard` rewrite (11/N).
- [MEDIUM] `MergeReview.tsx`/`DuplicateGroupList.tsx` claimed an escalation with no artifact —
  CLOSED (26fab00, ac8c519). The gap itself is closed, not merely re-worded: the backend
  (`GetStaffCustomerQueryHandler`, `GetDuplicateCustomerGroupsQueryHandler`) now returns
  `StaffCustomerViewModel.Cats` and `DuplicateCustomerMemberViewModel.RecordStatus`/
  `RegistrationChannel`; the frontend renders both (`MergeConfirmDialog` lists cats by name,
  `DuplicateGroupList` shows `CustomerStatusBadge` + registration channel). Two new backend
  integration tests prove the fields are genuinely per-member (a self-registered pair vs. a
  walk-in/self-registered pair produce different values), and updated component tests assert the
  rendered text changed accordingly (`merge-review.test.tsx`, `duplicate-group-list.test.tsx`).
  The diff touches only `QueryHandlers`/`ViewModels`, no entity/aggregate/mapping file, consistent
  with the commit's own "read-side projection only, no migration" claim. The prior finding's own
  complaint — an unverifiable escalation claim — is also resolved: this gate file is now that
  artifact.
- [LOW] `ProfileForm.tsx` didn't clear `currentPassword` on failure — CLOSED (20fad47, still
  present at HEAD). `setValue('currentPassword', '')` runs in the catch block; a real regression
  test (`profile-form.test.tsx`, `ReauthenticationFailed …`) asserts the field value is `''` after
  a failed submit — this would fail against the pre-fix code (the field previously kept whatever
  was typed).
- [LOW] `ProfileForm.tsx`'s conditionally-required current password used an ignored `register(...,
  {required})` — CLOSED (20fad47 / `ProfileForm.schema.ts`). Folded into a `superRefine` built
  per-`profile` (`makeProfileSchema`). Real test: submitting with email changed and password blank
  shows the client-side message and never reaches the mocked `PUT` (`putCalled` stays `false`) —
  this would fail without the fix, since the old code let the request through to the server.
- [LOW] `IdleLock.tsx`'s `activityMutation.mutateAsync()` had no `.catch` — CLOSED (f90d465). Both
  call sites (`onActivity`, `handleStaySignedIn`) now swallow a rejection with a documented no-op.
- [SUGGESTION] `activityMutation` in the effect's own dependency array — CLOSED (f90d465), via
  `activityMutationRef`.
- [SUGGESTION] `nextPath.ts` prefix matching (`/book` matching `/bookXYZ`) — CLOSED (d04a509).
  `matchesPrefix` now requires an exact match or a `/`/`?` boundary. New unit test file
  (`tests/unit/lib/next-path.test.ts`) covers the exact case the suggestion named
  (`/bookXYZ` rejected, `/book`/`/book/appointment`/`/book?slot=1` accepted), plus the
  trailing-slash-prefix edge case, `//`-protocol-relative rejection, and a multi-prefix list. Real
  assertions, not tautologies — each one pins a specific accept/reject outcome.

ARCH/SEC FIRST-PASS ITEMS TOUCHED BY THIS ROUND (verified, not re-scored — ARCHITECTURE/SECURITY's
own second pass owns the verdict)

- ARCH HIGH 1 (idle-timeout collapsed to "no session") — `getStaffSessionStatus()` now throws
  `StaffSessionIdleTimeoutError` instead of returning `null`; `StaffSessionGuard` treats that one
  error as "stay mounted"; `IdleLock` locks on it and uses the reported `idleTimeoutMinutes`
  instead of a bare `?? 30`. Read `StaffSessionGuard.tsx` end to end: `isIdleTimeout` is excluded
  from `unauthenticated` and included in `authenticated`, so the redirect effect never fires and
  `children` (which is `<IdleLock>`) stays mounted — correct. Covered by a component test that
  seeds a fresh query cache and calls `refetchQueries` to simulate a real `refetchOnWindowFocus`,
  and (8aa2eeb) a real-browser Playwright spec (`staff-auth.spec.ts`) that signs in, forces the
  idle-timeout via a test-only `window.__forceStaffIdleExpired` hook, waits past the 10s
  `staleTime`, and dispatches a real `visibilitychange` event — the actual trigger TanStack
  Query's installed `FocusManager` listens for (confirmed by the commit's own note that it read
  `focusManager.js` directly rather than trust MDN's `document`-scoped guidance). Both tests would
  fail against the pre-fix code (that was the literal reported bug).
- ARCH HIGH 2 (audience guessed from `/api/staff` path prefix) — the global handler and
  `staffIdleLockBridge.ts` are removed; `CustomerSessionGuard` and `IdleLock` each register their
  own `client.ts` session-error handler, so the acting route group (not the failing request's path)
  decides where a `session_invalid` routes to. Since Next only mounts one of `(portal)`/`(staff)`
  per URL, there is no remaining ambiguity. `idle-lock.test.tsx`'s U-G4 was rewritten to prove "any
  call" by hitting an unrelated endpoint through the real `client.ts` handler, not the deleted
  bridge — a stronger test than before, not just a relocated one.
- ARCH LOW (component size) — RegisterForm/ProfileForm/IdleLock/MergeReview/StaffAccountForm/
  NotificationPreferenceMatrix are still 179–265 lines, over §11's ~150-line guideline, after
  genuine extractions (`RegisterIdentifierFields`, `ProfileForm.schema.ts`,
  `AdditionalPhoneNumbersField`, `IdleLockDialogContent`, `MergeConfirmDialog`). Commit message
  55a4223 says so itself ("still over §11's ~200 lines"). Not re-flagged as a new finding — it was
  already LOW in the first pass and the round made a real, acknowledged dent in it rather than
  hiding it.
- SEC MEDIUM (GET-fallback password/CAPTCHA leak) — `method="post"` added to all nine
  password/CAPTCHA/PII forms (`AddressForm`, `DeleteAccountForm`, `ProfileForm`,
  `StaffAccountForm`, `ForgotPasswordForm`, `RegisterForm`, `ResetPasswordForm`, `SignInForm`,
  `WalkInCustomerForm`); confirmed by direct inspection of each `<form>` tag and by a
  `form.method === 'post'` test added per component.
- SEC LOW (translucent idle-lock overlay) — `DialogContent` gains an additive `overlayClassName`
  prop; `IdleLock` passes `bg-surface-page` (opaque in both themes). `cn()` uses `tailwind-merge`,
  so the override genuinely replaces `bg-ink/40` rather than stacking a second background class —
  checked `lib/utils.ts`'s `cn` implementation directly. Test asserts `.bg-surface-page` present
  and `.bg-ink\/40` absent while locked.
- SEC LOW (`pc_csrf` has no `Domain`, cross-subdomain deployment) — backend fix reviewed below,
  correct. I did not find a targeted commit in this round for the separate SEC LOW ("sign-in
  should `queryClient.clear()` on a person change") — worth SECURITY's own second pass confirming
  whether it's still open or was judged low-priority enough to defer; nothing in this round's 20
  commits touches the sign-in success handlers' cache-clear behaviour.

BACKEND (`26fab00`, `88c2591`) — read carefully line by line

- `SessionCookieWriter` gains `AccountOptions.CsrfCookieDomain` (default empty → host-only,
  unchanged). `WriteCsrf` sets `Domain` from it; `ExpireCsrf` now takes the same `Domain` — this is
  the correct fix for the SEC LOW's actual mechanism (a `Set-Cookie` expiry without the original
  `Domain` creates a second, separate host-only cookie rather than deleting the domain-scoped one;
  the session cookies stay host-only in both write and expire paths, correctly unaffected).
  `CustomerProfileController.DeleteAccount` now calls `sessionCookieWriter.ExpireCustomerSession`/
  `ExpireCsrf` instead of a bare `Cookies.Delete(name)`, closing the exact gap the change's own
  comment names.
- Four new integration tests (`CsrfCookieDomainTests.cs`) assert, via the raw `Set-Cookie` header
  (not the value-only helper, which strips attributes) that: no `Domain=` appears on any cookie by
  default; a configured domain lands on `pc_csrf` only, for both customer and staff sign-in; and
  the deletion flow's `pc_csrf` expiry carries the same configured `Domain` while `pc_session`
  still doesn't. These are genuine — the assertions target the literal `Domain=` substring the
  fix's mechanism produces or omits, not a proxy for it, and `AccountsApiFactory.WithCsrfCookieDomain`
  is a real second factory instance, not a mutated shared one.
- `GetDuplicateCustomerGroupsQueryHandler`/`GetStaffCustomerQueryHandler`/the two view models —
  reviewed above under "CR MEDIUM 2 (first-pass) status". `StaffCustomerViewModel.CatCount` is
  kept unchanged for `CustomerDetail.tsx`, and the new `Cats` reuses the existing
  `DuplicateCustomerCatViewModel` rather than adding a parallel type — correct instinct, avoids
  drift between the two read models' shapes for the same information.
- Confirmed via a from-scratch build (below) that these two backend changes compile and their own
  unit-test-tier coverage (Domain/Application) still passes; the new integration tests
  (`CsrfCookieDomainTests`, the two `DuplicateCustomerGroupsEndpointTests` additions,
  `WalkInEndpointTests.GetStaffCustomer_ReportsEveryOwnedCatByNameAlongsideCatCount`) were read but
  not executed — `CatEcommerce.IntegrationTests` needs Postgres, out of this gate's scope per the
  caller's brief ("unit-test projects only").

`UnsavedChangesGuard` — the "always prevent, capture href, replay via `router.push`" pattern

- Same-document `<a>` click: correct. Capture-phase listener, `.closest('a[href]')` so a nested
  icon/span inside the anchor still resolves the link, `event.preventDefault()` +
  `stopImmediatePropagation()` unconditionally when dirty, `href` captured into state, replayed via
  `router.push(href)` only on confirm.
- Modified clicks (new tab, download): correctly excluded — `event.button !== 0`,
  `metaKey`/`ctrlKey`/`shiftKey`/`altKey` all bail out before `preventDefault`, so ctrl/cmd-click
  still opens a new tab normally.
- Form submits: correctly unaffected — the listener only matches `a[href]`, never a
  `button[type=submit]`, so clicking Save on a dirty form is never intercepted by this guard.
- `beforeunload` (reload/close/tab-close): native prompt, `event.preventDefault()` +
  `returnValue = ''` while `when` is true — correct, and the only viable mechanism for that specific
  case (no custom-UI-capable alternative exists for a real document unload).
- **Browser back/forward buttons — not covered, and not disclosed as a limitation.** A back/forward
  navigation fires `popstate`, never a `click` event on an `<a>`, so this guard's only interception
  point never runs for it: pressing Back on a dirty `ProfileForm`/`NotificationPreferenceMatrix`
  loses the edit with no prompt at all. NFR-USA-5 (`docs/SRS.md`) is "Forms longer than one screen
  shall preserve entered data across validation errors and **accidental navigation**" — the browser
  Back button is the single most common form of accidental navigation, arguably more common than
  the in-app link click this component does guard. Both the component's own doc comment and
  ADR-FE-013 (`web/docs/decisions.md`) disclose only a narrower, different limitation — a
  `router.push()`/`router.replace()` call made from outside a click handler isn't covered — and say
  nothing about back/forward. That is an incomplete disclosure of a real gap, not a silent
  behavioural bug (the component correctly does what it claims for the case it does claim to
  handle), but the claim of having "documented" the platform limitation isn't quite true for the
  larger of the two gaps. **MEDIUM** — worth a follow-up: either intercept `popstate` (the standard
  workaround is to immediately push a compensating history entry on `popstate` while dirty, then
  prompt, replaying via `history.back()`/`forward()` on confirm — same shape as the click-replay
  pattern already used here) or, at minimum, extend the ADR-FE-013 "Consequence, explicitly
  accepted" paragraph to name the back/forward gap explicitly so it reads as a decision rather than
  an oversight. Not blocking this gate given it is a real, if incomplete, improvement over the
  `window.confirm()` baseline and the ARCH gate's own MEDIUM (which was about the confirm
  *mechanism*, not back/forward coverage) is otherwise fully closed.
- External links / an `<a href="https://...">` outside the app: `router.push(href)` on an absolute,
  cross-origin `href` is not how Next's client router is meant to be used and its behaviour there
  is unspecified by this component. Currently latent, not exploitable — I checked
  `portal-header.tsx` and the two pages that render this guard (`profile`, `notifications`) and
  found no external `<a>` in either page's render tree today. **Suggestion**, not a finding: if an
  external link is ever added to a page that also renders `UnsavedChangesGuard`, confirming it
  would silently fail to navigate. A same-origin check before choosing `router.push` vs.
  `window.location.assign(href)` would make the component correct regardless of what's added around
  it later.

Playwright flakiness at default worker count (developer-reported; not run here per the caller's
instruction — TEST gate owns this)

- Judged **most likely environmental**, not a shared-mutable-mock-state isolation bug. Reasoning:
  - `playwright.config.ts` runs `fullyParallel: true` against a **single** shared `webServer`
    (`npm run dev`, one `next dev` process, `reuseExistingServer` locally). Next's dev-mode compiler
    serialises on-demand route compilation; many workers hitting first-visit routes concurrently is
    a classic source of timeout-flavoured flakiness that eases at lower concurrency, independent of
    any test-data problem.
  - MSW mock state (`mocks/handlers/areas/accounts.ts`'s `customers`/`staffAccounts` maps) is a
    page-level JS module singleton, re-initialised on every fresh page load; Playwright gives each
    test its own browser context (and this repo doesn't override that), so this state does not
    persist or leak across tests or workers. Its `sessionStorage` persistence (`canPersist`,
    `persistMockState`) is scoped to the browsing context/tab, likewise per-test.
  - The one file that touches a truly global, cross-worker resource is
    `tests/e2e/real-api-verification.spec.ts` (`const unique = Date.now().toString().slice(-9)` at
    module scope) — two workers loading that module in the same millisecond would register the same
    mobile/email against a **real, shared** database. That file is explicitly a manual, real-API-only
    check per its own top comment, not part of the routine MSW-mode default run, but nothing in
    `package.json`'s `test:e2e` script or `playwright.config.ts` actually excludes it from a plain
    `npx playwright test` — worth TEST gate confirming this file is genuinely kept out of the
    parallel MSW run in practice (a `test.skip` gate on an env var, matching the third test in that
    same file, would make the exclusion structural rather than a documented convention).
  - The newly added `staff-auth.spec.ts` (8aa2eeb) does a real `page.waitForTimeout(11_000)` inside
    a `test.setTimeout(45_000)` budget — a tight-ish margin if dev-server compile contention (above)
    adds several seconds under high parallelism, which would present exactly as "flaky at default
    workers, fine at `--workers=2`" without any data-isolation defect at all.
  - Net judgement: reduce default worker count or accept the developer's `--workers=2` workaround
    for now; if flakiness persists even at low concurrency, that would point back at a real
    isolation bug and should be re-investigated then. Not a CODE_REVIEW blocker — no code change is
    indicated on the evidence gathered here.

STATIC CHECKS (web/), re-run at `ac8c519`
- `npx tsc --noEmit` — clean.
- `npx eslint .` — clean, no output at all (zero errors, zero warnings — the widened
  `no-restricted-imports` rule added in 20fad47 for §8.2 rule 4 doesn't fire, because the
  components it covers were actually migrated to props, not merely left to warn).
- `npx prettier --check .` — "All matched files use Prettier code style!".
- `npx vitest run` — 55 files, 186 tests passed.

BACKEND BUILD (isolated worktree)
- `git archive ac8c519` into a scratch folder (`scratchpad/acc2-cr2`, deleted after this run —
  confirmed removed), `dotnet build CatEcommerce.sln -c Release`: **Build succeeded, 0 errors** (20
  `NU1900` warnings — vulnerability feed for an internal GitLab NuGet source unreachable from this
  sandbox; unrelated to this diff).
- Unit-test projects only, `--no-build`, Release:
  - `CatEcommerce.Domain.Tests` — 161/161 passed.
  - `CatEcommerce.Application.Tests` — 66/66 passed.
  - `CatEcommerce.Infrastructure.Tests` — 28/28 passed.
  - `CatEcommerce.IntegrationTests` not run (needs Postgres; out of scope per the caller's brief).

FINDINGS

- [MEDIUM] `web/components/account/UnsavedChangesGuard.tsx` (and `web/docs/decisions.md`'s
  ADR-FE-013) — the browser Back/Forward buttons bypass the guard entirely (no `popstate`
  handling), and neither the component's own comment nor ADR-FE-013's "Consequence, explicitly
  accepted" paragraph names this gap — both mention only the narrower "`router.push()` called
  outside a click handler" limitation. Against NFR-USA-5's "accidental navigation," Back/Forward is
  the more common case than an in-app link click. Fix: either intercept `popstate` (push a
  compensating history entry while dirty, prompt, replay via `history.back()`/`forward()` on
  confirm — the same "always prevent, capture, replay on confirm" shape already used for clicks) or
  extend ADR-FE-013 to name this as an accepted, scoped-out gap rather than leaving it undisclosed.

SUGGESTIONS

- `web/components/account/UnsavedChangesGuard.tsx` — `router.push(href)` on an absolute/external
  `href` is unspecified Next.js router behaviour. No external link exists today in the two pages
  that render this guard, so it's latent, not a live bug. A same-origin check (fall back to
  `window.location.assign(href)` for a cross-origin `href`) would make the component correct
  independent of what gets added around it later.
- `web/tests/e2e/real-api-verification.spec.ts` — module-scope `Date.now()`-derived `unique` is
  shared across however many Playwright workers happen to load this file concurrently; harmless
  today only because the file isn't meant to run in the parallel MSW pass. A `test.skip` on a
  missing "real API" env var (matching the file's own third test) would make that exclusion
  structural instead of documentation-only.

NOTES

- All seven first-pass CODE_REVIEW findings (2 MEDIUM, 3 LOW, 2 suggestions, this file's own prior
  revision) are closed, each with a regression test that would fail against the pre-fix code, not
  a tautology re-asserting the new behaviour in isolation from the bug it replaced.
- The CR MEDIUM 2 fix (backend read-model extension + frontend rendering) is a real, coordinated
  two-sided change with new integration tests on the backend proving per-member derivation
  (not a constant), and updated component tests on the frontend proving the new text renders —
  good use of a from-scratch build to confirm the backend half independently of the frontend
  gate's own vitest run.
- No Critical findings: no superseded-architecture symbol reintroduced, no controller holding a
  repository/DbContext, no `202` on a command, no `AddServiceBusProvider`, no client-side
  authorization decision, no token/secret in a log or URL after the `method="post"` fix, no
  disabled CSRF path.

SCOPE CHECK
- Files touched in this round match the areas the caller named (idle-timeout/session routing,
  component prop splits + ESLint rule, `method="post"`, opaque overlay, `UnsavedChangesGuard` +
  `ConfirmDialog`, `nextPath` boundary matching, component splits, mock password hashing, e2e
  fixture rework, `web/docs/decisions.md`, backend `pc_csrf` Domain option, and the
  `StaffCustomerViewModel.Cats`/duplicate `RecordStatus`/`RegistrationChannel` read-model
  additions) plus their tests and two `docs/handover/HO-ACC-0{01,02}.md` Amended rows recording each
  fix. No file outside that description was touched.
