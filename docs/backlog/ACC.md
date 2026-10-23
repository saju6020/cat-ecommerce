# Backlog — Accounts web (HO-ACC-002)

Medium/low gate findings not blocking the merge (owner rule 2026-09-27: only BLOCKER/HIGH block a gate).
Fix in a later batch. Source: `docs/gates/HO-ACC-002-*.md`, `docs/test-cases/TC-HO-ACC-002.md`.

| # | Severity | Item | Source |
|---|---|---|---|
| 1 | MEDIUM | 7 §16 e2e cases never written: E-B2, E-B4, E-B5, E-B6, E-B7 (keyboard-only registration, no substitute), E-B8, E-J1 (only browser visit of `/portal/account/data*` and `/account-deleted`). | TEST / ACCEPTANCE |
| 2 | MEDIUM | Real-API e2e pass never run (BLOCKED rows incl. E-G1 reset-link half); needs API + Postgres running. | TEST |
| 3 | MEDIUM | Long `--workers=1` e2e runs flake (likely dev-server memory growth, ~3.3 GB); includes the sign-in e2e flake. | TEST / ACCEPTANCE |
| 4 | MEDIUM | TC rows claim more than their tests assert: U-X4 (storage audited once), U-B9 "refetched", U-B13 never clicks retry, U-G3 "never on a timer" half, U-B4 request body; stale TC sheet header. | ACCEPTANCE |
| 5 | MEDIUM | Full `vitest run` red in ~3 of 5 runs on 5 s timeouts (U-B4, U-B7, U-B9, U-C1, sign-in 429 test); files pass in isolation — load flakiness. | ACCEPTANCE |
| 6 | LOW | E-G3 cannot detect a regressed focus/online pause. | ARCHITECTURE |
| 7 | LOW | `resolveBystanderSessionInvalid`'s `fetchQuery` briefly overwrites the shared session query's `staleTime` (self-healing); document if the pattern is reused. | CODE_REVIEW |
| 8 | ~~LOW~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-ADM-WEB.** Was — Unmount window in `resolveBystanderSessionInvalid` (no guard before `router.replace`). | CODE_REVIEW |
| 9 | LOW | Components over ~150 lines: `IdleLock` (361), `UnsavedChangesGuard` (323). | ACCEPTANCE |
| 10 | LOW | No test clicks "New code" (CAPTCHA refresh). | ACCEPTANCE |
| 11 | LOW | No test visits `/staff/forgot-password`. | ACCEPTANCE |
| 12 | LOW | Page-level halves of U-C2, R6, U-E7 covered only by e2e or source. | ACCEPTANCE |
| 13 | LOW | Real-API script doesn't report the signal when its child is killed. | ACCEPTANCE |
| 14 | LOW | SECURITY gate file not updated since `ac8c519`; later IdleLock changes reviewed only inside CODE_REVIEW. | ACCEPTANCE |
| 15 | LOW | `/reset-password` scanned only in its invalid-link state; the valid-token form has no axe/overflow run. | ACCEPTANCE |
| 16 | LOW | E-X1/E-X2 don't assert the intended screen rendered (a sign-in redirect would pass silently); add URL/heading asserts. | ACCEPTANCE |
| 17 | LOW | Flakiness under `--workers=2` (route-compile contention). | CODE_REVIEW |
| 18 | Other owner | `web/lib/api/cats.ts` still uses the pre-FIL photo shape (HO-CAT-002/HO-FIL-002); no CSP headers in `next.config.ts`; blank lines break the HO-CAT-002 §1 table; `postcss` advisory. | ACCEPTANCE |
| A-APT1 | ~~LOW~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-ADM-WEB.** Was — `web/lib/validation/nextPath.ts:17-25` is a plain string-prefix test: `/book/..//evil` and `/portal/../..//evil` pass and normalise to `//evil`. Not an off-site redirect today (`router.push` stays same-origin), but becomes one if `next` ever reaches `location.href`, an `<a href>` or a `Location` header. Resolve with `new URL(next, origin)`, require same origin, prefix-check the normalised pathname, reject `\` and control characters. | HO-APT-002 SECURITY |
| A-APT2 | LOW | `UnsavedChangesGuard` lacks `consequence`/`onConfirm` props, so HO-APT-002 copied it as `LeaveHoldGuard` (APT row W14). | HO-APT-002 ARCHITECTURE |
| A-APT3 | LOW | Mirror in HO-ACC-002's §1 Amended the owner-approved edit HO-APT-002 made to `CustomerSessionGuard.tsx` (`?next=` carries the query string; owner decision 2026-10-04, HO-APT-002 b3377df). | HO-APT-002 ARCHITECTURE |
| A-SHP1 | LOW | Mirror in HO-ACC-002's §1 Amended the owner-approved HO-SHP-002 edits to HO-ACC-002 files: `tests/e2e/home.spec.ts` (signed-out `/` → `/shop`) and `app/(auth)/sign-in/page.tsx` (`/shop` on the `?next=` allow-list) — owner decision 2026-10-04, HO-SHP-002 ac3f9b3. | HO-SHP-002 ARCHITECTURE |

## Owner tuning 2026-10-19 (admin-set staff passwords), review round, non-blocking

| # | Severity | Item | Source |
|---|---|---|---|
| T1019-1 | MEDIUM | Setting a staff password (create-with-password and `PUT /api/staff-accounts/{id}/password`) does not ask the acting administrator to re-enter their own password. A stolen or unattended admin session could lock out other administrators. Options: require the admin's current password on both, and/or send an admin's own password change to a change-own-password flow. Owner to decide. | SECURITY gate (docs/gates/HO-ACC-001-owner-tuning-1019-SECURITY.md) |
| T1019-2 | LOW | An administrator setting their own password through the staff page is signed out on the next request (the stamp rotates). Safe, but the panel does not warn about it. | SECURITY gate |
| T1019-3 | LOW | `.MaximumLength` on the new password validators has no error code or message of its own (same as registration). | SECURITY gate |
| T1020-1 | LOW | Editing a staff member's mobile or email does not end their open sessions (owner, 2026-10-20: kept simple on purpose). If an identifier is ever changed because an account was compromised, set a new password too, which does end its sessions. Also, a staff reset token already sent to the old identifier stays usable for its 15 minutes; staff reset messages are not delivered in production. Option: cancel outstanding reset tokens when an identifier changes. | SECURITY gate (docs/gates/HO-ACC-001-staff-details-1020-SECURITY.md) |
