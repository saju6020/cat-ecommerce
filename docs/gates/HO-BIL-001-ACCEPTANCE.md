GATE: ACCEPTANCE
HANDOVER: HO-BIL-001
COMMITS: 50f1ab1..d974c14b on ho/BIL-001 (26 commits: 0c1cff77 39ea24b4 8f260e79 d5ca029d 4c0327a0 712fe0ae 2be78554 260b54d1 bd47c032 b511d666 d5115e82 418ff81e 92cc46b6 3ac2e82a ffb70cc4 84a222b6 1d246fe2 0508deba 8fd5988d 7b044018 8fb004d8 a4ae560e 85dcf9d6 92e6420a ede9f7d8 d974c14b). This gate includes a DELTA REVIEW of ede9f7d8 and d974c14b, which no full gate had seen.
DATE: 2026-10-07
STATUS: PASS

Owner rule (2026-09-27): only BLOCKER and HIGH findings block. This gate found **no BLOCKER and no HIGH**. Phase J (the visit, dispense and surgery item sources) is DEFERRED by owner decision and is not failed.

## Prior gates (read from docs/gates/ and docs/test-cases/, not from commit messages)

- **ARCHITECTURE:** round 1 FAIL (0508deba, 2 HIGH). Both HIGHs are closed by REVIEW-R2.
- **SECURITY:** round 1 FAIL (1d246fe2, 1 HIGH). The HIGH is closed by REVIEW-R2.
- **CODE_REVIEW:** PASS (84a222b6).
- **REVIEW-R2:** combined follow-up of the round-1 fixes, PASS (85dcf9d6). It closes all three round-1 HIGHs, which is the combined-review form the gate-scope rule allows.
- **TEST:** PASS (92e6420a).
  - The sheet docs/test-cases/TC-HO-BIL-001.md exists: 158 rows, 0 NOT RUN, 0 BLOCKED, 4 DEFERRED (Phase J).
  - Its 7 FAIL rows were probes and unfixed earlier findings.
  - Its A26 row reads FAIL. The sheet predates ede9f7d8 and d974c14b and was not re-executed by the TEST gate. I re-ran the rows instead (below).

## What this gate ran (worktree D:/my-repo/cat_ecommerce-wt/BIL-001, HEAD d974c14b)

- **Build.** `dotnet build CatEcommerce.sln -p:NuGetAudit=false`: 0 warnings, 0 errors.
- **Unit tests.** All pass, with nothing skipped:
  - Domain 1013/1013. The TEST gate had 973 passing and 2 skipped; the XU-3 and XU-6 probes are no longer skipped.
  - Application 236/236.
  - Infrastructure 147/147.
- **Billing integration classes**, run with `--filter FullyQualifiedName~.Billing` on catecommerce_bil only. CATECOMMERCE_TEST_DB was set to the committed local-dev default with Database=catecommerce_bil, exported in the same command.
  - Result: 146 passed, 0 failed, 2 skipped.
  - The 2 skipped are XI-7 (TRUNCATE) and XI-15 (export branch scope). Both are backlog items accepted by the owner.
  - XI-8 is no longer skipped, and it passes.
- **Evidence cross-check.** All 166 test methods that the TC sheet cites as evidence were matched against today's TRX files. None is missing and every one passed.
  - The I-J1 and I-J2 test (CustomersSeeOnlyTheirOwnIssuedInvoices_NeverADraft_NeverSomeoneElses) passed. Its body asserts own 200, foreign 404, draft 404, the PDF, and unauthenticated 401.
- **Guard probe.** A scratch console compiled CardNumberGuard.cs on its own. It ran targeted cases, 40,000 fuzzed PANs with every separator and digit script, 252,000 Luhn-valid numbers across network prefixes, and the bank-statement shapes. Results are in the DELTA REVIEW below.
- **ADR check.** A script compared ADR-177..194 in `.ai/decisions/adr.md` with §9: **18/18 match word for word** in both Decision and Reason. ADR-195 and ADR-196 are left free, and each heading appears once.

## Criteria (§18)

Each criterion names its evidence, and every named test **passed in this gate's runs at d974c14b**.

- A1 MET — U_C1_FinancialYear, U_C2_DocumentNumberFormat, U_C3_SeriesTake_ReturnsOneTwoThree_AndAdvances, I_C2_FiveInvoicesIssuedInSequence_HaveContiguousNumbers.
- A2 MET — I_C3 (×3: 20 concurrent issues, bounded retry, collision retried), I_C8, XI_1 and XI_1b (two hosts on one database, contiguous 1..40, retries seen).
- A3 MET — U_A1, U_A3, I_C1_OneInvoice_CombinesClinicShopAndBoarding_WithTaxPerLine, I_C9 (PDF carries the number, the TRN and the lines).
- A4 MET (server half, per §4.1/§4.2) — U_B2_DefaultBusinessLinePerSource, I_C1 (one invoice holds Clinic, Shop and Boarding lines).
- A5 MET — I_B1 (stands in for U-B4, which has no isolated unit test; the sheet says so), U_B5, C4_..._U_B6, I_B2 (broken capture: request 204, source listed, recoverable), I_B5.
- A6 MET — U_D7, U_D9, I_D1, I_D9, I_D10.
- A7 MET — I_D10, XU_1 (bucket edges), XI_12 (HTTP edges at 0/1/30/31/60/61/90/91 days).
- A8 MET — I_D2_SeveralPaymentsOfDifferentMethods_ClearTheBalance.
- A9 MET — U_C5 (×5), I_A3 (×2, through Npgsql), XI_6 (every Billing table refuses DELETE on a real row).
- A10 MET — U_E1 (×2), U_E2, U_E3, I_E1, I_E2 (the corrected draft is linked both ways), XI_11.
- A11 MET — U_G6, I_G1 (the shortfall is listed; the unremitted collection remains).
- A12 MET — U_G1..U_G5, I_G2 (unmatched entries flagged both ways), I_G3, I_G4, I_G5.
- A13 MET — U_F1 (×2), I_F1 (hand-computed oracle), I_F2.
- A14 MET — U_D2 (×2), U_D12, I_D1, I_D11 (actor, IP, before and after).
- A15 MET — U_D4 (×2), U_D5, U_D6, I_D3 (×2), I_D4 (403 for Reception).
- A16 MET — U_D10 (×2), U_D11, I_D7, I_D8 (no edit route; 405), I_D11.
- A17 MET — U_F1, I_F1.
- A18 MET — U_F2 (×3), U_F3, I_F2.
- A19 MET — U_A1..U_A3, U_C6, I_C1.
- A20 MET — U_A8, I_C4.
- A21 MET — U_I6 (×2), I_I3.
- A22 MET — U_H4, I_H1_H2.
- A23 MET — U_H1, U_H2, I_H1_H2 (byte-identical ZIP on re-run; documentId on every row).
- A24 MET — U_I1 (×3), U_I2, I_I1.
- A25 MET — U_A4, U_A4b, U_I3 (×2), I_I2.
- A26 MET, with the trade-offs judged in the DELTA REVIEW.
  - The handover's own evidence passes: U_D3 (×12), U_G5 (×2), I_D6, I_X1.
  - So do these tests:
    - XU_2 and XU_5;
    - XU_3, which is now un-skipped and covers 14 separators;
    - XU_6, which is now un-skipped and covers 8 everyday numbers;
    - CardNumberFieldTests, whose AssertRefused checks the violation code and that the refused value is never echoed;
    - BillingCardNumberEndpointTests (every free-text money field, plus the audit trail);
    - XI_8 (un-skipped: the remaining free-text fields);
    - XI_5 (Trace-level logs);
    - XI_9, the whole-database sweep. It ran after XI_8 and found no card-like value in any text or JSON column of the billing schema or the audit and notification tables.
  - Fields left unguarded on purpose:
    - Bank account number: §11.4 C1 says the guard "does not apply to bank account numbers".
    - Tax registration number and routing number: these are legal identifiers. A 13-digit BIN would be refused about one time in ten.
- A27 MET (server half) — U_E4, U_E5, I_E3, I_E4. RefundRules_MethodReferenceAndCardNumber_AreEnforced also passes.
- A28 MET — U_D1, U_D1b, U_X4. A grep of the diff finds no online member, token, webhook, voucher, hosted service or gateway shape.
- A29 MET — U_X2, I_J3 (every endpoint against every role), CustomersSeeOnlyTheirOwnIssuedInvoices_... (I-J1 and I-J2), AnotherBranchsPayment_IsNotFound_AndCustomersSeeOnlyTheirOwnAcceptedPayments.

Deferred or not built, as §18 names them: the order-side flows (HO-ORD-001); deposit confirmation, cycle invoicing and the spending limit (HO-BRD-001); **Phase J** (DEFERRED: TC rows J-1..J-4); the portal dashboard; reports; gift vouchers (OQ-7); the scheduled export (OQ-19); the gateway (Phase 5). None of them is built.

## Other §19 duties for this gate

- **§9.1 open-question defaults.** They are recorded in SettingDefinitions.Billing.cs, and all nine keys match §10.2 in type, default, bounds and branch override:
  - FY start 7 (OQ-2);
  - terms 0 days (OQ-6);
  - approval threshold 50000.00 (OQ-5);
  - loyalty off, at 100.00 per point, point value 1.00, minimum 100 (OQ-8).
  - OQ-7, OQ-13, OQ-19 and OQ-20 are not built, as their defaults say.
- **§8.13 published contracts.** IBillableChargeRecorder, ChargeRequestDto, ChargeSourceKey and the selector form of IssueInvoiceFromChargesCommand are present. So are RecordPaymentCommand, IssueCreditNoteCommand, IssueRefundCommand, the six integration events and the three published queries.
  - Two additions keep the published shape: InvoiceIssuedEvent carries an extra `Total`, and `BranchId` on the command is nullable.
- **§8.13 assumed contracts (Phase J).** These are left exactly as specified:
  - There is no VisitFinalisedEvent, PrescriptionDispensedEvent or SurgeryCompletedEvent handler.
  - There is no Visit, Dispense or Surgery item source.
  - BillingModule.cs:51 marks the seam.
  - The handover file is unchanged on the branch.
  - Note: HO-EMR-001 has since merged to master. When Phase J is picked up, §20.2 requires its real event to be compared with the assumed `VisitFinalisedEvent` row before anything is built.
- **C-ALL duties.**
  - C-ALL-1: all 21 billing tables (from every `ToTable`) are classified in RecordClassification.Billing.cs, which is aggregated by reflection.
  - C-ALL-2: BillingPersonalDataEraser is registered (BillingModule.cs:48). It reports Held under D-9 and mutates nothing.
  - C-ALL-3: BillingRetentionReviewSource is registered (BillingModule.cs:49). It is a review count only.
  - C-ALL-4: no aggregate method removes a Forbidden record. Only the draft lines, the requested-allocation working set and the profile's bank accounts are replaced, and those are AggregateOnly or drafts. ARCHITECTURE verified this, and nothing in R2 or the delta changes it.
  - C-ALL-5: not applicable. Billing is not clinical and writes no audit rows about clinical records.

## DELTA REVIEW — ede9f7d8, d974c14b (CardNumberGuard and the remaining free-text fields)

Verified:
- **Separators.** Every case in the un-skipped XU-3 is now refused: `, : ; | * ( ) [ ]`, en and em dash, the non-breaking hyphen, the minus sign, zero-width space and joiner, and the soft hyphen.
  - In the fuzz, 40,000 network-prefixed PANs were written with 23 separators, mixed ASCII, Bengali, full-width and supplementary-plane digits, and random surrounding text: **0 missed by LooksLikeCardNumber**.
- **Index mapping with filler chars holds.**
  - Normalise writes one char out for each char in, and a supplementary digit becomes the digit plus a filler.
  - The regex `[0-9]\uFFFF?` keeps the filler inside its group. `Digits()` drops it. A window start that lands on a filler is skipped, and a window end includes the trailing filler.
  - Across the fuzz and 12 targeted cases (an emoji before a card, two cards in one string, a supplementary digit at the start, middle and end, full-width letters around full-width digits), **no surrogate pair was ever split** and the text around a masked stretch was preserved exactly.
  - XU_3_ASupplementaryPlaneDigitCardNumber_IsRefused_AndMaskedInPlace passes.
- **Mobile exemption (`^8801[3-9]\d{8}$`).**
  - It applies only to a window of exactly 13 digits. A longer card window is tried first (longest first), so a card next to a mobile is still caught. The probe confirmed this, and so does `8801712345605 4111...`.
  - Of 252,000 Luhn-valid numbers of 13 to 19 digits across 18 prefixes, the only ones let through were 1,537 13-digit numbers starting 8801[3-9]. No card network issues 13-digit PANs in that range; 13-digit PANs are legacy Visa, which start with 4.
  - **Not a realistic bypass.**
- **Own-document exemption.**
  - It requires the literal `INV-` or `CN-`, a branch code, consecutive financial-year digits, and `\b`-bounded 6-digit sequences. That is at most 14 digits, so it cannot hide a PAN of 15 to 19 digits.
  - It is matched on the original text and blanks only that span.
  - `INV-A422-2222-222222` is still refused (XU_6_ADocumentNumberShapedString_CannotHideACardNumber).
  - Hiding a card behind it takes deliberate construction (for example `INV-A4111-1112-345678 90`). That is the same class as the letter trade-off. **Not a realistic bypass.**
- **Letters as non-joiners.** `4111x1111x1111x1111` and `4111 a 1111 b ...` are not refused. This is declared, and pinned by XU_3_ALetterBetweenGroups_DeliberatelyDoesNotJoinThem.
  - The probe also found `+ ' # " = & ~ { }` between groups are not joined.
  - All of these are deliberate evasion by a person who already knows the guard; nobody types them by accident. Joining letters would refuse ordinary mixed references.
  - **Accepted trade-off. LOW.**
- **New field coverage (d974c14b).**
  - Refused: the profile's legal name, address lines, phone, email, footer, bank name, account name and branch name (CardNumberFieldTests ×8); ReportingTag; BankAccountLabel; the courier code and name on a COD payment.
  - Masked: SourceFileName (`••••1111.csv`, tested).
  - Left unguarded on purpose: TRN, account number and routing number (see A26).
- **Declared but not yet in §1 Amended:**
  - The two exemptions narrow §8.8.2's literal rule ("a run of 13-19 digits passing Luhn → true").
  - The XU-3 letter case was moved from "must refuse" to "must not refuse".
  - SourceFileName is masked rather than refused.
  - The integrator should record all three in §1 Amended together with the other deviations.

**Is A26 met with these trade-offs? Yes.**
- Every free-text field a person types into a money record now refuses a card number in every accidental form tested.
- The only unrefused forms are deliberate obfuscations.
- The stored data, the logs and the audit rows were swept clean (XI-5, XI-9).

**None of the delta, and none of the residuals below, is BLOCKER or HIGH.**

FINDINGS (none blocking; for docs/backlog/BIL.md)
- [MEDIUM] src/Shared/CatEcommerce.Shared/Common/Helpers/CardNumberGuard.cs:128-161 (CardWindows, used by MaskCardNumbers) — the mask can hide the wrong digits when another digit group precedes the PAN in the same run.
  - **Cause.** Windows are taken leftmost and longest first. A preceding reference or date joined to the PAN's first groups often forms a 13-19-digit window that passes Luhn by chance. That window is masked, and the PAN's remaining groups are then too short to form a window of their own.
  - **Measured.** With 20,000 cases per shape, about 29% of `TRF <8 digits> <PAN>`, `POS <ref> <PAN> DHAKA` and `REF:<date>/<PAN>` inputs keep up to **13 of the 16 PAN digits** in clear. Example: `TRF 37085610 4003 3371 5286 9561` becomes `TRF 3••••3371 5286 9561`.
  - The delta's wider separator set makes such joins more frequent: `bKash 8801712345605; <PAN>` now joins across `;`.
  - **Scope.** Only the mask path is affected: bank statement Reference, Description and BankTransactionId on import, and SourceFileName. The refusal path is unaffected, since detection is boolean (0 misses in the fuzz).
  - **Why not HIGH.**
    - The input is a bank's own export, and a full PAN in it is itself a PCI violation by the bank.
    - The leak also needs a digit group right before the PAN.
    - XI-9 found nothing in any stored data.
    - U-G5 as the handover specifies it (a plain 16-digit run) holds.
  - **Fix.** Mask every digit of every group that takes part in any card-like window of the run, keeping only the last four, instead of taking only the first window found. Add the probe's `TRF <ref> <PAN>` shape to XU-5.
- [LOW] CardNumberGuard.cs:144 — the "one stray digit glued to the front" offset is `+1`. When the first digit is a supplementary-plane digit, `+1` lands on its filler and the start is skipped, so `𝟗𝟒111111111111111` is neither refused nor masked. The ASCII form `94111…` is caught. This needs mathematical-alphanumeric digits, so it is deliberate only. Fix: step past the filler.
- [LOW] CardNumberGuard.cs (pre-existing, not the delta) — a PAN glued to two or more digits in one unbroken run is not seen. Examples: `774111111111111111`, and `41111111111111111227` (a PAN with the expiry glued on). `4111… 1227` and `card 4111… exp 12/27` are refused.
- [LOW] src/Domain/CatEcommerce.Domain/Aggregates/Billing/CodRemittanceAggregate.cs:42 — the remittance `CourierCode` (≤ 30 characters, set from the HTTP body by StaffReconciliationController) is not guarded. d974c14b guarded the same field on the payment's collector. XI-8 does not cover it.
- [LOW] tests/CatEcommerce.IntegrationTests/Endpoints/BillingTestGateEndpointTests.cs XI_8 — every non-204/201 response counts as "refused" without checking the error code. A 400 for an unrelated reason would pass vacuously. The domain CardNumberFieldTests do check the code, so coverage holds. Assert `ReferenceLooksLikeCardNumber` here too.
- [LOW] docs/test-cases/TC-HO-BIL-001.md predates ede9f7d8 and d974c14b, so XU-3, XU-6, XI-8 and the A26 coverage row still read FAIL. This gate's re-execution supersedes them (see "What this gate ran"). Update the sheet at merge if it is kept as the record.
- [LOW] An untracked `bil-test-scratch/` (left by the TEST gate) sits in the worktree. It is not committed; delete it before merge.
- Carried and accepted by the owner, not re-raised:
  - XI-15 export branch scoping, and same-person record-and-refund (pending owner decision);
  - XI-7 TRUNCATE;
  - F-6, the idempotency loser getting 400;
  - the loyalty global-flag test coupling;
  - the U-X6 blind spots;
  - the 18 deviations declared to ARCHITECTURE;
  - the 13 files outside the literal §13 list.

SCOPE CHECK
- files in allowed scope: 263/276 changed files (50f1ab1..d974c14b). The 6 files of the delta are all inside §13: CardNumberGuard.cs, Aggregates/Billing/**, tests/CatEcommerce.Domain.Tests/** and tests/CatEcommerce.IntegrationTests/**. The 33 R2 files are also in scope (REVIEW-R2).
- files outside allowed scope: the same 13 files ARCHITECTURE listed, and no others:
  - StaffBillingControllerBase.cs, StaffCashUpController.cs, StaffLoyaltyController.cs, StaffReconciliationController.cs;
  - BillingIdempotencyReplayFilter.cs;
  - CodShortfall.cs, IBankMatchRecorder.cs, IBillingFinder.cs, ILoyaltyAccountStore.cs, LoyaltyCalculator.cs;
  - three tests/CatEcommerce.Infrastructure.Tests files.
  - All are in the Billing area, and none is in §14 forbidden scope. The owner accepted them, and the integrator records them in §1 Amended at merge.
- §4 out-of-scope work: none. There is no gateway, online member, token or card column, webhook, voucher, scheduled export, background worker, second DbContext or NuGet change. Phase J is not built.
- §20 conditions:
  - None was hit and worked around.
  - §20.6 (a card number found anywhere): XI-9 found none.
  - §20.9 (QuestPDF): the Community licence is already set by CatProfilesModule (SECURITY).

UNDECLARED DEVIATIONS
- None found in the delta. The exemptions, the letter trade-off, the SourceFileName mask and the unguarded TRN, account and routing numbers are all declared in the commits and the brief. They are still missing from §1 Amended, which the integrator owns.
