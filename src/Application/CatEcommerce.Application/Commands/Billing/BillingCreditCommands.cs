using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Billing;

/// <summary>One credited line: <c>Quantity</c> <b>or</b> <c>Amount</c> (gross), never both (§8.9).</summary>
public sealed record CreditLineRequest(Guid InvoiceLineId, decimal? Quantity = null, decimal? Amount = null);

/// <summary>
/// HO-BIL-001 §11.1 C16 (US-BIL-05 AC2) -- a credit note against an issued invoice. <see cref="FullCredit"/> credits every
/// remaining amount; otherwise <see cref="Lines"/> names what is credited. A credit that makes an invoice over-paid releases the
/// excess from its payments, newest allocation first, as the customer's credit (ADR-185/186).
/// </summary>
public sealed class IssueCreditNoteCommand : Command
{
    public required Guid CreditNoteId { get; init; }
    public required Guid InvoiceId { get; init; }
    public required Guid BranchId { get; init; }

    /// <summary>A <c>CreditNoteKind</c> name -- <c>Correction</c>, <c>Return</c>, <c>Cancellation</c> or <c>Goodwill</c>.</summary>
    public required string Kind { get; init; }
    public required string Reason { get; init; }
    public bool FullCredit { get; init; }
    public IReadOnlyList<CreditLineRequest>? Lines { get; init; }
    public string? IdempotencyKey { get; init; }
}

/// <summary>C17 -- a draft that corrects an invoice for which a credit note exists; its lines copy the credited invoice's.</summary>
public sealed class CreateCorrectedDraftCommand : Command
{
    public required Guid NewInvoiceId { get; init; }
    public required Guid InvoiceId { get; init; }
    public required Guid CreditNoteId { get; init; }
    public required Guid BranchId { get; init; }
}

/// <summary>The editable parts of a corrected draft's line (matched by position).</summary>
public sealed record CorrectedLineInput(string Description, decimal Quantity, decimal UnitPrice, decimal LineDiscount, Guid TaxClassId);

/// <summary>C18 -- edit a corrected draft's lines (no charge lines may be added or removed).</summary>
public sealed class SetCorrectedDraftLinesCommand : Command
{
    public required Guid InvoiceId { get; init; }
    public required Guid BranchId { get; init; }
    public required int ExpectedVersion { get; init; }
    public required IReadOnlyList<CorrectedLineInput> Lines { get; init; }
}

/// <summary>C19 (FR-BIL-5, FR-ORD-15 server half) -- money out, drawn from an accepted payment's unallocated amount.</summary>
public sealed class IssueRefundCommand : Command
{
    public required Guid RefundId { get; init; }
    public required Guid PaymentId { get; init; }
    public required Guid BranchId { get; init; }
    public Guid? CreditNoteId { get; init; }

    /// <summary>A <c>RefundMethod</c> name -- <c>CashInStore</c> or <c>BankTransfer</c>.</summary>
    public required string Method { get; init; }
    public required decimal Amount { get; init; }
    public string? Reference { get; init; }
    public required string Reason { get; init; }
    public string? IdempotencyKey { get; init; }
}
