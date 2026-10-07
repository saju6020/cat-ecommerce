## Second pass

GATE: ACCEPTANCE
HANDOVER: HO-ACC-002
COMMITS: 2d49593 10bd748 1fb69d4 cc77ec0 (head cc77ec0; delta f5067d6..cc77ec0 on top of the first-pass record 5cb55b6)
DATE: 2026-09-27
STATUS: PASS

The first pass's only blocking finding, the HIGH on A24/A25, is closed. This gate observed the evidence directly; the developer's report was not relied on. Every other criterion is unchanged from the first pass and remains MET.
- `git diff --stat f5067d6 cc77ec0` touches 5 files and no product code:
  - `web/tests/e2e/accounts-a11y-responsive.spec.ts`
  - `web/tests/components/auth/register-form.test.tsx`
  - `docs/test-cases/TC-HO-ACC-002.md`
  - `docs/handover/HO-ACC-002.md`
  - this file (the committed first pass)
- So the first-pass evidence for A1–A23, A26 and A27 still applies unchanged.

PRIOR GATES: unchanged from the first pass. ARCHITECTURE, SECURITY, CODE_REVIEW and TEST are all PASS, and their artifacts are present. This delta is test and doc work that closes this gate's own findings, so no earlier gate needs re-running.

VERIFICATION RUN BY THIS GATE (web/, head cc77ec0)
- `npx tsc --noEmit` exited 0. `npx eslint . --max-warnings 0` exited 0. `npx prettier --check .` exited 0.
- `npx vitest run`, five full runs, results in order:
  - Run 1: 236/239. U-B4 (register) failed, plus U-C1 and the 429 test in `sign-in-form`.
  - Run 2: 239/239.
  - Run 3: 237/239. U-B7 timed out, and U-B9 failed on `'EJami'` because the timed-out U-B7's `user.type` was still typing into U-B9's freshly rendered form.
  - Run 4: 237/239. U-C1 timed out, and the sign-in 429 test failed.
  - Run 5: 239/239.
- **U-B14 passed in all five runs.**
- Every failure was a 5 s timeout, or a timeout spilling into the next test, in the two userEvent-heavy auth files.
- Both files passed 18/18 in each of three isolated reruns.
- Neither file's code under test changed in this delta. See M7.
- Playwright, run directly by this gate:
  - Command: `next dev` on port 3102 (MSW), then `PORT=3102 npx playwright test tests/e2e/accounts-a11y-responsive.spec.ts --project=chromium --project=mobile-360 --workers=1`.
  - Result: **196/196 passed (7.2 min)**, which is 98 per project: E-X1 19 routes plus the errors-shown and idle-lock-open variants (21), E-X2 19 routes × 4 widths (76), and E-X3 (1).
- A scratch route probe (standalone Playwright script, not in the repo) checked that axe and overflow ran on the intended screen, not on a guard redirect or an error state. For each route it recorded the final URL, the headings, any error or permission text, and the control count:
  - **All 19 routes stayed on their own URL, with no error or permission text.**
  - Headings seen included "Create your account", "Staff sign in", "Set or reset password", "Your account has been deleted", "Your data", "Nadia Nurse" (staff-account detail), "Sam Rahman" (customer detail), "Duplicate customers" and "Review merge".
  - Portal account tabs rendered their forms: profile had 5 controls, notifications 5, delete 4.
  - The idle-lock variant opened the real lock ("You were signed out after 30 minutes of inactivity", with an embedded password field) over `/staff/customers/new`.
  - The errors variant showed 5 alerts and 5 fields with `aria-invalid="true"`.
- Cleanup: the dev server was stopped. Afterwards there was no listener on 3102, no `node.exe`, and `git status` was clean. The probe script lives only in the session scratchpad.

CRITERIA (changed this pass)
- A24 MET.
  - E-X1 axe runs with zero serious or critical violations on all 19 §8.3 screens, on both projects.
  - It also covers errors shown (`/register` after an empty submit) and the idle lock open. That is A24's named clause, and it was run over a real staff page.
  - The probe confirms each scan ran on the intended screen.
- A25 MET. E-X2 finds no horizontal overflow on all 19 screens at 360, 768, 1024 and 1440, on both projects (152/152).
- A1–A23, A26, A27: MET, with the first-pass evidence unchanged. No product code changed.

FINDINGS (this pass)
- [MEDIUM] M7, new. The full `vitest run` is still intermittently red (3 of 5 runs) from 5 s timeouts in `web/tests/components/auth/register-form.test.tsx` and `sign-in-form.test.tsx`: U-B4, U-B7, U-B9 (a cascade from U-B7), U-C1 and the sign-in 429 test.
  - These are not regressions. The files are untouched apart from U-B14, all pass in isolation, and nothing in the product changed.
  - But M4's closure is narrower than the TC sheet's third pass states: U-B14 is fixed, yet "`npm run test` red on a full run" is not.
  - This matches the developer's own "sign-in-form 429 flake" note.
  - Fix direction: raise `testTimeout` for these files, or use `userEvent.setup({ delay: null })`, and investigate worker contention. Backlog.
- [LOW] L12, new. E-X1/E-X2 scan `/reset-password` without a token, so only the invalid-link state is covered. The token-bearing form state (password field and requirements text) has no axe or overflow run.
  - It shares `PasswordField`/`AuthCard` with `/register`, which is covered, including with errors.
  - Fix direction: add a `/reset-password#token=valid-customer-token` entry.
- [LOW] L13, new. E-X1/E-X2 assert only the axe result or the overflow check. None confirms the intended screen rendered, which is what this gate's probe had to check. A future fixture failure that lands on a guard or error page would pass silently.
  - Fix direction: add a per-route `expect(page).toHaveURL(...)` and a heading assertion.
- Closed from the first pass:
  - The HIGH (A24/A25).
  - M4 (U-B14 itself; the wider suite is now M7).
  - M6: the Amended row names all three files and also declares the E-X1/E-X2 extension.
  - M5, only for the E-X1/E-X2/A24/A25/U-B14 rows, which the TC sheet has corrected.
- Still open, carried to backlog unchanged:
  - M1 and M2.
  - M3, which now also covers the developer's reported later e2e flake.
  - The rest of M5: U-X4, U-B9, U-B13, U-G3 and U-B4 assertion strength, and the stale TC sheet header.
  - L1 to L11, and R8.

SCOPE CHECK
- Files in allowed scope: 5/5. They are the spec and test (§13 `web/tests/components/auth/**`, plus the §1-recorded ACC e2e spec), the TC sheet and the gate file (§13), and `docs/handover/HO-ACC-002.md` (a gate-requested Amended row).
- Files outside allowed scope: none.
- Out-of-scope (§4) work found: none. No product code changed.
- Undeclared deviations: none. The E-X1/E-X2 extension is declared in §1 Amended and in the TC third pass.

BACKLOG CONSOLIDATION (for docs/backlog/ACC.md, supersedes the first pass's list)
M1 7 unwritten §16 e2e cases · M2 real-API pass never run · M3 long-run e2e flakiness / dev-server memory growth (includes the reported later sign-in e2e flake) · M5 remaining TC-row assertion-strength overstatements · M7 full-suite vitest timeout flakes in auth form tests · L1 E-G3 cannot detect a regressed pause · L2 fetchQuery staleTime on shared key · L3 unmount window in bystander resolve · L4 component size · L5 assertion strength details · L6 "New code" untested · L7 staff first-password path untested · L8 page-level halves of U-C2/R6/U-E7 · L9 script signal reporting · L10 SECURITY paper trail after ac8c519 · L11 other owners' items (CAT/FIL photo types, CSP headers, HO-CAT-002 table rendering, postcss advisory) · L12 reset-password form state not scanned · L13 E-X1/E-X2 don't assert the rendered screen · R8 `--workers=2` contention.

---

## First pass

GATE: ACCEPTANCE
HANDOVER: HO-ACC-002
COMMITS: f5067d6 (head of ho/ACC-002: merge of master cc5737e into 5455a07). Reviewed as `git diff master HEAD`: 178 files (120 added, 57 modified, 1 deleted). The merge brought 133 files, none under `web/`.
DATE: 2026-09-27
STATUS: FAIL

This gate fails on two §18 criteria that have no evidence for most of what they claim: A24 (axe on every screen, including the idle lock) and A25 (every screen at 360–1440 px).
- Every other criterion has concrete evidence.
- There is no scope breach that blocks.
- Nothing withdrawn by §4 was built.
- The fix is test work only. The product code may well already pass; nobody has looked.

PRIOR GATES (checked from docs/gates/ and docs/test-cases/, not from the caller's summary)
- ARCHITECTURE: PASS. Sixth pass, `docs/gates/HO-ACC-002-ARCHITECTURE.md`, delta 061aed3..9e8d33a.
- SECURITY: PASS. Second pass, `docs/gates/HO-ACC-002-SECURITY.md`, dated 2026-09-26 at ac8c519.
  - Later deltas had their security review inside CODE_REVIEW's combined "CODE_REVIEW/SECURITY" passes 4–6.
  - The F1 fix was reviewed by CODE_REVIEW passes 7–8. See LOW L10.
- CODE_REVIEW: PASS. Eighth pass, `docs/gates/HO-ACC-002-CODE_REVIEW.md`, 6ce5f02..5b01d22.
- TEST: PASS. Second pass, `docs/gates/HO-ACC-002-TEST.md`, with the sheet `docs/test-cases/TC-HO-ACC-002.md`.
  - The sheet has 87 rows: 2 BLOCKED rows and 7 recorded gaps. The first pass's 2 FAIL rows were flipped to PASS in the second pass.
  - There are no NOT RUN rows.

VERIFICATION RUN BY THIS GATE (web/, head f5067d6, no servers started)
- `npx tsc --noEmit`: exit 0.
- `npx eslint . --max-warnings 0`: exit 0.
- `npx vitest run`: 60 files, **238/239**. The one failure is U-B14 (`register-form.test.tsx:313`, "expected 2 to be 1").
  - Two isolated reruns of `register-form.test.tsx` both gave 13/13.
  - That makes 2 of 2 full-suite runs failing since the merge (the integrator's and this one), against 2 of 2 passing at 5b01d22.
  - Diagnosis: a test-harness defect, not a product bug. See M4.
- The backend numbers (dotnet 0 errors; Domain 314, Application 101, Infrastructure 56, Integration 455) are the integrator's recorded output. They were not re-run here.
- Merge check: `git diff f5067d6^1 f5067d6 -- web` is empty. Nothing from master's CAT/FIL changes reached `web/`, so no web code that ACC-002 depends on changed.
  - One consequence exists, but it is not ACC-002's: `web/lib/api/cats.ts` `CatPhoto`/`AddCatPhotoRequest` still use the pre-FIL shape (`storageKey`, `contentType`, `sizeInBytes`, `fileName`).
  - Master's backend now takes `FileId` and returns signed URLs.
  - This drift exists identically on master. It is outside §13 (HO-CAT-002/HO-FIL-002 own it). It touches no ACC-002 criterion: E-G2 edits cat fields, not photos. See L11.

CRITERIA
- A1 MET.
  - `register-form.test.tsx` U-B4: a valid submit leads to one success.
  - `registration-success.test.tsx`: no verif/OTP/SMS/inbox wording, no password, no full mobile; the heading is focused.
  - `accounts-auth.spec.ts` E-B1: the mobile path, through to sign-in.
  - The email-only path is shown by U-B7: an email-only submit passes client validation and reaches the server.
  - `register-schema.test.ts` shows email is optional.
  - E-B2 is a gap (M1).
- A2 MET. U-B2 (`register-form.test.tsx:62`) asserts the form-level "give a mobile number, an email, or both".
- A3 MET.
  - U-B10 asserts that the second submit's `captchaId` differs from the first, after a server 400.
  - U-B9 asserts the CAPTCHA message, and that the answer and password are cleared while the name is kept.
  - E-B3 shows wrong-then-right leads to success. The mock is single-use (`accounts.ts:916-918` deletes the id on every submit), so that success can only follow a fresh challenge.
  - U-B9's own "refetched" assertion is decorative (L5).
- A4 MET.
  - U-B11 (register) and the sign-in 429 test (`sign-in-form.test.tsx:62`) both show "9 minutes", with no raw seconds.
  - The reset form renders the same `RetryNotice` on `ApiRateLimitedError`; source read at `ForgotPasswordForm.tsx:67,76`.
  - E-B6 is a gap (M1).
- A5 MET.
  - U-B6 uses both fixtures (with and without email). The message names no "email", and it sits in the mobile field wrapper with Sign in and Reset links.
  - U-B7 shows the email duplicate never names mobile or phone.
  - E-B4 is a gap (M1).
- A6 MET.
  - U-B3: the client message is "at least 10 characters (you entered 9)".
  - U-B8: the server's breached and too-short messages are asserted to differ.
- A7 MET, on source plus a tested mechanism.
  - U-B12 asserts the reception-alternative text and that a "New code" text button exists.
  - `CaptchaField.tsx` wires "New code" to `onRetry`, which `RegisterForm.tsx:179` binds to `captcha.refetch()`.
  - U-B10 proves `refetch()` yields a new `captchaId`.
  - No test clicks "New code" (L6). E-B7 is a gap (M1).
- A8 MET.
  - U-C1 and E-C1 show an identical message for an unknown account and a wrong password; the password is cleared and the identifier kept.
  - The message offers a reset (`SignInForm.tsx:51`).
  - E-B1 signs in by mobile through the UI.
  - The fixtures sign in by email (`fixtures.ts:81-89`).
  - E-C2 shows the session survives a reload.
- A9 MET.
  - `sign-out-button.test.tsx`: the sign-out endpoint is called, the whole cache is cleared, and the user is routed to `/sign-in` (or `/staff/sign-in` for staff).
  - E-C3: after sign-out, Back hits the guard and no cached portal data shows.
- A10 MET.
  - U-D1: the fixed confirmation is shown; the body is never rendered.
  - U-D2: the token is read and stripped.
  - E-D1: `toHaveURL('/reset-password')` after the fragment, then a sign-in with the new password.
  - Source and a grep of app/components/lib found no storage use.
- A11 MET. U-D4 and E-D2 (`#token=expired-token`) both give the single invalid/expired state with "request another".
- A12 MET.
  - U-E3: one "Default" badge, which moves.
  - U-E4: the confirm names the label, and `DefaultAddressCannotBeRemoved` shows the server's message.
  - E-E1.
- A13 MET.
  - U-E6: a real `<table>`, with locked cells disabled and reading "Always on".
  - U-E8: only changed cells are sent.
  - "never autosaves" test.
  - E-E2: persisted across a reload.
- A14 MET.
  - U-F1: options come from the catalogue, and at least one is required.
  - U-F2: the union of roles is shown, captioned "Indicative".
  - E-G1 (admin half): the admin creates the account and the list shows it.
- A15 MET.
  - E-F1: a nurse signs in through the UI and opens the URL directly, and gets "You don't have permission to do this".
  - U-F4 checks the state names no role.
- A16 MET, on source for the second half.
  - Staff sign-in is shown by E-F1 (UI sign-in) and by every staff spec.
  - The first password: `SignInForm.tsx:82-83,152-157` renders "Set or reset password", linking to `/staff/forgot-password`.
  - That page calls `useRequestStaffPasswordReset`, which posts to `/api/staff/auth/password-reset/request` (it matches `StaffAuthController.cs:81`).
  - The shared `/reset-password` page is covered by U-D2 to U-D6. U-D6 links to both sign-in pages.
  - The staff success message pointing to "Set or reset password" is tested (`staff-account-form.test.tsx:77`, E-G1).
  - The live round trip is BLOCKED (M2). No test visits `/staff/forgot-password` (L7).
- A17 MET.
  - U-G4: overlay, inert background, focus inside, Escape blocked, values intact.
  - U-G7 and U-G9 are the race and staleTime regression tests. U-G8 shows a genuine sign-out still signs out.
  - E-G2: 40/40 at `--workers=1 --repeat-each=20` on both projects (TEST second pass, observed run).
- A18 MET.
  - U-G4 and E-G2 cover the same person.
  - U-G5 and E-G3 cover a different person: discard notice, cache cleared, route reloaded.
- A19 MET.
  - U-H1: lookup called, record linked, values kept.
  - U-H2: the Incomplete badge carries text and an icon.
  - E-H1.
- A20 MET. U-I1: cats are listed with a deceased badge, `NotYetRecordedNotice` is shown, and there are no orders or balances columns.
- A21 MET.
  - U-I2: fresh fetch on mount, no radio pre-selected, and the confirmation names the survivor, the retired record and "Whiskers, Mittens will move".
  - `mergeCalls === 1`.
  - U-I3: a 5xx shows "may be partly complete", and Run again sends the identical body.
  - U-I4: no cat endpoint is called.
  - E-I1 uses a deceased cat.
- A22 MET. U-J1: a Blob named `purrcare-my-data.json` is created and its URL revoked. E-J1 is a gap (M1).
- A23 MET.
  - U-J2: every consequence is listed, and the password and checkbox are required.
  - U-J3: `ReauthenticationFailed` gives a field error and clears the password.
  - U-J4: the cache is cleared and the user lands on `/account-deleted`.
  - E-J1 is a gap (M1).
- **A24 NOT MET.** The criterion is "zero serious/critical axe violations on every screen, **including the idle lock**". The only axe evidence is `tests/e2e/accounts-a11y-responsive.spec.ts` E-X1.
  - It runs axe on **7** routes: `/register`, `/sign-in`, three account tabs, the staff-accounts list, and duplicates.
  - §8.3 has about 19 screens. **No axe run covers**:
    - `/forgot-password`, `/staff/forgot-password`, `/reset-password`, `/account-deleted`, `/staff/sign-in`;
    - `/portal/account/data`, `/portal/account/data/delete`;
    - `/staff/admin/staff-accounts/new` and `/staff/admin/staff-accounts/[id]`;
    - `/staff/customers/new`, `/staff/customers/[customerId]`, `/staff/customers/duplicates/review`.
  - **There is no axe run at all with the idle lock open.** §2 names the lock as one of the three places where WCAG is hardest. U-G4 proves focus trap, inert and Escape behaviour, but not contrast, labels or ARIA validity on the lock.
  - There is no axe run with errors shown, as §16 E-X1 also requires.
  - The spec's own header says "a representative sample … not literally every route in §8.3". That narrowing is recorded nowhere else: not in §1 Amended, not in the TC sheet, and in no gate file.
  - The TC sheet's E-X1 row reads "axe on every route, clean/with-errors/idle-lock-open | PASS". That overstates what ran (M5).
- **A25 NOT MET.** The criterion is "every screen at 360–1440 px". E-X2 checks horizontal overflow at 360/768/1024/1440 for the same **7** routes only. The other 12 screens listed under A24 have no overflow or clipping check at any width.
  - The `mobile-360` project does drive some of them functionally at 360: reset, staff sign-in, create staff account, walk-in, customer detail and merge review. That shows the controls are usable at 360. It does not show "no horizontal scroll, no clipped control", and it says nothing about 768/1024/1440.
  - `/portal/account/data`, `/data/delete` and `/account-deleted` are not visited by any e2e test at any width (E-J1 is a gap).
- A26 MET.
  - U-X1 (`api-client.test.ts:31,47`): `X-CSRF-Token` equals `pc_csrf` on POST and is absent on GET.
  - U-G6 (`identity-removed.test.ts`): the file is gone and nothing imports it. Confirmed: `web/lib/identity.ts` is absent.
  - E-X3: no `pc_session` in `document.cookie`, and no plaintext password in storage (deviation recorded in §1 Amended).
  - This gate grepped app/components/lib and found no `localStorage`/`sessionStorage`/IndexedDB/Cache/JS-written cookie.
  - U-X4 is thinner than its row claims (L5).
- A27 MET.
  - U-X2 (`sign-in-form.test.tsx:93`, `cross-cutting.test.tsx:42`, `errors.test.ts:55`): `errorCode` is never in the DOM and never passed to `setError`.
  - U-X3 (`errors.test.ts:33`): an unmatched `propertyName` becomes a form-level message.

FINDINGS

Blocking:
- [HIGH] `web/tests/e2e/accounts-a11y-responsive.spec.ts:16-24` (with `docs/test-cases/TC-HO-ACC-002.md:150-151,275-276`). A24 and A25 lack evidence for 12 of about 19 §8.3 screens. A24's named idle-lock clause, and the with-errors state, have no axe evidence at all.
  - This is an undeclared narrowing of §16 E-X1/E-X2, recorded only in a code comment. The TC sheet marks it PASS as though complete.
  - Fix:
    - extend `ROUTES` to every §8.3 screen (seed ids for the `[staffAccountId]`/`[customerId]`/review routes);
    - add an E-X1 variant with validation errors shown, and one with the idle lock open (the `__forceStaffIdleExpired` + `__invalidateQueries` path E-G2 already uses);
    - re-run both projects;
    - correct the TC rows.
  - Then re-submit to ACCEPTANCE only. No product code change is implied unless the new runs find violations.

Non-blocking, for docs/backlog/ACC.md:
- [MEDIUM] M1. There are 7 unwritten §16 e2e cases: E-B2, E-B4, E-B5, E-B6, E-B7, E-B8 and E-J1.
  - Each affected criterion (A1, A4 to A7, A22, A23) has passing unit-level evidence.
  - E-B7 (keyboard-only registration including "New code") is the one with no substitute: nothing proves keyboard operability or visible focus end to end.
  - E-J1 is also the only thing that would visit `/portal/account/data*` and `/account-deleted` in a browser.
- [MEDIUM] M2. The real-API pass has never run. Every §16 real-API variant is BLOCKED, and so is E-G1's reset-link half.
  - HO-ACC-001 is merged to master, so this is no longer blocked by anything but a session with the API and DB up (`npm run test:e2e:real-api`).
  - §5.1: "A case that passed only against a mock verified the mock". Every e2e criterion above rests on MSW fidelity to HO-ACC-001 §12.
- [MEDIUM] M3. Full-suite `--workers=1` long-run flakiness. There were 9 unrelated failures in one 26-minute run, all 30/30 on a fresh rerun. A `node.exe` was seen at about 3.3 GB, which suggests dev-server memory growth (TEST second pass). This is separate from R8's `--workers=2` route-compile contention (LOW, known).
- [MEDIUM] M4. U-B14 flake: `web/tests/components/auth/register-form.test.tsx:292-314`. **Not a product bug.**
  - Evidence:
    - `toBeDisabled()` and `aria-busy` both held after click 1;
    - `onSuccess` fired once;
    - the count was 2.
  - So the second click submitted only after request 1 settled. The mock delay is 30 ms. The harness's `onSuccess` is a `vi.fn()`, so the form stays mounted, filled and re-enabled.
  - In the product, `app/(auth)/register/page.tsx` swaps to `RegistrationSuccess` on success, and a failed submit clears password and answer, so a late click cannot resend.
  - `RegisterForm.tsx:186` disables on `isSubmitting || isPending`, and `isSubmitting` is set synchronously.
  - It failed in 2 of 2 full runs after the merge (integrator's and this gate's), so `npm run test` is red on a full run today. Fix it soon: gate the mock response on a manually released promise, as U-G7 does.
- [MEDIUM] M5. TC sheet rows claim more than their tests assert. E-X1, E-X2, A24 and A25 are covered by the HIGH above. Others:
  - U-X4 claims a storage audit "after every form test", but it runs once (sign-in 400).
  - U-B9's "challenge refetched" asserts only that `src` is truthy, which was already true.
  - U-B13's "working retry" never clicks retry.
  - U-G3's "no posts with no interaction over 10 min" half (the §11.9 "never on a timer" rule) is untested.
  - U-B4 does not assert the request body or `captchaId`.
  - The sheet header still lists "ee5e907 plus uncommitted work"; that work is now committed.
- [MEDIUM] M6. Out-of-§13 edits are recorded only in gate artifacts, not in §1 Amended:
  - `web/tests/unit/lib/register-schema.test.ts` (new; `tests/unit/**` is not in §13; precedent: ARCH third pass rated `pagination-caps.test.ts` MEDIUM);
  - `web/app/providers.tsx`'s `window.__apiFetch` hook (added by the TEST gate; gated `NODE_ENV !== 'production'`; reviewed by CODE_REVIEW seventh pass);
  - `tests/CatEcommerce.IntegrationTests/AccountsIntegrationTestBase.cs` (`ExtractSetCookieHeader`, an 11-line helper under the SECURITY item 7 authorisation; listed in SECURITY's scope check but not named in HO-ACC-001 §1).
  - Add one Amended row naming all three.
- [LOW] L1. From ARCHITECTURE sixth pass: E-G3 (`staff-auth.spec.ts:275`) cannot tell "no refetch attempted" from "a refetch absorbed by `isError && !data`". The unit tests in `idle-lock-context.test.tsx` carry the proof.
- [LOW] L2. From CODE_REVIEW eighth pass, the fetchQuery staleTime LOW: `IdleLock.tsx:147-151`. The ad hoc `fetchQuery(..., staleTime: 0)` on the shared session key briefly overwrites that query's persisted `staleTime` (10 000 → 0) until `useStaffSession` next renders. It heals itself here; add a comment before the pattern is reused.
- [LOW] L3. From CODE_REVIEW seventh pass: `resolveBystanderSessionInvalid`'s `await` slightly widens the existing window in which `IdleLock` could unmount before `router.replace`/`setStatus` lands. No unmount guard.
- [LOW] L4. Component size is carried from ARCHITECTURE and CODE_REVIEW against §11's "~150 lines":
  - `IdleLock` 361, `UnsavedChangesGuard` 323, `MergeReview` 233, `IdleLockContext` 230;
  - `RegisterForm` 203, `ProfileForm` 198, `StaffAccountForm` 186, `NotificationPreferenceMatrix` 179.
- [LOW] L5. The assertion-strength items in M5 are LOW each: the U-X4, U-B9, U-B13, U-G3 and U-B4 details.
- [LOW] L6. No test clicks CAPTCHA "New code" or checks its live-region announcement (A7 rests on source plus U-B10).
- [LOW] L7. No test visits `/staff/forgot-password` or asserts the "Set or reset password" link on `/staff/sign-in` (A16's second half rests on source).
- [LOW] L8. From TEST: U-C2's session-invalidated-before-navigate half, and R6's `queryClient.clear()` on sign-in, are shown only through e2e or a source read. The pages are not unit-tested. U-E7's guard integration is not driven on `NotificationPreferenceMatrix` itself.
- [LOW] L9. From CODE_REVIEW sixth pass (suggestion): `web/scripts/run-real-api-e2e.mjs` exits 1 on a signal-killed child without reporting `result.signal`.
- [LOW] L10. `docs/gates/HO-ACC-002-SECURITY.md` was last written at ac8c519 (2026-09-26). The later security-relevant deltas were reviewed only inside CODE_REVIEW's files: the lock pause, `__apiFetch`, and the F1 bystander clear-or-lock decision. That review is sound (U-G8 proves a genuine sign-out still clears), but it is not labelled SECURITY. A one-line addendum to the SECURITY file would close the paper trail.
- [LOW] L11. Other owners, recorded for them, not ACC-002's to fix:
  - (a) `web/lib/api/cats.ts:61-69,303-309`: the photo types are stale against FIL-001's `FileId`/signed-URL contract. This is identical on master and belongs to HO-CAT-002/HO-FIL-002.
  - (b) `web/next.config.ts` has no CSP or frame-ancestors headers. This predates the branch (SECURITY first pass) and belongs to HO-CAT-002.
  - (c) `docs/handover/HO-CAT-002.md` §1 table has blank lines that break its Markdown rendering (ARCHITECTURE fifth-pass note; a master-side issue).
  - (d) The `postcss` advisory through `next` (`npm audit`, build-time only; SECURITY first pass).

SCOPE CHECK
- Out-of-scope (§4) work found: **none**.
  - No OTP, verification, 2FA or recovery screens or wording. The only hit is a comment in `RegistrationSuccess.tsx` saying it must not.
  - No forbidden package in `package.json`: no auth library, zxcvbn, libphonenumber, state library, idle-timer or CAPTCHA SDK.
  - No remember-me, dark mode or i18n.
  - No `dangerouslySetInnerHTML`.
  - No web storage in app/components/lib.
  - No `useQuery`/`useMutation` outside `lib/hooks/` (comments only).
  - No `fetch(` outside `lib/api/client.ts` (comments only).
  - The only `createContext` is `StaffLockContext`, which holds no server data.
  - Orders, invoices and balances appear only in `NotYetRecordedNotice`.
  - The mobile lookup runs only after `MobileNumberAlreadyRegistered` (`customers/new/page.tsx:17`).
  - No audit-log screen.
- Files in allowed scope: 175/178. These are §13 globs plus files recorded in HO-ACC-002, HO-ACC-001 or HO-CAT-002 §1 Amended.
  - This covers: the backend edits (SECURITY item 7 / CR MEDIUM 2, HO-ACC-001 Amended (22)–(25)); `package.json`, `eslint.config.mjs`, `playwright.config.ts` and `scripts/run-real-api-e2e.mjs`; `app/page.tsx`, `DataTable.tsx`, `WeightTrendChart.tsx`, `dialog.tsx`, `CatProfileDataShell.tsx` and `mocks/handlers/areas/cat-profiles.ts`; the 12 `cat-*` specs, the 7 `staff-shell-*` specs, `home.spec.ts`, `fixtures.ts` and the four ACC e2e specs; `nav/areas/accounts.ts`; `README.md`; and the handover documents.
  - `portal-header.tsx` and `staff-shell.tsx` fall under §13's "`lib/identity.ts` DELETE, and its imports". Each change only replaces the `STAFF_IDENTITY` import with the session name and `SignOutButton`, which §8.3 asks for.
- Files outside allowed scope with no handover record: 3. They are recorded only in gate artifacts (M6):
  - `web/tests/unit/lib/register-schema.test.ts`
  - `web/app/providers.tsx`: the `__apiFetch` part only; the file itself is in scope.
  - `tests/CatEcommerce.IntegrationTests/AccountsIntegrationTestBase.cs`

AMENDED ROWS: ACCURACY
Each §1 Amended claim about shipped code was checked against the tree at f5067d6. All hold:
- `ProfileForm`'s current-password field (`purpose="current"`) and its 429 handling;
- `mobileMatchedOn` rendered;
- `app/page.tsx` uses client-side session hooks and no `next/headers`;
- `DataTable` has `role="region"` and `tabIndex={0}`;
- `WeightTrendChart` has `accessibilityLayer={false}`;
- Playwright reads `PORT`, and the `real-api` project is added only when `E2E_REAL_API` is set;
- the ESLint rule is `error`; the `lib/hooks/**` exemption is `off`; the `SignOutButton` override repeats the `paths` ban;
- `--max-warnings 0` is set;
- the script uses `createRequire`, `process.execPath` and `shell: false`;
- the `focusManager`/`onlineManager` `setEventListener` pause, `restoreManagers()` with `setOnline(navigator.onLine)`, and no live `cancelQueries`;
- `useSignInStaff` uses `networkMode: 'always'`;
- `UnsavedChangesGuard` uses `sentinelActiveRef` with no `useId`;
- `dialog.tsx` has `overlayClassName`;
- `IdleLock` passes `staleTime: 0` and captures `next` before the `await`;
- `StaffCustomer.cats`, the `CustomerStatusBadge` in `DuplicateGroupList`, and cat names in `MergeConfirmDialog`;
- `staffIdleLockBridge.ts` is gone;
- the README same-SITE and `CsrfCookieDomain` sections;
- `.env.local.example` points at `localhost:5005`;
- `__apiFetch` is inside the `NODE_ENV !== 'production'` block.

The rows are accurate as far as they go. They are incomplete for the three M6 files. No Amended row declares the E-X1/E-X2 route sampling (the HIGH).

UNDECLARED DEVIATIONS
- E-X1/E-X2 were narrowed from "every route in §8.3, clean / with errors / idle lock open" to 7 routes, clean state only. The narrowing is disclosed only in the spec's header comment (HIGH).
- Three out-of-§13 edits have no handover record (M6).

BACKLOG CONSOLIDATION (all open MEDIUM/LOW across the five gates, for docs/backlog/ACC.md)
M1 7 unwritten §16 e2e cases · M2 real-API pass never run (BLOCKED rows) · M3 dev-server memory growth / long-run flakiness · M4 U-B14 flake (test harness, fix soon: red on full runs) · M5 TC-sheet rows overstating assertions · M6 three unrecorded out-of-§13 edits · L1 E-G3 cannot detect a regressed pause · L2 fetchQuery staleTime on shared key · L3 unmount window in bystander resolve · L4 component size · L5 assertion strength (U-X4/U-B9/U-B13/U-G3/U-B4) · L6 "New code" untested · L7 staff first-password path untested · L8 page-level halves of U-C2/R6/U-E7 · L9 script signal reporting · L10 SECURITY paper trail after ac8c519 · L11 other owners' items: CAT/FIL photo types, CSP headers, HO-CAT-002 table rendering, postcss advisory · R8 `--workers=2` contention flakiness (known).
