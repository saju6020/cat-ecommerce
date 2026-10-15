GATE: TEST
HANDOVER: HO-BRD-002
COMMITS: 5a187b6b..b1958d45 (tests and docs added on top by this gate)
DATE: 2026-10-15
STATUS: FAIL

One HIGH finding (row 76). Everything else passed or is MEDIUM/LOW and goes to `docs/backlog/BRD.md`. The case sheet is
`docs/test-cases/TC-HO-BRD-002.md` (section "TEST gate, 2026-10-15"; written before any command of the gate was run).

## Why FAIL

**Row 76 (HIGH): the owner's "View photo" link in the stay diary does not open the photo.** `web/lib/api/my-boarding.ts:371` returns
the API's signed file path as it came (`/api/files/{id}/content?exp=..&disp=inline&sig=..`) and `StayDiary.tsx:36` puts it in an `href`;
the browser resolves a path against the web origin and gets 404. `lib/api/files.ts` `resolveFileUrl` is the documented single place for
this and the cat and visit photos use it. Observed on the real API (T-R18, T-R18b): the staff upload succeeds, the entry appears in the owner's
diary with a "View photo" link whose href is `/api/files/b3864d53-...`; a fetch of that path against the web origin returned 404, and the
same path on the API origin returned `200 image/png`. The owner decision "Yes, diary photos too" therefore does not deliver a photo to the owner.
The fix is one line (`fileUrl: resolveFileUrl(...)`) plus a wire test. No production code was changed by this gate.

## Counts

| | T- rows (51) | Owner-decision rows (5) |
|---|---|---|
| PASS | 45 | 4 |
| FAIL | 4 | 1 |
| BLOCKED | 2 | 0 |
| NOT RUN | 0 | 0 |

FAIL rows: T-F1 (inventory `goods-receipt-form`, the known flake: 3 of 5 solo runs passed; outside Boarding, no change under inventory), T-R18 (row 76),
T-G13 and T-G14 (the LOW rows 74 and 75, open since the re-check, not regressions). BLOCKED: T-F3 (the SRG timeline test is not in this branch),
T-G6 (`overriddenOn` rendering: no test, no real stay with an override).

## What was run

| Run | Result |
|---|---|
| `tsc --noEmit`, `eslint . --max-warnings 0`, `next build` | all exit 0 (tsc and eslint re-run after the additions) |
| `vitest --maxWorkers=2` full | 1592 of 1594 pass; 2 failures in `inventory/shared-pieces` (outside Boarding), 17/17 alone. Boarding folder with the new tests: 84/84 |
| Boarding MSW Playwright, chromium | 69 of 69 |
| Boarding MSW Playwright, mobile-360 | 68 of 69; the failure was a 60 s MSW-ready wait in `signInEstablished` (a setup race), and the same test passes alone |
| Real API (`ho/BRD-001` 2216ff79, whose `src/` equals cede66fe), `boarding-real-api.spec.ts` | the existing 9 flows pass; new: vet-limit reason (refused, then saved), reload at review keeps the quote, Enquiry shows no balance, summary labels after a final invoice and after a cycle invoice, late check-out preview and submit, the `UnitOverstayed` banner on the next booking, diary photo upload, sign-in return to /boarding, axe on 16 real pages (zero serious/critical). One fail: T-R18b (row 76) |

The real-API pass was split in phases because a late check-out and a cycle invoice need stays whose dates are in the past: the spec creates the stays
(`LATE-SETUP`), two SQL statements move them (the stays, their cats and allocations; and the charges' service dates), and `LATE-RUN` runs against them.
Details, the seed requirements (Rabies vaccine, 0% tax rate and billing profile, units, rate cards) and the removal list are in the sheet and the spec header.

Seed gaps found and fixed in the throwaway seed (not product defects): the first run had no Rabies vaccine (the gate message names the vaccine), the
second had no billing profile (the worker was refused with `BillingProfileMissing`, so no final or cycle invoice), the third had charges that were not moved with the stay.

## Mutation spot-checks (T-M)

Each mutation was applied to the production file, the Boarding vitest folder run, and the file restored (`git status` clean afterwards).

| Mutation | Original 77 tests | After this gate's 7 new tests |
|---|---|---|
| null balance: adapter, StayCharges | killed (3 and 2 tests) | killed |
| null balance: ReservedPanel | **survived** | killed |
| vet-authority reason: requirement removed; reason not sent | **survived both** | killed (both) |
| `UnitOverstayed` label emptied | killed (constant only) | killed |
| early/late from the browser clock | **survived** | killed |
| signature: check-in mutation not reset; check-in drafts not cleared; check-out mutation not reset | **survived** | killed (3) |
| signature: check-out drafts not cleared | survived | survives: unobservable (the wizard swaps to the result panel); row 79, harmless |
| `normaliseGate` pass-through | killed (2) | killed |
| quote re-fetch removed | **survived** | killed |

The survivors were weak spots in the component tier, not product bugs: the form, reload and wizard behaviours were covered only by the real-API spec.
The new file is `web/tests/components/boarding/test-gate.test.tsx`; no existing test was deleted or weakened.

## Findings (rows for `docs/backlog/BRD.md`, numbered from 76)

| # | Sev | Status | Where | Item |
|---|---|---|---|---|
| 76 | **HIGH** | Fixed (fix round 3: `resolveFileUrl`, wire test) | `lib/api/my-boarding.ts:371` | Diary "View photo" href is API-relative; resolve it with `resolveFileUrl`. Add a wire test with an API-relative url. |
| 77 | MEDIUM | **Resolved** (fix round 5, with row 96). Fixed in the owner screens (`DepositSummary`) in 7dfcbd10 and in the staff screens (workspace header, `DepositPanel`, `StayList`, My stays, dashboard) in fix round 4 -- see row 86; BE row 84 open | `StayOverview`, `DepositSummary`, `StayWorkspace` header; BE `deposit` | An Enquiry reads 'Deposit ৳0.00 / No deposit needed / Nothing to pay up front' because the API sends `required 0.00, NotRequired`. FE: say the deposit is shown when reserving; BE: send none before reserving. |
| 78 | MEDIUM | Fixed in this gate | `tests/components/boarding/test-gate.test.tsx` | Seven tests pin the reason requirement, the reserved panel's null balance, the quote re-fetch, the server-decided early/late and the signature hygiene. |
| 79 | LOW | Open (no action) | `CheckOutWizard.tsx:84` | Clearing the check-out drafts after success is unobservable. |
| 80 | LOW | Fixed (test) | `IncidentForm.tsx:74-80` | The incident date/time fields and their future-time refusal have no test. |
| 81 | LOW | Partly fixed (`overriddenOn` rendering is tested; the `lineRef` charge kind is not) | tests | No test renders `overriddenOn` or reads the summary charge kind from `lineRef`; the `UnitOverstayed` pin is the label constant (the banner is pinned only on the real API). |
| 82 | LOW | Open (joins 51) | `boarding-real-api.spec.ts` | A committed seeder and the two SQL helpers are still owed (the throwaway was deleted as instructed). |
| 83 | LOW | Open | E-X3 | NFR-PRF-3 is still unmeasured; needs `next start` against the seeded API. |
| 74, 75 | LOW | Open (known) | see ARCH re-check | Summary charge kind / `approvedIncrease`; the swallowed re-quote failure. Still present at b1958d45. |

| 85 | **HIGH** | Deferred by owner decision 2026-10-15 ("Measure at merge"): the integrator measures p95 on the real API after BRD-001 merges | E-X3 / §18 A29; TC sheet | NFR-PRF-3 has never been measured, so A29 has no evidence. Supersedes LOW row 83. Close it **either** way: (a) measure it; (b) the owner explicitly defers A29, e.g. to the integrator's post-merge real-API run once the committed seeder exists (row 82). For (a): run `next build && next start` with mocks off against a seeded HO-BRD-001 API (head of `ho/BRD-001`), load `/staff/boarding` and `/staff/boarding/tasks` 20 times each, and record the first meaningful content (first section card or task group visible) and the p95 on the TC sheet. Then an ACCEPTANCE re-check |
| 86 | MEDIUM | **Resolved** (fix round 5, with row 96). Fixed (fix round 4: `DepositStateBadge` and `DepositPanel` say the deposit is set at reserve for an Enquiry; tested) | `StayWorkspace.tsx:104`, `DepositPanel.tsx:63`, `StayList.tsx:90`; `docs/gates/HO-BRD-002-TEST.md` row 77 | Row 77 is only half fixed. Staff screens still show "No deposit needed" for an Enquiry; only `DepositSummary` (owner) was changed. Apply the same Enquiry wording to the staff badge sites, or hide the badge for an Enquiry. Correct row 77's status to "Partly fixed" |
| 87 | MEDIUM | Open | `ReferralPanel.tsx`; tests | A19's staff half has no UI test on either run: refer, headroom, request approval, Cat is back and the staff answer. Add an MSW e2e for E-E1 (refer, request above the limit, the owner approves in the portal, the staff panel shows the decision) and drive it once on the real API |
| 88 | LOW | Open | `boarding-booking.spec.ts` | E-A3's first half is untested. Add an MSW case reserving two cats into a Family unit with two care-profile tabs, then four cats refused |
| 89 | LOW | Open | `IsolationPanel.tsx` | U-E7 (Record entry and Record exit with staff name and time) and U-E6's `NoIsolationUnitAvailable` refusal have no test |
| 90 | LOW (FE + BRD-001) | Shipped by owner decision 2026-10-15 ("Ship, follow-up fixes"); follow-up BRD-001-FU1 plus a screens follow-up | `StayWorkspace.tsx:180-186`, `StaffStayBookingForm.tsx:47` | Override vaccination is offered to any staff member with a blocked cat, because `canOverrideGate` is not on the wire. A non-admin learns it is not allowed only from the server's refusal. Ask BRD-001 for the flag (ADR-FE-107). Add an override-specific test of the mandatory reason |
| 91 | LOW (carried) | Open (carried to the next real-API run) | `boarding-real-api.spec.ts:768` | Re-run T-R18b at the next real-API run: the owner's View photo opens `200 image/*` from the API origin. The fix 7dfcbd10 is proven by the wire test and the observed API response, not by an observed click |
| 92 | MEDIUM (BRD-001) | Shipped by owner decision 2026-10-15; follow-up BRD-001-FU1 plus a screens follow-up | dashboard `callOwner[]`, incident queue | The real wire carries no customer name or phone, so Call owner reads "call the owner" with no `tel:` link. That defeats §11.9's purpose for the person who must call. Add `customerName` and `customerPhone` to `callOwner[]` and the incident queue rows. Owner question below |
| 93 | LOW (process) | Noted | `docs/gates/HO-BRD-002-TEST.md` (edited by 7dfcbd10) | The fix commit rewrote the TEST record's status cells (rows 76, 77, 80, 81) and added row 84 outside the findings table, while the record's `STATUS` still reads FAIL. Gate records should be changed by their gate. Builders should report fixed rows in the handover §1 or the commit message |
| 94 | LOW (integrator) | Open (integrator) | `docs/backlog/BRD.md` | BRD-002 rows 45-84 (and 85-95 here) collide with BRD-001's rows 45-60. Renumber on merge and record the old-to-new mapping |
| 95 | LOW | Open | `boarding-a11y-responsive.spec.ts:120` | E-X2 covers the five heaviest routes, not every §8.1 route. Extend the width loop to the route list E-X1 already uses |

## Checked and found correct

The vet-limit change on a `Reserved` stay is refused with no request sent until a reason is given, and is saved with one; a reload at the review step re-quotes
and keeps Reserve enabled; an Enquiry's review, My stays row, owner page, staff workspace tabs and list row carry no balance; the owner summary names
"Not yet invoiced" and "Invoiced, still to pay" and never "Balance due", both after the final and after a cycle invoice; a late check-out shows the server's
wording without a past "free from" clause, is not refused, and flags the next booking with "The previous cat in this unit has not left yet: move this booking.";
belongings and diary photos upload through the shared `FileUploader`; sign-in returns to the same `/boarding` search; axe is clean on real data.

| 84 | LOW | Open (BRD-001) | `StayViewModel.deposit` | Send no deposit (null) before the stay is reserved, instead of 0.00 / NotRequired, so no client can read an Enquiry as "no deposit needed". Boarding FE shows "worked out when you reserve" for an Enquiry meanwhile. |

## Scope

Added or changed by this gate: `docs/test-cases/TC-HO-BRD-002.md`, `docs/gates/HO-BRD-002-TEST.md`, `web/tests/e2e/boarding-real-api.spec.ts`,
`web/tests/components/boarding/test-gate.test.tsx`. Production code: none changed (row 76 is reported, not fixed).

Note (fix round 4): rows 85-95 are the ACCEPTANCE gate's (a41be7eb), copied here with their status after the owner decisions of 2026-10-15; this record's STATUS line is the gate's own verdict and is not changed by the fix commits.
