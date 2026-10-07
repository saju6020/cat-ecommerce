# Software Requirements Specification (SRS)
## Cat Clinic, Care Centre & Pet Supplies Platform

| Field | Value |
|---|---|
| Document | Software Requirements Specification |
| Product (working title) | **PurrCare Platform** |
| Version | 2.4 (Draft for stakeholder review) |
| Date | 2026-09-23 |
| Status | Draft — pending sign-off |
| Source brief | `instructions/srs_instruction.md` |
| Change control | See [§0 Document Revision History](#0-document-revision-history) |

---

## 0. Document Revision History

Every change to this document is recorded here before the change is made. The version number increments as:
**major** (x.0) when the product itself changes — a business line added or removed, or a fundamental change of purpose · **minor** (1.x) for requirements added, removed, deferred to a later phase, or materially reworded · **patch** (1.0.x) for typos, formatting and clarifications that do not change meaning.

*Note: deferring a capability to a later phase is a minor change, because the product's scope is unchanged — only its delivery sequence moves. Removing a capability permanently, or changing which business lines exist, is major.*

| Version | Date | Author | Change summary | Sections / IDs affected |
|---|---|---|---|---|
| 1.0 | 2026-09-19 | Claude Opus 5 | Initial draft created from the business brief in `instructions/srs_instruction.md`, covering all four business lines: clinic & surgery, retail shop, imported-food sourcing, and boarding care centre. 181 functional requirements, 46 non-functional, 15 business rules, 22 acceptance scenarios, 15 open questions. | All (new document) |
| 1.0.1 | 2026-09-19 | Claude Opus 5 | Added §0 Document Revision History and the change-control procedure below; added it to the header table and table of contents. No requirement text changed. | §0 (new), header table, ToC |
| 1.0.2 | 2026-09-19 | Claude Opus 5 | Derived the product backlog `docs/USER_STORIES.md` v1.0 from this SRS: 180 user stories, each tracing to the requirement IDs it satisfies. No requirement text changed; cross-reference to the backlog added in §0.2. A two-way coverage audit initially found four requirements with no story (FR-ACC-11, FR-IMP-16, FR-SHP-15, and an apparent FR-BRD-23 that proved to be example text in §0.1, not a real requirement); stories US-ACC-13, US-IMP-16 and US-SHP-14 were added to close the three real gaps. Every FR ID in this document is now traced by at least one story. | §0.2 (new cross-reference); no requirement IDs added, changed or removed |
| 2.0 | 2026-09-20 | Claude Opus 5 | **Microchip identification removed from the product entirely at the business owner's instruction.** Microchipping is not in general use in Bangladesh, so the capability is removed permanently rather than deferred — which makes this a major version under the rule in §0 above. Reworded FR-CAT-2 to drop the microchip number, retaining neuter status with date, current weight and weight history. Struck HW-3 (microchip reader input) in place, preserving the ID. Removed microchip from the UI-2 global search fields, from the §2.4 peripherals list, and from the Cat entity in §6. Corresponding story US-CAT-02 updated in `docs/USER_STORIES.md`, and `docs/handover/HO-CAT-001.md` amended before implementation started. Requested and approved by the business owner. | §2.4, §3.1 UI-2, §3.2 HW-3, §4.2 FR-CAT-2, §6 |
| 1.1 | 2026-09-19 | Claude Opus 5 | **Payment gateway integration deferred out of current scope at the business owner's instruction.** Online card and mobile-wallet payment, saved payment tokens, and automated gateway refunds are removed from the launch build and moved to a later phase. Launch payment methods are now cash on delivery, in-store cash, in-store card via a standalone terminal recorded manually, and bank transfer confirmed by staff. Reworded FR-ORD-8, FR-ORD-9, FR-ORD-14, FR-ORD-17, FR-BRD-7, FR-SUB-2, FR-BIL-7; added FR-ORD-21, FR-ORD-22, FR-BIL-12; amended A-2, BR-15, §1.2 scope, §3.3 interfaces, §10 release plan, AC-8, AC-17. Parks Q-2 (gateway choice) and raises the priority of Q-3 (COD policy), which now governs the primary payment method rather than a secondary one. Corresponding stories updated in `docs/USER_STORIES.md` v1.1. Requested and approved by the business owner. | §1.2, §2.6 A-2, §3.3, §4.6, §4.9, §4.10, §4.11, §7 BR-15, §9, §10, §11 |
| 2.1 | 2026-09-22 | Claude Opus 5 | **Phone and email verification removed from the product at the business owner's instruction; registration protected by a CAPTCHA instead.** An account is usable as soon as it is created — no OTP or signed-link verification gates registration, booking or checkout. Withdrew FR-ACC-2 (struck through, ID retained). Added FR-ACC-13: a self-hosted, single-use CAPTCHA on registration, with an in-person alternative for users who cannot solve it. Reworded FR-ACC-4 (reset token now goes to the *registered* mobile number or email, since no channel is verified). Extended NFR-SEC-6 to the registration and CAPTCHA-issue endpoints. Amended AC-1 (no OTP verification in the pass condition). Added Q-16 (what "incomplete" means for a walk-in record now that there is no verification, FR-ACC-9) and Q-17 (acceptability of the non-visual CAPTCHA alternative). OTP **login** (FR-ACC-3) and OTP as a transactional notification (FR-NTF-4) are unchanged — they are sign-in, not verification. This is a minor version: a requirement is withdrawn, no business line changes. Corresponding stories updated in `docs/USER_STORIES.md` v2.2 (US-ACC-01 reworded, US-ACC-02 and US-ACC-03 withdrawn, US-ACC-05 and US-ACC-10 amended), and `docs/handover/HO-US-ACC-01-BE.md` / `-FE.md` amended before implementation started. Requested by the business owner. | §4.1 FR-ACC-2, FR-ACC-4, FR-ACC-13 (new); §6.3 NFR-SEC-6; §9 AC-1; §11 Q-16, Q-17 (new) |
| 2.2 | 2026-09-22 | Claude Opus 5 | **Accounts and Identity simplified to identifier + password, at the business owner's instruction: no phone, email or OTP verification of any kind.** Customers register with a mobile number and/or an email plus a password (behind the CAPTCHA of FR-ACC-13) and log in with either identifier and their password. Reworded FR-ACC-1 (either identifier may be used; each is unique across customer accounts, because both are now login identifiers). Reworded FR-ACC-3 (OTP login removed; login failures do not reveal whether the identifier exists). FR-ACC-4 password reset **retained** at the owner's choice — a single-use reset token is account recovery, not verification. **Withdrew FR-ACC-8** (two-factor authentication for clinical and financial staff) at the owner's instruction — staff also sign in with identifier and password. Reworded FR-ACC-9 (a walk-in record is incomplete until the customer sets a password) and narrowed Q-16 accordingly. Removed OTP from NFR-SEC-6, FR-NTF-4 and the §3.3 SMS gateway row. Minor version: requirements withdrawn and reworded, no business line changes. Corresponding stories updated in `docs/USER_STORIES.md` v2.3. Requested by the business owner. | §3.3; §4.1 FR-ACC-1, FR-ACC-3, FR-ACC-4, FR-ACC-8, FR-ACC-9; §4.12 FR-NTF-4; §6.3 NFR-SEC-6; §11 Q-16 |
| 2.3 | 2026-09-22 | Claude Opus 5 | **Staff sign-in made explicit, at the business owner's instruction.** No requirement said how staff authenticate: FR-ACC-3 covered customers only, and the staff sign-in step had lived inside the now-withdrawn FR-ACC-8. Reworded FR-ACC-3 to cover staff as well as customers (mobile number or email plus password, no OTP; staff and customer sessions are separate). Reworded FR-ACC-4 so password reset also serves staff, and so a new staff account's first password is set through it. Corresponding story US-ACC-14 added in `docs/USER_STORIES.md` v2.4; the Accounts handovers are consolidated into `docs/handover/HO-ACC-001.md` (backend) and `HO-ACC-002.md` (frontend). Requested by the business owner. | §4.1 FR-ACC-3, FR-ACC-4 |
| 2.4 | 2026-09-23 | Claude Opus 5 | **Constraint C-6 aligned with how money is actually implemented, and the platform choices for file storage and message transports recorded.** C-6 said money is *stored* as integer minor units. The implementation computes in integer minor units — which is what C-6 exists to guarantee — but stores `numeric(12,2)`, matching the column type already specified across the handovers and keeping amounts readable in SQL for reconciliation. Two handovers reached that conclusion independently before the disagreement was noticed. Reworded C-6 to require integer-minor-unit **arithmetic** and an exact decimal **column**, and to keep the prohibition on floating-point money, which is unchanged. Recorded as ADR-317. No functional requirement changes and no business line is affected, so this is a minor version. Also records the owner's choice of **Google Cloud Storage** for uploaded files (ADR-318) and **ClamAV** for scanning them (ADR-319), which close NFR-SEC-5's implementation gap, and **SMTP/MailKit plus a config-driven SMS gateway** for notification transports (ADR-320); an SMS aggregator account is still an outstanding commercial decision. | §7 C-6; §6.3 NFR-SEC-5 (implementation note); §3.3 |

### 0.1 Change control procedure
1. Each edit adds one row above, newest last, before the edit is applied.
2. The **Version** and **Date** fields in the header table are updated to match the newest row.
3. Rows name the specific requirement IDs added, changed or removed (e.g. "added FR-BRD-23; reworded FR-SHP-11") so changes are traceable to design and test artifacts.
4. Requirement IDs are **never reused**. A withdrawn requirement is struck through and marked *Withdrawn in vX.Y* rather than deleted, so historical references remain resolvable.
5. Changes that resolve an open question from §11 cite the question ID (e.g. "resolves Q-6").
6. A change that alters agreed scope requires re-approval from the business owner; the row records who approved it.

### 0.2 Related documents
| Document | Contents | Kept in step with this SRS by |
|---|---|---|
| [`docs/USER_STORIES.md`](USER_STORIES.md) | 180 implementation-ready user stories derived from these requirements, each carrying a `Traces:` line back to the FR/NFR IDs it satisfies | Any change to a requirement here must be reflected in the stories that trace to it; the revision row names the affected IDs so the impacted stories can be found by searching for them |

---

## Table of Contents
0. [Document Revision History](#0-document-revision-history)
1. [Introduction](#1-introduction)
2. [Overall Description](#2-overall-description)
3. [External Interface Requirements](#3-external-interface-requirements)
4. [Functional Requirements](#4-functional-requirements)
5. [Data Requirements](#5-data-requirements)
6. [Non-Functional Requirements](#6-non-functional-requirements)
7. [Business Rules](#7-business-rules)
8. [Reporting and Analytics](#8-reporting-and-analytics)
9. [Acceptance Criteria](#9-acceptance-criteria)
10. [Release Plan](#10-release-plan)
11. [Open Questions and Decisions Required](#11-open-questions-and-decisions-required)
12. [Appendices](#12-appendices)

---

## 1. Introduction

### 1.1 Purpose
This document specifies the functional and non-functional requirements for the **PurrCare Platform**, a single integrated system supporting a cat-only business operating four connected lines of business:

1. A **veterinary clinic** offering consultation, surgery (spaying/neutering), diagnostics, vaccination and general medical assistance for cats.
2. An **e-commerce shop** selling cat food, litter, litter pots, carriers/bags and accessories.
3. An **imported food catalogue**, where food is sourced from several countries and must be tracked by origin, batch, import documentation and expiry.
4. A **cat care centre (boarding)** for short-term and long-term stays.

The document is the reference for design, implementation, testing and acceptance. Every requirement carries a unique ID so it can be traced to design, code and test cases.

### 1.2 Scope
The platform is a web application with a customer-facing storefront/portal and a staff-facing back office, plus a customer mobile app in a later phase.

Its defining characteristic is that **one cat profile is shared across all four business lines**. A cat treated in the clinic is the same record used to unlock prescription diets in the shop and to validate vaccination status at boarding check-in. Without that shared core these would be three disconnected products; with it, each line makes the others safer and more valuable.

**In scope**
- Customer accounts and cat (patient) profiles
- Appointment booking, clinic workflow, electronic medical records (EMR), surgery scheduling
- Prescriptions, vaccination and deworming schedules, reminders
- Product catalogue, cart, checkout, payments, delivery, returns
- Batch/expiry and country-of-origin tracking for imported goods
- Boarding reservations, unit occupancy, daily care logs
- Inventory, procurement, staff management, billing and reporting
- Notifications (email, SMS, push, in-app)

**Out of scope (v1)**
- Full accounting/general ledger (the system exports to accounting software)
- Payroll and HR administration
- In-house laboratory instrument control (results are attached manually or imported as files)
- Mobile/ambulatory vet visits and at-home grooming
- Treatment of species other than cats
- Third-party marketplace channel sync (Amazon, Daraz, eBay)
- **Online payment gateway integration** — card and mobile-wallet payment taken through a gateway, saved payment tokens, and automated online refunds. Deferred to a later phase by decision of the business owner (v1.1). Launch payment methods are listed at FR-ORD-8. This deferral is a sequencing decision, not a removal: §4.9 and §4.11 are written so gateway support can be added without reworking orders, invoices or refunds.

### 1.3 Definitions, Acronyms and Abbreviations

| Term | Meaning |
|---|---|
| **Patient / Cat** | An individual cat registered in the system, owned by a Customer |
| **Owner / Customer** | The person who owns one or more cats and holds the account |
| **EMR** | Electronic Medical Record — the clinical history of a cat |
| **SOAP** | Subjective, Objective, Assessment, Plan — consultation note format |
| **Spay / Neuter** | Ovariohysterectomy (female) / castration (male) sterilisation surgery |
| **FVRCP** | Core feline vaccine (rhinotracheitis, calicivirus, panleukopenia) |
| **FeLV / FIV** | Feline Leukaemia Virus / Feline Immunodeficiency Virus |
| **FLUTD** | Feline Lower Urinary Tract Disease — a common emergency presentation |
| **Prescription diet** | Therapeutic food sellable only on veterinary authorisation |
| **SKU** | Stock Keeping Unit — a sellable product variant |
| **Batch / Lot** | Stock sharing a manufacture date, expiry date and import consignment |
| **FEFO** | First-Expired-First-Out — allocation strategy for perishable goods |
| **COO** | Country of Origin |
| **Boarding** | Paid stay of a cat at the care centre |
| **Unit / Suite** | A physical boarding space (standard, deluxe, isolation) |
| **MFS** | Mobile Financial Service (mobile wallet payment) |
| **COD** | Cash on Delivery |
| **RBAC** | Role-Based Access Control |
| **MoSCoW** | Prioritisation scheme: Must / Should / Could / Won't (this release) |

### 1.4 References
- `instructions/srs_instruction.md` — originating business brief
- IEEE Std 830-1998 — recommended practice for SRS (structural basis)
- WSAVA / AAFP feline vaccination guidelines — clinical basis for default vaccine schedules
- Applicable national regulations for veterinary practice licensing, controlled-drug registers, animal feed import and labelling, consumer e-commerce and data protection *(specific statutes to be confirmed — see §11)*

### 1.5 Document Conventions
- Functional requirement IDs follow `FR-<MODULE>-<n>`; non-functional follow `NFR-<AREA>-<n>`.
- Priority uses MoSCoW: **M** = Must (launch blocker), **S** = Should, **C** = Could, **W** = Won't (deferred).
- "The system shall" denotes a binding requirement; "may" denotes a user option.

### 1.6 Intended Audience and Reading Order

| Reader | Suggested sections |
|---|---|
| Business owner / sponsor | §1, §2, §4 feature summaries, §9, §10, §11 |
| Product manager | Whole document |
| Developers / architects | §3, §4, §5, §6, §7 |
| QA engineers | §4, §7, §9 |
| Clinic & boarding staff | §4.3, §4.6, §7 |

---

## 2. Overall Description

### 2.1 Product Perspective
PurrCare is a new, self-contained product. It replaces paper registers, spreadsheets and messaging-app booking. Architecturally it is one system with four functional domains over a shared core (identity, cat profiles, inventory, payments, notifications).

```
                      +---------------------------------------+
   Customer Web/App   |           PurrCare Platform           |   Staff Back Office
  ------------------> |                                       | <------------------
                      |  +---------+  +---------+  +--------+ |
                      |  | Clinic  |  |  Shop   |  |Boarding| |
                      |  |  EMR    |  | Catalog |  | Units  | |
                      |  +----+----+  +----+----+  +---+----+ |
                      |       |            |           |      |
                      |  +----v------------v-----------v----+ |
                      |  |  SHARED CORE                     | |
                      |  |  Customers | Cats | Inventory    | |
                      |  |  Payments | Notifications | RBAC | |
                      |  +----------------------------------+ |
                      +-------------------+-------------------+
                                          |
             +----------------+-----------+-----------+----------------+
             |                |                       |                |
        Payment GW       SMS / Email             Courier API      Accounting
                                                                    export
```

### 2.2 Product Functions (summary)

| # | Function | Module |
|---|---|---|
| 1 | Register customers and their cats; maintain lifetime cat profiles | Core |
| 2 | Book, reschedule and cancel clinic appointments online | Clinic |
| 3 | Record consultations, diagnoses, treatments and attachments (EMR) | Clinic |
| 4 | Schedule and track surgeries including spay/neuter, with pre-op and post-op workflow | Clinic |
| 5 | Maintain vaccination/deworming schedules and send due reminders | Clinic |
| 6 | Issue prescriptions and dispense medicines from stock | Clinic |
| 7 | Browse, search and filter the catalogue, including by country of origin | Shop |
| 8 | Purchase online with multiple payment methods, delivery or pickup | Shop |
| 9 | Manage subscriptions / auto-repeat orders for food and litter | Shop |
| 10 | Track imported stock by batch, expiry, consignment and import documents | Inventory |
| 11 | Gate prescription diets on a valid veterinary authorisation | Shop + Clinic |
| 12 | Check boarding availability, reserve a unit, pay a deposit | Boarding |
| 13 | Run check-in/check-out with belongings and vaccination checklist | Boarding |
| 14 | Record daily care logs (feeding, litter, medication, mood, photos) | Boarding |
| 15 | Administer medication during boarding under clinic supervision | Boarding + Clinic |
| 16 | Manage staff, rosters, roles and permissions | Admin |
| 17 | Produce invoices, receipts, refunds and an accounting export | Billing |
| 18 | Produce operational and financial reports and dashboards | Reporting |

### 2.3 User Classes and Characteristics

| Class | Description | Technical skill | Key needs |
|---|---|---|---|
| **Guest** | Unauthenticated visitor | Low | Browse products/services and prices, register |
| **Customer (Cat Owner)** | Registered owner of one or more cats | Low–medium | Book, buy, track orders, see cat history, get reminders |
| **Receptionist / Front desk** | Walk-ins, phone bookings, billing, check-in | Medium | Fast search, quick booking, POS, queue view |
| **Veterinarian** | Licensed vet; consultations, surgery, prescribing | Medium | EMR entry, history at a glance, prescribing, surgery notes |
| **Veterinary Assistant / Nurse** | Vitals, prep, post-op, medication rounds | Medium | Task lists, vitals entry, medication administration log |
| **Boarding Caretaker** | Day-to-day care of boarded cats | Low–medium | Daily task list, care logs, photo upload, incident reporting |
| **Groomer** | Optional grooming services | Low | Service schedule, notes |
| **Inventory / Procurement Officer** | Purchasing, imports, stock, expiry | Medium | POs, goods receipt, batch entry, expiry alerts |
| **Shop Manager** | Catalogue, pricing, promotions, fulfilment | Medium | Product CRUD, order queue, discount rules |
| **Accountant** | Invoicing, reconciliation, refunds | Medium | Ledgers, exports, payment reconciliation |
| **Administrator / Owner** | Full access, configuration, reporting | Medium–high | Dashboards, user management, configuration |
| **Courier (external)** | Delivery partner | n/a | Receives dispatch data via API or manifest |

### 2.4 Operating Environment
- **Customer web:** responsive browser application; last two major versions of Chrome, Edge, Safari and Firefox; mobile-first layout (most traffic expected on Android phones).
- **Staff back office:** browser application optimised for 1366×768 and above; used on desktop and tablet.
- **Mobile app:** Android and iOS — Phase 2.
- **Server:** cloud-hosted Linux, containerised; relational primary database; object storage for images and documents; Redis-class cache and job queue.
- **Connectivity:** clinic and boarding sites may see intermittent internet; critical front-desk screens must degrade gracefully (NFR-AVL-3).
- **Peripherals:** thermal receipt printer, A4 printer (prescriptions, discharge notes), barcode scanner, digital scale (manual entry in v1).

### 2.5 Design and Implementation Constraints

| ID | Constraint |
|---|---|
| **C-1** | Cats are the only species supported; the data model shall still carry a species field so future expansion needs no migration |
| **C-2** | Controlled/scheduled drugs shall be recorded in a tamper-evident, append-only register; entries are never hard-deleted |
| **C-3** | Clinical records shall be retained for the statutory minimum (assumed 7 years — confirm) and never hard-deleted by application logic |
| **C-4** | Imported food shall display country of origin and expiry; expired batches shall never be sellable |
| **C-5** | Card data shall never be stored on platform servers. In v1.1 this is absolute: no card number, and no gateway token either, enters the system — an in-store card payment is recorded only as a method, amount and terminal slip reference. When a gateway is integrated (Phase 5), only its tokens are retained (PCI-DSS SAQ-A posture) |
| **C-6** | Money **arithmetic** is performed in integer minor units with an explicit currency code, and amounts are **stored in an exact decimal column** (`numeric(12,2)`); no floating-point type is used for money anywhere. Rounding happens once, where a computed figure becomes a stored one (ADR-317) |
| **C-7** | Timestamps are stored in UTC and rendered in the branch's configured local timezone |
| **C-8** | English at launch, with an i18n layer ready for a second language |
| **C-9** | Every state-changing staff action shall be attributable to a named user in the audit log |

### 2.6 Assumptions and Dependencies

| ID | Assumption |
|---|---|
| **A-1** | A single physical location at launch; the data model supports multiple branches from day one |
| **A-2** | *(Deferred in v1.1.)* No payment gateway is integrated at launch. Money is collected as cash on delivery, cash in store, card in store through a standalone terminal whose reference is recorded manually, or bank transfer confirmed by staff. The order and invoice model treats a payment as a record of money received by any method, so adding a gateway later requires no change to orders, invoices or refunds |
| **A-3** | Delivery uses a third-party courier with an API or CSV manifest; local same-day delivery may be self-operated |
| **A-4** | Cash on delivery is offered and must be reconciled against courier settlements |
| **A-5** | The business holds the licences required to import and sell animal feed and to operate a veterinary practice |
| **A-6** | Vets enter their own clinical notes; no transcription service is assumed |
| **A-7** | Default vaccination schedules follow standard feline guidelines and are editable by an administrator |
| **A-8** | Boarding and clinic operate at the same premises, so a boarded cat can be seen by a vet without transport |

---

## 3. External Interface Requirements

### 3.1 User Interfaces
- **UI-1** Responsive layouts at 360 / 768 / 1024 / 1440 px breakpoints.
- **UI-2** A persistent global search in the back office resolving customers, cats, orders, invoices and products by name, phone or ID.
- **UI-3** Destructive actions require confirmation stating exactly what will be affected.
- **UI-4** Every list view supports filtering, sorting, pagination and CSV export.
- **UI-5** Clinic queue, boarding occupancy and order fulfilment screens auto-refresh at least every 30 seconds without losing unsaved input.
- **UI-6** Accessibility target WCAG 2.1 AA: keyboard operable, visible focus, 4.5:1 text contrast, labelled fields, announced errors.
- **UI-7** A cat's profile header always shows name, photo, age, sex/neuter status, latest weight, allergies, chronic conditions and active alerts.
- **UI-8** Clinical screens shall show weight and dose fields with units always visible, never as bare numbers.

### 3.2 Hardware Interfaces
- **HW-1** Receipt printing over ESC/POS to a networked or USB thermal printer.
- **HW-2** Barcode/QR scanning as keyboard-wedge input on product, batch and boarding-unit screens.
- **HW-3** ~~Microchip number entry via reader (keyboard-wedge) or manual typing, with length/format validation.~~ **Removed in v2.0 — microchipping is not in general use in Bangladesh.** The ID is retained rather than reused, so that nothing already traced to HW-3 silently points at a different requirement.

### 3.3 Software Interfaces

| Interface | Direction | Purpose | Failure behaviour |
|---|---|---|---|
| ~~Payment gateway~~ | ~~Out + webhook in~~ | ~~Authorise, capture, refund~~ | **Deferred in v1.1 — not integrated at launch.** No external payment interface exists in the launch build; payments are recorded by staff or on delivery (FR-ORD-8). Retained here because the integration point is expected to return in a later phase |
| SMS gateway | Out | Password-reset tokens, reminders, order status | Queue with backoff retry; fall back to email |
| Transactional email | Out | Receipts, records, reminders | Queue and retry; failures listed in an admin log |
| Push notifications | Out | App alerts (Phase 2) | Silent fail; in-app inbox remains source of truth |
| Courier API | Out + in | Create shipment, fetch tracking | Fall back to CSV manifest export |
| Accounting export | Out | Journal/invoice export (CSV or API) | Scheduled and re-runnable for any date range |
| Object storage | Out + in | Images, lab PDFs, care photos | Retry uploads; surface failure to the user |
| Staff SSO (optional) | In | OIDC staff login | Local password login remains available |

### 3.4 Communications Interfaces
- **COM-1** All traffic over HTTPS (TLS 1.2+), with HSTS enabled.
- **COM-2** Public and internal APIs are REST/JSON with versioned paths (`/api/v1/...`).
- **COM-3** Webhooks are signature-authenticated and processed idempotently by event ID.
- **COM-4** Real-time back-office updates use WebSocket where available, degrading to polling.

---

## 4. Functional Requirements

Modules: **ACC** accounts & identity · **CAT** cat profiles · **CLN** clinic · **SRG** surgery · **PHM** pharmacy & prescriptions · **SHP** shop & catalogue · **IMP** imports, batches & inventory · **ORD** orders & fulfilment · **SUB** subscriptions · **BRD** boarding · **BIL** billing & payments · **NTF** notifications · **ADM** administration · **RPT** reporting

### 4.1 Accounts and Identity (ACC)

| ID | Requirement | Priority |
|---|---|---|
| FR-ACC-1 | Guests shall register with a name, a mobile number and/or an email address (at least one is required), and a password, after passing the CAPTCHA in FR-ACC-13. Mobile number is the primary identifier when both are supplied. Mobile number and email address are each unique across customer accounts, since either may be used to log in (FR-ACC-3). No phone or email verification is performed — the account is usable as soon as it is created. *(Reworded in v2.2.)* | M |
| FR-ACC-2 | ~~The system shall verify the mobile number by OTP and the email by a signed link before enabling checkout or booking.~~ **Withdrawn in v2.1** — the business owner does not want phone or email verification. An account is usable as soon as it is created; abuse of registration is controlled by FR-ACC-13 and NFR-SEC-6 instead. The ID is retained rather than reused. | — |
| FR-ACC-3 | Customers and staff shall log in with their registered mobile number or email address and their password. There is no OTP login. Customer and staff sign-in are separate: a customer credential never grants a staff session, and a staff session carries the roles assigned under FR-ACC-6. A failed login shall show one generic message that does not reveal whether the identifier exists; failed attempts shall be rate-limited and progressively delayed. *(Reworded in v2.2; staff added in v2.3.)* | M |
| FR-ACC-4 | Customers and staff shall reset passwords through a time-limited, single-use token sent to the mobile number, or the email if one was supplied, registered on the account. A new staff account has no password until its holder sets one this way. *(Staff added in v2.3.)* *(Reworded in v2.1 — no channel is verified. Retained in v2.2: the reset token is account recovery, not verification, and is sent only when the customer asks for a reset.)* | M |
| FR-ACC-5 | A customer profile shall store name, phone(s), email, multiple delivery addresses, a default address and communication preferences per channel. | M |
| FR-ACC-6 | Staff accounts shall be created only by an Administrator and assigned one or more roles (RBAC). | M |
| FR-ACC-7 | Staff sessions shall expire after a configurable idle period (default 30 minutes) and require re-authentication. | M |
| FR-ACC-8 | ~~Staff with clinical or financial privileges shall be required to use two-factor authentication.~~ **Withdrawn in v2.2** at the business owner's instruction — staff sign in with identifier and password, like customers; their accounts remain protected by FR-ACC-6 (administrator-created, RBAC), FR-ACC-7 (idle expiry), NFR-SEC-6 (rate limiting and lockout) and FR-ACC-12 (audit). The ID is retained rather than reused. | — |
| FR-ACC-9 | Receptionists shall create a customer record on behalf of a walk-in with only name and phone, marking it incomplete until the customer sets a password for online access. *(Reworded in v2.2 — no verification exists.)* | M |
| FR-ACC-10 | The system shall detect and offer to merge duplicate customer records matched on phone or email, preserving all cats, orders and history under the surviving record. | S |
| FR-ACC-11 | Customers shall be able to request account deletion; the system shall anonymise personal data while retaining clinical and financial records required by law (per C-3). | S |
| FR-ACC-12 | The system shall maintain an audit log of all authentication events and all staff data changes, recording actor, action, entity, before/after values and timestamp. | M |
| FR-ACC-13 | *(Added in v2.1.)* Self-service registration shall require a correctly solved CAPTCHA. The challenge shall be generated and checked by the platform itself (no third-party CAPTCHA service), be single-use and expire after a short period (default 2 minutes), and be checked before any account lookup or password hashing. A user who cannot solve it shall be able to request a new challenge and shall be told how to register in person at reception (FR-ACC-9). | M |

### 4.2 Cat Profiles (CAT)

| ID | Requirement | Priority |
|---|---|---|
| FR-CAT-1 | A customer shall register one or more cats. A cat shall have: name, photo(s), sex, date of birth (exact or estimated), breed, colour/markings, and species (default Cat). | M |
| FR-CAT-2 | A cat profile shall record neuter status with date, current weight and a weight history. | M |
| FR-CAT-3 | A cat profile shall record allergies, chronic conditions, current medications, dietary notes and behavioural notes (e.g. handling risk, does not tolerate other cats). | M |
| FR-CAT-4 | The system shall flag a cat as **high risk / handle with care** and display that flag prominently on every clinic, boarding and order screen. | M |
| FR-CAT-5 | The system shall maintain vaccination records: vaccine type, batch, date given, administering vet and next-due date. | M |
| FR-CAT-6 | The system shall maintain deworming and parasite-control records with next-due dates. | M |
| FR-CAT-7 | The system shall compute and display a cat's vaccination status as **Current**, **Due soon** (configurable window, default 30 days) or **Overdue**. | M |
| FR-CAT-8 | Ownership of a cat shall be transferable between customers by an Administrator, preserving the full medical history with an ownership-change audit entry. | S |
| FR-CAT-9 | A cat may be marked deceased with a date; the record shall be retained, hidden from booking flows and excluded from reminders. | M |
| FR-CAT-10 | Customers shall view and download their cat's medical history as a PDF. | S |
| FR-CAT-11 | Each cat shall have a printable QR code resolving to its profile, used on boarding units, cage cards and carriers. | C |

### 4.3 Clinic — Appointments and Consultations (CLN)

**Appointments**

| ID | Requirement | Priority |
|---|---|---|
| FR-CLN-1 | The system shall publish bookable services with duration, price, description and the roles qualified to perform them (consultation, vaccination, spay, neuter, dental, grooming, emergency, follow-up). | M |
| FR-CLN-2 | Customers shall view available slots by service, date and vet, and book a slot for a named cat. | M |
| FR-CLN-3 | Slot availability shall be derived from vet rosters, service duration, room/theatre availability and existing bookings; double-booking shall be prevented by a transactional hold. | M |
| FR-CLN-4 | A slot shall be held for a configurable period (default 10 minutes) while the customer completes booking, then released. | M |
| FR-CLN-5 | Customers shall reschedule or cancel up to a configurable cut-off (default 4 hours before); later changes require contacting the clinic. | M |
| FR-CLN-6 | The system shall support walk-in registration into a live queue with a triage priority of routine / urgent / emergency, where emergencies pre-empt the queue. | M |
| FR-CLN-7 | The system shall send appointment confirmations immediately, and reminders 24 hours and 2 hours before, on the customer's preferred channel. | M |
| FR-CLN-8 | Staff shall view a day/week calendar per vet and per room, with drag-and-drop rescheduling that re-validates all conflicts. | S |
| FR-CLN-9 | The system shall record no-shows against the customer and surface a no-show count on their profile. | S |
| FR-CLN-10 | The system shall support a waitlist: when a slot is cancelled, waitlisted customers are notified in order. | C |
| FR-CLN-11 | Appointment statuses shall be: `booked` → `checked_in` → `in_consultation` → `completed`, with `cancelled` and `no_show` as terminal states. Transitions shall be validated. | M |

**Consultation and EMR**

| ID | Requirement | Priority |
|---|---|---|
| FR-CLN-12 | A nurse or vet shall record vitals at check-in: weight, temperature, heart rate, respiratory rate, body condition score, pain score. | M |
| FR-CLN-13 | The vet shall record a consultation in SOAP structure, with free text plus a coded problem/diagnosis list. | M |
| FR-CLN-14 | The consultation screen shall display the cat's full history — previous visits, diagnoses, vaccinations, prescriptions, lab results, weight trend, boarding stays and purchased food — without leaving the screen. | M |
| FR-CLN-15 | The vet shall order diagnostics (blood work, urinalysis, FeLV/FIV test, imaging) and record results as structured values and/or uploaded files (PDF, JPEG, DICOM-as-file). | M |
| FR-CLN-16 | Lab results shall record reference ranges and flag out-of-range values visually. | S |
| FR-CLN-17 | The vet shall record treatments and procedures performed, with consumables and medicines used, which shall deduct from inventory. | M |
| FR-CLN-18 | The system shall generate a discharge/visit summary for the owner containing diagnosis, treatment, medicines, home-care instructions and the follow-up date, deliverable by print and by the customer portal. | M |
| FR-CLN-19 | A consultation shall be **finalised** by the vet; after finalisation the note becomes read-only and later corrections are recorded as timestamped, attributed addenda. | M |
| FR-CLN-20 | The vet shall schedule a follow-up appointment or reminder directly from the consultation. | M |
| FR-CLN-21 | The system shall support attaching photographs to a clinical record (e.g. wound progression) with date and note. | S |
| FR-CLN-22 | The system shall support clinical note templates per service type to speed repetitive entry. | C |
| FR-CLN-23 | The system shall record consent forms (surgery, anaesthesia, euthanasia) as signed uploads or on-screen signature, linked to the cat and the procedure. | M |
| FR-CLN-24 | Only a user holding the Veterinarian role shall finalise diagnoses, prescribe, or authorise prescription diets. | M |

### 4.4 Surgery — Spay, Neuter and Other Procedures (SRG)

| ID | Requirement | Priority |
|---|---|---|
| FR-SRG-1 | The system shall schedule surgeries against a theatre, a surgeon and an assistant, preventing conflicts on any of the three. | M |
| FR-SRG-2 | Booking a surgery shall create a pre-operative checklist: fasting instructions issued, pre-anaesthetic blood work, weight recorded, consent signed, risk assessment (ASA grade). | M |
| FR-SRG-3 | The system shall block a surgery from being marked started until consent is recorded, or shall require an explicit, audited override by a vet with a reason. | M |
| FR-SRG-4 | The system shall record an anaesthesia log: drugs, doses, route, times, and periodic vitals during the procedure. | M |
| FR-SRG-5 | The system shall record a surgical report: procedure performed, findings, technique, suture material, complications, surgeon and assistant. | M |
| FR-SRG-6 | On completion of a spay or neuter, the system shall automatically set the cat's neuter status and date. | M |
| FR-SRG-7 | The system shall generate post-operative instructions and schedule follow-ups (e.g. wound check day 3, suture removal day 10–14) as reminders. | M |
| FR-SRG-8 | Drugs used in surgery shall deduct from inventory by batch; controlled drugs shall additionally write an append-only register entry naming the administering vet, the cat, the dose and the wastage. | M |
| FR-SRG-9 | The system shall support recording a surgery for a cat that is simultaneously boarding, linking the stay and the procedure. | S |
| FR-SRG-10 | The system shall support recovery-ward status tracking (recovering / stable / ready for discharge) visible to reception for owner updates. | S |
| FR-SRG-11 | The system shall support subsidised or campaign spay/neuter pricing (e.g. community-cat programmes) as a distinct price list with its own reporting. | C |

### 4.5 Pharmacy and Prescriptions (PHM)

| ID | Requirement | Priority |
|---|---|---|
| FR-PHM-1 | A vet shall issue a prescription specifying medicine, strength, dose, route, frequency, duration, quantity and instructions, linked to the cat and the consultation. | M |
| FR-PHM-2 | The system shall calculate dose against the cat's most recent weight and warn when the entered dose falls outside the configured mg/kg range for that medicine. | S |
| FR-PHM-3 | The system shall warn on a prescribed medicine that conflicts with a recorded allergy or with an active medication interaction listed in the formulary. | S |
| FR-PHM-4 | The system shall maintain a feline formulary with medicine name, form, strength, default dosing range and contraindications, editable by an Administrator. | S |
| FR-PHM-5 | Dispensing a prescription shall deduct the exact batch from inventory and print a medicine label with cat name, owner, medicine, dose, instructions, date and expiry. | M |
| FR-PHM-6 | Prescriptions shall be repeatable within a validity period set by the vet; a repeat outside that period shall require a new vet authorisation. | S |
| FR-PHM-7 | Controlled drugs shall be flagged in the formulary, require a second staff witness on dispensing, and write an append-only register entry. Register entries shall never be edited or deleted; corrections are new, linked entries. | M |
| FR-PHM-8 | The system shall list all active prescriptions for a cat and let the owner view them in the portal. | M |
| FR-PHM-9 | The system shall let a vet issue a **prescription-diet authorisation** for a specific therapeutic food, cat and validity period, which unlocks that product for purchase in the shop (see FR-SHP-11). | M |

### 4.6 Boarding — Cat Care Centre (BRD)

| ID | Requirement | Priority |
|---|---|---|
| FR-BRD-1 | The system shall maintain boarding units with type (standard, deluxe, family/multi-cat, isolation), capacity in cats, location/block and nightly rate. | M |
| FR-BRD-2 | Customers shall check availability for a date range and number of cats, and see the unit types available with prices before booking. | M |
| FR-BRD-3 | Availability shall account for confirmed bookings, blocked maintenance periods, and a configurable turnaround/cleaning gap between stays. | M |
| FR-BRD-4 | The system shall support both **short-term** (nightly) and **long-term** (weekly/monthly) stays, with distinct rate cards and a configurable long-stay discount. | M |
| FR-BRD-5 | Booking shall require the cat's vaccination status to be **Current** (FR-CAT-7); if it is not, the system shall block confirmation and offer to book a vaccination appointment instead. An Administrator may override with a recorded reason. | M |
| FR-BRD-6 | Booking shall collect a stay profile: feeding schedule and food (own food or house food), medications and times, litter preference, vet-visit authorisation, emergency contact, and a spending limit for veterinary care during the stay. | M |
| FR-BRD-7 | *(Reworded in v1.1.)* The system shall require a deposit at booking (configurable percentage or fixed) and record the balance due at or before check-in. As no gateway is integrated, the deposit is paid by bank transfer or in person; the reservation shall be held in `reserved` (not `confirmed`) until a staff member records the deposit, and shall expire automatically after a configurable window, releasing the unit. | M |
| FR-BRD-8 | The system shall support multi-cat bookings where cats from one household share a unit, with per-cat care instructions but one reservation. | S |
| FR-BRD-9 | Check-in shall run a checklist: identity confirmation, vaccination proof verified, weight recorded, health observation on arrival, belongings inventory (carrier, bowls, toys, bedding, food) with photos, and owner signature. | M |
| FR-BRD-10 | The system shall produce a daily care task list per unit, generated from each cat's stay profile: feeds, medication doses, litter changes, play/enrichment sessions and observation checks. | M |
| FR-BRD-11 | Caretakers shall complete each task with a timestamp and their identity; missed or overdue tasks shall be highlighted on the boarding dashboard. | M |
| FR-BRD-12 | Caretakers shall record daily observations per cat: appetite (portion eaten), water intake, litter output, mood/behaviour, and free-text notes. | M |
| FR-BRD-13 | Caretakers shall upload daily photos or short videos, which the owner sees in the portal as a stay diary. | S |
| FR-BRD-14 | The system shall support incident reporting (injury, illness, escape attempt, fight, refusal to eat) with severity, immediate action taken, and automatic owner notification for medium and high severity. | M |
| FR-BRD-15 | A boarded cat shall be referable to the clinic, creating a consultation linked to the stay; resulting charges shall be added to the stay invoice and checked against the owner's spending limit, requiring owner approval above it. | M |
| FR-BRD-16 | The system shall support extending a stay if the unit is free, or relocating the cat to another unit, recording the reason. | M |
| FR-BRD-17 | The system shall support early check-out with pro-rata recalculation according to the configured cancellation and refund policy. | S |
| FR-BRD-18 | Check-out shall produce a stay summary: daily logs, weight change, incidents, medications given, vet visits, belongings returned, and final invoice, with owner signature. | M |
| FR-BRD-19 | The system shall enforce isolation rules: a cat with a suspected infectious condition shall only be assignable to an isolation unit, and the system shall record which staff entered isolation. | M |
| FR-BRD-20 | The boarding dashboard shall display, at a glance: occupancy today, arrivals, departures, cats not eating, overdue tasks, open incidents and unpaid balances. | M |
| FR-BRD-21 | The system shall support recurring/long-term billing: monthly invoicing for stays exceeding one billing cycle. | S |
| FR-BRD-22 | The system shall record daycare (same-day, non-overnight) stays as a distinct product with hourly or day rates. | C |

### 4.7 Shop — Catalogue and Products (SHP)

| ID | Requirement | Priority |
|---|---|---|
| FR-SHP-1 | The system shall maintain a product catalogue with categories: **Cat Food** (dry, wet, treats, kitten, adult, senior, therapeutic/prescription), **Litter** (clumping, silica, tofu, bentonite), **Litter Pots & Accessories** (trays, hooded boxes, scoops, mats, liners), **Bags & Carriers** (carriers, backpacks, travel bags, storage bags), **Health & Grooming**, **Toys & Furniture**. | M |
| FR-SHP-2 | A product shall carry: name, brand, description, images, category, tags, status (draft/active/archived) and SEO slug. | M |
| FR-SHP-3 | A product shall support variants (SKUs) by size/weight, flavour, colour and pack quantity, each with its own price, barcode, stock and images. | M |
| FR-SHP-4 | Food products shall additionally carry: **country of origin**, manufacturer, life stage, protein source, ingredient list, guaranteed analysis (protein/fat/fibre/moisture), feeding guide, storage instructions, and whether veterinary authorisation is required. | M |
| FR-SHP-5 | Customers shall filter the catalogue by **country of origin**, brand, category, life stage, protein source, price range, availability and rating; and sort by price, popularity, newest and rating. | M |
| FR-SHP-6 | The storefront shall present a browsable **"Shop by Country"** view (e.g. Germany, Thailand, Turkey, USA, Korea) showing origin flag, sourcing notes and the products from each origin. | M |
| FR-SHP-7 | Product search shall match name, brand, ingredient and tag, tolerate misspellings, and return results within the performance budget in NFR-PRF-2. | M |
| FR-SHP-8 | The product page shall display live availability derived from sellable stock (FR-IMP-8), and the earliest expiry date of the batch that would be allocated when that date is within a configurable window. | M |
| FR-SHP-9 | The system shall support cross-sell and up-sell blocks ("frequently bought together", "recommended for your cat's life stage"). | S |
| FR-SHP-10 | The system shall support verified customer reviews and ratings, postable only by customers who purchased the SKU, subject to moderation. | S |
| FR-SHP-11 | Products marked **prescription required** shall be visible but not purchasable unless the customer holds a valid, unexpired authorisation (FR-PHM-9) for a cat on their account; the cat shall be selected at add-to-cart and recorded on the order line. | M |
| FR-SHP-12 | The system shall recommend food to a customer based on their cat's life stage, weight, neuter status and recorded conditions. | C |
| FR-SHP-13 | The system shall support bundles/kits (e.g. "new kitten starter kit") priced as a unit while deducting each component from stock. | S |
| FR-SHP-14 | The system shall record a wishlist and a back-in-stock notification request per SKU. | S |
| FR-SHP-15 | Product content shall support two languages when i18n is enabled (C-8), falling back to the default language where a translation is missing. | C |

### 4.8 Imports, Batches and Inventory (IMP)

| ID | Requirement | Priority |
|---|---|---|
| FR-IMP-1 | The system shall maintain suppliers with country, contact details, currency, lead time and payment terms. | M |
| FR-IMP-2 | The system shall create purchase orders to a supplier for SKUs with quantity, unit cost, currency and expected delivery date. | M |
| FR-IMP-3 | The system shall maintain an **import consignment** record grouping one or more POs, holding: origin country, shipment reference, HS/tariff codes, import permit number, health/veterinary certificate, customs declaration, landed-cost components (freight, duty, clearing, insurance) and attached documents. | M |
| FR-IMP-4 | Goods receipt shall record, per SKU, one or more **batches**: batch/lot number, manufacture date, **expiry date**, quantity received, quantity rejected with reason, and the consignment it arrived on. | M |
| FR-IMP-5 | The system shall compute landed unit cost per batch by allocating consignment costs across received quantity, and use it for margin reporting. | S |
| FR-IMP-6 | Stock shall be tracked **per batch per location** (shop floor, warehouse, clinic dispensary, boarding store). | M |
| FR-IMP-7 | Order allocation shall use **FEFO** — the batch expiring soonest is allocated first — unless a staff member overrides with a recorded reason. | M |
| FR-IMP-8 | Sellable stock shall exclude expired batches, quarantined batches, damaged stock and quantities reserved by unpaid or in-flight orders. | M |
| FR-IMP-9 | The system shall block sale, dispensing and boarding use of any batch whose expiry date has passed, with no override. | M |
| FR-IMP-10 | The system shall raise expiry alerts at configurable thresholds (default 90 / 60 / 30 days) and provide a near-expiry report to support markdowns. | M |
| FR-IMP-11 | The system shall support a batch **recall**: marking a batch recalled shall quarantine remaining stock, list every order and cat that received it, and support notifying those customers. | M |
| FR-IMP-12 | The system shall raise low-stock alerts per SKU per location against a configurable reorder point, and suggest reorder quantities from consumption history and supplier lead time. | S |
| FR-IMP-13 | The system shall support stock transfers between locations and stock adjustments (damage, theft, sampling, internal use) with a mandatory reason and approval above a configurable value. | M |
| FR-IMP-14 | The system shall support cycle counts and full stock takes, producing a variance report before committing adjustments. | S |
| FR-IMP-15 | Every stock movement shall be an immutable ledger entry (receipt, sale, dispense, transfer, adjustment, return, wastage) recording SKU, batch, quantity delta, location, reference document and user. Current stock is derived from the ledger. | M |
| FR-IMP-16 | The system shall record temperature/storage requirements per product and flag storage-condition breaches reported by staff. | C |

### 4.9 Cart, Checkout, Orders and Fulfilment (ORD)

| ID | Requirement | Priority |
|---|---|---|
| FR-ORD-1 | Guests and customers shall add SKUs to a cart; a guest cart shall merge into the account cart on login. | M |
| FR-ORD-2 | The cart shall persist across sessions and devices for a logged-in customer. | M |
| FR-ORD-3 | The cart shall revalidate price and availability at checkout and clearly show any change before payment. | M |
| FR-ORD-4 | Checkout shall collect delivery address or pickup choice, delivery slot where applicable, payment method, and any prescription-cat selection required by FR-SHP-11. | M |
| FR-ORD-5 | The system shall compute order totals as: line subtotals − line discounts − order discounts + delivery charge + tax, showing each component. | M |
| FR-ORD-6 | The system shall support discount codes with rules: percentage or fixed, minimum spend, product/category scope, first-order-only, usage limits overall and per customer, and validity window. | S |
| FR-ORD-7 | The system shall support delivery charge rules by zone, order value, weight and express/standard service. | M |
| FR-ORD-8 | *(Reworded in v1.1 — gateway methods deferred.)* Payment methods at launch shall be: **cash on delivery**, **cash in store**, **card in store** through a standalone terminal with the terminal reference recorded manually, and **bank transfer** confirmed by staff. COD may be disabled per zone or above a value threshold. Online card and mobile-wallet payment are deferred (§1.2). | M |
| FR-ORD-9 | An order shall be confirmed, stock reserved by batch, confirmation sent and an invoice issued when payment is **recorded** — which at launch means a staff member confirms a bank transfer or in-store payment, or the order is accepted as COD. The system shall not assume payment confirmation arrives from an external gateway. | M |
| FR-ORD-10 | Order statuses shall be `pending_payment` → `confirmed` → `packing` → `dispatched` → `delivered`, with `cancelled`, `returned` and `refunded` as alternate terminal states; every transition shall be timestamped, attributed and validated. | M |
| FR-ORD-11 | Staff shall generate a pick list grouped by location and batch, and a packing slip per order. | M |
| FR-ORD-12 | Dispatch shall create a courier shipment (API or manifest) and record the tracking number, which is shown to the customer. | M |
| FR-ORD-13 | Customers shall track order status in the portal and receive status notifications on their preferred channel. | M |
| FR-ORD-14 | Customers shall cancel an order before it is packed; cancellation shall release reserved stock and trigger a refund where payment has already been received (FR-ORD-17). A COD order cancelled before dispatch involves no refund, as no money has changed hands. | M |
| FR-ORD-15 | The system shall support returns within a configurable window, with a reason, an approval step, and a policy that **opened food and used litter are non-returnable** unless a quality defect or recall is recorded. | M |
| FR-ORD-16 | Returned stock shall be received back into a quarantine location and require an explicit decision: restock (with original batch) or write off. | M |
| FR-ORD-17 | *(Reworded in v1.1.)* Refunds shall be issued as cash in store, by bank transfer, or as store credit, according to how the customer paid and what they choose. Every refund shall record method, amount, reference, the staff member issuing it, and the reason, and shall be linked to the original invoice as a credit note. Automated refund to a card or wallet returns when a gateway is integrated. | M |
| FR-ORD-18 | The system shall support in-store point-of-sale: staff scan items, take payment, print a receipt, and the sale shall use the same inventory ledger as online orders. | M |
| FR-ORD-19 | A single order shall be able to combine products, clinic charges and boarding charges into one invoice for a customer. | S |
| FR-ORD-20 | The system shall detect and prevent duplicate order submission from double-clicks, repeated form posts, or (when a gateway is later integrated) repeated payment callbacks, via an idempotency key. | M |
| FR-ORD-21 | *(Added in v1.1.)* A COD order shall be confirmable without prepayment. The system shall record the cash amount collected on delivery, the collecting courier or staff member, and the collection date, and shall reconcile collections against courier remittances (FR-BIL-7). An undelivered or refused COD order shall return stock to inventory through the returns path (FR-ORD-16). | M |
| FR-ORD-22 | *(Added in v1.1.)* A bank-transfer order shall be held in `pending_payment` while the customer submits a transfer reference, and shall be confirmed only when a staff member verifies receipt against the bank record. Held orders shall reserve stock for a configurable window (default 48 hours) and shall automatically expire and release that stock if payment is not confirmed, notifying the customer. | M |

### 4.10 Subscriptions and Auto-Repeat (SUB)

| ID | Requirement | Priority |
|---|---|---|
| FR-SUB-1 | Customers shall subscribe to a recurring delivery of food or litter at a chosen interval (e.g. every 2, 4 or 8 weeks). | S |
| FR-SUB-2 | *(Reworded in v1.1.)* The system shall generate the next order automatically at the interval and reserve stock. Without a gateway there is no saved payment token, so the generated order is raised as COD or as a bank transfer awaiting confirmation (FR-ORD-21, FR-ORD-22). Automatic charging returns when a gateway is integrated — until then, subscriptions save the customer from re-ordering, not from paying each time. | S |
| FR-SUB-3 | Customers shall pause, skip one delivery, change the interval or quantity, or cancel at any time before the next generation cut-off. | S |
| FR-SUB-4 | The system shall notify the customer a configurable number of days before each subscription charge. | S |
| FR-SUB-5 | Subscriptions shall support a configurable discount relative to one-off purchase. | C |
| FR-SUB-6 | A subscription for a prescription diet shall be suspended automatically when its veterinary authorisation expires, notifying the customer and the clinic. | S |

### 4.11 Billing and Payments (BIL)

| ID | Requirement | Priority |
|---|---|---|
| FR-BIL-1 | The system shall generate a sequential, non-reusable invoice number per branch and financial year. | M |
| FR-BIL-2 | Invoices shall itemise services, products, medicines, boarding nights and charges, showing tax per line where applicable. | M |
| FR-BIL-3 | The system shall support partial payments, deposits and outstanding balances per customer, with an ageing view. | M |
| FR-BIL-4 | The system shall support multiple tenders on one invoice (e.g. part card, part cash). | S |
| FR-BIL-5 | The system shall record refunds and credit notes against the original invoice, never by editing it. | M |
| FR-BIL-6 | Finalised invoices shall be immutable; corrections are made by credit note and re-issue. | M |
| FR-BIL-7 | *(Reworded in v1.1 — now Must.)* The system shall reconcile **COD remittances from couriers**, **bank transfers against the bank statement**, and **in-store cash against the till count** with recorded payments, and shall flag every mismatch for investigation. With no gateway settlement file to rely on, manual reconciliation is the only control over money received, so it is a launch requirement rather than a later refinement. | M |
| FR-BIL-8 | The system shall calculate tax per configurable tax classes per product and service. | M |
| FR-BIL-9 | The system shall support store credit and gift vouchers as tender. | C |
| FR-BIL-10 | The system shall export invoices, payments and credit notes to the accounting system for any date range, re-runnably. | S |
| FR-BIL-11 | The system shall support a loyalty scheme accruing points on spend and redeemable as discount. | C |
| FR-BIL-12 | *(Added in v1.1.)* Staff shall record a payment received outside the system with method, amount, currency, reference (transfer reference, terminal slip number or receipt number), date received and receiving staff member. Recording a payment shall be permission-controlled, fully audited, and shall require a second approval above a configurable value. | M |
| FR-BIL-13 | *(Added in v1.1.)* The system shall provide a daily cash-up view per branch listing cash, in-store card and transfer payments recorded that day, with expected versus counted totals and a recorded variance explanation. | S |

### 4.12 Notifications (NTF)

| ID | Requirement | Priority |
|---|---|---|
| FR-NTF-1 | The system shall send templated notifications over email, SMS, push and in-app inbox, selected by the customer's per-channel preference. | M |
| FR-NTF-2 | Notification templates shall be editable by an Administrator with variable placeholders and a preview. | S |
| FR-NTF-3 | The system shall send: appointment confirmation and reminders; vaccination and deworming due reminders; prescription refill reminders; surgery pre-op instructions; post-op follow-up; boarding confirmation, check-in and daily update; incident alerts; order confirmation, dispatch and delivery; subscription pre-charge; back-in-stock; and abandoned-cart reminders. | M |
| FR-NTF-4 | Transactional notifications (password reset, payment, medical, incident) shall be exempt from marketing opt-out; marketing notifications shall honour opt-out and include an unsubscribe mechanism. | M |
| FR-NTF-5 | The system shall not send non-urgent notifications outside configured quiet hours, queueing them until the window opens. | S |
| FR-NTF-6 | The system shall log every notification with channel, template, recipient, status and provider response, and support manual resend. | M |
| FR-NTF-7 | High-severity boarding incidents (FR-BRD-14) shall notify the owner within 15 minutes of the record being saved and escalate to a phone-call task if unacknowledged within a configurable period. | M |

### 4.13 Administration and Configuration (ADM)

| ID | Requirement | Priority |
|---|---|---|
| FR-ADM-1 | Administrators shall manage branches, opening hours, holidays and per-branch configuration. | M |
| FR-ADM-2 | Administrators shall manage staff, roles and granular permissions; permissions shall be enforced server-side on every request, not only hidden in the UI. | M |
| FR-ADM-3 | Administrators shall manage vet and caretaker rosters, leave and shift patterns, which drive appointment availability and boarding task assignment. | M |
| FR-ADM-4 | Administrators shall manage service catalogue, pricing, rate cards, tax classes, delivery zones and charges. | M |
| FR-ADM-5 | Administrators shall configure business rules exposed as parameters: booking cut-offs, deposit percentage, expiry alert thresholds, reorder points, vaccination validity windows, quiet hours, session timeout. | M |
| FR-ADM-6 | Administrators shall view the audit log filtered by user, entity, action and date range, and export it. | M |
| FR-ADM-7 | The system shall provide a content management capability for storefront pages, banners, FAQs and policy documents. | S |
| FR-ADM-8 | The system shall support a maintenance mode that keeps the back office available while the storefront shows a notice. | C |

### 4.14 Customer Portal (self-service view)

| ID | Requirement | Priority |
|---|---|---|
| FR-POR-1 | The portal shall show the customer's cats, upcoming appointments, boarding stays, orders and outstanding balances on one dashboard. | M |
| FR-POR-2 | The portal shall show each cat's vaccination status with a one-click path to book what is due. | M |
| FR-POR-3 | The portal shall show visit summaries, prescriptions and lab results once the vet has finalised them. | M |
| FR-POR-4 | The portal shall show the boarding stay diary, including daily logs and photos. | S |
| FR-POR-5 | The portal shall allow re-ordering a previous order in one action. | S |
| FR-POR-6 | The portal shall allow the customer to update their profile, addresses and notification preferences. | M |

---

## 5. Data Requirements

### 5.1 Core Entities

| Entity | Key attributes | Principal relationships |
|---|---|---|
| **Customer** | name, phones, emails, addresses, preferences, status | 1..* Cats, 1..* Orders, 1..* Invoices |
| **Cat** | name, sex, DOB, breed, colour, neuter status, weight history, allergies, conditions, flags, deceased date | belongs to Customer; 1..* Visits, Vaccinations, Prescriptions, Stays |
| **Staff User** | name, roles, licence number, branch, status, MFA | performs Visits, Surgeries, Care Tasks |
| **Appointment** | service, cat, vet, room, start, duration, status, triage priority | → Visit |
| **Visit (Consultation)** | SOAP note, vitals, diagnoses, treatments, finalised_at, addenda | belongs to Cat; → Prescriptions, Lab Orders, Invoice lines |
| **Surgery** | procedure, theatre, surgeon, assistant, consent, anaesthesia log, report | belongs to Cat and Visit |
| **Prescription** | medicine, dose, frequency, duration, quantity, validity, issuing vet | belongs to Cat and Visit; → Dispense records |
| **Diet Authorisation** | product, cat, issuing vet, valid from/to | gates Order lines |
| **Product** | name, brand, category, attributes, status | 1..* SKUs |
| **SKU** | size, flavour, barcode, price, prescription_required, COO | 1..* Batches |
| **Batch** | lot number, manufacture date, expiry date, consignment, landed cost | has Stock per Location |
| **Consignment** | origin country, shipment ref, HS code, permit, certificates, costs | 1..* Batches |
| **Supplier** | name, country, currency, lead time, terms | 1..* Purchase Orders |
| **Stock Ledger Entry** | SKU, batch, location, delta, reason, reference, user, timestamp | immutable |
| **Order** | customer, lines, totals, status, payment, shipment | 1..* Order Lines (each with SKU + batch allocation) |
| **Subscription** | SKU, quantity, interval, next run, intended payment method, status *(payment token deferred to Phase 5)* | generates Orders |
| **Boarding Unit** | type, capacity, block, nightly rate, status | 1..* Stays |
| **Boarding Stay** | cat(s), unit, date range, rate, deposit, care profile, status | 1..* Care Tasks, Care Logs, Incidents |
| **Care Task** | type, due time, completed at, completed by, notes | belongs to Stay |
| **Incident** | severity, description, action taken, notified at | belongs to Stay |
| **Invoice** | number, lines, tax, totals, status | 1..* Payments, Credit Notes |
| **Payment** | method, amount, currency, external reference (transfer ref, terminal slip, receipt no. — or a gateway ref in Phase 5), received date, recorded-by staff member, status | belongs to Invoice |
| **Notification** | channel, template, recipient, payload, status | audit record |
| **Audit Entry** | actor, action, entity, before, after, timestamp, IP | immutable |

### 5.2 Data Integrity Rules
- **D-1** A cat cannot exist without an owner; ownership transfer preserves the cat's identity and history.
- **D-2** A batch's expiry date is mandatory for food and medicine categories.
- **D-3** Stock on hand is never stored as an editable number; it is derived from the immutable stock ledger (FR-IMP-15).
- **D-4** An order line records the specific batch allocated, so a recall can trace from batch to customer to cat.
- **D-5** A finalised clinical note, a finalised invoice, a controlled-drug register entry and an audit entry are append-only.
- **D-6** Soft deletion is used for customer-facing records; clinical and financial records are never hard-deleted (C-3).
- **D-7** A boarding stay and a clinic visit for the same cat may overlap in time; no other stay may overlap for the same cat.

### 5.3 Data Retention and Privacy
- **D-8** Clinical records retained for the statutory minimum (assumed 7 years after last visit or after death).
- **D-9** Financial records retained per tax law (assumed 6 years).
- **D-10** Boarding photos and videos retained 12 months, then purged unless attached to an incident.
- **D-11** Personal data is exportable on request in a machine-readable format and erasable subject to D-8 and D-9.
- **D-12** Payment card numbers are never stored (C-5).

---

## 6. Non-Functional Requirements

### 6.1 Performance (PRF)
- **NFR-PRF-1** Storefront pages shall reach Largest Contentful Paint ≤ 2.5 s on a 4G connection for the 75th-percentile user.
- **NFR-PRF-2** Catalogue search and filter results shall return in ≤ 500 ms at p95 for a catalogue of up to 20,000 SKUs.
- **NFR-PRF-3** Back-office list and detail screens shall load in ≤ 1.5 s at p95.
- **NFR-PRF-4** The cat profile with full history shall render in ≤ 2 s at p95 for a cat with 10 years of records.
- **NFR-PRF-5** Checkout submission through to order placement shall complete in ≤ 3 s at p95. *(In v1.1 there is no gateway redirect to exclude; the budget covers the whole submission.)*
- **NFR-PRF-6** The system shall sustain 300 concurrent customer sessions and 40 concurrent staff sessions at launch, scaling horizontally to 5× without redesign.
- **NFR-PRF-7** Batch jobs (reminders, subscriptions, reports) shall not degrade interactive response times; they run on separate workers.

### 6.2 Availability and Reliability (AVL)
- **NFR-AVL-1** Target availability 99.5% monthly for the storefront and 99.9% during clinic opening hours for the back office.
- **NFR-AVL-2** Planned maintenance shall occur in a published low-traffic window with at least 48 hours' notice.
- **NFR-AVL-3** If connectivity to the server is lost, the front-desk application shall continue to display the day's appointment list and boarding task list from local cache, in read-only mode, and shall clearly indicate it is offline.
- **NFR-AVL-4** Database backups shall run at least daily with point-in-time recovery; RPO ≤ 15 minutes, RTO ≤ 4 hours.
- **NFR-AVL-5** Backup restoration shall be tested at least quarterly and the test recorded.
- **NFR-AVL-6** External integration failures shall degrade gracefully per §3.3 and never lose a customer's submitted data.

### 6.3 Security (SEC)
- **NFR-SEC-1** Passwords shall be stored using a memory-hard hash (Argon2id or bcrypt cost ≥ 12); a minimum length of 10 characters is enforced and known-breached passwords are rejected.
- **NFR-SEC-2** Authorisation shall be enforced server-side on every request; no endpoint shall rely on client-side role checks.
- **NFR-SEC-3** All data shall be encrypted in transit (TLS 1.2+) and at rest (database and object storage).
- **NFR-SEC-4** The application shall defend against OWASP Top 10 classes: parameterised queries, output encoding, CSRF tokens, strict Content Security Policy, signed and short-lived session tokens, and server-side validation of every input.
- **NFR-SEC-5** File uploads shall be type- and size-validated, virus-scanned, stored outside the web root and served through signed, expiring URLs.
- **NFR-SEC-6** Rate limiting and lockout shall apply to login, password reset and checkout endpoints *(OTP removed in v2.2)*. Rate limiting per client address shall also apply to the registration and CAPTCHA-issue endpoints *(added in v2.1)*.
- **NFR-SEC-7** Secrets shall be held in a managed secret store, never in source control or configuration files in the repository.
- **NFR-SEC-8** Access to clinical records shall be limited to clinical roles and the owning customer; every access to a clinical record shall be logged.
- **NFR-SEC-9** Dependencies shall be scanned for vulnerabilities on every build; critical findings block release.
- **NFR-SEC-10** A penetration test shall be completed before go-live and annually thereafter.

### 6.4 Usability (USA)
- **NFR-USA-1** A first-time customer shall be able to complete registration, add a cat and book an appointment in under 3 minutes without assistance.
- **NFR-USA-2** A receptionist shall be able to register a walk-in and place them in the queue in under 60 seconds.
- **NFR-USA-3** A caretaker shall be able to record a completed feeding task in no more than two taps on a tablet.
- **NFR-USA-4** Error messages shall state what went wrong and what to do next, in plain language, never exposing stack traces or internal identifiers.
- **NFR-USA-5** Forms longer than one screen shall preserve entered data across validation errors and accidental navigation.
- **NFR-USA-6** The interface shall meet WCAG 2.1 AA (UI-6).

### 6.5 Compatibility and Portability (CMP)
- **NFR-CMP-1** Supported browsers per §2.4; the storefront shall remain functional (if visually reduced) two versions further back.
- **NFR-CMP-2** The storefront shall be fully usable on a 360 px-wide screen.
- **NFR-CMP-3** The system shall run in containers and shall not depend on any single cloud provider's proprietary service for core function.

### 6.6 Maintainability and Supportability (MNT)
- **NFR-MNT-1** Automated test coverage shall be at least 70% of business-logic modules, with end-to-end tests covering the flows in §9.
- **NFR-MNT-2** All environments (dev, staging, production) shall be provisioned from code.
- **NFR-MNT-3** Structured logs with a correlation ID shall span a request across services.
- **NFR-MNT-4** The system shall expose health and readiness endpoints, and emit metrics for request rate, error rate, latency and queue depth, with alerting on defined thresholds.
- **NFR-MNT-5** Database schema changes shall be applied by versioned, reversible migrations.
- **NFR-MNT-6** Public APIs shall be documented (OpenAPI) and versioned; breaking changes require a new version.

### 6.7 Compliance and Legal (CMP-L)
- **NFR-LEG-1** The system shall support the record-keeping required of a veterinary practice, including the controlled-drug register (C-2).
- **NFR-LEG-2** Imported food records shall retain import permits and certificates for the statutory period and be retrievable per consignment for inspection.
- **NFR-LEG-3** Invoices and receipts shall carry the legally required business identifiers and tax details.
- **NFR-LEG-4** Consent shall be captured and stored for surgery, anaesthesia and euthanasia, and for marketing communication.
- **NFR-LEG-5** Published terms, privacy policy, refund policy and boarding terms shall be versioned, with the version the customer accepted recorded against their acceptance.

### 6.8 Scalability (SCL)
- **NFR-SCL-1** The architecture shall support additional branches without code change: data is branch-scoped and staff are branch-assigned.
- **NFR-SCL-2** Read-heavy catalogue traffic shall be servable from cache/CDN so that a promotional spike does not affect clinical operations.
- **NFR-SCL-3** Media shall be served from a CDN with responsive image variants.

---

## 7. Business Rules

| ID | Rule | Enforced by |
|---|---|---|
| **BR-1** | A cat may not be boarded unless its core vaccinations are current, except by audited administrator override. | FR-BRD-5 |
| **BR-2** | A therapeutic/prescription diet may only be purchased for a cat with a valid, unexpired veterinary authorisation. | FR-SHP-11, FR-PHM-9 |
| **BR-3** | Expired stock is never sellable, dispensable or feedable — no override exists. | FR-IMP-9 |
| **BR-4** | Stock is allocated FEFO by default; deviation requires a recorded reason. | FR-IMP-7 |
| **BR-5** | Surgery may not begin without recorded consent, except by audited veterinarian override with reason. | FR-SRG-3 |
| **BR-6** | Only a Veterinarian may finalise a diagnosis, prescribe, or authorise a prescription diet. | FR-CLN-24 |
| **BR-7** | Controlled drugs require a second staff witness and an append-only register entry. | FR-PHM-7 |
| **BR-8** | Veterinary treatment of a boarded cat beyond the owner's stated spending limit requires explicit owner approval. | FR-BRD-15 |
| **BR-9** | A cat suspected of an infectious condition may only occupy an isolation unit. | FR-BRD-19 |
| **BR-10** | Opened food and used litter are non-returnable unless defective or recalled. | FR-ORD-15 |
| **BR-11** | Finalised clinical notes and invoices are immutable; corrections are addenda or credit notes. | FR-CLN-19, FR-BIL-6 |
| **BR-12** | A boarding deposit is non-refundable inside the cancellation window; outside it, it is refunded in full. *(Window and percentages to be confirmed — §11.)* | FR-BRD-7, FR-BRD-17 |
| **BR-13** | A cat cannot have two overlapping boarding stays. | D-7 |
| **BR-14** | An order may not be dispatched while any line is unallocated or its batch is recalled. | FR-IMP-11, FR-ORD-10 |
| **BR-15** | COD is unavailable above a configurable order value or in configured zones. | FR-ORD-8 |
| **BR-16** | *(Added in v1.1.)* An order is never dispatched and a boarding unit is never held indefinitely on unconfirmed payment: bank-transfer orders and boarding reservations expire and release their stock or unit after their configured window. | FR-ORD-22, FR-BRD-7 |
| **BR-17** | *(Added in v1.1.)* Money recorded as received must be attributable to a named staff member and a reference, because no gateway record exists to corroborate it. | FR-BIL-12 |

---

## 8. Reporting and Analytics

### 8.1 Operational Reports
| Report | Contents | Primary user |
|---|---|---|
| Daily clinic schedule | Appointments, walk-ins, queue state, no-shows | Reception, Vet |
| Surgery register | Procedures by date, surgeon, outcome, complications | Vet, Admin |
| Vaccination due list | Cats due or overdue in a window, with contact details | Reception |
| Boarding occupancy | Occupancy rate by unit type, arrivals, departures, forecast | Boarding manager |
| Boarding care compliance | Tasks due vs completed, overdue tasks by caretaker | Boarding manager |
| Incident report | Incidents by severity, unit and outcome | Admin |
| Stock on hand | By SKU, batch, location, with valuation | Inventory |
| Near-expiry & expired | Batches by days to expiry, value at risk | Inventory |
| Low stock & reorder | SKUs below reorder point with suggested quantities | Procurement |
| Import consignment | Consignments, landed cost, documents, clearance status | Procurement |
| Order fulfilment | Orders by status, ageing, SLA breaches | Shop manager |

### 8.2 Financial Reports
| Report | Contents |
|---|---|
| Revenue by line of business | Clinic vs shop vs boarding, by period |
| Revenue by service and by product category | Including margin where landed cost is known |
| Revenue by country of origin | Sales split across sourced origins |
| Payment reconciliation | COD remittance vs delivered COD orders; bank transfers vs bank statement; in-store cash vs till count; variances flagged. *(Gateway settlement returns in Phase 5.)* |
| Daily cash-up | Cash, in-store card and transfer payments recorded per branch per day, expected vs counted, with variance explanations |
| Outstanding receivables | Balances by customer with ageing buckets |
| Refunds and credit notes | By reason and period |
| Tax summary | Output tax by class and period |

### 8.3 Customer and Growth Reports
| Report | Contents |
|---|---|
| New vs returning customers | By period and acquisition source |
| Customer lifetime value | Spend across all three lines of business |
| Cohort retention | Repeat purchase and repeat visit rates |
| Subscription health | Active, paused, churned; churn reasons |
| Cart abandonment | Rate and recovered value |
| Cross-line conversion | Clinic customers who buy food; boarding customers who use the clinic |

- **FR-RPT-1 (M)** Every report shall be filterable by date range and branch, viewable on screen, and exportable to CSV and PDF.
- **FR-RPT-2 (S)** Administrators shall schedule any report for recurring email delivery.
- **FR-RPT-3 (M)** A home dashboard per role shall show that role's key figures for today.

---

## 9. Acceptance Criteria

The release is accepted when each of the following end-to-end scenarios passes in the staging environment with production-like data.

| # | Scenario | Pass condition |
|---|---|---|
| **AC-1** | New customer registers, adds a cat, books a vaccination appointment | Account created after passing the CAPTCHA, with no verification step *(amended in v2.1)*; cat created; slot reserved without double-booking; confirmation received on the preferred channel |
| **AC-2** | Vet completes a consultation | SOAP note, vitals, diagnosis and treatment saved; consumables deducted from correct batches; discharge summary produced; note immutable after finalisation, with addendum possible |
| **AC-3** | Spay surgery, end to end | Pre-op checklist enforced; consent blocked start until recorded; anaesthesia log captured; neuter status auto-updated; post-op follow-ups scheduled; controlled drug register entry written and non-editable |
| **AC-4** | Prescription issued and dispensed | Dose warning fires outside range; allergy conflict warns; correct batch deducted; label prints; prescription visible in customer portal |
| **AC-5** | Customer browses imported foods by country | "Shop by Country" lists correct origins; filters and search return within NFR-PRF-2; product page shows origin, ingredients and availability |
| **AC-6** | Purchase with FEFO allocation | Order allocates the soonest-expiring non-expired batch; expired batch never allocated; stock ledger balances; invoice correct |
| **AC-7** | Prescription diet gating | Purchase blocked without authorisation; allowed with a valid one for the named cat; blocked again after expiry; cat recorded on the order line |
| **AC-8** | COD order delivered and reconciled | Order confirms without prepayment; flows through packing, dispatch with tracking, delivery; cash collected is recorded; COD remittance reconciles against the payment record and a deliberate shortfall is flagged |
| **AC-8b** | Bank-transfer order confirmed and expired | Order holds in `pending_payment` with stock reserved; staff confirmation moves it to `confirmed`; a second order left unconfirmed past its window expires automatically and releases its stock, notifying the customer |
| **AC-9** | Return and refund | Sealed product returned within window; received into quarantine; restock decision recorded; refund issued and audited; opened food rejected by policy |
| **AC-10** | Batch recall | Marking a batch recalled quarantines stock, lists every affected order, customer and cat, and supports notifying them |
| **AC-11** | Boarding booking blocked on vaccination | Booking blocked for an overdue cat; vaccination appointment offered; booking succeeds after vaccination is recorded |
| **AC-12** | Boarding stay, end to end | Deposit taken; check-in checklist with belongings photos completed; daily tasks generated and completed with attribution; daily logs and photos visible to owner; check-out summary and final invoice produced |
| **AC-13** | Boarding medical escalation | Vet referral from a stay creates a linked visit; charges above the spending limit require owner approval; charges appear on the stay invoice |
| **AC-14** | Boarding incident | High-severity incident notifies the owner within 15 minutes and escalates when unacknowledged |
| **AC-15** | Long-term stay billing | A two-month stay invoices per cycle at the long-stay rate with the discount applied correctly |
| **AC-16** | Combined invoice | One invoice correctly combines a consultation, a food purchase and boarding nights, with tax per line |
| **AC-17** | Subscription cycle | Next order generates on schedule as COD or awaiting transfer, notifies in advance, and skipping works. *(Automatic token charging is out of scope in v1.1.)* |
| **AC-18** | Role enforcement | A receptionist is denied prescribing and clinical finalisation at the API level, not merely in the UI |
| **AC-19** | Offline degradation | With the network disconnected, the front desk still shows today's appointments and boarding tasks read-only, with a clear offline indicator |
| **AC-20** | Audit and retention | Every state change in AC-2 through AC-13 appears in the audit log with actor, before and after values; no clinical record can be hard-deleted through the application |
| **AC-21** | Performance | NFR-PRF-1 to NFR-PRF-5 met under a load test at NFR-PRF-6 concurrency |
| **AC-22** | Security | Penetration test completed with no open critical or high findings |

---

## 10. Release Plan

| Phase | Scope | Rationale |
|---|---|---|
| **Phase 1 — Core operations (MVP)** | ACC, CAT, CLN (appointments + EMR), SRG (core), PHM (core), BIL (core), NTF (core), ADM, basic RPT | The clinic is the revenue engine and the source of the cat data every other line depends on |
| **Phase 2 — Commerce** | SHP, IMP, ORD, POS, remaining BIL, prescription-diet gating | Selling imported food safely needs batch/expiry tracking, so IMP ships with SHP, not after it |
| **Phase 3 — Care centre** | BRD in full, boarding billing, stay diary | Boarding depends on vaccination status from Phase 1 and on the food/stock model from Phase 2 |
| **Phase 4 — Growth** | SUB, loyalty, reviews, recommendations, mobile app, advanced RPT | Retention features once the operational base is stable |
| **Phase 5 — Online payments** *(added v1.1)* | Payment gateway integration: online card and wallet payment, saved tokens, automated refunds, gateway settlement reconciliation, subscription auto-charge | Deferred from launch by owner decision. Phases 1–4 are built so this slots in behind the existing payment record model without reworking orders, invoices or refunds |

Each phase is independently shippable and independently useful.

**Consequence of deferring Phase 5:** until a gateway exists, every payment passes through a person — a courier collecting cash, a staff member confirming a transfer, a till at the counter. The controls that substitute for a gateway's automatic record are therefore launch requirements, not refinements: FR-BIL-7 (reconciliation), FR-BIL-12 (attributable manual payment entry), FR-ORD-22 and FR-BRD-7 (expiry of unconfirmed holds) and BR-16 / BR-17.

---

## 11. Open Questions and Decisions Required

| # | Question | Owner | Blocks |
|---|---|---|---|
| **Q-1** | Which country's regulations govern veterinary record keeping, controlled drugs and feed import? Exact retention periods? | Owner / legal | C-2, C-3, NFR-LEG-1, D-8 |
| ~~**Q-2**~~ | ~~Which payment gateway(s), and which mobile wallets must be supported at launch?~~ **Parked in v1.1** — no gateway at launch. Revisit when Phase 5 is scheduled; it blocks nothing now. | Owner | — |
| **Q-3** | **Now high priority.** Is cash on delivery offered, and with what value ceiling and zone restrictions? With no gateway, COD is a primary payment method rather than a secondary one, so this decision now shapes the main revenue path. | Owner | FR-ORD-8, FR-ORD-21, BR-15 |
| **Q-3b** | *(Added v1.1.)* What is the bank-transfer confirmation window before an order expires and releases stock (default assumed 48 hours), and who is authorised to confirm receipt? | Owner | FR-ORD-22, FR-BIL-12 |
| **Q-3c** | *(Added v1.1.)* Is an in-store card terminal available at launch, and does its provider supply a settlement report that reconciliation can be checked against? | Owner | FR-ORD-8, FR-BIL-7 |
| **Q-4** | Which courier(s), and is an API available or is a CSV manifest the interface? | Owner | FR-ORD-12, A-3 |
| **Q-5** | Base currency, tax regime and tax rates per product/service class? | Accountant | FR-BIL-8, C-6 |
| **Q-6** | Boarding cancellation and refund policy: deposit percentage, cancellation window, early check-out pro-rata? | Owner | BR-12, FR-BRD-17 |
| **Q-7** | Boarding rate card: nightly, weekly, monthly rates by unit type, and multi-cat discount? | Owner | FR-BRD-4 |
| **Q-8** | Which source countries at launch, and are there import-licence constraints per origin? | Procurement | FR-SHP-6, FR-IMP-3 |
| **Q-9** | Is grooming an in-house service at launch or deferred? | Owner | FR-CLN-1 |
| **Q-10** | Are emergency and after-hours services offered, and with what staffing model? | Owner | FR-CLN-6 |
| **Q-11** | How many boarding units of each type exist, and is there a dedicated isolation ward? | Owner | FR-BRD-1, FR-BRD-19 |
| **Q-12** | Is a second language required at launch, and which? | Owner | C-8, FR-SHP-15 |
| **Q-13** | Which accounting system must the export target? | Accountant | FR-BIL-10 |
| **Q-14** | Is a customer mobile app contractually required, or is a responsive web app sufficient for Phase 2? | Owner | §2.4 |
| **Q-15** | Should community-cat / subsidised spay-neuter programmes be supported at launch? | Owner | FR-SRG-11 |
| **Q-16** | *(Added v2.1, narrowed v2.2.)* How does a walk-in customer whose *incomplete* record holds only name and phone (FR-ACC-9) set a password? Proposed: a password reset to the recorded phone (FR-ACC-4), since registering online with the same number is refused as a duplicate (FR-ACC-1). Owner to confirm. | Owner | FR-ACC-9, US-ACC-10 |
| **Q-17** | *(Added v2.1.)* Is registration in person at reception an acceptable alternative for customers who cannot solve an image CAPTCHA (WCAG 2.1 SC 1.1.1), or is an audio challenge also required? | Owner / accessibility | FR-ACC-13, NFR-USA-6 |

---

## 12. Appendices

### Appendix A — Traceability: business brief to requirements

| Brief item | Covered by |
|---|---|
| 1. Cat clinic with medical facilities, spaying, neutering, general medical assistance | §4.3 CLN (all), §4.4 SRG (all), §4.5 PHM (all), §4.2 CAT |
| 2. Sell cat food along with litter pots and cat bags | FR-SHP-1, FR-SHP-2, FR-SHP-3, §4.9 ORD, §4.11 BIL |
| 3. Different kinds of food sourced from different countries | FR-SHP-4, FR-SHP-5, FR-SHP-6, §4.8 IMP (all), §8.2 revenue by origin |
| 4. Cat care centre for temporary or long-term stays | §4.6 BRD (all), FR-BRD-4 short vs long term, FR-BRD-21 long-term billing |

### Appendix B — Primary workflows

**B.1 Clinic visit**
```
Book / walk-in → Check-in → Vitals → Consultation (SOAP)
   → [Diagnostics] → Treatment → [Prescription] → [Dispense]
   → Invoice → Payment → Discharge summary → Follow-up reminder
```

**B.2 Spay / neuter**
```
Book surgery → Pre-op checklist (fasting, bloods, weight, consent)
   → Admit → Anaesthesia log → Procedure → Surgical report
   → Neuter status updated → Recovery → Discharge + post-op instructions
   → Wound check (day 3) → Suture removal (day 10–14)
```

**B.3 Online order of imported food**
```
Browse / filter by country → Product page (origin, ingredients, expiry window)
   → Add to cart [prescription check] → Checkout → Payment
   → FEFO batch allocation + reservation → Pick & pack → Dispatch (tracking)
   → Delivered → [Return window]
```

**B.4 Boarding stay**
```
Check availability → Vaccination validation → Care profile → Deposit → Confirmed
   → Check-in (ID, vaccination proof, weight, health check, belongings, signature)
   → Daily: tasks generated → feeding / medication / litter / play / observation
            → logs + photos → owner sees diary
   → [Incident → owner notified]  [Vet referral → visit linked to stay]
   → Check-out (summary, belongings returned, final invoice, signature)
```

**B.5 Import to shelf**
```
Purchase order → Consignment (permit, certificates, HS code)
   → Customs clearance → Goods receipt (batch, expiry, quantity, rejects)
   → Landed cost allocation → Stock ledger entry → Sellable
   → Expiry alerts (90/60/30 days) → [Markdown] or [Write-off]
```

### Appendix C — Default state machines

| Entity | States |
|---|---|
| Appointment | `booked` → `checked_in` → `in_consultation` → `completed`; `cancelled`, `no_show` |
| Surgery | `scheduled` → `pre_op` → `in_theatre` → `recovery` → `discharged`; `cancelled` |
| Order | `pending_payment` → `confirmed` → `packing` → `dispatched` → `delivered`; `cancelled`, `returned`, `refunded` |
| Boarding stay | `enquiry` → `reserved` → `confirmed` → `checked_in` → `checked_out`; `cancelled`, `no_show` |
| Batch | `in_transit` → `received` → `available`; `quarantined`, `recalled`, `expired`, `written_off` |
| Invoice | `draft` → `issued` → `part_paid` → `paid`; `credited` |
| Prescription | `issued` → `dispensed` → `completed`; `expired`, `cancelled` |
| Subscription | `active` → `paused` → `active`; `suspended`, `cancelled` |

### Appendix D — Indicative role/permission matrix

Legend: **F** full · **W** create/edit · **R** read only · **—** no access

| Capability | Customer | Reception | Nurse | Vet | Caretaker | Inventory | Shop Mgr | Accountant | Admin |
|---|---|---|---|---|---|---|---|---|---|
| Own cat profile | W | W | W | W | R | — | — | — | F |
| Any cat profile | — | W | W | W | R | — | — | — | F |
| Appointments | W (own) | F | R | W | — | — | — | — | F |
| Clinical notes | R (own, finalised) | — | W (vitals) | F | — | — | — | — | R |
| Prescribe | — | — | — | F | — | — | — | — | — |
| Dispense medicine | — | W | W | F | — | R | — | — | F |
| Controlled drug register | — | — | R | W | — | R | — | — | R |
| Boarding bookings | W (own) | F | — | R | R | — | — | — | F |
| Care logs & tasks | R (own) | R | W | R | F | — | — | — | F |
| Incidents | R (own) | R | W | W | W | — | — | — | F |
| Product catalogue | R | R | — | — | — | R | F | — | F |
| Stock & batches | — | R | R | R | R | F | R | — | F |
| Purchase orders / imports | — | — | — | — | — | F | R | R | F |
| Orders & fulfilment | R (own) | W | — | — | — | R | F | R | F |
| Invoices & payments | R (own) | W | — | — | — | — | W | F | F |
| Refunds & credit notes | — | — | — | — | — | — | W | F | F |
| Reports | — | R (ops) | — | R (clinical) | R (boarding) | R (stock) | R (sales) | F (financial) | F |
| Staff & permissions | — | — | — | — | — | — | — | — | F |
| System configuration | — | — | — | — | — | — | — | — | F |
| Audit log | — | — | — | — | — | — | — | R | F |

### Appendix E — Default vaccination and preventive schedule *(administrator-editable; clinical sign-off required)*

| Item | Typical schedule | System behaviour |
|---|---|---|
| FVRCP (core) | From 6–8 weeks, every 3–4 weeks until 16 weeks; booster at 1 year; then every 1–3 years | Computes next-due date; drives reminders and boarding eligibility |
| Rabies | Per local law, commonly from 12 weeks; then annually or triennially | Same; may be legally mandatory for boarding |
| FeLV | Recommended for kittens and outdoor cats; test before vaccinating | Prompts FeLV/FIV test order before first dose |
| Deworming | Every 2 weeks from 2–12 weeks, then monthly to 6 months, then every 3 months | Generates due reminders |
| Flea/tick prevention | Monthly | Generates due reminders; can be linked to a product subscription |
| Annual health check | Yearly; twice yearly for cats over 10 | Generates reminders and suggests booking |

---

*End of document — PurrCare Platform SRS v1.0 (Draft).*
