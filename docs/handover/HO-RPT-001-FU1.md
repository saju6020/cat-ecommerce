# HO-RPT-001-FU1 — A typed variance direction on the reconciliation reports (backend follow-up)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-RPT-001-FU1** (follow-up slice to [`HO-RPT-001`](HO-RPT-001.md); consumed by [`HO-RPT-002`](HO-RPT-002.md)) |
| Date | 2026-10-14 |
| Branch | `ho/RPT-001-FU1` from master `5a187b6b` |
| Status | Backend: Complete. Not merged |

## 2. Objective

R13 (`payment-reconciliation`) and R14 (`daily-cash-up`) return `variance` as money text only, so HO-RPT-002 guessed "Mismatch / Matched" with a digit test (`isNonZeroAmount`, RPT backlog row 71) and could not say Over or Short. The sign convention also differs between rows. Give the server the job: a typed direction beside each variance.

**Owner decision (2026-10-14), verbatim:** "variances should read \"Over / Short\"".

**Premise corrected.** The brief called R14 "stock variance". In the shipped catalogue R14 is `daily-cash-up` (HO-BIL-001 cash-ups). No Reporting report returns a stock-count variance (R5-R8 have none), so none was changed. If the owner wants a stock-count variance report, that is a new report.

## 3. Chosen shape and why

A **new `Text` column `varianceDirection`, placed right after `variance`**, in both reports. Not a new `ReportColumnType`, because that would be a new enum value every consumer (the generic formatter, CSV, PDF, `formatCell`) must learn and would break older clients on an unknown `type`. A plain text column rides the existing pipeline unchanged: sorting, CSV, PDF and `formatCell`'s text kind all work, and the export reads "Over" / "Short" in words. The value is the enum name as a string (`VarianceDirection` enum; the row stores the string so the generic sort and CSV need no enum case).

Column meaning is carried in a new optional trailing `ReportColumn.Description` (exposed as `description` in `columns[]` of both the catalogue and the run result; `null` on every other column). Additive: no field changed or removed. The existing `variance` value is untouched.

## 4. Contract

| | |
|---|---|
| Field | `varianceDirection` |
| Where | `rows[i].varianceDirection` of `GET /api/staff/reports/payment-reconciliation` and `GET /api/staff/reports/daily-cash-up`; column entry `{ key: "varianceDirection", label: "Variance direction", type: "Text", description }` in `columns[]` of the run result and of `GET /api/staff/reports` |
| Values | `"Over"`, `"Short"`, `"Matched"`, or `null` |
| Totals | Neither report has a totals row (`HasTotals` false), so none carries a direction |

Meaning, **always read off the two figures compared, never off the sign of `variance`**:

| Report / row | Compared | `Over` | `Short` | `Matched` | `null` |
|---|---|---|---|---|---|
| R13 cash on delivery | remitted vs expected | never (remitted is capped at the amount) | remitted < expected (`variance` is expected - received, **positive**) | equal | never |
| R13 bank transfer | matched statement credits vs expected | never | some transfer unmatched (`variance` positive) | all matched | never |
| R13 cash in store | counted vs expected | counted > expected (`variance` is counted - expected, positive) | counted < expected (`variance` negative) | equal | day not counted (`received` null) |
| R13 card in store | nothing to compare against (SRS Q-3c) | | | | always |
| R14 daily cash-up | `counted` vs `expected` (`variance` = counted - expected) | counted > expected | counted < expected | equal | day not closed / not counted |

Equality is decimal equality, so `0`, `0.00` and `-0.00` are `Matched`; `±0.01` is `Over` / `Short`. `null` is never `Matched`: "nothing to compare" is not "agrees". **Frontend:** show the word from `varianceDirection`; delete the digit test and `isVarianceKey`. Do not read the sign of `variance` (the COD/bank rows and the cash row of the same report point opposite ways).

## 5. Scope

No schema change, no view change, no migration.

- `src/Shared/CatEcommerce.Shared/Common/Enums/VarianceDirection.cs` (new: enum, `VarianceDirections.Of/Name`, `ColumnKey`)
- `src/Shared/CatEcommerce.Shared/Common/Constants/ReportDefinition.cs` (`ReportColumn.Description`, trailing, optional)
- `src/Shared/CatEcommerce.Shared/Common/Constants/ReportCatalogue.Financial.cs` (the column in R13 and R14, `VarianceDirectionR13/R14` descriptions)
- `src/Read/CatEcommerce.Read/QueryHandlers/Reporting/Sources/PaymentReportBuilders.cs` (the value in every R13 and R14 row)
- `src/Read/CatEcommerce.Read/QueryHandlers/Reporting/ReportResultFormatter.cs`, `GetReportCatalogueQueryHandler.cs`, `src/Read/CatEcommerce.Read/ViewModels/ReportViewModels.cs` (`description` in the JSON)
- `docs/handover/CONTRACTS.md` row 41 (BIL-001-J uses 36-40 on its branch); `docs/handover/HO-RPT-001.md` R13/R14 rows; `docs/backlog/RPT.md` row `FU1-1`

## 6. Findings

- **R13 is inconsistent inside itself:** COD and bank rows show `expected - received`, the cash row shows `counted - expected`. Left as is (the variance value is a contract); the direction hides it. Not fixed here because HO-RPT-002 and the CSV already show the existing sign.
- The new column appears in the CSV and PDF exports (they render every visible column), in words.

## 7. Tests

- `tests/CatEcommerce.Application.Tests/QueryHandlers/Reporting/VarianceDirectionTests.cs` (26 cases): boundaries 0, 0.00, -0.00, +0.01, -0.01 for the R14 builder and the R13 cash row; R13 COD and bank transfer are `Short` with a positive variance; card in store and an uncounted day are `null`; both catalogue entries declare the column right after `variance` with a description; the enum has exactly three values.
- `ReportingFinancialEndpointTests` (I-D5, I-D6) through the real endpoint: COD `Short`, bank `Short`, cash `Short`, card `null`, a day not closed `null`, and the column and description in the run result and in the catalogue.
- Run on `catecommerce_rptfu1` (per-branch database, migrated to master head; never `catecommerce_dev` or `catecommerce_test`). Filtered `Reporting`: integration 109/109, Application `Report` 97/97.
