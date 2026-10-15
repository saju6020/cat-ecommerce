namespace CatEcommerce.Api.Contracts.Billing;

/// <summary><c>PUT /api/staff/billing/cash-ups/{date}/count</c> (C20).</summary>
public sealed class RecordCashCountRequest
{
    public decimal CountedCash { get; init; }
    public string? Explanation { get; init; }
}

/// <summary><c>POST /api/staff/billing/cash-ups/{date}/close</c> (C21).</summary>
public sealed class CloseCashUpRequest
{
    public int ExpectedVersion { get; init; }
}
