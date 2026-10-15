# HO-BRD-002 — Boarding — Cat Care Centre (frontend)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-BRD-002** |
| Title | Boarding — Cat Care Centre — frontend slice |
| Date | 2026-09-22 |
| Author | Claude Opus 5 |
| Backlog | [`docs/USER_STORIES.md`](../USER_STORIES.md) v2.4 §7 — **US-BRD-01 … US-BRD-21** |
| SRS | FR-BRD-1 … FR-BRD-22; FR-POR-4; **UI-1, UI-3, UI-5, UI-6, UI-7**; **NFR-USA-3** (two taps on a tablet), NFR-USA-4, NFR-USA-5, NFR-USA-6, NFR-CMP-2, NFR-PRF-3; AC-11 … AC-15 |
| Pairs with | [`HO-BRD-001`](HO-BRD-001.md) — **§12.4 there is the contract**. On any disagreement about the wire, HO-BRD-001 wins (§20.4) |
| Builds on | [`HO-CAT-002`](HO-CAT-002.md) (scaffold, `CatProfileHeader`), [`HO-ACC-002`](HO-ACC-002.md) (sessions, `client.ts`, error mapping, `PermissionDeniedState`, `UnsavedChangesGuard`, customer search), [`HO-ADM-002`](HO-ADM-002.md) (active branch, `lib/format`, `formatMoney`, `LimitedMarkup`, the in-repo grid approach, effective-dated editors), [`HO-NTF-002`](HO-NTF-002.md) (inbox), [`HO-APT-002`](HO-APT-002.md) (booking-flow patterns, `useServerNow`, front-desk polling) |
| Depends on — **not yet written** | `HO-BIL-002` (the record-payment dialog and invoice views), `HO-IMP-002` (issue house food), `HO-EMR-002` (the consultation screen a referral opens). §10.2 states what this slice does where they are absent |
| Delivery order | Section 13 of 17 in [`README.md`](README.md#delivery-order) |
| Approved design | None. Build to §11 with the existing shadcn/ui tokens |
| Baseline commit | `4007225` |
| Amended | **2026-09-22 — README C-6, C-7.** This slice's `/portal/boarding/[stayId]/diary` is now the **only** customer stay diary: HO-POR-002's competing page was retired, and its day-list, media-viewer and accessibility guidance (its withdrawn ADR-FE-125) is inherited here. The portal dashboard's stays tile links to this page. Surgery on a boarded cat (US-SRG-09) also lands in this area — the staff stay page shows **Away — in surgery** using the existing clinic-referral state.<br><br>**2026-10-14 — Amended (integrator, HO-XCT-002 §5.3; backlog XCT rows 61 and 107); ratifies edits already made on `ho/XCT-002`, recorded after the fact as the integrator's OQ-1 duty (ACCEPTANCE gate `docs/gates/HO-XCT-002-ACCEPTANCE.md`, record-only BLOCKER on §19/§20.2).** Appends only; no existing text is reworded. **HO-XCT-002 §11.3 — offline banner and read-only state:** the boarding task screen renders `OfflineBanner` and takes its read-only state from `useConnectivity()`. As built, no HO-BRD-002 file is edited: `OfflineBanner` is mounted once in `web/app/(staff)/layout.tsx` (a line HO-XCT-002 §13 allows), above every staff page including the boarding screens, and the shared `ConfirmDialog` and `WriteButton` disable with "Offline — can't save" through `useWriteEnabled()`. The boarding half of the offline board (A1) is HO-BRD-002's, as HO-XCT-002 §18 defers it. Why: HO-XCT-002 §5.3. The banner line was made by the HO-XCT-002 build before this row existed (declared in its §1 Amended (17) and (22)); it matches the §5.3 text, lies inside HO-XCT-002 §13, and was reviewed by ARCHITECTURE (re-check 2 PASS, `0b69692a`). The integrator ratifies it as made; nothing is to be rebuilt. |
| Amended (build) | **2026-10-14 -- owner started Boarding (it was on hold from 2026-10-10); built on `ho/BRD-002` from master `5a187b6b`, in parallel with HO-BRD-001 (`ho/BRD-001`, unmerged). Built against HO-BRD-001 section 12.4, then aligned to the contract the backend published in its section 1.1 Amended and to the view models it had committed (Phases A-E) -- the built backend wins.** <br><br>**Contract differences from section 12.4 (all handled in `lib/api/{my-boarding,staff-boarding,boarding-care}.ts`; the mock emits the built shapes so the adapters are exercised; fixtures from the C# records are in `tests/components/boarding/wire-adapters.test.ts`).** (1) Rate cards: `GET` is a flat array with `status: Current/Scheduled/Past` (not three lists); `POST` takes the four rates as plain `"1000.00"` strings. Rate cards are per branch (`X-Branch-Id`). (2) Care routine: times are `{kind, time}`. (3) Care profile: feedings carry `time` (not `localTime`); the spending limit is a plain `"0.00"` on write and a money object on read. (4) Vaccination gate reasons are `{vaccineCode, vaccineName, reason, dueOn}` (the member is `reason`, not `kind`); a stay's per-cat gate is `gate{status, reasons, evaluatedOn, overridden, overrideReason}`. (5) Stay view: `unit{unitId,code,name,unitType,block}` (not `unitName`), `deposit{required,state,dueBy,received,pending,outstanding}`, `needsAttention`/`attentionReason`, `checkIn{weightKg,...}`; it carries **no** `charges`, `invoices`, `referrals`, `approvals`, `incidents`, `pendingApprovals`, `cancellation` (no deposit outcome before cancelling), `canOverrideGate`, `customerPhone` or `catPhotoUrl` -- the adapter fills empty collections and derives Cancel from `allowedTransitions[]`, so **the Charges tab, the referral/approval lists, the portal incident/approval links on the stay page and the pre-cancel deposit sentence (U-B6) show nothing until the backend adds those members**. (6) Stay summary rows: `requestedUnitType`, `unitCode`, `customerName`; no gate/attention/isolation flags. (7) Staff availability lists `units[{unitId,code,unitType,capacityCats,block,isFree}]` flat (no per-type `freeUnits`, no unit `name`) and a `violation` member. (8) Occupancy is per-unit per-day `cells[{date,state: Free/Occupied/Maintenance,stayId,label}]` with no cat names and no turnaround marker: bars are built from consecutive cells, cats are not named and the turnaround hatching has no data. (9) Task day list: groups are `{unitId,code,name,unitType,block,tasks}`; a task has `catIsHighRisk`/`handlingNote`, `assignedCaretakerName`, no `minutesLate` (the overdue chip reads "Overdue" without minutes), no per-cat not-eating flag (the task board cannot show "Not eating -- flagged"; the observations answer and the dashboard carry it as `notEating[]` cat ids). (10) Check-in/out bodies use `signature` (not `ownerSignature`) and belongings carry `fileIds[]` (HO-FIL-001 file ids, not storage keys); **superseded by the owner decision below:** belongings photos are now uploaded through HO-FIL-002's `FileUploader` and their `fileId`s are sent. (11) Unit update (`PUT`) takes no `code`. (12) `GET /api/boarding/unit-types` is a bare array with `additionalCatNightlyRate` and no daycare member: the daycare form shows no prices until the backend adds them (Phase H is unbuilt). <br><br>**Gaps against the handover, not shimmed (section 20.5).** (a) Early check-out preview: **owner decision below** -- shown before confirming; the endpoint was not committed on `ho/BRD-001` when this was built (see the pending-alignment note). (b) The enquiry answer carries the quote but no deposit amount or pay-by time: the review confirm says the deposit is shown on reserving (the UI-3 sentence cannot state it). (c) Neither the availability answer nor the enquiry carries the check-in/check-out windows: the stay page shows only "Check-in from HH:mm" derived from `occupiedFrom`. (d) HO-BIL-002 is built on master, but its payment dialog is a payment component (section 14): **Record payment** links to `/staff/billing/payments/new?customerId=` instead. **Deviations from section 13 (each one line or one file):** nav entries are in `nav/areas/boarding.ts`, not the three layouts (the shells read the registry; the HO-APT-002/HO-IMP-002 precedent), plus a section tab row in `app/(staff)/staff/boarding/layout.tsx` and `app/(staff)/staff/admin/boarding/layout.tsx` (the admin Boarding group is one "Boarding setup" sidebar entry opening that tab row); `lib/hooks/useMediaQuery.ts` is a new hook (allowed); `tests/e2e/boarding-helpers.ts` is a helper outside the `boarding-*.spec.ts` glob (the APT/EMR/POR precedent) and reuses `portal-helpers.ts`'s hold-until-MSW-ready; `tests/components/boarding/*.test.ts` includes contract tests that are not component tests. **Owner decisions 2026-10-14 (each an owner-authorised scope amendment, applied on `ho/BRD-002`):** (1) *"Add the uploader"* -- check-in belongings photos reuse `components/files/FileUploader` (purpose `BoardingBelongings`, audience staff) unmodified and send the uploaded `fileId`s in each belonging's `fileIds[]`; this lifts ADR-FE-104 for **belongings** at check-in only (diary and incident media remain references until the backend's media shape is aligned; that is outside this amendment). (2) *"Yes, allow it"* -- `'/boarding'` is added to the customer sign-in `?next=` allow-list, a one-token edit to `app/(auth)/sign-in/page.tsx` (outside section 13), so an interrupted search returns with its parameters. (3) *"Show before"* -- the check-out wizard's summary step shows the server's recalculated nights, basis, lines and credit before submit (`EarlyCheckOutPreview`, `GET /api/staff/boarding/stays/{id}/check-out-preview`). **Pending alignment:** that route and its shape (`{isEarly, nightsBooked, nightsCharged, quote, lines, credit, creditOutcome}`) are the *expected* ones, built behind the mock because `ho/BRD-001` (9eb583c7) had not committed a preview endpoint (its check-out controller returns only `{stay}`); when it lands, re-align `getCheckOutPreview` and the mock, and until then the wizard keeps its plain notice and the post-submit result. `/boarding` sits under the `(portal)` guard, so it needs a session like `/book` (the public availability endpoint is unchanged). **Not built, named:** the offline board's boarding instructions (HO-XCT-002's `useConnectivity`/`OfflineBanner` are not on master -- the task and board screens will take them when XCT-002 merges), the portal dashboard stays tile (HO-POR-002 is outside section 13; owner-skipped 2026-10-10), the E-series against the real API (pending: HO-BRD-001 had Phases A-E committed and F-H in progress when this was built). |
| Amended (final alignment) | **2026-10-15 -- re-aligned to HO-BRD-001's FINAL contract (`ho/BRD-001` 95a97bab; contract commit 79b1236d; its section 1.1 Amended and CONTRACTS rows 50-59) and run against the real API. This row supersedes the stay-view, dashboard, occupancy, preview and 'pending' statements of the row above; the backend wins.** <br><br>**Now matched (adapters in `lib/api/{my-boarding,staff-boarding,boarding-care}.ts`; fixtures in `tests/components/boarding/built-wire.json` are JSON captured from the running API).** (1) Stay view: `charges[]` (`chargeId, lineRef, net, billed, voided`; voided dropped, kind read from the `lineRef`), `referrals[]` (with `returnedOn`, no `referredBy`), `approvals[]` (`decidedVia`, `decisionNote`), `incidents[]` (`ownerAcknowledged`; an `occurredOn` of `0001-01-01` means unset and falls back to `reportedOn`), `cats[].awayReason` (drives the away state), `cats[].checkIn.belongings[]` (the check-out list reads these, not the summary), staff `allocations[{unitId,unitType,occupiedFrom,occupiedTo}]`, `headroom[]`, `creditDue`. `allowedTransitions[]` carries `NoShow` (shown as Record no-show), `ExpireEnquiry`/`Lapse` (ignored: system moves) and no Extend/Relocate: those two are offered while Check in or Check out is allowed. History `changedBy` is an id. (2) Check-out preview: `GET .../check-out-preview` -> `{isEarly, nightsBooked, nightsCharged, quote, lines[{lineRef, description, net, action: Keep/Void/Record}], credit, creditOutcome None/Credit/Refund, today, bookedDepartureOn, chargedDepartureOn, unitFreeFrom, previousTotal, newTotal}`; the wizard shows it before submit. This closes the 'pending alignment' note above. (3) New: `POST .../referrals/{id}/return` (the **Cat is back** button on the referral list). (4) Dashboard rows are lean: `unitCode`, `dueOn` (shown in the branch time), balances `{status, amount, depositDueBy, reason}`; no customer name or phone on `callOwner[]`, no risk flags, no stay `balanceDue` or customer name on arrivals, no incident `kind`, no attention `reason`: those show as absent (a link to the stay instead of a name or number). (5) Occupancy cells have `catNames[]` and `inTurnaround`: bars name the cats and the turnaround day is marked. (6) Diary: `completedTasks`, observations without ids, media with a `file` signed-URL member (a **View photo** link). Summary: observations are a flat list per cat (grouped by date here), weights are numbers, `billing.invoiceIds` are the only invoices (the Charges tab and the customer stay page read invoice ids from the summary; number, kind and total are not on the wire). (7) Approvals: both answers are `{approve: boolean, note}`; the staff request sends the amount as a plain string; the owner approval has no limit/used/remaining (the sentence is shown only when sent). (8) Incidents: `occurredOn` is required (sent as now); there is no per-stay incident route, a stay's incidents are `GET incidents?stayId=`; the queue has no customer name or unit (shown without). (9) Media: `POST .../media` takes `{fileId, catId, incidentId, caption, capturedOnLocalDate}`. The diary-photo dialog now uses the shared `FileUploader` (purpose `BoardingMedia`) -- **an extension of the owner's 2026-10-14 'Add the uploader' decision from belongings to diary photos, made so the endpoint can be used at all. Owner decision 2026-10-15: "Yes, diary photos too" -- confirmed.** Incident photos have no field on the incident request and are not offered. (10) Daycare: day list `{date, capacity, slotsTaken, offered, visits[]}` (visit rows have no zone or cat risk flags; filled), book answers `{visitId}`, daycare rate cards are a flat list with `status` and a string-rate POST. (11) Task list: `date` is required (the board sends the branch's today); a task has no `minutesLate` (the chip says Overdue) and the list has no per-cat not-eating flag (the board shows it only from the observations and dashboard). (12) Gate: `vaccinationGate.blockedCats[].bookingLink` is an **absolute** URL on the web app's own origin; only its path and query are followed (`toInternalLink`), so a foreign host can never be linked. (13) The enquiry answer carries the quote but the stay view's `quote` is null for an Enquiry, so the booking flow keeps the quote from the enquiry answer. (14) **Billing auto-confirms**: recording the earmarked deposit confirmed the stay by itself on the real API; Confirm stay appears only until it has (the E-B1 real-API step accepts either). (15) A stay summary row's `unitCode` was null for an in-house stay on the real API (the staff list shows 'No unit'): a backend question. <br><br>**Not on the final wire, shown as absent (not shimmed):** stay-view `invoices` (use the summary), the pre-cancel deposit outcome (U-B6: the cancel confirm cannot state it before cancelling), `canOverrideGate` (the Override control is offered for a blocked cat and the server decides: a refusal shows its message), the enquiry's deposit amount and pay-by (the review says the deposit is shown on reserving), check-in/out windows on availability, `minutesLate` and the not-eating flag on tasks. <br><br>**Real-API run (2026-10-15).** API built from a clean detached checkout of `ho/BRD-001` 95a97bab (the BRD-001 worktree had uncommitted gate edits and a newer, unmigrated entity that broke observations), port 5117, throwaway database `catecommerce_brd002e2e` migrated from that commit; web on 3117 with `NEXT_PUBLIC_USE_MOCKS=false`; seeded by a throwaway seeder (since deleted). `tests/e2e/boarding-real-api.spec.ts` (skipped unless `BRD_REAL_API=1`): the gate, booking and reserve, deposit and confirm, check-in with a belongings photo, tasks and an observation, an incident, the owner diary, early check-out with the preview, and daycare -- 9 of 9 pass on a fresh stay. Referral return and the owner and staff approval answers were also exercised against the API directly. Not run against the real API: the E-B2 lapse, the E-A4 waitlist notice, the E-F1 60-night cycle invoice, the dashboard timing (E-X3) and axe on real data. |
| Amended (gate fix round) | **2026-10-15 -- fixes after the ARCHITECTURE (FAIL, b1671913, 1 HIGH), SECURITY (PASS, 32ad0001) and CODE_REVIEW (PASS, 86bec810) gates; `lib/api/my-boarding.ts` read against `ho/BRD-001` fc6ef889 (read only).** <br><br>**H1, the absent rule.** (1) The summary has no single balance: `StaySummary.balanceDue` is gone; the report's own `billing.{billedTotal, unbilledTotal, invoicedOutstanding}` is shown under its own labels ("Not yet invoiced", "Invoiced, still to pay"), so a final invoice no longer reads "Balance due 0.00". (2) A null `balanceDue` (an Enquiry has no quote) stays null end to end and every place that rendered it draws nothing (stay workspace, My stays, staff list, deposit summary, reserved panel, charges); currency is read from the deposit. (3) The signature `method` is not on the report: it is null and "(typed name)/(drawn signature)" is printed only when sent. (4) `referredBy` is no longer defaulted. Each is pinned by a test over the captured wire (`tests/components/boarding/gate-fixes.test.tsx`, `wire-adapters.test.ts`). <br><br>**M1, fc6ef889.** Staff changing the vet authorisation or limit on a stay past Enquiry must give a reason: the care-profile form shows a required reason field once either value differs and sends `vetAuthorityChangeReason`; `VetAuthorityReasonRequired` is mapped. `StayAttentionReason.VaccinationGateBlocked` is typed and labelled. The preview's `isLate` is read: a late check-out shows the server's recalculation and a note. The other view models were re-diffed (`git diff 95a97bab..fc6ef889` over contracts, view models and controllers): no other member changed; the rest is projection and value changes. Also: the gate override is shown from `gate.overridden` with its reason (the wire has no `overriddenBy`). <br><br>**CODE_REVIEW.** M1 reload at review: the booking flow asks the enquiry again for the quote (staff form keeps the enquiry answer's quote); Reserve is disabled until a quote is known. M2 the mock returns `quote: null` and `balanceDue: null` for an Enquiry. M3 the mock-api suite pins the clock to midday in Dhaka. M4 the task list's missing not-eating flag is now `null`, and the board takes it from the dashboard's `notEating[]` (the server's rule); a backend follow-up to put it on the task list is row 70. M5 the check-out wizard decides early/late from the preview (`isEarly`/`isLate`), shows an error with Try again when the preview fails, and says the server still recalculates on submit. M6 the incident form has date and time fields (branch time, default now, not in the future) sent as `occurredOn`. Lows: `waitForTimeout(800)` replaced by a quiet-network wait; the 48 px Done target is asserted on a real box (`boundingBox().height >= 48` in E-C2); the preview credit shows from `creditOutcome`. <br><br>**SECURITY lows.** The check-in and check-out drafts (signatures) are cleared after a successful submit and both mutations use `gcTime: 0` and are reset; `BRD_SEED` is required by the real-API spec and `web/seed*.json` is git-ignored; the daycare and reserve gate payloads go through the same link rewrite as the enquiry's (`normaliseGate`). <br><br>**Cheap lows.** ADR-FE-104 and ADR-FE-105 are corrected by an appended entry in `web/docs/decisions.md`; `lib/routing/boarding-links.ts` (inside the `web/lib/routing/**` glob, beyond "allow-list only", on the `portal-links.ts` precedent) is named here. The offline-board deferral reason in Amended (build) is stale: `useConnectivity`/`OfflineBanner` are on master and mounted in the staff layout; what is missing is a HO-BRD-001 `boardingTasks` snapshot source (`CrossCuttingModule.cs:64`), row 52. <br><br>**Not done, recorded.** Row 48 (query keys out of `BookingFlow`), row 49 / CODE low (literal `Asia/Dhaka` fallbacks for branch zones the wire omits), row 50 (admin strip entry), the Undo wording and per-task rollback, the occupancy turnaround wire test, and the E-series cases listed under Real-API run. <br><br>**Real-API re-run: not made.** The pass needs a seeded database; the throwaway seeder was deleted as instructed, so it cannot be reproduced from this branch (it created two branches' worth of staff and a customer through the aggregate repositories, one unvaccinated cat, vaccinated cats, a tax class and a vet). `boarding-real-api.spec.ts` documents the seed shape it reads from `BRD_SEED`. These fixes were verified against MSW and the captured wire only; the staff care-profile edit on a reserved stay (row 45) and the late-check-out preview still need a TEST pass against fc6ef889 or later. <br><br>**Backlog rows for `docs/backlog/BRD.md` (on `ho/BRD-001`; the integrator merges them, numbered from 45).** ARCH 45-53 and CODE 54-66, SECURITY 67-69 and the follow-up 70, as listed in the three gate files in `docs/gates/HO-BRD-002-*.md` with their status after this round: fixed here -- 45, 46, 47, 51, 54, 55, 56, 57, 58, 59, 61, 63, 65, 67, 68, 69; open -- 48, 49, 50, 52, 53, 60, 62, 64, 66, 70. |
| Amended (acceptance decisions) | **2026-10-15 -- owner decisions after the ACCEPTANCE gate (FAIL on A29 only, a41be7eb).** (1) *Speed check:* **"Measure at merge"** -- NFR-PRF-3 (p95 <= 1.5 s over 20 loads of the dashboard and the task board) is not measured on this branch; A29 is deferred to the integrator's real-API run after HO-BRD-001 merges (row 85). (2) *Backend gaps:* **"Ship, follow-up fixes"** -- Call owner has no name or phone on the dashboard and queue, the cancel and reserve confirms cannot state the deposit details, and Override vaccination is shown to every staff member because `canOverrideGate` is not on the wire. These ship as they are; the follow-ups are HO-BRD-001-FU1 (backend) and a matching screens follow-up (rows 90, 92). (3) Row 77/86: the staff screens no longer say "No deposit needed" for an Enquiry: the stay workspace header, the staff deposit panel, the staff list, My stays and the dashboard say the deposit is set at reserve (`DepositStateBadge` takes the stay status). Backend row 84 (send no deposit before reserve) stays open. |
| Amended (approval) | **2026-10-15 -- owner approved the merge ("merge"); merged to master by the integrator after HO-BRD-001.** (1) Owner-authorised edits outside section 13, recorded here: the sign-in `/boarding` allow-list token in `web/app/(auth)/sign-in/page.tsx` ("Yes, allow it"; `sanitizeNextPath` unchanged) and the root `.gitignore` entry `web/seed*.json` (SECURITY LOW, backlog old #68, so a real-API e2e seed file, which can hold credentials, cannot be committed). (2) Backlog rows 45-96 of this handover's gate files are appended to `docs/backlog/BRD.md` as rows 70-121 (new = old + 25); this document and the gate files keep the old numbers. (3) Carried to the post-merge real-API run (a committed seeder is still owed, old #82): the A29 p95, T-R18b and the E-A2/E-A3/E-A4/E-B2/E-D1/E-E1/E-E2/E-E4/E-F1/E-F2 conditions. |

---

## 2. Objective

Build **two very different products over one API**.

- **The owner's side.** Check availability for dates and cats, see prices and the total, give the
  care instructions, be told honestly when a cat's vaccinations block the booking (and book the
  vaccination from that page), reserve, read how to pay the deposit offline, then watch the stay —
  daily diary, incidents to acknowledge, vet-cost approvals, the check-out summary.
- **The care centre's side.** A **tablet task board** a caretaker can work one-handed, a check-in and
  check-out wizard with belongings and a signature, observations, incidents, isolation, an occupancy
  board, and a manager's dashboard that refreshes itself.

Four things make this harder than it looks:

1. **There is no payment step.** The owner is shown what to pay and how to pay it offline; a **staff
   member** records it in Billing and the stay then confirms. The UI must never look like it can take
   money (ADR-FE-106).
2. **The caretaker's budget is two taps.** NFR-USA-3 is a measured acceptance criterion, not a
   sentiment: the task board's completion path is one tap on a 44 px target with no dialog
   (ADR-FE-100).
3. **Time belongs to the branch.** Arrival, departure, due times and the check-in window are rendered
   in the branch's zone and labelled (HO-ADM-002 ADR-FE-014). Dates are `yyyy-MM-dd` strings.
4. **The server decides.** Availability, prices, capacity, the vaccination gate, allowed actions and
   overdue state all come from the API. The UI never computes a price or a conflict (ADR-FE-105,
   ADR-FE-107).

---

## 3. In scope

### 3.1 Inherited — do not revisit

The HO-CAT-002 stack (Next.js 15 App Router, React 19, TypeScript, Tailwind v4, shadcn/ui in-repo,
TanStack Query v5); HO-ACC-002's session model, route groups, `client.ts`, error mapping,
`PermissionDeniedState`, `UnsavedChangesGuard`, customer search; HO-ADM-002's active-branch header,
`lib/format` (dates, times, `formatMoney`), `LimitedMarkup` renderer, in-repo grid approach and
Current/Scheduled/Past editors; HO-CAT-002's `CatProfileHeader` (UI-7); HO-APT-002's `useServerNow`
and 30-second polling rules.

### 3.2 Stories and screens

| Story | Screens | Route |
|---|---|---|
| US-BRD-02, 03 | **Booking flow**: dates and cats → available unit types with itemised prices → cats → care profiles → review and reserve; full-range state offering nearest dates or the waitlist | `/boarding` (deep-linkable `?arrivalOn&departureOn&catCount&unitType`) |
| US-BRD-04 | Vaccination block panel inside the flow, per cat, with **Book a vaccination** (HO-APT-002's `/book`) and **Resume** on the saved enquiry | `/boarding`, `/portal/boarding` |
| US-BRD-05, 07 | Per-cat care-profile forms on one reservation | `/boarding` step 4 |
| US-BRD-06 | Reserved page: deposit amount, pay-by countdown, **how to pay** (content page), balance due | `/portal/boarding/[stayId]` |
| US-BRD-12 | Stay diary — days with observations and media | `/portal/boarding/[stayId]/diary` |
| US-BRD-13 | Incident detail with **I've seen this** acknowledgement | `/portal/boarding/[stayId]/incidents/[incidentId]` |
| US-BRD-14 | Vet-cost approval request: amount, what for, **Approve / Decline** | `/portal/boarding/[stayId]/approvals/[approvalId]` |
| US-BRD-17 | Stay summary and charges | `/portal/boarding/[stayId]/summary` |
| US-BRD-19 | **Boarding dashboard** | `/staff/boarding` |
| US-BRD-02, 15 | **Occupancy board** (units × days) with extend and relocate dialogs | `/staff/boarding/board` |
| US-BRD-06, 08, 16, 17 | Stay list and stay workspace: overview, care profiles, **check-in wizard**, tasks, observations, media, incidents, isolation, referrals, charges, **check-out wizard** | `/staff/boarding/stays`, `/staff/boarding/stays/[stayId]` |
| US-BRD-09, 10, 11 | **Task board** (tablet) and the observation sheet | `/staff/boarding/tasks` |
| US-BRD-13 | Incident queue incl. **Call owner** | `/staff/boarding/incidents` |
| US-BRD-18 | Isolation flag dialog, isolation entry log | Stay workspace |
| US-BRD-01, 03 | Units, maintenance blocks, rate cards, care routine (admin) | `/staff/admin/boarding/units`, `…/rate-cards`, `…/care-routine` |
| US-BRD-20 | Cycle and final invoices shown on the stay (issued by HO-BIL-002) | Stay workspace → Charges |
| US-BRD-21 | Daycare: customer booking and the staff day list | `/boarding/daycare`, `/staff/boarding/daycare` |
| US-BRD-02 AC3 | Waitlist join and my waitlist | `/boarding` (full state), `/portal/boarding` |

---

## 4. Out of scope

| Excluded | Why |
|---|---|
| Any backend change | HO-BRD-001 owns `src/**` |
| **Any payment, deposit capture, card field or payment link** | SRS A-2, C-5, Phase 5; ADR-FE-106. The deposit is recorded by staff in **HO-BIL-002**'s dialog |
| Invoice and credit-note rendering, refund flows | HO-BIL-002 — this slice links to them |
| Uploading files of any kind | No object storage (HO-BRD-001 OQ-1, ADR-FE-104) |
| The consultation screen a referral opens | HO-EMR-002 |
| Issuing house food from stock | HO-IMP-002 — this slice links to it and lists what was issued |
| Offline task list (NFR-AVL-3, US-XCT-01) | HO-XCT-002 |
| The portal home dashboard | HO-POR-002 |
| Occupancy and care-compliance reports | HO-RPT-002 |
| A calendar, scheduler, drag-and-drop, signature, chart, date-picker or time-zone library | §10 |

Forbidden shapes are HO-ACC-002 §4.3's, unchanged.

---

## 5. Prerequisites

| Required | State |
|---|---|
| Node 20 LTS, HO-CAT-002 scaffold | ❌ at baseline — §20.1 |
| **HO-ACC-002, HO-ADM-002, HO-NTF-002, HO-APT-002 built** | ❌ at baseline — §20.1 |
| HO-BRD-001 API | ❌ — build against MSW generated from its §12.4, then re-run every E-case against the real API |
| **HO-BIL-002's record-payment dialog** | ❌ not written — until it exists the staff deposit step shows an explicit *"Record the payment in Billing, then press Confirm"* path (§11.6, ADR-FE-106) |

---

## 6. Governing documents

| Document | What you need |
|---|---|
| [`HO-BRD-001`](HO-BRD-001.md) | §12.4 endpoints and shapes; §10.4 codes; §8.5 the status machine; §8.4/§8.8 what availability and a quote are; §8.6 the gate payload; §8.9 tasks and overdue; §8.10 deposit states; §8.19 the messages an owner receives; §12.3 who may do what |
| [`HO-ADM-002`](HO-ADM-002.md) | §8.3 active branch; §8.4 `lib/format` and `formatMoney`; ADR-FE-014 zones; ADR-FE-016 the grid; ADR-FE-017 `LimitedMarkup`; ADR-FE-018 effective-dated editors |
| [`HO-APT-002`](HO-APT-002.md) | §8.3 state keys; §8.4 `useServerNow`; ADR-FE-027 polling that never resets input; ADR-FE-028 actions from `allowedTransitions` |
| [`HO-ACC-002`](HO-ACC-002.md) | §11.2 client; §11.14 error mapping; customer search; `UnsavedChangesGuard`; `PermissionDeniedState` |
| [`HO-CAT-002`](HO-CAT-002.md) | `CatProfileHeader` (UI-7) and its compact variant; the honest "not available" pattern for media |
| [`docs/SRS.md`](../SRS.md) §3.1, §6.4 | UI-1/3/5/6/7; NFR-USA-3/4/5/6 |

---

## 7. SRS UI requirements as build rules

| Req | Rule | Where enforced |
|---|---|---|
| **UI-1 / NFR-CMP-2** | 360 px floor. The booking flow and every portal page are single-column; the occupancy board becomes a **day list of units** below 768 px (no horizontal grid scroll); the task board is a single column of cards on a phone and two columns on a tablet | §11.1, §11.5, §11.8 |
| **UI-3** | Confirm, stating what is affected: reserve (*"Reserve Family 2 for Mitu and Tom, 12–19 Oct, ৳9,000 — deposit ৳2,700 due by Sat 11 Oct, 18:00"*), cancel (naming the deposit outcome), override vaccination (naming the cat and the reason box), flag infectious (*"Mitu will be moved to Isolation 1"*), relocate, extend, check-out, hide media, resolve incident | Each screen |
| **UI-5** | Dashboard, task board, occupancy board and incident queue refetch every **30 s**; a refetch never resets an open form, wizard step or dialog (ADR-FE-101) | §11.5, §11.7, §11.8, §11.9 |
| **UI-6** | WCAG 2.1 AA. Task cards are `<button>`s in a labelled list, not a canvas; severity, gate status, deposit state and overdue are **text plus** colour/icon; the signature pad has a typed-name alternative (ADR-FE-103); the occupancy board has a list equivalent; live regions announce task completion, poll-driven new incidents and the pay-by countdown at 24 h, 1 h and expiry | §11 throughout |
| **UI-7** | `CatProfileHeader` (full on the stay workspace, compact on every task card, observation row, incident and diary entry) — the high-risk badge and handling note are what a caretaker must see before opening the unit | §11.5 … §11.9 |
| **NFR-USA-3** | **A completed feeding task takes no more than two taps on a tablet**: one to reach the cat or unit group (already open by default) and one on **Done**. No dialog, no confirmation, no required note (ADR-FE-100) | §11.7, U-D1, E-D1 |
| **NFR-USA-4/5** | Server messages in plain language; the wizards and care-profile forms keep values across validation errors and navigation (`UnsavedChangesGuard`) | §11.8, §11.3 |
| **NFR-PRF-3** | The dashboard and task board render their first meaningful content ≤ 1.5 s at p95 against the real API; nothing blocks on a secondary section (each section streams into its own card) | E-X3 |

---

## 8. Frontend architecture

### 8.1 Route map — additions

```
web/app/
  (portal)/
    boarding/page.tsx                                   booking flow (stepper)        US-BRD-02,03,04,05,07
    boarding/daycare/page.tsx                           daycare booking               US-BRD-21
    portal/boarding/page.tsx                            my stays + enquiries + waitlist
    portal/boarding/[stayId]/page.tsx                   stay overview, deposit, care  US-BRD-06
    portal/boarding/[stayId]/diary/page.tsx             diary                         US-BRD-12
    portal/boarding/[stayId]/incidents/[incidentId]/page.tsx  incident + acknowledge  US-BRD-13
    portal/boarding/[stayId]/approvals/[approvalId]/page.tsx  vet cost approval       US-BRD-14
    portal/boarding/[stayId]/summary/page.tsx           stay summary + charges        US-BRD-17
    layout.tsx                                          ⚠ nav entry: Boarding
  (staff)/staff/
    boarding/page.tsx                                   dashboard                     US-BRD-19
    boarding/board/page.tsx                             occupancy board               US-BRD-02,15
    boarding/stays/page.tsx                             stay list
    boarding/stays/new/page.tsx                         staff booking                 US-BRD-02…07
    boarding/stays/[stayId]/page.tsx                    workspace (tabbed)            US-BRD-06…18
    boarding/tasks/page.tsx                             ⭐ task board (tablet)         US-BRD-09,10,11
    boarding/incidents/page.tsx                         incident queue + call owner   US-BRD-13
    boarding/daycare/page.tsx                           daycare day list              US-BRD-21
    admin/boarding/units/page.tsx                       units + maintenance           US-BRD-01
    admin/boarding/rate-cards/page.tsx                  rate cards (+ daycare)        US-BRD-03,21
    admin/boarding/care-routine/page.tsx                routine times                 US-BRD-09
    layout.tsx                                          ⚠ nav entries: Boarding, Tasks
    admin/layout.tsx                                    ⚠ adminNavItems: Boarding group
  (portal)/pages/[slug]/page.tsx                        ⚠ reused for boarding-payment-instructions (HO-ADM-002)
```

`/boarding` is public up to the point of reserving; creating an enquiry requires a session, so an
unauthenticated visitor is sent to sign-in with `?next=` carrying the search parameters (one entry
added to HO-ACC-002's open-redirect allow-list).

### 8.2 API modules

`lib/api/boarding-public.ts` (availability, unit types), `lib/api/my-boarding.ts` (enquiry, care
profile, reserve, cancel, stays, diary, incidents, approvals, summary, waitlist, daycare),
`lib/api/staff-boarding.ts` (units, maintenance, rate cards, routine, availability, occupancy,
stays, reserve/confirm/cancel/no-show, check-in/out, extend/relocate, isolation, referrals,
approvals, dashboard), `lib/api/boarding-care.ts` (task day list, complete, skip, observations,
media, incidents). Types are generated by hand from HO-BRD-001 §12.4 into `lib/api/types.ts`.

### 8.3 State

| Kind | Where |
|---|---|
| Booking search (arrival, departure, cat count, unit type) | **URL search params** — reload-safe, deep-linkable |
| The enquiry | **On the server** (`stayId` in the URL from step 3 on) — a vaccination-blocked booking survives a reload and a trip to the vaccination booking (ADR-FE-099) |
| Care-profile drafts | react-hook-form per cat, submitted per cat on **Save**; `UnsavedChangesGuard` |
| Check-in / check-out wizard | Component state for the whole wizard, submitted as **one** request (ADR-FE-108) |
| My stays, stay detail, diary | `['me','boarding', …]` |
| Dashboard, task board, board, incident queue | `['staff','boarding', …, activeBranchId]`, each `refetchInterval: 30_000` |
| Task completion | Optimistic mutation with rollback on error (ADR-FE-100) |
| Pay-by countdown | `useServerNow()` (HO-APT-002 ADR-FE-025) |

### 8.4 Formatting — `lib/format` additions

- `formatStayDates(arrivalOn, departureOn, zone)` → `12–19 Oct 2026 · 7 nights`.
- `formatDueTime(dueOn, zone)` → `09:00`, with `formatOverdue(dueOn, now)` → `25 min late`.
- `formatCheckInWindow(window, zone)` → `Check-in 14:00–19:00`.
- Money: **`formatMoney` only** (HO-ADM-002); amounts stay strings end to end; **no arithmetic on
  money in the client** (ADR-FE-105).
- Dates chosen anywhere are branch-local `yyyy-MM-dd` strings, never `Date` objects.

---

## 9. ADRs and open questions

Record in `web/docs/decisions.md`, continuing HO-APT-002's series. **This handover owns
ADR-FE-099 … ADR-FE-108.**

| ADR | Decision | Why |
|---|---|---|
| **ADR-FE-099** | The booking flow is **one route with a stepper**; the search lives in URL params and the **enquiry lives on the server** from the cats step on, with `stayId` in the URL. A vaccination block renders in place with a **Book a vaccination** link and the enquiry is resumable from `/portal/boarding` | US-BRD-04 AC2 — "without re-entering details" only works if the draft is not in memory |
| **ADR-FE-100** | The task board completes with **one tap on a 44 px `Done` button**, optimistically, with a live-region announcement and an **Undo** only while the request is in flight; no dialog, no required note; a failure rolls the card back and shows the server's reason | NFR-USA-3 is measured (E-D1); a confirm dialog would double every tap |
| **ADR-FE-101** | Dashboard, task board, occupancy board and incident queue poll every 30 s; a poll **never** resets an open wizard, form, dialog or an in-flight completion — new data is applied around them, and newly arrived incidents are announced rather than re-ordering focus | UI-5 "without losing unsaved input" |
| **ADR-FE-102** | The occupancy board is an **in-repo CSS grid** (rows = units grouped by block, columns = days) with stay bars, maintenance bands and turnaround hatching; **no drag-and-drop** — extend and relocate are dialogs, because both need a reason and the server's alternatives | HO-ADM-002 ADR-FE-016; a drag cannot express "why" |
| **ADR-FE-103** | Signatures are captured by an **in-repo pointer-event canvas** posting stroke points as JSON, with an equal **typed-name + confirmation** alternative always visible; nothing is rendered as an image and no file is produced | HO-BRD-001 ADR-236; a canvas-only signature is a WCAG failure and there is no storage for an image |
| **ADR-FE-104** | **Nothing uploads.** Belongings, diary and incident media are recorded as a **reference** in a dev-only field with an honest inline note (*"Photo storage is not available yet — the item is recorded without a photo"*), exactly as HO-CAT-002 §11.6. Where `boarding.requireBelongingsPhotos` makes the server refuse, the message is surfaced verbatim | HO-BRD-001 OQ-1; inventing an upload endpoint is backend scope and NFR-SEC-5 is unmet |
| **ADR-FE-105** | Every price, total, discount, deposit, balance and charge line is **rendered from the server**; the client never adds, multiplies or prorates money, and never derives availability, capacity, overdue or "not eating" | HO-BRD-001 §8.8/§8.9 own those rules; a second implementation in the browser will disagree |
| **ADR-FE-106** | **There is no payment control anywhere.** The owner sees the amount, the pay-by time and a link to the payment-instructions page. Staff see **Record payment** → HO-BIL-002's dialog (or, until it exists, a link to Billing with the stay reference) → **Confirm stay**, and an explicit *awaiting approval* state. The UI never claims a stay is confirmed until the server says so | SRS A-2, C-5; the confirmation is the server's reading of Billing (HO-BRD-001 ADR-223) |
| **ADR-FE-107** | Actions are rendered **only** from `allowedTransitions[]` and the server's capability flags (`canOverrideGate`, `canRecordCare`, …); a `403` renders `PermissionDeniedState`. No role check is hardcoded | HO-BRD-001 §12.3; the role map is the server's (ADR-233) |
| **ADR-FE-108** | Check-in and check-out are **wizards that submit once**: per-cat steps collect everything and one request creates the whole checklist, so a half-checked-in stay cannot exist; the server's `missing[]` is mapped back onto the offending step and field | HO-BRD-001 §8.11 refuses an incomplete checklist as a unit |

| # | Question | Default |
|---|---|---|
| OQ-1 | How many days does the occupancy board show at once? | 14, paged by week, `from` in the URL |
| OQ-2 | Does the task board group by unit or by cat by default? | By unit (matches FR-BRD-10 "per unit"), with a **My tasks** toggle |
| OQ-3 | Should the owner see every observation, or a daily digest? | Every observation, newest first, grouped by day |
| OQ-4 | Should the portal show the vet spending limit and what is committed? | Yes — limit, used and remaining, as text |
| OQ-5 | Printable check-out summary? | Print stylesheet on `/summary`; no PDF (that is a backend renderer) |

---

## 10. Dependencies

**No new package.** A calendar, scheduler, drag-and-drop, signature-pad, chart, date-picker,
time-zone or image-processing library is a §20 stop. The signature canvas, occupancy grid and
countdown are in-repo (ADR-FE-102, ADR-FE-103).

---

## 11. Specified changes — components and screens

Each component under ~150 lines, with distinct loading / empty / error / permission-denied states.

### 11.1 Booking flow — US-BRD-02, 03, 04, 05, 07 · `/boarding`

| Step | Component | Behaviour |
|---|---|---|
| 1 | ⭐ `StaySearch` | Arrival and departure (branch-local date inputs, in-repo; departure > arrival), number of cats, branch (hidden with one). Shows the branch's check-in and check-out windows and the nights count |
| 2 | ⭐ `UnitTypeResults` | From `GET /api/boarding/availability`: one card per type — name, capacity, `freeUnitCount`, **itemised quote** (`QuoteBreakdown`: basis label *"Weekly rate"*, each rate unit line, additional-cat lines, **discount as its own line**, total) — **US-BRD-03 AC2**. Full: *"No units free for those dates."* with `NearestDatesList` (up to three alternatives, each a one-tap re-search) and **Join the waitlist** — **US-BRD-02 AC3** |
| 3 | `CatStep` | The customer's alive cats, selectable up to the type's capacity; over capacity is blocked client-side **and** the server's `UnitCapacityExceeded` message is shown verbatim if it still happens. Creates/updates the enquiry (`stayId` enters the URL) |
| 4 | ⭐ `VaccinationGatePanel` | Rendered whenever `vaccinationGate.passed === false`: per blocked cat the reason in plain language (*"Mitu's rabies vaccination is due on 14 Oct, during the stay"*), **Book a vaccination** → `bookingLink` (HO-APT-002 `/book`), and *"We've kept your booking — come back and press Resume."* The flow does not lose the step — **US-BRD-04 AC1** |
| 5 | ⭐ `CareProfileForm` (per cat, tabs) | Feeding schedule rows (time, portion, own/house food, description), medications (name, dose, route, times, instructions, from/to), litter preference, **vet-visit authorisation** (explicit yes/no with what it means), **veterinary spending limit** (money input, per cat), emergency contact, notes. Prefilled from the cat profile's medications and behavioural notes with *"from Mitu's profile — edit for this stay"*. Explicit **Save** per cat; a completeness badge per tab — **US-BRD-05 AC1, US-BRD-07 AC1** |
| 6 | ⭐ `ReviewAndReserve` | Dates, unit type, cats, per-cat profile summary, quote, **deposit due and by when**, balance due, and the boarding terms link (HO-ADM-002 policy page). **Reserve** → confirm (UI-3) → `POST …/reserve`. `400 VaccinationGateBlocked` → back to step 4 with the fresh gate; `400 CareProfileIncomplete` → the named cat's tab; `409 UnitNoLongerAvailable` / `CatAlreadyBoarding` → step 2 with the message and refetched availability |
| 7 | `ReservedPanel` | *"Reserved — not yet confirmed."* The deposit amount, `PayByCountdown`, **How to pay** (the `boarding-payment-instructions` content page rendered with `LimitedMarkup`), *"We'll confirm as soon as the clinic records your payment."* and a link to the stay — **US-BRD-06 AC1, AC2** |

`WaitlistJoinDialog` — dates and cat count prefilled; explains *"We'll message you if a unit frees
up — bookings are first come, first served."*; `UnitsStillAvailable` → *"Units have just become
available"* and results refetch.

### 11.2 My stays — `/portal/boarding`

`MyStaysList` — tabs *Upcoming* / *In house* / *Past*; each row: cats, dates, unit type, status as
text, deposit state, balance due. **Enquiries** section lists blocked or unfinished drafts with
**Resume** (deep link back into the flow at the right step) — **US-BRD-04 AC2**. **Waitlist** section
with **Withdraw**. **Book a stay** link.

### 11.3 Stay in the portal — US-BRD-06, 12, 13, 14, 17

| Component | Behaviour |
|---|---|
| `StayOverview` | Status with what happens next; dates and check-in window; unit type; cats with `CatProfileHeader` (compact); care profiles (read-only after check-in, editable before with **Save**); `PayByCountdown` while `Reserved`; deposit state (*Awaiting payment* / *Payment being checked* / *Received*); balance due; **Cancel** with a confirm that states the deposit outcome the server returns |
| ⭐ `StayDiary` | Day cards newest first: observations (portion eaten, water, litter, mood, notes), media entries (ADR-FE-104 placeholder with caption and date), completed-care count. Empty day → *"No entries yet for this day."* — **US-BRD-12 AC1** |
| `IncidentCard` / `IncidentDetail` | Kind, severity as text and colour, what happened, what was done, when. **I've seen this** → `POST …/acknowledge`, after which it reads *"Acknowledged at 21:14."* — **US-BRD-13** |
| ⭐ `VetApprovalDetail` | *"The vet would like to treat Mitu. Estimated cost ৳3,500. Your limit for this stay is ৳2,000, of which ৳1,200 is used."* **Approve** / **Decline** with confirms; after answering, the outcome and time — **US-BRD-14 AC3** |
| `StaySummary` | Per cat: daily logs, weight at check-in and check-out **with the change**, incidents, medications given, vet visits, belongings returned; the charge lines and any invoice links (HO-BIL-002); the signature record (signer and time). Print stylesheet — **US-BRD-17 AC1** |
| `StayCharges` | Charge lines from the server, deposit applied, balance; *"Your invoice"* links; **no payment control** (ADR-FE-106) — **US-BRD-17 AC2** |

### 11.4 Staff booking — `/staff/boarding/stays/new`

`StaffStayBookingForm` — customer search (HO-ACC-002) → cats of that customer → the same
`StaySearch`, `UnitTypeResults` (staff mode, showing per-unit availability and isolation units when
`requiresIsolation`), `CareProfileForm` and `ReviewAndReserve`, plus **Override vaccination** for
administrators (dialog: the blocked cats, a **mandatory reason**, an explicit *"This will be
recorded against you"*) — **US-BRD-04 AC3** — and **Record payment** → §11.6.

### 11.5 Dashboard — US-BRD-19 · `/staff/boarding`

⭐ `BoardingDashboard` — one card per section of `GET /api/staff/boarding/dashboard`, each with its own
empty state, count in the heading, and a link into the relevant screen:

`Occupancy today` (units occupied / active, cats in house, by type) · `Arrivals` (with deposit state
and a **Check in** action) · `Departures` (**Check out**) · **`Cats not eating`** (cat, unit, last two
entries) · **`Overdue tasks`** (grouped by unit, link to the board) · `Unassigned tasks` ·
**`Open incidents`** (severity, acknowledged?) · **`Call owner`** (escalated incidents with a
**Record call** dialog) · **`Unpaid balances`** (reserved awaiting deposit with the pay-by time; in
house / checked out with a balance) · `Refund due` · `Needs attention`. Polls every 30 s with
`generatedOn` shown as *"updated 10:42"*; a newly arrived high-severity incident is announced in a
live region — **US-BRD-19 AC1, AC2** (ADR-FE-101).

### 11.6 Deposit and confirmation — US-BRD-06 — ADR-FE-106

`DepositPanel` (stay workspace and arrivals card):

1. Shows required, accepted and outstanding amounts and the pay-by time.
2. **Record payment** → HO-BIL-002's dialog prefilled with the stay reference and amount. Until that
   handover exists: a link to Billing plus the reference to copy, and an inline note saying so.
3. **Confirm stay** → `POST …/confirm`. `200` → status `Confirmed`, toast *"Confirmed — the owner has
   been notified."* `400 DepositNotYetReceived` → *"Billing shows ৳0 received against this stay."*
   with the outstanding amount. `depositState: PendingApproval` → *"A payment is waiting for a second
   approval in Billing. The reservation won't expire while it is."* and the button becomes **Check
   again** — **US-BRD-06 AC2, AC3**.

No control anywhere takes an amount tendered, a card or a reference **into Boarding**.

### 11.7 ⭐ Task board — US-BRD-09, 10, 11 · `/staff/boarding/tasks`

Tablet-first, one screen, no navigation to complete anything.

| Component | Behaviour |
|---|---|
| `TaskBoardToolbar` | Date (today by default), **My tasks** toggle, unit filter, status filter (Due / Overdue / Done), counts |
| ⭐ `UnitTaskGroup` | One section per unit, expanded by default (OQ-2): unit name, block, the cats with compact `CatProfileHeader` (high-risk badge first), isolation banner where applicable |
| ⭐ `TaskCard` | Time, kind icon **with text**, title (*"Feed ⅓ cup dry — own food"*), cat name, assignee, **overdue chip** (*"25 min late"*); a full-width **Done** button ≥ 44 px. One tap → optimistic `Done`, live-region *"Feeding for Mitu marked done at 09:05."*, **Undo** while in flight. Failure → rollback plus the server's message — **US-BRD-10 AC1** (ADR-FE-100) |
| `SkipTaskDialog` | Secondary action; reason required (*"refused food"*) |
| ⭐ `ObservationSheet` | Per cat, one row: portion eaten (None / ¼ / ½ / ¾ / All as labelled buttons), water, litter output, mood, notes; **Save**. After two `None` entries the card shows *"Not eating — flagged"* from the server — **US-BRD-11** |
| `MediaRecordDialog` | Caption, date, reference (ADR-FE-104) |
| `IncidentQuickButton` | Opens §11.9's form prefilled with the cat and unit |

Polling never interrupts a tap or an open sheet (ADR-FE-101).

### 11.8 Stay workspace — US-BRD-08, 15, 16, 17, 18 · `/staff/boarding/stays/[stayId]`

Tabs: **Overview · Care · Tasks · Diary · Incidents · Isolation · Charges · History**.

| Component | Behaviour |
|---|---|
| `StayHeader` | Cats (`CatProfileHeader`), unit and block, dates and nights, status, deposit state, balance, gate state (with who overrode it and why), **Needs attention** banner with **Clear** |
| ⭐ `CheckInWizard` | Per-cat steps: identity confirmed, **vaccination proof verified**, weight (kg, units always visible — UI-8), arrival health observation, belongings (add rows: kind, description, photo reference note), **owner signature** (`SignaturePad` or typed name). A summary step lists everything; **Check in** submits **once** (ADR-FE-108). `400 CheckInChecklistIncomplete` → each missing item mapped to its cat step and field — **US-BRD-08** |
| ⭐ `SignaturePad` | In-repo canvas (pointer events), **Clear**, and an always-visible *"Type your name instead"* radio path with an explicit confirmation checkbox (ADR-FE-103) |
| ⭐ `CheckOutWizard` | Weight, belongings returned (checkbox per item, note required when not returned), signature; **early check-out** shows the server's recalculated lines *before* submitting (*"10 nights used — the long-stay discount no longer applies"*) and the refund or credit outcome — **US-BRD-16 AC1, AC2** |
| `ExtendDialog` | New departure date → `POST …/extend`. `400 UnitNotFreeForExtension` → the server's `relocationOptions[]` as a list with **Relocate and extend** — **US-BRD-15 AC1, AC2** |
| `RelocateDialog` | Target unit (from the server), effective date, **mandatory reason**; confirm names both units |
| ⭐ `IsolationPanel` | Per cat **Flag suspected infectious** (note + target isolation unit, with the confirm naming the move), **Clear flag** (vet/admin), and the **isolation entry log** with **Record entry** / **Record exit** — **US-BRD-18 AC2**. `NoIsolationUnitAvailable` is shown as the blocking problem it is |
| `ReferralPanel` | **Refer to the clinic** (reason, urgency) → the created referral with its visit link (HO-EMR-002 when built); **vet headroom** (limit, used, remaining, *"the owner has not authorised treatment"* when 0) and **Request approval** (amount, description) — **US-BRD-14** |
| `ChargesPanel` | Charge lines with kinds and amounts, invoiced state, invoice links, deposit applied, balance; **Issue final invoice** (HO-BIL-002) after check-out, with *"the worker will issue it within a minute if you don't"* — **US-BRD-20** |
| `HouseFoodPanel` | What Inventory has issued against this stay (HO-IMP-002 link; empty state until it exists) |

### 11.9 Incident queue — US-BRD-13 · `/staff/boarding/incidents`

`IncidentForm` (kind, severity with an explicit note that **medium and high message the owner
immediately**, description, immediate action, cat, media reference), `IncidentQueue` (open first, by
severity then time; acknowledged state; **escalated** chip), `CallOwnerCard` (*"No acknowledgement
after 30 minutes — call {customer} on {phone}"* with a `tel:` link and **Record call** capturing the
outcome), `ResolveIncidentDialog` (resolution required) — **US-BRD-13 AC2, AC3**.

### 11.10 Occupancy board — US-BRD-02, 15 · `/staff/boarding/board`

⭐ `OccupancyGrid` — CSS grid, rows = units grouped by block, columns = days (14, OQ-1); stay bars
(cats, status colour **and** a text label), maintenance bands (patterned, not colour alone),
turnaround hatching; an empty cell opens staff booking prefilled. Below 768 px → `OccupancyDayList`
(per day, the units and who is in them). Every bar is a link to the stay; keyboard reachable in row
order (ADR-FE-102).

### 11.11 Admin screens — US-BRD-01, 03 · `/staff/admin/boarding/**`

`UnitTable` + `UnitForm` (code, name, type, capacity, block, sort order, active) with the **type's
current nightly rate shown read-only and a link to the rate card** (HO-BRD-001 ADR-221) —
**US-BRD-01 AC1**; `MaintenanceBlockDialog` (dates, reason) surfacing `MaintenanceOverlapsStays` with
the affected stays — **AC2**; `RateCardEditor` per unit type using HO-ADM-002's Current / Scheduled /
Past pattern (never editable, **Schedule a new rate card** only — ADR-FE-018) with nightly, weekly,
monthly, additional-cat rates, tax class and effective date, plus the daycare card; `CareRoutineForm`
(litter, enrichment, observation times per branch).

### 11.12 Daycare — US-BRD-21

`DaycareBookingForm` (cat, date, hourly vs day with both prices shown, expected hours) and
`DaycareDayList` (staff: check in, check out, cancel, no-show, charge shown after check-out).
`DaycareNotOffered` renders as *"Daycare isn't offered at this branch."*

### 11.13 Error mapping — `lib/api/errors.ts`

Append every HO-BRD-001 §10.4 code with plain-language text and the field it belongs to:
`VaccinationGateBlocked`, `CareProfileIncomplete`, `UnitCapacityExceeded`, `UnitNoLongerAvailable`,
`CatAlreadyBoarding`, `NoUnitAvailable`, `DepositNotYetReceived`, `ReservationNotLapsable`,
`CheckInNotOnArrivalDate`, `CheckInChecklistIncomplete`, `CheckOutChecklistIncomplete`,
`InvalidSignature`, `UnitNotFreeForExtension`, `RelocationReasonRequired`, `IsolationUnitRequired`,
`NoIsolationUnitAvailable`, `NotInIsolation`, `MaintenanceOverlapsStays`, `RateCardCannotBeBackdated`,
`UnitTypeNotPriced`, `CareTaskNotPending`, `IncidentStayNotInHouse`, `VetApprovalAlreadyDecided`,
`InvalidMediaReference`, `UnitsStillAvailable`, `AlreadyWaitlisted`, `DaycareNotOffered`,
`DaycareFull`, `StayTooLong`, `StayBeyondHorizon`, `InvalidStayTransition`. The server's message is
preferred when present; the mapped text is the fallback. **No `errorCode` appears in the DOM.**

---

## 12. Mocks — `web/mocks/handlers/boarding/*.ts`

Generated from HO-BRD-001 §12.4. Fixtures: two branches in different zones; units of all four types
with a maintenance block; rate cards with and without a long-stay discount; a 5-night and a 20-night
quote; a full range with three alternatives; a cat blocked by vaccination and the same enquiry
passing after; a two-cat family reservation; a reserved stay whose deposit is due in 70 s (to
exercise the countdown) plus `depositState` in each of the four values; a lapsed reservation; an
in-house stay with tasks due, overdue and done, two `None` observations, diary entries, media without
storage, a medium and a high incident (one escalated), an isolation-flagged cat, a referral with
headroom exceeded and a pending approval; an extension refused with relocation options; an early
check-out recalculation with a credit; a 60-night stay with one cycle invoice; a daycare day at
capacity; every `403` and every error code in §11.13.

---

## 13. Allowed scope

```
web/app/(portal)/boarding/**
web/app/(portal)/portal/boarding/**
web/app/(portal)/layout.tsx                                   ← one nav entry only
web/app/(staff)/staff/boarding/**
web/app/(staff)/staff/admin/boarding/**
web/app/(staff)/staff/admin/layout.tsx                        ← the Boarding nav group only
web/app/(staff)/layout.tsx                                    ← two nav entries only
web/components/boarding/**                                    ← booking, stay, care, tasks, incidents, board, admin, daycare
web/components/signature/**
web/lib/api/{boarding-public,my-boarding,staff-boarding,boarding-care}.ts
web/lib/api/types.ts · web/lib/api/errors.ts                  ← append
web/lib/routing/**                                            ← `/boarding` added to the next-param allow-list only
web/lib/format/**                                             ← §8.4 additions
web/lib/hooks/**                                              ← new hooks only
web/lib/validation/**                                         ← boarding schemas only
web/mocks/handlers/boarding/** · web/mocks/handlers/index.ts
web/tests/components/boarding/**
web/tests/e2e/boarding-*.spec.ts
web/docs/decisions.md                                         ← ADR-FE-099…108
docs/test-cases/TC-HO-BRD-002.md · docs/gates/HO-BRD-002-*.md
```

## 14. Forbidden scope

| Path | Why |
|---|---|
| `src/**`, `tests/**` | HO-BRD-001 owns the server |
| `.ai/**`, `.claude/**`, `docs/**` except the two named paths | Governing docs, agents, inputs |
| Every other HO-CAT-002, HO-ACC-002, HO-ADM-002, HO-NTF-002, HO-APT-002 file beyond the lines in §13 | Their slices |
| `web/package.json`, `tsconfig.json`, `next.config.ts`, `eslint.config.mjs` | No dependency is added (§10) |
| Any payment, card, wallet or "pay now" component | SRS A-2, C-5 (ADR-FE-106) |
| Any upload input, `FormData` file post, image processing or storage client | ADR-FE-104 |
| Invoice, credit-note or refund rendering | HO-BIL-002 |

---

## 15. Component test cases

| ID | Case | Expected |
|---|---|---|
| **A — booking** | | |
| U-A1 | Availability with three types | One card per type with capacity, free count and total |
| U-A2 | 20-night quote with a discount | Basis label, rate-unit lines and a **separate** discount line; no client-side arithmetic — **US-BRD-03 AC2** |
| U-A3 | Full range | Alternatives list and **Join the waitlist**; each alternative re-searches — **US-BRD-02 AC3** |
| U-A4 | Type capacity 2 with 3 cats selected | Blocked with the capacity stated; the server's message shown verbatim when it arrives — **US-BRD-07 AC2** |
| U-A5 | `vaccinationGate.passed = false` | Per-cat reason in plain language and **Book a vaccination** pointing at `bookingLink` — **US-BRD-04 AC1** |
| U-A6 | Return to a blocked enquiry from `/portal/boarding` | Resumes at the review step with dates, cats and profiles intact — **AC2** |
| U-A7 | Care profile: missing spending limit; 7 feedings; medication without a time | Field errors; values preserved (NFR-USA-5) |
| U-A8 | Reserve `409 UnitNoLongerAvailable` | Back to results with the message and refetched availability |
| U-A9 | Reserved panel | Deposit amount, pay-by countdown, **how to pay** content, *"not yet confirmed"*; **no payment control anywhere in the flow** — ADR-FE-106 |
| U-A10 | Pay-by countdown with a server clock 90 s ahead | Counts to the server's time; announces at 24 h, 1 h, expiry |
| **B — portal stay** | | |
| U-B1 | Deposit states Awaiting / PendingApproval / Received | Distinct plain-language text each |
| U-B2 | Diary with observations and a media item | Grouped by day; media shows the honest placeholder — **US-BRD-12 AC1**, ADR-FE-104 |
| U-B3 | Incident detail, unacknowledged then acknowledged | **I've seen this** → acknowledged with time |
| U-B4 | Vet approval | Amount, limit, used, remaining; Approve/Decline with confirms — **US-BRD-14 AC3** |
| U-B5 | Summary | Logs, weight change, incidents, meds, vet visits, belongings, signature record; charges with no payment control — **US-BRD-17** |
| U-B6 | Cancel inside the window | Confirm states the deposit outcome from the server |
| **C — dashboard, board, admin** | | |
| U-C1 | Dashboard with every section populated | All eleven sections with counts and links — **US-BRD-19 AC1** |
| U-C2 | Poll while the *Record call* dialog is open with text typed | Values kept; data behind updated — **AC2**, ADR-FE-101 |
| U-C3 | Occupancy grid with a stay, a maintenance block and turnaround | Distinguishable without colour; keyboard reachable |
| U-C4 | Grid at 360 px | Day list, no horizontal scroll |
| U-C5 | Unit form | Nightly rate read-only with a link to the rate card — **US-BRD-01 AC1** |
| U-C6 | Maintenance overlapping a stay | `MaintenanceOverlapsStays` with the affected dates — **AC2** |
| U-C7 | Rate card editor | Current / Scheduled / Past; no edit control; past effective date refused |
| **D — task board and care** | | |
| U-D1 | **Task card Done** | **One** pointer event completes it; optimistic; live-region announcement; no dialog — **NFR-USA-3, US-BRD-10 AC1** |
| U-D2 | Completion fails with a server error | Card rolls back; message shown; focus preserved |
| U-D3 | Overdue task | *"25 min late"* chip as text — **US-BRD-10 AC2** |
| U-D4 | Poll during an open observation sheet | Sheet values untouched |
| U-D5 | Observation sheet, two consecutive `None` from the server | *"Not eating — flagged"* rendered from the server flag, never computed — **US-BRD-11 AC2**, ADR-FE-105 |
| U-D6 | Task card for a high-risk cat | Handling note visible before the action — UI-7 |
| U-D7 | Keyboard-only completion | Tab to **Done**, Enter completes; announcement made |
| **E — wizards, isolation, changes** | | |
| U-E1 | Check-in wizard with weight and signature missing; server `CheckInChecklistIncomplete` | Errors mapped to the right cat step and field; nothing submitted twice — **US-BRD-08 AC2**, ADR-FE-108 |
| U-E2 | Signature pad | Strokes captured and posted as JSON; **typed-name path** completes the same submission; no image produced — ADR-FE-103 |
| U-E3 | Belongings without a photo reference and the server refusing | The honest note plus the server's message — ADR-FE-104 |
| U-E4 | Extend refused | `relocationOptions[]` listed with **Relocate and extend** — **US-BRD-15 AC2** |
| U-E5 | Relocate without a reason | Blocked; confirm names both units |
| U-E6 | Flag suspected infectious | Confirm names the isolation unit; `NoIsolationUnitAvailable` surfaced as a blocking problem — **US-BRD-18 AC1** |
| U-E7 | Isolation entry log | Entry and exit with staff name and time — **AC2** |
| U-E8 | Early check-out | The server's recalculated lines and the refund/credit outcome shown **before** submit — **US-BRD-16** |
| **X — cross-cutting** | | |
| U-X1 | Every form: server `400` | Mapped to its field; `errorCode` not in the DOM; values preserved |
| U-X2 | Dirty care-profile form + navigate | `UnsavedChangesGuard` confirm |
| U-X3 | Actions with a restricted `allowedTransitions` and a `403` | Only allowed buttons render; `PermissionDeniedState` on `403` — ADR-FE-107 |
| U-X4 | Grep the boarding bundle for payment words (`card`, `cvv`, `pay now`, `gateway`, `token`) | None — ADR-FE-106 |

## 16. End-to-end test cases

MSW first, then the real API; both runs recorded.

| ID | Case | Expected |
|---|---|---|
| E-A1 | **AC-11**: owner books for an overdue cat → blocked → books the vaccination → (real API) vaccination recorded → resumes → reserved | Blocked with the link; resumed without re-entering anything; `Reserved` with deposit instructions — **US-BRD-04** |
| E-A2 | Owner books 20 nights | Weekly/monthly basis with the discount itemised; total matches the review step exactly — **US-BRD-03** |
| E-A3 | Two cats of one household into a family unit; then four cats | One reservation with two profiles; refusal stating the capacity — **US-BRD-07** |
| E-A4 | Full range → join the waitlist → another reservation lapses → worker pass → message in the inbox | Notice with a booking link — **US-BRD-02 AC3** |
| E-B1 | **AC-12 part 1**: reception records the deposit in Billing, then **Confirm** | `Confirmed`; confirmation in the owner's inbox — **US-BRD-06 AC3** |
| E-B2 | Reservation left unpaid past the window (mock 70 s) → pass | Lapsed; unit available again; lapse message — **AC4** |
| E-B3 | Deposit awaiting second approval | *"waiting for a second approval"*; no lapse; confirms after approval |
| E-C1 | **AC-12 part 2**: check-in wizard with belongings and signature | `CheckedIn`; incomplete attempt refused first — **US-BRD-08** |
| E-C2 | **NFR-USA-3**: caretaker completes a feeding task on a tablet viewport | Completed in **two taps or fewer**, measured by the test; timestamp and identity shown — **US-BRD-10 AC1** |
| E-C3 | Task left past its due time | Overdue on the board and in the dashboard's Overdue card — **AC2** |
| E-C4 | Two `None` observations | Cat appears under *Cats not eating* on the dashboard — **US-BRD-11 AC2** |
| E-D1 | High-severity incident recorded | Owner's inbox has it (real API) within the notification pass; unacknowledged → **Call owner** appears after the escalation period — **US-BRD-13 AC2, AC3** |
| E-D2 | Owner acknowledges from the inbox link | Acknowledged state on the staff queue; no escalation |
| E-E1 | **AC-13**: refer a boarded cat; a charge above the limit; owner approves in the portal | Referral linked; approval requested and answered; charges on the stay — **US-BRD-14** |
| E-E2 | Extend into a taken unit → relocate and extend | Relocation recorded with the reason; tasks continue on the new unit — **US-BRD-15** |
| E-E3 | Early check-out of a long stay | Recalculated basis and credit shown; summary and charges produced — **US-BRD-16, US-BRD-17** |
| E-E4 | Flag a cat infectious from the task board | Moved to isolation; isolation entries recorded — **US-BRD-18** |
| E-F1 | **AC-15**: a 60-night stay across a cycle boundary | Cycle invoice visible on Charges; final invoice after check-out covers the remaining nights — **US-BRD-20** |
| E-F2 | Dashboard open for 90 s with an emergency incident arriving | Sections update without resetting an open dialog; the new incident is announced — **US-BRD-19 AC2** |
| E-G1 | Daycare booked, checked in and out | Charge shown; no unit occupied on the board — **US-BRD-21** |
| E-X1 | axe on every route in §8.1, including the booking flow with the gate panel, the task board mid-completion, the signature step (both paths), the occupancy grid and every error state | Zero serious/critical |
| E-X2 | Every route at 360 / 768 / 1024 / 1440 px | No horizontal scroll, no clipped control; the task board is one-handed on a tablet |
| E-X3 | **NFR-PRF-3**: dashboard and task board against the real API with the seeded fixtures | First meaningful content ≤ 1.5 s at p95 over 20 loads |

---

## 17. Implementation order

1. Confirm prerequisites (§5) — else stop.
2. Types and API modules from HO-BRD-001 §12.4; MSW handlers; `lib/format` additions.
3. Booking flow: search → results and quote → cats → **gate panel** → care profiles → review →
   reserved panel.
4. Portal: my stays and enquiries → stay overview and deposit → diary → incident → approval → summary.
5. Staff: stay list and workspace overview → **check-in wizard** and `SignaturePad` → charges panel.
6. **Task board** and observation sheet (the NFR-USA-3 budget is measured here, before anything else
   staff-side is polished).
7. Dashboard → incident queue and **Call owner** → occupancy board → extend/relocate → check-out wizard.
8. Admin: units and maintenance → rate cards → care routine. Then waitlist and daycare.
9. U-series, E-series on MSW, then the E-series against the real API per HO-BRD-001 phase.

```bash
cd web && npm run lint && npm run typecheck && npm run test && npm run test:e2e && npm run build
```

---

## 18. Acceptance criteria

| # | Story | Criterion | Evidence |
|---|---|---|---|
| A1 | BRD-01 | Units and maintenance are manageable; the nightly rate is shown from the type's rate card | U-C5, U-C6 |
| A2 | BRD-02 AC1 | Owners see available types with prices and the total for their dates and cats | U-A1, E-A2 |
| A3 | BRD-02 AC3 | A full range offers nearest dates or the waitlist | U-A3, E-A4 |
| A4 | BRD-03 | Rate basis and discount are itemised, never merged | U-A2, U-C7, E-A2 |
| A5 | BRD-04 AC1 | A blocked cat is explained and a vaccination is bookable inline | U-A5, E-A1 |
| A6 | BRD-04 AC2 | The booking resumes after vaccination with nothing re-entered | U-A6, E-A1 |
| A7 | BRD-04 AC3 | An administrator override demands a reason | U-X3, §11.4 |
| A8 | BRD-05 | Every care-profile element is collected per cat and survives errors | U-A7 |
| A9 | BRD-06 AC1/AC2 | Deposit, pay-by and how to pay are shown; the stay reads *not yet confirmed* | U-A9, U-A10, U-B1 |
| A10 | BRD-06 AC3/AC4 | Staff record the payment in Billing and confirm; an unpaid reservation lapses visibly | E-B1, E-B2, E-B3 |
| A11 | A-2 / C-5 | No payment control, card field or gateway reference exists in this UI | U-X4, U-A9 |
| A12 | BRD-07 | Several cats on one reservation; over capacity refused with the capacity | U-A4, E-A3 |
| A13 | BRD-08 | The check-in checklist is complete or the stay does not move | U-E1, U-E2, E-C1 |
| A14 | BRD-09 / BRD-10 AC1 | The day's tasks appear per unit and complete in **two taps or fewer** with time and identity | U-D1, U-D7, E-C2 |
| A15 | BRD-10 AC2 | Overdue tasks are highlighted on the board and the dashboard | U-D3, E-C3 |
| A16 | BRD-11 | Observations are recorded per cat; "not eating" is surfaced from the server | U-D5, E-C4 |
| A17 | BRD-12 | The diary shows entries by date — *media as references until storage exists (ADR-FE-104)* | U-B2 |
| A18 | BRD-13 | Incidents are recorded, the owner can acknowledge, and unacknowledged high severity raises **Call owner** | U-B3, §11.9, E-D1, E-D2 |
| A19 | BRD-14 | Referral, headroom and owner approval are usable end to end | U-B4, E-E1 |
| A20 | BRD-15 | Extension when free; otherwise relocation with a reason | U-E4, U-E5, E-E2 |
| A21 | BRD-16 | Early check-out shows the recalculated basis and the refund or credit before submitting | U-E8, E-E3 |
| A22 | BRD-17 | The summary and itemised charges are shown (and printable) at check-out | U-B5, E-E3 |
| A23 | BRD-18 | Flagging moves the cat to isolation; entries are logged | U-E6, U-E7, E-E4 |
| A24 | BRD-19 AC1 | The dashboard shows all seven required figures plus escalations and attention | U-C1, E-F2 |
| A25 | BRD-19 AC2 | It refreshes every 30 s without losing unsaved input | U-C2, U-D4, E-F2 |
| A26 | BRD-20 | Cycle and final invoices are visible on the stay | E-F1 |
| A27 | BRD-21 | Daycare is bookable and runnable without occupying a unit | E-G1 |
| A28 | UI-6 / UI-1 | Zero serious/critical axe violations; 360–1440 px; signature and board have non-pointer paths | U-D7, U-E2, U-C3, U-C4, E-X1, E-X2 |
| A29 | NFR-PRF-3 | Dashboard and task board within budget | E-X3 |

**Deferred, named in §4**: payment recording UI and invoice rendering (HO-BIL-002), stock issue
(HO-IMP-002), the consultation screen (HO-EMR-002), offline task list (HO-XCT-002), portal home
(HO-POR-002), reports (HO-RPT-002).

---

## 19. Required gates

| # | Gate | Agent | Focus |
|---|---|---|---|
| 1 | ARCHITECTURE | `architecture-reviewer` | §8 layering; **no new dependency** (signature, grid, countdown in-repo); no client-side price, availability, capacity, overdue or not-eating logic (ADR-FE-105); the scoped edits to HO-ACC-002, HO-ADM-002 and the layouts match §13 |
| 2 | SECURITY | `security-reviewer` | **No payment control or card field** (U-X4); the `/boarding` next-param entry keeps the open-redirect guard; care notes, incident text and observations rendered as text (no HTML); signature payload bounded and numeric; actions only from `allowedTransitions`; another customer's stay, incident or approval renders the server's `404`, never inferred data |
| 3 | CODE_REVIEW | `code-reviewer` | Time only through `lib/format` and `useServerNow`; money only through `formatMoney` as strings; polling never resets forms, wizards or in-flight completions; component size; one submission per wizard |
| 4 | TEST | `test-engineer` | `TC-HO-BRD-002.md` from §15/§16 first; mock and real runs recorded separately; **E-C2 measures taps**, E-X3 measures time |
| 5 | ACCEPTANCE | `acceptance-reviewer` | Every §18 row; A17's storage caveat and A26's HO-BIL-002 dependency recorded, not passed silently |

---

## 20. BLOCKED conditions — stop and report

**20.1 A prerequisite is missing** (Node, HO-CAT-002 scaffold, HO-ACC-002, HO-ADM-002, HO-NTF-002,
HO-APT-002). Do not build a parallel shell, session model, branch picker or cat header.

**20.2 A screen seems to need client-side pricing, availability, capacity or conflict logic.** Ask
the server; do not compute it (ADR-FE-105).

**20.3 A payment step, card field, payment link or "pay deposit online" button is requested.** It
contradicts SRS A-2 and C-5 and HO-BRD-001 ADR-223. Report it; the deposit is recorded by staff in
Billing.

**20.4 The real API disagrees with HO-BRD-001 §12.4.** Report it; do not shim.

**20.5 A screen needs an endpoint HO-BRD-001 does not specify** (for example anything from Billing,
Inventory or the Clinic). Report it; do not invent a mock field or call another area's API by guess.

**20.6 A new dependency seems necessary** (§10) — in particular a signature pad, scheduler or
drag-and-drop library. Raise it.

**20.7 Media or signatures seem to need an upload.** There is no object storage (HO-BRD-001 OQ-1).
Do not add an upload input, `FormData` post or storage client.
