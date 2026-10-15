# HO-XCT-001 — Cross-cutting and Technical (backend)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-XCT-001** |
| Title | Cross-cutting and Technical — backend slice |
| Date | 2026-09-22 |
| Author | Claude Opus 5 |
| Backlog | [`docs/USER_STORIES.md`](../USER_STORIES.md) v2.4 §17 — **US-XCT-01, US-XCT-02, US-XCT-05, US-XCT-06** built here. **US-XCT-03, US-XCT-04, US-XCT-07** are standards every handover applies (§3.3). This slice does not build them |
| SRS | [`docs/SRS.md`](../SRS.md) v2.3 — **NFR-AVL-3, NFR-AVL-4, NFR-AVL-5**, NFR-PRF-1, NFR-CMP-2, NFR-CMP-3, **C-3, D-5, D-6, D-8, D-9, D-10, D-11**, FR-ACC-11, NFR-SEC-2, NFR-SEC-3, NFR-SEC-7, NFR-SEC-8, NFR-MNT-2, NFR-MNT-5, C-7, C-9, **AC-19, AC-20**, Appendix D |
| Pairs with | [`HO-XCT-002`](HO-XCT-002.md) — the frontend. **§12.4 of this document is its contract** |
| Builds on | [`HO-ACC-001`](HO-ACC-001.md) (sessions, `[RequireAction]`, `[PassiveSession]`, audit change capture and triggers, `CustomerAccountAggregate.Anonymise`, `CustomerAccountAnonymisedEvent`), [`HO-ADM-001`](HO-ADM-001.md) (settings registry, active branch, branch time model), [`HO-NTF-001`](HO-NTF-001.md) (worker shape, notification redaction), [`HO-APT-001`](HO-APT-001.md) (the day list tables), [`HO-CAT-001`](HO-CAT-001.md) (foundation). **It also depends on handovers that do not exist yet** — HO-BRD-001 above all. §5.2 states the contracts assumed of them |
| Delivery order | Section 17 of 17 in [`README.md`](README.md#delivery-order), the last one. Every area it touches is built before it |
| Approved design | None. There is no `architect` design artifact for this slice. §9 records the decisions as proposed ADRs |
| Baseline commit | `4007225` |
| Amended | *(none. Append rows here, and never edit a section once work has started)* <br><br>**2026-09-23 — README C-5 (file storage).** Object storage is now owned by [`HO-FIL-001`](HO-FIL-001.md) (GCS, ADR-318), which also implements this slice's `IRetentionPurgeSource` for files and deletes **object first, row second**. OQ-9 narrows to object **backup**, which is still unowned — and ADR-318 disables bucket versioning and soft-delete deliberately, so the erasure guarantee holds; backup must not reintroduce them. **2026-09-26 — by [`HO-ADM-001`](HO-ADM-001.md), gate fix (integrator-authorised, RECORDS).** `AdministrationPersonalDataEraser`, `PolicyAcceptanceAggregate.RedactIpAddress` and `PolicyAcceptanceIpRedactedEvent` (named in this document's own §5.3/§8, e.g. its eraser table and the aggregate-method row) are **already delivered** by HO-ADM-001, built ahead of this handover landing (the same "amendment already in the target's own Amended row" pattern §5.3 itself describes) — do not rebuild them; `src/Infrastructure/CatEcommerce.Infrastructure/Retention/Erasers/AdministrationPersonalDataEraser.cs` is the real file. Its actual authority is `docs/handover/CONTRACTS.md`'s "Cross-cutting duties" section (C-ALL-2), not this document's own §5.3 -- HO-ADM-001's own §1 Amended row (4) originally cited "HO-XCT-001 §5.3" for this, which was itself wrong and is corrected there in the same change that adds this row. This document's table-classification text naming the settings-value table as `admin.setting_value` is also corrected: the real table, from `BusinessSettingsConfiguration`, is `admin.business_settings_value` (`OwnsMany` on `BusinessSettingsAggregate.Values`, column `business_settings_id` + `key`).<br><br>**2026-10-12 — gate fix round (ARCHITECTURE FAIL, CODE_REVIEW PASS, SECURITY PASS; owner decisions of 2026-10-12, integrator-authorised).** (1) **BIL/IMP classification edits under integrator authority** (ADR-298/299 amendment): `billing.invoice_line`, `inventory.purchase_order_line` and the four `inventory.consignment_*` tables (`cost`, `exchange_rate`, `landed_cost_line`, `hs_code`) are reclassified `CurrentState`/`AggregateOnly` in `RecordClassification.Billing.cs` and `RecordClassification.Inventory.cs` (their shipped aggregates replace those rows while a document is a draft or being re-costed). `RetentionGuardExemptions` and its uses are deleted, so classification and enforcement are one statement and Q3 reports them correctly. (2) **Scope: files beyond §13's literal list**, each non-§14 and each necessary: `deploy/.gitattributes` (LF for scripts run in Linux containers); `deploy/backup/restore-mode.env`, `deploy/backup/scripts/render-config.sh` (inside `deploy/backup/**`); `src/Domain/.../DomainServices/IOperationsClock.cs` (the main-branch calendar port of §12.2, ADR-025 placement); `src/Shared/.../Constants/RecordClassification.Billing.cs` and `.Inventory.cs` (item 1); `SystemActors.Anonymous` (beside `RetentionWorker`); `tests/CatEcommerce.Infrastructure.Tests/**` (`AuditCaptureExclusionsTests`, `HardDeleteGuardInterceptorTests`, `HardDeleteSourceScanTests`, `RecordClassificationConventionTests`, `RetentionGuardMigrationTests`, `ClassificationEnforcementAgreementTests`; §13 lists only Domain, Application and Integration tests); `src/Shared/CatEcommerce.Shared/Common/Enums/ErasureIdentityCheck.cs` (a 13th enumeration beyond §10.1's twelve, the fixed identity-check codes of item 4); `tests/CatEcommerce.Domain.Tests/Common/BillingConventionTests.cs` (HO-BIL-001's own convention test: its D-6 assertion now admits `billing.invoice_line` as `CurrentState`/`AggregateOnly`, following item 1); the existing `AdministrationPersonalDataEraser` rewire of §5.3; `docs/backlog/XCT.md`. (3) **`Program.cs` interceptor order:** `HardDeleteGuardInterceptor` is third, after HO-ACC-001's `AuditEntryImmutabilityInterceptor` and HO-PHM-001's `ControlledDrugRegisterImmutabilityInterceptor`, which were moved from after change capture to before it; the stated intent of §12.7 (the guard runs before change capture) is met. (4) **Contract changes for HO-XCT-002:** `POST /api/admin/customers/{id}/erasure` now takes `{ channel, identityCheck }` where `identityCheck` is one of `RegisteredMobileCallback`, `RegisteredEmailReply`, `InPersonPhotoId`, `WrittenRequestSigned`; the free-text `reason` is removed (nothing free-text reaches the immutable audit row). The erasure journal is exported every 10 minutes (was daily). (5) The ledger-opening event handler dispatches C1 in a **new scope** (the anonymisation has already committed, ADR-021; precedent: Billing's capture handler), and C1 retries once in a fresh scope when its insert loses a race. |
| Amendments this slice needs in other handovers | Recorded in §5.3. **Not written into those files**, because other areas are being written in parallel and their files are outside this author's scope. The README owner records each one as an Amended row in the target's §1 **before** implementation starts (OQ-1) |
| Platform | `Platform.Infrastructure.Core` **1.0.34**. Do not downgrade |

---

## 2. Objective

§17 of the backlog holds the non-functional requirements that users can see. Three of its stories
(accessibility, clear errors, safe uploads) are standards that every other handover already applies.
This slice builds the **four capabilities that no single area owns**:

1. **The front desk keeps working when the internet drops** (US-XCT-01, NFR-AVL-3, AC-19). The server
   provides **one** compact, permission-filtered *offline snapshot*: today's appointments and today's
   boarding tasks for the active branch. It also provides a ping endpoint that the browser uses to
   detect lost connectivity. Caching, the offline indicator and read-only mode belong to the browser
   (HO-XCT-002).
2. **The storefront is fast on a phone** (US-XCT-02, NFR-PRF-1). The AC is a **field** metric: "LCP
   within 2.5 s for the 75th-percentile user". A lab test alone cannot prove it. This slice adds
   anonymous real-user measurement (web-vitals ingestion) and a p75 report per route. The lab budget
   and the 360 px sweep live in HO-XCT-002.
3. **Data is backed up and restorable** (US-XCT-05, NFR-AVL-4/5). The deliverables are:
   PostgreSQL point-in-time recovery with pgBackRest, continuous WAL archiving well inside the 15-minute
   RPO, and a scripted restore drill that measures its own RPO and RTO. In the application, backup
   health is visible and every quarterly restore test is recorded, attributed and audited.
4. **Clinical and financial records are retained and never hard-deleted** (US-XCT-06, C-3, D-5 to D-11,
   AC-20). The pieces are:
   - a classification of **every table**;
   - refusal of hard deletes at three layers (EF, database triggers, source scan);
   - a database role model in which the application cannot switch the triggers off;
   - an **erasure ledger** that finishes a customer's deletion request across every context,
     including personal data held inside retained records until their statutory period ends;
   - one retention worker that purges only what the schedule allows (D-10 boarding media, expired
     session artefacts, telemetry) and marks notification content redacted.

Four things this slice is careful about:

1. **"Never hard-deleted" is a property of the database, not a promise in code.** Triggers enforce it.
   The runtime role cannot disable a trigger, because it neither owns the tables nor is a superuser
   (ADR-300). A convention test fails the build if any table is left unclassified (ADR-298).
2. **Erasure and retention pull in opposite directions.** Personal data inside a retained record (a
   billing name on an invoice, an owner name on a controlled-drug entry) is **held**, not erased.
   Every hold carries a date, and the ledger erases the data when that date passes (ADR-302). Nothing
   is erased from a clinical or financial record ahead of its period, and nothing personal is kept
   beyond it.
3. **The offline snapshot is personal data that will sit on a device.** It carries the minimum fields
   the offline board shows. It is served `no-store` and is valid only for the branch-local day. Every
   issue that returns content is audited (NFR-SEC-8), and polling it never extends the staff idle
   session (ADR-306, ADR-307).
4. **A backup is only as good as the last restore.** RPO and RTO are **measured by the drill** and
   recorded. They are never asserted from configuration (ADR-311, ADR-313).

§17 builds the slice in **six phases**. Each phase compiles, migrates and passes its own tests before
the next begins.

---

## 3. In scope

### 3.1 Stories

| Story | Pri | Pts | Capability delivered here | Phase |
|---|---|---|---|---|
| US-XCT-06 | M | 5 | Classification of every table; hard-delete refusal (interceptor, triggers, source scan); non-owner runtime role and startup check; erasure ledger with immediate and held scopes; staff-initiated erasure; deferred anonymisation; review-eligibility report; retention worker and purges; notification content redaction | A, B, C |
| US-XCT-01 | M | 13 | *(server half)* The offline snapshot endpoint (sections filtered by permission, ETag / `304`, `validUntil`, `[PassiveSession]`), audited issue, and `GET /api/system/ping` | D |
| US-XCT-02 | M | 8 | *(server half)* Anonymous web-vitals ingestion and the p75 summary per route, metric and viewport class, compared against the NFR-PRF-1 budget | E |
| US-XCT-05 | M | 8 | pgBackRest PITR configuration, WAL archiving, schedule, off-site encrypted repository, erasure journal export, restore and drill scripts, a Docker-based drill test, the backup-run status read, restore-test records, the quarterly due / overdue indicator, runbooks | F |

The backlog flags US-XCT-01 at 13 points for splitting (§18.3: "offline appointments vs offline
boarding tasks"). That split is carried out *inside the pair*. The appointments section (D1) and the
boarding-tasks section (D2) are separate steps, and D2 is blocked until HO-BRD-001 is merged (§20.3).

### 3.2 Decisions already settled upstream — do not re-litigate

| Decision | Where |
|---|---|
| In-memory CQRS, one host, `IInMemoryDispatcher` only; never `202` | ADR-010, 013, 014, 019, 020 |
| PostgreSQL / EF Core, one `DbContext`, `xmin` → `409`; per-call `SaveChangesAsync` | ADR-011, 016, 021 |
| No `CommandService` layer; `Enumeration` in `Shared` | ADR-015, 022 |
| Every aggregate change audited in the same transaction; audit rows immutable at three layers (triggers refuse `UPDATE`/`DELETE`/`TRUNCATE`) | HO-ACC-001 ADR-040 |
| Account deletion is **in-place anonymisation**, never row removal; `CustomerAccountAnonymisedEvent` carries ids only | HO-ACC-001 ADR-041 |
| `[RequireAction]` + the one `ActionRoles` table; every endpoint declares its access | HO-ACC-001 ADR-036, HO-ADM-001 ADR-043 |
| `[PassiveSession]` endpoints do not record staff activity | HO-ACC-001 ADR-037 |
| Settings are a typed registry read through `IBusinessSettings`; new keys are one registry entry | HO-ADM-001 ADR-050 |
| Active branch from `X-Branch-Id`; branch-local dates only through HO-ADM-001's domain services | HO-ADM-001 ADR-045, ADR-046 |
| Background work is a `BackgroundService` in the Api host: a scope per unit of work, never throws out of `ExecuteAsync`, disabled in `Testing`, and has a cycle class that tests call | HO-NTF-001 ADR-060, HO-APT-001 §12.2 |
| No payment gateway in v1 (SRS Phase 5); no OTP or verification anywhere | Owner; HO-ACC-001 §4.2 |

### 3.3 Referenced standards — applied everywhere, built nowhere in particular

These three stories are satisfied by each area's own handover. This slice **adds nothing for them**
beyond applying them to its own endpoints:

| Story | Standard | How this slice applies it |
|---|---|---|
| US-XCT-03 (UI-6, NFR-USA-6) | WCAG 2.1 AA | No UI here. HO-XCT-002 applies it to its screens |
| US-XCT-04 (NFR-USA-4, NFR-USA-5) | Plain-language errors, no stack trace or internal id | Every `400`/`409` here uses the HO-ACC-001 `ValidationResponse` shape with a sentence that says what to do. `RecordIsRetained` (§10.4) is worded for a person: *"This record is kept for legal reasons and cannot be deleted."* |
| US-XCT-07 (NFR-SEC-5) | Uploads validated, scanned, served by signed expiring URL | This slice accepts **no file**. The restore-test drill output is pasted text (≤ 20 000 chars), not an upload. Boarding media purge (D-10) deletes through HO-BRD-001's storage port. It never touches files directly |

---

## 4. Out of scope

Building anything in this section is a **FAIL** at the acceptance gate.

### 4.1 Frontend — all of it

Screens are [`HO-XCT-002`](HO-XCT-002.md). The criteria phrased as UI behaviour are met here at the
contract level:

| Story AC (UI phrasing) | Server half — delivered here |
|---|---|
| US-XCT-01 "remain visible from local cache in read-only mode" | A snapshot built for caching: one payload, `validUntil`, `staffAccountId`, `idleTimeoutMinutes`, ETag. Local storage is the browser's (HO-XCT-002 ADR-FE-141) |
| US-XCT-01 "a clear indicator is shown and write actions are disabled" | `GET /api/system/ping`: cheap, `no-store`, passive. The indicator and the disabling are the browser's |
| US-XCT-01 "the view refreshes automatically" | ETag / `304`, so frequent refreshes cost almost nothing |
| US-XCT-02 "LCP within 2.5 s for p75 on 4G" | Field samples in, p75 out (`GET /api/admin/web-vitals/summary`). Making pages fast is the storefront's work, and the lab budget is HO-XCT-002's |
| US-XCT-02 "360 px, no horizontal scrolling" | Nothing. It is entirely a browser concern (HO-XCT-002 E-B-series) |
| US-XCT-05 "its result recorded" | `POST /api/admin/operations/restore-tests`, which the administrator uses from the drill's output |

### 4.2 Deferred to, or owned by, a named area — contracts stated here

| Capability | Owner | What this slice delivers or requires |
|---|---|---|
| Boarding task day list data | **HO-BRD-001** (not yet written) | `BoardingTaskSnapshotSource` reads HO-BRD-001's tables through the field contract in §5.2 (C-BRD-1) |
| Boarding photo and video purge after 12 months unless attached to an incident (D-10) | **HO-BRD-001** | The `IRetentionPurgeSource` port (§8.9). HO-BRD-001 implements it, deleting the object before its row. The worker invokes it |
| Personal data held inside clinical, financial and regulatory records (EMR, PHM, SRG, BIL, ORD, IMP, SUB, BRD) | Each of those handovers | The `IPersonalDataEraser` port (§8.8). Every context that stores a customer's personal data registers one. The convention test (U-B6) fails until each does or is exempted with a reason |
| Clinical and financial "past retention" counts for contexts other than Cat and Appointments | Each context | The `IRetentionReviewSource` port (§8.10) |
| Object-storage backup (versioning plus replication) | Whichever handover provisions object storage (NFR-SEC-5; none has yet) | The runbook section and the drill check, conditional on the store existing (OQ-9) |
| Monitoring, alerting, health/readiness (NFR-MNT-4) | No story | Not built. Only the backup-status read exists |
| Production hosting and environment provisioning (NFR-MNT-2) | No story | Only the backup components and the database role script. The host is OQ-8 |

### 4.3 Not buildable yet — named gaps

| Gap | Consequence |
|---|---|
| **SRS Q-1**: the jurisdiction and the exact retention periods | Periods are **settings** with the SRS's assumed defaults (7 years clinical, 6 years financial, 12 months boarding media). Nothing statutory is ever deleted by the application, so a wrong period can delay an anonymisation or a review flag. It can never destroy a record (ADR-301) |
| Personal data in audit entries (IP addresses, before/after values) | Audit entries are immutable (D-5, HO-ACC-001 ADR-040) and are **not** altered by erasure. HO-ACC-001 already omits the before-values of an anonymisation. Residual, and recorded (OQ-5) |
| Erasure of a `Merged` duplicate customer record | HO-ACC-001's `Anonymise` requires `Active`. Staff erasure of a merged record returns `400 CustomerRecordMerged`, whose message tells the operator to erase the surviving record (OQ-6) |
| A machine identity for the backup job | Authentication is identifier + password with sessions (HO-ACC-001). The backup job therefore writes only its own `ops.backup_run` rows through a dedicated database role (ADR-312). Restore tests are entered by a signed-in administrator |

### 4.4 Explicitly forbidden shapes

No `IBusMessageDispatcher`, `IEventPublisher`, `IDispatcher`, MassTransit, RabbitMQ, MongoDB, second
host, `CommandService` layer, rejection event, `202 Accepted`, `[Authorize]` or auth framework.

Specific to this slice:

- **No `DeleteAsync` added to the repository contract.** The platform contract has none (verified,
  `.ai/persistence-ef-core.md` §1), and that absence is part of the protection.
- **No hard delete of a `Forbidden` table by any path**, including `ExecuteDelete`, raw SQL,
  `TRUNCATE`, cascade from a parent, or disabling a trigger.
- **No disabling or dropping of any retention trigger** in any migration after the one that creates
  it, and no `session_replication_role` change anywhere.
- **No automatic deletion or anonymisation of a clinical, financial, regulatory or audit record at the
  end of its period.** The end of a period produces a report row (ADR-301).
- **No IP address, user id, session id, user agent or full URL** in a web-vitals sample.
- **No API response cached by a service worker** is designed for here. The snapshot is `no-store`.
- **No third-party backup SaaS or cloud-proprietary backup service** as the only copy (NFR-CMP-3).
- **No secret** in any committed file, including the pgBackRest cipher passphrase and the S3 keys
  (NFR-SEC-7).

---

## 5. Prerequisites

| Required | State |
|---|---|
| HO-CAT-001 merged | ✅ at `4007225` |
| **HO-ACC-001, HO-ADM-001, HO-NTF-001, HO-APT-001 merged** | ❌ **not implemented at baseline.** Phases A–E extend their types. §20.1 |
| **HO-BRD-001 merged** | ❌ **not written.** Blocks Phase D step D2 and the D-10 purge source only. §20.3 |
| EMR, PHM, SRG, BIL, ORD, IMP, SUB handovers merged | ❌ not written. Blocks **nothing to start**. Their tables are classified when this slice runs (it runs last), and U-A1/U-B6 fail until each is classified and has an eraser. §20.4 |
| PostgreSQL 17.5 `catecommerce_dev`, `dotnet-ef` 9.0.0 | ✅ |
| Docker Engine (for the backup drill test only, §16.2) | ✅ Engine 29.5.3 is running (HO-CAT-001 §5). ⚠ **Image pulls unverified**: `postgres:17-bookworm` and the PGDG `pgbackrest` apt package inside it. Pull in Phase F step 1, and stop on failure (§20.9) |
| New NuGet packages | **None.** `System.Security.Cryptography` (ETag hash), EF Core `ExecuteDeleteAsync` and `SaveChangesInterceptor` are already present |

### 5.1 Shared database

The rules of HO-CAT-001 §16.1 and HO-ACC-001 §16.1 apply unchanged. Two points bind harder here:

- **After Phase A, the triggers refuse `DELETE` on every protected table.** A test that "cleans up" a
  cat or an appointment now fails loudly. Isolate by data ownership, as before.
- **Purge tests must only ever purge rows they created.** Each purge case seeds rows with a
  run-unique marker and asserts only on those rows. It never asserts a global count (§16.1 rule 9).

### 5.2 Contracts assumed of handovers that do not exist yet

State each of these to the author of the named handover. If the merged code does not meet one, it is
a §20 stop. **Never shim around it.**

| # | Owner | Contract |
|---|---|---|
| **C-BRD-1** | HO-BRD-001 | A queryable per-branch, per-local-day list of care tasks, carrying: task id; branch id; due instant; task type (feeding, medication, cleaning, …, as an `Enumeration` with a display name); a short human label; unit name; cat id and name; cat high-risk flag and handling note (from Cat); the instruction text shown to the caretaker; status (open / done / skipped); completed-on; assignee display name. Staff action for reading it: **`BoardingTasks.View`** (assumed roles: Caretaker, Nurse, Vet, Reception, Administrator, per Appendix D "Care logs & tasks") |
| **C-BRD-2** | HO-BRD-001 | An implementation of `IRetentionPurgeSource` (§8.9) with `SourceKey = "boarding-media"`. It purges photos and videos older than `retention.boardingMediaMonths` that are **not attached to an incident**. It deletes the stored object **before** the metadata row, is idempotent, and handles at most `batchSize` items per call |
| **C-BRD-3** | HO-BRD-001 | An `IPersonalDataEraser` (§8.8) for whatever customer personal data BRD stores (emergency contact, belongings notes, …) |
| **C-ALL-1** | EMR, PHM, SRG, BIL, ORD, IMP, SUB, BRD | Every table the context adds is entered in `RecordClassification` (§10.3) under the §8.2 rules, in the same change that adds the table. Otherwise U-A1 fails |
| **C-ALL-2** | Same | Every context that stores personal data about a customer, beyond the id, registers an `IPersonalDataEraser` that erases what is not held, and returns `Held(until)` for data inside a retained record. Otherwise the context is exempted in `ErasureExemptions` with a one-line reason. Otherwise U-B6 fails |
| **C-ALL-3** | EMR, PHM, SRG, BIL, IMP | An `IRetentionReviewSource` (§8.10) returning the count of its records past retention (clinical: `retention.clinicalYears` after the cat's last visit or death, D-8; financial: `retention.financialYears` after the document date, D-9) |
| **C-ALL-4** | Every aggregate that is a clinical, financial or regulatory record | **No method removes the record, and no owned child that is itself a record.** A correction is a new entry (D-5) |

### 5.3 Amendments this slice needs in other handovers

These are small, additive changes to other areas' files. The edits are in §13's allowed scope. **The
Amended rows are not written by this handover** (OQ-1):

| Target | Amendment |
|---|---|
| HO-ADM-001 §10.2 | Nine settings are appended to `SettingDefinitions` (§10.2 here). Settings UI rows render automatically, because HO-ADM-002 renders the registry |
| HO-ADM-001 §8.8 | `PolicyAcceptanceAggregate.RedactIpAddress(asOf)`: idempotent, sets `IpAddress = null`, raises `PolicyAcceptanceIpRedactedEvent` (ids only). Used by `AdministrationPersonalDataEraser` when the hold expires (§8.8) |
| HO-ACC-001 §11.9 step 7 | `ops.web_vital_batch` (and its owned samples) and `ops.retention_run` are excluded from `AuditChangeCaptureInterceptor`. The first is anonymous telemetry. The second is itself an audit-grade log whose entries are summarised by one `BusinessEvent` row per run (ADR-309, ADR-303). This is the same mechanism as HO-NTF-001 ADR-066 |
| HO-ACC-001 §12.4 / §12.9 | A second exception filter entry: `ProtectedRecordDeletionException` → `409 RecordIsRetained` (§12.5) |
| HO-NTF-001 §8.6 | The redaction reason enumeration gains **`Retention`**. `Redact(Retention)` redacts the body, subject, title, HTML and link **and nulls the destination**, like `Anonymisation` does |
| HO-APT-001 | None. The day list is read, not changed |

---

## 6. Governing architecture documents

Where any of these disagrees with a `.ai/templates/*.cs` file, **the markdown wins**.

| Document | What you need from it |
|---|---|
| [`.ai/architecture.md`](../../.ai/architecture.md) | Layer responsibilities; §4 "When a Domain Service is warranted", because the retention, erasure and snapshot ports sit in `Domain.DomainServices` for the ADR-025 reason, not because they are domain logic; §5 **"the only asynchronous element"**, since this slice adds a third background service and states why (ADR-303); §6.2 the two silent-failure rules (`ValidateCommand = true`, runtime-type dispatch) |
| [`.ai/cqrs.md`](../../.ai/cqrs.md) | Why `IInMemoryDispatcher` and nothing else; §5 rules. The worker dispatches commands through the same dispatcher in a fresh scope |
| [`.ai/bounded-contexts.md`](../../.ai/bounded-contexts.md) | **Rule 1** "one aggregate mutated per command". Two recorded exceptions here: executing an erasure scope, and re-applying erasures after a restore (ADR-302, ADR-314). **Rule 3** unbounded collections, which is why web-vitals batches, retention runs and restore tests are their own roots. The `Operations` context is not listed (OQ-2, ADR-297) |
| [`.ai/namespace-conventions.md`](../../.ai/namespace-conventions.md) | Every namespace in §11/§12. `Operations` is a new `[Feature]` segment (ADR-297). Retention infrastructure is `Infrastructure.Retention` and backup reads are `Infrastructure.Operations`, both under the "Other infrastructure: `Infrastructure.[Area]`" row. Do not invent anything else |
| [`.ai/decisions/adr.md`](../../.ai/decisions/adr.md) | **ADR-009 … ADR-023 are binding.** ADR-011 ("indexes and schema in migrations"), ADR-016, ADR-020 (a failing event handler fails the command after commit, see §11.6) and ADR-021 are the ones this slice turns on |
| [`.ai/command-handler-pattern.md`](../../.ai/command-handler-pattern.md) | §3 handler shape; §6 the rule-placement table. Hold computation belongs to the eraser (the context's own knowledge); ledger state transitions belong to the aggregate |
| [`.ai/read-side-pattern.md`](../../.ai/read-side-pattern.md) | §4 rule 5 **"a query never writes"**, which is why the snapshot issue is recorded by a command dispatched by the controller (ADR-307); **§5 "reads that span aggregates"**, which is the licence for the snapshot sources and the review report to read across contexts; rule 4 pagination |
| [`.ai/persistence-ef-core.md`](../../.ai/persistence-ef-core.md) | §1 the repository contract **without** a delete; §2 the repository (already implemented, do not re-copy); §4 owned mapping; §6 `xmin`; §8 column types; the ADR-021 corrections (`ValueGeneratedNever`, optional `Tags`/`Language`/`ServiceId`) |
| [`.ai/domain-events-and-outbox.md`](../../.ai/domain-events-and-outbox.md) | §3: the ledger-opening handler runs after the anonymisation committed, so a failure there fails the request after the fact. This slice makes that handler infallible in practice (insert-if-absent) and has the worker re-derive missing ledgers (§11.6); §4 the background-service shape |
| [`.ai/enumeration-pattern.md`](../../.ai/enumeration-pattern.md) | The enumerations in §10.1, persisted by `Value` |
| [`.ai/di-registration-guide.md`](../../.ai/di-registration-guide.md) | §3 validators as `IValidator<T>`; ports registered as `IEnumerable<T>` (multiple erasers, purge sources, snapshot sources); §4 lifetimes |
| [`.ai/host-composition.md`](../../.ai/host-composition.md) | §1 `Program.cs`; §3 lifetimes (the worker is a Singleton that creates a scope per unit); §5 never-register list; §6 configuration, with **no secret in `appsettings.json`** |
| [`.ai/modules/persistence.md`](../../.ai/modules/persistence.md) | "Soft delete is the default; clinical and financial records are never hard-deleted (SRS C-3, D-6)". This slice makes that sentence enforceable |
| [`.ai/feature-creation-checklist.md`](../../.ai/feature-creation-checklist.md) | File order within each phase |
| [`HO-ACC-001`](HO-ACC-001.md) | §11.9 change capture and triggers (the model for §12.1's trigger); §12.5 `[PassiveSession]`; ADR-041 anonymisation; §16.1 shared-database rules |
| [`HO-NTF-001`](HO-NTF-001.md) §12.2 | The worker, work-cycle and work-queue shape, copied for `RetentionWorker` |

---

## 7. Verified platform facts

HO-CAT-001 F1–F10, HO-ACC-001 F11–F18, HO-ADM-001 F19–F22, HO-NTF-001 F23–F27 and HO-APT-001 F28–F30
apply. The following were established by reading the governing docs and the baseline code. **F32 and
F35 must be re-confirmed** against the merged code in Phase A step 1:

| # | Fact | Consequence |
|---|---|---|
| F31 | `IAggregateRootRepository<T>` has `GetByIdAsync`, `GetByFilterAsync`, `ExistAsync`, `SaveAsync`, `UpdateAsync` ×2, and **no delete** | Application code can hard-delete only through `DbContext.Remove`, the removal of an owned-collection item, `ExecuteDelete`, or raw SQL. Those are the four paths §12.1 guards |
| F32 | HO-CAT-001's `UpdateMedicalProfile` **replaces** the allergy, chronic-condition and medication collections wholesale, so EF emits `DELETE` on `clinic.cat_allergy`, `clinic.cat_chronic_condition` and `clinic.cat_medication` rows | A blanket `DELETE` trigger on every `clinic.*` table would break a shipped command. These tables are **`CurrentState` / `AggregateOnly`** (§8.2). Their history is the `DataChange` audit trail. Re-confirm the table names |
| F33 | HO-ACC-001's migration already installs `BEFORE UPDATE OR DELETE` and `BEFORE TRUNCATE` triggers on `audit.audit_entry` | Do **not** add a second trigger there. U-A1/I-A3 accept HO-ACC-001's triggers as the protection for that table |
| F34 | EF Core `ExecuteDelete`/`ExecuteUpdate` **bypass** `SaveChangesInterceptor`s | The interceptor alone is not enough. The triggers (database) and the source scan (build) cover the bypass |
| F35 | PostgreSQL: a table **owner** can `ALTER TABLE … DISABLE TRIGGER`. Row triggers are skipped when `session_replication_role = replica`, which only a superuser can set | Triggers protect only when the runtime role is neither owner nor superuser. ADR-300 and the startup check (§12.8) |
| F36 | A `TRUNCATE` does not fire row-level `DELETE` triggers | Each protected table needs a **statement-level** `BEFORE TRUNCATE` trigger as well |
| F37 | `HttpContext` cookies are sent on a same-origin `fetch` unless `credentials: 'omit'` | The browser omits credentials on the web-vitals beacon, so no session or CSRF check applies (HO-XCT-002 ADR-FE-146). The endpoint ignores identity regardless |
| F38 | HO-ACC-001's session middleware records staff activity on every non-`[PassiveSession]` staff endpoint | A 60 s snapshot poll or a 15 s ping would keep an unattended terminal's session alive for ever. **Both endpoints are `[PassiveSession]`** (ADR-306, ADR-308) |

---

## 8. Domain model

### 8.1 Context and aggregate boundaries

```
Operations context — schema `ops` (plus schema `retention` for the trigger function only)
ErasureRequestAggregate      (root, Id == CustomerAccountId)        ops.erasure_request
└── Scopes [owned many ≤ 32] ErasureScope                             ops.erasure_scope
RetentionRunAggregate        (root)                                  ops.retention_run
└── Passes [owned many ≤ 16] RetentionPassResult                      ops.retention_pass_result
RestoreTestRecordAggregate   (root, append-only)                     ops.restore_test_record
└── Checks [owned many ≤ 50] RestoreCheckResult                        ops.restore_check_result
WebVitalBatchAggregate       (root, append-only, no events)          ops.web_vital_batch
└── Samples [owned many ≤ 10] WebVitalSample                           ops.web_vital_sample
BackupRun                    (read-only table, written by the backup job — ADR-312)   ops.backup_run

Existing roots touched (through their own public methods only)
CustomerAccountAggregate     Anonymise — staff-initiated erasure and re-application (HO-ACC-001 §8.10a)
CustomerAddressBookAggregate / CustomerNotificationPreferencesAggregate   Clear — verification in the Account eraser
NotificationAggregate / NotificationRequestAggregate   Redact(Retention) / RedactVariables (HO-NTF-001, §5.3 amendment)
PolicyAcceptanceAggregate    RedactIpAddress (HO-ADM-001, §5.3 amendment)
```

**One aggregate is mutated per command**, with two recorded exceptions. Executing an erasure scope
lets the context's eraser save that context's aggregates and then updates the ledger (ADR-302).
Re-applying erasures after a restore loops over accounts (ADR-314). Every other command here mutates
exactly one root.

### 8.2 Record classification — ADR-298

Every table in the EF model has **one entry** in `RecordClassification` (Shared, §10.3):
`(Schema, Table, RecordClass, HardDeletePolicy, Basis)`.

| `RecordClass` | Meaning | Default `HardDeletePolicy` | Retention rule |
|---|---|---|---|
| `Clinical` | Anything recording the care of a cat: profile identity, vaccinations, treatments, weights, appointments, visits, notes, surgery, prescriptions, lab results | **Forbidden** | D-8: `retention.clinicalYears` after the cat's last visit or death. **End = review flag only** |
| `Financial` | Invoices, payments, credit notes, refunds, till and COD reconciliation, price and tax history | **Forbidden** | D-9: `retention.financialYears` after the document date. **End = review flag only** |
| `Regulatory` | Controlled-drug register, consent records, import permits and certificates, policy versions and acceptances, the erasure ledger | **Forbidden** | Statutory (Q-1). **End = review flag only** |
| `Audit` | Audit entries, retention runs, restore-test records | **Forbidden** | Indefinite (D-5) |
| `CustomerPersonal` | Customer account, address book, preferences | Root **Forbidden** (anonymised in place, HO-ACC-001 ADR-041); owned collections **AggregateOnly** | Anonymised on request; not otherwise expired |
| `CurrentState` | Owned collections that an aggregate method legitimately **replaces** (allergies, conditions, medications, weekly hours, setting values) | **AggregateOnly** | History lives in the `DataChange` audit rows |
| `Operational` | Configuration and staff records (branches, rooms, services, rosters, leave, staff accounts, templates, content) | **Forbidden** for roots; owned current-state children **AggregateOnly** | Kept (attribution, C-9) |
| `Notification` | The notification log and requests | **Forbidden** (redact in place) | Content redacted after `retention.notificationContentMonths`; metadata kept (ADR-305) |
| `Security` | Sessions and password-reset tokens | **Purgeable** | Purged `retention.securityArtefactDays` after expiry or end |
| `Media` | Boarding photos and videos (HO-BRD-001) | **Purgeable** through its purge source only | D-10: purged after `retention.boardingMediaMonths` unless attached to an incident |
| `Telemetry` | Web-vitals batches and samples; backup-run rows | **Purgeable** | `retention.webVitalsDays`; backup runs 400 days |

**`HardDeletePolicy`**:
- **`Forbidden`**: no delete by any path. Enforced by the database trigger (§12.1), the interceptor and
  the source scan.
- **`AggregateOnly`**: a row may disappear **only** as an owned entity removed through its owning
  aggregate, in a save where the owner is itself tracked and not deleted. No trigger, because
  a trigger cannot tell an aggregate replace from a raw delete. The interceptor and the source scan
  enforce it.
- **`Purgeable`**: deleted **only** by `RetentionPurger` (§12.2) or a registered `IRetentionPurgeSource`,
  under the schedule. The interceptor refuses a tracked delete of a purgeable **root** outside that
  path.

**Classification rules for a table not listed in §10.3** (future contexts, C-ALL-1). Apply them in
this order:
1. It records care given to a cat → `Clinical`.
2. It records money owed, paid or refunded, or a price or tax in force → `Financial`.
3. The law names it (controlled drugs, consent, import documents, accepted policy text) → `Regulatory`.
4. It is an owned collection that an aggregate method replaces wholesale, and it is not itself a record
   under 1–3 → `CurrentState`.
5. Anything else that accumulates history → `Operational`.
6. Only sessions, tokens, telemetry and D-10 media are purgeable. **Nothing else becomes `Purgeable`
   without a new ADR.**

A table that fits none of these, or two of them → **§20.4 stop**. Do not guess.

### 8.3 `ErasureRequestAggregate` — ADR-302

`src/Domain/CatEcommerce.Domain/Aggregates/Operations/ErasureRequestAggregate.cs` · namespace
`CatEcommerce.Domain.Aggregates.Operations`. All setters private; collections as
`IReadOnlyList<T>` over a private list.

| Property | Type | Notes |
|---|---|---|
| `Id` | `Guid` | **= `CustomerAccountId`** (1:1). A second anonymisation event finds the existing ledger |
| `RequestedOn` | `DateTimeOffset` | From the anonymisation event's `TimeStamp` |
| `Source` | `ErasureRequestSource` | `SelfService` \| `StaffOnBehalf` \| `RestoreReconciliation` |
| `Status` | `ErasureRequestStatus` | `Open` → `Completed` |
| `CompletedOn` | `DateTimeOffset?` | |
| `Scopes` | `IReadOnlyList<ErasureScope>` | One per registered eraser `ContextKey` |

`ErasureScope` (owned, `Domain.Entities`): `Id`, `ContextKey` (≤ 50), `Status` (`ErasureScopeStatus`:
`Pending` \| `Held` \| `Completed` \| `Failing`), `HeldUntil` (`DateOnly?`), `Attempts` (`int`),
`LastAttemptOn?`, `LastErrorCode?` (≤ 100, **a code, never a message or a value**), `CompletedOn?`.

| Method | Invariants | Event |
|---|---|---|
| `Open(ErasureOpenDto)` | ≥ 1 context key, distinct, each ≤ 50; every scope starts `Pending` | `ErasureRequestOpenedEvent` (`CustomerAccountId`, `Source`) |
| `AddMissingScopes(keys)` | Adds a `Pending` scope for any registered key not yet present (a context added after the request). Reopens a `Completed` request. No-op when there is nothing to add | `ErasureScopesAddedEvent` (only when added) |
| `RecordScopeOutcome(ErasureScopeOutcomeDto)` | Scope exists and is not `Completed`. `Completed` → completed-on. `Held(until)` → `until` strictly after today, else `InvalidHoldDate`. `Failed(code)` → `Failing`, attempts + 1, code stored. When **every** scope is `Completed` → `Status = Completed` | `ErasureScopeCompletedEvent` / `ErasureScopeHeldEvent` / `ErasureScopeFailedEvent`; plus `ErasureRequestCompletedEvent` on the last one |

The ledger contains **no personal data**. It holds a customer id that is already anonymised, context
keys, dates and codes. That is why it can be `Regulatory` and kept indefinitely: it is the proof that
the request was honoured.

### 8.4 `RetentionRunAggregate` — ADR-303

`Trigger` (`Scheduled` \| `Manual`), `RequestedBy`, `RequestedOn`, `StartedOn?`, `FinishedOn?`,
`Status` (`Requested` → `Running` → `Succeeded` \| `CompletedWithErrors`), `Passes` (owned
`RetentionPassResult`: `PassKind`, `Cutoff?`, `Examined`, `Affected`, `Held`, `Failed`, `ErrorCodes`
(≤ 10 distinct codes), `DurationMs`).

Methods: `Request`, `Start`, `RecordPass`, `Finish`, each guarded by status. Each raises one event, so
`RetentionRunFinishedEvent` gives the audit handler a single place to write the run's `BusinessEvent`
summary. Excluded from change capture (§5.3), because the summary row replaces what would otherwise be
seven `DataChange` rows per night.

### 8.5 `RestoreTestRecordAggregate` — ADR-313

Append-only. `Record(RestoreTestDto)` is **one-shot**: a second call → `RestoreTestAlreadyRecorded`.
No other public mutator exists. A correction is a new record carrying `SupersedesId`.

| Property | Type | Rule |
|---|---|---|
| `PerformedOn` | `DateOnly` | Not future |
| `PerformedByStaffId` | `Guid` | From `UserContext`, never from the request |
| `Environment` | `string` ≤ 100 | e.g. "drill host, isolated" |
| `BackupSetLabel` | `string` ≤ 200 | pgBackRest label restored |
| `SimulatedFailureAt` | `DateTimeOffset` | The instant the drill treats as the failure |
| `RecoveredTo` | `DateTimeOffset` | The latest committed transaction present after restore. `≤ SimulatedFailureAt` → else `InvalidRestoreTestTimes` |
| `RestoreStartedAt` / `RestoreCompletedAt` | `DateTimeOffset` | `Completed > Started` → else `InvalidRestoreTestTimes` |
| `AchievedRpoMinutes` | `decimal(8,2)` | **Computed by the aggregate** = `SimulatedFailureAt − RecoveredTo` |
| `AchievedRtoMinutes` | `decimal(8,2)` | **Computed** = `RestoreCompletedAt − RestoreStartedAt` |
| `Checks` | owned `RestoreCheckResult` (`Name` ≤ 100, `Passed`, `Detail` ≤ 500) | ≥ 1 → else `RestoreTestChecksRequired` |
| `Outcome` | `RestoreTestOutcome` | **Computed**: `Passed` iff every check passed **and** RPO ≤ `RecoveryObjectives.RpoMinutes` (15) **and** RTO ≤ `RecoveryObjectives.RtoMinutes` (240). Otherwise `Failed`. The caller never supplies it |
| `DrillOutput` | `string` ≤ 20 000 | The drill script's JSON, pasted. Stored as text and never rendered as markup |
| `Notes` | `string?` ≤ 2 000 | |
| `SupersedesId` | `Guid?` | An earlier record in the same context |

Raises `RestoreTestRecordedEvent` (`Id`, `Outcome`, `PerformedOn`).

### 8.6 `WebVitalBatchAggregate` — ADR-309

`ReceivedOn`, `BuildId` (≤ 64, `^[A-Za-z0-9._-]+$`), `Samples` (owned `WebVitalSample`: `Metric`
(`WebVitalMetric`: `LCP`, `INP`, `CLS`, `FCP`, `TTFB`), `Value` (`decimal(12,4)`, ≥ 0; ms except
`CLS`), `RouteTemplate` (≤ 200, §11.2 pattern), `NavigationType` (≤ 20, allow-list), `EffectiveConnectionType`
(`4g` \| `3g` \| `2g` \| `slow-2g` \| null), `ViewportClass` (`Narrow` < 768 \| `Medium` < 1280 \| `Wide`)).
`Record(dto)`: 1–10 samples; value bounds per metric (`LCP`/`FCP`/`TTFB`/`INP` ≤ 120 000 ms, `CLS` ≤ 50).
**No event, no identity fields.** `CreatedBy` is set from the anonymous `UserContext` (`Guid.Empty`)
**even when a caller is signed in**. The handler overwrites it (§11.4).

### 8.7 `BackupRun` — ADR-312

Not an aggregate. It is a read model over a table that only the backup job writes. Columns: `id uuid`,
`kind text` (`full` \| `diff` \| `archive-check` \| `erasure-journal`), `started_on timestamptz`,
`finished_on timestamptz`, `succeeded boolean`, `repository text` (`repo1` \| `repo2`),
`backup_label text null`, `size_bytes bigint null`, `wal_archived_until timestamptz null`,
`error_summary text null` (≤ 500, **no secret**: the script strips the connection string and keys).
Mapped read-only in `Read` (`HasKey(id)`, no repository, never tracked). The migration creates it.
`deploy/database/roles.sql` grants **`INSERT` only** to `catecommerce_backup_reporter`.

### 8.8 Erasure ports — ADR-302, ADR-316

`src/Domain/CatEcommerce.Domain/DomainServices/` (flat). These are ports, placed here for the ADR-025
layering reason.

```csharp
public interface IPersonalDataEraser
{
    string ContextKey { get; }                         // "account", "notification", "administration", "boarding", …
    Task<ErasureOutcome> EraseAsync(Guid customerAccountId, DateTimeOffset asOf, UserContext actor,
                                    CancellationToken ct);
}

public abstract record ErasureOutcome
{
    public sealed record Completed : ErasureOutcome;
    public sealed record Held(DateOnly Until, string Basis) : ErasureOutcome;   // Basis e.g. "D-9 invoice"
    public sealed record Failed(string ErrorCode) : ErasureOutcome;
}
```

**The contract every eraser meets** (U-B7 runs it against each registered implementation):
1. **Idempotent.** A second call after `Completed` is `Completed` and writes nothing.
2. Erases **everything not held** on every call, then returns `Held(earliest-remaining-until)` while
   anything is held.
3. Mutates its own context's aggregates **through their own public methods** and repository. It never
   writes SQL, never touches another context, and never deletes a `Forbidden` row.
4. Never throws for a customer it has no data for. It returns `Completed`.
5. Returns `Failed(code)` for a recoverable problem. An exception is caught by the handler and
   recorded as `Failed("UnhandledEraserException")`.
6. Logs ids and codes only.

**Implementations in this slice** (`Infrastructure/Retention/Erasers/`):

| Eraser | `ContextKey` | Behaviour |
|---|---|---|
| `AccountPersonalDataEraser` | `account` | Account `Anonymised`? If not → `Failed("AccountNotAnonymised")`, which never happens in the normal flow. Address book and preferences not cleared (their HO-ACC-001 handlers failed after commit) → call `Clear` on each through its repository. → `Completed` |
| `NotificationPersonalDataEraser` | `notification` | Every notification of the customer not yet redacted with `Anonymisation` → `Redact(Anonymisation)`; every request → `RedactVariables`. This repeats HO-NTF-001's handler idempotently and covers the case where that handler failed → `Completed` |
| `AdministrationPersonalDataEraser` | `administration` | Policy acceptances with an IP: those with `AcceptedOn + retention.financialYears ≤ today` → `RedactIpAddress` (the §5.3 amendment). Any remaining → `Held(min(AcceptedOn) + years, "NFR-LEG-5 acceptance evidence")`. None → `Completed` |

`ErasureExemptions` (Shared) lists contexts that store **no** customer personal data beyond the id,
each with a reason: Cat (`OwnerId` only; the cat is not a person), Appointments (ids and a
`reasonForVisit`, which is part of the clinical record and retained under D-8, OQ-7). **U-B6**
reflects over every aggregate with a property named `*CustomerId`/`*CustomerAccountId`/`OwnerId` and
fails when its context is neither registered nor exempt.

### 8.9 Purge port — ADR-304

```csharp
public interface IRetentionPurgeSource
{
    string SourceKey { get; }                                   // "boarding-media"
    Task<PurgeResult> PurgeAsync(DateTimeOffset cutoff, int batchSize, CancellationToken ct);
}
public sealed record PurgeResult(int Examined, int Purged, int Held, int Failed);
```

`IRetentionPurger` (Domain.DomainServices port, implemented by `Infrastructure/Retention/RetentionPurger`)
deletes **only** from the tables it has a method for. There are no string table names from outside:

| Method | Deletes | Cutoff |
|---|---|---|
| `PurgeCustomerSessionsAsync` / `PurgeStaffSessionsAsync` | rows with `ended_on` or `expires_on` < cutoff | `now − retention.securityArtefactDays` |
| `PurgePasswordResetTokensAsync` | expired or used < cutoff | same |
| `PurgeWebVitalBatchesAsync` | `received_on` < cutoff (samples cascade, same class) | `now − retention.webVitalsDays` |
| `PurgeBackupRunsAsync` | `finished_on` < cutoff | `now − 400 days` (`RetentionRules.BackupRunDays`) |

Each is `ExecuteDeleteAsync` with a `LIMIT`-style batch (`WHERE id IN (SELECT id … LIMIT n)`), and
asserts at startup that its table is `Purgeable` in `RecordClassification` (an `InvalidOperationException`
at the first call otherwise).

### 8.10 Review port

```csharp
public interface IRetentionReviewSource
{
    string SourceKey { get; }
    Task<IReadOnlyList<RetentionReviewCount>> CountPastRetentionAsync(DateOnly asOf, CancellationToken ct);
}
public sealed record RetentionReviewCount(RecordClass RecordClass, string RecordKind, int Count, DateOnly? OldestAnchor);
```

This slice implements `CatClinicalReviewSource`: cats whose `DeceasedOn`, or whose latest `Completed`
appointment, or (with neither) whose `CreatedDate`, is older than `retention.clinicalYears`. The report
states: **"Eligible for review. Nothing is deleted automatically."**

### 8.11 Offline snapshot — ADR-306

A Read-side composition (no aggregate). `IFrontDeskSnapshotSource` (Read, flat in `Queries/Operations`
is wrong; it lives in `Read.QueryHandlers.Operations` as an interface consumed only by the handler):
`string SectionKey`, `string? RequiredAction`, `Task<object?> BuildAsync(Guid branchId, DateOnly localDate,
TimeZoneInfo zone, CancellationToken ct)`.

| Source | Section | Required action | Reads |
|---|---|---|---|
| `ClinicDayListSnapshotSource` | `appointments` | `Appointments.View` | `clinic.appointment` for the branch and local date, in status `Booked`, `CheckedIn`, `InConsultation`, `Completed`, `NoShow`, and walk-ins in the queue. **Not** `Held`, `Cancelled` or expired. Joined to the cat (name, high-risk flag, handling note), the customer (display name, primary mobile), the service snapshot name, the performer and the room |
| `BoardingTaskSnapshotSource` | `boardingTasks` | `BoardingTasks.View` (C-BRD-1) | HO-BRD-001's care tasks for the branch and local date |

Local-date boundaries are computed with HO-ADM-001's `BranchCalendar`/time-zone helpers (ADR-045).
**Never with `DateTime.Today`.**

### 8.12 Exceptions

Reuse `DomainRuleViolationException`. One new Infrastructure exception,
`ProtectedRecordDeletionException(schema, table, policy)`. It is a programming error: it is raised by
the interceptor and mapped to `409 RecordIsRetained` (§12.5), and it is logged at `Error` with the
table name and correlation id.

---

## 9. ADRs to record during this build

Append to [`.ai/decisions/adr.md`](../../.ai/decisions/adr.md) in the implementation commit of the
phase that needs each one. **This handover owns ADR-297 … ADR-316** (reserved by the coordinator; the
README ADR table is updated by its owner, OQ-1).

| ADR | Decision | Why it must be recorded |
|---|---|---|
| **ADR-297** | The **`Operations`** bounded context: folder and namespace segment `Operations`, schema `ops`, owning the roots in §8.1. Retention infrastructure lives in `Infrastructure.Retention` and backup reads in `Infrastructure.Operations` | Not in `.ai/bounded-contexts.md` or `.ai/namespace-conventions.md` (OQ-2) |
| **ADR-298** | **Every table is classified** in `RecordClassification` (Shared) as one `RecordClass` with one `HardDeletePolicy`, under §8.2's ordered rules. `RecordClassificationConventionTests` enumerates the EF model, including owned tables, and **fails on any unclassified table** | "Never hard-deleted" has to be true for tables that do not exist yet. A test that knows every table is the only thing that keeps it true |
| **ADR-299** | **Hard deletes of `Forbidden` tables are refused at three layers.** (1) The database: `retention.refuse_hard_delete()` via a `BEFORE DELETE` row trigger and a `BEFORE TRUNCATE` statement trigger on every `Forbidden` table (F36). (2) EF: `HardDeleteGuardInterceptor`. (3) Build: a source scan allowing `ExecuteDelete` and raw `DELETE`/`TRUNCATE` only in `RetentionPurger` and migrations. `AggregateOnly` owned tables get no trigger (F32) | The database is authoritative ("any application path", AC-20). The interceptor gives a clear error before SQL. The scan catches the bypass (F34) at build time |
| **ADR-300** | **Production runs as a non-owner, non-superuser role.** `catecommerce_migrator` owns every schema and applies migrations. `catecommerce_app` has `SELECT, INSERT, UPDATE, DELETE` (DELETE is needed for `AggregateOnly` and `Purgeable` tables; triggers refuse the rest), **no `TRUNCATE`, no DDL, no ownership**. `catecommerce_backup_reporter` has `INSERT` on `ops.backup_run` only. Outside `Development`/`Testing` the host **refuses to start** if the current role owns any table in the solution's schemas or is a superuser | A table owner can disable a trigger (F35), so without this the triggers are advisory |
| **ADR-301** | **Retention periods are settings; the end of a statutory period deletes nothing.** Clinical, financial, regulatory and audit classes produce a *review-eligible* count at the end of their period. Only held personal data inside them is anonymised when its hold ends. Settings are appended to HO-ADM-001's registry (§10.2), Administrator-only and audited like every setting | SRS Q-1 is open, and C-3 says "never hard-deleted by application logic". A wrong default must be unable to destroy anything |
| **ADR-302** | **The erasure ledger.** `CustomerAccountAnonymisedEvent` opens an `ErasureRequestAggregate` (id = customer id) with one scope per registered `IPersonalDataEraser`. The retention worker's erasure pass executes due scopes (`Pending`, `Failing` with back-off, `Held` whose date has passed). The eraser saves its own context's aggregates and the ledger records the outcome. That is a **recorded rule-1 exception**, safe because erasers are idempotent (§8.8 contract) | FR-ACC-11 and D-11 require erasure "subject to D-8 and D-9" across contexts that are built by ten different handovers. Without a ledger, a held item is never revisited |
| **ADR-303** | **`RetentionWorker`** is one `BackgroundService` in the Api host, on HO-NTF-001's worker shape. The erasure pass runs every 15 min. The daily passes (purges, redaction, review counts) run once per day at `retention.sweepHour` in the **main branch's** time zone. A run is a `RetentionRunAggregate` with one pass result per pass and one `BusinessEvent` audit summary. Batches are bounded, one command per unit, and an error in one unit never stops the pass. Disabled in `Testing`; tests call `RetentionWorkCycle` | The third asynchronous element in a design that calls itself nearly synchronous (`.ai/architecture.md` §5). NFR-PRF-7 "batch jobs … on separate workers" is met in-process by bounded batches outside opening hours |
| **ADR-304** | **Purge = hard delete of `Purgeable` classes only**, by `RetentionPurger` methods with fixed tables, or by registered `IRetentionPurgeSource`s (media: object first, then row; incident-attached media are held). Each batch writes one `BusinessEvent` audit row with the class, cutoff and count. Rows are not audited individually | D-10 and security hygiene require deletion. Limiting it to named methods means no caller can pass a table name |
| **ADR-305** | **Notification content is redacted, not deleted**, `retention.notificationContentMonths` after creation, via HO-NTF-001's `Redact(Retention)` (the §5.3 amendment), which also nulls the destination. Channel, template, status, timestamps and recipient id are kept | SRS §5.1 calls the notification log an audit record. HO-NTF-001 §4 deferred log retention to this slice |
| **ADR-306** | **The offline snapshot is one composite, permission-filtered read** (`GET /api/staff/offline-snapshot`). It covers the active branch's local day, with minimal fields only (§12.4), `Cache-Control: no-store, private`, a strong ETag over the content and `304` on match. It carries `validUntil` = end of the branch-local day + 2 h, `staffAccountId`, `idleTimeoutMinutes` and `refreshAfterSeconds`. It is **`[PassiveSession]`** (F38). Sections the caller may not see are omitted and listed with the reason | Caching the per-area endpoints would store more personal data than the board shows, would not give a receptionist the boarding list, and would give no single moment the data was true at |
| **ADR-307** | **Issuing snapshot content is audited** (NFR-SEC-8, since it holds handling notes and medication instructions). The controller runs the query, and on a non-`304` result dispatches `RecordOfflineSnapshotIssuedCommand`, which writes one `BusinessEvent` row (branch, local date, section counts, ETag). A `304` writes nothing | "A query never writes" (`.ai/read-side-pattern.md` §4 rule 5). This is the HO-ADM-001 audit-export pattern |
| **ADR-308** | **`GET /api/system/ping`**: public, `[PassiveSession]`, `204`, `Cache-Control: no-store`, no body, `ping` rate limit (120/min/IP). It is the browser's server-reachability probe | `navigator.onLine` reports the LAN, not the server. A probe that extended sessions or was cacheable would lie |
| **ADR-309** | **Web-vitals ingestion is anonymous telemetry.** `POST /api/telemetry/web-vitals` is public and rate-limited (`telemetry`, 30/min/IP). The payload is bounded (≤ 10 samples, ≤ 4 KB). It stores **no IP, user id, session, user agent or full URL**, only a route template and a viewport class. The handler overwrites any identity with the anonymous context. Excluded from change capture (§5.3). Purged after `retention.webVitalsDays` | NFR-PRF-1 is a p75 field metric. Telemetry that could identify a customer would need consent and a legal basis this product has not chosen |
| **ADR-310** | **p75 is computed in PostgreSQL** (`percentile_cont(0.75) WITHIN GROUP (ORDER BY value)`) per route template, metric and viewport class, over a range ≤ 93 days. A verdict is given only with ≥ `WebVitalBudgets.MinimumSamples` (50) samples. Budgets live in `WebVitalBudgets` (LCP 2500 ms, INP 200 ms, CLS 0.1) | One definition of "p75 user", so the report and the acceptance evidence agree |
| **ADR-311** | **Backup is pgBackRest with continuous WAL archiving** (`archive_timeout = 60 s`): weekly full, daily differential, 35-day recovery window. There are two repositories. `repo1` is a local volume, for fast restore. `repo2` is an off-site **S3-compatible** bucket with a different provider or region, encrypted with `repo2-cipher-type=aes-256-cbc`, the passphrase coming from the secret store. The configuration and scripts are in `deploy/backup/`. Provider-neutral (NFR-CMP-3) | RPO 15 min needs WAL shipping, not nightly dumps. The design must survive the loss of the whole database host |
| **ADR-312** | **`ops.backup_run` is written only by the backup job**, through `catecommerce_backup_reporter` (`INSERT` only). It is read by the API as a read model. This is a **recorded exception** to "every write goes through an aggregate": the job has no application identity (§4.3), and the row describes the job, not the business | Without it, backup health is invisible in the product until the day it matters |
| **ADR-313** | **A restore test is an append-only, attributed record** entered by an Administrator from the drill's JSON output. **RPO, RTO and the outcome are computed by the aggregate**, never supplied. The next test is due `operations.restoreTestIntervalDays` (92) after the last *passed* test. The due / overdue state is computed read-side | NFR-AVL-5 "tested … and the test recorded". A self-declared "pass" would record nothing |
| **ADR-314** | **Restores and erasure.** Each night the backup job exports an **erasure journal** (customer ids and requested-on only, no personal data) into both repositories. After any restore to a point earlier than the latest WAL, the runbook requires `POST /api/admin/retention/erasures/reapply` with the journal entries later than the recovery point. `ReapplyErasuresAfterRestoreCommand` anonymises each account again, idempotently, with source `RestoreReconciliation`. That is a recorded rule-1 exception (a loop over accounts). Backups expire at 35 days, so erased data leaves the backups within 35 days | A restore would otherwise silently resurrect people who asked to be forgotten |
| **ADR-315** | **Actions** appended to the one `ActionRoles` table (§12.3): `Offline.FrontDeskSnapshot`, `Retention.View`, `Retention.Run`, `Customers.Erase`, `Operations.View`, `Operations.RecordRestoreTest`, `Telemetry.View`. "Compliance officer" and "security officer" are not Appendix D roles, so they map to Administrator, plus Accountant for read-only retention views, as the audit log does (HO-ADM-001 OQ-6 precedent) | The backlog names roles the matrix does not have |
| **ADR-316** | **The erasure and classification contracts bind every future context** (C-ALL-1…4). U-A1 (every table classified) and U-B6 (every personal-data context has an eraser or an exemption) are the enforcement, and a context cannot merge while either fails | This slice is last, but the rules must hold for tables added after it too |

### 9.1 Open questions — raised, not resolved

Each has a default so that the slice is buildable. **Do not change a default without an Amended row.**

| # | Question | Owner | Default in this build |
|---|---|---|---|
| **OQ-1** | The amendments in §5.3 and the README ADR-table row (ADR-297…316, ADR-FE-139…148) need writing into the other files. This author was told not to edit them | README owner / coordinator | Implementation of the affected step waits for the Amended rows. The code edits are already in §13 |
| **OQ-2** | `.ai/bounded-contexts.md` and `.ai/namespace-conventions.md` need the `Operations` context | Architect (separate task) | ADR-297 records it |
| **OQ-3** | **SRS Q-1**: jurisdiction and statutory periods | Owner / legal | 7 years clinical after last visit or death; 6 years financial; 12 months boarding media; everything else per §10.2 |
| **OQ-4** | Is 24 months right for keeping notification content? | Owner | 24 months, then redact (ADR-305) |
| **OQ-5** | Personal data inside immutable audit entries (IP, before/after values) is never erased. Is that acceptable under the governing law? | Owner / legal | Accepted residual; HO-ACC-001 already omits anonymisation before-values |
| **OQ-6** | How should a `Merged` duplicate record be erased? | Owner | Refused with `CustomerRecordMerged`; erase the survivor |
| **OQ-7** | Is an appointment's `reasonForVisit` part of the clinical record (retained) or personal data (erased)? | Clinical lead | Clinical, retained under D-8 |
| **OQ-8** | Production hosting: self-managed PostgreSQL in a container, or a managed PostgreSQL with native PITR? | Owner / deployment | Self-managed with pgBackRest. If managed is chosen, `deploy/backup/` is replaced by the provider's PITR; the drill, `ops.backup_run` reporting and restore-test records stay |
| **OQ-9** | **Partly closed 2026-09-23 — README C-5.** Object storage now exists and has an owner: [`HO-FIL-001`](HO-FIL-001.md), on Google Cloud Storage (ADR-318). **Its backup still has none.** Note ADR-318 turns **object versioning and soft-delete off** on purpose, so the erasure ledger can guarantee deletion — which means the obvious backup answer (versioning) is unavailable here | Owner | A **dual-region bucket** plus a scheduled copy to a separate project, so a compromised project cannot delete both. HO-FIL-001 OQ-3 carries the same question. The drill's `media-store` check becomes real once a target exists |
| **OQ-10** | Should the offline snapshot include the customer's phone number? | Owner | **Yes**: during an outage reception has to call people. Masking it would defeat the purpose |
| **OQ-11** | Should administrators be notified (in-app) when a restore test is overdue or a backup is stale? | Owner | No notification. Status endpoint and an in-app banner only (HO-XCT-002) |
| **OQ-12** | Must the privacy policy mention anonymous performance telemetry and the 35-day backup window? | Owner / legal | Yes; the content itself is HO-ADM's editor. Flagged to the ACCEPTANCE gate |
| **OQ-13** | Should erasure honour a legal hold (litigation, inspection) that overrides even held-until dates? | Owner / legal | Not built. No AC asks for it |

---

## 10. Enumerations, constants and violation codes

### 10.1 Enumerations — `Shared/Common/Enums/`, derived from `Enumeration`, persisted by `Value`

| Type | Members |
|---|---|
| `RecordClass` | `Clinical`, `Financial`, `Regulatory`, `Audit`, `CustomerPersonal`, `CurrentState`, `Operational`, `Notification`, `Security`, `Media`, `Telemetry` |
| `HardDeletePolicy` | `Forbidden`, `AggregateOnly`, `Purgeable` |
| `ErasureRequestSource` | `SelfService`, `StaffOnBehalf`, `RestoreReconciliation` |
| `ErasureRequestStatus` | `Open`, `Completed` |
| `ErasureScopeStatus` | `Pending`, `Held`, `Completed`, `Failing` |
| `ErasureRequestChannel` | `InPerson`, `Phone`, `Email`, `Letter` |
| `RetentionRunTrigger` | `Scheduled`, `Manual` |
| `RetentionRunStatus` | `Requested`, `Running`, `Succeeded`, `CompletedWithErrors` |
| `RetentionPassKind` | `Erasure`, `PurgeSessions`, `PurgeResetTokens`, `PurgeWebVitals`, `PurgeBackupRuns`, `PurgeSources`, `RedactNotifications`, `ReviewCounts` |
| `RestoreTestOutcome` | `Passed`, `Failed` |
| `WebVitalMetric` | `LCP`, `INP`, `CLS`, `FCP`, `TTFB` |
| `ViewportClass` | `Narrow`, `Medium`, `Wide` |

### 10.2 Settings appended to HO-ADM-001's registry — `SettingDefinitions.cs` (append only)

| Key | Group | Type | Default | Bounds | Branch override | Consumer |
|---|---|---|---|---|---|---|
| `retention.clinicalYears` | Retention | Integer | 7 | 1–50 | ❌ | Review report; held clinical personal data (D-8) |
| `retention.financialYears` | Retention | Integer | 6 | 1–50 | ❌ | Review report; held financial / acceptance personal data (D-9) |
| `retention.boardingMediaMonths` | Retention | Integer | 12 | 1–120 | ❌ | `boarding-media` purge source (D-10) |
| `retention.notificationContentMonths` | Retention | Integer | 24 | 1–120 | ❌ | Redaction pass (ADR-305) |
| `retention.securityArtefactDays` | Retention | Integer | 30 | 7–365 | ❌ | Session and token purges |
| `retention.webVitalsDays` | Retention | Integer | 90 | 7–730 | ❌ | Telemetry purge |
| `retention.sweepHour` | Retention | Integer | 2 | 0–23 | ❌ | Daily pass time, main-branch local |
| `operations.restoreTestIntervalDays` | Operations | Integer | 92 | 7–92 | ❌ | Restore-test due date (quarterly is the ceiling, NFR-AVL-5) |
| `offline.snapshotRefreshSeconds` | Operations | Integer | 60 | 30–600 | ✅ | `refreshAfterSeconds` in the snapshot |

### 10.3 Constants — `Shared/Common/Constants/`

- **`RecordClassification`**: `IReadOnlyList<TableClassification(Schema, Table, RecordClass,
  HardDeletePolicy, Basis)>` plus `Find(schema, table)`. **Every table in the model.** For the tables
  already specified:

| Tables | Class | Policy | Basis |
|---|---|---|---|
| `clinic.cat`, `clinic.cat_vaccination`, `clinic.cat_parasite_treatment`, `clinic.cat_weight_record` | Clinical | Forbidden | D-8 |
| `clinic.cat_allergy`, `clinic.cat_chronic_condition`, `clinic.cat_medication` | CurrentState | AggregateOnly | F32; history in audit |
| `clinic.cat_photo` | CustomerPersonal | AggregateOnly | No removal method today |
| `clinic.vaccine` | Operational | Forbidden | Referenced by clinical records |
| `clinic.appointment` (+ its status-change table) | Clinical | Forbidden | D-8 |
| `clinic.waitlist_entry` | Operational | Forbidden | |
| `audit.audit_entry` | Audit | Forbidden (**HO-ACC-001's triggers**, F33) | D-5 |
| `account.customer_account` | CustomerPersonal | Forbidden | HO-ACC-001 ADR-041 |
| `account.customer_address_book`, `account.customer_delivery_address`, `account.customer_notification_preferences`, `account.customer_marketing_consent` | CustomerPersonal | AggregateOnly | `Clear` / remove address |
| `account.customer_session`, `account.staff_session`, `account.password_reset_token` | Security | Purgeable | |
| `account.staff_account` | Operational | Forbidden | C-9 attribution |
| `admin.branch`, `admin.branch_closure`, `admin.room`, `admin.staff_branch_assignment`, `admin.staff_roster`, `admin.roster_exception`, `admin.leave_request`, `admin.clinic_service`, `admin.tax_class`, `admin.business_settings`, `admin.content_item`, `admin.policy_document`, `admin.system_status` | Operational | Forbidden | |
| `admin.service_price`, `admin.tax_rate` | Financial | Forbidden | D-9 (price in force) |
| `admin.policy_version`, `admin.policy_acceptance` | Regulatory | Forbidden | NFR-LEG-5 |
| `admin.branch_opening_interval`, the staff-roster shift tables, `admin.setting_value` | CurrentState | AggregateOnly | Replaced wholesale |
| every `notify.*` log and request table | Notification | Forbidden | ADR-305 |
| `notify.*` template tables | Operational | Forbidden | |
| `ops.erasure_request`, `ops.erasure_scope` | Regulatory | Forbidden | ADR-302 |
| `ops.retention_run`, `ops.retention_pass_result`, `ops.restore_test_record`, `ops.restore_check_result` | Audit | Forbidden | ADR-303, ADR-313 |
| `ops.web_vital_batch`, `ops.web_vital_sample`, `ops.backup_run` | Telemetry | Purgeable | ADR-309, ADR-312 |

  The exact table names for owned tables and for the NTF/APT/ADM roots are whatever those merged
  configurations use. **Transcribe them from the EF model**, and U-A1 proves the list complete. A name
  in this table that does not exist in the model also fails U-A1: the list is exact, not approximate.
- **`ErasureExemptions`**: `(ContextKey, Reason)` pairs (§8.8).
- **`RetentionRules`**: `BatchSize = 500`, `ErasurePassMinutes = 15`, `ErasureBackoffMinutes = [15, 60, 240, 1440]`, `BackupRunDays = 400`, `ReapplyMaxIds = 500`.
- **`RecoveryObjectives`**: `RpoMinutes = 15`, `RtoMinutes = 240`, `BackupStaleHours = 26`, `ArchiveStaleMinutes = 15`.
- **`WebVitalBudgets`**: `LcpMs = 2500`, `InpMs = 200`, `Cls = 0.1m`, `MinimumSamples = 50`, `MaxSamplesPerBatch = 10`, `MaxPayloadBytes = 4096`, `MaxRangeDays = 93`.
- **`OfflineSnapshotRules`**: `ValidityGraceHours = 2`, `SchemaVersion = 1`.
- **`OperationsMessages`**: shared plain-language messages (§3.3).

**Actions**: appended to HO-ACC-001's `Actions` and the **same** `ActionRoles` table (§12.3).

### 10.4 Violation codes — append to `BusinessViolationCodes`

`RecordIsRetained` · `InvalidHoldDate` · `ErasureRequestNotFound` · `ErasureScopeNotFound` ·
`CustomerRecordMerged` · `CustomerAccountNotFound` *(reuse if HO-ACC-001 defines it)* ·
`ReapplyListTooLarge` · `RetentionRunAlreadyInProgress` · `RestoreTestAlreadyRecorded` ·
`InvalidRestoreTestTimes` · `RestoreTestChecksRequired` · `InvalidWebVitalsPayload` ·
`ActiveBranchRequired`.

---

## 11. Specified changes — write side

### 11.1 Commands — `Application/Commands/Operations/`

Namespace `CatEcommerce.Application.Commands.Operations`. Data only. Ids are minted in the controller
or the work cycle, and `ValidateCommand = true` is set everywhere (F3).

| # | Command | Payload | Caller | Phase |
|---|---|---|---|---|
| C1 | `OpenErasureRequestCommand` | `CustomerAccountId`, `RequestedOn`, `Source` | Event handler §11.6 and the worker's self-heal | B |
| C2 | `ExecuteErasureScopeCommand` | `CustomerAccountId`, `ContextKey` | Worker | B |
| C3 | `EraseCustomerOnRequestCommand` | `CustomerAccountId`, `Channel`, `IdentityCheck` (≤ 500), `Reason` (≤ 500) | Admin API | B |
| C4 | `ReapplyErasuresAfterRestoreCommand` | `CustomerAccountIds[]` (1…500), `RestoreReference` (≤ 200) | Admin API | B |
| C5 | `RequestRetentionRunCommand` | `RetentionRunId`, `Trigger` | Admin API and worker | C |
| C6 | `StartRetentionRunCommand` / `RecordRetentionPassCommand` / `FinishRetentionRunCommand` | run id + pass result | Worker | C |
| C7 | `PurgeExpiredRecordsCommand` | `PassKind`, `Cutoff`, `BatchSize` | Worker | C |
| C8 | `RunPurgeSourceCommand` | `SourceKey`, `Cutoff`, `BatchSize` | Worker | C |
| C9 | `RedactExpiredNotificationContentCommand` | `NotificationId` | Worker (one per id) | C |
| C10 | `RecordOfflineSnapshotIssuedCommand` | `BranchId`, `LocalDate`, `SectionCounts` (key → count), `ETag` | Snapshot controller | D |
| C11 | `RecordWebVitalsCommand` | `BatchId`, `BuildId`, `Samples[]` | Telemetry controller | E |
| C12 | `RecordRestoreTestCommand` | `RestoreTestId`, fields of §8.5 except the computed ones and the performer; `Checks[]` | Admin API | F |

### 11.2 Validators — `CommandHandlers/Validators/` (flat), registered as `IValidator<T>`

Shape only:
- C3: `IdentityCheck` and `Reason` required, ≤ 500; `Channel` defined.
- C4: 1…`RetentionRules.ReapplyMaxIds` distinct non-empty ids (else `ReapplyListTooLarge`); `RestoreReference` required.
- C11: 1…10 samples; `RouteTemplate` matches `^/[a-z0-9\-\[\]\(\)/\.]{0,199}$` with **no `?`, `#` or `@`**; `BuildId` pattern; metric defined; `NavigationType` in `navigate|reload|back-forward|back-forward-cache|prerender|restore`; `Value` finite. Anything else → `InvalidWebVitalsPayload` (one generic code, so that the endpoint is not an oracle).
- C12: required strings, lengths, ≥ 1 check, check names distinct.

Time ordering, hold dates and bounds that need `TimeProvider` are **aggregate** rules (§8).

### 11.3 Mappers and DTOs

`OperationsMapper` (static `ToDomainDto()` extensions, passing `UserContext`/`CorrelationId` through).
DTOs in `Domain/Models/` (flat), one per aggregate method.

### 11.4 Command handlers — `CommandHandlers/Operations/`

The `RegisterCatCommandHandler` shape (HO-CAT-001 §11.5), with `DomainRuleViolationException` →
`CommandResponse`.

| Handler | Behaviour |
|---|---|
| **C1 Open** | Ledger exists → `AddMissingScopes(registered keys)` → `UpdateAsync` if changed. Otherwise `new` → `Open` with the keys of every `IPersonalDataEraser` in DI → `SaveAsync`. A `23505` on the primary key (a concurrent open) → treat as exists, reload, and retry once |
| **C2 Execute scope** | Load the ledger; scope `Completed` → success, nothing written. Resolve the eraser by `ContextKey` (unknown key → `Failed("EraserNotRegistered")`). `try { outcome = await eraser.EraseAsync(...) } catch (Exception ex) { log id + type; outcome = Failed("UnhandledEraserException") }`. Then `RecordScopeOutcome` → `UpdateAsync`. **The ADR-302 exception**: the eraser's own saves commit before the ledger save. Idempotency makes a crash between the two safe, because the next pass repeats the scope |
| **C3 Erase on request** | Load `CustomerAccountAggregate`. Not found → `CustomerAccountNotFound`. `Anonymised` → success `{ isReapplication: true }` and no write. `Merged` → `CustomerRecordMerged`. Otherwise `Anonymise(new CustomerAnonymisationDto(now, context))` → `UpdateAsync`. Then **directly** write one `AuditEntryAggregate` (`BusinessEvent`, `Action = "CustomerErasureRequestedByStaff"`, `Reason` = channel + reason, `AfterValue` = `{ channel, identityCheck }`, IP from `IClientIpAccessor`), as HO-ACC-001 C11 does. HO-ACC-001's anonymisation handlers and this slice's C1 handler follow through events |
| **C4 Reapply** | For each id: the C3 logic with channel `RestoreReconciliation` and reason `RestoreReference`. **The ADR-314 exception**: N accounts in one command. Each account is its own `UpdateAsync`, and a failure on one is recorded in the result and does not stop the rest. Returns `{ processed, alreadyErased, notFound, merged }`. One `BusinessEvent` row for the whole call |
| **C5 Request run** | Any run `Requested` or `Running` → `RetentionRunAlreadyInProgress`. Otherwise `SaveAsync` a new `Requested` run |
| **C7 Purge** | `PassKind` → the matching `IRetentionPurger` method (a switch; an unknown kind is a programming error). Writes one `BusinessEvent` audit row with kind, cutoff and count. Returns the count |
| **C8 Purge source** | Resolve by `SourceKey`, call, write one `BusinessEvent` row, return the result |
| **C9 Redact** | Load the notification (HO-NTF-001 repository). Already redacted → success. Otherwise `Redact(Retention)` → `UpdateAsync`. Excluded from audit capture by ADR-066 already |
| **C10 Snapshot issued** | One `AuditEntryAggregate` (`BusinessEvent`, `Action = "OfflineSnapshotIssued"`, `EntityType = "Branch"`, `EntityId = BranchId`, `AfterValue` = `{ localDate, sections, etag }`). No personal data in the row |
| **C11 Web vitals** | `new WebVitalBatchAggregate().Record(dto)` with **`UserContext` replaced by the anonymous context** before `SetDefaultValue`. `SaveAsync(DoNotPublish)` |
| **C12 Restore test** | `SupersedesId` given → it must exist. `new` → `Record` (performer = `UserContext.UserId`) → `SaveAsync` |

### 11.5 Domain events — `Events/Operations/`

Ids, enums, dates and counts only. `ErasureRequestOpenedEvent`, `ErasureScopesAddedEvent`,
`ErasureScopeCompletedEvent`, `ErasureScopeHeldEvent` (`ContextKey`, `HeldUntil`),
`ErasureScopeFailedEvent` (`ContextKey`, `ErrorCode`), `ErasureRequestCompletedEvent`,
`RetentionRunRequestedEvent`, `RetentionRunStartedEvent`, `RetentionPassRecordedEvent`,
`RetentionRunFinishedEvent` (`Status`, pass counts), `RestoreTestRecordedEvent`. HO-ACC-001's
reflection test U-X1 (no personal data on events) covers this assembly.

### 11.6 Domain event handlers — `DomainEventHandlers/Operations/`

| Handler | Handles | Does |
|---|---|---|
| `OpenErasureRequestOnCustomerAnonymisedHandler` | HO-ACC-001 `CustomerAccountAnonymisedEvent` | Source = `SelfService` if the event's `UserContext.Roles` is `["Customer"]`; `RestoreReconciliation` if the correlation belongs to a C4 call (C4 stamps a known `CorrelationId` prefix, `RetentionCorrelation.Reapply`); otherwise `StaffOnBehalf`. Dispatches C1 in the same scope. **Summary comment**: a failure here fails the deletion request after the anonymisation committed (`.ai/domain-events-and-outbox.md` §3). The erasure pass **self-heals** by opening a ledger for any `Anonymised` account that has none (§12.2), so a failure is logged at `Error` and does not lose the request |
| `AuditRetentionRunFinishedHandler` | `RetentionRunFinishedEvent` | One `BusinessEvent` row: run id, trigger, status, pass counts |
| `AuditErasureRequestCompletedHandler` | `ErasureRequestCompletedEvent` | One `BusinessEvent` row: customer id and completed-on. This is the proof point for D-11 |

Data changes to the ledger and restore records are audited by HO-ACC-001's change capture (they are
not excluded).

---

## 12. Specified changes — Infrastructure, read side, Api, host, deploy

### 12.1 Persistence and retention guards

**DbSets and configurations** for the four `ops` roots (§8.1). Schema `ops`. Apply the F15 lines,
`ValueGeneratedNever()` on roots and owned ids (ADR-021), enumerations by `Value`, instants →
`timestamptz`, `DateOnly` → `date`, and `decimal(8,2)` / `decimal(12,4)` as stated. `BackupRunReadModel`
is mapped in the same `DbContext` with `ToTable("backup_run", "ops")`, no navigation, and queried only
with `AsNoTracking()`.

| Index | Kind |
|---|---|
| `erasure_scope (status, held_until)` | due scopes |
| `erasure_request (status, requested_on)` | ledger list |
| `retention_run (status)`, `retention_run (requested_on)` | in-progress check, list |
| `restore_test_record (performed_on)`, `(outcome, performed_on)` | last passed test |
| `web_vital_batch (received_on)` | purge |
| `web_vital_sample (route_template, metric, viewport_class)` + the batch FK | summary |
| `backup_run (kind, finished_on)` | status |

**`HardDeleteGuardInterceptor : SaveChangesInterceptor`** (`Infrastructure/Retention/`). In
`SavingChangesAsync`, for every `EntityState.Deleted` entry: resolve `(schema, table)` from
`entry.Metadata.GetSchema()/GetTableName()`, and look it up in `RecordClassification` (not found →
throw; U-A1 makes that unreachable).
- `Forbidden` → throw `ProtectedRecordDeletionException`.
- `AggregateOnly` → allowed only if the entity type is **owned** and its owner entry is tracked and
  not `Deleted`. Otherwise throw.
- `Purgeable` root via change tracking → throw. Purges go through `ExecuteDelete` in `RetentionPurger`.

It is registered **before** HO-ACC-001's change-capture interceptor in the `AddInterceptors` list, so
that a refused delete is never audited as if it happened.

**Migration `AddRetentionGuards` (Phase A)**, read before commit:
```sql
CREATE SCHEMA IF NOT EXISTS retention;
CREATE OR REPLACE FUNCTION retention.refuse_hard_delete() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  RAISE EXCEPTION 'Hard delete of %.% is not permitted: retained record (C-3, D-6)',
                  TG_TABLE_SCHEMA, TG_TABLE_NAME USING ERRCODE = 'P0001';
END $$;
-- then, generated in C# from RecordClassification for every Forbidden table except audit.audit_entry:
CREATE TRIGGER trg_refuse_hard_delete BEFORE DELETE ON <schema>.<table>
  FOR EACH ROW EXECUTE FUNCTION retention.refuse_hard_delete();
CREATE TRIGGER trg_refuse_truncate BEFORE TRUNCATE ON <schema>.<table>
  FOR EACH STATEMENT EXECUTE FUNCTION retention.refuse_hard_delete();
```
The migration's C# builds these statements by iterating `RecordClassification` **as it stood when the
migration was written**. The list is inlined as literals in the migration, never read at migration
time, because migrations must be reproducible. Any later context that adds a `Forbidden` table attaches
the two triggers in **its own** migration (C-ALL-1), and I-A3 verifies it. `Down` drops the triggers
and the function. The function is `SECURITY INVOKER`, owned by the migrator.

**Migrations**: `AddRetentionGuards` (A), `AddErasureLedger` (B), `AddRetentionRuns` (C),
`AddWebVitals` (E), `AddRestoreTestsAndBackupRuns` (F). Phase D adds no table.

**`RecordClassification` convention test** (U-A1): builds the model from `CatEcommerceDbContext`,
enumerates every entity type's `(schema, table)` including owned types and excluding
`__EFMigrationsHistory`, and asserts **set equality** with `RecordClassification`. It also asserts that
no two entries collide.

**Source scan** (U-A2): reads every `src/**/*.cs` (excluding `Migrations/` and `obj/`), and fails on
`ExecuteDelete(`, `ExecuteDeleteAsync(`, or `ExecuteSqlRaw`/`ExecuteSqlInterpolated`/`SqlQueryRaw`
whose argument text contains `DELETE` or `TRUNCATE` (case-insensitive), outside
`Infrastructure/Retention/RetentionPurger.cs`. It also fails on `DISABLE TRIGGER` and
`session_replication_role` anywhere under `src/`, including migrations.

### 12.2 Infrastructure services and the worker

| Class | Namespace | Notes |
|---|---|---|
| `Retention/HardDeleteGuardInterceptor` | `…Infrastructure.Retention` | §12.1 |
| `Retention/RetentionPurger : IRetentionPurger` | same | §8.9, the only `ExecuteDelete` in `src/` |
| `Retention/Erasers/{Account,Notification,Administration}PersonalDataEraser` | `…Infrastructure.Retention.Erasers`, which `Infrastructure.[Area]` permits one level down; if the ARCHITECTURE gate reads that as an invented sub-namespace, flatten to `…Infrastructure.Retention` (OQ-2) | §8.8 |
| `Retention/CatClinicalReviewSource : IRetentionReviewSource` | same | §8.10 |
| `Retention/RetentionWorkQueue` | same | Read-only due-work queries: due erasure scopes (`Pending`; `Failing` past its back-off; `Held` with `held_until ≤ today`), `Anonymised` accounts without a ledger (self-heal), notifications due for redaction, a `Requested` run. `AsNoTracking`, oldest first, `BatchSize`. **Returns ids and never writes** |
| `Operations/DatabaseRoleGuard` | `…Infrastructure.Operations` | Startup check (ADR-300): outside Development/Testing, queries `pg_roles.rolsuper` for `current_user` and `pg_tables.tableowner` across the solution's schemas. Superuser or any owned table → throw with *"The application is connected as a role that can disable retention triggers. Use catecommerce_app (see deploy/database/roles.sql)."* `Operations:Database:EnforceNonOwnerRole` (default `true`) may be set `false` **only** in Development and Testing. Startup throws if false elsewhere |
| `Configuration/OperationsOptions` | `…Infrastructure.Configuration` | Section `Operations`: `RetentionWorker:Enabled` (true; **false in Testing**), `RetentionWorker:PollSeconds` (60), `Database:EnforceNonOwnerRole` |

**`RetentionWorkCycle`** (Api, `Background/`, Scoped). Each unit is a fresh scope and one command,
with `SystemActors.RetentionWorker` (a fixed Guid, added to `SystemActors` beside HO-NTF-001's) and a
new correlation id. An exception is logged with the id and swallowed.
- `RunErasurePassAsync()`: self-heal (C1 for each anonymised account without a ledger), then C2 for each due scope.
- `RunDailyPassesAsync(trigger)`: C5 (skip if one is in progress) → C6 start → in order `PurgeSessions`,
  `PurgeResetTokens`, `PurgeWebVitals`, `PurgeBackupRuns` (C7, looping batches until fewer than
  `BatchSize` remain, capped at 20 batches per pass), `PurgeSources` (C8 for each registered source),
  `RedactNotifications` (C9 per id, capped), `ReviewCounts` (queries only; counts are stored on the
  pass result) → C6 record each → finish.

**`RetentionWorker : BackgroundService`** (Api, `Background/`): when enabled, it loops every
`PollSeconds`. It runs the erasure pass every `ErasurePassMinutes`. It runs the daily passes when the
main branch's local hour equals `retention.sweepHour` and no run has started in that local date, or
when a `Manual` run is `Requested`. It honours the stopping token and never throws out of
`ExecuteAsync`.

### 12.3 Authorization

Appended to the one `ActionRoles` table (HO-ACC-001 §12.8; HO-ADM-001 §12.3):

| Action | Staff roles | Endpoints |
|---|---|---|
| `Offline.FrontDeskSnapshot` | Reception, Nurse, Vet, Caretaker, Administrator | `GET /api/staff/offline-snapshot`. Sections are then filtered by `Appointments.View` / `BoardingTasks.View`, **resolved against the same `ActionRoles` table** |
| `Retention.View` | Administrator, Accountant | schedule, runs, review, erasure requests |
| `Retention.Run` | Administrator | `POST /api/admin/retention/runs` |
| `Customers.Erase` | Administrator | staff erasure, reapply |
| `Operations.View` | Administrator | backup status, restore tests |
| `Operations.RecordRestoreTest` | Administrator | `POST …/restore-tests` |
| `Telemetry.View` | Administrator, ShopManager | web-vitals summary |

`[PublicEndpoint]` goes on ping and telemetry, and `[PassiveSession]` on ping and the snapshot. **U-A1
of HO-ADM-001** (every endpoint declares its access) must still pass.

### 12.4 Api — the contract HO-XCT-002 consumes

Controllers inject `IInMemoryDispatcher` and `IUserContextProvider` (plus `IActiveBranchAccessor` for
the snapshot). JSON is camelCase, instants are ISO-8601 with offset, and local dates are `yyyy-MM-dd`.
Every write uses the HO-ACC-001 `400` / `401` / `403` / `409` shapes (R1–R8).

| Method | Route | Access · limit | Command / query → success |
|---|---|---|---|
| `GET` | `/api/system/ping` | public · `[PassiveSession]` · `ping` | `204`, `Cache-Control: no-store`, no body |
| `GET` | `/api/staff/offline-snapshot` | `Offline.FrontDeskSnapshot` · `[PassiveSession]` | Q1. `If-None-Match` equal → `304` (no audit). Otherwise C10, then `200` with `ETag`, `Cache-Control: no-store, private`, body below. No active branch → `400 ActiveBranchRequired` |
| `POST` | `/api/telemetry/web-vitals` | public · `telemetry` | C11 → `204`. Body > 4 KB → `413`. Invalid → `400 InvalidWebVitalsPayload`. **Accepts `Content-Type: application/json` or `text/plain`** (a beacon) and parses JSON either way |
| `GET` | `/api/admin/web-vitals/summary?from&to&routeTemplate&viewportClass` | `Telemetry.View` | Q2, range required and ≤ 93 days: `{ from, to, budgets: { lcpMs, inpMs, cls }, rows: [{ routeTemplate, viewportClass, metric, sampleCount, p75, verdict: "Pass" \| "Fail" \| "InsufficientData" }] }`, sorted by route |
| `GET` | `/api/admin/retention/schedule` | `Retention.View` | Q3: every `RecordClass` with its policy, basis, effective period (from settings), action at end (`ReviewOnly` \| `Purge` \| `Redact` \| `AnonymiseOnRequest` \| `Keep`), the table count, and **the list of classified tables** |
| `GET` | `/api/admin/retention/runs?pageNumber&pageSize` · `/{runId}` | `Retention.View` | Q4, paged / detail with passes |
| `POST` | `/api/admin/retention/runs` | `Retention.Run` | C5 (`Manual`) → `201 { retentionRunId, status: "Requested" }`; `409 RetentionRunAlreadyInProgress` |
| `GET` | `/api/admin/retention/review` | `Retention.View` | Q5: `{ asOf, rows: [{ recordClass, recordKind, count, oldestAnchor, sourceKey }], note: "Eligible for review. Nothing is deleted automatically." }` |
| `GET` | `/api/admin/retention/erasure-requests?status&pageNumber&pageSize` · `/{customerAccountId}` | `Retention.View` | Q6: `{ customerAccountId, requestedOn, source, status, completedOn, scopes: [{ contextKey, status, heldUntil, basis?, attempts, lastErrorCode, completedOn }] }`. **No name, phone or email** (they no longer exist) |
| `POST` | `/api/admin/customers/{customerAccountId}/erasure` | `Customers.Erase` | C3 → `200 { isReapplication }`; `404`; `400 CustomerRecordMerged` |
| `POST` | `/api/admin/retention/erasures/reapply` | `Customers.Erase` | C4 → `200 { processed, alreadyErased, notFound, merged }` |
| `GET` | `/api/admin/operations/backup-status` | `Operations.View` | Q7: `{ lastFull, lastDiff, lastArchiveCheck, lastErasureJournal` (each `{ finishedOn, succeeded, repository, label?, sizeBytes? }` or null), `walArchivedUntil, isBackupStale, isArchiveStale, lastRestoreTest: { id, performedOn, outcome, achievedRpoMinutes, achievedRtoMinutes }?, lastPassedRestoreTestOn?, nextRestoreTestDueOn, isRestoreTestOverdue, objectives: { rpoMinutes, rtoMinutes } }` |
| `GET` | `/api/admin/operations/restore-tests?pageNumber&pageSize` · `/{id}` | `Operations.View` | Q8, paged / detail incl. checks, drill output (text) and supersedes chain |
| `POST` | `/api/admin/operations/restore-tests` | `Operations.RecordRestoreTest` | C12 → `201 { restoreTestId, outcome, achievedRpoMinutes, achievedRtoMinutes }` |

**Offline snapshot body (Q1, schema version 1):**
```jsonc
{
  "schemaVersion": 1,
  "branch": { "id": "…", "name": "Main clinic", "timeZoneId": "Asia/Dhaka" },
  "localDate": "2026-10-12",
  "generatedOn": "2026-10-12T10:42:05+06:00",
  "validUntil": "2026-10-13T02:00:00+06:00",          // end of local day + 2 h
  "staffAccountId": "…",                               // who fetched it — the client must match it
  "idleTimeoutMinutes": 30,                            // security.staffIdleTimeoutMinutes
  "refreshAfterSeconds": 60,                           // offline.snapshotRefreshSeconds (branch)
  "sections": {
    "appointments": { "items": [ {
      "appointmentId": "…", "startsOn": "…", "endsOn": "…", "status": "CheckedIn", "isWalkIn": false,
      "triagePriority": "Routine" /* or null */, "catName": "Mitu", "catIsHighRisk": true,
      "handlingNote": "Bites when restrained" /* null unless high-risk */, "customerName": "…",
      "customerPhone": "+8801…" /* OQ-10 */, "serviceName": "Vaccination", "performerName": "Dr Rahman",
      "roomName": "Consult 1" } ] },
    "boardingTasks": { "items": [ {
      "taskId": "…", "dueOn": "…", "taskType": "Medication", "taskLabel": "Evening medication",
      "unitName": "Suite 3", "catName": "…", "catIsHighRisk": false, "handlingNote": null,
      "instructions": "½ tablet with food", "status": "Open", "completedOn": null, "assigneeName": "…" } ] }
  },
  "omittedSections": [ { "section": "boardingTasks", "reason": "NotPermitted" | "NotAvailable" } ]
}
```
The **ETag** is the SHA-256 (hex, quoted, strong) of the canonical JSON of `branch`, `localDate`,
`sections` and `omittedSections`. It excludes `generatedOn`, so an unchanged day returns `304`. Items
are sorted by `startsOn` / `dueOn`, then id, so the hash is stable. `NotAvailable` means that no source
is registered for the section, for example before HO-BRD-001 is merged.

### 12.5 Filters

- HO-ACC-001's exception filter set gains a `ProtectedRecordDeletionException` mapping →
  `409 { errors: [{ errorCode: "RecordIsRetained", errorMessage: OperationsMessages.RecordIsRetained }] }`.
  A `PostgresException` with `P0001` whose message starts `Hard delete of` maps to the same response.
  Other `P0001`s rethrow.
- `UniqueConstraintExceptionFilter`: `pk_erasure_request` (a concurrent open) → handled in C1, never
  reaches the filter. No other new constraint.
- Rate-limit policies `ping` (120 / min / IP) and `telemetry` (30 / min / IP), in HO-ACC-001 ADR-030's
  shape, with `429` + `Retry-After`.

### 12.6 Read side — `src/Read/`

Queries in `Queries/Operations/`, handlers in `QueryHandlers/Operations/`, ViewModels flat. Explicit
member-by-member projections, `AsNoTracking()`, and every list paginated.

| # | Query | Notes |
|---|---|---|
| Q1 | `GetFrontDeskOfflineSnapshotQuery(BranchId, CallerRoles, StaffAccountId)` | Resolves the branch and zone and the local date **via HO-ADM-001's calendar helpers**. For each `IFrontDeskSnapshotSource`: caller allowed its `RequiredAction` → build; not allowed → `NotPermitted`; no source for a known section key → `NotAvailable`. Computes the ETag |
| Q2 | `GetWebVitalsSummaryQuery` | One SQL statement with `percentile_cont` (ADR-310), grouped; verdicts in C# from `WebVitalBudgets` |
| Q3 | `GetRetentionScheduleQuery` | From `RecordClassification` + `IBusinessSettings`. No database read beyond settings |
| Q4 | `GetRetentionRunsQuery`, `GetRetentionRunQuery` | |
| Q5 | `GetRetentionReviewQuery` | Calls every `IRetentionReviewSource` |
| Q6 | `GetErasureRequestsQuery`, `GetErasureRequestQuery` | `basis` comes from the scope's latest `ErasureScopeHeldEvent`? **No**: store `HeldBasis` (≤ 100) on the scope in `RecordScopeOutcome` (add it to §8.3's `ErasureScope`) |
| Q7 | `GetBackupStatusQuery` | Latest row per `kind` with `succeeded`; staleness from `RecoveryObjectives` and `TimeProvider`; restore-test due from the last `Passed` record + `operations.restoreTestIntervalDays` (no passed test ever → due **today** and overdue) |
| Q8 | `GetRestoreTestsQuery`, `GetRestoreTestQuery` | |

### 12.7 Host — `Program.cs`

Register every handler, validator (as `IValidator<T>`), query handler and event handler in §11–§12.6.
Count them against those tables, because there is no assembly scanning.
- `IPersonalDataEraser` ×3, `IRetentionReviewSource` ×1, `IFrontDeskSnapshotSource` ×1 (plus the
  boarding one in step D2) as **multiple Scoped registrations of the same interface**, consumed as
  `IEnumerable<T>`. `IRetentionPurger` and `RetentionWorkQueue` are Scoped.
- `HardDeleteGuardInterceptor` is added to the existing `AddInterceptors(...)` call **first** in the
  list (§12.1).
- `RetentionWorkCycle` is Scoped and `AddHostedService<RetentionWorker>()`.
- `OperationsOptions` with `ValidateOnStart`; the `DatabaseRoleGuard` runs at startup after migrations
  would have been applied (never applies migrations itself).
- Rate-limit policies `ping`, `telemetry`.
- `ValidateScopes` / `ValidateOnBuild` stay on. **No new package.**

`appsettings.json` gains the `Operations` section with **no secret**. `appsettings.Testing.json` (or
the test factory) sets `Operations:RetentionWorker:Enabled = false` and
`Operations:Database:EnforceNonOwnerRole = false`.

### 12.8 Deploy — backup and database roles (Phase F)

`deploy/` is new. **No secret in any file.** Every secret is a `${VAR}` read from the environment,
which the secret store injects (NFR-SEC-7).

| Path | Content |
|---|---|
| `deploy/database/roles.sql` | Idempotent (`DO $$ … IF NOT EXISTS`) creation of `catecommerce_migrator` (owner of every solution schema; `ALTER DEFAULT PRIVILEGES` so new tables grant DML to the app), `catecommerce_app` (`USAGE` on schemas; `SELECT, INSERT, UPDATE, DELETE` on tables; `USAGE, SELECT` on sequences; **no** `TRUNCATE`, no `CREATE`), `catecommerce_backup_reporter` (`INSERT` on `ops.backup_run` only). Passwords are set from psql variables, never literals |
| `deploy/backup/Dockerfile` | `FROM postgres:17-bookworm`, `apt-get install pgbackrest` from PGDG (pinned major), non-root `postgres` user, config templates copied in |
| `deploy/backup/postgresql.backup.conf` | `wal_level = replica`, `archive_mode = on`, `archive_command = 'pgbackrest --stanza=purrcare archive-push %p'`, **`archive_timeout = 60`**, `max_wal_senders = 3` |
| `deploy/backup/pgbackrest.conf.template` | Stanza `purrcare`. `repo1` posix, `repo1-retention-full=5`. `repo2` s3 (`repo2-s3-endpoint`, `-bucket`, `-region`, `-key`/`-key-secret` from env; `repo2-cipher-type=aes-256-cbc`, passphrase from env), `repo2-retention-full=5`. `start-fast=y`, `compress-type=zst`, `process-max=2`, `archive-async=y` |
| `deploy/backup/schedule.cron` | Full Sunday 01:00, diff Monday–Saturday 01:00 (server local = Asia/Dhaka, documented); `archive-check` every 10 min; erasure journal daily 01:30; `pgbackrest expire` after each backup |
| `deploy/backup/scripts/backup.sh` | `pgbackrest backup --type=$1 --repo=1` then `--repo=2`; calls `report-run.sh` for each with the outcome |
| `deploy/backup/scripts/archive-check.sh` | `pgbackrest check` plus `pgbackrest info --output=json`, then extracts the latest archived WAL time and reports `archive-check` |
| `deploy/backup/scripts/report-run.sh` | One `INSERT INTO ops.backup_run …` as `catecommerce_backup_reporter`, parameters via `psql -v`. The error summary is truncated to 500 characters and scrubbed of anything matching `key|secret|password|passphrase` |
| `deploy/backup/scripts/export-erasure-journal.sh` | `\copy (SELECT id, requested_on FROM ops.erasure_request WHERE requested_on > now() - interval '40 days') TO STDOUT CSV`, uploaded into both repositories' `erasure-journal/YYYY-MM-DD.csv` (repo1 path; repo2 via pgBackRest's `repo-put`, or the S3 CLI in the same image if the installed version lacks it, recorded in the runbook). Reports `erasure-journal`. **Ids and dates only** (ADR-314). The query runs as the migrator because the reporter has no `SELECT`; the runbook documents the read-only credential used |
| `deploy/backup/scripts/restore-pitr.sh` | `pgbackrest restore --type=time --target="$TARGET" --target-action=promote --delta`, with a mandatory `--confirm` flag and a refusal to run against a data directory whose `postgresql.auto.conf` names the production cluster unless `--i-understand-this-replaces-production` is given |
| `deploy/backup/scripts/restore-drill.sh` | On an **isolated** host or container: restore the latest backup to `SimulatedFailureAt = now − 5 min` from `repo2` (off-site, to prove it), time the restore, run `verify-restore.sql`, and emit JSON exactly matching C12's shape (`environment`, `backupSetLabel`, `simulatedFailureAt`, `recoveredTo` = `max(occurred_on)` from `audit.audit_entry` ≤ target, `restoreStartedAt`, `restoreCompletedAt`, `checks[]`) |
| `deploy/backup/scripts/verify-restore.sql` | Checks as named rows: `migrations-current` (latest `__EFMigrationsHistory` = expected, passed as a variable); `audit-triggers-present`; `retention-triggers-present` (count equals the number of `Forbidden` tables supplied); `row-counts-nonzero` for `account.customer_account`, `clinic.cat`, `clinic.appointment`; `audit-chain-recent` (`recoveredTo` within 15 min of the target); `media-store` (`skipped` until object storage exists, OQ-9) |
| `deploy/backup/test/docker-compose.drill.yml` + `run-drill-test.sh` | §16.2: a disposable Postgres 17 with the backup image, a posix repo and an optional MinIO-compatible S3 endpoint. Seeds rows, backs up, writes more, notes T, writes more, stops, restores to T, and asserts |
| `docs/runbooks/backup-and-restore.md` | Architecture diagram (text); the RPO/RTO argument; daily checks; **the restore procedure step by step, with the erasure reconciliation (ADR-314) as a mandatory step**; the quarterly drill and how to record it in the app; key rotation; the object-storage section (OQ-9) |
| `docs/runbooks/retention-and-erasure.md` | The classification rules (§8.2); what the worker does nightly; how to read the review report; how a staff-initiated erasure is verified and recorded; what "held" means, for the customer-facing explanation |

---

## 13. Allowed scope

```
src/Shared/CatEcommerce.Shared/Common/Enums/{the twelve enumerations in §10.1}.cs
src/Shared/CatEcommerce.Shared/Common/Constants/{RecordClassification,ErasureExemptions,RetentionRules,RecoveryObjectives,WebVitalBudgets,OfflineSnapshotRules,OperationsMessages,RetentionCorrelation}.cs
src/Shared/CatEcommerce.Shared/Common/Constants/{Actions,ActionRoles,BusinessViolationCodes}.cs            ← append only
src/Shared/CatEcommerce.Shared/Common/Constants/SettingDefinitions.cs (and SettingKeys.cs)                ← the nine keys of §10.2 only
src/Shared/CatEcommerce.Shared/Common/Constants/SystemActors.cs                                          ← RetentionWorker only
src/Domain/CatEcommerce.Domain/Aggregates/Operations/**
src/Domain/CatEcommerce.Domain/Aggregates/Administration/PolicyAcceptanceAggregate.cs                    ← RedactIpAddress only (§5.3)
src/Domain/CatEcommerce.Domain/Aggregates/Notification/{the notification aggregate}.cs                    ← Redact(Retention) behaviour only (§5.3)
src/Shared/CatEcommerce.Shared/Common/Enums/{NTF redaction reason}.cs                                    ← Retention member only (§5.3)
src/Domain/CatEcommerce.Domain/Entities/{ErasureScope,RetentionPassResult,RestoreCheckResult,WebVitalSample}.cs
src/Domain/CatEcommerce.Domain/DomainServices/{IPersonalDataEraser,ErasureOutcome,IRetentionPurgeSource,IRetentionPurger,IRetentionReviewSource}.cs
src/Domain/CatEcommerce.Domain/Models/**                                                                 ← new DTOs only
src/Domain/CatEcommerce.Domain/Events/Operations/**
src/Domain/CatEcommerce.Domain/Events/Administration/PolicyAcceptanceIpRedactedEvent.cs
src/Application/CatEcommerce.Application/Commands/Operations/**
src/Application/CatEcommerce.Application/CommandHandlers/Operations/**
src/Application/CatEcommerce.Application/CommandHandlers/Validators/{one per command in §11.1}.cs
src/Application/CatEcommerce.Application/DomainEventHandlers/Operations/**
src/Application/CatEcommerce.Application/DataMappers/OperationsMapper.cs
src/Read/CatEcommerce.Read/Queries/Operations/**
src/Read/CatEcommerce.Read/QueryHandlers/Operations/**                                                   ← incl. IFrontDeskSnapshotSource and the two sources
src/Read/CatEcommerce.Read/ViewModels/{the ViewModels in §12.4}.cs
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/CatEcommerceDbContext.cs                      ← DbSets and BackupRunReadModel mapping only
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Configurations/{ErasureRequest,RetentionRun,RestoreTestRecord,WebVitalBatch,BackupRunReadModel}Configuration.cs
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Migrations/**                                  ← the five in §12.1 only
src/Infrastructure/CatEcommerce.Infrastructure/Retention/**
src/Infrastructure/CatEcommerce.Infrastructure/Operations/**
src/Infrastructure/CatEcommerce.Infrastructure/Configuration/OperationsOptions.cs
src/Infrastructure/CatEcommerce.Infrastructure/Auditing/AuditCaptureExclusions.cs                         ← the two ops roots only (§5.3)
src/Api/CatEcommerce.Api/Controllers/{SystemPing,StaffOfflineSnapshot,Telemetry,AdminWebVitals,AdminRetention,AdminCustomerErasure,AdminOperations}Controller.cs
src/Api/CatEcommerce.Api/Contracts/Operations/**
src/Api/CatEcommerce.Api/Background/{RetentionWorker,RetentionWorkCycle}.cs
src/Api/CatEcommerce.Api/Filters/{the HO-ACC-001 exception filter file}.cs                                ← the RecordIsRetained mapping only
src/Api/CatEcommerce.Api/RateLimiting/RateLimitPolicies.cs                                               ← ping, telemetry only
src/Api/CatEcommerce.Api/appsettings.json                                                                ← Operations section only
src/Api/CatEcommerce.Api/Program.cs                                                                      ← §12.7 only
tests/CatEcommerce.Domain.Tests/**
tests/CatEcommerce.Application.Tests/**
tests/CatEcommerce.IntegrationTests/**
deploy/database/**
deploy/backup/**
docs/runbooks/backup-and-restore.md
docs/runbooks/retention-and-erasure.md
docs/test-cases/TC-HO-XCT-001.md
docs/gates/HO-XCT-001-*.md
.ai/decisions/adr.md                                                                                    ← ADR-297…316, append only
```

The erasers for boarding (C-BRD-3) and every other future context, and the boarding purge source
(C-BRD-2), are **not** this slice's files. They belong to those handovers. The only exception is
`BoardingTaskSnapshotSource` (a read, in `QueryHandlers/Operations/`).

## 14. Forbidden scope

| Path | Why |
|---|---|
| `.ai/**` except `decisions/adr.md`, `.claude/**` | Governing docs and agents (OQ-2 is a separate task) |
| `docs/SRS.md`, `docs/USER_STORIES.md`, `docs/handover/**` (including README and every other HO file) | Inputs. §5.3 amendments are recorded by the README owner (OQ-1) |
| `web/**` | HO-XCT-002 |
| Every file of HO-ACC-001, HO-ADM-001, HO-NTF-001, HO-APT-001 and HO-CAT-001 beyond the lines in §13 | Their areas. In particular **no aggregate of theirs gains a delete, and `CustomerAccountAggregate` is not edited** (only its `Anonymise` is called) |
| Any HO-BRD / EMR / PHM / SRG / BIL / ORD / IMP / SUB aggregate, eraser or purge source | Their handovers own them (§5.2). If one is missing, that is a §20 stop |
| `audit.audit_entry` triggers and columns | HO-ACC-001's (F33) |
| `NuGet.Config`, `global.json`, `*.sln`, `*.csproj` | No package is added |
| `src/Api/…/appsettings.Development.json` | Local-only, git-ignored |

---

## 15. Unit test cases

`tests/CatEcommerce.Domain.Tests` / `Application.Tests`. Infrastructure and convention rows sit in
`IntegrationTests` as plain unit tests where they need the EF model or the file system.

| ID | Case | Expected |
|---|---|---|
| **A — classification and guards** | | |
| U-A1 | `RecordClassificationConventionTests`: every `(schema, table)` in the EF model, owned included, vs `RecordClassification` | **Set equality**; no duplicates. Fails on an unclassified **or** a stale entry |
| U-A2 | Source scan over `src/**/*.cs` | No `ExecuteDelete`, raw `DELETE`/`TRUNCATE`, `DISABLE TRIGGER` or `session_replication_role` outside `RetentionPurger.cs` (and no trigger-disabling even in migrations) |
| U-A3 | Interceptor: `Remove(cat)` | `ProtectedRecordDeletionException` before SQL |
| U-A4 | Interceptor: remove an owned allergy via `UpdateMedicalProfile` | Allowed (`AggregateOnly`, owner tracked Modified) |
| U-A5 | Interceptor: `Remove` an owned `CustomerDeliveryAddress` directly with the owner not tracked | Refused |
| U-A6 | Interceptor: tracked `Remove` of a `customer_session` row | Refused (purgeable roots go through the purger only) |
| U-A7 | Every `Forbidden` entry in `RecordClassification` | Appears in `AddRetentionGuards`' literal list, or is `audit.audit_entry` (F33), or is attached by a later migration (asserted by I-A3) |
| U-A8 | `RetentionPurger` table set | Every method's table is `Purgeable` in `RecordClassification` |
| **B — erasure ledger** | | |
| U-B1 | `Open` with keys `account`, `notification`, `administration` | Three `Pending` scopes; event carries ids only |
| U-B2 | `RecordScopeOutcome` `Held(tomorrow)`; `Held(today)` | `Held`, date set / `InvalidHoldDate` |
| U-B3 | Last scope completed | `Status = Completed`, `ErasureRequestCompletedEvent` once |
| U-B4 | `AddMissingScopes` on a completed request with a new key | Reopened; new scope `Pending` |
| U-B5 | C2 handler: eraser throws | Scope `Failing`, `UnhandledEraserException`, attempts 1; **no exception escapes** |
| U-B6 | `ErasureCoverageConventionTests`: every aggregate with a `*CustomerId` / `*CustomerAccountId` / `OwnerId` property | Its context has a registered eraser **or** an `ErasureExemptions` entry |
| U-B7 | Eraser contract suite run against each registered eraser (fakes for repositories) | Idempotent second call; `Completed` for an unknown customer; never throws for that case; logs no personal value |
| U-B8 | `AdministrationPersonalDataEraser`: one acceptance 7 years old, one 1 year old, financialYears 6 | Old one redacted; `Held(acceptedOn(young) + 6y)` |
| U-B9 | C3: active / anonymised / merged / unknown | Anonymises + audit row / `isReapplication` no write / `CustomerRecordMerged` / `CustomerAccountNotFound` |
| U-B10 | C4 with 501 ids (validator); with 3 ids of which 1 unknown | `ReapplyListTooLarge`; `{ processed: 2, notFound: 1 }` and continues past the unknown |
| U-B11 | Ledger-open handler source derivation: customer caller; staff caller; C4 correlation | `SelfService` / `StaffOnBehalf` / `RestoreReconciliation` |
| **C — worker and purges** | | |
| U-C1 | `RetentionRunAggregate` transitions out of order | Each refused |
| U-C2 | C5 while a run is `Running` | `RetentionRunAlreadyInProgress` |
| U-C3 | Work cycle with a throwing unit | Logged; next unit runs; the run finishes `CompletedWithErrors` |
| U-C4 | Daily trigger: main branch `Asia/Dhaka`, sweepHour 2, `FakeTimeProvider` at 01:59 / 02:00 / 02:30 after a run | No / yes / no |
| U-C5 | Erasure back-off | `Failing` scope retried at 15, 60, 240, then 1440 min |
| U-C6 | `CatClinicalReviewSource`: deceased 8 y ago, last visit 3 y ago, never visited created 9 y ago; clinicalYears 7 | Counts 2; oldest anchor correct |
| **D — snapshot** | | |
| U-D1 | ETag: same content with different `generatedOn` | Same ETag |
| U-D2 | ETag: one status change | Different ETag |
| U-D3 | Sections: Caretaker (no `Appointments.View`); Reception | `appointments` omitted `NotPermitted` / both present (boarding per C-BRD-1) |
| U-D4 | No boarding source registered | `boardingTasks` omitted `NotAvailable` |
| U-D5 | `validUntil` for `Asia/Dhaka` 2026-10-12 | `2026-10-13T02:00:00+06:00` |
| U-D6 | Clinic source status filter | `Held`, `Cancelled` excluded; walk-ins included; `handlingNote` null unless high-risk |
| **E — web vitals** | | |
| U-E1 | Validator: route with `?q=`, with `@`, 201 chars; 11 samples; unknown metric; `NaN` | Each `InvalidWebVitalsPayload` |
| U-E2 | C11 with a signed-in customer's `UserContext` | Stored `CreatedBy == Guid.Empty`; no identity anywhere in the row |
| U-E3 | Verdicts: 49 samples; p75 2400 LCP; p75 2600 | `InsufficientData` / `Pass` / `Fail` |
| **F — restore tests** | | |
| U-F1 | Failure 12:00, recovered 11:52, restore 40 min, all checks pass | RPO 8, RTO 40, `Passed` |
| U-F2 | RPO 16 min | `Failed` |
| U-F3 | RTO 241 min | `Failed` |
| U-F4 | One check failed, objectives met | `Failed` |
| U-F5 | `RecoveredTo > SimulatedFailureAt`; completed before started; no checks | `InvalidRestoreTestTimes` ×2 / `RestoreTestChecksRequired` |
| U-F6 | `Record` twice | `RestoreTestAlreadyRecorded` |
| U-F7 | Due date: last passed 2026-07-01, interval 92; last test failed after it | Due 2026-10-01; the failure does not reset it |
| **X** | | |
| U-X1 | Every new event (reflection) | No string property other than codes and context keys |
| U-X2 | `SettingDefinitions` with the nine new keys | Defaults within bounds; keys unique (HO-ADM-001 U-D6 still passes) |

## 16. Integration test cases

Real HTTP, `Testing` environment, shared database (HO-ACC-001 §16.1 rules 1–8). Real staff and
customers are signed in through the cookie path. Workers are off; tests call `RetentionWorkCycle`
directly with `FakeTimeProvider`.

### 16.1 Additional rules for this slice

9. **Purge and redaction cases seed their own rows** with ids they record, and assert only on those
   ids (exists before, gone after). They never assert "the table has N rows".
10. **Never try to clean up protected rows.** After Phase A that fails, by design.
11. Cases that need a *past* row (an old session, an old notification) insert it through the repository
    with a `FakeTimeProvider` set in the past. They never back-date by raw SQL `UPDATE`.

| ID | Case | Expected |
|---|---|---|
| **A — guards** | | |
| I-A1 | Host starts, scope validation on | Every registration resolves, including three erasers and the worker |
| I-A2 | Migrations on an empty database | `retention.refuse_hard_delete()` exists; both triggers on every `Forbidden` table except `audit.audit_entry`; `ops` tables and indexes |
| I-A3 | **For every `Forbidden` table in `RecordClassification`**: query `pg_trigger` | A `BEFORE DELETE` row trigger and a `BEFORE TRUNCATE` statement trigger calling the function (HO-ACC-001's for the audit table). **AC-20's "any path" proof, table by table** |
| I-A4 | Raw `DELETE FROM clinic.cat WHERE id = <own cat>` through Npgsql; `TRUNCATE clinic.appointment` in a rolled-back transaction | Both raise `P0001` "Hard delete … not permitted" |
| I-A5 | An endpoint path that would remove a cat (test-only controller registered only in `Testing`, calling `db.Remove`) | `409 RecordIsRetained` with the plain-language message; the row still exists |
| I-A6 | `UpdateMedicalProfile` removing an allergy through the shipped endpoint | `200`; the allergy row is gone (AggregateOnly works; F32 not broken) |
| I-A7 | `DatabaseRoleGuard` in `Production` connected as `postgres` (superuser, owner) | Startup fails with the stated message |
| I-A8 | Guard with a role created by `deploy/database/roles.sql` (`catecommerce_app`) in a scratch schema copy | Starts; that role's `ALTER TABLE … DISABLE TRIGGER` is refused (not owner) |
| **B — erasure** | | |
| I-B1 | Customer self-deletes (HO-ACC-001 `POST /api/customers/me/deletion`) | Ledger opened, three `Pending` scopes, source `SelfService` |
| I-B2 | Run the erasure pass | `account`, `notification` `Completed`; `administration` `Held` if the customer accepted a policy, else `Completed` |
| I-B3 | Advance `FakeTimeProvider` past the hold, run the pass | Acceptance IP null; request `Completed`; an `ErasureRequestCompleted` `BusinessEvent` row — **US-XCT-06 AC1** |
| I-B4 | Clinical and financial rows referencing the customer after erasure | Still present and readable; the owner shows the placeholder — **AC1 "retained"** |
| I-B5 | Admin `POST /api/admin/customers/{id}/erasure` with channel and identity check | `200 { isReapplication: false }`; account anonymised; `CustomerErasureRequestedByStaff` audit row with channel; ledger source `StaffOnBehalf` |
| I-B6 | Same again | `200 { isReapplication: true }`; nothing written |
| I-B7 | Receptionist calls it | `403`; an `AuthorizationDenied` row |
| I-B8 | Anonymisation committed but the ledger handler forced to fail (fault injection) | The request fails as HO-ACC-001 describes; the next erasure pass self-heals by opening the ledger |
| I-B9 | Reapply with ids of one anonymised, one active, one unknown | `{ processed: 2, alreadyErased: 1, notFound: 1 }`; the active one is now anonymised with source `RestoreReconciliation` |
| I-B10 | `GET /api/admin/retention/erasure-requests/{id}` | Scopes with status, `heldUntil`, `basis`; **no name, phone or email in the body** |
| **C — worker** | | |
| I-C1 | Seed own ended sessions 40 days ago and 10 days ago, securityArtefactDays 30; run daily passes | The 40-day one is gone, the 10-day one stays; a `BusinessEvent` purge row with the count |
| I-C2 | Seed own notification 25 months old and one 1 month old; run | The old one is `[redacted]` with a null destination; metadata kept; the young one is untouched |
| I-C3 | Seed web-vital batches 100 and 10 days old | Old purged, young kept |
| I-C4 | A registered fake `IRetentionPurgeSource` | Called with the right cutoff; its result is on the pass |
| I-C5 | `POST /api/admin/retention/runs` twice | `201` then `409` while the first is in progress |
| I-C6 | `GET …/runs/{id}` after a run | Every pass with counts; status `Succeeded` |
| I-C7 | `GET /api/admin/retention/review` | Rows including the cat-clinical count; the "nothing is deleted automatically" note |
| I-C8 | `GET /api/admin/retention/schedule` | Every class, effective periods from settings; lowering `retention.clinicalYears` shows immediately; a `DataChange` audit row for the setting |
| I-C9 | After I-C1…C3: every clinical table row the tests created | Still present — the worker deleted nothing protected |
| **D — offline snapshot** | | |
| I-D1 | Receptionist at a test branch with 3 appointments (one cancelled) `GET /api/staff/offline-snapshot` | `200`, 2 items sorted, `ETag`, `Cache-Control: no-store, private`, `validUntil`, `staffAccountId` = caller; an `OfflineSnapshotIssued` audit row |
| I-D2 | Same request with `If-None-Match` | `304`, no body, **no new audit row** |
| I-D3 | Check one appointment in, request with the old ETag | `200`, new ETag |
| I-D4 | Poll the snapshot and the ping every "minute" for 40 fake minutes with idle timeout 30 | `401 session_idle_timeout` at minute 31 — **neither endpoint extends the session** (F38) |
| I-D5 | Customer session; anonymous; Inventory staff | `401`, `401`, `403` |
| I-D6 | Caretaker (step D2, after HO-BRD-001) | `appointments` omitted `NotPermitted`; `boardingTasks` present with instructions |
| I-D7 | Before HO-BRD-001 is merged | `boardingTasks` omitted `NotAvailable` — expected, not a failure |
| I-D8 | `GET /api/system/ping` anonymous and staff | `204`, `no-store`, no body; staff `lastActivityOn` unchanged |
| I-D9 | Body scan of I-D1 | No field beyond §12.4's list (explicit projection) |
| **E — web vitals** | | |
| I-E1 | `POST /api/telemetry/web-vitals` JSON, 3 samples | `204`; one batch, 3 samples; no IP/user columns exist in the table |
| I-E2 | Same as `text/plain` (beacon) with a customer cookie **and no CSRF header** | `204` — the endpoint reads no identity (the browser omits credentials, but the server must not depend on it) |
| I-E3 | 5 KB body; 11 samples; route with a query string | `413`; `400`; `400` |
| I-E4 | 31 posts in a minute | 31st `429` + `Retry-After` |
| I-E5 | Seed 60 LCP samples with known values; summary | p75 equals PostgreSQL's `percentile_cont` computed independently in the test; verdict per budget — **US-XCT-02 AC1 measuring instrument** |
| I-E6 | Log capture during I-E1…E4 | No IP or user id logged beyond the framework's request log at Debug |
| I-E7 | Change-capture exclusion | No `DataChange` audit row for web-vital batches |
| **F — restore tests and status** | | |
| I-F1 | `POST …/restore-tests` with the drill JSON of a passing drill | `201`, computed RPO/RTO, `Passed`; performer = caller; a `DataChange` row |
| I-F2 | `GET …/backup-status` with no restore test ever | `isRestoreTestOverdue: true`, due today |
| I-F3 | Insert `ops.backup_run` rows as `catecommerce_backup_reporter` (role from `roles.sql` in a scratch database) | Insert works; `SELECT`/`UPDATE`/`DELETE` refused for that role; status shows the latest per kind |
| I-F4 | Latest successful diff 27 h old | `isBackupStale: true` |
| I-F5 | Accountant calls backup status | `403` |

### 16.2 Backup drill test — Docker, category `BackupDrill`

It runs separately (`dotnet test --filter Category=BackupDrill`) or through
`deploy/backup/test/run-drill-test.sh`. It is **mandatory before the ACCEPTANCE gate** and not part of
the default suite, because it needs image pulls.

| ID | Case | Expected |
|---|---|---|
| I-G1 | Build `deploy/backup/Dockerfile`; start the drill compose; `pgbackrest stanza-create` and `check` | Both succeed |
| I-G2 | Seed 1 000 rows in a test table; full backup; 1 000 more with timestamps; record T; 1 000 more; stop Postgres; `restore-pitr.sh --target=T` | Exactly the rows up to T exist — **PITR works** |
| I-G3 | With `archive_timeout = 60` and a write every 5 s, kill Postgres uncleanly; restore to latest | Data loss ≤ 60 s + one archive cycle, **well inside the 15-min RPO** — measured, not assumed |
| I-G4 | `restore-drill.sh` against the drill cluster | Emits JSON that C12 accepts as-is (posted to a test API) and that the aggregate computes `Passed` |
| I-G5 | Restore from `repo2` only, with repo1 deleted | Succeeds — the off-site copy is sufficient |
| I-G6 | `repo2` without the cipher passphrase | Restore fails — the repository is encrypted |
| I-G7 | `grep -riE 'password\|secret\|passphrase\|key' deploy/` for literal values | Only `${…}` placeholders |
| I-G8 | `export-erasure-journal.sh` | CSV of ids and dates only; present in both repos |

---

## 17. Implementation order and verification

| Phase | Stories | Delivers |
|---|---|---|
| **A** | US-XCT-06 AC2 | Confirm F32/F35 against merged code; enums and constants; `RecordClassification` for **every** table in the model; interceptor; `AddRetentionGuards` with triggers; source scan; exception mapping; `DatabaseRoleGuard`; `deploy/database/roles.sql`. U-A*, I-A* green |
| **B** | US-XCT-06 AC1 | Erasure ports, ledger, the three erasers, the ledger-open handler, C3, C4, the §5.3 ADM/NTF amendments; `AddErasureLedger`. U-B*, I-B* |
| **C** | US-XCT-06 | Retention run, purger, worker and cycle, redaction pass, review source, schedule and review reads; `AddRetentionRuns`. U-C*, I-C* |
| **D** | US-XCT-01 | D1: ping, snapshot endpoint with the clinic section, audit command. **D2 (blocked until HO-BRD-001 is merged)**: `BoardingTaskSnapshotSource`. U-D*, I-D* |
| **E** | US-XCT-02 | Web-vitals ingestion, summary, exclusion, purge wiring; `AddWebVitals`. U-E*, I-E* |
| **F** | US-XCT-05 | Restore-test aggregate and endpoints, backup-run read model and status; `AddRestoreTestsAndBackupRuns`; `deploy/backup/**`; runbooks; drill test (§16.2). U-F*, I-F*, I-G* |

```bash
dotnet restore CatEcommerce.sln
dotnet build CatEcommerce.sln                        # 0 warnings, 0 errors
dotnet test tests/CatEcommerce.Domain.Tests
dotnet test tests/CatEcommerce.Application.Tests
dotnet dotnet-ef migrations add <PhaseMigration> --project src/Infrastructure/CatEcommerce.Infrastructure --startup-project src/Api/CatEcommerce.Api --output-dir Persistence/Migrations
#   read the migration — the trigger list in AddRetentionGuards must match RecordClassification's Forbidden set
dotnet dotnet-ef database update --project src/Infrastructure/CatEcommerce.Infrastructure --startup-project src/Api/CatEcommerce.Api
dotnet run --project src/Api/CatEcommerce.Api        # starts clean in Development (role guard off)
dotnet test tests/CatEcommerce.IntegrationTests
# Phase F
docker build -t purrcare-backup:dev deploy/backup
bash deploy/backup/test/run-drill-test.sh           # I-G1…I-G8
dotnet test tests/CatEcommerce.IntegrationTests --filter Category=BackupDrill
```

---

## 18. Acceptance criteria

A row with no evidence is not met.

| # | Story | Criterion | Evidence |
|---|---|---|---|
| A1 | XCT-06 AC2 | **No clinical record can be hard-deleted by any application path**: database triggers on every protected table, the EF guard, and the build-time scan | U-A2, U-A3, U-A7, I-A3, I-A4, I-A5 |
| A2 | XCT-06 AC2 | The protection holds for every table, present and future: every table is classified | U-A1, U-B6 |
| A3 | XCT-06 AC2 | The application's database role cannot disable the protection | I-A7, I-A8 |
| A4 | XCT-06 AC2 | Legitimate aggregate replacement of current-state data still works | U-A4, I-A6 |
| A5 | XCT-06 AC1 | A deletion request (self-service or staff-taken) anonymises personal data across contexts, and the completion is recorded | U-B1, U-B3, U-B9, I-B1, I-B2, I-B3, I-B5 |
| A6 | XCT-06 AC1 | Clinical and financial records are retained, and personal data inside them is held until its period ends, then erased | U-B8, I-B3, I-B4 |
| A7 | XCT-06 | Only purgeable data is ever deleted by the retention worker, and each purge is audited | U-A8, I-C1, I-C3, I-C4, I-C9 |
| A8 | XCT-06 / D-10 | *(server half)* Boarding media purge runs on schedule through its source. **The source itself: HO-BRD-001 (C-BRD-2)** | I-C4 |
| A9 | XCT-06 | Records past their statutory period are reported for review, never deleted | U-C6, I-C7 |
| A10 | XCT-01 AC1 | *(server half)* Today's appointment list and boarding task list are served as one cacheable snapshot for the active branch, filtered by permission | U-D3, U-D6, I-D1, I-D6 |
| A11 | XCT-01 AC3 | *(server half)* Refresh is cheap: an unchanged day returns `304` | U-D1, U-D2, I-D2, I-D3 |
| A12 | XCT-01 | The snapshot and the ping never keep an idle staff session alive | I-D4, I-D8 |
| A13 | XCT-01 / NFR-SEC-8 | Every issue of snapshot content is audited, and the payload is minimal | I-D1, I-D2, I-D9 |
| A14 | XCT-02 AC1 | *(server half)* p75 LCP per storefront route is measured from anonymous real-user samples and judged against 2.5 s | U-E3, I-E1, I-E5 |
| A15 | XCT-02 | Telemetry identifies no one | U-E2, I-E2, I-E6, I-E7 |
| A16 | XCT-05 AC1 | Daily backups with PITR exist, off-site and encrypted; measured data loss is within 15 min | I-G1, I-G2, I-G3, I-G5, I-G6 |
| A17 | XCT-05 AC1 | Restore time is measured by the drill and judged against 4 h | U-F1, U-F3, I-G4 |
| A18 | XCT-05 AC2 | A quarterly restore test is recorded, attributed and judged by the system, and an overdue test is visible | U-F1…U-F7, I-F1, I-F2 |
| A19 | XCT-05 | Backup health is visible; the backup job cannot touch anything but its own log | I-F3, I-F4 |
| A20 | XCT-05 / ADR-314 | A restore cannot resurrect erased customers: journal and reapply | U-B10, I-B9, I-G8 |
| A21 | — | Host starts with every registration; no secret is committed | I-A1, I-G7 |

**Deferred, named in §4.2 / §5.2**: the boarding tasks section's data (HO-BRD-001, C-BRD-1 — A10 is
met for appointments before it lands and for both after); the boarding media purge source (C-BRD-2);
erasers and review sources for contexts not yet written (C-ALL-2, C-ALL-3); object-storage backup
(OQ-9). **Browser halves** of A10–A14: HO-XCT-002.

---

## 19. Required gates

In order. Each writes `docs/gates/HO-XCT-001-<GATE>.md` **before** reporting.

| # | Gate | Agent | Focus |
|---|---|---|---|
| 1 | ARCHITECTURE | `architecture-reviewer` | `Operations` context (ADR-297, OQ-2); the classification registry and its convention test (ADR-298); guard layering (ADR-299); the two rule-1 exceptions (ADR-302, ADR-314) and the `ops.backup_run` exception (ADR-312); the third background service (ADR-303); snapshot as a composite read (ADR-306) and the query-never-writes split (ADR-307); scoped edits to ACC/ADM/NTF files match §13 exactly; ADR-297…316 recorded |
| 2 | SECURITY | `security-reviewer` | Triggers on every protected table (I-A3); non-owner role and startup guard (ADR-300); purger limited to named tables; erasure completeness and no personal data in the ledger; snapshot minimality, `no-store`, passive session, audit; telemetry anonymity and bounds; backup encryption, no secrets in `deploy/`, restore script safety flags; erasure journal content; **named residuals: audit-entry personal data (OQ-5), phone in snapshot (OQ-10), 35-day backup window (OQ-12)** |
| 3 | CODE_REVIEW | `code-reviewer` | `.ai/` conventions; worker never throws; time only through HO-ADM-001's helpers and `TimeProvider`; explicit projections; idempotent erasers; shell scripts `set -euo pipefail` and quoted |
| 4 | TEST | `test-engineer` | Writes `TC-HO-XCT-001.md` from §15/§16 **first**; I-A3 runs over every table, not a sample; §16.2 drill executed and recorded with its measured RPO/RTO |
| 5 | ACCEPTANCE | `acceptance-reviewer` | Every §18 row; A8/A10 split recorded as deferrals; OQ-3 defaults flagged as provisional pending SRS Q-1; §5.3 amendment rows confirmed present in the target handovers |

Each gate needs the handover ID, the commits and the changed-file list. ACCEPTANCE also needs the four
prior results.

---

## 20. BLOCKED conditions — stop and report, do not work around

Working around any of these instead of reporting it is an automatic acceptance **FAIL**.

**20.1 A prerequisite handover is not merged** (HO-ACC-001, HO-ADM-001, HO-NTF-001, HO-APT-001). This
slice extends their filters, interceptors, settings, aggregates and tables. Do not stub them.

**20.2 The §5.3 Amended rows are not present** in the target handovers when the step that edits their
files begins. Stop at that step. Do not edit the other area's file on the strength of this document
alone (OQ-1).

**20.3 HO-BRD-001 is not merged, or does not meet C-BRD-1.** Ship Phase D1 (appointments), with
`boardingTasks` reported `NotAvailable`. **Do not** invent boarding tables, read from a guessed schema,
or mock a boarding section into production code. If BRD is merged but lacks a C-BRD-1 field, report it
and do not substitute a different field.

**20.4 A table cannot be classified** under §8.2's rules, fits two classes, or looks as if it needs to
become `Purgeable` and is not one of the listed kinds. Stop and ask. **Never classify something
`AggregateOnly` or `Purgeable` to make U-A1 or a failing test pass.**

**20.5 A shipped command breaks because of a trigger** (as F32 would have). That means the table is
mis-classified or the command deletes a record. Report which. Never drop the trigger and never
reclassify silently.

**20.6 A context stores customer personal data and has no eraser** (U-B6 fails for it). Report it
against the owning handover. Do not write that context's eraser here, and do not add an exemption
without a stated reason the owner accepts.

**20.7 Do not delete or anonymise any clinical, financial, regulatory or audit record at the end of
its period**, even if a setting says the period has passed. ADR-301.

**20.8 Do not add identity to telemetry.** If a performance investigation "needs" a user or session
id, it is a new privacy decision for the owner.

**20.9 The backup images or packages do not pull or install** (`postgres:17-bookworm`, PGDG
`pgbackrest`). Stop. Do not substitute `pg_dump` cron jobs (they cannot meet RPO 15 min), a
proprietary cloud backup (NFR-CMP-3), or an unvetted image.

**20.10 A secret is needed in a committed file.** It never is: use a `${VAR}` and the secret store
(NFR-SEC-7).

**20.11 Production cannot run as a non-owner role** (for example, a hosting constraint). Stop and
report. Never set `EnforceNonOwnerRole = false` outside Development/Testing (the host refuses to start
anyway).

**20.12 A requirement conflict is a stop, not an edit.** SRS, backlog and every handover are forbidden
scope.

**20.13 Do not downgrade `Platform.Infrastructure.Core` below 1.0.34.**
