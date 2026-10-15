GATE: CODE_REVIEW
HANDOVER: HO-SRG-001
COMMITS: a8761829..75ae3bda (head 75ae3bda, branch ho/SRG-001)
DATE: 2026-10-11
STATUS: PASS

Reviewed: status machine and transition table (one table shared by aggregate and read side), booking / reschedule / cancel
(single SurgerySlotClaim sequence, every clash named, 23P01 mapped to 409 by route, staff-time exclusion constraint for the
assistant), checklist / ASA / admit / start (consent re-read from the records, override vet-checked and stored), anaesthesia
log (drug + batch deduction + witnessed register enlisted and committed in one save; witness verified before any write;
corrections append-only, linked, new witness, DB immutability trigger), end / report / addenda, neuter status handler and
repair command (shared helper, idempotent, deceased cat audited), recovery board (two SQL commands, test I_F2), discharge and
follow-up reminders (stamp / status guards), campaigns (price lock, cap count excluding cancelled, booking-time snapshot),
templates and guards, time zones (TimeProvider and branch-local dates throughout, no DateTime.UtcNow), money (Money value
object, numeric(12,2)), AsNoTracking and projections on reads, controllers on IInMemoryDispatcher, DI registrations (every
Surgery handler registered in SurgeryModule). Domain.Tests Surgery filter: 96/96 pass. TC sheet spot-check (U-A1, U-C4, U-D8,
U-D9, I-F2, I-X1, I-A1): rows map to real asserting tests; U-D5 is honestly marked integration-only.

FINDINGS
- [MEDIUM] src/Infrastructure/.../Migrations/20261010182212_AddAnaesthesiaLog.cs (ix_anaesthesia_log_entry_corrects) — the one-correction-per-entry rule is an ExistAsync pre-check; the index is not unique, so concurrent corrections fork the chain and double-return stock / double-write register Corrections. Backlog SRG 16.
- [MEDIUM] src/Application/.../Surgery/AnaesthesiaCommandHandlers.cs LogAnaesthesiaDrugCommandHandler — server-minted EntryId means a retry or double-click writes a second entry, deduction and register entry. Backlog SRG 17.
- [LOW] AnaesthesiaCommandHandlers.cs — aggregate call runs after ports are enlisted; comment says the opposite; "all three or none" rests on nothing else saving in scope. Backlog SRG 18.
- [LOW] SurgeryRetentionReviewSource — UTC date for retention anchors. Backlog SRG 19.
- [LOW] SurgeryCampaignQueryHandlers (cases report) — missing standard price counted as zero in the comparison total. Backlog SRG 20.
- [LOW] NeuterStatusOutcome / SurgeryBusyKind are C# enums; U-D5 and U-D9 mapping thin. Backlog SRG 21.

No BLOCKER or HIGH findings. Pre-existing owner-visible limits (cap race, consent kinds, boarding) are already in backlog SRG 1-15.

SCOPE CHECK
- files in allowed scope: 170/170 (SRG-owned plus the recorded shared touch points: Clinic appointment aggregate/handlers/queries, UniqueConstraintExceptionFilter, adr.md)
- files outside allowed scope: none

---

Re-check (fix round 8719c96a, 2026-10-11): the code-quality delta was reviewed inside the combined ARCHITECTURE re-check. See
`HO-SRG-001-ARCHITECTURE.md` § "Re-check (fix round 8719c96a)".
- Rows 16 (unique correction index), 17 (drug-entry idempotency) and 18 (`DryRun` before the ports) are closed.
- New items, all non-blocking:
  - the `DuplicateAnaesthesiaEntry` message is wrong for the PK race (MEDIUM, backlog 41);
  - a replay does not compare the payload (LOW, 42);
  - corrections return a 400 when sequential and a 409 when raced (LOW, 43).
The overall re-check is FAIL on a scope BLOCKER, not on code quality.
