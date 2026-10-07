# TC-HO-ACC-002 — Accounts and Identity (frontend)

Handover: docs/handover/HO-ACC-002.md
Commit(s) under test: `ee5e907` (HEAD, `ho/ACC-002`, worktree `ACC-002`) plus uncommitted TEST-gate
work in the same worktree (not committed — see report): `web/app/providers.tsx` (new `__apiFetch`
test hook), `web/tests/e2e/staff-auth.spec.ts` (offline-lock test fixed to use it),
`web/tests/components/staff/idle-lock.test.tsx` (+U-G2), `web/tests/components/auth/sign-out-button.test.tsx`
(new, U-C4), plus the pre-existing uncommitted additions found at the start of this pass: U-B8/U-B10/U-B14
(register-form), U-C2 (next-path), U-C5 (session-guards), U-D5 (reset-password-form), U-E5 (address-list).
Written: 2026-09-27 · Executed: 2026-09-27
Second pass written/executed: 2026-09-27 · commits under test for the second pass: `d14d591`,
`ddbae6d`, `aae12d4` (F1 idle-lock race fix) and `13d46ae`, `410b93a`, `5b01d22` (fix for a
CODE_REVIEW gate seventh-pass HIGH found in that fix: explicit `staleTime: 0` on the authoritative
`fetchQuery`), branch head `5b01d22`.
See "## Second pass — 2026-09-27" near the end of this file for the full second-pass record; rows
below that were re-run are updated in place with a "Second pass" note appended to their Evidence
column — their original first-pass text is kept, not replaced, since it is what F1 was actually
found from.

**Correction (third pass, 2026-09-27, ACCEPTANCE gate HIGH) — the "uncommitted" framing above is
now stale.** Every file the header names — `web/app/providers.tsx`'s `__apiFetch` hook,
`tests/e2e/staff-auth.spec.ts`, `tests/components/staff/idle-lock.test.tsx`,
`tests/components/auth/sign-out-button.test.tsx`, and the pre-existing U-B8/U-B10/U-B14/U-C2/U-C5/
U-D5/U-E5 additions — was committed before or during the second pass (see that pass's own commit
list above). Nothing in this branch is uncommitted as of this third pass's own head, either — see
"## Third pass — 2026-09-27" near the end of this file. The per-row "(uncommitted, this pass)" notes
below (e.g. U-B14's) describe history, not current state.
Third pass written/executed: 2026-09-27 · commits under test for the third pass: `2d49593`
(E-X1/E-X2 route-coverage fix) and `10bd748` (U-B14 flake fix), branch head at the time of writing
`10bd748`. See "## Third pass — 2026-09-27" near the end of this file for the full record.

> Statuses record what was **observed**, not inferred. `BLOCKED` marks a case that could not be
> executed (no live backend in this session) or whose result was reasoned about rather than
> watched — it is not a failure, and it is not hidden.

## Summary

| | Count |
|---|---|
| Total (57 unit + 19 E2E-with-a-case + 9 regression + 2 blocked) | 87 |
| PASS | 83 |
| FAIL | 2 |
| BLOCKED | 2 |
| NOT RUN | 0 |
| Gaps recorded (§16 case with no test written) | 7 |

The 2 FAILs are the **same underlying observed failure**, counted once in the E2E table (E-G2) and
once in Regression cases (R3, the ARCHITECTURE fourth-pass HIGH this case exists to guard): a real,
reproducible (not merely "flaky under load") production race — see Finding F1. Per the owner's rule
this is HIGH (a failing test of a required, named behaviour), not a backlog item. The 2 BLOCKED rows
are collapsed, blanket rows (every §16 real-API variant; E-G1's reset-link half) rather than one row
per affected case ID — see "Cases I could not run".

## Environment

- `web/` on worktree `D:\my-repo\cat_ecommerce-wt\ACC-002`, Node scaffold from HO-CAT-002.
- `npx tsc --noEmit`: clean. `npm run lint` (`eslint . --max-warnings 0`): clean. `npx prettier --check .`: clean.
- Unit/component suite: `npx vitest run` — **60 files / 236 tests, all passed**, run twice (once before, once after this pass's additions).
- E2E: Playwright against **MSW mocks only** (`next dev`, `PORT=3102`, `--project=chromium --project=mobile-360`). No `dotnet` API and no Postgres were started this session (coordinator: ports 3102/5005 free, no servers running) — the **real-API** pass (`--project=real-api`, `E2E_REAL_API=1`) is **BLOCKED** for every case below that needs it; §12.3's "run against MSW, then against the real API" second half was not exercised in this pass.
- Full E2E suite run twice at `--workers=2` (244 tests both times: 235/244 then 229/244 passed — different failure sets each run) and once at `--workers=1` for every file that failed at `--workers=2` (all passed except E-G2, see below). This matches CODE_REVIEW gate's own diagnosis (dev-server route-compile contention under `fullyParallel` at default/2 workers) for every file **except** E-G2.
- `next dev` (port 3102) was the only server this session started; it was stopped after the runs below (verified no listener remains — see report).
- `tests/e2e/real-api-verification.spec.ts` confirmed structurally excluded from the default run: `npx playwright test --project=chromium --project=mobile-360 --list` lists 0 of its tests (CODE_REVIEW gate's own suggestion, verified).

## Unit cases (§15)

Evidence for every row is "`npx vitest run` — PASS" against the file named, part of the
60-file/236-test green run above, unless noted otherwise.

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| U-B1 | Schema: name + mobile + 10-char password + answer | Passes; email optional | PASS | `tests/unit/lib/register-schema.test.ts` |
| U-B2 | Schema: neither mobile nor email | Form-level "give at least one" | PASS | `tests/components/auth/register-form.test.tsx` |
| U-B3 | Schema: 9-char password | Names 10 and actual length | PASS | `tests/components/auth/register-form.test.tsx` |
| U-B4 | Valid submit | `register` called once; success rendered, heading focused | PASS | `tests/components/auth/register-form.test.tsx` |
| U-B5 | Success text | No verification/OTP wording; no password/mobile | PASS | `tests/components/auth/registration-success.test.tsx` |
| U-B6 | Duplicate mobile, both fixtures | Identical message; both links; no "email" | PASS | `tests/components/auth/register-form.test.tsx` |
| U-B7 | Duplicate email | Message on email; both links; no "mobile" | PASS | `tests/components/auth/register-form.test.tsx` |
| U-B8 | Breached vs too-short from the server | Distinct texts on the password field | PASS | `tests/components/auth/register-form.test.tsx` (uncommitted, this pass) |
| U-B9 | `CaptchaInvalid` | Message on CAPTCHA; refetched; answer+password cleared | PASS | `tests/components/auth/register-form.test.tsx` |
| U-B10 | Server `PasswordTooShort` then corrected resubmit | Second submit carries a different `captchaId`, succeeds | PASS | `tests/components/auth/register-form.test.tsx` (uncommitted, this pass) |
| U-B11 | `429 Retry-After: 540` | "9 minutes"; no raw seconds | PASS | `tests/components/auth/register-form.test.tsx`, `sign-in-form.test.tsx` |
| U-B12 | CAPTCHA a11y | `alt` states purpose; `aria-describedby`; "New code" text button; announced | PASS | `tests/components/auth/register-form.test.tsx` |
| U-B13 | CAPTCHA endpoint `500` | "couldn't load" with working retry | PASS | `tests/components/auth/register-form.test.tsx` |
| U-B14 | Double click submit (slow mock) | One request; busy state announced | PASS | `tests/components/auth/register-form.test.tsx`. **Third pass (2026-09-27)**: this test failed on every full `vitest run` since master was merged (a fixed 30ms mock delay only made the second click LIKELY, not provably, to land while request 1 was in flight); fixed by gating the mock response on a manually released promise, the same technique U-G7 uses for its own race — see "## Third pass" for the fix commit and 3 green full-suite runs |
| U-C1 | Unknown vs wrong password | Identical message; password cleared; identifier kept | PASS | `tests/components/auth/sign-in-form.test.tsx` |
| U-C2 | Success: session invalidated before nav; `?next=` honoured/evil ignored | Partially unit-tested — see note | PASS (partial, noted) | `tests/unit/lib/next-path.test.ts` (evil-`next` half, uncommitted this pass) + E-C2/E-C4 (e2e) for the nav/cache half — `(auth)/sign-in/page.tsx` is a thin page (§8.2), not independently unit-tested; SECURITY gate's own source read verified `queryClient.clear()` there |
| U-C3 | `CustomerSessionGuard` pending/null/present | Loading / redirect+next / children | PASS | `tests/components/auth/session-guards.test.tsx` |
| U-C4 | Sign-out | Sign-out called; `queryClient.clear()`; routed to sign-in | PASS | `tests/components/auth/sign-out-button.test.tsx` (**new, written this pass** — no test existed for `SignOutButton` at all before) |
| U-C5 | Any `401 session_invalid` from a portal query | Cache cleared; routed to sign-in with `next` | PASS | `tests/components/auth/session-guards.test.tsx` (uncommitted, this pass) |
| U-D1 | Forgot password: `200` with arbitrary body | Fixed client confirmation; body text not rendered | PASS | `tests/components/auth/reset-password-form.test.tsx` |
| U-D2 | Reset page with `#token=abc` | Token read; URL becomes `/reset-password`; `no-referrer` | PASS | `tests/components/auth/reset-password-form.test.tsx` |
| U-D3 | Reset page without a token | Invalid-link state | PASS | `tests/components/auth/reset-password-form.test.tsx` |
| U-D4 | `InvalidPasswordResetToken` | Single invalid/expired state, "request another" | PASS | `tests/components/auth/reset-password-form.test.tsx` |
| U-D5 | `BreachedPassword` on reset | Field error; second attempt reuses same token | PASS | `tests/components/auth/reset-password-form.test.tsx` (uncommitted, this pass) |
| U-D6 | After `204` | Links to both sign-in pages; no account-kind hint | PASS | `tests/components/auth/reset-password-form.test.tsx` |
| U-E1 | Profile with `canEditEmail: false` | Email read-only + explanation; mobile always read-only | PASS | `tests/components/account/profile-form.test.tsx` |
| U-E2 | Four additional numbers | Add button explains limit of 3 | PASS | `tests/components/account/profile-form.test.tsx` |
| U-E3 | Addresses: set default | One "Default" badge with text | PASS | `tests/components/account/address-list.test.tsx` |
| U-E4 | Remove an address | Confirmation names label; `DefaultAddressCannotBeRemoved` server message | PASS | `tests/components/account/address-list.test.tsx` |
| U-E5 | `409` on an address change | "Someone else changed this — reload", entered edit not discarded | PASS | `tests/components/account/address-list.test.tsx` (uncommitted, this pass) |
| U-E6 | Preference matrix | Rendered from API cells; locked cells disabled "Always on"; `<table>` | PASS | `tests/components/account/notification-preference-matrix.test.tsx` |
| U-E7 | Toggle then navigate away | Unsaved-changes confirm; nothing saved until Save | PASS (composed, noted) | `notification-preference-matrix.test.tsx` ("never autosaves") + `unsaved-changes-guard.test.tsx` (the shared guard `NotificationPreferenceMatrix` renders when dirty) + `account-area.spec.ts` e2e proves the identical composition on `ProfileForm`; no test mounts `UnsavedChangesGuard` wrapping `NotificationPreferenceMatrix` itself and drives a real Back press |
| U-E8 | Save sends only changed cells | Request body has exactly the changed cells | PASS | `tests/components/account/notification-preference-matrix.test.tsx` |
| U-J1 | Download | Blob saved as `purrcare-my-data.json`; URL revoked; not cached | PASS | `tests/components/account/data-export-delete.test.tsx` |
| U-J2 | Delete form | Lists every consequence; requires password + checkbox | PASS | `tests/components/account/data-export-delete.test.tsx` |
| U-J3 | `ReauthenticationFailed` | Field error; password cleared | PASS | `tests/components/account/data-export-delete.test.tsx` |
| U-J4 | Deletion success | Cache cleared; `/account-deleted` | PASS | `tests/components/account/data-export-delete.test.tsx` |
| U-G1 | `StaffSessionGuard` null | Redirect to `/staff/sign-in?next=` | PASS | `tests/components/auth/session-guards.test.tsx` |
| U-G2 | Idle warning timing | Appears at `idleExpiresOn − 2min` from `serverTime`, skewed local clock corrected | PASS | `tests/components/staff/idle-lock.test.tsx` (**new, written this pass** — no test existed for the warning banner at all before; local clock deliberately set 5 min off `serverTime` under fake timers, asserted the banner is absent at 4999ms and present at 5001ms of virtual elapsed time) |
| U-G3 | Activity posting | Keydown twice within 60s → one post | PASS | `tests/components/staff/idle-lock.test.tsx` |
| U-G4 | `session_idle_timeout` with typed values | Overlay shown; background inert; focus trapped; Escape blocked; values intact after re-sign-in | PASS | `tests/components/staff/idle-lock.test.tsx` |
| U-G5 | Re-sign-in as a different `staffAccountId` | Discard notice; cache cleared; route reloaded | PASS | `tests/components/staff/idle-lock.test.tsx` |
| U-G6 | `lib/identity.ts` | Does not exist; no import anywhere | PASS | `tests/components/staff/identity-removed.test.ts` |
| U-G7 (added second pass) | F1 regression: session query's own `session_idle_timeout` gated slow; a bystander page query's `session_invalid` resolves first | Bystander does not win the race — no redirect, no cache clear, dialog appears only once the session query's own (slow) response is released, still showing the lock, not a sign-out | PASS (second pass) | `tests/components/staff/idle-lock.test.tsx`, "TEST gate F1: a bystander session_invalid that resolves before the session query's own session_idle_timeout must not win the race and sign the user out" |
| U-G8 (added second pass) | F1 regression, negative case: a bystander `session_invalid` when the session is genuinely revoked/unknown (no idle-timeout in play at all) | Still signs out: `replace` called with `/staff/sign-in`, cache cleared, no dialog | PASS (second pass) | `tests/components/staff/idle-lock.test.tsx`, "TEST gate F1 (genuine sign-out preserved): a bystander session_invalid for a genuinely revoked/unknown session still signs the user out" |
| U-G9 (added second pass) | CODE_REVIEW gate seventh-pass HIGH regression: authoritative `fetchQuery` must not be satisfied by a session read that succeeded <30s ago (the app's real `staleTime: 30_000` default) | Still fetches fresh and locks, rather than silently trusting the stale-but-not-yet-expired cached "still signed in" data | PASS (second pass) | `tests/components/staff/idle-lock.test.tsx`, "CODE_REVIEW gate seventh pass, HIGH: the authoritative session check must not be satisfied by a recently-fresh cached session read (staleTime 30s, matching app/providers.tsx)" |
| U-F1 | Role field | Options from catalogue mock; skeleton; ≥1 required | PASS | `tests/components/admin/staff-account-form.test.tsx` |
| U-F2 | Capability preview | Union of selected roles; captioned "Indicative" | PASS | `tests/components/admin/staff-account-form.test.tsx` |
| U-F3 | Role removal | Confirm names role + sign-out consequence; `PUT` sends whole set | PASS | `tests/components/admin/staff-account-detail.test.tsx` |
| U-F4 | `403` | `PermissionDeniedState`, no role named | PASS | `tests/components/admin/staff-account-detail.test.tsx` |
| U-H1 | Walk-in duplicate | Lookup called; `ExistingCustomerNotice` links record; values kept | PASS | `tests/components/customer/walk-in-customer-form.test.tsx` |
| U-H2 | `CustomerStatusBadge` Incomplete | Text + icon, not colour alone | PASS | `tests/components/customer/walk-in-customer-form.test.tsx` |
| U-I1 | Duplicates group | Cats incl. deceased badge; `NotYetRecordedNotice`; no orders/balances | PASS | `tests/components/customer/duplicate-group-list.test.tsx` |
| U-I2 | Merge review | Fresh fetch on mount; no radio pre-selected; confirmation lists every cat | PASS | `tests/components/customer/merge-review.test.tsx` |
| U-I3 | Merge `5xx` | "may be partly complete" + Run again sends identical body | PASS | `tests/components/customer/merge-review.test.tsx` |
| U-I4 | Merge flow | No cat endpoint called | PASS | `tests/components/customer/merge-review.test.tsx` |
| U-X1 | `client.ts` on `POST` | Sends `X-CSRF-Token` = `pc_csrf` cookie; `GET` does not | PASS | `tests/unit/lib/api-client.test.ts` |
| U-X2 | Any `400` | `errorCode` appears nowhere in the DOM | PASS | `tests/unit/errors.test.ts`, `tests/components/cross-cutting.test.tsx`, others |
| U-X3 | Unmatched `propertyName` | Form-level message | PASS | `tests/unit/errors.test.ts` |
| U-X4 | Storage audit after every form test | `localStorage`/`sessionStorage` empty; no secret in query cache | PASS | `tests/components/cross-cutting.test.tsx` |

## End-to-end cases (§16) — MSW pass

Real-API variants (§12.3's second half) are **BLOCKED** for every row below: no `dotnet` API / DB
was started this session.

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| E-B1 | Register with mobile, sign in | Success page; sign-in lands on portal | PASS | `accounts-auth.spec.ts` — passed at `--workers=1`; not in either `--workers=2` failure list |
| E-B2 | Register with email only, sign in by email | Works | **GAP — no case written** | see "Gaps left deliberately" |
| E-B3 | Wrong CAPTCHA, then right | Message + new image, fields kept, then success | PASS | `accounts-auth.spec.ts` |
| E-B4 | Duplicate mobile / duplicate email | Specific messages, links, no cross-naming | **GAP — no case written** | see "Gaps left deliberately" |
| E-B5 | Short then breached password | Two distinct reasons | **GAP — no case written** | see "Gaps left deliberately" |
| E-B6 | Trigger `429` | "wait N minutes" | **GAP — no case written** | see "Gaps left deliberately" |
| E-B7 | Keyboard-only registration incl. "New code" | Completable; focus always visible | **GAP — no case written** | see "Gaps left deliberately" |
| E-B8 | Timed unassisted registration | Well inside NFR-USA-1; record the time | **GAP — no case written** | see "Gaps left deliberately" |
| E-C1 | Wrong password vs unknown account | Identical screens | PASS | `accounts-auth.spec.ts` |
| E-C2 | Reload the portal after sign-in | Still signed in | PASS | `accounts-auth.spec.ts` |
| E-C3 | Sign out, press Back | Guard redirects; no cached portal data | PASS | `accounts-auth.spec.ts` |
| E-C4 | Open `/portal/account/profile` signed out | Redirect to sign-in, then back after sign-in | PASS | `accounts-auth.spec.ts` |
| E-D1 | Forgot → link → reset → sign in with new password | Works; token absent from address bar/history | PASS | `accounts-auth.spec.ts` |
| E-D2 | Reuse the link | Invalid/expired state | PASS | `accounts-auth.spec.ts` |
| E-E1 | Add three addresses, change default, remove one | One default throughout | PASS | `account-area.spec.ts` |
| E-E2 | Opt into SMS marketing, save, reload | Persisted; transactional cells locked | PASS | `account-area.spec.ts` — failed once at `--workers=2` (2nd full run), passed at `--workers=1`; judged environmental per CODE_REVIEW gate's own diagnosis |
| E-J1 | Download data, then delete account | JSON downloaded; `/account-deleted`; old credentials fail | **GAP — no case written** | see "Gaps left deliberately" |
| E-G1 | New staff: admin creates → member sets password via reset → signs in | Back office loads with their name | PASS (partial, by design) | `staff-admin.spec.ts` — file's own comment: covers only "admin creates the account"; the reset-link half needs the real API's Development log, deferred to the (BLOCKED) real-API pass |
| E-G2 | Type into a cat form, idle timeout, re-sign-in as same person | Lock shown; typed values intact; save works | **FAIL** (first pass, intermittent, real race — see Finding F1); **PASS (second pass)** | `staff-auth.spec.ts:211` — first pass: 10/10 pass at `--workers=2`+`--workers=1` combined in most runs, but reproducibly fails ~1/3 of isolated single-worker repeats on `mobile-360` with a full redirect to `/staff/sign-in` (draft lost) instead of the lock dialog. See regression row R3 and Finding F1. **Second pass (2026-09-27), after `d14d591`/`13d46ae`/`5b01d22`: `--workers=1 --repeat-each=20` on both `chromium` and `mobile-360` (40 runs total) — 40/40 passed, 0 failures**, where the pre-fix code reproduced ~1/3 in the same isolated-repeat style. Also passed once inside the second pass's full-suite `--workers=1` regression run (see "Second pass" section). |
| E-G3 | Same, sign in as someone else | Draft discarded notice | PASS | `staff-auth.spec.ts:275` — this is the CODE_REVIEW/ARCHITECTURE sixth-pass gap-closer test (see R1); passed in every run this pass, including 10 isolated repeats |
| E-F1 | A nurse opens `/staff/admin/staff-accounts` directly | `PermissionDeniedState` | PASS | `staff-admin.spec.ts` |
| E-H1 | Walk-in new number; existing number | Created Incomplete; existing record offered | PASS | `staff-customers.spec.ts` — failed once at `--workers=2`, passed at `--workers=1` (both runs); environmental |
| E-I1 | Merge a duplicate pair with a deceased cat | Review lists both; after merge both on survivor | PASS | `staff-customers.spec.ts` — failed once at `--workers=2` (1st full run), passed reliably at `--workers=1`; environmental |
| E-X1 | axe on every route, clean/with-errors/idle-lock-open | Zero serious/critical violations | PASS (first/second pass, overstated — see below); **PASS, verified in full (third pass)** | `accounts-a11y-responsive.spec.ts`. **First/second-pass evidence covered only 7 of ~19 §8.3 routes, clean state only — no errors-shown or idle-lock-open run at all.** That narrowing was disclosed only in the spec's own header comment, nowhere else (ACCEPTANCE gate HIGH). **Third pass (2026-09-27)**: `ROUTES` extended to all 19 §8.3 screens this handover owns (seeded MSW ids for the `[staffAccountId]`/`[customerId]`/duplicates-review dynamic routes), plus two new dedicated cases — "E-X1 (with errors shown)" (`/register` after an empty submit, every inline `role="alert"` visible) and "E-X1 (idle lock open)" (the `__forceStaffIdleExpired`/`__invalidateQueries` path E-G2 uses, axe run with the dialog open and the background `inert`). `--workers=1`, `PORT=3102`: chromium 21/21 passed; mobile-360 21/21 passed. Zero serious/critical violations found on any route or state — no product code change was needed. |
| E-X2 | Every route at 360/768/1024/1440 | No horizontal scroll, no clipped control | PASS (first/second pass, overstated — see below); **PASS, verified in full (third pass)** | `accounts-a11y-responsive.spec.ts` — one `/staff/admin/staff-accounts@1440px` instance failed at `--workers=2` (2nd run), passed at `--workers=1`; environmental. **First/second-pass evidence covered only the same 7 routes as E-X1 (ACCEPTANCE gate HIGH, same finding).** **Third pass (2026-09-27)**: all 19 routes × 4 widths (360/768/1024/1440), `--workers=1`, `PORT=3102`: chromium 76/76 passed; mobile-360 76/76 passed. Zero overflow found on any route at any width — no product code change was needed. |
| E-X3 | After the whole suite | `localStorage`/`sessionStorage` empty; no `pc_session` readable | PASS | `accounts-a11y-responsive.spec.ts` |

## Regression cases from earlier gates

| ID | Gate | Finding | Case | Status | Evidence |
|---|---|---|---|---|---|
| R1 | ARCHITECTURE (4th pass HIGH / CODE_REVIEW 5th pass LOW / ARCHITECTURE 6th pass LOW) | Idle-lock pause must reach mounted queries (`focusManager`/`onlineManager` swap), and the E2E proof of the pause itself (not only the `isError && !data` fallback) was vacuous | E-G3, `idle-lock-context.test.tsx` | PASS | E-G3 passed in every MSW run this pass, incl. 10 isolated `--repeat-each` runs; `idle-lock-context.test.tsx` is part of the 236-test green unit run |
| R2 | ARCHITECTURE (5th pass MEDIUM) | Locking while genuinely offline deadlocks the re-sign-in mutation (`onlineManager` frozen `false`); gate explicitly asked TEST to add the real-browser `context.setOffline(true)` variant | `staff-auth.spec.ts` "locking while genuinely offline…" (chromium + mobile-360) | PASS (test fixed this pass — see Finding F2) | Both projects pass at `--workers=1`, and both passed inside the two full `--workers=2` runs |
| R3 | ARCHITECTURE (4th pass HIGH) | Same lock-pause mechanism as R1, exercised via the real HTTP round trip (E-G2) rather than the unit/E-G3 mechanism proof | E-G2, U-G7, U-G8, U-G9 | **FAIL (first pass, intermittent); PASS (second pass)** | First pass: see Finding F1 — a real race between the session query's `session_idle_timeout` and the cat query's `session_invalid`, both forced to 401 by the same refocus, can resolve in the wrong order. **Second pass (2026-09-27)**: fixed in `d14d591` (route the bystander through an authoritative, serialized `queryClient.fetchQuery` on the session-status query instead of trusting its own response) and `13d46ae` (explicit `staleTime: 0` on that check, closing a CODE_REVIEW seventh-pass HIGH in the first fix). Verified: `idle-lock.test.tsx`'s U-G7/U-G8/U-G9 all PASS; E-G2 40/40 at `--workers=1 --repeat-each=20` on both projects; full-suite `--workers=1` regression run also passed (see "Second pass" section for the exact run). |
| R4 | SECURITY (1st pass MEDIUM) | Password/CAPTCHA/PII forms need `method="post"` (pre-hydration native-submit leak) | `SEC gate MEDIUM (first pass)` tests | PASS | Present in `register-form.test.tsx`, `sign-in-form.test.tsx`, and others per the gate's own file list; part of the green unit run |
| R5 | SECURITY (1st pass LOW 1) | Idle-lock overlay must be opaque, not the shared translucent one | `idle-lock.test.tsx` | PASS | `document.querySelector('.bg-surface-page')` asserted non-null, `.bg-ink/40` asserted absent |
| R6 | SECURITY (1st pass LOW 2) | Sign-in must `queryClient.clear()` (not only the session key) so a person change doesn't leak the previous person's cache | E-C2, E-C4 (E2E); source-level only at unit level | PASS (E2E); not independently unit-tested | See U-C2's own note — `(auth)/sign-in/page.tsx` is a thin page per §8.2 |
| R7 | CODE_REVIEW (2nd pass suggestion) | `real-api-verification.spec.ts` should be *structurally* excluded from the default MSW run, not only by convention | `playwright.config.ts` `testIgnore` | PASS | `npx playwright test --project=chromium --project=mobile-360 --list` lists 0 of its tests |
| R8 | CODE_REVIEW (developer-reported) | Playwright flakiness at default worker count — judged "most likely environmental" (dev-server route-compile contention), asked TEST to confirm | Full suite at `--workers=2` (×2) and targeted `--workers=1` reruns | PASS for every file except `staff-auth.spec.ts`'s E-G2 | 14 distinct test identities failed across the two `--workers=2` runs (different set each time); all but E-G2 passed cleanly at `--workers=1`, corroborating "environmental." E-G2 is a separate, real finding — see F1 |
| R9 | HO-CAT-002 suites behind the new guards (§17 step 5, §19 gate 4) | Cat-profile/vaccination/weight/error-handling/staff-shell E2E and component suites must still pass wrapped in `StaffSessionGuard`/`IdleLock` | `cat-profile-header.spec.ts`, `cat-weights.spec.ts`, `cat-vaccinations.spec.ts`, `cat-error-handling.spec.ts`, `staff-shell-*.spec.ts`, plus the full 236-test unit run | PASS | All passed at `--workers=1` in the targeted rerun; the `--workers=2` failures among these were part of the same environmental pattern as R8 |

## Findings (production bugs — reported, not fixed)

**F1 — HIGH — real race between concurrent `session_idle_timeout` and `session_invalid` on refocus
can drop the lock and sign the user out instead (E-G2, `staff-auth.spec.ts:211`).**

**Second pass (2026-09-27) — fixed, verified closed.** `d14d591` routes the bystander
`session_invalid` handler through one authoritative, serialized `queryClient.fetchQuery` on
`STAFF_SESSION_QUERY_KEY` instead of deciding from the bystander's own response — see
`IdleLock.tsx`'s own doc comment on `resolveBystanderSessionInvalid` for the full mechanism.
CODE_REVIEW's own seventh pass then found and this pass's `13d46ae` fixed a second, narrower HIGH in
that same fix: the `fetchQuery` call had no explicit `staleTime`, so it inherited the app-wide
`staleTime: 30_000` default and could resolve to a recently-cached "still signed in" answer without
ever touching the network, silently missing a session that had genuinely just ended — `staleTime: 0`
closes that. Both defects now have deterministic unit coverage (U-G7/U-G9) plus a negative case
(U-G8) proving a genuine sign-out is still honoured, and E-G2 passed 40/40 at
`--workers=1 --repeat-each=20` on both `chromium` and `mobile-360` this pass, where the pre-fix code
reproduced the race in roughly 1/3 of an equivalent isolated-repeat run. See the "Second pass"
section near the end of this file for the full second-pass record.

When a real window-focus event (or the deterministic `__invalidateQueries` stand-in E-G2 and the
production `focusManager` both use) refetches *two* stale staff-audience queries at once — the
session-status query (`useStaffSession()`) and a page's own data query (here, the cat-profile query)
— and **both** have just been forced to 401 by the same idle-timeout event, `IdleLock.tsx` has two
independent, unsynchronized paths that react to them:

1. `useStaffSession()`'s own `sessionError` effect: if the error is `StaffSessionIdleTimeoutError`,
   calls `lockNow()`.
2. `setSessionErrorHandler`'s global callback (registered by `IdleLock`, fed by *every other*
   staff-audience `apiFetch` call, `client.ts`): `session_idle_timeout` → `lockNow()`;
   **otherwise, if not already locked, `queryClient.clear()` and redirect to `/staff/sign-in`.**

The mock's `cat-profiles.ts` endpoint (matching the real API's own "idle timeout ends the session on
first detection; every later call sees `session_invalid`, never `session_idle_timeout` again"
behaviour) reports `session_invalid`, not `session_idle_timeout` — it is not the query that detected
the idle timeout itself, only a bystander of the same already-ended session. If the browser processes
this `session_invalid` response *before* the session-status query's own `session_idle_timeout`
response reaches the first effect, path (2) runs while `lockedRef.current` is still `false`: the
whole cache is cleared and the user is redirected straight to `/staff/sign-in`, **losing the draft
entirely**, instead of the intended graceful in-place lock.

- **Reproduced directly**: `Error Context` for one failing run shows the page has already navigated
  to "Staff sign in" (not the cat-edit page with a dialog) when the assertion times out.
- **Frequency observed**: 1 failure in 3 isolated (`--workers=1`, single spec, single test)
  `--repeat-each` runs on `mobile-360`; 0 failures in a separate 10-repeat run across both projects.
  Consistent with a genuine, narrow race rather than load-dependent flakiness (unlike every other
  file in R8/R9, this one fails even fully isolated, with a warm dev server and an already-compiled
  route).
- **Why this is HIGH, not "flaky E2E"**: it is not the test that is racy in isolation from any real
  behaviour — the *production* session-error handling genuinely has an unguarded order dependency
  between two concurrent 401s, and the losing order silently discards unsaved staff work and forces
  a re-sign-in, which is exactly the failure mode ADR-FE-010/§11.9 and the ARCHITECTURE gate's fourth
  pass exist to prevent. E-G2 is a named, required case for A17/A18.
- **Not fixed here** — this is the TEST gate; reported for ARCHITECTURE/the owner to address, e.g. by
  having the `setSessionErrorHandler` callback check `queryClient`'s own session-query error/state
  (or a short-lived "an idle-timeout was just seen" flag) before treating a bystander
  `session_invalid` as a real sign-out.

**F2 — test-authoring bug (found and fixed this pass, not a production bug) — the original
"locking while genuinely offline" E2E test could never pass, for a reason unrelated to the fix it was
meant to prove.**

The test (found already written, uncommitted, at the start of this pass) drove the lock the same way
every *other* lock test in the file does: `__forceStaffIdleExpired` + `__invalidateQueries(['session',
'staff'])`. That refetches `useStaffSession()` through `useQuery`'s own observer, which — like every
query in this app — defaults to `networkMode: 'online'`. `context.setOffline(true)`'s real `offline`
event (fired *before* this step, matching the scenario under test) flips `onlineManager` to `false`
first, so TanStack Query correctly (matching production) never even attempts that refetch: the lock
could never engage, and the test could never pass, regardless of whether the `networkMode: 'always'`
fix under test was present or not. This is a test-design bug, not a rediscovery of the deadlock
ARCHITECTURE gate fifth pass already fixed (`networkMode: 'always'` on the re-sign-in mutation itself
is unaffected and correct).

Fixed by reaching the lock the way `idle-lock.test.tsx`'s own jsdom counterpart does: a raw
`apiFetch()` call (via a new, precedented, test-only `window.__apiFetch` hook in
`app/providers.tsx`, gated the same way the existing `__apiBaseUrl`/`__invalidateQueries` hooks are)
to an arbitrary already-idle-checked staff endpoint (`/api/staff-roles`), bypassing the
`useQuery`/`useMutation` `networkMode` gate entirely — exactly the same "any staff-audience call"
mechanism `client.ts`'s session-error handler is documented to react to. Verified passing on both
projects, at `--workers=1` and inside the full suite.

## Acceptance-criteria coverage

| Criterion | Covered by | Status |
|---|---|---|
| A1 | U-B4, U-B5, E-B1, E-B2 | **Partial** — E-B2 is a gap (email-only registration path); U-B4/U-B5/E-B1 pass |
| A2 | U-B2 | PASS |
| A3 | U-B9, U-B10, E-B3 | PASS |
| A4 | U-B11, E-B6 | **Partial** — E-B6 is a gap; U-B11 passes |
| A5 | U-B6, U-B7, E-B4 | **Partial** — E-B4 is a gap; U-B6/U-B7 pass |
| A6 | U-B3, U-B8, E-B5 | **Partial** — E-B5 is a gap; U-B3/U-B8 pass |
| A7 | U-B12, E-B7 | **Partial** — E-B7 is a gap; U-B12 passes |
| A8 | U-C1, E-C1, E-C2 | PASS |
| A9 | U-C4, E-C3 | PASS |
| A10 | U-D1, U-D2, E-D1 | PASS |
| A11 | U-D4, E-D2 | PASS |
| A12 | U-E3, U-E4, E-E1 | PASS |
| A13 | U-E6, U-E7, U-E8, E-E2 | PASS |
| A14 | U-F1, U-F2, E-G1 | PASS (E-G1 partial by design, see its own row) |
| A15 | U-F4, E-F1 | PASS |
| A16 | E-G1 | PASS (partial by design — real-API-dependent half BLOCKED) |
| A17 | U-G4, E-G2, U-G7, U-G8, U-G9 | **FAIL (first pass)** — E-G2 failed intermittently; see Finding F1. This is the criterion F1 most directly threatened. **PASS (second pass)** — F1 fixed in `d14d591`/`13d46ae`; E-G2 40/40 at `--workers=1 --repeat-each=20` both projects, U-G7/U-G8/U-G9 all pass |
| A18 | U-G4, U-G5, E-G2, E-G3, U-G7, U-G8, U-G9 | **FAIL (first pass)** (via E-G2, same as A17) — U-G4/U-G5/E-G3 all passed even in the first pass. **PASS (second pass)** — same fix and evidence as A17 |
| A19 | U-H1, U-H2, E-H1 | PASS |
| A20 | U-I1 | PASS |
| A21 | U-I2, U-I3, U-I4, E-I1 | PASS |
| A22 | U-J1, E-J1 | **Partial** — E-J1 is a gap; U-J1 passes |
| A23 | U-J2, U-J3, U-J4, E-J1 | **Partial** — E-J1 is a gap; U-J2/U-J3/U-J4 pass |
| A24 | E-X1 and every a11y row | **NOT MET (first/second pass — 7/~19 routes, no errors-shown or idle-lock-open axe run at all; ACCEPTANCE gate HIGH). MET (third pass)** — see E-X1's own row |
| A25 | E-X2 | **NOT MET (first/second pass — same 7/~19 routes as A24; ACCEPTANCE gate HIGH). MET (third pass)** — see E-X2's own row |
| A26 | U-X1, U-X4, U-G6, E-X3 | PASS |
| A27 | U-X2, U-X3 | PASS |

## Cases I could not run

| ID | Why | What would unblock it |
|---|---|---|
| Every §16 case's real-API variant (§12.3) | No `dotnet` API process or Postgres database was started this session (coordinator: no servers running) | Start the merged HO-ACC-001 backend against a real DB with this worktree's `appsettings.Development.json`, then `npm run test:e2e:real-api` |
| E-G1's reset-link half | Needs the real API's Development log for the actual reset link (MSW cannot fabricate this realistically) | Same as above |

## Gaps left deliberately

| Gap | Why not covered | Suggested owner |
|---|---|---|
| E-B2 (register with email only, sign in by email) | Not written this pass — time-boxed; the *mobile* path (E-B1) and the underlying duplicate-email logic (U-B7) are covered, so the residual risk is narrow (an email-only account failing to authenticate later) | TEST, next pass |
| E-B4 (duplicate mobile/email messages, E2E) | U-B6/U-B7 already prove the messages and links at the component level with the real server-shaped fixtures; the E2E add is mostly a routing/wiring check | TEST, next pass |
| E-B5 (short then breached password, E2E) | U-B3/U-B8 already prove both distinct messages at the component level | TEST, next pass |
| E-B6 (429, E2E) | U-B11 already proves the "N minutes" copy at the component level for both forms | TEST, next pass |
| E-B7 (keyboard-only registration) | U-B12 proves the CAPTCHA a11y attributes statically; a real keyboard-only completion run (Tab order, visible focus throughout) is not equivalent and was not attempted | TEST, next pass — this is the gap I am least comfortable leaving, since keyboard operability is exactly the kind of thing a component test cannot prove |
| E-B8 (timed unassisted registration) | A measurement/UX case, not a pass/fail correctness case; no baseline "assisted" party was available this session | Owner/UX, next pass |
| E-J1 (download data, then delete account, E2E) | U-J1–U-J4 cover both the download and delete mechanics in isolation at the component level; the full round trip (download → delete → old credentials fail) was not additionally driven through the browser | TEST, next pass |
| U-C2's cache/`next`-honoured half, and R6 | `(auth)/sign-in/page.tsx` is a thin page per §8.2 and this codebase's convention does not unit-test pages directly; covered by E-C2/E-C4 instead, plus SECURITY gate's own source read | Acceptable as-is, noted for the record |
| U-E7's guard-integration half on `NotificationPreferenceMatrix` specifically | The shared `UnsavedChangesGuard` is exhaustively unit-tested on its own, and the identical composition is E2E-proven on `ProfileForm`; no test drives a real Back press through `NotificationPreferenceMatrix` itself | TEST, next pass, low priority |

## Existing tests judged weak, and strengthened this pass

- `web/tests/e2e/staff-auth.spec.ts`'s "locking while genuinely offline…" test was not merely weak —
  it could never pass at all, for a reason unrelated to the behaviour it claimed to test (Finding
  F2). Fixed in place (see F2); now genuinely exercises the ARCHITECTURE gate fifth-pass fix, on both
  projects.
- No other existing assertion in the suites touched this pass was found to pass regardless of the
  production code's correctness (i.e., decoration). The `U-C4`/`U-G2` additions were new coverage for
  previously-untested behaviour (`SignOutButton` had **no test file at all**; the idle-warning banner
  had no test at all), not strengthening of an existing weak assertion.

## Second pass — 2026-09-27

Handover: same, `docs/handover/HO-ACC-002.md`. Commits under test for this pass: `d14d591`,
`ddbae6d`, `aae12d4` (F1 idle-lock race fix and its own regression coverage) and `13d46ae`,
`410b93a`, `5b01d22` (CODE_REVIEW gate seventh-pass HIGH found in that fix — a missing
`staleTime: 0` on the authoritative `fetchQuery` — plus its own regression coverage). Branch head:
`5b01d22`. This section is additive: the first pass above is kept verbatim; rows it names (F1, E-G2,
R3, A17, A18) were also updated in place with an appended "Second pass" note, per the task's
instruction, rather than only recorded here.

### Second-pass summary

| | Count |
|---|---|
| New unit rows this pass (U-G7, U-G8, U-G9) | 3, all PASS |
| First-pass rows re-verified and flipped FAIL → PASS this pass (E-G2, R3, A17, A18) | 4, all PASS |
| E-G2 isolated-repeat runs (`--workers=1 --repeat-each=20`, chromium + mobile-360) | 40/40 PASS, 0 failures |
| Full E2E suite regression run (`--workers=1`, chromium + mobile-360, port 3102) | 235/244 PASS, 9 FAIL — all 9 unrelated to F1/E-G2/staleTime, all 9 re-verified PASS in a fresh isolated rerun (30/30) — new MEDIUM backlog item, not a gate blocker |
| BLOCKED / gaps carried over from the first pass, unchanged | 2 BLOCKED rows, 7 gap rows (owner's rule: not blockers) |

### Environment (second pass)

- Same worktree, `D:\my-repo\cat_ecommerce-wt\ACC-002`, branch `ho/ACC-002` at `5b01d22`.
- `npx tsc --noEmit`: clean (0 errors). `eslint . --max-warnings 0`: clean (0 warnings).
  `npx prettier --check .`: clean. `npx vitest run`: **60 files / 239 tests, all passed** — run
  twice, both green (239 = the first pass's 236 + this fix's 3 new tests: `ddbae6d`'s two F1 tests
  and `410b93a`'s one staleTime regression test).
- E2E: Playwright against MSW mocks only, `PORT=3102`, `--project=chromium --project=mobile-360`.
  No `dotnet` API / Postgres started this session (ports 3102/5005 confirmed free before starting,
  and confirmed free — no LISTENING socket, no `node.exe` process — after every run this pass) — the
  real-API BLOCKED rows from the first pass are unchanged.
- The narrow CODE_REVIEW eighth pass running in parallel was not touched: only
  `docs/gates/HO-ACC-002-TEST.md` (this file's own gate report) and
  `docs/test-cases/TC-HO-ACC-002.md` were edited this pass, both via `Edit` (append-only relative to
  the first pass's content).

### New unit cases this pass

See U-G7/U-G8/U-G9 added directly into the "Unit cases (§15)" table above (kept together with their
siblings rather than duplicated here). Evidence for all three: `npx vitest run
tests/components/staff/idle-lock.test.tsx` — all three observed PASS, individually confirmed via
`--reporter=verbose`, as part of the full 239-test green run (twice).

### E-G2 targeted repeat run

`PORT=3102 npx playwright test --project=mobile-360 --project=chromium --workers=1
--repeat-each=20 -g "E-G2"` — **40/40 passed** (20 repeats × 2 projects), 2.8 minutes, 0 failures.
The first pass reproduced 1 failure in 3 isolated single-project repeats under the pre-fix code; this
pass's 40/40 at the same isolation level (`--workers=1`, single spec grep, repeated) is the intended
apples-to-apples comparison. Directly observed via the Playwright list reporter's per-test pass
lines and the final `40 passed (2.8m)` summary — not inferred.

### Full E2E suite regression run (`--workers=1`, both projects, port 3102)

`PORT=3102 npx playwright test --project=chromium --project=mobile-360 --workers=1` — **244 tests,
one continuous 26.1-minute run: 235 passed, 9 failed.** Watched directly via the run's own final
summary block (`9 failed` with the full list of test identities, `235 passed (26.1m)`), not inferred.

**None of the 9 failures are E-G2, F1, or the CODE_REVIEW seventh-pass staleTime fix.** Both E-G2
instances (chromium and mobile-360) passed in this run. The 9 failures, all watched directly in the
run's own error output:

| # | Project | Spec | Failure |
|---|---|---|---|
| 1 | chromium | `staff-customers.spec.ts` E-H1 | `getByText('Incomplete')` not visible within 5s |
| 2 | chromium | `staff-customers.spec.ts` E-I1 | `toHaveURL` — merge review URL never navigated to within 5s |
| 3 | mobile-360 | `cat-deceased.spec.ts` E11/E12 | `getByLabel(/^Name/)` `toBeDisabled` — element not found within 5s |
| 4 | mobile-360 | `cat-register.spec.ts` E2 | `getByLabel('Owner customer ID')` — 30s test timeout |
| 5 | mobile-360 | `staff-admin.spec.ts` E-G1 (partial) | `getByLabel(/full name/i)` — 30s test timeout |
| 6 | mobile-360 | `staff-auth.spec.ts` E-G3 | `getByLabel(/^Name/)` not visible within 5s (**before** any lock/session logic runs — same generic pattern as #1–5, not a session-handling assertion) |
| 7 | mobile-360 | `staff-customers.spec.ts` E-H1 | Same as #1 |
| 8 | mobile-360 | `staff-customers.spec.ts` E-I1 | Same as #2, this time a 30s timeout rather than 5s |
| 9 | mobile-360 | `staff-shell-sheet-closes-on-back-navigation.spec.ts` | `[aria-label="Expand menu"]` — 30s test timeout |

Every one of these is an "element/URL never appeared within the timeout" failure on a page unrelated
to session/idle-lock logic (walk-in lookup, cat-deceased edit lock, cat registration, staff-account
creation, a nav-sheet focus test), on tests that were already known-flaky under contention in the
first pass (E-H1/E-I1 — see R8/R9) or are otherwise ordinary page-render assertions. Only #6 (E-G3)
sits in the same spec file as the mechanism under test, but its failing assertion
(`getByLabel(/^Name/)` visible) runs at the very start of the test, before the lock is even engaged —
identical in shape to #1–5/7–9, not a session-handling assertion failing.

**Re-verified: not a regression.** Observed one node.exe process holding ~3.3GB of memory partway
through this run (`tasklist`, checked live) — consistent with 26 minutes of continuous `next dev`
serving ~240 sequential page loads/route compiles in one process, not previously exercised this way
(the first pass's own full-suite runs were at `--workers=2`, twice, with only the *failing files*
individually rerun at `--workers=1` afterwards — never the entire 244-test suite in one unbroken
`--workers=1` session). To confirm this rather than assume it, re-ran all 6 affected spec files
together, fresh (`--workers=1`, both projects, freshly started dev server, no prior 26-minute
history): `PORT=3102 npx playwright test --project=chromium --project=mobile-360 --workers=1
tests/e2e/staff-customers.spec.ts tests/e2e/cat-deceased.spec.ts tests/e2e/cat-register.spec.ts
tests/e2e/staff-admin.spec.ts tests/e2e/staff-auth.spec.ts
tests/e2e/staff-shell-sheet-closes-on-back-navigation.spec.ts` — **30/30 passed** (2.6 minutes),
including every one of the 9 identities that failed above, on both projects, and including E-G2/E-G3
again. Watched directly via the run's own `30 passed (2.6m)` summary.

**Conclusion, recorded as a new MEDIUM backlog item, not a gate blocker (owner's rule):** a single
continuous `--workers=1` run of the full 244-test suite exhibits its own class of flakiness —
distinct from the CODE_REVIEW gate's already-diagnosed `--workers=2` route-compile contention (R8) —
most likely long-run resource growth (dev-server memory, or accumulated fixture/browser-context
state) rather than test-order data pollution, since the failing tests are scattered across unrelated
files/fixtures and all pass cleanly, together, when rerun without the preceding 26 minutes of history.
This is new information this pass surfaced (the first pass never ran the full suite at `--workers=1`
end to end) — recorded honestly rather than re-run repeatedly until a clean pass was found and only
that reported.

### Updated first-pass rows (also edited in place above)

| Row | First-pass status | Second-pass status | Evidence |
|---|---|---|---|
| F1 (Finding) | HIGH, open | Fixed, verified closed | `d14d591`/`13d46ae`; U-G7/U-G8/U-G9 PASS; E-G2 40/40 |
| E-G2 (E2E) | FAIL (intermittent) | PASS | 40/40 isolated repeats; full-suite regression run (below) |
| R3 (Regression) | FAIL (intermittent) | PASS | Same evidence as E-G2/F1 |
| A17 (Acceptance) | FAIL | PASS | Same evidence as E-G2/F1 |
| A18 (Acceptance) | FAIL | PASS | Same evidence as E-G2/F1 |

### Gaps and BLOCKED rows — unchanged this pass

Per the task's scope, this pass did not attempt to close the first pass's 7 recorded gaps (§16 cases
E-B2/E-B4/E-B5/E-B6/E-B7/E-B8/E-J1) or the 2 BLOCKED real-API rows — the owner's rule already treats
these as MEDIUM/LOW backlog items, not gate blockers, and nothing about the F1/staleTime fix touches
them.

### New finding this pass: full-suite `--workers=1` long-run flakiness (MEDIUM, backlog)

Not present in the first pass (which never ran the full 244-test suite at `--workers=1` end to end —
only `--workers=2` twice, plus per-file `--workers=1` reruns of that run's own failures). This pass's
task explicitly asked for the full-suite `--workers=1` run, which surfaced 9 failures in one
continuous 26.1-minute session, none related to F1/E-G2/staleTime, all 9 confirmed passing (30/30) in
a fresh, shorter rerun of the same spec files immediately afterward — see "Full E2E suite regression
run" above for the full evidence and the specific test identities. Per the owner's rule this is
MEDIUM (reproducible only under a specific long-run condition, not tied to any acceptance criterion,
and not the code under test), not a gate blocker. Recorded for the backlog: the CODE_REVIEW gate's
existing `--workers=2` contention diagnosis (R8) does not cover this — a distinct, additional
observation about the cost of a very long unbroken single-worker session (possible dev-server memory
growth: one `node.exe` was observed holding ~3.3GB partway through).

| Gap | Why not covered (this pass) | Suggested owner |
|---|---|---|
| Full-suite `--workers=1` long-run flakiness (9 identities, see table above) | Confirmed environmental (30/30 pass on immediate fresh rerun of the same files); investigating the root cause (dev-server memory growth vs. Playwright browser-context accumulation) is outside this pass's scope (verifying the F1/staleTime fix) | TEST/DevEx, next pass — consider periodic dev-server/browser restarts within a single CI `--workers=1` invocation, or accept `--workers=2` with per-file `--workers=1` reruns of failures as the standing convention (as the first pass already did) |

## Third pass — 2026-09-27

Handover: same, `docs/handover/HO-ACC-002.md`. Commits under test: `2d49593` (E-X1/E-X2 route
coverage, ACCEPTANCE gate HIGH) and `10bd748` (U-B14 flake, ACCEPTANCE gate MEDIUM M4), plus
`docs/handover/HO-ACC-002.md`'s own Amended row for M6. Branch head at the time of writing this
section: `10bd748`. Additive, same convention as the second pass: E-X1/E-X2/A24/A25/U-B14 are
updated in place above with a "third pass" note; this section carries the full run evidence.

### Third-pass summary

| | Count |
|---|---|
| ACCEPTANCE gate HIGH (A24/A25 route coverage) | Closed — see E-X1/E-X2 run evidence below |
| ACCEPTANCE gate MEDIUM M4 (U-B14 flake) | Closed — see full-suite evidence below |
| ACCEPTANCE gate MEDIUM M6 (unrecorded out-of-§13 files) | Closed — see `docs/handover/HO-ACC-002.md` §1 Amended, dated 2026-09-27 |
| New/extended E2E cases this pass (E-X1 errors-shown, E-X1 idle-lock-open, plus 12 newly-covered routes on both existing E-X1/E-X2 describe blocks) | All PASS, both projects |

### Environment (third pass)

- Same worktree, `D:\my-repo\cat_ecommerce-wt\ACC-002`, branch `ho/ACC-002` at `10bd748`.
- `npx tsc --noEmit`: clean (0 errors). `npx eslint . --max-warnings 0`: clean (0 warnings).
  `npx prettier --check .`: clean.
- `npx vitest run`: **60 files / 239 tests, all passed — run three times, all three green**, per the
  gate's own instruction (M4's fix needed to be shown stable, not just fixed once).
- E2E: Playwright against MSW mocks only, `PORT=3102`, one project at a time (`--project=chromium`,
  `--project=mobile-360`), `--workers=1`. Ports 3102/5005 confirmed free before starting and after
  every run (no LISTENING socket).

### A24/A25 — E-X1/E-X2 route coverage (ACCEPTANCE gate HIGH)

`ROUTES` in `tests/e2e/accounts-a11y-responsive.spec.ts` extended from 7 to 19 — every §8.3 screen
this handover owns: the full `(auth)` set (`/register`, `/sign-in`, `/forgot-password`,
`/staff/forgot-password`, `/reset-password`, `/account-deleted`, `/staff/sign-in`), the full
`(portal)/account` set (`profile`, `addresses`, `notifications`, `data`, `data/delete`), and the full
`(staff)` set this handover builds (`admin/staff-accounts` list/new/`[id]`, `customers/new`/`[id]`,
`customers/duplicates`/`review`). Dynamic routes use seeded `accountsFixtureIds` (`nurseStaffId` for
the staff-account detail route, `customerId` for the staff-customer detail route,
`duplicateGroupCustomerA`/`duplicateGroupCustomerB` for the merge-review route's `?ids=` query
string) so each one resolves real data rather than a 404/`ErrorState`. Each route now declares its
own `audience` (`'none' | 'customer' | 'staff'`) rather than the old `route.startsWith('/staff/')`
guess, because `/staff/sign-in` and `/staff/forgot-password` are public `(auth)` pages that happen to
share a `/staff/` path prefix with the gated `(staff)` routes — the old guess would have signed in as
staff before visiting either, which is not what a real visitor to either page does.

Two new dedicated E-X1 cases close the "errors shown" and "idle lock open" gap, per §16's own wording
and the gate's fix direction — both single, representative cases (§2: the CAPTCHA, the idle lock and
the preference matrix are the three places WCAG is hardest) rather than every route repeated in both
states:
- **"E-X1 (with errors shown)"**: `/register`, submit with every field empty, wait for the first
  `role="alert"` (RegisterForm.tsx renders every field-level and form-level error this way), then run
  axe.
- **"E-X1 (idle lock open)"**: sign in as staff, go to `/staff/customers/new`, force the idle timeout
  via the same `__forceStaffIdleExpired` + `__invalidateQueries(['session','staff'])` path
  `staff-auth.spec.ts`'s E-G2/ARCH-gate-HIGH-1 tests use, wait for the dialog, then run axe over the
  whole page (dialog open, background `inert`).

Runs, `--workers=1`, `PORT=3102`, one project at a time:

| Suite | chromium | mobile-360 |
|---|---|---|
| E-X1 (19 routes + errors-shown + idle-lock-open = 21 tests) | 21/21 passed (2.5m) | 21/21 passed (part of the full 98-test run below) |
| E-X2 (19 routes × 4 widths = 76 tests) | 76/76 passed (4.2m) | 76/76 passed (part of the full 98-test run below) |
| Full spec (E-X1 + E-X2 + E-X3 = 98 tests) | not re-run as one block (see the two rows above plus a standalone E-X3 run, 1/1 passed, 25.1s) | 98/98 passed in one run (6.4m) |

Zero serious/critical axe violations and zero horizontal-overflow instances were found on any route,
at any width, or in either new error/idle-lock state, on either project. **No product code change was
needed** — the finding was a test-coverage gap, exactly as the ACCEPTANCE gate's own report said it
expected.

### U-B14 flake (ACCEPTANCE gate MEDIUM M4)

`tests/components/auth/register-form.test.tsx`'s U-B14 test used a fixed 30ms `setTimeout` inside its
mock's `POST /api/customers` handler to make the second click of a double-click "likely" to land while
request 1 was still in flight. That is a real-wall-clock race, not a guarantee — under load (the same
`node.exe` memory-growth conditions the ACCEPTANCE gate's own M3 finding observed), the first request
can settle in under 30ms, letting the second click through as a genuine second submission, which is
exactly what the gate observed failing on 2 of 2 full-suite runs since the merge ("expected 2 to be
1"). Fixed by gating the mock's own response on a manually released promise — the same technique
`idle-lock.test.tsx`'s U-G7 uses for its own race (see that test's own history above) — so request 1 is
*provably* still unsettled at the moment of the second click, asserted directly (`requestCount === 1`)
before the response is ever released, rather than inferred from timing.

`npx vitest run`, full suite, run three times in direct succession: **60 files / 239 tests, all green,
all three runs.** No `isolated rerun of just this file` caveat needed this time — the fix removes the
timing dependency entirely rather than narrowing the window it was previously getting away with.

### M6 — unrecorded out-of-§13 files (ACCEPTANCE gate MEDIUM)

Closed by a new dated Amended row in `docs/handover/HO-ACC-002.md` §1, naming all three files the
gate listed: `web/tests/unit/lib/register-schema.test.ts`, the `window.__apiFetch` hook in
`web/app/providers.tsx`, and `tests/CatEcommerce.IntegrationTests/AccountsIntegrationTestBase.cs`. The
same row also declares the E-X1/E-X2 route-coverage extension above, closing the gate's own
"no Amended row declares the E-X1/E-X2 route sampling" note.

### Not attempted this pass

Per the gate's own instruction ("re-submit to ACCEPTANCE only" once the HIGH is fixed), this pass did
not attempt any of the MEDIUM/LOW backlog items beyond M4 and M6 above (M1–M3, M5, L1–L11) — those
remain exactly as the ACCEPTANCE gate's own "BACKLOG CONSOLIDATION" line recorded them, for
`docs/backlog/ACC.md`.
