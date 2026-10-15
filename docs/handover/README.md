# Handovers

One executable instruction set per slice. The gates read this document: §6 names the governing
architecture docs, §11/§12 the specified changes, §13 the allowed scope and §14 the forbidden
scope.

A handover with an incomplete §6 is not a licence to review from memory — the ARCHITECTURE gate
reads the `.ai/` docs anyway and records the omission as a finding against the handover.

## One pair per functional area

Each backlog section in [`docs/USER_STORIES.md`](../USER_STORIES.md) gets exactly **two** handovers:
`HO-<AREA>-001` for the backend and `HO-<AREA>-002` for the frontend. A large area is built in phases
*inside* its pair, never split into per-story handovers.

## Delivery order

Sections are ordered by **dependency**, so no area is built on a foundation that does not exist yet.
This deliberately differs from the SRS §10 release phases in one place. Consultation (US-CLN-15) and
pharmacy dispensing (US-PHM-05) must deduct stock from specific batches, so Shop catalogue and
Imports & Inventory come before them. Following the release phases would mean building clinical
stock deduction twice.

| # | Backlog section | Handover pair | Needs first | Status |
|---|---|---|---|---|
| 1 | §1 Accounts and Identity | [`HO-ACC-001`](HO-ACC-001.md) / [`HO-ACC-002`](HO-ACC-002.md) | — | Written |
| 2 | §2 Cat Profiles | [`HO-CAT-001`](HO-CAT-001.md) / [`HO-CAT-002`](HO-CAT-002.md) | Accounts (owner) | Backend implemented; frontend written |
| 3 | §14 Administration and Configuration | [`HO-ADM-001`](HO-ADM-001.md) / [`HO-ADM-002`](HO-ADM-002.md) | Accounts (staff, roles) | Written |
| 4 | §13 Notifications | [`HO-NTF-001`](HO-NTF-001.md) / [`HO-NTF-002`](HO-NTF-002.md) | Accounts (preferences), Administration (quiet hours) | Written. Transports decided — SMTP/MailKit and a config-driven SMS adapter (**ADR-320**); an SMTP and an aggregator **account** are still owner-supplied |
| 5 | §3 Clinic — Appointments | [`HO-APT-001`](HO-APT-001.md) / [`HO-APT-002`](HO-APT-002.md) | Administration (hours, rooms, rosters, services), Notifications | Written. Open appointments — no deposit or payment at booking (owner, 2026-09-22) |
| 6 | §17 (US-XCT-07) **File storage** | [`HO-FIL-001`](HO-FIL-001.md) / [`HO-FIL-002`](HO-FIL-002.md) | Accounts, Administration | Written 2026-09-23. **Resolves C-5** — extracted from HO-EMR-001 §8.10; Google Cloud Storage (ADR-318), ClamAV (ADR-319) |
| 7 | §8 Shop — Catalogue and Products | [`HO-SHP-001`](HO-SHP-001.md) / [`HO-SHP-002`](HO-SHP-002.md) | Administration (tax classes), **File storage** | Written, reconciled. Reads `inventory.v_sellable_stock` (C-1); diet port renamed (C-4) |
| 8 | §9 Imports, Batches and Inventory | [`HO-IMP-001`](HO-IMP-001.md) / [`HO-IMP-002`](HO-IMP-002.md) | Shop (SKUs) | Written. Owns `IStockLedger` (C-2) and the recall trace (C-3) — four areas were reconciled onto it |
| 9 | §4 Clinic — Consultation and Medical Records | [`HO-EMR-001`](HO-EMR-001.md) / [`HO-EMR-002`](HO-EMR-002.md) | Appointments, Inventory, **File storage** | Written, reconciled onto `IStockLedger` (C-2). Its §8.10 file facility was **extracted to HO-FIL-001** (C-5); OQ-6 closed by ADR-319 |
| 10 | §6 Pharmacy and Prescriptions | [`HO-PHM-001`](HO-PHM-001.md) / [`HO-PHM-002`](HO-PHM-002.md) | Consultation, Inventory | Written. Owns `IDietAuthorisationGate` (C-4); reconciled onto `IStockLedger` (C-2, C-3). OQ-16/17 closed 2026-09-23 — Inventory added `AdjustAsync`/`GetBranchBalancesAsync` |
| 11 | §5 Surgery — Spay, Neuter and Procedures | [`HO-SRG-001`](HO-SRG-001.md) / [`HO-SRG-002`](HO-SRG-002.md) | Consultation, Pharmacy (controlled drugs) | Written, reconciled onto `IStockLedger` (C-2). US-SRG-09 now owned by HO-BRD-001 (C-7) |
| 12 | §12 Billing and Payments | [`HO-BIL-001`](HO-BIL-001.md) / [`HO-BIL-002`](HO-BIL-002.md) | Services and prices, visits, surgery | Written. Publishes the charge / payment / credit-note contracts |
| 13 | §10 Cart, Checkout and Orders | [`HO-ORD-001`](HO-ORD-001.md) / [`HO-ORD-002`](HO-ORD-002.md) | Shop, Inventory, Billing, Notifications | Written, reconciled (C-2, C-3, C-4). OQ-19 closed 2026-09-23. US-ORD-11 AC1 not met (courier API, SRS Q-4); US-ORD-17 deferred to HO-BIL/HO-BRD |
| 14 | §7 Boarding — Cat Care Centre | [`HO-BRD-001`](HO-BRD-001.md) / [`HO-BRD-002`](HO-BRD-002.md) | Cats (vaccination), rosters, Inventory (food), Billing (deposits), Notifications, **File storage** | Written. **Owns** the customer stay diary (C-6) and now US-SRG-09 in new §8.21 (C-7) |
| 15 | §11 Subscriptions | [`HO-SUB-001`](HO-SUB-001.md) / [`HO-SUB-002`](HO-SUB-002.md) | Orders | Written. Each cycle places a normal order — no stored-card auto-charge |
| 16 | §15 Customer Portal | [`HO-POR-001`](HO-POR-001.md) / [`HO-POR-002`](HO-POR-002.md) | Everything it summarises | Written. Owns no data; stay diary retired to HO-BRD (C-6), US-POR-03 evidenced there |
| 17 | §16 Reporting | [`HO-RPT-001`](HO-RPT-001.md) / [`HO-RPT-002`](HO-RPT-002.md) | Everything it reports on, **File storage** | Written. Read-only SQL views; the `bytea` export store is withdrawn for HO-FIL-001 (C-5); US-RPT-06 now waits on an SMTP **account**, not a design |
| 18 | §17 Cross-cutting and Technical | [`HO-XCT-001`](HO-XCT-001.md) / [`HO-XCT-002`](HO-XCT-002.md) | — | Written. Imposes classification + eraser duties on every context |

Notes on the table:

- **File storage (#6) is not a backlog section.** It is the shared facility behind US-XCT-07, pulled
  out of `HO-XCT` and ahead of Shop because five areas were blocked on it and one of them had already
  built it privately (README C-5). It is the one place a file is accepted, scanned, stored and served.
- **Prefixes.** §3 and §4 share the `CLN` story prefix, so their handovers are named `APT` and `EMR`.
- **Cross-cutting (§17).** Accessibility, clear errors and safe uploads (US-XCT-03, 04, 07) are applied
  in every handover. `HO-XCT` covers what remains: the offline front desk, backup and restore, and
  retention enforcement.
- **Sprint plan.** Backlog §19's sprint plan still shows Booking as sprint 2. This order puts
  Administration and Notifications first because Booking cannot be built without them.

Keep the **Status** column current. It is the one place that shows where handover writing has
reached.

## ADR numbering

| Range | Owner |
|---|---|
| ADR-001 … ADR-023 | Recorded in `.ai/decisions/adr.md` |
| ADR-024 … ADR-041 | `HO-ACC-001` |
| ADR-042 … ADR-054 | `HO-ADM-001` |
| ADR-055 … ADR-066 | `HO-NTF-001` |
| ADR-067 … ADR-076 | `HO-APT-001` |
| ADR-FE-001 … ADR-FE-012 | `HO-ACC-002` |
| ADR-FE-013 … ADR-FE-018 | `HO-ADM-002` |
| ADR-FE-159 … ADR-FE-163 | `HO-NTF-002` (all used; moved from 019–023, which clashed with HO-ADM-002) |
| ADR-077 … ADR-096 | `HO-SHP-001` (all used) |
| ADR-097 … ADR-116 | `HO-IMP-001` (all used) |
| ADR-117 … ADR-136 | `HO-EMR-001` (117–134 used) |
| ADR-137 … ADR-156 | `HO-PHM-001` (137–154 used) |
| ADR-157 … ADR-176 | `HO-SRG-001` (157–173 used) |
| ADR-177 … ADR-196 | `HO-BIL-001` (177–194 used) |
| ADR-197 … ADR-216 | `HO-ORD-001` (197–214 used) |
| ADR-217 … ADR-236 | `HO-BRD-001` (all used) |
| ADR-237 … ADR-256 | `HO-SUB-001` (237–249 used) |
| ADR-257 … ADR-276 | `HO-POR-001` (257–267 used) |
| ADR-277 … ADR-296 | `HO-RPT-001` (277–292 used) |
| ADR-297 … ADR-316 | `HO-XCT-001` (all used) |
| ADR-317 … ADR-320 | `.ai/decisions/adr.md` — cross-cutting: money (317), file storage (318), malware scanning (319), email/SMS transports (320) |
| ADR-321 … ADR-340 | `HO-FIL-001` (reserved; the platform choices live in ADR-318/319) |
| ADR-341 | `.ai/decisions/adr.md` — cross-cutting: per-area partial registries and module registration for parallel delivery |
| ADR-342 | `.ai/decisions/adr.md` — cross-cutting: the `Use<Area>Module` pipeline-hook convention (one shared host-level prefix, ending in a non-body-reading exception boundary, run once ahead of every area's own middleware). Recorded from `HO-ACC-001`'s own build (its ARCHITECTURE and SECURITY gate fixes), not pre-allocated to any range above — a cross-cutting decision made from an area branch, same footing as ADR-317…320, and awaiting integrator/owner ratification when the next area needs a pipeline hook of its own. |
| ADR-FE-024 … ADR-FE-028 | `HO-APT-002` |
| ADR-FE-029 … ADR-FE-038 | `HO-SHP-002` (all used) |
| ADR-FE-039 … ADR-FE-048 | `HO-IMP-002` (all used) |
| ADR-FE-049 … ADR-FE-058 | `HO-EMR-002` (all used) |
| ADR-FE-059 … ADR-FE-068 | `HO-PHM-002` (all used) |
| ADR-FE-069 … ADR-FE-078 | `HO-SRG-002` (069–076 used) |
| ADR-FE-079 … ADR-FE-088 | `HO-BIL-002` (079–086 used) |
| ADR-FE-089 … ADR-FE-098 | `HO-ORD-002` (all used) |
| ADR-FE-099 … ADR-FE-108 | `HO-BRD-002` (all used) |
| ADR-FE-109 … ADR-FE-118 | `HO-SUB-002` (109–114 used) |
| ADR-FE-119 … ADR-FE-128 | `HO-POR-002` (119–126 used) |
| ADR-FE-129 … ADR-FE-138 | `HO-RPT-002` (129–138 used) |
| ADR-FE-139 … ADR-FE-148 | `HO-XCT-002` (139–148 used) |
| ADR-FE-149 … ADR-FE-158 | `HO-FIL-002` (all used) |
| ADR-FE-170 … ADR-FE-172 | HO-ADM-002-FU1 (170–171 used) |

A range is that handover's to spend. Unused numbers inside a range stay with it for later
amendments; they are not handed to the next area. **ADR-317 … ADR-320** and **ADR-342** are
cross-cutting decisions recorded in `.ai/decisions/adr.md` rather than in any one handover, because
more than one area is bound by each. The next new area starts at **ADR-347** (ADR-344 to ADR-346 are HO-BIL-001 Phase J) / **ADR-FE-173** (ADR-FE-164 was used by the HO-CAT-002 photo follow-up; 170–172 are reserved by HO-ADM-002-FU1).

## Open conflicts between written handovers

All 17 pairs are written. Sections 6–17 were written in parallel, so each author assumed the
contracts of areas that did not exist yet. These were the places where two written handovers
disagreed. **All eight were reconciled on 2026-09-22**: the losing handover carries an Amended row in
its §1 naming the conflict, and the sections it affected were rewritten against the contract that
won. The table is kept as the record of what changed and why.

| # | Conflict | Who disagreed | Resolution applied |
|---|---|---|---|
| **C-1** | Shop's stock copy: **push or pull**. HO-SHP-001 expects `RecordSkuStockLevelCommand` pushed into a Shop-owned copy (its ADR-082 admits this is an exception to the no-projections rule); HO-IMP-001 publishes the view `inventory.v_sellable_stock` to be read instead (ADR-116) | `HO-SHP-001` vs `HO-IMP-001` | ✅ **Inventory.** HO-SHP-001 §8.7 rewritten to join `inventory.v_sellable_stock`; `SkuStockStatusAggregate`, its table and its command dropped; ADR-082 narrowed to the sales-stats copy, so the ADR-012 exception no longer exists for stock. New HO-SHP-001 OQ-21: NFR-PRF-2 now depends on Inventory's view being indexed for this query shape |
| **C-2** | The stock-movement port has **five names**: `IStockLedger` (IMP, owner), `IStockAvailabilityReader` (EMR), `IStockMovementEnlister` (PHM), `IStockReservationService` + `IStockAvailability` (ORD), `IStockIssuer` (SRG) | IMP vs EMR, PHM, ORD, SRG | ✅ **`IStockLedger` (HO-IMP-001).** All four consumers rewritten. Renames only — every consumer had assumed the enlist-without-save shape IMP built. Three real gaps surfaced: HO-PHM-001 OQ-16 (no branch-wide balances for register reconciliation), OQ-17 (no adjustment method for a register correction), HO-ORD-001 OQ-19 (no reallocate / restock / write-off). Each names Inventory as the owner |
| **C-3** | Recall tracing. HO-IMP-001 ADR-104 **deletes** the per-module `IBatchConsumptionQuery` from `.ai/flows/batch-recall-flow.md` in favour of a ledger stamp; HO-ORD-001 still implements `IBatchConsumptionQuery` and HO-PHM-001 implements `IBatchConsumptionSource` | IMP vs ORD, PHM | ✅ **Inventory.** `OrderBatchConsumptionQuery` and `DispensingBatchConsumptionSource` deleted; both slices now stamp `StockReference` + `StockParty` on every movement instead. ⚠️ `.ai/flows/batch-recall-flow.md` still describes the old fan-out and needs updating |
| **C-4** | The prescription-diet port has **four names**: `IDietAuthorisationGate` (PHM, owner), `IDietAuthorisationReader` (SHP), `IDietAuthorisationService` (ORD, SUB) | PHM vs SHP, ORD, SUB | ✅ **`IDietAuthorisationGate` (HO-PHM-001).** SHP, ORD and SUB renamed. SUB's two assumed calls collapse into one — `CheckAsync`'s result already carries `ValidTo`. New HO-SUB-001 OQ-16: the gate does not return `IssuedByStaffId`, which US-SUB-05's clinic notice wanted. SHP's deny-all stub stands (fail-closed). ⚠️ `.ai/flows/prescription-diet-flow.md` still names `IDietAuthorisationService` |
| **C-5** | File storage was needed by SHP (product images), EMR (clinical photos, consent), BRD (belongings, diary, incidents), RPT (`bytea` export store) and XCT (backup coverage). Only HO-EMR-001 built a facility (ADR-125), and its own stop condition forbade a second upload path | five areas | ✅ **Resolved 2026-09-23 — owner chose GCP.** EMR's §8.10 was the right design in the wrong place, so it is **extracted** into [`HO-FIL-001`](HO-FIL-001.md) / [`HO-FIL-002`](HO-FIL-002.md), placed ahead of Shop in the delivery order. Google Cloud Storage via `Google.Cloud.Storage.V1` behind one `IFileStore` port (**ADR-318**); ClamAV before the bucket, fail-closed (**ADR-319**). HO-RPT-001's `bytea` store is withdrawn; HO-EMR-001 ADR-125 is narrowed to its clinical purposes. New HO-FIL-001 OQ-3 (object backup) and OQ-4 (EXIF is **not** stripped) |
| **C-6** | The **boarding stay diary is built twice**: HO-POR-001/002 own `/api/portal/stays/{id}/diary` and `/portal/stays/[stayId]/diary`; HO-BRD-001/002 own `/api/customers/me/boarding/stays/{id}/diary` and `/portal/boarding/[stayId]/diary` | POR vs BRD | ✅ **Boarding.** HO-POR-001 §8.8, ADR-265, `GetStayDiaryQuery` and the `/api/portal/stays/**` route retired; HO-POR-002's page, §11.3, ADR-FE-125 and row E-C1 retired. The portal's stays tile links to HO-BRD-002's `/portal/boarding/[stayId]/diary`, which inherits the accessibility guidance. POR OQ-6 closed |
| **C-7** | **US-SRG-09 (surgery for a boarded cat) is owned by nobody.** HO-SRG-001 §4.2 defers it to HO-BRD-001; HO-BRD-001 §4.2 lists it as HO-SRG-001's | SRG vs BRD | ✅ **Boarding.** New HO-BRD-001 §8.21 takes US-SRG-09, adding **no** new port — the stay lookup, `IBoardingVetSpendingPolicy`, the approval event and `IBoardingBillableSource` already exist for the clinic-referral path. HO-SRG-001's A5/A9 are evidenced there. New OQ-18: whether a boarding treatment authority covers surgical consent (default: no) |
| **C-8** | Money representation: SRS **C-6** says integer minor units, **ADR-054** says `decimal(12,2)`, the `.ai` docs say `numeric(19,4)`. HO-ORD-001 (ADR-198) and HO-BIL-001 (OQ-17) each resolved it independently | three sources | ✅ **Recorded as [ADR-317](../../.ai/decisions/adr.md) — compute in `long` minor units, store `numeric(12,2)`.** ⚠️ SRS constraint C-6 still says *stored* as minor units and now differs from the code. That needs an owner-approved SRS revision with a version bump; it was **not** edited here |

### Still open after reconciliation

All eight conflicts are resolved, and the storage and transport gaps are decided (ADR-318 … ADR-320).
What is left needs an owner **action**, not a document change:

| What | Who | Consequence until then |
|---|---|---|
| **A GCS bucket per environment** + Workload Identity granting the API `roles/storage.objectAdmin` on it | Owner / infra | Uploads work in Development and CI on `LocalFileStore`; nothing can be deployed — HO-FIL-001 §20.1 |
| **A `clamd` deployment** beside the API (Compose is written; GCP is not) | Infra | Uploads refuse with `FileScanUnavailable` outside Development — fail-closed by design (ADR-319) |
| **An SMTP account** | Owner | Email notifications and US-RPT-06's scheduled reports deliver nothing; the adapter is built (ADR-320) |
| **An SMS aggregator account** with a registered sender ID (a commercial decision, not an architectural one) | Owner | SMS delivers nothing; the config-driven adapter is built (ADR-320) |
| **Object backup / disaster recovery** — HO-XCT-001's backup story covers PostgreSQL only | Owner | Files are not in any backup. HO-FIL-001 OQ-3 recommends a dual-region bucket |
| **EXIF is not stripped from uploaded photos** — a phone photo can carry the owner's home coordinates, and the portal serves it back | Owner + architect | HO-FIL-001 OQ-4. Recommended, deliberately not built without a decision on clinical photos |

Two `.ai/flows` documents were rewritten to match what was actually built:
`batch-recall-flow.md` (the per-module fan-out is gone) and `prescription-diet-flow.md` (the port is
`IDietAuthorisationGate`).

**SRS constraint C-6** was amended to match ADR-317 in SRS v2.4.

## Operational notes (gate fix 2026-09-26, HO-ADM-001)

*Integrator-authorised 2026-09-26: the integrator asked HO-ADM-001's gate-fix round to record these
deployment notes here, where every later handover's operator reads them.*

- **`ex_leave_request_staff_no_overlap` needs `CREATE EXTENSION btree_gist`.** HO-ADM-001's
  `AddAdministration` migration runs `CREATE EXTENSION IF NOT EXISTS btree_gist` in its own `Up()`
  to support a GiST exclusion constraint on `admin.leave_request`. `btree_gist` is a
  PostgreSQL-trusted extension on 13+ (no superuser required to install it), but the migration
  role still needs `CREATE` on the target database — confirm the deployment role has it before this
  migration runs anywhere outside a developer's own worktree database.
- **Always `dotnet ef database update`, never `EnsureCreated`, once this migration exists.** EF Core
  does not model exclusion constraints, so `EnsureCreated` (or any tool that infers schema from the
  model rather than replaying migrations) creates every table `AddAdministration` describes but
  silently omits both the extension and the constraint — the leave-overlap race this constraint
  exists to close would then go unguarded with no error.
