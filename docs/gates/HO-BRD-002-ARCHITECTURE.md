GATE: ARCHITECTURE
HANDOVER: HO-BRD-002
COMMITS: ef4bba16 0e24d681 baf7f254 111c8cc2 bb1d3862 baf68c86 df3720a6 70b7936b 669744e6 c65bf03a 05233fd7 5d579280 8b4c8073 75cb8e85 (range 5a187b6b..75cb8e85, branch ho/BRD-002, 122 files)
DATE: 2026-10-15
STATUS: FAIL

## Basis

- I reviewed the range with `git diff 5a187b6b..75cb8e85` and read these in full:
  - every modified shared file (`sign-in/page.tsx`, `nav/areas/boarding.ts`, `mocks/handlers/index.ts`, `errors.ts`, `types.ts`, `web/docs/decisions.md`, the handover);
  - the two tab-row layouts and the route shells;
  - the adapters in `lib/api/{my-boarding,staff-boarding,boarding-care}.ts`;
  - the components that render adapter output where a ruling depends on it.
- I read the tests for scope and fixture shape only.
- Sources:
  - HO-BRD-002 §1 (all four Amended rows), §2–§14 and §19–§20;
  - `web/docs/decisions.md` ADR-FE-099…108;
  - web/README "Adding a functional area" and `nav/types.ts`;
  - `components/admin/adminNavItems.ts`.
- The backend at `ho/BRD-001` 95a97bab (the frontend's alignment point) and fc6ef889 (the gated head):
  - `BoardingStayViewModels.cs`, `BoardingIncidentViewModels.cs`;
  - `BoardingStayQueryHandlers.cs`, `BoardingDepartureQueryHandlers.cs`;
  - `StaffBoardingDepartureController.cs`;
  - `git diff 95a97bab..fc6ef889` over `src/Api`, `src/Read`, `src/Domain/**/Models`, `src/Shared`.
- I applied the earlier frontend §13/§14 rulings: HO-APT-002, HO-IMP-002, HO-EMR-002, HO-SRG-002, HO-RPT-002, HO-POR-002 and HO-ORD-002 ARCHITECTURE.
- `.ai/architecture.md`, `cqrs.md`, `bounded-contexts.md`, `decisions/adr.md`, `namespace-conventions.md` and `di-registration-guide.md` are the backend rules. The diff has no `src/**`, `tests/**`, `.ai/**` or `.claude/**` change (0 files), so the aggregate, dispatch, EF Core and host checklist has nothing to apply to.
- This gate built nothing and ran nothing. Runtime claims are marked for TEST.
- Owner rule (2026-09-27): only BLOCKER and HIGH fail. MEDIUM and LOW are proposed below as `docs/backlog/BRD.md` rows. That file exists only on `ho/BRD-001`, where the highest row is 44, so the proposals start at 45. SECURITY (32ad0001) and CODE_REVIEW (86bec810) are already committed, so the integrator may renumber.

## Rulings asked for

### Scope
- **Nav in `nav/areas/boarding.ts`: accepted.**
  - The shells read the registry; `(portal)/layout.tsx` and `(staff)/layout.tsx` hold no nav entries. This is the HO-APT/IMP/EMR/SRG/RPT precedent.
  - The edit is additive: two staff entries, one portal entry, and a third staff entry "Boarding setup" in place of §13's admin-layout Boarding group.
  - That admin group is really `components/admin/adminNavItems.ts`, which belongs to HO-ADM-002. `admin/layout.tsx` only renders `AdminNav`.
  - Putting the group in the area registry keeps it off another slice's file. The cost: the AdminNav strip does not list Boarding, and the sidebar entry shows to every staff role, because `NavItem` has no roles. LOW L1.
- **Tab-row layouts: accepted, literally in §13.**
  - Both files sit under the §13 globs `app/(staff)/staff/boarding/**` and `.../admin/boarding/**`.
  - They are presentation only: links from `lib/routing/boarding-links.ts`, no data, no role check ("pages answer their own 403", ADR-FE-107).
  - The admin one nests inside HO-ADM-002's admin layout and does not replace it.
- **`tests/e2e/boarding-helpers.ts`: accepted.** It is a non-spec helper beside the `boarding-*.spec.ts` glob (the APT/EMR/SRG/RPT precedent) and is disclosed in Amended (build). It imports only test files and `lib/format`. LOW L2.
- **Record payment links to billing: compliant.**
  - §14 forbids "any payment, card, wallet or pay now component". ADR-FE-106 names "a link to Billing with the stay reference" as the fallback.
  - `DepositPanel.tsx:77-83` is a plain `<Link>` to `/staff/billing/payments/new?customerId=`. That route exists on master (HO-BIL-002 §11.4, `?customerId&invoiceId`).
  - Boarding takes no amount, card or reference. **Confirm stay** is driven only by `allowedTransitions.includes('Confirm')`.
- **Authorised sign-in edit: accepted.** `app/(auth)/sign-in/page.tsx` is +2 comment lines and one token, `'/boarding'`, in the `sanitizeNextPath` allow-list. That is exactly the owner's 2026-10-14 "Yes, allow it". The `isSafeInternalPath` guard is unchanged; SECURITY owns its behaviour.
- **`lib/routing/boarding-links.ts`: accepted.** It is inside the `web/lib/routing/**` glob but beyond §13's "allow-list only" note. `portal-links.ts` (HO-POR-002) set the per-area link-file precedent. LOW L3.
- **`docs/handover/HO-BRD-002.md` (+2 Amended rows): accepted.** §14 excludes `docs/**`, but recording deviations in §1 Amended is what the gates require, as in the HO-SRG-002 precedent.

### Contract alignment: what the BRD-001 fix round changed after 95a97bab

`git diff 95a97bab..fc6ef889` keeps every view model the frontend reads, with the same member names. It changes these shapes and values:

| # | Change on fc6ef889 | Frontend effect | Finding |
|---|---|---|---|
| a | `CareProfileRequest.vetAuthorityChangeReason` (new). Required when **staff** change vet authorisation or the spending limit on a non-Enquiry stay (`BoardingStayAggregate` → `VetAuthorityReasonRequired`). | `staff-boarding.ts:383-385` never sends it, and `errors.ts` has no mapping for the code. A staff edit of either value on a reserved stay is refused, and staff have no field to give the reason. | MEDIUM M1 |
| b | `StayAttentionReason.VaccinationGateBlocked` (new value 5). | `types.ts:245-249` and `ATTENTION_LABEL` (`labels.ts:163-168`) lack it. `StayWorkspace.tsx:139` would render an empty banner sentence. | MEDIUM M1 |
| c | `EarlyCheckoutPreviewViewModel.IsLate` (new, default false); overstay check-out extends the allocation (`[BoardingClaimEndpoint]`). | The preview renders only when `isEarly` (`EarlyCheckOutPreview.tsx:32`), so a late check-out shows no preview. The change is additive. | LOW (CODE_REVIEW L12 already has it) |
| d | `[EnableRateLimiting(Booking)]` on enquiry and daycare booking. | `client.ts:326` already maps `429` to `ApiRateLimitedError` with plain text. | none |
| e | Value changes, same shapes: `refundDue` only when owed > 0; `creditDue` netted; `gate.overridden` from `GateOverriddenBy`; projected reads. | The frontend renders these as sent. | none |

Rows (a) and (b) are drift the backend introduced after the frontend aligned. They are not a frontend violation, so they are not HIGH here. A staff path does break on merge, though, so the integrator must not let them sit silently in the backlog. TEST should re-run the staff care-profile edit against fc6ef889 or later.

One older mismatch is still present at 95a97bab:
- The stay gate is `{status, reasons, evaluatedOn, overridden, overrideReason, overriddenOn}`, with no `overriddenBy`.
- `my-boarding.ts:132-133` and `StayWorkspace.tsx:119-122` show the override only when `gateOverriddenBy` is set, so the **reason** that is on the wire never appears (§11.8 "gate state, with who overrode it and why").

MEDIUM M2.

### The "absent" rule: FAIL (HIGH H1)

Amended (final alignment) says fields not on the wire are "shown as absent (not shimmed)". Most are:
- the stay-view invoices come from the summary;
- the dashboard shows a stay link instead of a name or phone;
- the overdue chip has no minutes;
- the pre-cancel deposit sentence is omitted;
- `staff-boarding.ts:663` keeps a null dashboard `balanceDue` null.

Several customer-facing adapters do invent values, however (see H1). The `?? 0` occupancy defaults (`staff-boarding.ts:688-694`) and the `gateStatus ?? 'Passed'` default are inert, because those members are non-null on the wire. I did not count them.

### Deferred parts (XCT-002 and POR-002 are on master)

- **Offline board boarding instructions: still correctly deferred. The reason in Amended (build) is stale.**
  - Master now has `useConnectivity`/`OfflineBanner`, mounted once in `(staff)/layout.tsx`, so the boarding screens get the banner on merge with no BRD-002 code.
  - The offline board's boarding section (`components/offline/OfflineBoard.tsx`, `OfflineBoardingTasksList.tsx`) belongs to HO-XCT-002 (§14). It reads the snapshot's `boardingTasks` section.
  - No backend source is registered for that section on fc6ef889. `CrossCuttingModule.cs:64` says "boardingTasks has none until Boarding exists". So the remaining work is a HO-BRD-001 snapshot source, not frontend code. LOW L4 / row 52.
- **Portal stays tile: correctly deferred.**
  - HO-POR-002 is merged with no stays tile and no stays section, by owner skip on 2026-10-10 (`PortalDashboard.tsx:28`). The tile lives in `components/portal/**`, which is outside §13.
  - It needs a backend `stays` dashboard section (HO-POR-001) and a HO-POR-002 follow-up linking to `/portal/boarding/[stayId]/diary`.
  - Recorded as row 53. No BRD-002 finding.

### ADR-FE-099…108 collisions

I searched master and every local `ho/*` branch except `ho/BRD-002`, for `ADR-FE-(099|10[0-8])` under `web/`. There are **no hits**. Master's `docs/handover/README.md:96` reserves the block for HO-BRD-002. No collision.

The **content** of two entries contradicts the build (MEDIUM M3):
- **ADR-FE-104** still says "Nothing uploads", gives the reason "consuming `FileUploader` is outside section 13/14", and has status "See `MediaReferenceFields`", a component that is not in the diff.
- **ADR-FE-105** says the preview "cannot be shown before submit" and that the chip "formats the server's `minutesLate`".

The Amended rows record the owner decisions that supersede both, so this is a stale record, not a loosened rule.

### Uploader and the BoardingMedia / BoardingBelongings purposes: compliant

- `FileUploader` is consumed **unmodified**: no `components/files/**` or `lib/api/files.ts` file is in the diff.
- It is used in `CheckInWizard.tsx:281-298` (`purpose="BoardingBelongings"`, `audience="staff"`, appending `fileId` to the belonging's `fileIds[]`) and in `MediaRecordDialog.tsx:91-99` (`purpose="BoardingMedia"`, `audience="staff"`, the `fileId` posted to `…/media`).
- Both purposes are in master's `FilePurpose` (`lib/api/files.ts:24-25`), and the backend attach checks use the same purposes (`BoardingCareCommandHandlers.cs:55`, `BoardingIncidentCommandHandlers.cs:46`).
- Nothing in boarding code calls `FormData`, renders `type="file"` or uses a storage client.
- Incident photos are not offered, which matches the wire.
- The diary-photo use is now owner-confirmed ("Yes, diary photos too"). Amended (final alignment) (9) still says "the owner is asked to confirm it". LOW L5.

## What was verified (compliant)

- **Layering (HO-ACC-002/HO-CAT-002 §8.2).**
  - The API calls are in the four §8.2 modules.
  - Queries and mutations are in `useMyBoarding`, `useStaffBoarding` and `useBoardingCare`.
  - No `useQuery`/`useMutation`/`fetch(`/`apiFetch` appears in `components/boarding/**`, `components/signature/**` or the route shells. The one exception is `useQueryClient` in `BookingFlow.tsx` (MEDIUM M4).
  - There is no `localStorage`, `sessionStorage`, cookie, IndexedDB, `dangerouslySetInnerHTML` or `console.*`.
- **No dependency (§10).**
  - `package.json`, the lockfile, `tsconfig.json`, `next.config.ts` and `eslint.config.mjs` are untouched.
  - The third-party imports are `react`, `next/*`, `lucide-react`, `@tanstack/react-query`, `react-hook-form`, `zod` and `msw`.
  - The signature pad, occupancy grid and countdown are in-repo.
- **Polling (ADR-FE-101).** `refetchInterval: BOARDING_POLL_MS` is set on the dashboard, occupancy, daycare, task-day and incident queries, all keyed `['staff','boarding',…,branchId]`.
- **Actions from the server (ADR-FE-107).**
  - `mapTransitions` passes `allowedTransitions` through, renames `NoShow`, drops the system moves, and offers Extend/Relocate while CheckIn/CheckOut is allowed. That rule is disclosed in Amended (1), and the server refuses otherwise.
  - The derived `canOverrideGate` (`my-boarding.ts:205`) is disclosed. It is used only on the staff workspace, and the server decides.
- **Shared files are append-only.** `types.ts` and `errors.ts` have no deleted lines. `mocks/handlers/index.ts` adds one import and one spread.
- **No money arithmetic (ADR-FE-105).** No `+ − × ÷` is applied to amounts. `EarlyCheckOutPreview.tsx:33` uses `Number(credit.amount) > 0` as a visibility test only (LOW L6).

## FINDINGS

- **[HIGH] H1 — the "absent" rule.** `web/lib/api/my-boarding.ts:520`, `:198`, `:316` and `:497-501`. The rule (Amended, final alignment; §20.4/§20.5; ADR-FE-105 "every … balance … rendered from the server") is that a field the backend does not send is shown as absent and never invented. The adapters invent values in four places:
  1. **Summary balance mislabelled.** `normaliseStaySummary` builds `balanceDue` from `billing.unbilledTotal ?? billing.invoicedOutstanding ?? w.balanceDue`.
     - `StayReportViewModel` has no `balanceDue`.
     - `SummaryBillingViewModel.UnbilledTotal` is a non-null money object, so the `??` never reaches `invoicedOutstanding`.
     - After the final invoice, `StayCharges.tsx:70-71` therefore tells the owner **"Balance due ৳0.00" while the invoice is still outstanding**.
  2. **Null balance shown as zero.** `normaliseStay` (`:198`) and `normaliseSummary` (`:316`) turn the wire's nullable `balanceDue` into `{amount:'0.00'}`. The backend sends null when the stay has no quote (every Enquiry, Amended (13)). So `StayWorkspace.tsx:112`, `StayOverview` → `DepositSummary`, `MyStaysList.tsx:58` and the staff `StayList.tsx:91` show "Balance ৳0.00" instead of nothing. `staff-boarding.ts:663` keeps the same member null, so the two adapters disagree.
  3. **Signature method invented.** `checkOutSignature.method` is hard-coded `'Typed'` when the wire carries only `checkOutSignerName`/`checkOutSignedOn`. `StaySummaryView.tsx:44` then prints "(typed name)" on the owner's summary for a signature that may have been drawn.
  4. Inert today (it is not rendered), but the same pattern: `referredBy: 'Care team'` at `:172`.

  **Why HIGH.** Under HO-ORD-002's HIGH ("turns every missing endpoint into a plausible-looking wrong answer"), these are wrong money and wrong record statements to a customer. The builder's own Amended row says they do not exist, and the pattern will propagate if copied.

  **Fix.** Keep `balanceDue` nullable end to end and render nothing, or "—", when it is null. On the summary, show the server's `unbilledTotal` and `invoicedOutstanding` under their own labels, or ask HO-BRD-001 for a `balanceDue`. Make `method` nullable and omit "(typed name)/(drawn)" when it is absent. Drop the `referredBy` default. Add a wire-fixture test from `built-wire.json` with an invoiced summary and an Enquiry stay.
- **[MEDIUM] M1** (row 45) — `web/lib/api/staff-boarding.ts:383-385`, `web/lib/api/errors.ts`, `web/lib/api/types.ts:245-249`, `web/components/boarding/shared/labels.ts:163-168`. Backend drift after 95a97bab: `vetAuthorityChangeReason` and `VetAuthorityReasonRequired` are unhandled, and `StayAttentionReason.VaccinationGateBlocked` is unlabelled. Re-align to fc6ef889 or later before merge, or record the broken staff path. For TEST.
- **[MEDIUM] M2** (row 46) — `web/lib/api/my-boarding.ts:132-133`, `web/components/boarding/stay/StayWorkspace.tsx:119-122`. The gate override shows only when there is an `overriddenBy`, which the wire never sends. Key the display on `gate.overridden` and show `overrideReason` and `overriddenOn`.
- **[MEDIUM] M3** (row 47) — `web/docs/decisions.md`, ADR-FE-104 and ADR-FE-105. The texts and statuses contradict the build. The superseding owner decisions are only in §1 Amended. Amend both entries; the retirement note at the end of `decisions.md` is the format to follow.
- **[MEDIUM] M4** (row 48) — `web/components/boarding/booking/BookingFlow.tsx:4,78,159,234`. A component holds query-key knowledge (`invalidateQueries(['me','boarding','stay'])`, `['boarding','availability']`). Move it into the mutation callbacks in `useMyBoarding`. This is the HO-ORD-002 row 64 precedent.
- **[MEDIUM] M5** (row 49) — `web/lib/api/my-boarding.ts:317,433,543`, `web/lib/api/staff-boarding.ts:306,653,751`. A literal `'Asia/Dhaka'` stands in for a branch zone the wire does not send (summary rows, approval, waitlist, occupancy, dashboard, daycare). `BoardingDashboard.tsx:141` then labels it as the branch's zone. §2(3) and HO-ADM-002 ADR-FE-014 require the branch's own zone: use `useBranchTimeZone()` or the active branch. This overlaps CODE_REVIEW L10; the integrator may merge the two rows.
- **[LOW] L1** (row 50) — `web/nav/areas/boarding.ts:24-29`. "Boarding setup" sits in the staff sidebar for every role rather than in `adminNavItems.ts` (HO-ADM-002), so the admin strip does not list Boarding. Accepted. If wanted, the HO-ADM-002 owner adds one `adminNavItems` entry.
- **[LOW] L2** — `web/tests/e2e/boarding-helpers.ts`. It is outside the `boarding-*.spec.ts` glob by the letter only, and is disclosed. No row needed.
- **[LOW] L3** — `web/lib/routing/boarding-links.ts`. It is beyond §13's "allow-list only" note for `lib/routing/**`, under the `portal-links.ts` precedent. Name it in §1 Amended. Row 51, combined with L5.
- **[LOW] L4** (row 52) — the Amended (build) reason for deferring the offline boarding instructions ("XCT-002 not on master") is stale. The real gap is a HO-BRD-001 `boardingTasks` snapshot source (`CrossCuttingModule.cs:64` on fc6ef889). Raise it on the BRD-001 backlog.
- **[LOW] L5** — Amended (final alignment) (9) still asks the owner to confirm diary photos. The owner has: "Yes, diary photos too". Update the row (row 51).
- **[LOW] L6** — `web/components/boarding/stay/EarlyCheckOutPreview.tsx:33`. `Number(credit.amount) > 0` decides whether the credit shows. The server's `creditOutcome` (None/Credit/Refund) is authoritative; use it. Row 51.
- **[LOW] L7** — `web/tests/e2e/boarding-real-api.spec.ts:41`. The real-API pass needs a `BRD_SEED` file from a throwaway seeder that was deleted, so the 9/9 run cannot be reproduced. Compare the committed `SurgeryE2ESeed`. For TEST. Row 51.

## Proposed rows for `docs/backlog/BRD.md` (start at 45; the integrator may renumber)

| # | Sev | Status | Where | Item | Source |
|---|---|---|---|---|---|
| 45 | MEDIUM | Open (before merge) | `web/lib/api/staff-boarding.ts`, `errors.ts`, `types.ts`, `shared/labels.ts` | Re-align to the BRD-001 fix round. (1) Send `vetAuthorityChangeReason` from the staff care-profile form when the vet authorisation or limit changes on a reserved stay, and map `VetAuthorityReasonRequired` to that field. (2) Add `VaccinationGateBlocked` to `StayAttentionReason` and `ATTENTION_LABEL`. | BRD-002 ARCH M1 |
| 46 | MEDIUM | Open | `my-boarding.ts:132`, `StayWorkspace.tsx:119` | Show a gate override from `gate.overridden`, with `overrideReason` and `overriddenOn`. The wire has no `overriddenBy`. | BRD-002 ARCH M2 |
| 47 | MEDIUM | Open | `web/docs/decisions.md` | Amend ADR-FE-104 (the uploader is used for belongings and diary photos by owner decision; drop `MediaReferenceFields`) and ADR-FE-105 (the preview is shown before submit; there is no `minutesLate`). | BRD-002 ARCH M3 |
| 48 | MEDIUM | Open | `BookingFlow.tsx:78,159,234` | Move the `useQueryClient` invalidations into the `useMyBoarding` mutation callbacks. | BRD-002 ARCH M4 |
| 49 | MEDIUM | Open | `my-boarding.ts`, `staff-boarding.ts` (six sites) | Replace the literal `'Asia/Dhaka'` fallbacks with the branch's zone (`useBranchTimeZone`), or ask BRD-001 for `branchTimeZoneId` on those reads. | BRD-002 ARCH M5, CODE L10 |
| 50 | LOW | Open (HO-ADM-002 owner) | `nav/areas/boarding.ts`, `components/admin/adminNavItems.ts` | Optional: add a "Boarding" entry to `adminNavItems` so the admin strip reaches Boarding setup. | BRD-002 ARCH L1 |
| 51 | LOW | Open | HO-BRD-002 §1 Amended; `EarlyCheckOutPreview.tsx:33`; `boarding-real-api.spec.ts` | Record `lib/routing/boarding-links.ts` and the owner's diary-photo confirmation in Amended. Read `creditOutcome`, not `Number(credit.amount)`. Commit a reproducible seed for the real-API pass. | BRD-002 ARCH L3, L5, L6, L7 |
| 52 | LOW | Open (BRD-001) | `CrossCuttingModule.cs:64` | Register a `boardingTasks` offline snapshot source so the HO-XCT-002 offline board can show today's boarding tasks and handling notes. Then correct the BRD-002 Amended deferral reason. | BRD-002 ARCH L4 |
| 53 | LOW | Open (POR owner) | HO-POR-001/002 | Portal dashboard stays tile: a backend `stays` section and a tile linking to `/portal/boarding/[stayId]/diary`. Owner-skipped 2026-10-10; no BRD-002 change. | BRD-002 ARCH deferral ruling |

## SCOPE CHECK
- files in allowed scope: 122/122.
  - 117 are literally within §13.
  - 5 are accepted with recorded authority or precedent:
    - `web/app/(auth)/sign-in/page.tsx` (owner 2026-10-14, one token);
    - `web/nav/areas/boarding.ts` (registry substitution);
    - `web/tests/e2e/boarding-helpers.ts` (helper precedent);
    - `web/lib/routing/boarding-links.ts` (inside the glob, beyond its note);
    - `docs/handover/HO-BRD-002.md` (§1 Amended rows).
  - The `FileUploader` uses lift §14's "any upload input" by the owner's decisions of 2026-10-14 and 2026-10-15.
- files outside allowed scope: none

## To pass

Fix H1 and re-run this gate as a delta: the adapter lines named above, the components that render them, and a test that pins each. M1 should be done in the same round, since the backend it aligns to is the one being merged.

## Status after the 2026-10-15 fix round (builder's note, for the delta re-run)

- H1: fixed (summary billing totals under their own labels, null balance stays null everywhere, no assumed signature method, no default `referredBy`); pinned by `gate-fixes.test.tsx`.
- Rows 45, 46, 47, 51: fixed (row 51: `boarding-links.ts` named, the diary-photo confirmation recorded, `creditOutcome` read; the seed cannot be reproduced -- recorded in Amended).
- Rows 48, 49, 50, 52, 53: open, unchanged.

---

## Combined re-check (ARCHITECTURE + SECURITY + CODE_REVIEW), 2026-10-15

GATE: ARCHITECTURE (combined re-check of the fix round; covers the SECURITY and CODE_REVIEW findings)
HANDOVER: HO-BRD-002
COMMITS: 2c23a1cd (range b1671913..2c23a1cd, branch ho/BRD-002, 44 files)
DATE: 2026-10-15
STATUS: PASS

### Basis

- Read `git diff b1671913..2c23a1cd` in full: every adapter, component, mock, test, the `decisions.md` append, the handover row and `.gitignore`.
- Backend at `ho/BRD-001`:
  - fc6ef889, the builder's alignment point;
  - **cede66fe**, the owner's "Charge extra nights, never block". Committed 5 minutes before 2c23a1cd. Read with `git diff fc6ef889..cede66fe -- src/`.
  - Also read on cede66fe: `StayCatGateViewModel`, `StayReportViewModel`/`SummaryBillingViewModel`, `StayViewModel`/`StaySummaryViewModel` (`BalanceDue` nullable), `CareProfileViewModel`, `EarlyCheckoutPreviewViewModel` and its handler, `EarlyCheckout.Compute`, `VaccinationGateStatus`, `StayAttentionReason`, and the dashboard and tasks `RequireAction`.
- Targeted vitest run: `gate-fixes`, `mock-api`, `wire-final`, `wire-adapters`, `care-and-stay`. **63/63 pass at 02:14 local**, inside the 00:00-03:20 Dhaka window where `mock-api` used to fail. I created a temporary `node_modules` junction and removed it afterwards. No full suite.
- Owner rule: only BLOCKER and HIGH fail.

### 1. H1, the absent rule: closed

- **Null `balanceDue`.**
  - `normaliseStay` (`my-boarding.ts:199`) and `normaliseSummary` (`:318`) keep it null. `types.ts` has `BoardingMoney | null` on the stay base, `MyStaySummary` and `StaffStaySummary`.
  - Every renderer draws nothing for null: `StayWorkspace` header, `DepositSummary`, `MyStaysList`, `StayList`, `StayCharges`.
  - Currency now comes from `deposit.required` (`ReferralPanel`, `StayOverview`, `StayWorkspace`, `StaffStayBookingForm`). That member is non-null on the wire.
  - The one exception is `ReservedPanel.tsx:48-50`, which prints "Not available" (L9).
  - The staff and customer adapters now agree.
- **Summary.**
  - `StaySummary.balanceDue` is gone. The adapter maps `billing.{billedTotal, unbilledTotal, invoicedOutstanding}`, which are all non-null `MoneyViewModel` on cede66fe, so the `money()` fallback is inert.
  - `StayCharges` shows "Not yet invoiced" and "Invoiced, still to pay" under their own labels. `billedTotal` is mapped but not shown. Not showing a server value is not an invention.
- **Signature method.** `method` is `checkOutSignatureMethod ?? null`. `SummaryCatViewModel` has no such member, so it is always null. `StaySummaryView.tsx:44` omits the parenthesis when it is null.
- **`referredBy`.** The default is gone; the type is optional.
- **Remaining defaults.** I grepped `lib/api/{my-boarding,staff-boarding,boarding-care}.ts` for `?? '...'`, `?? 0`, `?? {...}`, `?? false`, `'Typed'`, `'BDT'` and `'Care team'`.
  - The money and number defaults left are on members that are non-null on the wire (deposit, care-profile limit, occupancy counts), so they are inert.
  - Three LOW, none rendered as a value:
    - the summary `charges[].kind: 'RateUnit'` literal (styling only);
    - `approvedIncrease` 0.00 in `getVetHeadroom`, which no component reads;
    - `catName ?? 'Your cat'`. L11.
- **Pinned.** `gate-fixes.test.tsx` "H1" (5 tests over `built-wire.json`), plus `wire-adapters`/`wire-final`, which assert `balanceDue: null` and `referredBy` null.

### 2. M1 alignment: fc6ef889 closed; cede66fe adds one new MEDIUM

- **`vetAuthorityChangeReason`.**
  - `StayWorkspace` passes `requireAuthorityReason={stay.status !== 'Enquiry'}`.
  - `CareProfileForm` shows a reason field once `vetVisitAuthorised` or `vetSpendingLimit` differs from the saved baseline. It refuses to submit without a reason and sends it through `useSaveStaffCareProfile` -> `saveStaffCareProfile` (`staff-boarding.ts:378-393`), and only when one is given.
  - `VetAuthorityReasonRequired` is mapped in `errors.ts`, though `describeBoardingError` prefers the server's own message anyway.
  - The customer form and the staff booking form (an Enquiry) do not pass the flag, which matches the backend (staff, past Enquiry).
  - Gap: no component test of the form field. Runtime against the API is TEST's.
- **`VaccinationGateBlocked`.** Typed and labelled (`labels.ts:168`).
- **`isLate`.** Typed and read. The wizard warns, and the preview renders for `isEarly || isLate` with a late sentence. On cede66fe the preview answers 200 for every CheckedIn stay (on time is both false, so nothing renders) and 404 otherwise, so always fetching it is correct.
- **Override.**
  - `gateStatus` takes `gate.overridden ? 'Overridden' : gate.status`. `Overridden` is a real `VaccinationGateStatus` (value 3), so this is not an invented enum.
  - The staff line shows "Override reason: ..." when a reason is sent. `overrideReason` is staff-only on the wire.
  - `overriddenOn` is still not shown (L10, residual of row 46).
- **Re-diff against cede66fe.** These shapes and values changed after fc6ef889:

| # | Change on cede66fe | Frontend effect | Finding |
|---|---|---|---|
| a | `StayAttentionReason.UnitOverstayed` (value 6), set by the new `FlagStaysOnOverstayHandler` on a booking that overlaps a late check-out | Missing from `types.ts:245-250` and `ATTENTION_LABEL`. `StayWorkspace.tsx:139-141` renders "Needs attention -" followed by nothing on exactly the stay the owner's decision expects staff to move. The dashboard row has no reason, so it says only "needs attention". | **MEDIUM M6** |
| b | Late check-out: the allocation is not extended; `[BoardingClaimEndpoint]` removed from check-out | No 409 path in the frontend to retire (grep: no "overstay"/"next booking" text). The wizard's late note ("charges the extra nights") matches. | none |
| c | Late preview `unitFreeFrom` = the original `OccupiedTo` (`EarlyCheckout.Compute` returns `window`) | `EarlyCheckOutPreview` still says "the unit is free from {unitFreeFrom}", which is a past instant for a late stay. | LOW L8 |
| d | `WaitlistLimitReached`, `DaycareLimitReached` replace `AlreadyWaitlisted`/`StayBeyondHorizon` for the caps | No component branches on either code. `describeBoardingError` shows the server message first. | none |
| e | Lapse re-evaluates the gate; dashboard `refundDue` has no look-back; the confirm pass stops re-flagging | Values only, same shapes | none |

M6 is the same class as the first round's M1(b): backend drift after the builder's alignment point (cede66fe landed 5 minutes before the fix commit), not a frontend rule broken. That is why it is MEDIUM. It is a two-line fix (one union member, one label), and it must land before merge or be recorded as a known broken banner.

### 3. CODE_REVIEW M1-M6

- **M1 (quote on reload).** `BookingFlow` re-asks `changeEnquiry` with the stay's own values on the review step when it has no quote. One request, guarded by a ref. `ReviewAndReserve` disables Reserve while `quotePending` or while there is no quote. `StaffStayBookingForm` keeps the enquiry answer's quote. Closed. LOW L12: a page load now issues a command (`ChangeEnquiry` bumps the version and raises `BoardingEnquiryChangedEvent`), which follows the slice's own `resume()` precedent. A failure is swallowed, leaving Reserve disabled with no message. The proper fix is a backend quote on the Enquiry stay view.
- **M2 (mock).** `store.ts:274-277` returns `quote: null` and `balanceDue: null` for an Enquiry. A `mock-api` test pins this. Closed.
- **M3 (clock).** `mock-api.test.ts` fakes `Date` only, pinned to 2026-10-14T06:00Z (midday in Dhaka). It is proven by today's 02:14 run. Closed for the suite. The dev mock and e2e task-board runs are still clock-relative; row 56's wording ("the suite") is honest about that.
- **M4 (not-eating). Ruling: a server rule, not an invented derivation.**
  - The task list's flag is now `null` (absent), not `false`.
  - `TaskBoard.tsx:69-82` fills it only by membership in the dashboard's `notEating[]`, which on cede66fe is `NotEatingRule.IsNotEating(...)` computed by the server (`BoardingIncidentQueryHandlers.cs:189`).
  - The board computes nothing from observations. Both reads are under `Boarding.ViewStays`, so a task-board user can read the dashboard.
  - Residual LOW L13: a failed dashboard read turns "unknown" into "not eating: no", and the current flag is applied to whatever date is picked. Rows 57 and 70 cover the proper fix.
- **M5 (preview).** The preview error renders `role="alert"` with Try again and says the server still recalculates. Early and late come from `preview.isEarly`/`isLate` (the browser-clock `todayInZone` comparison is deleted). The credit shows from `creditOutcome !== 'None'`. Closed.
- **M6 (incident time).** Date and time inputs in branch time (`useBranchTimeZone`), defaulting to now and converted with `zonedLocalToInstant`. The form refuses empty or invalid values and anything more than 60 s in the future, and sends `occurredOn`. The mock honours it. Closed. The future check uses the browser clock; the server remains the authority.

### 4. SECURITY lows: closed

- **Signatures.**
  - `CheckInWizard` resets the drafts to `EMPTY_CHECK_IN_CAT` and calls `checkIn.reset()` after success.
  - `CheckOutWizard` blanks each draft's signature and calls `checkOut.reset()`.
  - Both mutations set `gcTime: 0` (`useStaffBoarding.ts`).
- **`BRD_SEED`.** It is required: the spec throws when `BRD_REAL_API=1` without it, and there is no default path. `.gitignore` gets `web/seed*.json`.
- **`normaliseGate`.** Extracted in `my-boarding.ts`. It is used by `normaliseEnquiry`, the reserve refusal (`BookingFlow.tsx:243`) and the daycare refusal (`DaycareBookingForm.tsx:74`), and a test pins it. Components import this one pure function from `lib/api`, which makes no fetch, so it is not a layering finding.

### 5. Cheap lows

- **ADR-FE-104/105.** `decisions.md` is +10/-0, a new entry at the end ("Appended; the entries above are left as written"). It is append-only, and the content matches the build.
- **`waitForTimeout(800)`** is replaced by `waitForLoadState('networkidle')` in both places. That is acceptable for a 30 s poll, though Playwright discourages `networkidle`. A locator or response wait would be firmer. No row.
- **48 px.** E-C2 now asserts `boundingBox().height >= 48` (it was 44). The jsdom class check is annotated.

### 6. Backlog rows 45-70: honest, with two wording corrections

- Every "Fixed" row I checked matches the code: 45, 47, 54, 55, 57, 58, 59, 61, 63, 65, 67, 68, 69.
- Every "Open" row is unchanged in code. Examples: `useQueryClient` still in `BookingFlow.tsx:4,79` (48); `'Asia/Dhaka'` literals still in the adapters (49, 64).
- Corrections:
  - **Row 46** should read "Fixed, except `overriddenOn` not shown" (L10).
  - **Row 51** is marked Fixed, but its last item ("commit a reproducible seed") is not done; only the BRD_SEED hygiene is. Split it: the seed part stays Open, for TEST (see 7).
- New rows proposed below: 71-75.

### 7. Real-API re-run: ruling, TEST must re-run it

- The fix round changed wire-facing behaviour that MSW cannot prove:
  - the `vetAuthorityChangeReason` body member and its refusal;
  - the re-quote on reload;
  - null balances against real Enquiry stays;
  - the summary billing labels;
  - the late preview.
- The merge target is now cede66fe, whose late check-out the frontend has never met.
- The 9/9 pass on 95a97bab does not cover any of this.
- **How:**
  1. Build the API from a clean checkout of `ho/BRD-001` at **cede66fe or later**, on a per-branch throwaway database (never `catecommerce_dev`).
  2. TEST **may** write a throwaway seeder, using the shape documented in `boarding-real-api.spec.ts`'s `Seed`, and delete it afterwards. Put the seed file outside the repo or at a `web/seed*.json` path (now ignored) and pass it with `BRD_SEED`.
  3. Run the existing 9 flows. Add these checks, by hand or in the spec:
     - (a) a staff care-profile edit of the vet limit on a Reserved stay, refused without a reason and saved with one;
     - (b) a reload at the review step that shows the quote, with Reserve disabled until it does;
     - (c) an Enquiry on My stays and the staff list with no balance text;
     - (d) a checked-out summary showing "Not yet invoiced" / "Invoiced, still to pay";
     - (e) a late check-out, for a stay seeded CheckedIn with its departure in the past: the preview says it is past departure and charges the extra nights, the check-out is not refused, and an overlapping next booking on the same unit is flagged `UnitOverstayed`. Its banner text is empty until M6 lands; TEST should record that, not pass over it.
- Committing a reproducible seeder (as `SurgeryE2ESeed` was) remains row 51's open item. It does not block this gate.

### 8. Regressions and scope

- No regression found.
- **Shared files.** The changed lines in `types.ts` are all lines this slice appended in the first range. Others' content is untouched; `errors.ts` is append-only.
- **Hook file.** `useStaffBoarding.ts` is this slice's own new file.
- **Unchanged files.** No `package.json`, lockfile, config, `components/files/**`, `src/**`, `tests/**`, `.ai/**` or `.claude/**` change.
- **Storage and payments.** No new storage, `FormData` or payment component.
- **Scope.**
  - `.gitignore` (repo root) is outside §13's list and not in §14. It is accepted as the SECURITY gate's own remediation and is disclosed in Amended (gate fix round). LOW L15, no row.
  - `docs/handover/HO-BRD-002.md` (one Amended row) is accepted on the first round's precedent.
- **Integrator note only.** 2c23a1cd carries a `Co-Authored-By: Claude Sonnet 5.5` trailer. Per the caller, a harness reminder told the builder to use it. This is not a gate finding.

FINDINGS
- [MEDIUM] M6 (row 71, before merge) — `web/lib/api/types.ts:245-250`, `web/components/boarding/shared/labels.ts:163-170`, `web/components/boarding/stay/StayWorkspace.tsx:139-141`. On cede66fe, `StayAttentionReason.UnitOverstayed` is not typed or labelled, so the staff banner reads "Needs attention -" followed by nothing on the booking staff must move after a late check-out. Fix: add the union member and a label (e.g. "The cat before this stay in the same unit has not left yet: move this booking."), and let TEST confirm it against cede66fe.
- [LOW] L8 (row 72) — `web/components/boarding/stay/EarlyCheckOutPreview.tsx:65-72`. For `isLate`, cede66fe sends `unitFreeFrom` as the original `OccupiedTo`, a past instant, because the allocation is not extended. The sentence "the unit is free from ..." then states a past time. Fix: omit the clause when `isLate`.
- [LOW] L9 (row 73) — `web/components/boarding/booking/ReservedPanel.tsx:47-50`. A null `balanceDue` prints "Not available" rather than drawing nothing. It is inert after reserve, because a quote exists. Fix: align it with the other renderers.
- [LOW] L10 (row 46 residual) — `web/lib/api/my-boarding.ts:132-136`, `web/components/boarding/stay/StayWorkspace.tsx`. The override line is keyed on reason presence, and `overriddenOn` is neither mapped nor shown.
- [LOW] L11 (row 74) — `web/lib/api/my-boarding.ts` `normaliseStaySummary` (summary `kind: 'RateUnit'` for every line, so the discount and credit styling is lost; read the kind from `lineRef` as `normaliseStay` does), `web/lib/api/staff-boarding.ts:627` (`approvedIncrease` 0.00 default, which nothing renders; drop it).
- [LOW] L12 (row 75) — `web/components/boarding/booking/BookingFlow.tsx:131-151`. The reload re-quote is a `ChangeEnquiry` command issued on page load, and its failure is swallowed (Reserve stays disabled with no message). Fix: show an error with Try again. Backend follow-up (HO-BRD-001): the quote on the Enquiry stay view.
- [LOW] L13 (rows 57/70) — `web/components/boarding/tasks/TaskBoard.tsx:69-82`. If the dashboard read fails, the absent flag becomes `false`; the current flag also shows on any picked date.
- [LOW] L14 — backlog wording: row 51's seed item and row 46's `overriddenOn` item are still open (section 6).
- [LOW] L15 — root `.gitignore` is outside §13. It is accepted as SECURITY-directed and disclosed. No row.

#### Proposed rows for `docs/backlog/BRD.md` (after 70; the integrator may renumber)

| # | Sev | Status | Where | Item | Source |
|---|---|---|---|---|---|
| 71 | MEDIUM | Open (before merge) | `types.ts`, `shared/labels.ts` | Type and label `StayAttentionReason.UnitOverstayed` (HO-BRD-001 cede66fe). TEST confirms the banner on a booking overlapping a late check-out. | BRD-002 ARCH re-check M6 |
| 72 | LOW | Open | `EarlyCheckOutPreview.tsx` | Omit "the unit is free from ..." for a late check-out: the allocation is not extended, so `unitFreeFrom` is in the past. | ARCH re-check L8 |
| 73 | LOW | Open | `ReservedPanel.tsx` | Draw no balance line for a null `balanceDue`, like the other renderers. | ARCH re-check L9 |
| 74 | LOW | Open | `my-boarding.ts` (summary charges), `staff-boarding.ts` (`getVetHeadroom`) | Read the summary charge kind from `lineRef`; drop the unrendered `approvedIncrease` default. | ARCH re-check L11 |
| 75 | LOW | Open (FE + BRD-001) | `BookingFlow.tsx` | Show an error when the review re-quote fails. Ask HO-BRD-001 for the quote on the Enquiry stay view, so a reload need not issue `ChangeEnquiry`. | ARCH re-check L12 |
| 46 | MEDIUM | Fixed (`overriddenOn` shown, fix round 2) | `my-boarding.ts`, `StayWorkspace.tsx` | Show `overriddenOn` with the override reason. | ARCH re-check L10 |
| 51 | LOW | Fixed except the seed: the throwaway seeder was deleted and cannot be reproduced from the branch (recorded in Amended); a committed seeder is for TEST | `boarding-real-api.spec.ts` | Commit a reproducible seeder for the real-API pass (the `SurgeryE2ESeed` precedent). | ARCH re-check section 7 |

SCOPE CHECK
- files in allowed scope: 44/44.
  - 41 are literally within §13 (including `docs/gates/HO-BRD-002-*.md`).
  - 3 are accepted:
    - `docs/handover/HO-BRD-002.md` (Amended row, first-round precedent);
    - root `.gitignore` (SECURITY remediation, disclosed);
    - `web/lib/hooks/useStaffBoarding.ts` (this slice's own new hook file).
- files outside allowed scope: none
