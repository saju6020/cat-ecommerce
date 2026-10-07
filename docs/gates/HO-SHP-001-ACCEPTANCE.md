GATE: ACCEPTANCE
HANDOVER: HO-SHP-001
COMMITS: branch ho/SHP-001, HEAD 44fef7d, `git diff master...HEAD`: f2e871b 2f774f5 16bc0b4 82f82e3 bd6271e 1c255ec(merge) c7bc6ee 9f2ae08 85691ab 07c0408 f24cdc1 1375c3a(merge) c0756c7 22d0889 97b3514 0d5d09d 48588a5 8eec49c a81be7a 64b550a ff04d57 f6a024a 5de95f9 7cafa22 abe212a 8fe100b 4bbd6c3 6e061b9 fbdc691 44fef7d. This pass reviewed the delta fbdc691..44fef7d.
DATE: 2026-10-04
STATUS: PASS

SECOND PASS. This record replaces the first-pass record that 44fef7d committed. The first pass was
BLOCKED on one HIGH, a handover defect: §18 A4 (the displayed-stock clause), A5, A17 and A18 had no
evidence and no recorded deferral. Their cited tests need HO-IMP-001, and HO-IMP-001 cannot start until
HO-SHP-001 is merged.

That HIGH is RESOLVED. 44fef7d records the deferral, with every element the first pass asked for. All
37 criteria are now MET or met by deferral. Four of them (A4's clause, A5, A17's display half and A18)
are **met by deferral and not evidenced**. They stay owed under backlog row 51.

Owner rule (2026-09-27): only BLOCKER/HIGH findings fail a gate. MEDIUM/LOW items are for
docs/backlog/SHP.md, which this gate has not edited.

DELTA SINCE THE FIRST PASS (`git diff --stat fbdc691 44fef7d`)
- Three files changed:
  - `docs/handover/HO-SHP-001.md`: 5 lines (+5/−5). Line 19 gains the new §1 Amended entry, and the §18 A4, A5, A17 and A18 evidence cells change.
  - `docs/backlog/SHP.md`: +1 line, row 51.
  - `docs/gates/HO-SHP-001-ACCEPTANCE.md`: the first-pass record.
- No `src/`, `tests/` or migration file changed. No code changed, so no suite was re-run. The first pass's runs at the same code still stand:
  - Domain 683/683, Application 134/134, Infrastructure 107/107;
  - Shop/Product integration 130/130;
  - build 0 warnings, 0 errors.

THE DEFERRAL (HO-SHP-001 §1 Amended, "2026-10-04 — Deferral of stock-dependent evidence to HO-IMP-001, and two late declarations"). Checked against the first pass's UNBLOCK list:
- **Criteria:** A4 (the displayed-stock clause only), A5, A17 (the display half) and A18.
- **Why they cannot be evidenced:** the cited evidence needs `v_sellable_stock` / `IStockLedger`, and HO-IMP-001 requires HO-SHP-001 to be merged first.
- **State until the trigger:** fail-closed. Every SKU is OutOfStock and not purchasable, and `expiryNotice` is absent. Cited to `ShopFailClosedAvailabilityEndpointTests`, which the first pass confirmed passes.
- **Owner:** the project owner.
- **Trigger:** HO-IMP-001 merged to master.
- **Who does the swap:** an HO-SHP-001 follow-up slice that the integrator schedules on the trigger. The entry gives the reason: HO-IMP-001 §14 bars that slice from Shop files.
- **Steps:** the entry lists all five:
  - the view's `in_stock` expression and `ApproximateSellableQuantity` replaced by the `v_sellable_stock` join;
  - `next_allocatable_expiry_on` populated, and `ExpiryNotice` wired into the detail (the entry names `GetProductDetailQueryHandler.cs:70`);
  - a staff `stock` field;
  - the flag and its guards deleted;
  - U-C4, I-C1…I-C6, I-F5 as written and I-G3 run on Inventory's fixtures.
- **Re-judging:** by that slice's ACCEPTANCE.
- **Tracking:** backlog row 51, which exists and matches.
- **§18 cells:** A4, A5, A17 and A18 each point at the deferral and name the tests owed.
- **Standing:** the entry is recorded by the integrator under owner delegation and is pending owner confirmation, the same standing as decisions (1)-(3). This gate accepts it on that basis; see the LOW finding below.

CRITERIA (§18, as amended 44fef7d)

| # | Verdict | Evidence |
|---|---|---|
| A1 | MET | U-A1, U-A5, U-A5b, I-A3, I-A6; tags U-A10/U-A13; description I-H4/I-H5. The null image URL is now declared (§1 Amended (2); backlog row 2). |
| A2 | MET | U-A6; I-A5 `Detail_DraftProduct_ReturnsNotFound`. Archived/unlisted is shown by source: `GetProductDetailQueryHandler.cs:30`, and the view's `WHERE` at `AddBundlesAndRelated.cs:121`. |
| A3 | MET | U-A2; I-A4 `Categories_ListsAllSixWithSubcategories`. |
| A4 | MET; displayed-stock clause MET BY DEFERRAL (not evidenced) | Code, barcode, price and images: U-A3, U-A4, U-A11, I-A6, I-A7, I-A8. The displayed-stock clause is deferred to the HO-IMP-001 trigger; I-C1 is owed (backlog 51). |
| A5 | MET BY DEFERRAL (not evidenced) | No test exists. The deferral names U-C4 and I-C1 as owed. Until the trigger, production is fail-closed: every SKU is out of stock, so "other variants remain purchasable" does not hold in production yet, by §4.3's design. |
| A6 | MET | U-B2 (domain and HTTP); I-B1. |
| A7 | MET | U-B5; I-B2. |
| A8 | MET | U-B3, U-B4; I-B3. |
| A9 | MET | U-E3; I-E2 `I_E2_EveryFilter_AloneAndCombined_…`; R9. |
| A10 | MET (server half; URL persistence is HO-SHP-002's) | U-E2; I-E3 `I_E3_EverySort_…`. |
| A11 | MET | I-E11: p95 267.0/266.4/277.7 ms (TEST) and 233.9 ms (CODE_REVIEW); budget 500 ms. |
| A12 | MET | I-E4 `ShopByCountry_ListsOriginWithNotesAndProductCount`. |
| A13 | MET | I-E5: list half and filters half. |
| A14 | MET | U-E1; I-E6. Product-name typo: the first pass's SQL gave `word_similarity` 0.6875 ≥ 0.45. |
| A15 | MET | I-E7 (ingredient); U-A10 (tag in SearchText). The first pass's SQL gave `word_similarity('grain free', …grain-free…)` = 1. |
| A16 | MET | I-E8 `Search_NoResults_ReturnsSuggestions` (unconditional). |
| A17 | Fail-closed half MET; display half MET BY DEFERRAL (not evidenced). The computation is HO-IMP-001's (US-IMP-08) | "Never reported → OutOfStock" is evidenced by `ShopFailClosedAvailabilityEndpointTests` and by the pure rule U-C2. "Exactly Inventory's sellable figure" is deferred; I-C4 (staff `stock`) and I-C6 are owed. |
| A18 | MET BY DEFERRAL (not evidenced) | U-C3 proves the pure window rule only. The display is not wired: `GetProductDetailQueryHandler.cs:70` passes `null`, as the deferral states. I-C3 is owed. |
| A19 | MET | U-F4, U-F5; I-F7. |
| A20 | MET, against the controllable reader until HO-ORD-001 | I-D1 `Submit_WithPurchase_PendingModeration_NotYetPublic`. |
| A21 | MET | I-D2; I-D6 (shipped stub). |
| A22 | MET | U-D2; I-D1, I-D3, I-D4. |
| A23 | MET (the consultation link is HO-SHP-002's) | U-F1; I-F1; I-F8, which asserts `PrescriptionAuthorisationRequired`. |
| A24 | MET, contract half; recording the cat on the order line is HO-ORD-001's | I-F2 (two tests). |
| A25 | MET, contract half; blocking the checkout line is HO-ORD-001's | I-F3 `I_F3_EligibilityContract_…ExpiredTheDayAfterValidTo`. |
| A26 | MET | U-H1, U-H3, U-H5; I-H1 (four tests). |
| A27 | MET, under recorded decision (3), pending owner confirmation | U-H2, U-H2a-d; I-H2; `ShopCodeReviewFixesEndpointTests`. |
| A28 | MET, contract half; deducting each component is HO-ORD-001 / HO-IMP-001's | I-F4. |
| A29 | MET (Shop half) | U-F2; I-F5s, which runs with the Testing flag on; source routing through `EvaluateBundle`. I-F5 as written is owed under backlog 51. |
| A30 | MET, with the consent and provider caveats recorded | U-G2, U-G3; I-G2, I-G3, I-G4. Notification is limited to customers whose consent permits a channel (F33/OQ-2), and is in-app only until a provider exists (HO-NTF-001 OQ-1). "Stock exists" is the Testing-only signal; I-G3 must be re-run on real stock (backlog 51). |
| A31 | MET | I-H4 (two tests). |
| A32 | MET | U-H6; I-H5. |
| A33 | MET | U-H7; I-H4, I-H6. |
| A34 | MET | I-E9. |
| A35 | MET | I-G6, I-G7. |
| A36 | MET | U-F1; I-F8; `ShopBundlePrescriptionEndpointTests` (decision 2). |
| A37 | MET | I-A2; U-X1; I-X1, I-X2. |

On the flag-on evidence for A4, A29 and A30: the first pass's reasoning is unchanged.
- There is one availability seam.
- The behaviour under test is independent of where "available" comes from.
- The production fail-closed default is proven separately.

The deferral also adds the I-F5 and I-G3 re-runs to the work owed on the trigger.

FINDINGS
- First-pass HIGH (A4/A5/A17/A18 not evaluable and no deferral): **RESOLVED** by the §1 Amended deferral and backlog row 51 (44fef7d).
- No BLOCKER or HIGH findings.
- [LOW] docs/handover/HO-SHP-001.md §1 Amended (2026-10-04 deferral): the deferral, like decisions (1)-(3), was recorded by the integrator under owner delegation and is "pending owner confirmation". Four criteria (A4's clause, A5, A17's display half, A18) and the fail-closed default rest on it. The owner's final check should confirm it explicitly. If the owner reverses it, those four criteria are reopened.
- [LOW] docs/handover/HO-SHP-001.md §18 A17 evidence cell: it cites "U-C2 (fail-closed half)". U-C2 is the pure rule; the fail-closed display half is actually evidenced by `ShopFailClosedAvailabilityEndpointTests`, which the §1 entry cites but the cell does not.
- [LOW] docs/test-cases/TC-HO-SHP-001.md "Acceptance-criteria coverage": the A4, A5, A17 and A18 rows still show BLOCKED and do not cite the deferral, so the sheet and §18 now disagree. (Carried from the first pass.)
- Carried, not re-raised: backlog rows 2-8, 10, 12-16, 18-24, 29-42, 44-51. Rows 2 and 3 are now declared, and they stay open as owed work.

OUT-OF-SCOPE CHECK (§4, §14, §20)
- The result is unchanged from the first pass, because the delta is docs-only:
  - no §4.4 forbidden shape;
  - no cart, order, reservation, payment, diet-authorisation or inventory type;
  - no stock column;
  - no enqueue in an event handler;
  - no Shop upload path.
- §20.3 (Shop computing stock): reported in §1 Amended and made Testing-only by decision (1). Not worked around silently.

UNDECLARED DEVIATIONS
- None.
- Both items the first pass named are now declared in §1 Amended 2026-10-04:
  - (2): image URLs are null, and HO-FIL-001 signed URLs are not wired (backlog row 2);
  - (3): six files are outside §13, each with a stated reason (backlog row 3).

SCOPE CHECK
- files in allowed scope: 362/362 changed files, plus this record. The count includes:
  - the files listed in §13;
  - the files declared in §1 Amended, now including the six late-declared files (`RateLimiting/ShopRateLimitPolicies.cs`, `Helpers/Gtin.cs`, `ViewModels/ProductMoneyExtensions.cs`, `Persistence/Views/{StorefrontCatalogueEntry,ProductRatingSummaryRow,CatalogueTermRow}.cs`);
  - the ADR-341 per-area partials and the `ShopModule`;
  - `docs/handover/HO-SHP-001.md`, where only the §1 Amended rows and the four §18 evidence cells changed;
  - `docs/backlog/SHP.md`;
  - `tests/**`.
- files outside allowed scope: none.
