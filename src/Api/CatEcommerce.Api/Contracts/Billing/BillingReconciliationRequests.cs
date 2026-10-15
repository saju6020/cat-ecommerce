using CatEcommerce.Application.Commands.Billing;
using Microsoft.AspNetCore.Http;

namespace CatEcommerce.Api.Contracts.Billing;

/// <summary><c>POST /api/staff/billing/cod/remittances</c> (C22).</summary>
public sealed class RecordCodRemittanceRequest
{
    public string CourierCode { get; init; } = string.Empty;
    public DateOnly ReceivedOn { get; init; }
    public decimal Amount { get; init; }
    public string Method { get; init; } = string.Empty;
    public string? Reference { get; init; }
    public IReadOnlyList<RemittanceLineInput> Lines { get; init; } = [];
}

/// <summary><c>POST /api/staff/billing/bank-statements</c> (multipart, C23).</summary>
public sealed class ImportBankStatementRequest
{
    public IFormFile? File { get; init; }
    public string BankAccountLabel { get; init; } = string.Empty;
    public DateOnly PeriodFrom { get; init; }
    public DateOnly PeriodTo { get; init; }
}

/// <summary><c>POST /api/staff/billing/bank-matches</c> (C25).</summary>
public sealed class MatchBankLineRequest
{
    public Guid StatementId { get; init; }
    public Guid StatementLineId { get; init; }
    public Guid PaymentId { get; init; }
}

/// <summary><c>POST /api/staff/billing/bank-matches/{id}/unmatch</c>.</summary>
public sealed class UnmatchBankLineRequest
{
    public string Reason { get; init; } = string.Empty;
}
