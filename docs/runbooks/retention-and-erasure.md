# Retention and erasure runbook

HO-XCT-001 (US-XCT-06, ADR-298..305, ADR-314). Audience: an administrator who answers "what do we keep, for how long, and what happens when a customer asks us to delete them?"

## 1. The classification rules

Every table in the system carries a **record class** and a **hard-delete policy** (`RecordClassification` in `src/Shared/.../Constants`). The rules, in order:

| If the table holds... | Class | Policy | What it means |
|---|---|---|---|
| Clinical facts about a cat or a visit (cats, vaccinations, appointments, notes, consents) | Clinical | **Forbidden** | Never deleted by the application. Kept 7 years after the cat's last visit or death (D-8), then offered for **review**. |
| Money (prices, invoices, payments, ledgers) | Financial | **Forbidden** | Kept 6 years (D-9), then offered for review. |
| Evidence that a rule was followed (policy versions, acceptances, erasure ledger) | Regulatory | **Forbidden** | Kept; the one personal item inside (an IP address on an acceptance) is blanked when its period ends. |
| Who did what (audit trail, retention runs, restore tests) | Audit | **Forbidden** | Kept indefinitely. |
| Customer personal details (profile, addresses) | CustomerPersonal | AggregateOnly | Changed or cleared through the owning record, and anonymised on request. |
| Current state that is replaced as a whole (allergy list, opening hours) | CurrentState | AggregateOnly | The owning record rewrites it; history is in the audit trail. |
| Messages sent | Notification | **Forbidden** | The row stays; the **content** is blanked after 24 months. |
| Sessions and reset tokens | Security | **Purgeable** | Deleted after 30 days. |
| Anonymous performance samples | Telemetry | **Purgeable** | Deleted after 90 days. Backup-job log rows after 400 days. |

*Forbidden* is enforced three times: the application refuses to delete (409 "This record is kept for legal reasons"), the database refuses (a trigger on every such table, also for `TRUNCATE`), and a build-time scan fails if anyone writes a delete outside the one purger. A new table cannot be added without a class: the build fails until it has one.

**The end of a period is never an automatic deletion.** For clinical, financial, regulatory and audit records it produces a line in the review report ("Eligible for review. Nothing is deleted automatically."). Deleting one would need a new owner decision.

## 2. What the worker does

* **Every 15 minutes - the erasure pass.** Opens a ledger for any anonymised customer that has none (self-heal), then works every due step (section 4).
* **Once a day at the main branch's local `retention.sweepHour` (default 02:00), or when an administrator asks for a run** - the daily run, in this order: purge sessions and reset tokens; purge old web-vital samples; purge backup-job log rows; run each registered purge source that has a retention rule; blank the content of notifications older than the notification period; count records past their period for the review report. Each is a *pass* with counts and error codes, never record contents.
* A step that fails is counted and the next one still runs; the run then ends `CompletedWithErrors` and the administrator sees which pass and which code.
* One run at a time: asking for a second while one is waiting or running answers `409 RetentionRunAlreadyInProgress`.

## 3. Reading the pages

* **Retention schedule** (`/api/admin/retention/schedule`): each class, its policy, the period from the current settings, what happens at the end (`ReviewOnly`, `Purge`, `Redact`, `AnonymiseOnRequest`, `Keep`) and the tables. Changing a retention setting shows here immediately.
* **Runs** (`/api/admin/retention/runs`): the history; open one to see each pass (examined, affected, held, failed, error codes).
* **Review** (`/api/admin/retention/review`): counts of records past their period, per source, with the oldest anchor date. Use it to decide, with the owner, whether anything may be removed by a separate, deliberate process.

Settings (Administration -> Settings, group Retention): `retention.clinicalYears` (7), `retention.financialYears` (6), `retention.boardingMediaMonths` (12), `retention.notificationContentMonths` (24), `retention.securityArtefactDays` (30), `retention.webVitalsDays` (90), `retention.sweepHour` (2). Lowering a period is allowed; nothing protected is deleted because of it.

## 4. A customer asks to be deleted

**Self-service** (the customer deletes their account): their account is anonymised at once (name, phone, email become placeholders; sign-in stops; sessions end). That opens an **erasure ledger**: one line per part of the system that holds their personal data. The worker clears each part:

| Part | What is cleared | What may be held |
|---|---|---|
| account | address book, preferences | nothing |
| notification | message content, destination, request variables | nothing |
| administration | the IP address on policy acceptances | acceptance evidence is held until `retention.financialYears` after the acceptance (NFR-LEG-5), then the IP is blanked |
| files | (when the Files part is registered) the customer's uploads and file names | legal-hold files |

Clinical and financial records that mention the customer are **kept** (the owner shows as "Deleted customer") - the law requires it; the personal data inside them is held until its period ends. The ledger shows each part as `Pending`, `Held` (with the date it will be released and the basis), `Completed`, or `Failing` (retried after 15 minutes, 1 hour, 4 hours, then daily). The request is `Completed` when every part is. That completion is itself recorded in the audit trail: it is the proof that the request was honoured.

**On behalf of a customer** (phone, in person, email, letter): an administrator uses `POST /api/admin/customers/{id}/erasure` with the channel and **how identity was verified, chosen from a fixed list of codes**: `RegisteredMobileCallback`, `RegisteredEmailReply`, `InPersonPhotoId`, `WrittenRequestSigned`. There is no free-text field: the code is written to the immutable audit trail, and nothing the customer said or showed (ID numbers, dates of birth, phone numbers) may be typed there. The same anonymisation and ledger follow. Doing it twice is harmless. A customer record that was merged into another is refused: erase the surviving record.

**What "held" means for the customer-facing explanation:** "We have deleted your personal details. We must keep records of treatment and payments for the legal periods; in those records you appear only as 'Deleted customer'. A small amount of evidence (the internet address from which you accepted our terms) is kept for six years and then removed automatically."

## 5. After a restore from backup

Follow `docs/runbooks/backup-and-restore.md` sections 4 and 5: export the journal from the current database BEFORE restoring, start the application with its workers off, replay the journal, then turn the workers on.

## 6. Residuals (accepted, named)

* The audit trail may itself contain a personal value that was typed into a field before it was erased (OQ-5).
* The snapshot for front-desk staff includes the customer's primary mobile number (OQ-10).
* A deleted customer's data may remain in backups for up to 35 days (OQ-12); the journal re-erases it after a restore.
* Uploaded files in object storage are not in these backups (OQ-9).
* The purge sources registered by the file storage area (`stored-file:<purpose>`) have **no retention rule in this slice** and are deliberately not scheduled: several of those purposes are clinical. The only scheduled source rule is `boarding-media` (D-10), which belongs to the Boarding area (not built).
