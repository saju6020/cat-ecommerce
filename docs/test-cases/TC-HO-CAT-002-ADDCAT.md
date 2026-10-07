# TC-HO-CAT-002-ADDCAT — Customer "add a cat" screen

Handover: docs/handover/HO-CAT-002-ADDCAT.md
Commit(s) under test: 82ae258 (range 578dc79..82ae258)
Written: 2026-10-06  ·  Executed: 2026-10-06/07

> Statuses record what was **observed**, not inferred. `BLOCKED` marks a case that could not be
> executed or was reasoned about rather than watched.
> The handover's section 7 gives IDs U-AC1..U-AC11, U-RP1, U-RP2, E-AC1..E-AC3; they are used as is.
> U-RP1 and U-RP2 map to the `describe` blocks in `booking-return-path.test.ts`.

## Summary

| | Count |
|---|---|
| Total | 33 |
| PASS | 33 |
| FAIL | 0 |
| BLOCKED | 0 |
| NOT RUN | 0 |

Rows E-R1..E-R3 aggregate the wide Playwright run; its 11 first-run failures are classified below.

## Environment

Worktree D:/my-repo/cat_ecommerce-wt/CAT-002-ADDCAT, head 82ae258 (plus the sheet commit). tsc, eslint,
vitest (140 files, 715 tests) in web/. Playwright against `next dev` with the MSW mock API (no real
backend, no database, no broker), PORT=3105, --workers=1, one server at a time; master baseline at
D:/my-repo/cat_ecommerce (efc107e), PORT=3101. The real backend was not started, so nothing here proves
the live `POST /api/cats` F19 owner replacement (that is the backend's own test).

## Wide Playwright run (chromium + mobile-360; cat-*, appointments-*, shop-portal, account-area)

Run 1: 312 tests, 300 passed, 1 skipped, 11 failed (25 min). Warm rerun of the 11 (line-number
selectors ran both projects, 20 tests): 4 failed (vaccinations E9 x2, shop E-D3 x2). Master baseline.

| Failure (run 1) | Warm rerun | Master | Classification |
|---|---|---|---|
| chromium appointments-calendar:38 E-E1 drag | pass | not needed | flake |
| chromium appointments-calendar:94 E-E1 busy | pass | not needed | flake |
| chromium cat-parasite-control:18 | pass | not needed | flake |
| chromium cat-profile-header:24 E3 | pass | not needed | flake |
| mobile cat-profile-header:41 E4 | pass | not needed | flake |
| mobile cat-vaccinations:52 E8 | pass | not needed | flake |
| mobile cat-a11y:72 | pass | not needed | flake |
| mobile cat-add-customer:55 E-AC3 | pass | not needed | flake (passed in the earlier 6/6 run, the warm rerun and three later runs) |
| chromium + mobile cat-vaccinations:63 E9 | fail x2 | fails x2 (also 2 more master runs) | pre-existing |
| chromium shop-portal:79 E-D3 | fail (both projects) | passes (3 master runs) | flake: passed on branch in 3 later runs, including the identical 10-test sequence and cat-add-customer + E-D3 together |

E-D3 deserves a note: it failed in run 1 (chromium) and in the first rerun (both projects), then
passed on the branch in three further runs. It was not reproducible and no code path of this slice
touches /portal/cats/{id} or the food page, but it is the one flake that failed twice in a row, so it is
listed as a MEDIUM backlog item rather than ignored.

## Unit cases

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| U-AC1 | customer-mode CatForm has no owner field | no Owner control, button says Add cat | PASS | vitest verbose on the 3 files: 26/26 pass; name seen as passing in verbose output |
| U-AC2 | empty name refused | plain-words error, nothing sent, values kept | PASS | vitest verbose on the 3 files: 26/26 pass; name seen as passing in verbose output |
| U-AC3 | Exact precision without date | error shown, nothing sent | PASS | vitest verbose on the 3 files: 26/26 pass; name seen as passing in verbose output |
| U-AC4 | server 400 mapped to field | error on its field, typed values kept | PASS | vitest verbose on the 3 files: 26/26 pass; name seen as passing in verbose output |
| U-AC5 | success redirect and POST body | goes to /portal/cats/{id}; body has empty-GUID owner, species Cat, null DOB | PASS | vitest verbose on the 3 files: 26/26 pass; name seen as passing in verbose output |
| U-AC6 | /book next | returns to /book with params and new catId | PASS | vitest verbose on the 3 files: 26/26 pass; name seen as passing in verbose output |
| U-AC7 | off-site / non-book next | ignored, cat page | PASS | vitest verbose on the 3 files: 26/26 pass; name seen as passing in verbose output |
| U-AC8 | server error on page | typed values kept | PASS | vitest verbose on the 3 files: 26/26 pass; name seen as passing in verbose output |
| U-AC9 | empty cat step | Add a cat link with next carrying params | PASS | vitest verbose on the 3 files: 26/26 pass; name seen as passing in verbose output |
| U-AC10 | OwnerId server error, customer mode | form-level message, not hidden field | PASS | vitest verbose on the 3 files: 26/26 pass; name seen as passing in verbose output |
| U-AC11 | double click after success | button disabled, exactly one POST | PASS | vitest verbose on the 3 files: 26/26 pass; name seen as passing in verbose output |
| U-RP1 | open-redirect payloads (dot-segments, encoded dots, backslash, //, other origin, /book/../portal/account) | all fall back to cat page; valid /book?.. returns with catId | PASS | vitest verbose on the 3 files: 26/26 pass; name seen as passing in verbose output |
| U-RP2 | buildAddCatHref | carries every booking parameter, round-trips | PASS | vitest verbose on the 3 files: 26/26 pass; name seen as passing in verbose output |
| U-X1 | whole vitest suite, --maxWorkers=3 | no failures | PASS | 140 files / 715 tests passed, 131s, --maxWorkers=3 |
| U-X2 | tsc --noEmit | clean | PASS | tsc --noEmit exit 0 |
| U-X3 | eslint | clean | PASS | eslint app components lib tests mocks exit 0 |

## Integration cases

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| E-AC1 chromium | add from My cats, see it, book with it | passes | PASS | cat-add-customer run: 6 passed (35.4s) |
| E-AC1 mobile-360 | same | passes | PASS | cat-add-customer run: 6 passed (35.4s) |
| E-AC2 chromium | empty name keeps page and values | passes | PASS | cat-add-customer run: 6 passed (35.4s) |
| E-AC2 mobile-360 | same | passes | PASS | cat-add-customer run: 6 passed (35.4s) |
| E-AC3 chromium | booking detour returns to /book with cat chosen | passes | PASS | cat-add-customer run: 6 passed (35.4s) |
| E-AC3 mobile-360 | same | passes | PASS | cat-add-customer run: 6 passed (35.4s); FLAKED once in the wide run (failed), passed on warm rerun and in 3 further runs |
| E-R1 | regression set chromium: cat-*, appointments-*, shop-portal, account-area | no failure that is a regression vs master | PASS | wide run chromium+mobile-360: 300 passed, 1 skipped, 11 failed; after warm rerun and master baseline no regression (see classification below) |
| E-R2 | regression set mobile-360: same files | same | PASS | same run; see classification |
| E-R3 | every persistent failure re-run on master (PORT 3101) and classified | each classified pre-existing / flake / regression | PASS | vaccinations E9 fails on master too (pre-existing); the other 10 passed on warm rerun or in later runs (flake) |

## Mutation cases

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| M1 | safeBookingPath skips the post-parse check | U-RP1 payload tests fail | PASS | removed origin/path/final checks after URL parse: 5 U-RP1 payload tests failed (/book/..//evil, %2e%2e, .%2e, ./.., /book/../portal/account); reverted with git checkout |
| M2 | remove the `opening` guard in page.tsx | U-AC11 fails | PASS | isSubmitting={registerCat.isPending} only: U-AC11 failed (1 failed, 9 passed); reverted |

## Regression cases from earlier gates

| ID | Gate | Finding | Case | Status | Evidence |
|---|---|---|---|---|---|
| R1 | ARCHITECTURE | M1 OwnerId error routed to hidden field | U-AC10 | PASS | U-AC10 passed |
| R2 | SECURITY | HIGH open redirect via ?next= normalisation | U-RP1 + M1 | PASS | U-RP1 passed and M1 shows it detects the bug |
| R3 | CODE_REVIEW | M double-click duplicate | U-AC11 + M2 | PASS | U-AC11 passed and M2 shows it detects the bug |
| R4 | CODE_REVIEW | M U-AC5/6 assert no POST body | U-AC5 body assertion | PASS | U-AC5 asserts the exact POST body; passed |
| R5 | CODE_REVIEW | M U-AC9 only proves prop rendering | U-RP2 | PASS | U-RP2 passed (buildAddCatHref round-trip); U-AC9 still only proves the prop, covered by U-RP2 |
| R6 | CODE_REVIEW | LOW staff form still shows Owner after `audience` change | staff CatForm/cat-register tests | PASS | cat-form.test.tsx (staff) types into "Owner customer ID" and passes; cat-register e2e passed in the wide run (chromium and mobile-360) |

## Acceptance-criteria coverage

| Criterion | Covered by | Status |
|---|---|---|
| 1 open from My cats, register without owner | E-AC1, U-AC1, U-AC5 | PASS |
| 2 plain-language errors, values kept | U-AC2, U-AC3, U-AC4, U-AC8, E-AC2 | PASS |
| 3 success to cat page or back to /book | U-AC5, U-AC6, E-AC1, E-AC3 | PASS |
| 4 staff registration unchanged | cat-register e2e + existing staff unit tests (R6) | PASS |
| 5 no backend/migration/dependency | git diff 578dc79 HEAD --name-only: no src/, tests/, .ai/, package.json or lockfile path | PASS |
| 6 OwnerId error as form-level message | U-AC10 | PASS |
| 7 unclean next falls back | U-AC7, U-RP1, M1 | PASS |

## Cases I could not run

| ID | Why | What would unblock it |
|---|---|---|
| (none blocked) | Live-API behaviour of POST /api/cats for a customer was not exercised: Playwright uses the MSW mock | the real-api project / backend integration tests (HO-CAT-001) |

## Gaps left deliberately

| Gap | Why not covered | Suggested owner |
|---|---|---|
| Truly empty cat list in e2e (U-AC9 is component only) | shared mock not owner-scoped, known in the handover | CAT backlog (owner-scoped list) |
| BookingFlow-level test of the multi-parameter `next` (CODE_REVIEW M3) | covered by U-RP2 on the helper and E-AC3 on single param; component wiring in BookingFlow.tsx is not itself unit tested | CAT/APT frontend |
| Mock fallback treats any blank ownerId as the customer (ARCH/CR LOW) | mock-only divergence | CAT backlog |
