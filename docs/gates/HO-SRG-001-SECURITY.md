GATE: SECURITY
HANDOVER: HO-SRG-001
COMMITS: a8761829..75ae3bda (ho/SRG-001; head 75ae3bda — 20aeeed5, 7a232f04, 75ae3bda and the earlier SRG build commits in that range)
DATE: 2026-10-11
STATUS: PASS

No BLOCKER or HIGH findings. MEDIUM and LOW findings are in docs/backlog/SRG.md, rows 22-27 (prefixed "SECURITY:").

FINDINGS
- [MEDIUM] src/Shared/CatEcommerce.Shared/Common/Constants/ActionRoles.Surgery.cs:12 — `Surgery.View` (which includes Reception) guards GET anaesthesia-log (StaffSurgeryAnaesthesiaController.Get) and GET report (StaffSurgeryReportsController.Get). The anaesthesia log carries controlled-drug name, batch, quantities drawn/wasted, administering vet and **witness identity**; the report carries findings and complications. Elsewhere in the app Reception has neither: `Visits.View` (clinical notes) excludes Reception and `ControlledDrugs.View` (the register) excludes Reception, per Appendix D. Exploit: a receptionist, or anyone using a reception login on a shared front-desk machine, lists the branch's surgeries and pulls controlled-drug and witness details plus full surgical findings that EMR and PHM refuse them. Handover §12.3 grants this explicitly ("log (read), report (read)"), so it does not block. Owner to confirm. Fix: add `Surgery.ViewClinical` (Nurse, Vet, Administrator) for the log, the report and the clinical sections of the detail. Backlog SRG 22.
- [MEDIUM] (concur, already backlog SRG 16) ix_anaesthesia_log_entry_corrects is not unique — two simultaneous corrections of one controlled-drug entry write two register Correction rows and two stock movements. Integrity of the controlled register; needs two vets plus witnesses racing. No new row.
- [LOW] src/Api/CatEcommerce.Api/Controllers/StaffSurgeriesController.cs Discharge / Reschedule — both return the full `SurgeryDetailViewModel` (ASA grade, consent-override reason, checklist notes, recovery notes) without `TryLogClinicalAccessAsync`, so a clinical read goes unlogged. Handover §12.3 says every staff endpoint that reads the detail is logged (NFR-SEC-8). Fix: log the access (fail closed) or return only `{ id, version }`. Backlog SRG 23.
- [LOW] src/Infrastructure/CatEcommerce.Infrastructure/Surgery/SurgeryPorts.cs:46 + FlagSurgeryOnCatTransferredHandler — only `Scheduled && StartsOn > now` surgeries move to the new owner. A surgery that is PreOp/InTheatre/Recovery at transfer, or Scheduled with a passed start, stays on the PREVIOUS owner's /api/customers/me/surgeries, who then sees post-op instructions and follow-ups after discharge, while the new owner never sees it. Narrow edge, but it is cross-owner disclosure. Fix: move every non-terminal surgery, or have the owner confirm the current behaviour. Backlog SRG 24.
- [LOW] src/Application/CatEcommerce.Application/CommandHandlers/Surgery/AnaesthesiaCommandHandlers.cs:73 — for a controlled drug, `AdministeredById` only has to hold the Vet role. It is not checked for being active, able to sign in or assigned to the branch, and that vet never authenticates. The calling vet can attribute an administration (and its wastage) to a colleague who was not there. Mitigations: the register stamp and the audit row record the real caller, and the witness check works. Fix: require administering vet == caller, or an active vet assigned to the surgery's branch. Backlog SRG 25.
- [LOW] src/Application/CatEcommerce.Application/CommandHandlers/Validators/SurgeryRecoveryCommandValidators.cs (Discharge PostOpInstructions) and SurgeryCampaignCommandValidators.cs (Description) — no `NoControlCharacters()`, although the protocol path validates the same post-op text with it. Discharge text is shown in the customer portal. Fix: apply the same rule. Backlog SRG 26.
- [LOW] src/Application/CatEcommerce.Application/CommandHandlers/Surgery/SurgeryProcedureCommandHandlers.cs EndSurgicalProcedure / ApplySterilisationOutcome — Vet-only, but enforced by `[RequireAction]` alone. Every other Vet-only action (drug, ASA, waiver, override, report) re-checks `IsVetAsync` in the handler. Not exploitable today. Fix: add the re-check for consistency. Backlog SRG 27.
- [LOW] (concur, already backlog SRG 7) The campaign `MaxCases` cap is a count at booking. Overbooking risk is rated LOW: only staff with `Surgery.Schedule` can book, an overrun needs bookings that race for the last place, and the impact is financial only (one or a few discounted cases). No new row.

VERIFIED (no finding)
- Role mapping matches ADR-173 / handover §12.3. Nurses: vitals, milestones and their corrections. A drug correction is refused for a non-vet BEFORE witness verification. The Administrator has no clinical write. Campaign writes are Administrator only. Override is Vet only at the endpoint, in the handler and in the aggregate. The plain `/start` never reads a body, so it cannot carry an override.
- Branch scoping: every by-id command goes through `LoadSurgeryAsync(id, branchId)` and every by-id read filters on `BranchId == active branch`, so another branch gets the same 404. Theatre, surgeon and assistant are resolved only from the active branch's inputs. The campaign case list is branch-scoped. `excludeSurgeryId` is honoured only within the branch. `GetCatSurgeriesQuery` and `GetSurgeryChargeableItemsQuery` are unscoped but have no HTTP endpoint (internal contracts).
- Customer endpoints: `[RequireCustomerSession]`, and `CustomerId == caller` is in the query predicate, so another customer's surgery is 404. No staff-only data is exposed: no ASA, anaesthesia, witness, report, recovery notes or checklist notes. Post-op instructions only after Discharged. `Cache-Control: no-store`.
- Witness: reuses HO-PHM-001's `StaffWitnessVerifier` unchanged — Argon2id verify with a dummy verify on every failure path (no timing or enumeration oracle), one generic failure, failed attempts count toward the witness account's lockout like a sign-in, a masked identifier in the audit row, and witness ≠ caller. Witness ≠ administering vet is checked in the handler. The PHM witness rate-limit policy covers the drug and correction routes. `WitnessPassword` is absent from both commands' `ToString` overrides, from events (ids only), from audit rows and from logs. No body logging was found.
- Consent override: the reason (10 to 1000 characters, no control characters) is stored on the aggregate and written as a `BusinessEvent` audit row with the vet as actor. Consent is re-read live from consent records at start.
- Immutability: anaesthesia log rows have UPDATE/DELETE/TRUNCATE triggers. A finalised report and its addenda are trigger-protected, and adding an addendum does not touch the report row (no version bump), so the trigger is never tripped. There are no PUT/PATCH/DELETE routes. Erasure exempts surgery records. The retention source is read-only. P0001 "immutable:" maps to 409; any other P0001 is rethrown.
- Booking races: the appointment's performer, room and cat exclusion constraints plus the new `ex_appointment_staff_time` (assistant) settle races, and 23P01 maps to 409. The surgery aggregate uses an xmin concurrency token.
- CSRF: the global `SessionAuthenticationMiddleware` check applies to every SRG POST/PUT, and no SRG action is `[CsrfExempt]`.
- Clinical-access logging: GET detail, GET log and GET report write an EMR `RecordClinicalRecordAccessCommand` and fail closed (500, nothing released). Exception: finding 3 above.
- Notifications carry booking logistics and fasting instructions (allowed by handover §8.12). No ASA, report or recovery text, and events carry ids only.
- Input validation: lengths and `NoControlCharacters` on all free text except finding 6. Enum membership is validated. Dose and quantity bounds are set. The witness identifier and password are length-capped.

SCOPE CHECK
- files in allowed scope: 170/170
- files outside allowed scope: none (the only `.ai/**` file touched is `.ai/decisions/adr.md`, which handover §14 permits)

---

Re-check (fix round 8719c96a, 2026-10-11): the security delta was reviewed inside the combined ARCHITECTURE re-check. See
`HO-SRG-001-ARCHITECTURE.md` § "Re-check (fix round 8719c96a)". No security BLOCKER or HIGH was found.
- Client `entryId` idempotency: replay only for the same vet, same surgery and a Drug entry. The generic 409 carries no data.
- Discharge and Reschedule read-backs now log clinical access and fail closed. Backlog 23 is closed.
- `NoControlCharacters` closes 26, and the handler `IsVetAsync` closes 27.
The overall re-check is FAIL on a scope BLOCKER (`ConsentRules.cs`, an HO-EMR-001 file), not on security.
