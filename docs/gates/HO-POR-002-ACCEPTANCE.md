GATE: ACCEPTANCE
HANDOVER: HO-POR-002
COMMITS: 4c2cd1a2 00a0e164 e2c80176 f39f0fa8 c38e6bf4 0f3497b9 f38ebba3 (build); ff31a745 (gate fix round); 3c1f0745 (owner answers); gate records 5047c9a6 8558dc3c e686e83d ef31836c 9ae18133 (branch ho/POR-002, b12fd39d..3c1f0745)
DATE: 2026-10-13
STATUS: PASS (re-check of 3c1f0745, at the end of this file; the first run at ef31836c was BLOCKED)

## Why BLOCKED (first run, ef31836c -- superseded by the re-check at the end)

One §18 row has no evidence and no recorded decision defers it: **A13 (NFR-PRF-1, dashboard LCP within 2.5 s on 4G)**. E-X3 is NOT RUN. My rules forbid a PASS while any criterion lacks evidence. I am returning BLOCKED, not FAIL, because the builder cannot close this gap inside the handover's own scope:
- E-X3 asks for Lighthouse. Lighthouse is not installed. §14 forbids `web/package.json`, and §20.6 says "a new dependency seems necessary -- raise it". The builder raised it: the TC sheet says "NOT-RUN ... owed".
- The project's LCP tooling belongs to HO-XCT-002 (ADR-FE-147: Playwright + CDP lab budget, "no Lighthouse dependency"; E-B1 there). That tooling is not built yet.
- E-X3 also needs the real API with a populated customer. Today a fresh database gives an empty dashboard.

So A13 depends on an owner decision that nobody has made. Nothing else is blocking. Every other criterion is met, or retired/deferred by a recorded decision (see CRITERIA).

**To unblock.** The owner adds one line, as a §1 Amended row in HO-POR-002 or as a recorded owner decision. It should say either:
- (a) "A13's LCP half is carried to HO-XCT-002's lab budget (ADR-FE-147, E-B1), with `/portal` added to its route list", or
- (b) "measure E-X3 now", with the tooling named.

Option (a) is what HO-SHP-002 ACCEPTANCE did (its condition C-3, carried, PASS). That record did not cite a deferral decision, and I do not repeat that here. Under (a), the re-check is a formality: no code change and no other gate.

## Prior gates (from docs/gates/ and docs/test-cases/, not from commit messages)

- ARCHITECTURE: PASS (e686e83d). SECURITY: PASS (5047c9a6). CODE_REVIEW: PASS (8558dc3c). TEST: PASS (ef31836c).
- Sheet `docs/test-cases/TC-HO-POR-002.md`: every row has a status.
  - PASS: U-*, E-A1 (page half), E-A2..A5, E-B1..B4, E-X1, E-X2, R1-R9.
  - BLOCKED-SCOPE: E-A1 landing half.
  - NOT RUN: E-X3, the real-API re-run by TEST, and the real-API re-order success and data tiles.
  - N/A: U-A11, E-C*.
  - BLOCKED, NOT RUN and N/A rows are not counted as passes here.
- Fix round ff31a745 came after ARCH, SEC and CODE_REVIEW. It is about 50 production lines in `useReorder.ts`, `errors.ts` and `OrdersTile.tsx`.
  - TEST covered it. Rows R1-R6 and R9 each mutate production code and show the named test failing.
  - I read the delta myself. `shouldRenewReorderKey` keeps the key on a network error, on 5xx and on `409 RequestInProgress`, and renews it on any other 4xx. `IdempotencyKeyRequired` is matched before the generic 400 branch. The order date goes through `localDateOf(..., 'Asia/Dhaka')`.
  - The HO-POR-001 ACCEPTANCE precedent (fix round 38a1c2f0) applies: the tiny-fix rule, so no re-review by ARCH, SEC or CODE_REVIEW is needed.
- Re-run by this gate: `npx vitest run tests/components/portal --maxWorkers=2`. Result: **3 files, 47 tests passed** (dashboard 23, reorder 19, links-and-guards 5).
- Playwright was not re-run (memory). The e2e figures below are the TEST gate's: 68/68 MSW with `--retries=0`, chromium and mobile-360. The real-API spec's 2/2 is the developer's run only. **No gate observed it.**

## CRITERIA (§18, narrowed by §1 Amended 2026-09-22, 2026-10-10 and 2026-10-13)

MSW-only evidence is judged as in the HO-ADM/FIL/NTF/APT/SHP-002 ACCEPTANCE records: a criterion is MET on MSW e2e plus unit evidence, and the real-API half is carried as a condition. A carried half is not counted as a pass.

- **A1 MET** for the six delivered sections. The stays part is **DEFERRED** by recorded decision: Amended 2026-10-10 (1) skips BRD. That decision was delegated by the owner and still awaits the owner's review (POR 18). The real-API data half is carried (C-2). Evidence:
  - U-A5 (Book now href for DueSoon and Overdue, none for Current);
  - U-A7 (London appointment shown with the zone named);
  - U-A4;
  - E-A2 (`portal-dashboard.spec.ts`), which asserts six h2 tile headings, zero stays/boarding/subscription headings, BDT 3,250 and USD 40.00, a tracking number, a follow-up date and `(Europe/London)`.
- **A2 MET.** Evidence:
  - U-A1 (the no-cats invitation and true-empty text on the other tiles);
  - U-A2 (cats `Unavailable`: Try again, no `no-cats` testid, no Add a cat link);
  - E-A1 page half (Add a cat reaches `/portal/cats/new`);
  - E-A3b;
  - the real-API spec asserts `cats.state === 'Ready'` before checking the invitation (developer run only).

  The "lands on /portal after sign-in" half of E-A1 is ruled below. It is not part of A2's criterion text.
- **A3 MET** for visits and prescriptions. Subscriptions are **DEFERRED** (Amended 2026-10-10 (1), SUB skipped). Evidence:
  - U-A10 (each tile header link equals its `portal-links` constant);
  - the source guard "no portal component writes a path of its own";
  - U-A13, U-A16;
  - U-A11b replaces U-A11: there is no subscriptions tile and no placeholder;
  - E-A4 follows the built links. The orders and prescriptions links are checked by href only, because those pages are not built (§20.5, POR 28).
- **A4 MET.** Evidence:
  - U-A2;
  - U-A3 (balances `Unavailable`: no amount, no "Nothing to pay", retry refetches);
  - U-A6;
  - U-A12 (a missing key reads "Not available yet", with no retry);
  - "a whole-request failure is an error with a retry";
  - E-A3 (degraded, then Try again fills the tile after the mock recovers), E-A3b and E-A3c.
- **A5 MET (MSW); the basket half is carried (C-2).** Evidence:
  - U-B1: busy text and `aria-disabled` while in flight, re-enabled afterwards. U-B1 does not count requests. The "one request" half rests on source I read: the `inFlight` ref guard in `ReorderButton.tsx:30-40`. The sheet's PASS overstates the test (LOW, row 40).
  - U-B5: `['cart']` is invalidated and the dashboard key is not, asserted both ways.
  - E-B1: the dialog reads "3 items ... were added", View basket points at `/cart`, and focus returns to the opener.
  - "Reach the basket" can only be observed once HO-ORD-002's `/cart` and checkout exist.
- **A6 MET (MSW; real-API carried).** Evidence:
  - U-B2: the count is in a live region, and the not-added list has 5 items, each with its reason as text;
  - U-B3: "Nothing could be added", and View basket is still offered;
  - U-B4 ×3: three plain messages and no code in the DOM;
  - U-B8: an unknown reason code gets the generic text;
  - E-B2: out of stock, needs a prescription, and `PrescriptionNotAuthorised` absent from the DOM.
- **A7 MET for accidental duplication. The deliberate-repeat half is RETIRED by recorded decision**, which still awaits the owner's review (owner question 3).
  - **Evidence for accidental duplication:**
    - U-B1b: a retry after a network error reuses the key, and a deliberate second re-order gets a new key;
    - the five renewal tests: 400, 404 and `409 StaleVersion` renew; 5xx keeps; `409 RequestInProgress` keeps;
    - TEST mutation rows R1-R5 prove each of those tests can fail.
  - **The deliberate-repeat half.** Amended 2026-10-13 (2) retires E-B3. That amendment follows Amended 2026-10-10 (2): use Orders' endpoint, which re-adds the lines. The amendment was written by the builder and derives from an owner-delegated decision. Its replacement e2e (E-B3, "second request") asserts only the dialog. Its key-uniqueness line is vacuous under MSW, as the spec admits (LOW, row 41). A real top-up semantics is ORD's job (POR 21, `AlreadyInCart`).
- **A8, A9, A10 RETIRED.** README C-6 moves the diary to HO-BRD-002, and Amended 2026-10-10 (1) skips BRD. No diary code is on the branch (diff sweep: the only "stay" or "diary" hits are comments saying they are absent).
- **A11 MET.** U-A4 checks every button and link on the whole dashboard: none has a name matching `/\bpay/i`. The balances links all start with `/portal/billing`. E-A2 shows zero pay buttons or links in the browser. The real-API spec has the same check (developer run).
- **A12 MET for the dashboard and the re-order dialog. The viewer parts are RETIRED with the diary** (U-C5, U-C6, E-C2). Evidence:
  - E-X1 (`portal-a11y-responsive.spec.ts`): zero serious or critical axe violations on six fixtures and with the mixed result dialog open, on chromium and mobile-360;
  - E-X2: no horizontal scroll at 360/768/1024/1440 for full, new and manyCats, and the dialog box fits the viewport;
  - U-B6: focus returns to the trigger.

  Source: 23/23 on each project (TEST).
- **A13 NOT EVIDENCED. This is the BLOCKED reason above.** U-A9 shows a fixed-height skeleton (by testid) that is replaced by the tiles, and `DashboardSkeleton.tsx` uses fixed `h-*` classes, so the layout-shift half is supported by source. No LCP figure exists. E-X3 is NOT RUN, and no recorded decision defers it.

## Rulings asked for

1. **E-A1 "lands on /portal after sign-in" (BLOCKED-SCOPE, POR 26): deferred pending the owner. It is not met, and it is not an implementation failure.**
   - A2's criterion text does not contain the landing, so A2 is met without it.
   - The landing appears in §2 (objective) and in E-A1's expected result. It also bears on US-POR-01 AC1 ("Given I log in, then ... on one screen"): today the customer lands on `/portal/cats` and needs one click on Home.
   - The handover contradicts itself on this point:
     - §2 asks for the landing;
     - §13 allows only "the default landing constant" in `lib/routing/**`;
     - §13's footnote expects HO-ACC-002 Amended rows;
     - §14 forbids every other HO-ACC-002 file.
   - The builder made the constant (`DEFAULT_CUSTOMER_LANDING = '/portal'`, U-L1). The builder did not edit `app/(auth)/sign-in/page.tsx` or `app/page.tsx`: `git diff` over them is empty. The gap was reported, not worked around.
   - This is a document defect for the owner to resolve. It does not make the verdict FAIL.
2. **Real-API rows that need ORD-002's checkout (re-order success, basket contents, data in the orders, visits, prescriptions and balances tiles): carried condition C-2.**
   - They are not counted as passes, and they are not failures. This follows the same precedent as the FE ACCEPTANCE records named above.
   - The parts that need no order were run by the developer (2/2): the wire key set with no stays or subscriptions key, the cat tile, `400` without a key, and `404` with a key.
   - No gate observed that run. Re-run `portal-real-api.spec.ts` when the integrator next has the API up.
3. **E-X3 Lighthouse LCP: not evidenced, and BLOCKED pending an owner decision** (see the top of this record).
   - There is a further question. NFR-PRF-1 is worded for "storefront pages", and HO-XCT-002's lab budget covers "the storefront routes". `/portal` may therefore fall outside both, unless the owner adds it.
4. **Boarding and subscriptions are absent by owner decision: accepted as DEFERRED, not met.**
   - Amended 2026-10-10 (1) is an owner-delegated decision of 2026-10-10 that still awaits the owner's review (POR 18 and 13).
   - The decision is implemented exactly. U-A11b and E-A2 assert no tile, no heading and no placeholder. ADR-FE-126 and the merged DTO carry no `stays` or `subscriptions` key.
   - If the owner reverses it, A1's stays part, A3's subscriptions part and A8-A10 reopen.

## FINDINGS

- [BLOCKED, document/dependency] docs/handover/HO-POR-002.md §18 A13 / §16 E-X3.
  - **The gap:** the criterion needs Lighthouse against the real API. The tooling is absent and forbidden to add here (§14, §20.6), and HO-XCT-002 owns the project's LCP gate.
  - **What it needs:** an owner decision to carry the criterion or to measure it now (see Why BLOCKED).
- No BLOCKER or HIGH against the implementation.
- [LOW] web/tests/components/portal/reorder.test.tsx:26-49. U-B1 does not count requests on a double click. The sheet marks "one request" PASS, but only the source guard shows it. Add a request counter to U-B1. Proposed row 40.
- [LOW] web/tests/e2e/portal-dashboard.spec.ts:153-170, web/tests/e2e/portal-real-api.spec.ts:117-151.
  - E-B3's key-uniqueness assertion is vacuous under MSW: Playwright does not see requests that the service worker answers.
  - The real-API `404` case uses an unknown order id. It does not use another customer's order. The SECURITY record says "an order belonging to someone else", which overstates it.
  - Proposed row 41.
- [LOW] web/tests/e2e/portal-helpers.ts (added in ff31a745).
  - It lies outside §13's literal `web/tests/e2e/portal-*.spec.ts` glob.
  - No §1 Amended row records it. It is described only in the TC sheet and in POR 34/38. No gate ruled on its scope, because it came after ARCH, SEC and CODE_REVIEW.
  - It is a test-only helper, the same as the APT/EMR/SHP-002 `*-helpers.ts` precedent and the IMP-002 ACCEPTANCE LOW.
  - Accepted by precedent. Proposed row 42.
- Carried, not re-raised: ARCH LOW 31, 32 and 36; TEST LOW 38 and 39; ORD/POR backend rows 21-25; owner rows 26-28.

## CARRIED CONDITIONS (not counted as passes; each needs its owner to close it)

- **C-1 Owner.** The A13 decision (see Why BLOCKED).
- **C-2 Integrator.** After HO-ORD-002 merges:
  - real-API re-order success and basket contents (A5, A6);
  - the E-A2 data tiles on a populated customer;
  - a re-run of `portal-real-api.spec.ts` that a gate observes;
  - the order-detail `ReorderButton` (§2; the page does not exist);
  - reconciling the duplicate re-order module and mock (POR 31);
  - the `/portal/orders`, `/portal/prescriptions` and `/cart` links (POR 28).
- **C-3 Owner.** The landing (POR 26), Home's position in the nav (POR 27), and the review of the 2026-10-10 delegated decisions that this acceptance rests on (POR 18).

## Owner questions

1. A13: carry the LCP half to HO-XCT-002, with `/portal` added to its route list, or measure it now? (This unblocks the gate.)
2. POR 26: allow a one-line edit each in `app/(auth)/sign-in/page.tsx` and `app/page.tsx`, plus a HO-ACC-002 §1 Amended row, so that customers land on `/portal`? Or keep `/portal/cats`?
3. A7: is "a deliberate second re-order adds the lines again" acceptable? Amended 2026-10-13 (2) retired E-B3 on that basis. Or should ORD add an `AlreadyInCart`/top-up rule (POR 21)?
4. Confirm the 2026-10-10 delegated decisions: BRD and SUB skipped (stays, subscriptions and diary absent), and re-order through Orders (POR 18).

## Proposed backlog rows (docs/backlog/POR.md; the highest row is 39)

| # | Sev | Item |
|---|---|---|
| 40 | LOW | ACCEPTANCE (HO-POR-002): `reorder.test.tsx` U-B1 does not assert "one request" on a double click (no counter in its handler). Only the `inFlight` ref in `ReorderButton.tsx` shows it, yet the TC sheet marks U-B1 PASS. Add `expect(calls).toBe(1)`. |
| 41 | LOW | ACCEPTANCE (HO-POR-002): (a) E-B3's replacement key-uniqueness check in `portal-dashboard.spec.ts` is vacuous under MSW. Count requests in the MSW handler, or drop the claim. (b) The `portal-real-api.spec.ts` `404` case uses an unknown order id, not another customer's order. Once ORD-002 checkout exists, add a second customer's real order. |
| 42 | LOW | ACCEPTANCE (HO-POR-002): `web/tests/e2e/portal-helpers.ts` lies outside §13's `portal-*.spec.ts` glob and has no §1 Amended row (APT/EMR/SHP-002 precedent). Record it in the approval Amended row along with `nav/areas/portal.ts`. |
| 43 | LOW | ACCEPTANCE (HO-POR-002), owner: A13/E-X3 (dashboard LCP ≤ 2.5 s on 4G) is not measured. There is no Lighthouse, and HO-XCT-002's lab budget covers only "storefront routes". Decide whether `/portal` joins HO-XCT-002 E-B1. Until then NFR-PRF-1 is unproven for the portal. |

## SCOPE

- Out-of-scope work found: none. §4 is missing, so I checked against the §1 Amended exclusions:
  - no stays tile, subscriptions tile, diary, media viewer or order-detail `ReorderButton`;
  - no payment control;
  - no edit to the sign-in page, `app/page.tsx` or `nav/index.ts`;
  - no `src/**`, `tests/**`, `.ai/**` or `.claude/**`, and no package, tsconfig, next or eslint config (`git diff --stat` empty).

## SCOPE CHECK

- Files in allowed scope: 39/39.
  - 35 match §13 literally.
  - 4 are accepted on precedent or authority:
    - `web/nav/areas/portal.ts` (ARCH ruling, POR 27);
    - `docs/handover/HO-POR-002.md` (append-only §1 Amended rows);
    - `docs/backlog/POR.md` (owner rule 2026-09-27);
    - `web/tests/e2e/portal-helpers.ts` (test helper, LOW row 42).
- Files outside allowed scope: none.

## UNDECLARED DEVIATIONS

- U-B1's "one request" is marked PASS but is not asserted (row 40).
- E-B3's replacement key-uniqueness line asserts nothing under MSW (row 41).
- `portal-helpers.ts` is not in §13 and not in an Amended row (row 42).
- None of these is BLOCKER or HIGH.

---

# RE-CHECK of 9ae18133..3c1f0745 (2026-10-13) -- STATUS: PASS

## What changed (12 files, +79/-25)

- **Code:**
  - `web/app/(auth)/sign-in/page.tsx`: one import, and the `sanitizeNextPath` fallback changes from `'/portal/cats'` to `DEFAULT_CUSTOMER_LANDING`.
  - `web/app/page.tsx`: one import, and the signed-in customer redirect changes to `DEFAULT_CUSTOMER_LANDING`.
  - `web/lib/routing/portal-links.ts`: comment only.
- **Tests:**
  - `reorder.test.tsx`: U-B1 now counts requests; a new "E-B3 (component)" test checks the two keys.
  - `portal-dashboard.spec.ts`: two sign-in landing cases; the vacuous key check is removed from E-B3.
  - `home.spec.ts` and `accounts-auth.spec.ts`: assertions moved to the new landing.
- **Docs:**
  - §1 Amended rows in HO-POR-002, HO-ACC-002 and HO-XCT-002 (XCT's E-B1 row now names `/portal`);
  - the TC sheet;
  - POR backlog rows 18, 21, 26 and 40-43.

## Owner decisions this re-check relies on

The answers are dated 2026-10-13 and quoted in the HO-POR-002 §1 Amended row "owner answers to the ACCEPTANCE gate". This gate did not see the owner's own message. The quoted answers are the only record, and they are recorded where they belong.
1. **A13: "Defer to XCT-002".** HO-XCT-002 has an Amended row, and its E-B1 row now covers the storefront routes **and `/portal`**, measured against the real API with a populated customer.
2. **Landing: "Land on /portal".** Recorded in the HO-ACC-002 Amended row.
3. **A7: "Fine as is".**
4. **"Confirm all"** for the 2026-10-10 delegated decisions (POR 18 resolved).

## Evidence observed by this gate

- `npx vitest run tests/components/portal tests/components/auth tests/components/cross-cutting.test.tsx --maxWorkers=2`: **10 files, 90 tests passed.**
  - The portal folder has 48 tests, including the new key test and U-B1's `expect(requests).toBe(1)`.
  - The auth folder includes `sign-in-form` and `session-guards`.
- **One targeted Playwright run:** `PORT=3119 ... portal-dashboard.spec.ts -g "E-A4" --project=chromium --workers=1 --retries=0 --repeat-each=3`. **3 passed** (27.2 s). The dev server was stopped afterwards.
- Not re-run by me: the two sign-in landing e2e cases, `home.spec.ts` and `accounts-auth.spec.ts`. They are green according to the coordinator, but I did not watch them. The landing is evidenced below by source and by U-L1.

## Criteria (changes from the first run only; all other rows stand as ruled above)

- **A13 DEFERRED by owner decision.** The LCP half is carried to HO-XCT-002 E-B1, which now includes `/portal`. The U-A9 half is supported by source (fixed `h-*` skeleton). A13 now counts as neither met nor failed, which matches the EMR-002 and SRG-001 deferral precedents. The BLOCKED reason is closed.
- **A7 MET.** Accidental duplication evidence stands: U-B1 now asserts one request on a double click, plus U-B1b, the renewal tests and R1-R5. The deliberate-repeat half is now **owner-accepted** ("Fine as is"), not just retired by the builder. The new component test "two deliberate re-orders are two requests with different Idempotency-Keys" proves the key behaviour. The e2e version no longer claims it.
- **A1, A3, A8-A10:** the stays, subscriptions and diary deferrals now rest on **owner-confirmed** decisions ("Confirm all"), no longer only delegated ones.
- **A5 and A6:** unchanged. MET on MSW, with the basket and real-API halves carried (C-2).
- **§2 landing / E-A1 landing half: MET.** Evidence:
  - Source I read: `sign-in/page.tsx:49-53` passes `DEFAULT_CUSTOMER_LANDING` as the fallback. `sanitizeNextPath` (`lib/validation/nextPath.ts`, unchanged; empty diff) returns the fallback when `next` is null, and returns an allowed `next` unchanged.
  - U-L1 asserts that the constant is `/portal`.
  - `app/page.tsx:56` redirects a signed-in customer to the same constant.
  - The e2e cases "signing in with no ?next= lands on /portal" and "an explicit ?next= still wins" exist and are reported green. I did not run them.

## Ruling: is the HO-ACC-002 landing edit authorised, recorded, and free of further gates?

**Yes.**
- **Authorised.** The owner's answer is quoted in HO-POR-002 Amended (2). HO-POR-002 §13's own footnote anticipated exactly this: "The landing change ... touch[es] HO-ACC-002 ... files; each of those handovers gets a §1 Amended row recording it".
- **Recorded.** The HO-ACC-002 §1 Amended row names both files and both lines, and says the allow-list and `sanitizeNextPath` are unchanged.
- **No further gate needed under the small-fix rule.**
  - The change is two lines plus two imports, and it changes only a fallback constant.
  - SECURITY's §19 focus was "`?next=` guard untouched". It is untouched: `git diff 9ae18133..3c1f0745 -- web/lib/validation` is empty, and the prefix allow-list is unchanged.
  - The new fallback is a compile-time internal constant, not user input, so no new open-redirect surface exists.
- **One gap (LOW, proposed row 44).** The HO-ACC-002 Amended row names `home.spec.ts` but not the `accounts-auth.spec.ts` edit. Both regexes are loosened from `/\/portal\//` to `/\/portal(\/|$)/`, which still asserts the portal and is correct. That file is ACC-002's, outside HO-POR-002 §13, and should be named in the same row.

## Ruling: the undiagnosed E-A4 flake

**LOW. It does not block the gate (owner rule 2026-09-27). Proposed row 45.**
- It failed once for the coordinator and passed on re-run with `--retries=0`. Here it passed 3 of 3 on a warm server, so I could not reproduce it.
- The failure output was not given to me, so this is not diagnosed. The likeliest cause, unproven, is the dev-only compile race: E-A4 is the only portal case that client-navigates into four other areas' routes (`/book`, `/portal/appointments`, `/portal/billing`, `/portal/cats`) and then calls `goBack`. On a cold `next dev`, a first-time route compile can outlast `toHaveURL`'s default 5 s.
- **Why no criterion is at risk.** A3's link evidence does not rest on E-A4 alone. U-A10 and the "no portal component writes a path of its own" source guard assert every href deterministically. A wrong link would fail every run, not one in many.
- **What to do next time.** Keep the trace (`--trace retain-on-failure`) on the next failure. If the cause is the cold compile, give the cross-area `toHaveURL` calls an explicit timeout. Do not add retries.

## FINDINGS (re-check)

- No BLOCKER or HIGH.
- [LOW] docs/handover/HO-ACC-002.md (2026-10-13 Amended row): it omits `web/tests/e2e/accounts-auth.spec.ts`, which was edited for the new landing. Proposed row 44.
- [LOW] web/tests/e2e/portal-dashboard.spec.ts:89 (E-A4): an intermittent failure, not diagnosed and not reproduced here (0 of 3). Proposed row 45.
- Still open and carried: POR 41(b), a real-API `404` with another customer's real order once the ORD-002 checkout exists.

## Proposed backlog rows (docs/backlog/POR.md; the highest row is now 43)

| # | Sev | Item |
|---|---|---|
| 44 | LOW | ACCEPTANCE re-check (HO-POR-002): the HO-ACC-002 §1 Amended 2026-10-13 row lists `home.spec.ts` but not `tests/e2e/accounts-auth.spec.ts`, whose two post-sign-in `toHaveURL` regexes were changed for the `/portal` landing. Add it to the row. |
| 45 | LOW | ACCEPTANCE re-check (HO-POR-002): `portal-dashboard.spec.ts` E-A4 failed once and passed on re-run, `--retries=0`, cause undiagnosed. ACCEPTANCE ran it 3 of 3 green. Suspected cause: a first-time `next dev` compile of the cross-area routes outlasting `toHaveURL`'s 5 s. On the next failure keep the trace; if the cause is compile time, give those `toHaveURL` calls an explicit timeout. No retries. |

## CARRIED CONDITIONS (updated)

- C-1 is **closed**: A13 is deferred to HO-XCT-002 E-B1.
- **C-2 Integrator** stands. After HO-ORD-002 merges:
  - a real-API re-order success and the basket contents;
  - the E-A2 data tiles;
  - a gate-observed re-run of `portal-real-api.spec.ts`;
  - the order-detail `ReorderButton`;
  - POR 28 and POR 31;
  - POR 41(b).
- **C-3** is reduced to POR 27 (Home's position in the nav). The landing and the delegated decisions are closed.

## SCOPE CHECK (re-check range)

- Files in allowed scope: 12/12.
  - In §13: `portal-links.ts`, `tests/components/portal/**`, `portal-dashboard.spec.ts`, and the TC sheet.
  - On owner authority, recorded in Amended rows (HO-POR-002 (1)/(2), HO-ACC-002, HO-XCT-002): `app/(auth)/sign-in/page.tsx`, `app/page.tsx`, `home.spec.ts`, `accounts-auth.spec.ts` (row 44), and the HO-ACC-002 and HO-XCT-002 handovers.
  - On the 2026-09-27 owner rule and the append-only Amended precedent: the HO-POR-002 handover and POR.md.
- Files outside allowed scope: none.
- No `src/**`, `.ai/**`, package or config change.

## Verdict

**PASS.**
- Every §18 criterion is MET, or DEFERRED/RETIRED by a recorded decision that the owner has confirmed.
- All four prior gates are recorded PASS.
- The fix rounds fall under the small-fix rule.
- No changed file lies outside scope once the recorded owner authority is counted.
- No out-of-scope feature was built.
- The §20 conditions the build hit (§20.5 unbuilt routes, §20.6 tooling, the landing scope gap) were reported, not worked around.

The previous commit 642467b8 appended an unrelated scratch file (an HO-XCT-001 combined-review text) to this record by mistake. This commit removes it. That text is not part of the HO-POR-002 ACCEPTANCE record.
