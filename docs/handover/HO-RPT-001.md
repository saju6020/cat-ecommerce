# HO-RPT-001 — Reporting (backend)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-RPT-001** |
| Title | Reporting — backend slice |
| Date | 2026-09-22 |
| Author | Claude Opus 5 |
| Backlog | [`docs/USER_STORIES.md`](../USER_STORIES.md) v2.4 §16 — **US-RPT-01 … US-RPT-06** |
| SRS | [`docs/SRS.md`](../SRS.md) v2.3 — **§8.1, §8.2, §8.3, FR-RPT-1, FR-RPT-2, FR-RPT-3**; FR-IMP-5 (landed cost for margin), FR-IMP-10, FR-IMP-12, FR-BIL-7, FR-BIL-13, FR-BRD-20, FR-ORD-8 (the four launch payment methods), **NFR-PRF-3, NFR-PRF-7**, NFR-SEC-2, NFR-SEC-8, NFR-SCL-1, UI-4, C-6, C-7, C-9, A-1, A-2, Appendix D ("Reports" row) |
| Pairs with | [`HO-RPT-002`](HO-RPT-002.md) — the frontend. **§12.4 of this document is its contract** |
| Builds on | [`HO-ACC-001`](HO-ACC-001.md) (staff, roles, `ActionRoles`, audit), [`HO-ADM-001`](HO-ADM-001.md) (branches, active branch, time model, settings, services, tax classes, CSV guard), [`HO-NTF-001`](HO-NTF-001.md) (outbox, templates, guards, senders), [`HO-CAT-001`](HO-CAT-001.md) (cats, vaccinations, QuestPDF), [`HO-APT-001`](HO-APT-001.md) (appointments, queue). **Reads from handovers not yet written** — HO-SHP-001, HO-IMP-001, HO-EMR-001, HO-PHM-001, HO-SRG-001, HO-BIL-001, HO-ORD-001, HO-BRD-001, HO-SUB-001. §8.4 states the data contract assumed from each |
| Delivery order | Section 16 of 17 in [`README.md`](README.md#delivery-order) — "Everything it reports on" |
| Approved design | None. The `architect` has not produced a design for this slice. The approach below — read-only SQL views — follows `.ai/read-side-pattern.md` §5 and ADR-012. Architect review is OQ-1 |
| Baseline commit | `4007225` |
| Amended | **2026-09-23 — README C-5 (file storage).** The `bytea` export store is **withdrawn**: generated report files go through [`HO-FIL-001`](HO-FIL-001.md) under the `ReportExport` purpose, with 7-day retention expressed as `RetainUntil`. ADR-289 amended, OQ-12 closed. Separately, **ADR-320** settles email (SMTP via MailKit), so US-RPT-06 is blocked on an SMTP **account**, not on a missing design. <br><br>**2026-10-13 — build (owner scope decision: build every phase whose sources are merged).** **Delivered:** phases A (framework), B (R1–R4), C (R5–R8), D (R9–R17), E (the dashboard, 18 tiles), F (R18–R20), G (schedules, runs, the HO-NTF-001 attachment amendment) and H (R27 only). **Not delivered yet — never faked, listed in [`docs/backlog/RPT.md`](../backlog/RPT.md):** R21 and the subscription part of R22 (HO-SUB-001); the cross-line part of R22 (needs the boarding line); R23 (HO-ORD-001 has no cart reminders — owner decision 10 there); R24–R26 and the three `boarding.*` tiles (HO-BRD-001); the seven-day purge pass, `PurgeReportRunFileCommand` (C8) and I-G7 (HO-XCT-001); visit, dispense and surgery revenue (HO-BIL-001 Phase J — the R9 and R12 notes say so); visit treatments in the stock ledger (HO-EMR-001 Phase F). **Adaptations to what shipped:** (1) `InvoiceLineKind` does not exist — `BusinessLineRules` classifies HO-BIL-001's stored `business_line` and `item_kind` (unknown → `Other`); no deposit, voucher or store-credit tender exists. (2) R13 is derived (Billing persists no Matched/Mismatch/Resolved status). (3) No `rv_reorder_rule`: R7 reuses HO-IMP-001's own reorder calculation. (4) Landed cost is read from `consignment_landed_cost_line`; no purchase-cost fallback. (5) Billing keeps issued invoices on the original customer id, so `rv_customer_activity` resolves a merged customer to the survivor. (6) Views added beyond §8.4: `rv_branch`, `rv_staff_branch`, `rv_credit_note`, `rv_refund`, `rv_order_return`. (7) Migrations: `AddReportingSchema`, `AddClinicReportViews`, `AddStockReportViews`, `AddFinancialReportViews`, `AddDashboardReportViews` (new), `AddGrowthReportViews` (only `rv_customer_activity`), `AddNotificationAttachments`, `AddReportSchedules` (two tables; no `report_file`); `AddOperationsReportViews` not created. (8) Report files go through HO-FIL-001 (`ReportExport`, `RetainUntil`) — no `IReportFileStore`; the notification's `AttachmentId` is the run id and `ReportRunAttachmentSource` resolves run → file. (9) After downtime the one run is for the latest due slot. (10) `run-now` carries `report-run-now` only (one named limiter per endpoint). (11) The Accountant cannot open Sales reports (§12.3), so cannot receive R10 on a schedule. **HO-NTF-001 amendment built (§11.7):** `NotificationSubjectKind.ScheduledReport` (10), `NotificationAttachmentKind.ReportRun` (1), `AttachmentKind`/`AttachmentId` on the request, the notification and their create DTOs, `INotificationAttachmentSource`/`INotificationAttachmentResolver`, `OutgoingMessage.Attachments` and `OutgoingAttachment`, the Expand and Deliver handler lines, `INotificationLinkBuilder.ReportLink`, the `report.scheduled-delivery` template, `ScheduledReportGuard`, and `DevelopmentChannelSender` logging a file name and size only; migration `AddNotificationAttachments`. **ADR-277 … ADR-292** are recorded in `.ai/decisions/adr.md`. <br><br>**2026-10-13 — gate fix round (ARCHITECTURE 94ac1894 FAIL, SECURITY 1f2eaf4e FAIL, CODE_REVIEW 8e52b7c1 PASS; owner decisions the same day).** (12) **Clinical-access log (SECURITY HIGH, owner "Log access"):** viewing, CSV/PDF export and each recipient of a scheduled email of a Clinical-category report (R3) write a `RecordClinicalRecordAccessCommand` row (`ClinicalRecordKind.VisitList`, the report path, fail closed with a bare 500; a scheduled run writes one row per recipient before anything is stored and fails the run if a write fails). (13) **Accountant holds `Reports.Sales.View` (owner "Yes, allow Sales")**: ActionRoles row changed, recorded in HO-ACC-001 Amended (65); I-G6 re-proved with an Accountant recipient of R10. (14) **Per-account limits (owner "Add a per-user limit")**: the `reports` policy takes a per-account permit (`Reporting:MaxConcurrentPerAccount`, default 2) and then the host-wide permit; `report-run-now` is 5 per 10 minutes per ACCOUNT, one in flight, then host-wide; the dashboard has its own per-account limit (`report-dashboard`). This supersedes §12.3's "per IP" for run-now. (15) **C5 changes one aggregate (ARCHITECTURE HIGH 2):** `ScheduledReportRunner` stores the file through `IFileStore` and registers it with HO-FIL-001's `RegisterStoredFileCommand` as its own step (as `FilesController` does) and sends C5 with the `FileId`; C5 creates the run and enlists the emails only. A duplicate slot, a refused command or a failed save is followed by a separate `DeleteStoredFileCommand` step; a save failure clears the tracked entities. A file orphaned by a crash between the steps carries `RetainUntil` and is swept by HO-XCT-001 (backlog 43). ADR-277 and ADR-289 are not widened: Reporting is a producer that calls FIL's published port and command, one aggregate per command. (16) **Read side projects (ARCHITECTURE HIGH 1):** every schedule read selects columns before materialising; the eligible-recipients read filters by role and branch in SQL and is bounded at 200; the delivery guard projects. (17) **Recipient branch rights** (SECURITY MEDIUM, CODE_REVIEW): `ReportAccessPolicy.CanReceive` is used when a schedule is saved, in the detail read and by `ScheduledReportGuard` at send time: a cross-branch role (Administrator, Accountant) for an all-branches schedule, a current assignment for a branch schedule. (18) **Merge chain** (CODE_REVIEW MEDIUM): `rv_customer_activity` version 2 follows the whole chain A→B→C (recursive, bounded at 20 hops); R15 follows it in code. (19) **R7 reads views** (ARCHITECTURE MEDIUM): new views `rv_stock_position`, `rv_reorder_policy`, `rv_on_order`; the usability rule and the suggestion remain HO-IMP-001's own pure `StockUsability` and `ReorderSuggestionCalculator`. No change to HO-IMP-001 was needed. (20) Migration `AddReorderViewsAndCustomerMergeChain` (views in `report` only). (21) The boarding figure of `revenue.today-by-line` is absent, not 0, until boarding revenue exists. (22) Reporting saves use `InMemoryPublishOnly` (the audit write C1 keeps `DoNotPublish`). (23) **Files outside §13's literal list, recorded** (none is §14): `Api/Background/ScheduledReportRunner.cs` (the §8.9 steps 2–7, shared by the worker and run-now), `Domain/DomainServices/IScheduledReportRunStore.cs` (replaces `IReportFileStore`), `Shared/Common/Enums/ReportParameterType.cs`, `Api/appsettings.Testing.json` (the worker is off in Testing, §10.2), and in HO-NTF-001 `Infrastructure/Notifications/EfNotificationOutbox.cs` (see HO-NTF-001 Amended (76)). (24) Lows: money rounds half away from zero (the invoice rule), failed-run and parameter-check dates are branch-local, the file `RetainUntil` is the branch-local today + the retention days, the module comment is corrected, and `ReportSchedulePeriods.IsCompatible` has a direct test. The gates' proposed rows are numbered 28–52 in `docs/backlog/RPT.md`. <br><br>**2026-10-14 — HO-RPT-001-FU1 (variance direction; owner decision "Over / Short"; recorded by the integrator at merge).** The §8 definitions of **R13** `payment-reconciliation` and **R14** `daily-cash-up` were edited in place by [`HO-RPT-001-FU1`](HO-RPT-001-FU1.md): each now lists a **variance direction** column right after `variance`, and R14's notes define it (`Over` when counted exceeds expected, `Short` when lower, `Matched` when equal, null when not counted), computed on the server (`VarianceDirection`). The prior text of both rows is at `5a187b6b`. The column contract is CONTRACTS row 41. |
| Platform | `Platform.Infrastructure.Core` **1.0.34** — do not downgrade |

---

## 2. Objective

Give every staff role **the numbers for its job**, and give the owner **the numbers for the business**, without
letting reporting become a second place where business data lives or changes.

Reporting is the one area that reads from every other one: appointments, surgeries, cats, stock, invoices,
payments, orders, stays and subscriptions. The risk is that it turns into a tangle of cross-module joins, or a
copy of other modules' data that slowly drifts from the source. This slice sets four rules to prevent that:

1. **Reports read, and never write to another module.** Every report and dashboard figure comes from
   **read-only SQL views** in a `report` schema. Reporting's migrations create the views over the source
   modules' tables. Every report query runs inside a PostgreSQL **`READ ONLY` transaction**, so an accidental
   write fails in the database, not in code review (ADR-278, ADR-279). Reporting writes to exactly two places
   outside its own schema, both through contracts that already exist: the audit trail (HO-ACC-001's
   `AuditEntryAggregate`, for export records) and HO-NTF-001's outbox (scheduled report emails).
2. **The views are the published contract.** When a source module renames a column, only the view's SQL
   changes. The view's columns, the report sources and the API stay the same. Where a module is not built
   yet, §8.4 states the columns this slice assumes it will expose, and names the owning handover.
3. **One catalogue, one pipeline.** Each of the 27 reports is a *definition*: key, category, access action,
   parameters and typed columns. It is served by one generic query endpoint, one CSV writer and one PDF
   renderer. The date range and branch filters that FR-RPT-1 requires are part of the pipeline, so no report
   can be missing them (ADR-280).
4. **Money is exact and honest.** Revenue is net of tax and discounts and recognised on invoice issue. Margin
   is shown **only** where every contributing batch has a landed cost, and each margin figure states its
   coverage. No estimate fills the gap (ADR-283, ADR-284). **There is no payment gateway** (SRS §1.2, Phase 5):
   the payment reports cover exactly **cash on delivery, cash in store, card in store and bank transfer**.

§17 builds the slice in **eight phases**. Each phase compiles, migrates and passes its own tests before the next
starts. A phase whose source module is not merged is blocked (§20.1), never stubbed.

---

## 3. In scope

### 3.1 Stories

| Story | Pri | Pts | Capability delivered here | Phase |
|---|---|---|---|---|
| US-RPT-01 | M | 8 | **AC2 first**: the report framework — catalogue, date range and branch filters, on-screen results, CSV and PDF export, export audit. **AC1**: daily clinic schedule, surgery register, vaccination due list, queue performance (queue state) | A, B |
| US-RPT-02 | M | 5 | Stock on hand and movement by SKU / batch / location with valuation; near-expiry and expired with value at risk; low stock with reorder suggestions. Also the §8.1 import consignment report | C |
| US-RPT-03 | M | 8 | Revenue by business line (clinic / shop / boarding); shop revenue by category and by country of origin; margin beside revenue where landed cost is known. Also the §8.2 revenue-by-service and payment reports (reconciliation, daily cash-up, receivables, refunds and credit notes, tax summary) | D |
| US-RPT-05 | M | 8 | Role home dashboard: today's key figures for the caller's roles at the active branch, each figure gated server-side by an action the caller holds | E |
| US-RPT-04 | S | 8 | New vs returning customers, lifetime value across the three lines, cohort retention, subscription health, cross-line conversion. Also the §8.3 cart abandonment report | F |
| US-RPT-06 | S | 5 | Report schedules (administrators only, FR-RPT-2): daily, weekly or monthly in branch-local time; the export emailed to staff recipients as an attachment, exactly once per scheduled time | G |
| *(§8.1 trace)* | — | — | The remaining operational reports: boarding occupancy, boarding care compliance, incident report, order fulfilment | H |

**Why the reports not named in any AC are in scope.** Each story traces a whole SRS section (§8.1, §8.2,
§8.3), and FR-RPT-1 says *every* report is filterable and exportable. The framework makes each extra report one
definition, one view and one source, and each one is built in a later phase than the ACs it does not serve.
Subsidised-programme reporting (FR-SRG-11) is **not** in §8 and is not built (§4.2).

### 3.2 Decisions already settled upstream — do not re-litigate

| Decision | Where |
|---|---|
| In-memory CQRS, one host, `IInMemoryDispatcher` only; never `202` | ADR-010, 013, 014, 019, 020 |
| PostgreSQL / EF Core, **one** `DbContext`, `xmin` → `409` | ADR-011, ADR-016 |
| **No projections and no read database**: queries read the tables the write side committed | ADR-012 |
| A report may join across contexts; **prefer a database view created by a migration**, mapped with `ToView` | `.ai/read-side-pattern.md` §5 |
| `[RequireAction]` + the one `ActionRoles` table; every endpoint declares its access; fail closed; denials audited | HO-ACC-001 ADR-036, HO-ADM-001 ADR-043 |
| Active branch from `X-Branch-Id`, validated against the staff member's assignment | HO-ADM-001 ADR-046 |
| Branch-local dates and times via the branch's IANA zone; every instant is `timestamptz` | HO-ADM-001 ADR-045 |
| Money is `decimal(12,2)` + ISO-4217 code, sent as a **string**; no floating point | HO-ADM-001 ADR-054 |
| Settings through `IBusinessSettings`, registry in `Shared` | HO-ADM-001 ADR-050 |
| CSV export: cells starting with `= + - @` are prefixed with `'`; exports are audited; 413 over the cap | HO-ADM-001 ADR-051 |
| Messages only through `INotificationOutbox.Enqueue`, in the command handler, before the save; never from an event handler | HO-NTF-001 ADR-056, ADR-057 |
| No SMS or email provider yet; outside Development only in-app is delivered | HO-NTF-001 OQ-1 |
| PDF rendering with QuestPDF in Infrastructure (licence to be confirmed) | HO-CAT-001 §12.2, §20.6 |
| No payment gateway; payments are COD, cash in store, card in store (terminal slip), bank transfer | SRS v1.1 FR-ORD-8, A-2 |

---

## 4. Out of scope

### 4.1 Frontend — all of it

Screens are [`HO-RPT-002`](HO-RPT-002.md). Criteria phrased as UI are met here at the contract level:

| Story AC (UI phrasing) | Server half — delivered here |
|---|---|
| US-RPT-01 "available" / "viewable on screen" | `GET /api/staff/reports/{key}` returns typed columns, rows, totals and notes |
| US-RPT-01 "exportable to CSV and PDF" | `…/export.csv` and `…/export.pdf` for every catalogue entry |
| US-RPT-05 "the home dashboard shows" | `GET /api/staff/dashboard` returns the tiles for the caller's roles, already filtered |
| US-RPT-06 "given a report and a schedule" | `…/admin/report-schedules` CRUD, a next-run preview and run history |

### 4.2 Deferred or owned elsewhere

| Capability | Where it lands | Why not here |
|---|---|---|
| Operational screens that already show live figures: the clinic queue (HO-APT-001), the boarding dashboard FR-BRD-20 (HO-BRD-001), the reconciliation workflow FR-BIL-7 and cash-up entry FR-BIL-13 (HO-BIL-001) | Their owners | Reporting **reports on** them and does not rebuild them. The queue report is a per-day summary, not the live queue; the cash-up report reads what Billing recorded |
| Subsidised spay/neuter programme reporting (FR-SRG-11, SRS Q-15) | HO-SRG-001 | Not in SRS §8 |
| Accounting export (FR-BIL-10) | HO-BIL-001 | An integration feed, not a report |
| Gateway settlement reconciliation | SRS **Phase 5** | No gateway exists (SRS §1.2). No report column or method value anticipates one |
| Retention enforcement over report files beyond this slice's own purge | HO-XCT-001 | The purge of Reporting's own generated files is here (§8.10) |
| Offline caching of dashboards (NFR-AVL-3) | HO-XCT | NFR-AVL-3 names the day list and task list only |

### 4.3 Not buildable yet — named gaps

| Gap | Consequence |
|---|---|
| **No email provider** (HO-NTF-001 OQ-1) | Scheduled reports are generated, stored and enqueued, but **outside Development nothing reaches an inbox**: staff have no in-app copy (HO-NTF-001 R6), so the notification ends `Undeliverable`. The run history shows the generated file. A21 carries this caveat |
| **Source modules not yet written** (§8.4) | Each view is written against the owning handover's actual tables **at build time**. The view columns in §8.4 are the contract. If a source cannot supply a contracted column, stop (§20.2) |
| Base currency and tax regime (SRS Q-5) | Reports group by `currency_code` and never sum across currencies (§8.6) |
| Object storage (SRS §2.4) | Generated files are stored as `bytea`, capped (ADR-289, OQ-12) |
| Bengali-script names in PDFs | Depends on the fonts HO-CAT-001's renderer embeds (OQ-11, §20.6) |

### 4.4 Explicitly forbidden shapes

No `IBusMessageDispatcher`, MassTransit, a second host, a `CommandService` layer, `202 Accepted`, `[Authorize]`.
Specific to this slice:

- **No write to another module's table.** No `IAggregateRootRepository<T>` of another context used to mutate, no
  `ExecuteUpdate`/`ExecuteDelete`, no raw `INSERT`/`UPDATE`/`DELETE` against a non-`report` schema. The only
  exceptions are audit entries and notification requests, through their existing ports (ADR-277).
- **No projection, no domain-event subscription feeding a reporting table, no copy of source rows** into
  `report.*` (ADR-278).
- **No DDL on another context's schema**: no index, column or trigger on `clinic.*`, `account.*`, `admin.*`,
  `notify.*` or any later schema. The one exception is the HO-NTF-001 amendment (§11.7). A missing index is a
  stop (§20.4).
- **No second `DbContext`** (ADR-011). View row types are keyless entities in the one context.
- **No report computed in the browser** and no endpoint that returns raw table rows for the client to aggregate.
- **No free-text email address as a recipient.** Recipients are staff accounts (ADR-288).
- **No gateway payment method** in any enumeration, view or column (SRS Phase 5).
- **No floating point** in any view row, ViewModel or calculation (C-6, HO-ADM-001 ADR-054).

---

## 5. Prerequisites

| Required | State |
|---|---|
| HO-CAT-001 merged — cats, vaccinations, QuestPDF | ✅ at `4007225` |
| **HO-ACC-001, HO-ADM-001, HO-NTF-001, HO-APT-001 merged** | ❌ **not implemented at baseline** — §20.1 |
| **HO-SHP-001, HO-IMP-001, HO-EMR-001, HO-PHM-001, HO-SRG-001, HO-BIL-001, HO-ORD-001, HO-BRD-001, HO-SUB-001 written and merged** | ❌ **not written at baseline.** Each gates the phases that read it (§17). Delivery order puts Reporting 16th, after all of them |
| **Amended rows appended to HO-NTF-001, HO-ADM-001 and HO-ACC-001** recording the changes in §11.7 and §10 | ❌ **not appended** — those files are being edited in parallel and are forbidden scope for the author of this handover. The orchestrator appends them before Phase A (OQ-15) |
| QuestPDF licence confirmed (HO-CAT-001 §20.6) | ⚠️ inherited — §20.6 |
| PostgreSQL 17.5 `catecommerce_dev`, `dotnet-ef` 9.0.0 | ✅ |
| New NuGet packages | **None.** QuestPDF is already referenced by Infrastructure; `System.Threading.RateLimiting` is in the shared framework (HO-ACC-001 F18) |

The shared-database rules of HO-CAT-001 §16.1, HO-ACC-001 §16.1 and HO-APT-001 §16.1 apply. **Reports aggregate,
so they are sensitive to other tests' data**. §16.1 adds the rule that every assertion is scoped to a branch the
test created.

---

## 6. Governing architecture documents

| Document | What you need from it |
|---|---|
| [`.ai/architecture.md`](../../.ai/architecture.md) | Layer responsibilities and reference direction: `Read` references `Infrastructure` (for the `DbContext` and the read scope) and `Domain` (for the pure calculators it calls); `Application` and `Read` never reference each other. That is why the worker in `Api` orchestrates a scheduled run: query in `Read`, then a command in `Application` (§8.9) |
| [`.ai/cqrs.md`](../../.ai/cqrs.md) | Queries return data and never write; commands mutate one aggregate. Viewing a report is a query. Recording an export audit and recording a scheduled run are commands |
| [`.ai/bounded-contexts.md`](../../.ai/bounded-contexts.md) | Rule 1 (one aggregate per command): the scheduled-run sequence uses two commands, not one two-aggregate command (§8.9). Rule 2 (reference by id). Rule 3: runs and files are their own roots or records, never collections on a schedule. The "Recall … Read layer" row sets the precedent that cross-context tracing is a read. **`Reporting` is not listed there** — OQ-1, ADR-277 |
| [`.ai/namespace-conventions.md`](../../.ai/namespace-conventions.md) | `Reporting` is a new `[Feature]` segment in every namespace in §11–§12. Do not invent others |
| [`.ai/decisions/adr.md`](../../.ai/decisions/adr.md) | **ADR-009 … ADR-023 are binding.** ADR-011 (one `DbContext`), **ADR-012** (no projections — the reason for views), ADR-014, ADR-015, ADR-020 and ADR-021 matter most here |
| [`.ai/read-side-pattern.md`](../../.ai/read-side-pattern.md) | **§5 is the design basis of this slice**: reading across contexts is allowed, writing is not; heavy reports use a view created by a migration and mapped with `ToView`, never migrated from the read side. Rules 1–7: project, `AsNoTracking`, paginate, a query never writes, index what you filter, return `QueryResponse<T>` |
| [`.ai/command-handler-pattern.md`](../../.ai/command-handler-pattern.md) | §3 handler shape for the schedule and run commands; §6 rule placement: schedule invariants go in the aggregate, recipient permission checks in the handler |
| [`.ai/persistence-ef-core.md`](../../.ai/persistence-ef-core.md) | §2 the repository (already implemented — do not re-copy); §6 `xmin`; §7 indexes in configuration; §8 column types. Keyless `ToView` entities are **not** migrated by EF; their SQL lives in Reporting's migrations |
| [`.ai/domain-events-and-outbox.md`](../../.ai/domain-events-and-outbox.md) | §4: email leaves the process, so it goes through the outbox. The run and its notification requests commit in one transaction (HO-NTF-001 F23). §5: Reporting subscribes to **no** domain events, because nothing here must happen "as part of" another context's command |
| [`.ai/enumeration-pattern.md`](../../.ai/enumeration-pattern.md) | The enumerations in §10.1, derived from `Shared`'s `Enumeration` (ADR-022), persisted by `Value` |
| [`.ai/di-registration-guide.md`](../../.ai/di-registration-guide.md) | §3 validators as `IValidator<T>`; §4 `ValidateScopes`/`ValidateOnBuild` stay on; the worker creates a scope per unit of work, as the outbox dispatcher does |
| [`.ai/host-composition.md`](../../.ai/host-composition.md) | `Program.cs` shape; §3 lifetimes; §5 the never-register list; `AddHostedService<ReportingWorker>()` |
| [`.ai/feature-creation-checklist.md`](../../.ai/feature-creation-checklist.md) | File order within each phase |
| [`HO-ADM-001`](HO-ADM-001.md) | §8.2 branch-scoping rule; §8.3 active branch; §8.5 and ADR-045 the time model; §8.7 settings; §12.3 `ActionRoles`; ADR-051 the CSV guard |
| [`HO-NTF-001`](HO-NTF-001.md) | §8.2 catalogue rules; §8.4 producer rules; §8.5 staff recipients; §8.7 guards; §8.9 sender contract; §11.4 C4 and C5 — **amended by §11.7 here** |
| [`HO-APT-001`](HO-APT-001.md) | §8.2–§8.3 appointment fields and statuses; §8.7 queue ordering (holds never count as appointments) |

---

## 7. Verified platform facts

HO-CAT-001 F1–F10, HO-ACC-001 F11–F18, HO-ADM-001 F19–F22, HO-NTF-001 F23–F27 and HO-APT-001 F28–F30 apply. The
facts below are prefixed `F-RPT-` so they cannot collide with numbers other handovers are assigning in
parallel. **None of F-RPT-1…6 has been observed on this repository's database**, because no module this slice
reads is implemented at `4007225`. They are documented PostgreSQL / EF Core behaviour. **Phase A step 1 is to
observe each one** in a rolled-back transaction or a scratch test, and to record the result in the test sheet.
If any fails to hold, stop (§20.3).

| # | Fact (to observe in Phase A) | Consequence |
|---|---|---|
| F-RPT-1 | Inside `BEGIN; SET TRANSACTION READ ONLY;` any `INSERT`, `UPDATE`, `DELETE` or DDL fails with SQLSTATE **`25006` `read_only_sql_transaction`**, including against an auto-updatable view | `ReportingReadScope` makes writes impossible for report code, whatever that code does (ADR-279). I-A3 proves it |
| F-RPT-2 | `SET LOCAL statement_timeout = '<n>s'` applies only to the current transaction; a statement over it fails with SQLSTATE **`57014`** | Runaway reports are cut off without affecting other requests. Mapped to `400 ReportTimedOut` (§12.5) |
| F-RPT-3 | EF Core 9 entities configured with `HasNoKey().ToView("rv_x", "report")` produce **no** `CreateTable` in generated migrations and cannot be added or updated through the context | Views are created by hand-written SQL in Reporting's migrations. A generated migration that tries to create a `report.rv_*` **table** means a configuration is wrong: stop and fix it |
| F-RPT-4 | PostgreSQL records view dependencies. `ALTER TABLE … DROP COLUMN` on a column a view uses fails with *"cannot drop column … because other objects depend on it"*; `RENAME COLUMN` is followed automatically by the view | A later source migration that drops a reported column fails loudly rather than silently breaking a report. The owning area must replace the view in the same migration (§20.2) |
| F-RPT-5 | `generate_series(date, date, interval '1 day')` in a view expands a stay's date range into one row per night | Boarding occupancy is computable from stays without a nightly snapshot table (§8.4 `rv_boarding_night`) |
| F-RPT-6 | ASP.NET Core 9's `RateLimitPartition.GetConcurrencyLimiter` is in the shared framework, and a rejected request gets the limiter's `RejectionStatusCode` | The `reports` concurrency policy (ADR-290) needs no package |

---

## 8. Domain model

### 8.1 Context and boundaries

```
Reporting context — schema `report`
ReportScheduleAggregate        (root)                              report.report_schedule
└── Recipients [primitive collection 1…20] StaffAccountId          → recipient_ids uuid[]
ScheduledReportRunAggregate    (root)  → ScheduleId                report.scheduled_report_run
ReportFileRecord               (record, not an aggregate)  → RunId report.report_file          (bytea)

Read-only views — schema `report`, created by Reporting's migrations, mapped keyless with ToView
report.rv_*   (§8.4)  — over clinic.*, account.*, admin.*, notify.* and the later areas' schemas
```

**What Reporting owns:** its schedules, its generated runs and files, and the view SQL. **What it never owns:**
any business fact. No view is materialised, and no row is copied (ADR-278).

**One aggregate mutated per command.** The scheduled-run sequence (§8.9) is two commands, each on one root:
record the run (a new `ScheduledReportRunAggregate`, plus the `ReportFileRecord` and the notification requests
enlisted in the same save, as every outbox producer does), then advance the schedule. No recorded exception is
needed.

**`ReportFileRecord` is not an aggregate root.** It is a write-once blob keyed by run id. It is added to the
scoped context through the `IReportFileStore` port and committed by the run's save, the same mechanism as
`EfNotificationOutbox` (HO-NTF-001 §8.4, F23). It is not an `AggregateRoot`, so HO-ACC-001's change capture does
not copy the bytes into the audit table. U-G8 asserts that.

### 8.2 The report catalogue — ADR-280

`ReportCatalogue` (Shared, static, exposed through `IReportCatalogue` so tests can add definitions) is the one
list of reports. A `ReportDefinition`:

| Field | Meaning |
|---|---|
| `Key` | `^[a-z]+(-[a-z0-9]+)+$`, ≤ 60, e.g. `clinic-daily-schedule`. Stable: it appears in URLs, schedules and audit rows |
| `Title`, `Description` | Shown in the catalogue and on exports |
| `Category` | `ReportCategory` (§10.1). It decides the access action (§8.8) |
| `Kind` | `Period` (rows are events inside `[from, to]`) or `AsAt` (a position at the end of `to`, with movements over `[from, to]`). **Both take a date range and a branch** (FR-RPT-1) |
| `DefaultPeriod` | A `ReportRelativePeriod` the client pre-selects |
| `MaxRangeDays` | 366 unless stated in §8.3 |
| `Parameters` | Name, type (`Enum` with allowed values, `Guid`, `Integer` with bounds, `Boolean`), default, label. Validated server-side; unknown parameters are refused |
| `Columns` | `Key`, `Label`, `Type` (`ReportColumnType`), `Unit?`, `RestrictedToAction?` (§8.8), `Sortable` |
| `DefaultSort`, `HasTotals`, `TimeBucketed` | `TimeBucketed` reports take `groupBy=Day\|Week\|Month`, and the client may chart them |
| `Formats` | `Csv`, `Pdf` — both for every report (US-RPT-01 AC2) |
| `SourceAreas` | The handovers whose tables its views read — documentation, and the phase gate in §17 |

**Every definition has exactly one `IReportSource`** (Read, `QueryHandlers/Reporting/Sources/`), registered
explicitly. U-A2 fails the build on a definition without a source or a source without a definition.

### 8.3 The reports

Money columns carry `currencyCode`. "Branch" is the report's branch filter (§8.7).

| # | Key | Story | Category | Kind | Columns (summary) | Rules |
|---|---|---|---|---|---|---|
| R1 | `clinic-daily-schedule` | RPT-01 AC1 | ClinicOperations | Period (default `Today`, max 31) | local date, start, cat, customer, service, performer, room, channel, walk-in, status, checked in, started, completed, wait min | **Holds never appear** (`Held`, `HoldExpired`, `HoldReleased` — HO-APT-001 §8.1). Totals by status: booked, checked in, completed, cancelled, **no-show**, walk-ins. Walk-ins sort by check-in time |
| R2 | `clinic-queue-performance` | RPT-01 AC1 "queue state" | ClinicOperations | Period (default `Today`) | local date, walk-ins by triage (routine / urgent / emergency), booked check-ins, seen, **waiting now**, average wait min, longest wait min, left without being seen | Wait = `started_on − checked_in_on`. "Left without being seen" = checked in, then cancelled. **Waiting now** is filled for today only; other days show `null`, rendered "—" (not zero). The live ordered queue is HO-APT-001's screen |
| R3 | `surgery-register` | RPT-01 AC1 | Clinical | Period (default `Last7Days`) | date, procedure, category (spay / neuter / other), cat, customer, surgeon, assistant, theatre, ASA grade, status, outcome, complications (yes/no), complications summary, consent override, controlled-drug entries | §8.1's "procedures by date, surgeon, outcome, complications". Clinical text is Clinical-only (§8.8, NFR-SEC-8) |
| R4 | `vaccination-due-list` | RPT-01 AC1 | ClinicOperations | Period — the **due window** (default next 30 days) | cat, customer, mobile, email, vaccine, last given, next due, status (`DueSoon` / `Overdue`), last reminder sent, next appointment booked | Latest record per vaccine via HO-CAT-001's `CatVaccinationStatusCalculator` (HO-NTF-001 F26). Never re-derived in SQL. Excludes deceased cats, cats whose owner is `Merged` or `Anonymised`, and inactive vaccines. Parameters `includeOverdue` (true) and `includeParasiteControl` (false; adds deworming rows, `Combined` counting as deworming — HO-NTF-001 F25). Branch attribution per ADR-292 |
| R5 | `stock-position` | RPT-02 | Stock | AsAt (default `MonthToDate`) | SKU, product, category, batch, expiry, location, branch, opening qty, in, out, adjustments, **closing qty**, batch status, landed unit cost, **valuation** | Quantities are sums of HO-IMP-001's immutable ledger (D-3). Closing = on hand at the end of `to`. Valuation = closing × landed unit cost, or `null` when no cost exists; the totals row excludes `null`s and states coverage (ADR-284). Parameter `includeZero` (false) |
| R6 | `stock-near-expiry` | RPT-02 | Stock | Period — the **expiry window** (default next 90 days) | SKU, batch, location, expiry, days to expiry, band, qty, landed unit cost, value at risk (cost), retail value at risk | Bands from `ExpiryBand` against the thresholds FR-IMP-10 configures in HO-IMP-001 (default 90 / 60 / 30). `includeExpired` (true) adds batches already past expiry that still have stock. Retail value = qty × the SKU's **current** price (HO-SHP-001) |
| R7 | `stock-low-reorder` | RPT-02 | Stock | Period — the **consumption window** (default `Last30Days`, max 365) | SKU, location, on hand, reorder point, average daily consumption, supplier, lead time days, **suggested quantity** | The suggestion comes from **HO-IMP-001's own FR-IMP-12 calculation**, called and not re-implemented (§8.4 contract; U-C4 spies it) |
| R8 | `import-consignments` | §8.1 | Stock | Period (default `Last30Days`) | reference, origin country, supplier, POs, clearance status, landed cost total, permit / health certificate / customs declaration present, batches received | |
| R9 | `revenue-by-business-line` | RPT-03 AC1 | Financial | Period, `TimeBucketed` (default `PreviousMonth`, max 731) | bucket, line (Clinic / Shop / Boarding / Other), gross, discounts, credit notes, **net revenue**, tax, cost of goods, **margin**, margin %, **margin coverage %** | ADR-283 and ADR-284. `Other` is shown when non-zero, never dropped |
| R10 | `revenue-shop-by-category` | RPT-03 AC1, AC2 | Sales | Period, `TimeBucketed` | category, sub-category, units, net revenue, *cost of goods, margin, margin %, coverage %* | Italic columns are `RestrictedToAction = Reports.Financial.View` (§8.8). Categories from FR-SHP-1. Shop net revenue here reconciles to R9's Shop line (I-D2) |
| R11 | `revenue-shop-by-origin` | RPT-03 AC1, AC2 | Sales | Period, `TimeBucketed` | country of origin, units, net revenue, *cost, margin, margin %, coverage %* | Origin from the SKU's product (HO-SHP-001 FR-SHP-4). Products with no origin (litter, carriers) are grouped as "Not applicable" |
| R12 | `revenue-by-service` | §8.2 | Financial | Period, `TimeBucketed` | line, service or product group, quantity, net revenue, cost, margin, coverage % | Clinic services by `ServiceCategory` (HO-ADM-001); boarding by stay kind; medicines and consumables costed where the ledger allows |
| R13 | `payment-reconciliation` | §8.2 | Financial | Period (default `PreviousDay`) | business date, method, expected, received / counted, variance, **variance direction** (FU1), matched, mismatches open, mismatches resolved | **Methods are exactly `CashOnDelivery`, `BankTransfer`, `CashInStore`, `CardInStore`** (FR-ORD-8), plus `StoreCredit` as a non-cash tender row when FR-BIL-9 exists. COD against courier remittances, transfers against bank statement lines, cash against till counts (FR-BIL-7). Card in store: expected only, until SRS Q-3c supplies a terminal settlement report (note shown, OQ-17). **No gateway row**. **`varianceDirection`** (HO-RPT-001-FU1): `Over` / `Short` / `Matched` / null, read off received-or-counted against expected, never off the sign of `variance` (the COD and bank rows show expected minus received, the cash row counted minus expected) |
| R14 | `daily-cash-up` | §8.2 / FR-BIL-13 | Financial | Period (default `Today`) | business date, cash, card in store, bank transfer, expected, counted, variance, **variance direction** (FU1), explanation, closed by | Reads HO-BIL-001's cash-up records; reports "Not closed" for an open day. `varianceDirection`: `Over` when counted exceeds expected (positive variance), `Short` when lower, `Matched` when equal, null when not counted (HO-RPT-001-FU1) |
| R15 | `receivables-ageing` | §8.2 | Financial | AsAt (default `Today`) | customer, invoices outstanding, balance, 0–30, 31–60, 61–90, > 90 days | Age from invoice issue date to the end of `to`. Anonymised customers shown by the placeholder name (HO-ACC-001 `AnonymisationRules`) |
| R16 | `refunds-credit-notes` | §8.2 | Financial | Period | reason, refund method (cash in store / bank transfer / store credit — FR-ORD-17), count, amount | |
| R17 | `tax-summary` | §8.2 | Financial | Period, `TimeBucketed` | tax class, rate %, taxable net, output tax; credit notes as negative | Sum of output tax equals the invoices' tax for the period (I-D6) |
| R18 | `customers-new-vs-returning` | RPT-04 | Growth | Period, `TimeBucketed` (max 731) | bucket, active customers, new, returning, new by acquisition source (self-service / reception), registrations | Definitions in ADR-291 |
| R19 | `customer-lifetime-value` | RPT-04 | Growth | Period — the **acquisition window** | band, customers, average LTV, clinic / shop / boarding share; top 50 customers (name, lines used, LTV) | LTV = net revenue to the end of `to` across all three lines, for customers first seen inside the window |
| R20 | `cohort-retention` | RPT-04 | Growth | Period (max 731; monthly cohorts) | cohort month, cohort size, % retained at month 1…12 — repeat purchase (shop), repeat visit (clinic), any line | OQ-8 definitions |
| R21 | `subscription-health` | RPT-04 | Growth | Period | active at start, new, paused, suspended, cancelled (churned), active at end, churn %, churn reasons | From HO-SUB-001's status history (§8.4) |
| R22 | `cross-line-conversion` | RPT-04 | Growth | Period | segment, customers, % who used shop, % who used boarding, % who used clinic | "Clinic customers who buy food; boarding customers who use the clinic" (SRS §8.3), as a 3 × 3 matrix |
| R23 | `cart-abandonment` | §8.3 | Sales | Period | carts abandoned, value, reminders sent, recovered carts, recovered value, abandonment %, recovery % | HO-ORD-001 defines "abandoned"; recovery = an order within 7 days of the abandoned-cart reminder (OQ-9) |
| R24 | `boarding-occupancy` | §8.1 | Boarding | Period (includes future dates as forecast) | date, unit type, units, capacity, occupied cat-nights, occupancy %, arrivals, departures, forecast flag | Occupancy = occupied ÷ available capacity (active units minus blocked periods). Future dates count `confirmed` and `reserved` stays and are flagged forecast |
| R25 | `boarding-care-compliance` | §8.1 | Boarding | Period | date, caretaker, tasks due, done on time, done late, missed, overdue now | "On time" = completed within the task's window from HO-BRD-001 (§8.4) |
| R26 | `boarding-incidents` | §8.1 | Boarding | Period | date, unit, severity, type, action taken (yes/no), owner notified within 15 min (medium/high), outcome, open | FR-BRD-14, FR-NTF-7 |
| R27 | `order-fulfilment` | §8.1 | Sales | Period | order, placed, channel, status, age hours, SLA breach, dispatched, delivered | Age buckets; breach against HO-ORD-001's dispatch SLA setting (OQ-18) |

**27 definitions.** Every one takes `from`, `to` and `branchId` (FR-RPT-1), and U-A1 enforces it.

### 8.4 Source contracts — the views — ADR-278

Each view is **one migration-owned SQL file**. Its **column list is the contract** the report sources code
against. The `FROM` clause binds to whatever the owning handover actually named. Schema and table names for
areas not yet written are indicative only. A column the source cannot supply is a stop (§20.2), never a
constant or a guess.

**Over areas already specified**

| View | Contract columns | Reads | Owner |
|---|---|---|---|
| `rv_clinic_appointment` | `appointment_id, branch_id, local_date, starts_on, ends_on, customer_id, customer_name, cat_id, cat_name, service_name, service_category, performer_id, performer_name, room_name, channel, is_walk_in, triage_priority, status, checked_in_on, started_on, completed_on, cancelled_on, no_show_recorded_on` — **excludes the three hold statuses** in the `WHERE` | `clinic.appointment`, `account.*`, `admin.room`, `admin.branch` | HO-APT-001 §8.2 (written) |
| `rv_cat_vaccination` | `cat_id, cat_name, owner_id, owner_name, owner_mobile, owner_email, owner_status, cat_life_status, vaccine_id, vaccine_name, vaccine_active, administered_on, next_due_on` — **every** record; "latest" is chosen by the calculator | `clinic.cat`, `clinic.cat_vaccination`, `clinic.vaccine`, `account.customer_account` | HO-CAT-001 (built), HO-ACC-001 |
| `rv_cat_parasite_treatment` | `cat_id, …owner columns…, treatment_type, given_on, next_due_on` | `clinic.cat_parasite_treatment` | HO-CAT-001 |
| `rv_cat_last_branch` | `cat_id, branch_id, last_appointment_on` — branch of the most recent non-hold appointment | `clinic.appointment` | HO-APT-001 |
| `rv_care_reminder_sent` | `subject_kind, subject_id, subject_ref, subject_due_on, sent_on` — the latest `Sent` or `InboxDelivered` notification per subject | `notify.notification` | HO-NTF-001 §8.6 |
| `rv_staff` | `staff_account_id, full_name, role_ids, email_present` | `account.staff_account` | HO-ACC-001 |
| `rv_customer` | `customer_id, display_name, status, registration_channel, registered_on` — `display_name` is the stored name (already the placeholder for anonymised records) | `account.customer_account` | HO-ACC-001 |

**Over areas not yet written — the contract this slice assumes.** Each row is an **open dependency** (OQ-14).

| View | Contract columns | Assumed source | Owner (not written) |
|---|---|---|---|
| `rv_surgery` | `surgery_id, branch_id, local_date, procedure_name, procedure_category (Spay/Neuter/Other), cat_id, cat_name, customer_id, customer_name, surgeon_id, surgeon_name, assistant_name, theatre_name, asa_grade, status (Appendix C), outcome, has_complications, complications_summary, consent_override, controlled_drug_entries` | Surgery root with theatre, surgeon, assistant, checklist ASA grade, consent and override flags, report outcome and complications (FR-SRG-1…5); a count of register entries linked to the surgery (FR-SRG-8, via HO-PHM-001's register) | **HO-SRG-001**, HO-PHM-001 |
| `rv_stock_ledger` | `entry_id, sku_id, batch_id, location_id, branch_id, movement_kind (Receipt/Sale/Dispense/BoardingUse/Transfer/Adjustment/Return/Wastage), quantity_delta (integer, negative = out), occurred_on, source_type, source_line_id` | The immutable stock ledger (FR-IMP-15, D-3). **`source_type` + `source_line_id` must identify the order line, POS line, dispense record or stay charge that caused a sale-type movement** — margin depends on it (ADR-284) | **HO-IMP-001** |
| `rv_batch` | `batch_id, sku_id, lot_number, expiry_on (date), status (Appendix C batch states), consignment_id, landed_unit_cost (nullable), currency_code` | FR-IMP-4, FR-IMP-5 | **HO-IMP-001** |
| `rv_stock_location` | `location_id, branch_id, name, kind` | FR-IMP-6. **Locations carry `BranchId`** per HO-ADM-001 §8.2's rule | **HO-IMP-001** |
| `rv_reorder_rule` | `sku_id, location_id, reorder_point, supplier_id, supplier_name, lead_time_days` | FR-IMP-12 | **HO-IMP-001** |
| — *(no view)* | Reorder suggestion | **A pure domain service or query HO-IMP-001 exposes** (e.g. `ReorderSuggestionCalculator.Suggest(onHand, reorderPoint, dailyConsumption, leadTimeDays)`). R7 calls it. If HO-IMP-001 exposes only an HTTP endpoint, stop (§20.2) | **HO-IMP-001** |
| `rv_consignment` | `consignment_id, reference, origin_country, supplier_name, po_count, clearance_status, landed_cost_total, currency_code, has_permit, has_health_certificate, has_customs_declaration, batches_received, created_on, cleared_on` | FR-IMP-3 | **HO-IMP-001** |
| `rv_sku` | `sku_id, sku_code, variant_label, product_id, product_name, brand, category, sub_category, country_of_origin (nullable ISO-3166 alpha-2), current_price, currency_code, requires_vet_authorisation` | FR-SHP-1…4; price effective-dated per HO-ADM-001 ADR-049, current price resolved in the view | **HO-SHP-001** |
| `rv_invoice_line` | `invoice_line_id, invoice_id, invoice_number, branch_id, customer_id, invoice_status, issued_on, line_kind, service_id, service_category, sku_id, stay_id, source_type, source_line_id, quantity, net_amount (after line and allocated order discounts, excluding tax), discount_amount, tax_class_id, tax_rate_percent, tax_amount, currency_code` | FR-BIL-1, 2, 8; **a line kind on every line** (HO-BIL-001's `InvoiceLineKind`); combined invoices (FR-ORD-19) classify per line | **HO-BIL-001** |
| `rv_credit_note_line` | same columns plus `credit_note_id, reason, refund_method (CashInStore/BankTransfer/StoreCredit)`, amounts **positive**, `issued_on` | FR-BIL-5, FR-ORD-17 | **HO-BIL-001** |
| `rv_payment` | `payment_id, branch_id, customer_id, invoice_id, method (CashOnDelivery/CashInStore/CardInStore/BankTransfer/StoreCredit), amount, currency_code, received_on, status, recorded_by` | FR-BIL-12 | **HO-BIL-001** |
| `rv_reconciliation` | `branch_id, business_date, method, expected_amount, actual_amount, variance, status (Matched/Mismatch/Resolved), source (CourierRemittance/BankStatement/TillCount)` | FR-BIL-7 | **HO-BIL-001** |
| `rv_cash_up` | `branch_id, business_date, cash_recorded, card_recorded, transfer_recorded, expected_total, counted_total, variance, explanation, closed_by_name, closed_on` | FR-BIL-13 | **HO-BIL-001** |
| `rv_invoice_balance` | `invoice_id, branch_id, customer_id, issued_on, outstanding_amount` (as at now; the source computes paid and credited) | FR-BIL-3 | **HO-BIL-001** |
| `rv_order` | `order_id, branch_id, customer_id, channel (Online/Pos), status (Appendix C), placed_on, confirmed_on, packed_on, dispatched_on, delivered_on, cancelled_on, payment_method` | FR-ORD-10, FR-ORD-18 | **HO-ORD-001** |
| `rv_cart` | `cart_id, customer_id, value, abandoned_on (nullable), reminder_sent_on (nullable), converted_order_id (nullable), converted_on (nullable)` | FR-NTF-3 abandoned-cart reminders; HO-ORD-001 defines "abandoned" | **HO-ORD-001** |
| `rv_boarding_unit` | `unit_id, branch_id, unit_type, capacity, is_active` | FR-BRD-1 | **HO-BRD-001** |
| `rv_boarding_unit_block` | `unit_id, starts_on, ends_on` | FR-BRD-3 | **HO-BRD-001** |
| `rv_boarding_night` | `stay_id, branch_id, unit_id, unit_type, night (date), cats, status` — one row per night via `generate_series` (F-RPT-5) | Stays with date range, unit, cats and Appendix C status | **HO-BRD-001** |
| `rv_boarding_movement` | `stay_id, branch_id, arrival_on, departure_on, checked_in_on, checked_out_on, status` | FR-BRD-9, FR-BRD-18 | **HO-BRD-001** |
| `rv_care_task` | `task_id, branch_id, stay_id, kind, due_from, due_to, completed_on, completed_by_id, completed_by_name` | FR-BRD-10, 11 — **a due window per task** | **HO-BRD-001** |
| `rv_incident` | `incident_id, branch_id, unit_name, cat_id, kind, severity, occurred_on, recorded_on, action_taken_present, owner_notified_on, outcome, is_open` | FR-BRD-14 | **HO-BRD-001** |
| `rv_subscription_change` | `subscription_id, customer_id, from_status, to_status, changed_on, reason_code, reason_label` | FR-SUB-3, FR-SUB-6; Appendix C subscription states | **HO-SUB-001** |
| `rv_visit_open` | `visit_id, branch_id, vet_id, completed_on, finalised_on (nullable)` | FR-CLN-19 | **HO-EMR-001** — dashboard only |
| `rv_customer_activity` | `customer_id, business_line, occurred_on, net_amount, currency_code, kind (Invoice/CreditNote/Visit/Stay/Order)` — a `UNION ALL` of the views above, used by the growth reports | derived | Reporting |

**Contract events: none.** Reporting subscribes to no domain event (`.ai/domain-events-and-outbox.md` §5): every
figure is derivable from committed tables. Two assumptions keep that true, and both are recorded in OQ-14:

- Every context that holds a customer id **reassigns it to the survivor on `CustomerMergedEvent`**, as HO-ACC-001
  ADR-039 binds them to (HO-APT-001 §11.6 does). Growth reports then count a merged customer once.
- Ledger, invoices, credit notes, payments and status histories are **append-only** in their owners (D-3, D-5,
  BR-11), so a report over a past period is reproducible.

### 8.5 Read-only execution — ADR-279

`IReportingReadScope` (Infrastructure, Scoped) is the only way a report source or dashboard tile touches the
database:

```csharp
Task<T> RunAsync<T>(Func<CatEcommerceDbContext, CancellationToken, Task<T>> work, CancellationToken ct);
```

1. Opens a transaction on the scoped context's connection.
2. `SET TRANSACTION READ ONLY; SET LOCAL statement_timeout = '{Reporting:QueryTimeoutSeconds}s';`
3. Runs `work` against keyless view `DbSet`s only, all `AsNoTracking()`.
4. **Rolls back** — never commits. There is nothing to commit.
5. SQLSTATE `57014` → `ReportTimedOutException` → `400 ReportTimedOut` (§12.5). SQLSTATE `25006` is a
   programming error, rethrown and logged at `Error`: code tried to write.

Two convention tests back the database guarantee: U-A3 (no repository, `SaveChanges`, `Execute*` or raw SQL in
`QueryHandlers/Reporting/**`) and U-X3 (every keyless view entity maps `ToView(…, "report")`).

### 8.6 Revenue, cost and margin — ADR-283, ADR-284

**Revenue**

- Recognised on the **invoice issue instant** (`issued_on`) for invoices in `Issued`, `PartPaid`, `Paid` or
  `Credited` (Appendix C). `Draft` never counts.
- **Net revenue = Σ `net_amount`** (after line and allocated order discounts, **excluding tax**) − Σ credit-note
  `net_amount` dated in the period. A credit note counts on **its own** issue date, so a closed month never
  changes (BR-11).
- **Business line by line kind, not by invoice** — `BusinessLineRules.Classify(InvoiceLineKind)` (Shared, pure):

| Line kind (HO-BIL-001's, mirrored) | Business line |
|---|---|
| `ClinicService`, `Surgery`, `Medicine`, `ClinicConsumable`, `LabTest` | **Clinic** — including clinic charges on a stay invoice (FR-BRD-15) |
| `Product`, `DeliveryCharge` | **Shop** — online and POS alike (FR-ORD-18), prescription diets included |
| `BoardingNight`, `BoardingService` | **Boarding** — daycare included (FR-BRD-22) |
| `Deposit`, `Voucher` (sale of) | **Not revenue** — a deposit is a payment in advance; a voucher is a liability until redeemed |
| anything else, or a kind added later | **Other** — shown, never dropped (U-D1) |

- **Currency**: every aggregate groups by `currency_code`. A report never adds two currencies together. With one
  currency (`commerce.currencyCode`, HO-ADM-001) there is one group.
- Arithmetic is `numeric` in SQL and `decimal` in C#. Percentages are rounded half-away-from-zero to 1 dp,
  **server-side only**.

**Cost of goods and margin**

- For an invoice line whose `source_line_id` has sale-type ledger entries (`Sale`, `Dispense`, `BoardingUse`,
  minus `Return`): **COGS = Σ(−quantity_delta × batch.landed_unit_cost)**.
- A line is **costed** only if every contributing batch has a `landed_unit_cost`. A partly costed line is
  treated as uncosted. **There is no fallback to purchase-order cost or any estimate.**
- **Margin = net revenue of costed lines − their COGS.** Margin % is relative to that same costed revenue.
- **Coverage % = costed net revenue ÷ total net revenue**, shown beside every margin figure. When coverage is
  below 100 %, the result carries a note: *"Margin covers 83.0 % of shop revenue. 17.0 % came from batches with
  no landed cost yet."* When coverage is 0 %, margin is `null` ("Not available"), never zero. This is how
  US-RPT-03 AC2's "given landed cost is known" is honoured without implying a figure for the rest.
- Services have no cost. Clinic margin covers medicines and consumables only, and the coverage says so.

### 8.7 Time, periods and branches — ADR-282, ADR-281

- `from` and `to` are **branch-local `DateOnly`, inclusive**. The instant range is `[from 00:00, to + 1 day
  00:00)` in the report branch's IANA zone.
- **All branches** (`branchId=all`): the range is interpreted in the **main branch's** zone
  (`AdministrationSeed.MainBranchId`). With A-1's single branch this is the same zone. A second branch in
  another zone is labelled in the result's `timeZoneId` and noted.
- `ReportPeriodResolver` (Domain, **pure**, the time model of HO-ADM-001 ADR-045, as HO-NTF-001's `QuietHours`
  uses it) resolves relative periods for the client defaults and for schedules: `Today`, `PreviousDay`,
  `PreviousWeek` (Monday–Sunday), `PreviousMonth`, `MonthToDate`, `Last7Days`, `Last30Days` (both ending
  yesterday), as of an instant, in a zone.
- `TimeBucketed` reports bucket by the local date in the same zone (weeks start Monday).
- **Branch scope**:
  - No `branchId` → the caller's **active branch** (HO-ADM-001 ADR-046).
  - A `branchId` not in the caller's assignment → `403 BranchNotAssigned` (HO-ADM-001's code).
  - `all` → only callers holding a role in `ReportRules.CrossBranchRoles` (`Administrator`, `Accountant` —
    OQ-6), else `403 CrossBranchReportNotPermitted`.
- **Not branch-scoped data** (HO-ADM-001 §8.2): customers and cats. Growth reports filter by the branch of the
  **transaction** (invoice branch). The vaccination due list uses ADR-292's attribution.

### 8.8 Access — ADR-281

Appendix D's "Reports" row reads: Reception R (ops) · Nurse — · Vet R (clinical) · Caretaker R (boarding) ·
Inventory R (stock) · Shop Mgr R (sales) · Accountant F (financial) · Admin F. **Each category is one action**,
appended to the one `ActionRoles` table:

| Category | Action | Staff roles | Reports |
|---|---|---|---|
| ClinicOperations | `Reports.ClinicOperations.View` | Reception, **Vet**, Administrator | R1, R2, R4 — SRS §8.1 names the Vet as a primary user of the daily schedule (OQ-7) |
| Clinical | `Reports.Clinical.View` | Vet, Administrator | R3 |
| Boarding | `Reports.Boarding.View` | Caretaker, Administrator | R24–R26 |
| Stock | `Reports.Stock.View` | Inventory, Administrator | R5–R8 |
| Sales | `Reports.Sales.View` | ShopManager, Administrator | R10, R11, R23, R27 |
| Financial | `Reports.Financial.View` | Accountant, Administrator | R9, R12–R17 **and the restricted columns of R10, R11** |
| Growth | `Reports.Growth.View` | Administrator | R18–R22 |
| — | `Reports.Schedule` | Administrator | schedules (FR-RPT-2 "Administrators shall schedule") |
| — | `Dashboard.View` | all eight staff roles | the home dashboard (tiles gated individually, §8.11) |

**Nurse** has no report action (Appendix D "—"). A nurse's dashboard shows clinic tiles gated by actions the
nurse already holds (`Appointments.View`, `Cats.View`).

**Column restriction.** A column with `RestrictedToAction` is removed from the result, the CSV and the PDF when
the caller does not hold that action. It is removed from the payload entirely, not blanked. The totals row and
notes drop it too. The catalogue endpoint lists only visible columns. A ShopManager sees shop revenue by category
and origin **without** cost or margin (OQ-5).

Exporting needs the same action as viewing: FR-RPT-1 makes export part of "reading" a report.

### 8.9 Scheduled reports — US-RPT-06 — ADR-287, ADR-288

**`ReportScheduleAggregate`**

| Field | Rules |
|---|---|
| `Name` | ≤ 100 |
| `ReportKey` | In the catalogue |
| `BranchId?` | A branch id, or null = all branches (only if the creator may use `all`). Also the **zone** for send time and periods (main branch's zone when null) |
| `Period` | A `ReportRelativePeriod` compatible with the report (`Today` only for `AsAt` and today-style reports) |
| `Parameters` | `jsonb`, validated against the definition |
| `Format` | `Csv` or `Pdf` |
| `Frequency` | `Daily`; `Weekly` + `DayOfWeek`; `Monthly` + `DayOfMonth` (1–28) **or** `LastDayOfMonth` |
| `SendAtLocal` | `TimeOnly`, 5-minute steps |
| `RecipientIds` | 1…`ReportScheduleRules.MaxRecipients` (20) staff account ids, distinct |
| `Status` | `Active` ⇄ `Paused`; either → `Cancelled` (terminal; kept for history) |
| `NextRunOn?` | Instant, from `ReportScheduleCalculator`; null unless `Active` |
| `LastRunOn?`, `LastRunDueOn?` | |

| Method | Rules |
|---|---|
| `Create`, `Update` | As above; recompute `NextRunOn` from now |
| `Pause`, `Resume` | `Resume` recomputes from now (no catch-up for the paused time) |
| `Cancel` | Terminal |
| `RecordRunCompleted(dueOn, now)` | Idempotent: if `LastRunDueOn ≥ dueOn`, no change. Else `LastRunDueOn = dueOn`, `LastRunOn = now`, `NextRunOn = calculator.NextAfter(max(dueOn, now))` — **missed slots collapse to one** (ADR-287) |

**No domain events.** Nothing subscribes, and data changes are audited by HO-ACC-001's interceptor. The
handlers of `Create` and `Update` check every recipient **in the handler**:

- active staff with an email address, else `ReportRecipientHasNoEmail` (HO-NTF-001 R2 would otherwise
  suppress the email as `NoPermittedChannel`);
- holds the report's category action, else `ReportRecipientNotPermitted`;
- for a branch-scoped schedule, assigned to that branch, else `ReportRecipientNotAssignedToBranch`.

**`ReportScheduleCalculator`** (Domain, **pure**): `NextAfter(schedule, instant, zone)`. The next local occurrence
of `SendAtLocal` on a matching day, converted with the branch zone. A local time skipped by a DST change moves
to the first valid instant after it. An ambiguous (repeated) local time takes the **first** occurrence. U-G1
covers Dhaka (no DST) and `Europe/London` across both changes.

**`ScheduledReportRunAggregate`** — `ScheduleId`, `DueOn`, `Trigger` (`Schedule` \| `Manual`), `Status`
(`Generated` \| `Failed` \| `Purged`), `PeriodFrom`, `PeriodTo`, `TimeZoneId`, `Format`, `FileName`,
`ContentType`, `ContentLength`, `ContentSha256`, `RowCount`, `ColumnsIncluded` (text[]), `AttachmentOmitted`
(bool), `FailureCode?`, `GeneratedOn`, `PurgedOn?`. Methods: `RecordGenerated`, `RecordFailed`, `Purge`.

**Exactly once per slot.** A unique index `ix_scheduled_report_run_slot` on `(schedule_id, due_on) WHERE
trigger = Schedule AND status IN (Generated, Purged)`. A second generation for the same slot hits `23505`, which
the handler treats as **success with no second row and no second email** (U-G2, I-G3).

**The run sequence** — `ReportingWorkCycle` (Api, `Background/`), one schedule at a time:

1. `GetDueReportSchedulesQuery(now)` → active schedules with `NextRunOn ≤ now`, oldest first, at most
   `ReportScheduleRules.MaxSchedulesPerPass` (20) per pass.
2. Resolve the period with `ReportPeriodResolver` as of the schedule's due instant, in its zone.
3. Resolve **visible columns** = the columns every recipient may see: the **intersection** of their restricted
   actions (ADR-288). Nobody receives a column they could not open on screen.
4. Dispatch `RunReportQuery` in export mode, with a `ReportAccess` built from step 3 and the schedule's branch —
   never from an unrestricted system identity.
5. Render with `IReportExportRenderer` (CSV or PDF, the same code as interactive export).
6. Dispatch **`RecordScheduledReportRunCommand`**. The handler:
   - creates the run;
   - if the file is ≤ `reporting.maxAttachmentMegabytes`, stores it via `IReportFileStore.Add` (enlisted);
     otherwise sets `AttachmentOmitted = true` and stores nothing;
   - for each recipient, calls `INotificationOutbox.Enqueue` with template `report.scheduled-delivery`,
     `RecipientKind = Staff`, subject `ScheduledReport` / run id / schedule id, `AttachmentKind = ReportRun` /
     run id (unless omitted), `BranchId`, and `NotAfter` = the file's purge instant;
   - saves once — run, file and requests commit together (HO-NTF-001 F23).
7. Dispatch **`AdvanceReportScheduleCommand(scheduleId, dueOn)`** → `RecordRunCompleted`.

**Failure.** If step 4 or 5 throws (timeout, too many rows for a PDF, anything else), dispatch
`RecordFailedReportRunCommand`: a `Failed` run with a `FailureCode`, no file and no email. A schedule whose slot
has `ReportScheduleRules.MaxAttemptsPerSlot` (3) failed runs is advanced anyway, so one bad slot never blocks
the schedule. `ReportTooLargeForPdf` is not retried and advances at once. Failed runs appear in the run history
(§12.4). The worker never throws out of `ExecuteAsync` (HO-NTF-001's worker rules).

**Manual run** (`POST …/{id}/run-now`): the same sequence with `Trigger = Manual`, `DueOn = now`. It is not
subject to the slot index and does not advance the schedule. It is rate-limited (§12.3).

### 8.10 Report files and retention — ADR-289

`IReportFileStore` (Domain port; Infrastructure `EfReportFileStore`): `Add(runId, fileName, contentType,
bytes)` enlists a `ReportFileRecord`; `Remove(runId)` enlists its deletion; `Task<ReportFile?> GetAsync(runId)`
reads it. `bytea`, ≤ `reporting.maxAttachmentMegabytes` (default 10 MB, upper bound 20).

**Purge.** Each worker pass purges files older than `reporting.exportRetentionDays` (7), one run per command
(`PurgeReportRunFileCommand`). The command removes the `ReportFileRecord` (Reporting's own table) and calls
`Purge()` on the run (`Status = Purged`, `PurgedOn`). A notification not yet sent then fails its guard (§11.7)
and is cancelled rather than sent without its attachment.

### 8.11 Role dashboards — US-RPT-05 — ADR-286

`GET /api/staff/dashboard` returns tiles for **today** (the active branch's local date) at the **active branch**.

- **Tile set** = the union of `DashboardTiles.ForRole(role)` over the caller's roles, deduplicated, in the order
  of the caller's first role in `StaffRoles.All` order.
- **Each tile declares its gate action.** A tile is **computed only if the caller holds it**. The tile source
  is not even invoked otherwise (U-E2). That is the server half of "nothing I am not permitted to see".
- A tile whose source module is not merged yet is **absent**, not zero.
- Every tile has `drillDown` — a report key and parameters, only if the caller may open that report — or null.
- Values are computed live through `IReportingReadScope`. There is no cache, and each tile is one aggregate
  query. The whole payload is budgeted at **NFR-PRF-3's 1.5 s p95**; I-E4 measures it.

| Tile key | Roles | Gate action | Figures (today, active branch) | Source |
|---|---|---|---|---|
| `clinic.appointments-today` | Reception, Vet, Nurse, Administrator | `Appointments.View` | booked, checked in, completed, no-show | HO-APT-001 |
| `clinic.queue-now` | Reception, Vet, Nurse | `Appointments.View` | waiting now, emergencies waiting, longest wait (min) | HO-APT-001 |
| `clinic.my-appointments-today` | Vet | `Appointments.View` | the caller's own appointments today, remaining | HO-APT-001 |
| `clinic.vaccinations-due` | Reception, Nurse | `Cats.View` | due in the next 7 days, overdue (branch-attributed, ADR-292) | HO-CAT-001 |
| `clinic.consultations-unfinalised` | Vet | HO-EMR-001's visit read action | the caller's completed-but-unfinalised visits | HO-EMR-001 |
| `surgery.today` | Vet, Nurse, Reception | HO-SRG-001's read action | scheduled, in theatre, in recovery | HO-SRG-001 |
| `boarding.occupancy-today` | Caretaker, Reception, Administrator | HO-BRD-001's read action | occupied / available units, cats in | HO-BRD-001 |
| `boarding.movements-today` | Caretaker, Reception | same | arrivals, departures | HO-BRD-001 |
| `boarding.care-attention` | Caretaker | HO-BRD-001's care-task action | overdue tasks, cats not eating, open incidents | HO-BRD-001 |
| `stock.expiry` | Inventory, Administrator | `Reports.Stock.View` | batches expiring ≤ 30 days, value at risk (cost) | HO-IMP-001 |
| `stock.low` | Inventory | `Reports.Stock.View` | SKU-locations below reorder point | HO-IMP-001 |
| `stock.consignments-open` | Inventory | `Reports.Stock.View` | consignments awaiting clearance | HO-IMP-001 |
| `orders.today` | ShopManager, Administrator | `Reports.Sales.View` | placed, to pack, dispatched | HO-ORD-001 |
| `orders.awaiting-transfer` | ShopManager, Accountant | `Reports.Sales.View` or `Reports.Financial.View` | bank-transfer orders in `pending_payment`, oldest age (h) | HO-ORD-001 |
| `orders.returns-pending` | ShopManager | `Reports.Sales.View` | returns awaiting a restock or write-off decision | HO-ORD-001 |
| `revenue.shop-today` | ShopManager | `Reports.Sales.View` | shop net revenue today (no cost) | HO-BIL-001 |
| `payments.today` | Accountant, Administrator | `Reports.Financial.View` | recorded today by method: COD, cash in store, card in store, bank transfer | HO-BIL-001 |
| `payments.unreconciled` | Accountant | `Reports.Financial.View` | open mismatches, amount | HO-BIL-001 |
| `billing.cash-up` | Accountant | `Reports.Financial.View` | today's cash-up: open / closed, variance | HO-BIL-001 |
| `billing.receivables` | Accountant, Administrator | `Reports.Financial.View` | outstanding total, over 90 days | HO-BIL-001 |
| `revenue.today-by-line` | Administrator | `Reports.Financial.View` | net revenue today: clinic, shop, boarding | HO-BIL-001 |

Where an owning handover names no read action, the gate falls back to the category action shown in §8.8 for
that area. The implementer names every fallback used in the ACCEPTANCE report.

### 8.12 Exports — ADR-285

- **Same result, three shapes.** Screen, CSV and PDF all come from one `ReportResult` produced by the source.
  The CSV and PDF therefore contain exactly the rows and columns the caller may see.
- **CSV** (`ReportCsvWriter`, Infrastructure): UTF-8 **with BOM** (so spreadsheet tools read Bengali names
  correctly), CRLF, header row of column labels, money as plain `0.00` strings with a separate currency column,
  instants in the report zone as `yyyy-MM-dd HH:mm`. Cells beginning with `= + - @`, tab or CR are prefixed with
  `'` — **HO-ADM-001 ADR-051's rule**. If HO-ADM-001 put that guard in a shared helper, call it; do not copy it.
  Cap: `ReportRules.CsvMaxRows` (50 000) → `413`.
- **PDF** (`QuestPdfReportRenderer`, Infrastructure): A4. Portrait up to 6 columns, landscape above. Header
  with clinic name, branch, report title, period with zone, filters, generated on, generated by. Repeated table
  header, totals row, notes (coverage), page *n* of *m*. Cap: `ReportRules.PdfMaxRows` (5 000) → `413` with
  *"Too many rows for a PDF — export CSV or narrow the filters."* Fonts as HO-CAT-001's renderer (OQ-11).
- **Audit.** Before streaming, the controller dispatches `RecordReportExportCommand`. It writes a `BusinessEvent`
  audit entry `ReportExported` with the report key, format, filters, row count and visible column keys —
  **never row data** (HO-ADM-001 ADR-051's precedent). A failed audit write fails the export (`500`, logged):
  an unaudited financial export is worse than a retry. Viewing on screen is **not** audited, because a query
  never writes (`.ai/read-side-pattern.md` rule 5).
- Responses carry `Content-Disposition: attachment; filename="purrcare-{key}-{from}-{to}.{csv|pdf}"` and
  `Cache-Control: no-store`.

### 8.13 Ports and domain services

| Name | Kind | Notes |
|---|---|---|
| `ReportPeriodResolver` | pure domain service | §8.7 |
| `ReportScheduleCalculator` | pure domain service | §8.9 |
| `ReportAccessPolicy` | pure domain service | Visible columns, branch scope, cross-branch roles and tile gating from roles and `ActionRoles` — one place, used by the API, the worker, the command handlers and the guard |
| `BusinessLineRules` | pure (Shared) | §8.6 |
| `IReportFileStore` | port | §8.10 |
| `IReportCatalogue` | port over the static catalogue | §8.2 |
| `IReportingReadScope` | Infrastructure service | §8.5 |
| `IReportSource`, `IDashboardTileSource` | Read-side strategy interfaces | One per report key / tile key |
| `IReportExportRenderer` | Infrastructure | CSV and PDF (§8.12) |

### 8.14 Exceptions

Reuse `DomainRuleViolationException`. `ReportTimedOutException` (Infrastructure) exists only to be mapped to a
`400` by the filter (§12.5).

---

## 9. ADRs to record during this build

Append to `.ai/decisions/adr.md`. **This handover owns ADR-277 … ADR-296** (orchestrator assignment; the range is
added to the README ADR table under OQ-15). It uses ADR-277 … ADR-292; ADR-293 … ADR-296 stay unused.

| ADR | Decision | Why it must be recorded |
|---|---|---|
| **ADR-277** | **`Reporting` bounded context**, namespace segment `Reporting`, schema `report`, owning schedules, runs, report files and the `report.rv_*` views. It writes nothing outside `report` except audit entries (HO-ACC-001's aggregate) and notification requests (HO-NTF-001's outbox) | Not in `.ai/bounded-contexts.md` (OQ-1). The one area that reads everything must be the one area proven to write nothing elsewhere |
| **ADR-278** | **Reports read migration-owned SQL views**, keyless and mapped with `ToView`. No projection, no event-fed reporting table, no copy of source rows. Each view's **column list is the reporting contract**. Source renames change only the view SQL. View SQL files are **versioned by name** (`rv_x.v1.sql`) so an applied migration never changes | ADR-012 removed projections; `.ai/read-side-pattern.md` §5 prescribes views. A projection would duplicate the ledger and invoices and drift from them. Considered and rejected: event-fed projections (a second copy, redelivery and replay problems, ADR-006's history); materialised views (staleness and a refresh job, kept as OQ-10's escape hatch only) |
| **ADR-279** | **Read-only execution.** Every report and tile runs inside `IReportingReadScope`: `READ ONLY` transaction, `SET LOCAL statement_timeout`, always rolled back. Backed by U-A3 (no repository, `SaveChanges`, `Execute*` or raw SQL in reporting code) and I-A3 (a write inside the scope fails with `25006`) | Makes "reports never write to another module's tables" a database guarantee rather than a review habit |
| **ADR-280** | **One catalogue, one pipeline.** `ReportCatalogue` in `Shared` defines every report: key, category, kind, parameters, typed columns, restricted columns, range limit, formats. One generic endpoint family, one `IReportSource` per key (U-A2 enforces 1:1), one CSV writer, one PDF renderer | FR-RPT-1 ("every report") is then structural. Twenty-seven hand-built endpoints would drift on filters, paging and export |
| **ADR-281** | **Access.** One action per report category, mapped from Appendix D's "Reports" row. Vet added to clinic operations (OQ-7). Cost and margin columns restricted to `Reports.Financial.View` and **removed from the payload** when not held. Branch scope: the active branch by default, assigned branches only, `all` for Administrator and Accountant (OQ-6). Report endpoints carry a `Reports.Any` marker action; the report's own category action is enforced in the handler with the same `403` shape and denial audit | NFR-SEC-2; US-RPT-05 "nothing I am not permitted to see" also applies inside a report |
| **ADR-282** | **Time.** `from`/`to` are inclusive branch-local dates in the report branch's zone; all-branches uses the main branch's zone. Relative periods and schedule times are resolved by pure domain services on HO-ADM-001's time model | ADR-045. A report that shifts a day at midnight UTC is wrong for a Dhaka clinic six hours ahead |
| **ADR-283** | **Revenue.** Recognised on invoice issue; net of line and allocated discounts; excluding tax; credit notes negative on their own issue date; business line by **invoice line kind** via `BusinessLineRules` (unknown → `Other`, never dropped); deposits and voucher sales are not revenue; never summed across currencies | US-RPT-03 needs one definition that every revenue report shares, or R9 and R10 will not reconcile. Accrual versus cash basis is OQ-2 |
| **ADR-284** | **Cost and margin.** COGS from sale-type ledger movements × batch landed unit cost, linked to invoice lines by the shared source-line reference. A line is costed only if every batch has a cost. Margin is null when coverage is zero, and **coverage % is always shown**. No estimate or PO-cost fallback | US-RPT-03 AC2 "given landed cost is known". A blended guess would make margin look complete when it is not |
| **ADR-285** | **Exports.** CSV (UTF-8 BOM, ADR-051 injection guard, ≤ 50 000 rows) and PDF (QuestPDF, ≤ 5 000 rows) from the same `ReportResult` as the screen; `413` beyond the caps; every export audited as `ReportExported` with filters and row count, never row data; the audit write must succeed before streaming | Exported financial and customer data leaves the system; who took what must be known |
| **ADR-286** | **Role dashboards are server-composed.** Tile set per role (union across roles), each tile gated by an existing action and **not computed** when the gate fails; today at the active branch; live, uncached; absent (not zero) when its module is not built | FR-RPT-3 and US-RPT-05. Client-side role maps drift and leak |
| **ADR-287** | **Schedules.** `ReportScheduleAggregate` with `NextRunOn` from a pure calculator in branch-local time (DST rules stated); exactly one run per slot via a unique index; missed slots collapse to the latest; three failed attempts advance the slot; administrators only | FR-RPT-2. A restart must never send ten copies, and a DST night must never send none |
| **ADR-288** | **Scheduled delivery.** Recipients are **staff accounts only**, each checked for the report's action at creation and again at send time by a subject guard. Restricted columns are included only if **every** recipient may see them. The file is attached **by reference** through an HO-NTF-001 amendment (§11.7), loaded at send time and never copied into `notify.*` | The attachment is the data. Sending it to someone who has lost the role, or to a free-text address, bypasses every control in ADR-281 |
| **ADR-289** | **Report files** are stored as `bytea` in `report.report_file` through `IReportFileStore`, enlisted with the run (F23), capped (default 10 MB, above which the email carries a link and a note), purged after `reporting.exportRetentionDays` (7) | No object-storage port exists yet (OQ-12). Keeping files briefly makes delivery retryable without regenerating a different answer |
| **ADR-290** | **Protecting interactive work (NFR-PRF-7).** Single host (ADR-010), so reports are isolated by **bounded concurrency**: the `reports` concurrency limiter (default 4 in flight, queue 8, excess `429`), a statement timeout, and scheduled runs executed **sequentially** by `ReportingWorker`. This stands in for NFR-PRF-7's "separate workers" | Records a deliberate reading of NFR-PRF-7 under ADR-010, so the ARCHITECTURE gate does not read it as an omission |
| **ADR-291** | **Customer definitions for growth reports.** An *active customer* in a period has ≥ 1 issued invoice in it. *New* = first issued invoice ever falls in the period; *returning* = had one before. *Acquisition source* = HO-ACC-001's `RegistrationChannel`. Merged customers count under the survivor (source contexts reassign, HO-ACC-001 ADR-039). Anonymised customers count but are never named beyond the stored placeholder | SRS §8.3 names the reports but not their definitions. Without one, two reports disagree on "new" |
| **ADR-292** | **Vaccination due list branch attribution.** Cats are not branch-scoped (HO-ADM-001 §8.2), so a cat belongs to the branch of its **most recent appointment**. Cats never seen are "Unattributed", included for `all` or with `includeUnattributed=true` | US-RPT-01 AC1 needs a branch filter on a list whose subject has no branch |

### 9.1 Open questions

| # | Question | Owner | Default in this build |
|---|---|---|---|
| OQ-1 | `.ai/bounded-contexts.md` needs the `Reporting` context. No `architect` design exists for this slice; the view-based approach should be reviewed | Architect | ADR-277, ADR-278 record it; the ARCHITECTURE gate reviews it |
| OQ-2 | Revenue on an **invoiced** (accrual) or **cash-received** basis? | Owner + Accountant | Invoiced (ADR-283). Cash received is visible in R13, R14 and the `payments.today` tile |
| OQ-3 | "Value at risk" for near-expiry: at landed cost or at retail price? | Owner | Both columns; totals and the dashboard tile at cost |
| OQ-4 | Stock valuation for batches with no landed cost yet | Accountant | Blank, excluded from totals, coverage shown (ADR-284). No PO-cost fallback |
| OQ-5 | May the Shop Manager see shop revenue (without cost or margin)? | Owner | Yes — R10 and R11 in Sales, cost columns restricted |
| OQ-6 | Who may run reports across **all branches**? | Owner | Administrator and Accountant |
| OQ-7 | Appendix D gives the Vet "R (clinical)" only, but SRS §8.1 lists the Vet as a primary user of the daily schedule | Owner | Vet gets `Reports.ClinicOperations.View` too |
| OQ-8 | Cohort retention: is a "repeat visit" a second completed appointment, and a "repeat purchase" a second shop order? | Owner | Yes; monthly cohorts; months 1–12 |
| OQ-9 | Cart recovery window after the abandoned-cart reminder | Owner (HO-ORD-001) | 7 days |
| OQ-10 | If a report misses NFR-PRF-3 on production-size data: materialised views? | Architect | Plain views. A materialised view would need an Amended row and ADR-278's escape hatch |
| OQ-11 | PDF fonts for Bengali-script customer names | Owner (with HO-CAT-001) | Use HO-CAT-001's renderer fonts. If Bengali renders as boxes, stop (§20.6) |
| ~~OQ-12~~ | **Closed 2026-09-23 — README C-5.** Generated exports go to [`HO-FIL-001`](HO-FIL-001.md) under the `ReportExport` purpose (PDF, CSV, ZIP; 50 MB), not `bytea`. **ADR-289 is amended**: the export store is `IFileStore`, and the 7-day retention becomes `RetainUntil` on the file row, enforced by HO-XCT-001's retention worker through HO-FIL-001's purge source. Attachments by reference (§11.7) now resolve to a `fileId` | — | — |
| OQ-13 | May schedules send to **external** addresses (an outside accountant)? Attaching financial data by email at all? | Owner | Staff accounts only; attachment as US-RPT-06 says, within the size cap |
| OQ-14 | **Cross-area data contracts** (§8.4) for HO-SHP-001, HO-IMP-001, HO-EMR-001, HO-PHM-001, HO-SRG-001, HO-BIL-001, HO-ORD-001, HO-BRD-001, HO-SUB-001 — especially the **ledger's source-line reference** (margin), the **invoice line kind** (business line), the **order-discount allocation** to lines, HO-IMP-001's **reorder suggestion as a callable pure service**, **stock locations carrying `BranchId`**, and **merge reassignment** in every context | Each owning handover's author | Assumed as §8.4 states. Any gap found at build time is a §20.2 stop |
| OQ-15 | Amended rows on HO-NTF-001 (§11.7), HO-ADM-001 (§10.2 settings) and HO-ACC-001 (`ActionRoles` additions), and this handover's ADR range in the README, cannot be written by this handover's author (parallel editing) | Orchestrator | Appended before Phase A (§5) |
| OQ-16 | Dashboard refresh interval | Owner | 60 s (`reporting.dashboardRefreshSeconds`), returned in the payload |
| OQ-17 | Card-in-store reconciliation without a terminal settlement report (SRS Q-3c) | Owner | R13 shows expected card totals with the note "No terminal settlement report to compare against (SRS Q-3c)" |
| OQ-18 | Fulfilment SLA for R27 | Owner (HO-ORD-001) | HO-ORD-001's setting; 24 h to dispatch if it defines none |

---

## 10. Enumerations, constants and violation codes

### 10.1 Enumerations — `Shared/Common/Enums/`, derived from `Enumeration`, persisted by `Value`

| Type | Members |
|---|---|
| `ReportCategory` | `ClinicOperations`, `Clinical`, `Boarding`, `Stock`, `Sales`, `Financial`, `Growth` |
| `ReportKind` | `Period`, `AsAt` |
| `ReportColumnType` | `Text`, `Integer`, `Decimal`, `Money`, `Percent`, `Date`, `Instant`, `Boolean` |
| `ReportFormat` | `Csv`, `Pdf` |
| `ReportRelativePeriod` | `Today`, `PreviousDay`, `PreviousWeek`, `PreviousMonth`, `MonthToDate`, `Last7Days`, `Last30Days` |
| `ReportFrequency` | `Daily`, `Weekly`, `Monthly` |
| `ReportScheduleStatus` | `Active`, `Paused`, `Cancelled` |
| `ScheduledReportRunStatus` | `Generated`, `Failed`, `Purged` |
| `ReportRunTrigger` | `Schedule`, `Manual` |
| `BusinessLine` | `Clinic`, `Shop`, `Boarding`, `Other` |
| `ExpiryBand` | `Expired`, `Within30Days`, `Within60Days`, `Within90Days` |
| `TimeBucket` | `Day`, `Week`, `Month` |

`InvoiceLineKind` and `PaymentMethod` are **HO-BIL-001's**. `BusinessLineRules` references them once HO-BIL-001
exists; Phase D is gated on it. If HO-BIL-001's payment method enumeration contains any gateway member, stop
(§20.7).

HO-NTF-001 amendment (§11.7): `NotificationSubjectKind` gains **`ScheduledReport`** (append only). A new
enumeration **`NotificationAttachmentKind`** is added, with the single member `ReportRun`.

### 10.2 Settings — appended to HO-ADM-001's `SettingDefinitions`

| Key | Group | Type | Default | Bounds | Branch override |
|---|---|---|---|---|---|
| `reporting.exportRetentionDays` | Reporting | Integer | 7 | 1–90 | ❌ |
| `reporting.maxAttachmentMegabytes` | Reporting | Integer | 10 | 1–20 | ❌ |
| `reporting.dashboardRefreshSeconds` | Reporting | Integer | 60 | 30–600 | ❌ |

No migration, because values are rows. Configuration (`appsettings.json`, section `Reporting`):
`QueryTimeoutSeconds` (30, 5–120, `ValidateOnStart`), `MaxConcurrentReports` (4), `ConcurrencyQueueLimit` (8),
`Worker:Enabled` (true; false in `Testing`), `Worker:IntervalSeconds` (60).

### 10.3 Other constants

`ReportCatalogue` (the 27 definitions of §8.3) · `ReportRules` (`MaxPageSize = 200`, `CsvMaxRows = 50_000`,
`PdfMaxRows = 5_000`, `DefaultMaxRangeDays = 366`, `CrossBranchRoles`) · `ReportScheduleRules` (`MaxRecipients =
20`, `MaxAttemptsPerSlot = 3`, `SendAtStepMinutes = 5`, `MaxSchedulesPerPass = 20`) · `DashboardTiles` (the tile
table of §8.11) · `BusinessLineRules` · `ReportingMessages` · the `report.scheduled-delivery` template definition
appended to `NotificationTemplateCatalogue` (§11.7).

### 10.4 Violation codes — append to `BusinessViolationCodes`

`UnknownReport` · `ReportRangeRequired` · `ReportRangeInverted` · `ReportRangeTooLong` · `InvalidReportParameter`
· `UnknownReportParameter` · `CrossBranchReportNotPermitted` · `ReportTooLargeForCsv` · `ReportTooLargeForPdf` ·
`ReportTimedOut` · `ReportFormatNotSupported` · `ReportScheduleNotFound` · `InvalidReportSchedule` ·
`InvalidReportSchedulePeriod` · `ReportRecipientRequired` · `ReportRecipientLimitExceeded` ·
`ReportRecipientNotPermitted` · `ReportRecipientHasNoEmail` · `ReportRecipientNotAssignedToBranch` ·
`ReportScheduleNotActive` · `ReportScheduleCancelled`.

`BranchNotAssigned` is HO-ADM-001's and is reused. Messages are plain language with the next step, e.g.
`ReportRangeTooLong`: *"This report covers at most 366 days. Choose a shorter date range."*

---

## 11. Specified changes — write side

### 11.1 Commands — `Application/Commands/Reporting/`

Namespace `CatEcommerce.Application.Commands.Reporting`. Data only; ids minted in the controller or worker;
`ValidateCommand = true` everywhere (HO-ACC-001 F3).

| # | Command | Sent by | Phase |
|---|---|---|---|
| C1 | `RecordReportExportCommand` (`ReportKey`, `Format`, `From`, `To`, `BranchId?`, `Parameters`, `RowCount`, `ColumnKeys`) | Export endpoints | A |
| C2 | `CreateReportScheduleCommand` (`ScheduleId`, `Name`, `ReportKey`, `BranchId?`, `Period`, `Parameters`, `Format`, `Frequency`, `DayOfWeek?`, `DayOfMonth?`, `LastDayOfMonth`, `SendAtLocal`, `RecipientIds`) | Administrator | G |
| C3 | `UpdateReportScheduleCommand` (same + `ExpectedVersion`) | Administrator | G |
| C4 | `PauseReportScheduleCommand` · `ResumeReportScheduleCommand` · `CancelReportScheduleCommand` (`ScheduleId`) | Administrator | G |
| C5 | `RecordScheduledReportRunCommand` (`RunId`, `ScheduleId`, `DueOn`, `Trigger`, `PeriodFrom`, `PeriodTo`, `TimeZoneId`, `Format`, `FileName`, `ContentType`, `Content` (bytes), `RowCount`, `ColumnKeys`) | Worker, run-now | G |
| C6 | `RecordFailedReportRunCommand` (`RunId`, `ScheduleId`, `DueOn`, `Trigger`, `FailureCode`) | Worker | G |
| C7 | `AdvanceReportScheduleCommand` (`ScheduleId`, `DueOn`) | Worker | G |
| C8 | `PurgeReportRunFileCommand` (`RunId`) | Worker | G |

A file on a command is safe **only** because dispatch is in-process (`.ai/cqrs.md`). It must not be logged. U-X4
checks the logs.

### 11.2 Validators — `CommandHandlers/Validators/` (flat), registered as `IValidator<T>`

Shape only: ids non-empty, name length, enum membership, `SendAtLocal` on the 5-minute grid, `DayOfMonth` 1–28,
exactly one of the monthly options, recipients 1–20 and distinct, file name `^[a-z0-9-]+\.(csv|pdf)$`, content
non-empty. Catalogue membership, parameter validity, recipient permission and branch assignment are handler
rules.

### 11.3 Mappers and DTOs

`ReportScheduleMapper` (static `ToDomainDto()` extensions). DTOs in `Domain/Models/`: `ReportScheduleDto`,
`ScheduledReportRunDto`, `ReportFileDto`, and `NotificationRequestDto`'s two new fields (§11.7).

### 11.4 Command handlers — `CommandHandlers/Reporting/`

| Handler | Specifics |
|---|---|
| **C1 Record export** | Creates an `AuditEntryAggregate` through HO-ACC-001's audit factory: category `BusinessEvent`, action `ReportExported`, entity type `Report`, entity id = a deterministic GUID from the key, `afterValue` = JSON of the command's filters, row count and column keys. **One aggregate** — the audit entry |
| **C2 / C3 Create / update** | Definition exists (`UnknownReport`); parameters valid (`InvalidReportParameter` / `UnknownReportParameter`); period compatible (`InvalidReportSchedulePeriod`); `BranchId` null only if the **caller** holds a cross-branch role (`CrossBranchReportNotPermitted`). Recipients: `Application` cannot reference `Read`, so the handler loads each `StaffAccountAggregate` by id through its repository (**read only, never mutated**), and HO-ADM-001's `StaffBranchAssignmentAggregate` for a branch schedule, then applies `ReportAccessPolicy` (Domain, pure) — §8.9's three recipient rules. Then `Create` + `SaveAsync` / `Update` + `UpdateAsync` |
| **C4** | State transitions; `ReportScheduleCancelled` on anything after cancel |
| **C5 Record run** | §8.9 step 6. Loads the schedule (read) for its recipients, branch and name. `23505` on `ix_scheduled_report_run_slot` → **success, nothing written** (the save that failed carried the duplicate run, its file and its requests together, so none of them persist) |
| **C6** | Creates a `Failed` run; no file, no enqueue |
| **C7** | `RecordRunCompleted(dueOn, now)`; idempotent |
| **C8** | `IReportFileStore.Remove(runId)` (Reporting's own table) and `run.Purge()`; idempotent |

### 11.5 Domain events

**None.** Reporting raises no domain events and handles none (§8.4 "Contract events"). HO-ACC-001's interceptor
audits every change to the two roots.

### 11.6 Domain event handlers

**None.** Subscribing to another context's events to maintain a reporting table is forbidden (ADR-278).

### 11.7 Amendment to HO-NTF-001 — attachments by reference (ADR-288)

HO-NTF-001 sends no attachments today (§8.9 there: `OutgoingMessage` has none). US-RPT-06 needs one. This slice
builds the following **inside HO-NTF-001's files, limited to the lines listed in §13**. HO-NTF-001's §1 Amended
row must record it (OQ-15).

1. **Request and notification**: `NotificationRequestAggregate`, `NotificationRequestDto` and
   `NotificationAggregate` gain `AttachmentKind?` (`NotificationAttachmentKind`) and `AttachmentId?` (Guid).
   `ExpandNotificationRequestCommandHandler` (HO-NTF-001 C4) copies them from request to notification.
   Migration `AddNotificationAttachments` adds two nullable columns to each table. **No attachment bytes ever
   enter `notify.*`.**
2. **Port** `INotificationAttachmentSource` (Domain.DomainServices): `NotificationAttachmentKind Kind`;
   `Task<NotificationAttachment?> LoadAsync(Guid id, CancellationToken)` → `(FileName, ContentType, byte[]
   Content)`. `NotificationAttachmentResolver` picks the source by kind. Reporting's implementation is
   `ReportRunAttachmentSource` (Infrastructure/Reporting, `AsNoTracking`, reads `report.report_file`).
3. **Sending**: `OutgoingMessage` (declared in `INotificationChannelSender.cs`) gains
   `IReadOnlyList<OutgoingAttachment> Attachments` (empty by default). `DeliverNotificationCommandHandler`
   (HO-NTF-001 C5) loads the attachment immediately before `SendAsync`, **for the `Email` channel only**. A
   missing attachment (purged) → the notification is `Cancelled` with `SubjectNoLongerApplies`, never sent
   without it.
4. **Senders**: `DevelopmentChannelSender` logs attachment **file name and size only**. `CapturingChannelSender`
   records attachments for assertions. `UnconfiguredChannelSender` is unchanged. **The provider-adapter contract
   (HO-NTF-001 §8.9) gains rule 7**: attachments are sent as MIME parts and never logged.
5. **Template** `report.scheduled-delivery`, appended to `NotificationTemplateCatalogue`: `Transactional`,
   **`Normal`** urgency (quiet hours apply — the preview endpoint says when delivery will be held, §12.4),
   channels **`Email` only**, `ContainsSecret = false`. Variables: `reportTitle`, `periodLabel`, `branchName`,
   `generatedOnLocal`, `rowCount` (Text), `attachmentNote` (Text, empty or *"The export was too large to attach
   — open the report in PurrCare."*), `reportLink` (Url, **required on Email**).
6. **Guard**: `NotificationSubjectKind.ScheduledReport` → `ScheduledReportGuard` (Infrastructure/Notifications/
   Guards). It is valid while the run exists and is `Generated`, **and** the recipient staff account is active
   and still holds the report's category action, **and** — if the run includes a restricted column — the
   restricted action too. It reads only.
7. **Link**: `INotificationLinkBuilder.ReportLink(reportKey, from, to, branchId?)` →
   `{WebBaseUrl}/staff/reports/{reportKey}?from={from}&to={to}&branchId={id|all}`.

U-G6 … U-G9 and I-G2 … I-G8 test the amendment.

---

## 12. Specified changes — Infrastructure, read side, Api, host

### 12.1 Persistence

**Tables** (schema `report`; HO-ACC-001 §12.1 conventions; the F15 lines; `ValueGeneratedNever()`; enumerations
by `Value`; instants `timestamptz`):

| Index / constraint | Kind |
|---|---|
| `report_schedule (status, next_run_on)` | due-schedule lookup |
| `scheduled_report_run (schedule_id, due_on)` **UNIQUE** filtered `WHERE trigger = Schedule AND status IN (Generated, Purged)` | `ix_scheduled_report_run_slot` — exactly once (ADR-287). Enumeration values written from the enumeration in the migration code, never as literals (U-X3) |
| `scheduled_report_run (schedule_id, generated_on)` | run history |
| `scheduled_report_run (status, generated_on)` | purge pass |
| `report_file (run_id)` PK | one file per run |

**Views**: each `report.rv_*` in §8.4 is one embedded SQL file,
`Persistence/ReportingViews/Sql/rv_<name>.v<n>.sql`, run by `migrationBuilder.Sql(...)`. The `Down` drops it.
Changing a view is a **new file version and a new migration** — an applied migration's SQL never changes
(ADR-278). The keyless row types (`Persistence/ReportingViews/*Row.cs`) and their configurations
(`Configurations/Reporting/*ViewConfiguration.cs`) use `HasNoKey().ToView("rv_…", "report")`, with every column
mapped explicitly: money as `decimal`, dates as `DateOnly`, instants as `DateTimeOffset`.

**Migrations**, each **read before commit** (a generated `CreateTable` for any `rv_*` is a configuration bug,
F-RPT-3):

| Migration | Phase | Contents |
|---|---|---|
| `AddReportingSchema` | A | `CREATE SCHEMA report`; `rv_staff`, `rv_customer` |
| `AddClinicReportViews` | B | `rv_clinic_appointment`, `rv_cat_vaccination`, `rv_cat_parasite_treatment`, `rv_cat_last_branch`, `rv_care_reminder_sent`, `rv_surgery`, `rv_visit_open` |
| `AddStockReportViews` | C | `rv_stock_ledger`, `rv_batch`, `rv_stock_location`, `rv_reorder_rule`, `rv_consignment`, `rv_sku` |
| `AddFinancialReportViews` | D | `rv_invoice_line`, `rv_credit_note_line`, `rv_payment`, `rv_reconciliation`, `rv_cash_up`, `rv_invoice_balance` |
| `AddGrowthReportViews` | F | `rv_subscription_change`, `rv_cart`, `rv_order`, `rv_customer_activity` |
| `AddReportSchedules` | G | the three tables and their indexes |
| `AddNotificationAttachments` | G | the HO-NTF-001 amendment's four columns |
| `AddOperationsReportViews` | H | `rv_boarding_unit`, `rv_boarding_unit_block`, `rv_boarding_night`, `rv_boarding_movement`, `rv_care_task`, `rv_incident` |

**No migration touches a table outside `report`**, except `AddNotificationAttachments` (the amendment). U-X5
reads every Reporting migration's operations and fails on any other `AlterTable`, `CreateIndex` or `AddColumn`
outside `report` (§20.4).

### 12.2 Infrastructure services and the worker

| Class | Notes |
|---|---|
| `Reporting/ReportingReadScope : IReportingReadScope` | §8.5 |
| `Reporting/ReportCsvWriter`, `Reporting/QuestPdfReportRenderer` (both behind `IReportExportRenderer`) | §8.12 |
| `Reporting/EfReportFileStore : IReportFileStore` | §8.10 |
| `Reporting/ReportRunAttachmentSource : INotificationAttachmentSource` | §11.7 |
| `Notifications/Guards/ScheduledReportGuard` | §11.7 |
| `Notifications/NotificationLinkBuilder` | `ReportLink` added |
| `Notifications/NotificationAttachmentResolver` | §11.7 |

**`ReportingWorker : BackgroundService`** (Api, `Background/`) — the same shape and rules as HO-NTF-001's
`NotificationWorker`: a scope per unit of work, never throws out of `ExecuteAsync`, disabled in `Testing`, and a
`ReportingWorkCycle` that tests can call. One pass every `Reporting:Worker:IntervalSeconds`: run due schedules
(§8.9), then purge expired files (§8.10). **Sequential** — one report at a time (ADR-290).

### 12.3 Authorization

Appended to the one `ActionRoles` table (HO-ACC-001 §12.8, HO-ADM-001 §12.3). Never a second table:

| Action | Staff roles | Endpoints |
|---|---|---|
| `Reports.ClinicOperations.View` | Reception, Vet, Administrator | reports R1, R2, R4 (view and export) |
| `Reports.Clinical.View` | Vet, Administrator | R3 |
| `Reports.Boarding.View` | Caretaker, Administrator | R24–R26 |
| `Reports.Stock.View` | Inventory, Administrator | R5–R8 |
| `Reports.Sales.View` | ShopManager, Administrator | R10, R11, R23, R27 |
| `Reports.Financial.View` | Accountant, Administrator | R9, R12–R17; restricted columns |
| `Reports.Growth.View` | Administrator | R18–R22 |
| `Reports.Any` *(marker)* | Reception, Vet, Caretaker, Inventory, ShopManager, Accountant, Administrator — **not** Nurse | every `/api/staff/reports/**` endpoint |
| `Reports.Schedule` | Administrator | every `/api/admin/report-schedules/**` endpoint |
| `Dashboard.View` | Reception, Nurse, Vet, Caretaker, Inventory, ShopManager, Accountant, Administrator | `GET /api/staff/dashboard` |

**The per-report action is dynamic**, because the route carries `{reportKey}`. The report endpoints carry
`[RequireAction(Actions.ReportsAny)]` — a marker mapped to every role holding at least one report category — so
the fail-closed filter and HO-ADM-001's `EndpointAccessConventionTests` still see a declared, mapped action. The
query handler then enforces the report's own category action through `ReportAccessPolicy`. A caller without it
gets **the same `403 ActionNotPermitted` body** (HO-ACC-001 R6), and the denial is audited by dispatching
HO-ACC-001's denial-audit command (C11 there), exactly as the filter does. `404 UnknownReport` is returned
**only** for keys that are not in the catalogue. U-A7 and I-A6 prove the two layers agree.

**Rate and concurrency.** Report view and export endpoints use the `reports` concurrency policy (ADR-290): one
permit per request, `MaxConcurrentReports`, queue `ConcurrencyQueueLimit`, `429` + `Retry-After: 5` beyond it,
with HO-ACC-001's R4 body shape. `run-now` also uses `report-run-now`: a fixed window of 5 per 10 minutes per IP
(HO-ACC-001 ADR-030's shape).

### 12.4 Api — the contract HO-RPT-002 consumes

JSON camelCase. Dates `yyyy-MM-dd` (branch-local). Instants ISO-8601 with offset. Money as `{ "amount":
"1500.00", "currencyCode": "BDT" }` (HO-ADM-001 §12.4). Percentages as strings with 1 dp (`"12.5"`). Integers as
JSON numbers. **A value the system does not record is `null`, never `0`** (HO-ACC-002 ADR-FE-012's rule, now on
the server).

**Reports**

| Method | Route | Access | Returns |
|---|---|---|---|
| `GET` | `/api/staff/reports` | `Reports.Any` | The catalogue **filtered to the caller**: `[{ key, title, description, category, kind, timeBucketed, defaultPeriod, defaultRange: { from, to }, maxRangeDays, formats, parameters: [{ name, label, type, allowedValues?, min?, max?, default }], columns: [{ key, label, type, unit?, sortable }], canUseAllBranches, canSchedule }]`. `defaultRange` is `defaultPeriod` resolved server-side for the active branch, so the client never computes dates. Restricted columns the caller cannot see are absent |
| `GET` | `/api/staff/reports/{reportKey}?from&to&branchId&groupBy&sort&pageNumber&pageSize&{parameters}` | `Reports.Any` + the report's action · `reports` | `ReportResult`: `{ reportKey, title, generatedOn, filters: { from, to, branchId \| null, branchName \| "All branches", timeZoneId, groupBy?, parameters }, columns: [...visible...], rows: [{ <columnKey>: value }], totals: { <columnKey>: value } \| null, notes: [{ code, text }], coverage: { costedRevenue, totalRevenue, percent } \| null, pageNumber, pageSize, totalCount }` — `pageSize` ≤ 200; `sort` = a sortable column key with optional `-` prefix |
| `GET` | `/api/staff/reports/{reportKey}/export.csv?…same…` | same · `reports` | C1, then `text/csv; charset=utf-8`, attachment, `no-store`; `413 ReportTooLargeForCsv` |
| `GET` | `/api/staff/reports/{reportKey}/export.pdf?…same…` | same · `reports` | C1, then `application/pdf`; `413 ReportTooLargeForPdf` |

Errors: `400` (`ReportRangeRequired`, `ReportRangeInverted`, `ReportRangeTooLong`, `InvalidReportParameter`,
`UnknownReportParameter`, `ReportTimedOut`), `403` (`ActionNotPermitted`, `BranchNotAssigned`,
`CrossBranchReportNotPermitted`), `404 UnknownReport`, `429`.

**Dashboard**

| Method | Route | Access | Returns |
|---|---|---|---|
| `GET` | `/api/staff/dashboard` | `Dashboard.View` | `{ branchId, branchName, localDate, timeZoneId, generatedOn, refreshSeconds, tiles: [{ key, title, figures: [{ key, label, value, type, unit? }], drillDown: { reportKey, from, to, parameters } \| null, note? }] }` — uses `X-Branch-Id` (HO-ADM-001 §8.3) |

**Schedules** (administrator)

| Method | Route | Access | Command / query → success |
|---|---|---|---|
| `GET` | `/api/admin/report-schedules?status&reportKey&pageNumber&pageSize` | `Reports.Schedule` | paged: `id, name, reportKey, reportTitle, branchName, frequencyLabel, sendAtLocal, timeZoneId, format, recipientCount, status, nextRunOn, lastRunOn, lastRunStatus` |
| `GET` | `/api/admin/report-schedules/{id}` | `Reports.Schedule` | detail incl. `version`, parameters, recipients `[{ id, name, emailPresent, stillPermitted }]` |
| `POST` | `/api/admin/report-schedules` | `Reports.Schedule` | C2 → `201 { scheduleId, nextRunOn }` |
| `PUT` | `/api/admin/report-schedules/{id}` | `Reports.Schedule` | C3 → `200 { nextRunOn }`; `409` stale version |
| `POST` | `/api/admin/report-schedules/{id}/pause` · `/resume` · `/cancel` | `Reports.Schedule` | C4 → `204` |
| `POST` | `/api/admin/report-schedules/{id}/run-now` | `Reports.Schedule` · `report-run-now` | the §8.9 sequence with `Trigger = Manual`, completed inside the request (ADR-014) under the statement timeout → `200 { runId, status, rowCount }` |
| `POST` | `/api/admin/report-schedules/preview` | `Reports.Schedule` | body = a draft schedule → `{ nextRuns: [3 instants], periodForNextRun: { from, to }, deliveryHeldUntil?: instant }` (quiet hours, HO-NTF-001 ADR-059); nothing written. **The client never computes a schedule** |
| `GET` | `/api/admin/report-schedules/{id}/runs?pageNumber&pageSize` | `Reports.Schedule` | `runId, dueOn, trigger, status, periodFrom, periodTo, format, rowCount, contentLength, attachmentOmitted, failureCode, generatedOn, purgedOn, deliveries: [{ recipientName, status }]` (from HO-NTF-001's notification rows, read) |
| `GET` | `/api/admin/report-schedules/recipients?reportKey&branchId` | `Reports.Schedule` | eligible staff: `id, name, roles, emailPresent` — only those who hold the report's action (and the branch) |

Every write uses HO-ACC-001's `400` / `401` / `403` / `409` shapes (R1–R8 there).

### 12.5 Filters

- **`ReportTimedOutExceptionFilter`**: `ReportTimedOutException` → `400` with `ReportTimedOut`, message *"This
  report took too long to run. Choose a shorter date range or a single branch."* A `PostgresException` `57014`
  raised anywhere else is **not** mapped — it rethrows.
- `UniqueConstraintExceptionFilter` is **not** extended. The slot index is handled inside C5 (success on
  duplicate).

### 12.6 Read side — `src/Read/`

Queries in `Queries/Reporting/`, handlers in `QueryHandlers/Reporting/`, ViewModels flat.

- **`RunReportQuery`** (`ReportKey`, filters, parameters, `ExportMode`, `ReportAccess?`) → `ReportResult`. The
  handler validates filters (`ReportFilterValidator`), builds `ReportAccess` from the caller when not supplied
  (the worker supplies it — §8.9), resolves the one `IReportSource` for the key, and runs it inside
  `IReportingReadScope`. In export mode, paging is replaced by the format cap + 1, so the handler can answer
  `413` without loading everything.
- **`IReportSource`** — one class per key in `QueryHandlers/Reporting/Sources/`: `Task<ReportResult>
  RunAsync(ReportRequest, CatEcommerceDbContext, CancellationToken)`. Sources `Select` from view `DbSet`s with
  explicit projections, aggregate in SQL where possible, and call the pure domain services named in §8.3
  (HO-CAT-001's vaccination calculator, HO-IMP-001's reorder suggestion) on the reduced rows. **Never** a
  repository.
- **`GetReportCatalogueQuery`** → the caller's catalogue (§12.4).
- **`GetDashboardQuery`** → tiles via `IDashboardTileSource` (one per tile key, `QueryHandlers/Reporting/
  Dashboard/`), each gated before invocation (§8.11).
- Schedules: `GetReportSchedulesQuery`, `GetReportScheduleQuery`, `GetReportScheduleRunsQuery`,
  `GetEligibleReportRecipientsQuery`, `PreviewReportScheduleQuery`, and the worker's `GetDueReportSchedulesQuery`
  and `GetPurgeableReportRunsQuery`. These read Reporting's own tables with `AsNoTracking()`.

### 12.7 Host — `Program.cs`

Register every command handler, validator (as `IValidator<T>`), query handler, `IReportSource` (27) and
`IDashboardTileSource` (21) explicitly — **count them**, because there is no assembly scanning. I-A1 proves they
resolve, and U-A2 proves the count matches the catalogue. Also register: `IReportingReadScope` (Scoped),
`IReportFileStore` (Scoped), `IReportExportRenderer` implementations (Singleton — stateless), `IReportCatalogue`
(Singleton), `INotificationAttachmentSource` → `ReportRunAttachmentSource` (Scoped),
`NotificationAttachmentResolver` (Scoped), `ScheduledReportGuard` into HO-NTF-001's `SubjectGuardResolver`, the
template via the catalogue, `ReportingWorkCycle` (Scoped), **`AddHostedService<ReportingWorker>()`**, the
`reports` concurrency policy and the `report-run-now` policy, `Reporting` options with `ValidateOnStart`, and the
`ReportTimedOutExceptionFilter`. `ValidateScopes`/`ValidateOnBuild` stay on. **No new package.**

---

## 13. Allowed scope

```
src/Shared/CatEcommerce.Shared/Common/Enums/{ReportCategory,ReportKind,ReportColumnType,ReportFormat,ReportRelativePeriod,ReportFrequency,ReportScheduleStatus,ScheduledReportRunStatus,ReportRunTrigger,BusinessLine,ExpiryBand,TimeBucket}.cs
src/Shared/CatEcommerce.Shared/Common/Enums/NotificationSubjectKind.cs                              ← ScheduledReport only
src/Shared/CatEcommerce.Shared/Common/Enums/NotificationAttachmentKind.cs                           ← new (HO-NTF-001 amendment)
src/Shared/CatEcommerce.Shared/Common/Constants/{ReportCatalogue,ReportDefinition,ReportRules,ReportScheduleRules,DashboardTiles,BusinessLineRules,ReportingMessages}.cs
src/Shared/CatEcommerce.Shared/Common/Constants/{Actions,ActionRoles,BusinessViolationCodes}.cs     ← append only
src/Shared/CatEcommerce.Shared/Common/Constants/SettingDefinitions.cs                               ← the three keys of §10.2 only
src/Shared/CatEcommerce.Shared/Common/Constants/NotificationTemplateCatalogue.cs                    ← report.scheduled-delivery only
src/Domain/CatEcommerce.Domain/Aggregates/Reporting/**
src/Domain/CatEcommerce.Domain/Aggregates/Notification/{NotificationRequestAggregate,NotificationAggregate}.cs ← AttachmentKind, AttachmentId only
src/Domain/CatEcommerce.Domain/DomainServices/{ReportPeriodResolver,ReportScheduleCalculator,ReportAccessPolicy,IReportFileStore,IReportCatalogue,INotificationAttachmentSource}.cs
src/Domain/CatEcommerce.Domain/DomainServices/INotificationLinkBuilder.cs                           ← ReportLink only
src/Domain/CatEcommerce.Domain/DomainServices/INotificationChannelSender.cs                         ← OutgoingAttachment + OutgoingMessage.Attachments only
src/Domain/CatEcommerce.Domain/Models/**                                                           ← new DTOs; NotificationRequestDto gains the two attachment fields
src/Application/CatEcommerce.Application/Commands/Reporting/**
src/Application/CatEcommerce.Application/CommandHandlers/Reporting/**
src/Application/CatEcommerce.Application/CommandHandlers/Validators/{one per command in §11.1}.cs
src/Application/CatEcommerce.Application/CommandHandlers/Notification/ExpandNotificationRequestCommandHandler.cs ← copy attachment fields only
src/Application/CatEcommerce.Application/CommandHandlers/Notification/DeliverNotificationCommandHandler.cs     ← attachment load and missing-attachment cancel only
src/Application/CatEcommerce.Application/DataMappers/ReportScheduleMapper.cs
src/Read/CatEcommerce.Read/Queries/Reporting/**
src/Read/CatEcommerce.Read/QueryHandlers/Reporting/**                                               ← incl. Sources/, Dashboard/
src/Read/CatEcommerce.Read/ViewModels/{Report*,Dashboard*}.cs
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/CatEcommerceDbContext.cs                 ← DbSets only
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Configurations/{ReportScheduleConfiguration,ScheduledReportRunConfiguration,ReportFileRecordConfiguration}.cs
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Configurations/Reporting/**             ← keyless view configurations
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Configurations/{NotificationRequestConfiguration,NotificationConfiguration}.cs ← attachment columns only
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/ReportingViews/**                       ← row types + versioned .sql files (embedded)
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Migrations/**                           ← the eight migrations of §12.1 only
src/Infrastructure/CatEcommerce.Infrastructure/Reporting/**
src/Infrastructure/CatEcommerce.Infrastructure/Notifications/Guards/ScheduledReportGuard.cs
src/Infrastructure/CatEcommerce.Infrastructure/Notifications/NotificationAttachmentResolver.cs
src/Infrastructure/CatEcommerce.Infrastructure/Notifications/NotificationLinkBuilder.cs            ← ReportLink only
src/Infrastructure/CatEcommerce.Infrastructure/Notifications/Senders/{DevelopmentChannelSender,CapturingChannelSender}.cs ← attachment metadata only
src/Infrastructure/CatEcommerce.Infrastructure/CatEcommerce.Infrastructure.csproj                   ← EmbeddedResource for Persistence/ReportingViews/Sql/*.sql only
src/Api/CatEcommerce.Api/Controllers/{StaffReports,StaffDashboard,AdminReportSchedules}Controller.cs
src/Api/CatEcommerce.Api/Contracts/Reporting/**
src/Api/CatEcommerce.Api/Background/{ReportingWorker,ReportingWorkCycle}.cs
src/Api/CatEcommerce.Api/RateLimiting/RateLimitPolicies.cs                                          ← reports, report-run-now only
src/Api/CatEcommerce.Api/Filters/ReportTimedOutExceptionFilter.cs
src/Api/CatEcommerce.Api/appsettings.json                                                           ← Reporting section only
src/Api/CatEcommerce.Api/Program.cs                                                                 ← §12.7 only
tests/CatEcommerce.Domain.Tests/**
tests/CatEcommerce.Application.Tests/**
tests/CatEcommerce.IntegrationTests/**
docs/test-cases/TC-HO-RPT-001.md
docs/gates/HO-RPT-001-*.md
.ai/decisions/adr.md                                                                                ← ADR-277…292, append only
```

The HO-NTF-001 amendment is small, but it touches another area's aggregates, two of its handlers and its sender
contract. Its migration is `AddNotificationAttachments`, its tests are U-G6…U-G9 here, and HO-NTF-001's §1 Amended
row records it (OQ-15).

## 14. Forbidden scope

| Path | Why |
|---|---|
| `.ai/**` except `decisions/adr.md`, `.claude/**` | Governing docs and agents (OQ-1) |
| `docs/SRS.md`, `docs/USER_STORIES.md`, `docs/handover/**` | Inputs |
| `web/**` | HO-RPT-002 |
| **Every aggregate, configuration, migration, command, handler and event of another area** — Account, Administration, Cat, Clinic (appointments, visits), Surgery, Pharmacy, Shop, Inventory, Billing, Orders, Boarding, Subscriptions — and every HO-NTF-001 file beyond the lines in §13 | ADR-277. Reporting reads them through views and changes none of them |
| Any DDL (index, column, trigger, constraint) on a schema other than `report`, except `AddNotificationAttachments` | §4.4, §20.4 |
| `audit.audit_entry` columns and triggers | HO-ACC-001's; C1 writes rows through its aggregate only |
| A second `DbContext`, a materialised view, a projection table, a domain event handler | ADR-011, ADR-278 |
| `NuGet.Config`, `global.json`, `*.sln`, any `*.csproj` other than the one embedded-resource line | No package is added |

---

## 15. Unit test cases

`tests/CatEcommerce.Domain.Tests` / `Application.Tests`. Infrastructure and Api classes as plain unit tests in
`IntegrationTests`.

| ID | Case | Expected |
|---|---|---|
| **A — framework** | | |
| U-A1 | Catalogue convention over every definition | Unique key matching the pattern; category set; category action present in `ActionRoles`; ≥ 1 column; both formats; `MaxRangeDays > 0`; **accepts `from`, `to`, `branchId`** — FR-RPT-1, **US-RPT-01 AC2** |
| U-A2 | Reflection over `QueryHandlers/Reporting/Sources/**` and `Dashboard/**` against the catalogue and `DashboardTiles` | Exactly one `IReportSource` per key and one `IDashboardTileSource` per tile; none extra |
| U-A3 | Source-text and reflection scan of `QueryHandlers/Reporting/**` and `Infrastructure/Reporting/**` (except `EfReportFileStore`) | No `IAggregateRootRepository<`, `SaveChanges`, `ExecuteUpdate`, `ExecuteDelete`, `ExecuteSql`, `FromSql` — ADR-279 |
| U-A4 | `ReportFilterValidator`: no range; `to < from`; 367 days for a 366-day report; unknown parameter; `Enum` value not allowed; integer out of bounds | `ReportRangeRequired`, `ReportRangeInverted`, `ReportRangeTooLong`, `UnknownReportParameter`, `InvalidReportParameter` ×2 |
| U-A5 | `ReportPeriodResolver` range → instants: Dhaka `2026-10-01…2026-10-31`; `Europe/London` over the October DST change; `all` uses the main branch zone | `[2026-09-30T18:00Z, 2026-10-31T18:00Z)`; the 25-hour Sunday handled; main zone applied |
| U-A6 | Relative periods at `2026-10-14T03:00+06:00` (Wednesday) | `PreviousDay` 13 Oct; `PreviousWeek` 5–11 Oct; `PreviousMonth` Sep; `MonthToDate` 1–14 Oct; `Last7Days` 7–13 Oct; `Last30Days` 14 Sep–13 Oct |
| U-A7 | `ReportAccessPolicy`: Reception + R1; Reception + R9; Nurse + any report; Vet + R1; Vet + R3; Accountant + `all`; Reception + `all`; unassigned branch | allow; deny; deny; allow (OQ-7); allow; allow; `CrossBranchReportNotPermitted`; `BranchNotAssigned` |
| U-A8 | Column restriction: ShopManager vs Accountant on R10 | ShopManager's columns, totals and notes have **no** cost, margin or coverage keys; Accountant's have all |
| U-A9 | `ReportCsvWriter` with `=HYPERLINK(...)`, `+1`, `-1`, `@x`, a tab-leading cell, a Bengali name, `null`, money `1500.5` | Guarded with `'`; BOM present; CRLF; Bengali intact; empty cell; `1500.50` |
| U-A10 | `QuestPdfReportRenderer` with 4 and 9 columns | Non-empty `%PDF-`; title, period, zone and branch present in the text; portrait / landscape |
| U-A11 | Export-mode request over the cap | `ReportTooLargeForPdf` / `…Csv` raised **before** rendering (cap + 1 rows fetched, not all) |
| U-A12 | C1 audit payload | Key, format, filters, row count, column keys; **no row values** |
| **B — clinic** | | |
| U-B1 | R1 source over rows including `Held`, `HoldExpired`, `HoldReleased` | None appear; totals count booked, checked in, completed, cancelled, no-show and walk-ins correctly |
| U-B2 | R2: walk-in checked in 09:00, started 09:35; one checked in then cancelled; today vs yesterday | Wait 35; left-without-being-seen 1; `waitingNow` set today and `null` yesterday |
| U-B3 | R4 with two records of one vaccine (renewed), a deceased cat, a merged owner, an inactive vaccine | Only the latest record, via `CatVaccinationStatusCalculator` (spy called); the others excluded |
| U-B4 | R4 attribution: cat last seen at branch B; cat never seen | Listed under B; unattributed unless `all` or `includeUnattributed` — ADR-292 |
| U-B5 | R4 `includeParasiteControl` with a `Combined` treatment | Counted as deworming (HO-NTF-001 F25) |
| **C — stock** | | |
| U-C1 | R5 roll-forward per batch × location: receipts, sales, transfer out / in, adjustment, return | opening + in − out ± adjustments = closing; a transfer is out at one location and in at the other |
| U-C2 | R5 valuation with one batch without landed cost | That row's valuation `null`; totals exclude it; coverage note present |
| U-C3 | R6 bands at 91, 90, 61, 60, 31, 30, 0, −1 days | none, 90, 90, 60, 60, 30, 30, Expired |
| U-C4 | R7 | HO-IMP-001's reorder calculation invoked once per row (spy); the source contains no reorder arithmetic of its own |
| **D — money** | | |
| U-D1 | `BusinessLineRules.Classify` over every `InvoiceLineKind` member, plus a synthetic unknown | Mapped per §8.6; `Deposit` and `Voucher` excluded; unknown → `Other` |
| U-D2 | Revenue: a draft, an issued, a paid and a credited invoice; a credit note next month | Draft excluded; three counted; credit note negative in **its** month; tax excluded |
| U-D3 | Margin: fully costed line; line with one uncosted batch; all uncosted | Margin on the first only; coverage = costed ÷ total; 0 % coverage → margin `null`, not 0 |
| U-D4 | Reflection over `Persistence/ReportingViews/*Row`, `ViewModels/Report*`, `Dashboard*` | No `double` or `float` property anywhere |
| U-D5 | R13 method rows | Exactly `CashOnDelivery`, `BankTransfer`, `CashInStore`, `CardInStore` (+ `StoreCredit` when present); card row carries the Q-3c note; no other method |
| U-D6 | R15 ageing boundaries 30 / 31 / 60 / 61 / 90 / 91 days | 0–30, 31–60, 31–60, 61–90, 61–90, > 90 |
| U-D7 | Two currencies in one period (synthetic) | Two groups; never summed |
| **E — dashboards** | | |
| U-E1 | Tile set for each role; for Vet + Administrator | Per §8.11; union deduplicated, ordered by the first role in `StaffRoles.All` |
| U-E2 | Caller lacking a tile's gate action | That tile's source **never invoked** (spy) and absent |
| U-E3 | Nurse | Clinic tiles only; no stock, sales, payment or revenue tile |
| U-E4 | A tile whose source module is not registered (test catalogue) | Absent, not zero |
| **F — growth** | | |
| U-F1 | Customer whose first invoice is in the period; one with an earlier invoice; one with registrations only | new; returning; counted in registrations, not active — ADR-291 |
| U-F2 | Cohort matrix over three monthly cohorts | Offsets 1…12 correct; incomplete months `null` |
| U-F3 | Cross-line sets: clinic-only, clinic + shop, boarding + clinic | Matrix percentages per SRS §8.3 |
| U-F4 | Subscription churn = cancelled in period ÷ active at start | As stated; reasons grouped by `reason_code` |
| **G — schedules and delivery** | | |
| U-G1 | `ReportScheduleCalculator`: daily 07:00 Dhaka; weekly Monday; monthly 28th; last day of February 2027; London 01:30 on spring-forward day; London 01:30 on fall-back day | Correct instants; the skipped time moves to 02:00 BST; the repeated time takes the first (BST) occurrence |
| U-G2 | `RecordRunCompleted` called twice for one `dueOn`; after 3 days of downtime | Second call no-op; **one** next slot after now (missed collapsed) |
| U-G3 | Schedule invariants: 0 and 21 recipients, duplicate recipient, `DayOfMonth` 29, an incompatible period, unknown key | Each code |
| U-G4 | Visible-column intersection: ShopManager + Accountant recipients on R10 | No cost columns; Accountant + Administrator → all columns |
| U-G5 | `ScheduledReportGuard`: recipient still permitted; recipient's roles changed; account inactive; run purged | valid; invalid; invalid; invalid |
| U-G6 | `report.scheduled-delivery` definition | Passes HO-NTF-001 `TemplateRules`; `Email` only; `Transactional`; `Normal`; not secret; `reportLink` required |
| U-G7 | `DeliverNotificationCommandHandler` with an `AttachmentId` whose file exists / was purged | `OutgoingMessage.Attachments` has one item / notification `Cancelled SubjectNoLongerApplies` |
| U-G8 | Audit change capture during C5 | No audit row contains the file bytes or any substring of the CSV content |
| U-G9 | `DevelopmentChannelSender` with an attachment | Logs file name and size; **never** content |
| **H — operations** | | |
| U-H1 | R24: 4 units × capacity 2, one unit blocked 2 days, 5 cat-nights on a day | Capacity 6 that day; occupancy 83.3 %; future nights flagged forecast |
| U-H2 | R25: task done inside, after and never within its window | on time; late; missed |
| U-H3 | R27 ageing and breach at SLA 24 h | 23 h not breached; 25 h breached |
| **X — cross-cutting** | | |
| U-X1 | `SettingDefinitions` additions | Defaults within bounds; keys unique |
| U-X2 | `ReportingWorker` loop with a throwing pass | Logged; the next pass runs |
| U-X3 | Every keyless view entity; the slot index predicate | Maps `ToView(…, "report")` to a view created by a Reporting migration; predicate uses `ReportRunTrigger.Schedule.Value` etc., never literals |
| U-X4 | Log capture across C5 and every report source | No file content, no row data, no customer contact detail at `Information`+ |
| U-X5 | Operations of every Reporting migration | Nothing outside schema `report`, except `AddNotificationAttachments`' four columns — ADR-277 |

## 16. Integration test cases

Real HTTP, `Testing` environment, shared database (HO-ACC-001 §16.1 rules 1–8, HO-APT-001 §16.1). Workers off;
tests call `ReportingWorkCycle` and HO-NTF-001's `NotificationWorkCycle`, advancing `FakeTimeProvider`.

### 16.1 Additional shared-database rules

1. **Every test creates its own branch** (HO-ADM-001 endpoints as an administrator) and assigns its staff to it.
   Every report assertion passes `branchId={that branch}`. **Never assert on an all-branches total** — other
   tests' data is in it.
2. Growth reports cannot be branch-scoped by customer, so their tests assert **only on customers the test
   created**, by filtering the top-N and matrix rows to those ids, or on a delta before and after.
3. Source data is created **through the owning area's own endpoints or command handlers**, never by inserting
   into their tables — the same rule Reporting follows.
4. The CSV and PDF caps and `QueryTimeoutSeconds` are lowered in the factory **only** in the cases that test them.

| ID | Case | Expected |
|---|---|---|
| **A — framework** | | |
| I-A1 | Host starts, scope validation on | Every registration resolves, including 27 sources and 21 tile sources |
| I-A2 | Migrations on an empty database | Schema `report`; every §8.4 view in `pg_views WHERE schemaname = 'report'`; the three tables; no `rv_*` in `pg_tables` |
| I-A3 | A test-only source attempts `INSERT` into a `report` table through the scope | Fails with SQLSTATE `25006`; nothing written — **ADR-279** |
| I-A4 | Timeout lowered to 1 s; a test source runs `pg_sleep(2)`; in parallel `GET /api/staff/appointments/day` | Report → `400 ReportTimedOut`; the appointment endpoint answers normally — NFR-PRF-7 |
| I-A5 | `GET /api/staff/reports` as each role; as a customer; anonymous | Each role sees exactly its categories; Nurse `403` (no `Reports.Any`); customer `401`; anonymous `401` |
| I-A6 | For **every** report key × every staff role | `200` where §12.3 allows, else `403 ActionNotPermitted` with one `AuthorizationDenied` audit row; unknown key `404` — **US-RPT-05 AC**, NFR-SEC-2 |
| I-A7 | CSV export of **every** report for the test branch | `200`, `text/csv`, BOM, header row = visible column labels, `no-store`, attachment file name; one `ReportExported` audit row with the filters — **US-RPT-01 AC2** |
| I-A8 | PDF export of **every** report | `200`, `application/pdf`, starts `%PDF-` — **US-RPT-01 AC2** |
| I-A9 | Caps lowered to 3 rows; a report with 4 rows | CSV `413`; PDF `413`; no audit row |
| I-A10 | Range 367 days; branch not assigned; `all` as Reception; `all` as Accountant | `400 ReportRangeTooLong`; `403 BranchNotAssigned`; `403 CrossBranchReportNotPermitted`; `200` |
| I-A11 | `MaxConcurrentReports = 1`, queue 0; two slow reports in parallel | One `200`, one `429` with `Retry-After`; clinic endpoints unaffected |
| **B — clinic (HO-APT-001, HO-CAT-001, HO-SRG-001)** | | |
| I-B1 | Through HO-APT-001's endpoints: 3 booked, 1 walk-in, 1 no-show, 1 cancelled, 1 open hold at the test branch, plus 1 booking at another branch; `GET` R1 for the date | 6 rows, no hold, nothing from the other branch; totals as seeded — **US-RPT-01 AC1** |
| I-B2 | R2 for today and yesterday after checking in and starting cats | Waits match the seeded times; `waitingNow` today only — **US-RPT-01 AC1 (queue state)** |
| I-B3 | Cats with a vaccination due in 10 days, overdue, renewed, a deceased cat, an anonymised owner; each with an appointment at the test branch; R4 for next 30 days | The due and overdue cats with the owner's mobile and email; the others absent; "last reminder sent" filled after HO-NTF-001's sweep — **US-RPT-01 AC1** |
| I-B4 | Surgeries via HO-SRG-001: one with complications, one with a consent override; R3 | Both rows with surgeon, outcome and complications — **US-RPT-01 AC1**. *Gated on HO-SRG-001 (§17)* |
| **C — stock (HO-IMP-001, HO-SHP-001)** | | |
| I-C1 | Receive two batches (one with landed cost, one without), sell some, transfer, adjust; R5 | Closing per batch × location equals HO-IMP-001's own stock endpoint; valuation for the costed batch; coverage note — **US-RPT-02** |
| I-C2 | Batches expiring in 20, 50, 80 days and one expired with stock; R6 | Bands correct; value at risk at cost and retail; expired row with `includeExpired` — **US-RPT-02** |
| I-C3 | SKU below its reorder point; R7 | Row present; suggested quantity **equals** HO-IMP-001's own low-stock suggestion for the same SKU and location — **US-RPT-02** |
| I-C4 | Consignment with permit and certificate but no customs declaration; R8 | Document flags as seeded |
| **D — revenue and payments (HO-BIL-001, HO-ORD-001, HO-SHP-001)** | | |
| I-D1 | **AC-16**: one invoice with a consultation, a food product and boarding nights; a draft invoice; a credit note on the shop line the next day; R9 grouped by day | Clinic, Shop and Boarding net revenue as seeded; tax excluded; draft ignored; credit note on its own day — **US-RPT-03 AC1** |
| I-D2 | R10 and R11 for the same period | Their net revenue totals **equal** R9's Shop line — the reports reconcile |
| I-D3 | One food line from a costed batch, one from an uncosted batch; R9, R10 as Accountant | Margin on the costed line only; coverage 50 % by revenue; note present — **US-RPT-03 AC2** |
| I-D4 | R10 as ShopManager | No cost, margin or coverage columns in JSON, CSV or PDF — ADR-281 |
| I-D5 | **AC-8**: COD order delivered with a deliberate courier-remittance shortfall; a bank transfer matched; a till count with a variance; R13 | COD mismatch flagged with the variance; transfer matched; cash variance shown; method rows exactly the four (+ store credit); no gateway row — **the §8.2 payment report** |
| I-D6 | R14, R15, R16, R17 over the seeded data | Cash-up as recorded by HO-BIL-001; ageing buckets; refunds by reason and method; tax summary equals Σ invoice tax |
| **E — dashboards** | | |
| I-E1 | `GET /api/staff/dashboard` as each role at the test branch, after I-B1 / I-C1 / I-D1 data | Tiles per §8.11; each figure equals the matching report's figure for today (e.g. `clinic.appointments-today` = R1 totals) — **US-RPT-05 AC** |
| I-E2 | As Nurse; as ShopManager | Nurse: no financial, stock or sales tiles. ShopManager: `revenue.shop-today` without cost; no `payments.*` — **"nothing I am not permitted to see"** |
| I-E3 | `X-Branch-Id` of another assigned branch | Figures for that branch; the first branch's data absent |
| I-E4 | Dashboard latency over 50 sequential calls (Administrator, all tiles) | p95 ≤ 1.5 s — NFR-PRF-3 |
| **F — growth (HO-BIL-001, HO-SUB-001, HO-ORD-001)** | | |
| I-F1 | Two customers created by the test: one first invoiced this month, one invoiced last month and again this month; R18 filtered to them | 1 new, 1 returning; acquisition source as registered — **US-RPT-04** |
| I-F2 | R19 for the test customers | LTV = the sum of their invoices across lines; shares per line — **US-RPT-04** |
| I-F3 | R20 with a second visit a month later | Retained at month 1 — **US-RPT-04** |
| I-F4 | Subscriptions created, paused, cancelled with a reason; R21 | Counts, churn % and reason — **US-RPT-04** |
| I-F5 | A clinic customer who then buys food; a boarding customer who then has a consultation; R22 | Both conversions counted — **US-RPT-04** |
| I-F6 | Abandoned cart, reminder, order within 7 days; R23 | Recovered cart and value |
| I-F7 | Merge a test customer into another (HO-ACC-001) | Growth reports count the survivor once |
| **G — schedules (US-RPT-06)** | | |
| I-G1 | Administrator creates a daily 07:00 schedule for R9 (CSV) with an Accountant recipient; a ShopManager tries to create one; a recipient without email; Reception as a recipient for R9 | `201` + `nextRunOn` = next 07:00 Dhaka; `403`; `400 ReportRecipientHasNoEmail`; `400 ReportRecipientNotPermitted` — FR-RPT-2 |
| I-G2 | Advance to 07:00; run `ReportingWorkCycle`; advance to 08:00 (the default quiet hours 21:00–08:00 hold a `Normal` email — HO-NTF-001 ADR-059); run `NotificationWorkCycle` | One `Generated` run; one file; one notification request per recipient with the attachment reference; **the capturing sender received one email with `purrcare-revenue-by-business-line-….csv` attached, whose bytes equal an interactive CSV export for the same filters and columns** — **US-RPT-06 AC** |
| I-G3 | Run `ReportingWorkCycle` twice for the same due time; dispatch C5 twice concurrently | One run, one email per recipient — exactly once |
| I-G4 | Worker off for 3 days, then one cycle | One run for the latest slot; `nextRunOn` tomorrow |
| I-G5 | Remove the recipient's Accountant role between generation and delivery | Notification `Cancelled SubjectNoLongerApplies`; no email sent — ADR-288 |
| I-G6 | Recipients ShopManager + Accountant on R10 | Attached CSV has no cost columns |
| I-G7 | Advance 8 days; cycle | File purged; run `Purged`; a resend attempt fails the guard |
| I-G8 | File over `reporting.maxAttachmentMegabytes` (lowered) | Email without attachment, `attachmentNote` set, link present; run `AttachmentOmitted` |
| I-G9 | `run-now`; pause then advance past the time; resume; cancel | `200` with run; no run while paused; next run computed from resume; cancelled never runs |
| I-G10 | Schedule for a `Europe/London` branch across the October DST change | Runs at 07:00 local both days (UTC shifts by an hour) |
| I-G11 | `POST …/preview` for a 22:00 send time | `deliveryHeldUntil` = next 08:00 local |
| **H — operations (HO-BRD-001, HO-ORD-001)** | | |
| I-H1 | Stays, a blocked unit, arrivals and departures; R24 over past and future dates | Occupancy as seeded; future flagged forecast |
| I-H2 | Care tasks done on time, late and never; R25 | Per caretaker as seeded |
| I-H3 | Incidents low / medium / high, one notified after 20 minutes; R26 | The late notification flagged |
| I-H4 | Orders in each status with ages around the SLA; R27 | Ageing and breaches as seeded |
| **X — cross-cutting** | | |
| I-X1 | Log capture across all cases at `Information`+ | No row data, no customer contact details, no file content, no report filter containing a customer id |
| I-X2 | Every report response body | No field named like `/password\|hash\|token/i`; money values are strings |

---

## 17. Implementation order and verification

Build **phase by phase**. Each phase: code → its unit tests green → its migration generated **and read** → its
integration tests green. **A phase whose source handovers are not merged is blocked (§20.1) — skip nothing, stub
nothing.** Record each phase's commit in the test sheet.

| Phase | Stories | Needs merged | Delivers |
|---|---|---|---|
| **A** | US-RPT-01 AC2 (framework) | HO-ACC-001, HO-ADM-001, HO-NTF-001 | Observe F-RPT-1…6; enums, settings, actions, codes; catalogue types; `ReportingReadScope`; `RunReportQuery` pipeline; CSV writer; PDF renderer; C1 export audit; concurrency policy; timeout filter; `AddReportingSchema`; conventions U-A1…U-A3 |
| **B** | US-RPT-01 AC1 | + HO-APT-001, HO-SRG-001, HO-EMR-001 | R1, R2, R4, R3; `AddClinicReportViews` |
| **C** | US-RPT-02 | + HO-SHP-001, HO-IMP-001 | R5–R8; `AddStockReportViews` |
| **D** | US-RPT-03, §8.2 | + HO-BIL-001, HO-ORD-001, HO-PHM-001 | R9–R17; `BusinessLineRules`; margin; `AddFinancialReportViews` |
| **E** | US-RPT-05 | B–D | Dashboard, 21 tile sources, gating |
| **F** | US-RPT-04, §8.3 | + HO-SUB-001 | R18–R23; `AddGrowthReportViews` |
| **G** | US-RPT-06 | A, and at least one report | Schedules, runs, files, worker, purge; **the HO-NTF-001 amendment**; `AddReportSchedules`, `AddNotificationAttachments` |
| **H** | §8.1 remainder | + HO-BRD-001 | R24–R27; `AddOperationsReportViews` |

**Single reports whose source is late.** If one report's source (for example R3 on HO-SRG-001) is the only
unmerged piece of a phase, the phase may complete without that report. The report then lands with an Amended row
once its source merges. **A report whose source is missing is absent from the catalogue**, never present with
empty rows, and the ACCEPTANCE report lists it.

```bash
dotnet restore CatEcommerce.sln
dotnet build CatEcommerce.sln            # 0 warnings, 0 errors
dotnet test tests/CatEcommerce.Domain.Tests
dotnet test tests/CatEcommerce.Application.Tests
dotnet dotnet-ef migrations add <PhaseMigration> --project src/Infrastructure/CatEcommerce.Infrastructure --startup-project src/Api/CatEcommerce.Api --output-dir Persistence/Migrations
# read the migration: raw SQL only for report.rv_*; no CreateTable for any rv_*; nothing outside schema report
dotnet dotnet-ef database update --project src/Infrastructure/CatEcommerce.Infrastructure --startup-project src/Api/CatEcommerce.Api
psql -d catecommerce_dev -c "select viewname from pg_views where schemaname = 'report' order by 1;"
dotnet run --project src/Api/CatEcommerce.Api
dotnet test tests/CatEcommerce.IntegrationTests
```

Environment: add the `Reporting` section to the committed `appsettings.json` (no secrets). `Reporting:Worker:
Enabled` is `false` in `Testing`.

---

## 18. Acceptance criteria

Each row is checked against its evidence. **A row with no evidence is not met.**

| # | Story | Criterion | Evidence |
|---|---|---|---|
| A1 | RPT-01 AC1 | Given a date and branch, the **daily schedule** is available (appointments, walk-ins, no-shows; no holds; other branches excluded) | U-B1, I-B1 |
| A2 | RPT-01 AC1 | Given a date and branch, the **surgery register** is available (procedure, surgeon, outcome, complications) | I-B4 |
| A3 | RPT-01 AC1 | Given a date and branch, the **vaccination due list** is available with contact details, excluding deceased cats, retired owners and renewed records | U-B3, U-B4, U-B5, I-B3 |
| A4 | RPT-01 AC1 | Given a date and branch, the **queue state** is available (waiting now today; waits, triage mix and left-without-being-seen per day) | U-B2, I-B2 |
| A5 | RPT-01 AC2 | **Every** report is filterable by date range and branch | U-A1, U-A4, U-A5, U-A7, I-A10 |
| A6 | RPT-01 AC2 | **Every** report exports to CSV (injection-guarded) | U-A9, I-A7, I-A9 |
| A7 | RPT-01 AC2 | **Every** report exports to PDF | U-A10, U-A11, I-A8, I-A9 |
| A8 | RPT-02 | Stock on hand by SKU, batch and location with valuation (coverage shown where cost is missing) | U-C1, U-C2, I-C1 |
| A9 | RPT-02 | Near-expiry (and expired) with value at risk | U-C3, I-C2 |
| A10 | RPT-02 | Low stock with reorder suggestions from HO-IMP-001's own calculation | U-C4, I-C3 |
| A11 | RPT-03 AC1 | Revenue broken down by clinic, shop and boarding | U-D1, U-D2, I-D1 |
| A12 | RPT-03 AC1 | Shop revenue by category and by country of origin, reconciling to the shop line | I-D2 |
| A13 | RPT-03 AC2 | Margin shown beside revenue where landed cost is known, with coverage; never estimated | U-D3, I-D3 |
| A14 | RPT-04 | New vs returning customers | U-F1, I-F1 |
| A15 | RPT-04 | Lifetime value across all three lines | I-F2 |
| A16 | RPT-04 | Cohort retention | U-F2, I-F3 |
| A17 | RPT-04 | Subscription health | U-F4, I-F4 |
| A18 | RPT-04 | Cross-line conversion | U-F3, I-F5 |
| A19 | RPT-05 | The home dashboard shows the role's key figures for today at the active branch | U-E1, I-E1, I-E3, I-E4 |
| A20 | RPT-05 | …and nothing the caller is not permitted to see — tiles, reports and columns | U-E2, U-E3, U-A8, I-A6, I-D4, I-E2 |
| A21 | RPT-06 | A report on a schedule is delivered by email with the export attached — **proven with the capturing sender. Outside Development no email is delivered until HO-NTF-001 OQ-1 is closed**; this caveat is recorded, not passed silently | I-G2, U-G6, U-G7 |
| A22 | RPT-06 | Schedules run exactly once per scheduled time, in branch-local time across DST, and catch up once after downtime | U-G1, U-G2, I-G3, I-G4, I-G10 |
| A23 | RPT-06 / ADR-288 | Recipients receive only what they may see; losing the role stops delivery | U-G4, U-G5, I-G5, I-G6 |
| A24 | FR-RPT-2 | Only administrators create and manage schedules | I-G1 |
| A25 | §8.2 | Payment reports cover **cash on delivery, cash in store, card in store and bank transfer** — no gateway | U-D5, I-D5 |
| A26 | §8.2 | Daily cash-up, receivables ageing, refunds and credit notes, tax summary | U-D6, I-D6 |
| A27 | §8.1 | Import consignments, boarding occupancy, care compliance, incidents, order fulfilment | I-C4, U-H1, U-H2, U-H3, I-H1, I-H2, I-H3, I-H4 |
| A28 | §8.3 | Cart abandonment with recovered value | I-F6 |
| A29 | ADR-277 / ADR-279 | Reports never write to another module's tables — database-enforced and convention-checked | U-A3, U-X5, I-A3 |
| A30 | ADR-285 | Every export is audited with its filters and row count, never row data | U-A12, I-A7 |
| A31 | NFR-PRF-7 | Reporting does not degrade interactive work (timeout, concurrency limit, sequential scheduled runs) | I-A4, I-A11 |
| A32 | — | Host starts with every registration; no report without a source | I-A1, U-A2 |

**Deferred, named in §4**: subsidised-programme reporting (HO-SRG-001), accounting export (HO-BIL-001), gateway
settlement (Phase 5), email delivery outside Development (HO-NTF-001 OQ-1).

---

## 19. Required gates

In order. Each writes `docs/gates/HO-RPT-001-<GATE>.md` **before** reporting.

| # | Gate | Agent | Focus |
|---|---|---|---|
| 1 | ARCHITECTURE | `architecture-reviewer` | `Reporting` context (ADR-277, OQ-1 — **no architect design exists; review the view approach itself**); views not projections (ADR-278 against ADR-012 and `.ai/read-side-pattern.md` §5); read-only scope (ADR-279); **no write, DDL, repository mutation or event handler touching another context** (U-A3, U-X5); one `DbContext`; the two-command run sequence under rule 1; the HO-NTF-001 amendment confined to §13's lines; the NFR-PRF-7 reading (ADR-290); each §8.4 view bound to the owning handover's actual tables |
| 2 | SECURITY | `security-reviewer` | The `Reports.Any` marker and the in-handler category check agreeing with the fail-closed filter (I-A6); column restriction removed from payloads (I-D4); branch scope and `all`; export audit before streaming; CSV injection guard; **scheduled attachments: staff-only recipients, the send-time guard, intersection of columns, no bytes in `notify.*` or audit**; no row data or contact details in logs; statement timeout and concurrency as DoS controls |
| 3 | CODE_REVIEW | `code-reviewer` | One pipeline for screen, CSV and PDF; money `decimal` only (U-D4); time only through `ReportPeriodResolver` / `ReportScheduleCalculator`; no reorder or vaccination logic re-implemented; explicit projections; versioned view SQL files; the worker never throws |
| 4 | TEST | `test-engineer` | `TC-HO-RPT-001.md` from §15/§16 first; §16.1's per-test branch and "create source data through its owner" rules; I-A6/I-A7/I-A8 as **full** matrices over every report key; I-G2 compares attachment bytes with an interactive export |
| 5 | ACCEPTANCE | `acceptance-reviewer` | Every §18 row; A21's provider caveat recorded; reports absent because their source is unmerged listed explicitly; §8.11 tile gate fallbacks named |

---

## 20. BLOCKED conditions — stop and report

**20.1 A source handover is not merged.** HO-ACC-001, HO-ADM-001, HO-NTF-001 and HO-APT-001 gate everything. Each
later area gates its phase (§17). **Do not stub a source table, create one, or fake its rows.** A report whose
source is missing is left out of the catalogue and named in the ACCEPTANCE report.

**20.2 A source cannot supply a contracted column** (§8.4). Examples: HO-IMP-001's ledger has no source-line
reference; HO-BIL-001's invoice lines have no line kind; HO-IMP-001's reorder suggestion is not callable as a pure
service; stock locations carry no branch. **Stop and report which contract column is missing and which report and
AC it blocks.** Never fill a column with a constant, a guess or a join you invented, and never change the other
area's code. The fix is an Amended row on the owning handover. The same applies later, if a source migration must
drop a column a view uses (F-RPT-4): the owning area replaces the view in the same migration.

**20.3 A platform fact F-RPT-1…6 does not hold** when observed in Phase A — above all, if a write inside
`READ ONLY` does not fail. Stop. Do not replace the read-only scope with a convention.

**20.4 A report misses its budget because of a missing index on another area's table.** Stop and report the query
plan. **Do not add the index from a Reporting migration**, and do not add a materialised view (OQ-10). The owning
area adds the index by amendment.

**20.5 A report seems to need data no table records** — e.g. a historic queue position, or a price as it was
displayed. Stop. Do not start capturing it with an event handler or a snapshot table. That is a new decision
(ADR-278).

**20.6 QuestPDF licence unconfirmed (HO-CAT-001 §20.6), or Bengali names render as boxes.** Stop before merge. Do
not swap in an AGPL library, and do not transliterate names.

**20.7 A gateway payment method appears** in HO-BIL-001's enumeration, or someone asks for a gateway settlement
report. It contradicts SRS §1.2 (Phase 5). Report it; build nothing for it.

**20.8 Do not send reports to free-text addresses** or to a recipient who fails the guard. Do not weaken the
column intersection to "the creator's columns" (ADR-288).

**20.9 Do not edit HO-NTF-001 beyond §13's lines.** If attachments need more than §11.7 describes (MIME types,
provider behaviour), stop and raise it for HO-NTF-001's owner.

**20.10 A requirement conflict is a stop, not an edit.** SRS and backlog are forbidden scope.
