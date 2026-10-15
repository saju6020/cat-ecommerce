# TC-HO-BIL-002 — Billing and Payments (frontend)

Handover: `docs/handover/HO-BIL-002.md`. Branch `ho/BIL-002` from master `0442f9ff`. Mock runs and real-API run are recorded **separately**.

## How it was run

| Run | Command / setup | Result |
|---|---|---|
| Typecheck, lint | `npx tsc --noEmit`, `npm run lint` | clean |
| Unit/component | `npx vitest run` | 157 files, 905 tests pass (37 new, in `tests/components/billing/`) |
| Build | `npm run build` | passes; every Billing route is in the output |
| E-series on MSW | `PORT=3112 npx playwright test tests/e2e/billing-functional.spec.ts --workers=1` per project | chromium 15/15, mobile-360 15/15 |
| E-X1/E-X2 on MSW | `tests/e2e/billing-a11y-responsive.spec.ts` (chromium) | 31/31: 29 routes + the print views + dialogs, 360/768/1024/1440, axe serious/critical = 0 |
| E-series on the REAL API | API from this worktree on :5007 (`Cors__AllowedOrigins__0=http://localhost:3112`), web on :3112 with `NEXT_PUBLIC_USE_MOCKS=false`, `BIL_REAL_API=1 BIL_SEED=<seed.json>` `tests/e2e/billing-real-api.spec.ts` | 13/13 (serial) |

Real-run seeding: a scratch console app (outside the repo, deleted) wrote one administrator, one reception and one accountant staff member with passwords, a branch with a billing profile, a 15 % tax class with a rate in force, and three walk-in customers into `catecommerce_dev`. Nothing else was touched; the backend integration suite was not run.

## U-series (component)

| ID | Evidence | Result |
|---|---|---|
| U-A1 groups, select all in group | `documents-ops.test.tsx` | PASS |
| U-A2 preview debounce, keep previous | hook uses a 400 ms debounce and `keepPreviousData`; no timing test | NOT TESTED (code only) |
| U-A3 estimate banner, rate per line | `documents-ops.test.tsx` | PASS |
| U-A4 `ChargeAlreadyInvoiced` marks and unticks | `documents-ops.test.tsx` | PASS |
| U-A5 issue confirm text | `documents-ops.test.tsx` | PASS |
| U-A6 draft `409` reload | e2e E-A2 (mock and real) covers the stale/already-issued path | PASS (e2e) |
| U-B1 invoice lines and seller snapshot | `documents-ops.test.tsx` | PASS |
| U-B2 no edit control | `payment-flows.test.tsx` | PASS |
| U-B3 credit wizard, keep-as-credit, refund now | e2e E-C1/E-C3 (mock and real) | PASS (e2e) |
| U-C1, U-C2, U-C3 method flags | `payment-flows.test.tsx` | PASS |
| U-C4 card number: blocked, no request, value not echoed | `payment-flows.test.tsx` | PASS |
| U-C5/U-C6 one key across retry, renewed after success | `payment-flows.test.tsx` | PASS |
| U-C7 pending result ends the loop | `payment-flows.test.tsx` | PASS |
| U-C8 split loop | `payment-flows.test.tsx` (920: 400 then 520, then *Invoice paid*) | PASS |
| U-C9 own payment: disabled with reason, forced request mapped | `payment-flows.test.tsx` | PASS |
| U-C10 reverse confirm text | e2e E-B4 (mock) | PASS (e2e) |
| U-D1, U-D2, U-D3 cash-up | `documents-ops.test.tsx` | PASS |
| U-E1 statement file bounds | `money-guard.test.ts` | PASS |
| U-E2 malformed rows listed | `documents-ops.test.tsx` | PASS |
| U-E3 keyboard match | `documents-ops.test.tsx` | PASS |
| U-F1 portal How to pay, no pay-online | `documents-ops.test.tsx` | PASS |
| U-F2 ageing CSV = server strings | `money-guard.test.ts` | PASS |
| U-X1 server `400` mapped to its field, `errorCode` not rendered | e2e E-B3 (real: server message under the field); no dedicated per-form test | PARTIAL |
| U-X2 unsaved-changes on composer/payment/credit/cash-up | guard is mounted on all four; no navigation test | NOT TESTED (code only) |
| U-X3 lint rule | `money-guard.test.ts` (ESLint API on an injected `Number(amount)`) | PASS |
| U-X4 money accessible text | `money-guard.test.ts` | PASS |

## E-series

| ID | Mock (chromium / mobile-360) | Real API |
|---|---|---|
| E-A1 three business lines, one numbered invoice, tax per line | PASS / PASS | PASS (charges created as manual fees in the three lines; the clinic/boarding item sources are Phase J and not built) |
| E-A2 second issue of the same draft | PASS / PASS (issued "elsewhere" through a direct call) | PASS (second call refused) |
| E-B1 split tender 400 / 350 / 400 | PASS / PASS | PASS (three receipts, invoice Paid) |
| E-B2 60 000 pending, approved in another context | PASS / PASS (seeded pending payment) | PASS (reception records, accountant approves) |
| E-B3 card number blocked, nothing sent | PASS / PASS | PASS, plus the real server refuses it with `ReferenceLooksLikeCardNumber` and does not echo it |
| E-B4 reverse and re-enter | PASS / PASS | PASS |
| E-C1 credit note, credit kept, apply to another invoice | PASS / PASS | PASS |
| E-C2 corrected re-issue linked both ways | PASS / PASS | PASS |
| E-C3 refund cash | PASS / PASS | PASS |
| E-D1 short count, explanation, close | PASS / PASS | PASS |
| E-D2 COD remittance with shortfall | PASS / PASS | PASS |
| E-D3 statement import, auto-match, manual match | PASS / PASS | PASS (the real auto-matcher matched the unique line and left the other for the manual match) |
| E-D4 identical export twice | link only (the browser download is not interceptable by MSW) | PASS (same bytes, same `X-Export-Sha256`) |
| E-E1 loyalty redemption on a draft | PASS (mock) / PASS | NOT RUN: needs `billing.loyaltyEnabled` and accrued points on the dev database |
| E-F1 portal invoice, PDF, other customer's id | PASS / PASS | PASS (registered customer, real PDF `application/pdf`, not-found state) |
| E-X1/E-X2 | PASS (29 routes, dialogs, print) | PASS (12 staff routes at 360 and 1440) |

## Findings

1. **Contract differences found only against the real API (HO-BIL-001 wins, §20.4):** the credit-note result's `releasedToCustomerCredit`, the apply-credit result (`applied[].amount`, `freeCreditRemaining`, `invoiceOutstanding`) and the remittance result's `unapplied` are bare decimals, not money objects. The client reads either shape (`moneyFromWire`, text only, no arithmetic); the mocks now emit the real shapes. Found because E-C1 showed no result screen.
2. The real server accepts a money amount sent as a JSON string as well as a number.
3. After a payment reversal the payment reads `Reversed` slightly before its invoice returns to `Issued`; an immediate re-entry was refused with `InvoiceNotIssued`. The real spec polls.
4. A reference containing a 13-digit run (a timestamp) was refused by the server's card guard (about 1 in 10 are Luhn-valid) — expected, noted for anyone building references.
5. PDF, CSV and ZIP downloads are plain links: an anchor cannot send `X-Branch-Id`, so the server uses the caller's primary branch. Wrong for a user working at a non-primary branch (open question).
6. The pending-approval explanation cannot quote the threshold: no field carries it.
7. Charges carry no per-row action list, so *Void* is offered on every unbilled charge and the server decides.

## Fix round 1 (gate findings) — 2026-10-08

| Finding | Change | Evidence | Result |
|---|---|---|---|
| ARCH BLOCKER: ADR copy in `.ai/**` | reverted the `.ai/decisions/adr.md` hunk; `web/docs/decisions.md` keeps ADR-FE-079..086 | `git diff 0442f9ff HEAD -- .ai` is empty | PASS |
| ARCH HIGH: downloads without `X-Branch-Id` | `downloadBillingFile` (shared `xhrRequest`: CSRF, `X-Branch-Id`, same error mapping) behind `DownloadButton` for invoice PDF, credit-note PDF, invoice CSV, payment CSV; the upload reuses the same helper; ZIP export and portal PDFs unchanged | `downloads.test.tsx` (header sent, error shown); real E-DL (header equals the branch, a real `.pdf`/`.csv` file is saved) | PASS |
| CR HIGH: allocation rows bound by index | allocations are React state keyed by invoice id and built from the live invoice list at submit | `payment-flows.test.tsx`: tick, refetch with a reordered/changed list, submit, assert the invoice id | PASS |
| CR HIGH: composer retry | `syncDraft` before every save and issue: creates once, reads the real version, calls `setCharges` whenever the selection differs | `documents-ops.test.tsx`: create, issue fails, change selection, issue again: one create, one PUT with the new ids, issue carries the version after the PUT; saved draft saves a changed selection | PASS |
| CR MEDIUM: unsaved ticks wiped | a changed draft keeps unsaved ticks and says so | `documents-ops.test.tsx` | PASS |
| SEC MEDIUM: refund idempotency key | key renewed on close, on open and whenever the request differs; a retry of the same request keeps its key; success shows amount and method | `refund-dialog.test.tsx` (2) | PASS |
| SEC LOW: card number in filters | invoice-number and courier-code filters refuse a card number and write nothing to the URL | `downloads.test.tsx` | PASS |
| SEC LOW: export branch | the export defaults to the active branch; "All branches" is an explicit choice (`branch=all`) | e2e E-D4 link check | PASS |
| CR MEDIUM: U-A2, U-X2 missing | tests added (one preview request after the debounce; the dirty composer asks before a link navigates away) | `documents-ops.test.tsx` | PASS |

Re-run totals: vitest 159 files / 916 tests; mock e2e functional chromium 15/15 and mobile-360 15/15, a11y/responsive 31/31; real API 14/14 (adds E-DL); tsc, lint and `next build` clean.

## TEST gate re-run — 2026-10-08 (after merging master, head 221ab7e3)

Written before execution; every row starts NOT RUN and is updated one at a time as observed.
The case IDs (U-, E-) are those above; this table records what the gate itself watched.

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| G1 | `npx tsc --noEmit` | no errors | PASS | exit 0, no output |
| G2 | `npm run lint` | no errors | PASS | `eslint . --max-warnings 0` exit 0, no output |
| G3 | `npx vitest run` (whole suite) | all pass | PASS | `--maxWorkers=4`: 159 files, 916 tests, 916 passed, 0 failed (JSON reporter). An unconstrained first run (159 workers) had 7 timeouts/assertion misses in 6 files (appointments, booking, calendar, front-desk, templates, file-upload, plus billing U-A1/U-C1 timing out at 5 s); all 32 files re-run at 3 workers passed 257/257 and the full suite at 4 workers passed. Load-induced flakiness (5 s default timeout), not a product fault; logged as a backlog row |
| G4 | `npx next build` | passes, Billing routes present | PASS | build completes after `rm -rf .next`; 22 lines matching billing in the route table |
| G5 | `billing-functional.spec.ts` chromium, MSW, PORT 3112, workers=1 | 15/15 | PASS | First full run on a cold dev server: 14/15 (E-D2 failed a 5 s wait for "Shortfall lines"); an earlier combined run also failed E-B4 at 194 ms when the shell timeout cut it. Both re-run alone on a warm server and passed (E-D2 3.9 s, E-B4 7.3 s); the other 13 passed in the full run. Treated as cold-compile timing, not a product fault; backlog row 51. So the result is 15/15 observed in total, but not in one clean run |
| G6 | `billing-a11y-responsive.spec.ts` chromium, MSW | 31/31 | PASS | 31/31 ok (26 routes + 4 + dialogs, listed) |
| G7 | `billing-functional.spec.ts` mobile-360, MSW | 15/15 | PASS | 15 passed (1.5m) |
| G8 | `billing-real-api.spec.ts` against the real API on :5007 (E-A1..E-F1, E-DL) | 14/14 | PASS | 14 passed (3.1 m), API from this worktree on :5007 against catecommerce_dev, web on :3112 with mocks off. First attempt failed E-A1 (13 not run): `POST .../issue` returned 400 `BillingProfileMissing` because the MAIN branch had no billing profile in the dev DB (environment, the product refused correctly). Set one through `PUT /api/admin/billing-profiles/{MAIN}` as the seeded `e2e.bil.admin` (204), then re-ran from the start: 14/14 |
| G9 | E-E1 loyalty on the real API | needs loyalty data | NOT RUN | needs `billing.loyaltyEnabled` and accrued points on the dev database; no such data was seeded. The mock E-E1 passed (G5, G7) |
| G10 | Production-bug review: any failure above attributable to product code | none | PASS | none found; every failure traced to load, cold compile or missing dev data (rows above) |

### Real-run setup (this gate)

Existing `e2e.bil.admin`, `e2e.bil.accountant`, `e2e.bil.reception` staff (roles Administrator, Accountant, Reception; all on MAIN) were found in `catecommerce_dev` by a read-only query and reused, with the password from the git-ignored `web/.env.e2e.local`; sign-in returned 200. The requested `e2e.vet`/`e2e.accountant` names were not created: the billing spec needs no clinical role and the three above cover admin, accountant and reception. Tax class `bdbb81d4-...` (15 %) and three `Real Customer n` accounts from the earlier seed were reused through a throwaway `seed.json` in the scratch folder. The only write this gate made to the dev DB was the MAIN billing profile above, through the API. No migrations were run. The API and the web server were stopped after the run.
