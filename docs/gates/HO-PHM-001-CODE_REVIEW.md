GATE: CODE_REVIEW
HANDOVER: HO-PHM-001
COMMITS: master...0634368e (branch ho/PHM-001)
DATE: 2026-10-09
STATUS: PASS

Reviewed (read, not run): dispense/reverse/correction/seal handlers, EfRegisterChain, ControlledDrugRegisterSql and its configuration, the immutability
interceptor, StaffWitnessVerifier, DispensingConfiguration and the ix_dispensing_supply migration, UniqueConstraintExceptionFilter additions,
PharmacyControllerBase, PharmacyWorkCycle/Worker, verification query, and TC-HO-PHM-001 against HO-PHM-001 s15/s16. Test results taken from the sheet (122 PASS, U-G4 BLOCKED).

Verified sound:
- Transaction boundary: every IStockLedger.Deduct/ReceiveReturn/Adjust and IControlledDrugRegister.Enlist happens after all validation and before ONE repository save;
  a refusal after a partial enlist returns a violation and saves nothing (per-request scope; I-E9 covers it). The witness check, which saves a lockout/audit row on its own,
  always runs before the first tracked mutation (dispense, reverse, correction), so it cannot flush half a business change.
- Supply numbering: prescription xmin plus ix_dispensing_supply (unique, filtered to Dispensed, predicate written from the enumeration) gives 409 for the losing dispenser
  (ConcurrencyExceptionFilter / PharmacyMappings); a reversed supply frees its number. I-E7/I-E8 exercise 8-way and 2-way races.
- Seal race: unique (branch_id, chain_position) plus PK = entry id; 23505 is caught, the tracked seals detached, nothing written, retried on the next poll. Chain verification
  walks by chain_position, so a late-committing lower entry_number sealed after a higher one does not read as a break.
- Controllers: IInMemoryDispatcher only, no DbContext, 201/200/204/400/403/404/409 as specified. 23514 on ck_stock_position_balance maps to 409 via the existing Inventory entry.

FINDINGS
- [MEDIUM] src/Application/CatEcommerce.Application/CommandHandlers/Prescription/DispensingCommandHandlers.cs:134 — `lineId = groupLineIds[Math.Min(index++, groupLineIds.Count - 1)]` reuses the last line id when the ledger returns more movements than requested lines; two DispensingLine rows would then share a primary key and the save fails as a 500 after stock was enlisted. Not reachable today (explicit batch lines give one movement each) but the clamp hides the broken assumption. Use Guid.NewGuid() per movement, or assert movements.Count == group.Count().
- [MEDIUM] src/Infrastructure/CatEcommerce.Infrastructure/Pharmacy/EfRegisterChain.cs:99-104 and SealControlledDrugEntriesCommandHandler — the lost-race path (23505 on ix_cd_register_seal_chain or the seal PK, detach, report Sealed=0) has no test at any level; Application.Tests has no pharmacy tests and no integration test runs two seal passes concurrently or against a pre-seeded tip. The handover names seal-race safety as a deliverable. Add an integration test that runs RunSealPassAsync twice in parallel and asserts one chain, no gaps, intact verification.
- [MEDIUM] tests — U-G4 (gate messages carry no diagnosis, dose or vet notes) has no test. The gate is the diet-authorisation port other modules show to customers, so the privacy claim is unproved. Add a test that asserts the message set from DietAuthorisationGate against the clinical text of a seeded authorisation.
- [MEDIUM] tests — case-level substitutions: U-A3 (template rules) and U-D4 (interceptor) pass only at integration level; Application.Tests contains no handler tests for DispensePrescription, ReverseDispensing, RecordControlledDrugCorrection or Seal, so the branch-heavy handler logic (witness ordering, multi-SKU grouping, reverse window) is protected only by endpoint tests that need Postgres.
- [LOW] DispensingCommandHandlers.cs:142 — `prices.PriceAtAsync` runs once per movement inside the loop (N+1). Cache per SKU.
- [LOW] DispensingCommandHandlers.cs (Dispense, DispensingId) — a client retry with a DispensingId that already committed collides on the dispensing primary key, which is not in PharmacyMappings, so it surfaces as 500 rather than the idempotent 200/409 the sibling Billing payment handlers give. State is safe (rolled back); the answer is wrong.
- [LOW] ControlledDrugCommandHandlers.cs:27-100 — a correction may target another Correction entry and does not check the target medicine is still controlled; both are harmless to the chain but unspecified. Decide and test.
- [LOW] StaffWitnessVerifier.cs — wrong witness passwords count toward the witness account's lockout exactly like sign-in, so any staff member can lock a colleague out by naming them as witness (limited by the witness rate limit). Same trade-off as sign-in; flag for SECURITY.
- [LOW] PharmacyControllerBase.cs RespondToFailure — `response.ValidationResult!` is dereferenced for every failure; a failed response with only a message would throw. Guard it.
- [LOW] SealSettleSeconds = 30 (PrescriptionRules.cs:49) is a heuristic for slower-than-30-s transactions; late entries are still sealed correctly (later chain position) but the assumption is undocumented. Add a line to the handover s12.2 or a test.

SCOPE CHECK
- files in allowed scope: 189/189 (backend, tests, five migrations, docs/handover and docs/test-cases for this handover, .ai/decisions/adr.md append)
- files outside allowed scope: none found (the three-line additions to Billing/Clinic/Visit Application tests are fixture constructor changes forced by the new DbContext surface)
