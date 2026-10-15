using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// Billing (HO-BIL-001) table classifications, added in the same change that adds the tables
/// (HO-XCT-001 §5.2 C-ALL-1). HO-XCT-001 §8.2: <c>Financial</c> -- "invoices, payments, credit notes,
/// refunds, till and COD reconciliation, price and tax history" -- is <c>Forbidden</c> (D-9, review
/// flag only). An owned child that is itself a record (invoice lines, allocation entries, credit-note
/// lines, remittance lines, statement lines) is also <c>Forbidden</c> (C-ALL-4: no method removes it).
/// The one deliberate exception is <c>payment_requested_allocation</c> -- a transient working set
/// (pending approval only, cleared once applied; §8.8) that an aggregate method replaces wholesale,
/// the <c>CurrentState</c> carve-out, hence <c>AggregateOnly</c>. <c>invoice_line</c> of a
/// <c>Draft</c> invoice is deleted by the aggregate while it is still a draft (§12.1), but the
/// <c>Forbidden</c> rule still holds for issued lines -- the database trigger (ADR-180) is what
/// distinguishes them, so the table is <c>Forbidden</c>.
/// </summary>
public static partial class RecordClassification
{
    private static readonly IReadOnlyList<TableClassification> BillingEntries =
    [
        new TableClassification("billing", "billing_profile", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-BIL-001 §8.1 -- the seller details snapshotted onto every invoice at issue"),
        new TableClassification("billing", "billing_profile_bank_account", RecordClass.Operational, HardDeletePolicy.AggregateOnly, "Owned by billing_profile; the bank-account list is replaced wholesale (CurrentState carve-out, C-ALL-1)"),
        new TableClassification("billing", "document_number_series", RecordClass.Financial, HardDeletePolicy.Forbidden, "HO-BIL-001 §8.7 -- proves gapless numbering (FR-BIL-1)"),
        new TableClassification("billing", "charge", RecordClass.Financial, HardDeletePolicy.Forbidden, "HO-BIL-001 §8.3 -- input to invoices; voided, never deleted (D-6)"),
        new TableClassification("billing", "invoice", RecordClass.Financial, HardDeletePolicy.Forbidden, "HO-BIL-001 §8.6 -- D-9"),
        new TableClassification("billing", "invoice_line", RecordClass.CurrentState, HardDeletePolicy.AggregateOnly, "HO-BIL-001 §8.6 -- a Draft invoice lines are replaced by InvoiceAggregate.SetDraftLines; trg_invoice_line_immutable refuses any change once issued (HO-XCT-001 ADR-298 amendment 2026-10-12, owner: reclassify properly)"),
        new TableClassification("billing", "payment", RecordClass.Financial, HardDeletePolicy.Forbidden, "HO-BIL-001 §8.8 -- D-9"),
        new TableClassification("billing", "payment_allocation_entry", RecordClass.Financial, HardDeletePolicy.Forbidden, "HO-BIL-001 §8.8 -- append-only allocation history (ADR-185)"),
        new TableClassification("billing", "payment_requested_allocation", RecordClass.Financial, HardDeletePolicy.AggregateOnly, "HO-BIL-001 §8.8 -- the pending-approval working set, cleared once applied (CurrentState carve-out)"),
        new TableClassification("billing", "credit_note", RecordClass.Financial, HardDeletePolicy.Forbidden, "HO-BIL-001 §8.9 -- D-9"),
        new TableClassification("billing", "credit_note_line", RecordClass.Financial, HardDeletePolicy.Forbidden, "HO-BIL-001 §8.9 -- a line of a financial document (C-ALL-4)"),
        new TableClassification("billing", "refund", RecordClass.Financial, HardDeletePolicy.Forbidden, "HO-BIL-001 §8.9 -- money out; D-9"),
        new TableClassification("billing", "cash_up", RecordClass.Financial, HardDeletePolicy.Forbidden, "HO-BIL-001 §8.10 -- till reconciliation"),
        new TableClassification("billing", "cod_remittance", RecordClass.Financial, HardDeletePolicy.Forbidden, "HO-BIL-001 §8.1 -- COD reconciliation"),
        new TableClassification("billing", "cod_remittance_line", RecordClass.Financial, HardDeletePolicy.Forbidden, "HO-BIL-001 §8.1 -- a line of a financial reconciliation record (C-ALL-4)"),
        new TableClassification("billing", "bank_statement", RecordClass.Financial, HardDeletePolicy.Forbidden, "HO-BIL-001 §8.1 -- bank reconciliation"),
        new TableClassification("billing", "bank_statement_line", RecordClass.Financial, HardDeletePolicy.Forbidden, "HO-BIL-001 §8.1 -- a line of a financial reconciliation record (C-ALL-4)"),
        new TableClassification("billing", "bank_match", RecordClass.Financial, HardDeletePolicy.Forbidden, "HO-BIL-001 §8.1 -- an unmatch keeps the row (C25)"),
        new TableClassification("billing", "accounting_export_run", RecordClass.Financial, HardDeletePolicy.Forbidden, "HO-BIL-001 §8.1 -- who exported what, append-only (ADR-190)"),
        new TableClassification("billing", "loyalty_account", RecordClass.Financial, HardDeletePolicy.Forbidden, "HO-BIL-001 §8.11 -- a points balance; moved on merge, never deleted"),
        new TableClassification("billing", "loyalty_entry", RecordClass.Financial, HardDeletePolicy.Forbidden, "HO-BIL-001 §8.11 -- append-only points history"),
    ];
}
