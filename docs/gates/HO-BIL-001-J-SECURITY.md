GATE: SECURITY
HANDOVER: HO-BIL-001 (Phase J -- visit, dispense and surgery item sources)
COMMITS: 081197e6 10c74551 10faf707 f946b7af 663e05c5 (range 5a187b6b..663e05c5, branch ho/BIL-001-J)
DATE: 2026-10-14
STATUS: PASS

No BLOCKER or HIGH finding. Static review of every non-test source file in the range (35 files; the migration
Designer/snapshot skimmed). No tests were run: `CATECOMMERCE_TEST_DB` is not set in this environment and no
connection string for `catecommerce_bilj` was available without reading user secrets. The verdict rests on the code.

FINDINGS
- [MEDIUM] src/Read/CatEcommerce.Read/QueryHandlers/Billing/BillingChargeQueryHandlers.cs:528,538,547 -- the uncaptured
  list counts a source as captured as soon as it has ANY charge (`!Any(c => c.SourceKind == k && c.SourceId == id)`).
  The Phase J sources have several lines, and a capture can record some lines and not others: a line skipped as
  `TaxClassNotFound` (a diagnostic whose visit service has gone), a description refused by `CardNumberGuard` (for
  example a 15-digit microchip number in an Imaging/Other description that happens to pass Luhn), or the losing run of a
  capture race that stops early (row 73). That source then drops off the list, so the missing lines are never billed and
  nobody can see them. Row 73 says "the uncaptured list ... completes the set", and that is not true. No attacker is
  needed for this; it is a gap in billing completeness. Fix: compare each source's expected line refs with the recorded
  ones (or keep a per-source "capture incomplete" marker) and list the partially captured sources; correct row 73.
- [LOW] src/Application/CatEcommerce.Application/CommandHandlers/Billing/ChargeCommandHandlers.cs:151-155 -- the manual
  capture route checks the branch only over `result.Items`. If another branch's source is not billable, or has no items,
  the route answers 200 with the reason instead of `SourceNotFound`: `VisitNotFinalised`, `VisitCustomerNotRecorded`,
  `DispensingReversed`, `SurgeryNotDischarged`, or `SkuNotPriced` skips. Anyone at any branch who holds
  `Billing.ManageCharges` (Reception and up) and knows a source GUID can therefore learn that the source exists and what
  clinical state it is in. Nothing is billed or changed, and the ids are random GUIDs, so the impact is small. Fix: return
  the source's `BranchId` on `BillableSourceResult` and compare it before the `IsBillable` check.
- [LOW] src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Migrations/20261013192812_AddVisitBillingFacts.cs --
  the diagnostic order's frozen `unit_price_amount`, `unit_price_currency_code` and `tax_class_id` are protected by the
  aggregate only (private setters, no mutator, no route). `clinic.diagnostic_order_guard` still freezes only cat, visit,
  branch and the soft-delete flag, so a raw UPDATE by the application role can re-price an order before it is captured.
  This is defence in depth, not a reachable path. Fix (integrator migration): add the three columns to
  `diagnostic_order_guard`.
- [LOW] src/Application/CatEcommerce.Application/DomainEventHandlers/Billing/CaptureChargesOnPhaseJSources.cs:199-229 with
  DispenseBillableItemSource.cs:127 -- a check-then-act race between capture and reversal. A capture can read the
  dispensing as `Dispensed`; a reversal that commits in that gap voids the (still empty) charge set; the capture then
  saves active charges for a reversed supply. The window is narrow because both normally run in-process right after their
  own saves, but nothing closes it. Fix: after saving, re-check the dispensing status in the capture (or have the
  reversal handler re-run after a short delay); otherwise the case falls to the manual correction in row 72.
- [LOW] src/Infrastructure/CatEcommerce.Infrastructure/Billing/ItemSources/SurgeryBillableItemSource.cs:215 -- a surgery
  bills `surgery.CustomerId` (the customer at booking, after the merge chain is followed), while a visit bills the cat's
  owner at finalising. If the cat is transferred between booking and discharge, the previous owner is billed. The
  account is not attacker-chosen, so this is not exploitable, but the two "who pays" rules differ and nobody has decided
  that they should. Fix: owner question. Either take the owner at discharge, as visits do, or record that the booking
  customer pays.

Checked and found sound (no finding):
1. Customer. `FinaliseVisitCommandHandler` reads `cat.OwnerId` before `Finalise`, and the aggregate refuses a missing
   owner (`Guid.Empty` -> `CatNotFound`). `CustomerId` has a private setter and is set only in `Finalise`, after
   `EnsureOpen`. `clinic.visit_frozen` (unchanged in this range, `HardenClinicalAndBillingTriggers`) refuses every
   UPDATE when `OLD.status = Finalised`, so `customer_id` and all clinical content are frozen at the database once
   finalised. The finalising UPDATE passes only because OLD is still Open. The trigger was not relaxed; the HO-EMR-001
   Amended row describes behaviour that already existed. A cat transfer after finalising cannot redirect the bill: the
   source reads the stored id, never the cat or the event. Merges are followed through `MergedIntoCustomerId`, capped
   at 10 hops. Visits finalised before Phase J are `VisitCustomerNotRecorded` and are excluded from the uncaptured list.
   There is no fallback to the appointment's customer.
2. Diagnostic prices. They are registry settings edited through `/api/admin/settings` (`Settings.Manage` =
   Administrator only), `BranchOverridable = false`, validated in `BusinessSettingsAggregate.TryValidateValue` to the
   range 0.00 to 10,000,000.00 (no negatives; the upper bound fits `numeric(12,2)`), with a `BusinessSettingsChangedEvent`
   per change. The price is read once by `OrderDiagnosticCommandHandler` and frozen on the order. An order is allowed
   only on an Open visit. J5 pins that changing the price later does not re-price an existing order.
3. Double billing. `ix_charge_source (source_kind, source_id, source_line_ref)` is unique, so duplicates are refused by
   the database. Line refs are stable ids: `treatment:{id}`, `diagnostic:{id}`, `line:{dispensingLineId}`, `procedure`,
   `drug:{originalEntryId}`. The customer, branch and price come from the source, never from the request. The manual
   route needs `Billing.ManageCharges` and takes its branch from the session (`TryResolveBranch`). A billable source of
   another branch is refused with `SourceNotFound` (see the LOW finding for non-billable ones).
4. Reversal void. It looks up charges only by (`Dispense`, `DispensingId`). `VoidChargeCommand` refuses a charge from
   another branch (`charge.BranchId != BranchId`) or one on any non-discarded invoice, drafts included
   (`GetBilledChargeIdsAsync`). The void records the reversing user as actor plus a fixed reason, so the audit trail
   names who reversed it. An already-voided charge throws, and the error is logged and ignored.
5. Logs. `SourceChargeCapture` and `VoidChargesOnDispensingReversedHandler` log only the kind, ids, error codes and the
   exception type. They log no message text, description or customer data.
6. Uncaptured list. It requires `Billing.View` (Reception, ShopManager, Accountant, Administrator) and is scoped to the
   session branch. It shows the customer id and a label (service name, the dispensing's medicine line, or the procedure
   name). Billing staff already see that text on the charge or invoice line, so it is not a new exposure.
7. Surgery drug pricing. `ISkuPriceReader.PriceAtAsync(sku, OccurredOn)`. `OccurredOn` is bounded by the aggregate to
   [admission - 1h, now + tolerance], so staff can only choose a time inside the stay, and the quantity is the
   stock-deducting `QuantityDrawn` (it is also in the controlled-drug register). A drug with no price is skipped and
   reported, not guessed. I found no practical way to manipulate it.

PROPOSED BACKLOG ROWS (docs/backlog/BIL.md, next free number 77)
| 77 | MEDIUM | **A partly captured multi-line source disappears from the uncaptured list.** `GetUncapturedSourcesQueryHandler` treats a visit, dispensing or surgery as captured once it has any charge. A line skipped as `TaxClassNotFound`, a description refused by the card guard, or a race loser that stops early (row 73) leaves lines unbilled with nothing listing them. Row 73's "the uncaptured list ... completes the set" is wrong. List sources whose recorded line refs are fewer than the source's lines. | SECURITY gate Phase J |
| 78 | LOW | **The capture route reveals another branch's non-billable source.** `CaptureSourceChargesCommandHandler` compares branches only over the items, so a non-billable or empty source of another branch answers 200 with its reason (`VisitNotFinalised`, `DispensingReversed`, `SurgeryNotDischarged`, ...) instead of `SourceNotFound`. Carry `BranchId` on `BillableSourceResult` and check it first. | SECURITY gate Phase J |
| 79 | LOW | **A diagnostic order's frozen price is not frozen in the database.** `clinic.diagnostic_order_guard` freezes identity and the soft-delete flag but not `unit_price_amount`, `unit_price_currency_code` or `tax_class_id`. Add them (integrator migration). | SECURITY gate Phase J |
| 80 | LOW | **Capture/reversal race on a dispensing.** A capture that read `Dispensed` can save after a reversal has already voided the (empty) charge set, which leaves active charges on a reversed supply. Re-check the status after the capture saves, or re-run the void. | SECURITY gate Phase J |
| 81 | LOW — Owner decision | **Surgery bills the booking customer; a visit bills the owner at finalising.** If the cat is transferred during an admission, the previous owner is billed for the surgery. Decide which rule applies. | SECURITY gate Phase J |

SCOPE CHECK
- files in allowed scope: 56/56 (Billing area plus the cross-area edits that the 2026-10-14 owner decisions authorise
  and that the HO-EMR/SRG/PHM Amended rows record)
- files outside allowed scope: none
