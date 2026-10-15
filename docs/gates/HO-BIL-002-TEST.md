# HO-BIL-002 — TEST gate

Branch `ho/BIL-002`, after merging master (head `221ab7e3` plus the sheet commits). Case sheet: `docs/test-cases/TC-HO-BIL-002.md` (section "TEST gate re-run"). Date 2026-10-08/09.

## Verdict: PASS

No BLOCKER or HIGH finding. Four LOW/Note rows went to `docs/backlog/BIL.md` (50 to 53).

## Results

| Suite | Result |
|---|---|
| `tsc --noEmit` | clean |
| `npm run lint` (`--max-warnings 0`) | clean |
| vitest, whole suite | 159 files, 916 tests, 916 passed at `--maxWorkers=4`. At the default worker count one run had 7 timeouts/misses in 6 files (two in Billing); all passed on re-run, so load-sensitive, not a product fault (backlog 50) |
| `next build` | passes; Billing routes present |
| Playwright chromium, MSW, `billing-functional` | 15/15 (14/15 on the first cold run, E-D2 timed out at 5 s; E-D2 and E-B4 passed on a warm re-run, backlog 51) |
| Playwright chromium, MSW, `billing-a11y-responsive` | 31/31 |
| Playwright mobile-360, MSW, `billing-functional` | 15/15 |
| Real API (:5007, `catecommerce_dev`), `billing-real-api` | 14/14 after setting the missing MAIN billing profile (first attempt: E-A1 got 400 `BillingProfileMissing`, an environment gap) |
| E-E1 loyalty on the real API | NOT RUN: no loyalty data on the dev DB (passes on mocks) |

## Merge of master

`git merge master` was clean: no conflicts, no manual resolution (56 files, docs rows from both sides merged by git).

## Product bugs

None found. Not fixed, nothing to fix.

## Notes for acceptance

- The real run used existing `e2e.bil.*` staff; the requested `e2e.vet`/`e2e.accountant` names were not created (not needed).
- Backlog 52: no test asserts that the composer shows the server's reason when issue is refused.
