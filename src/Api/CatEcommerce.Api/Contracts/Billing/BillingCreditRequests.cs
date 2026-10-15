using CatEcommerce.Application.Commands.Billing;

namespace CatEcommerce.Api.Contracts.Billing;

/// <summary><c>POST /api/staff/billing/invoices/{id}/credit-notes</c> (C16).</summary>
public sealed class IssueCreditNoteRequest
{
    public string Kind { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public bool FullCredit { get; init; }
    public IReadOnlyList<CreditLineRequest>? Lines { get; init; }
}

/// <summary><c>POST /api/staff/billing/invoices/{id}/corrected-draft</c> (C17).</summary>
public sealed class CreateCorrectedDraftRequest
{
    public Guid CreditNoteId { get; init; }
}

/// <summary><c>PUT /api/staff/billing/invoices/{id}/lines</c> (C18).</summary>
public sealed class SetCorrectedDraftLinesRequest
{
    public int ExpectedVersion { get; init; }
    public IReadOnlyList<CorrectedLineInput> Lines { get; init; } = [];
}

/// <summary><c>POST /api/staff/billing/refunds</c> (C19).</summary>
public sealed class IssueRefundRequest
{
    public Guid PaymentId { get; init; }
    public Guid? CreditNoteId { get; init; }
    public string Method { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string? Reference { get; init; }
    public string Reason { get; init; } = string.Empty;
}

/// <summary><c>POST /api/staff/billing/invoices/{id}/loyalty-redemption</c> (C27).</summary>
public sealed class ApplyLoyaltyRedemptionRequest
{
    public int Points { get; init; }
}

/// <summary><c>POST /api/staff/billing/customers/{customerId}/apply-credit</c> (C13).</summary>
public sealed class ApplyCustomerCreditRequest
{
    public Guid InvoiceId { get; init; }

    /// <summary>Null means as much as the credit and the outstanding allow.</summary>
    public decimal? Amount { get; init; }
}
