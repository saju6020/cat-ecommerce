namespace CatEcommerce.Api.Contracts.Billing;

/// <summary>HO-BIL-001 §12.4 -- <c>POST /api/staff/billing/charges</c> (C2). Either <see cref="ServiceId"/> or
/// <see cref="Description"/> + <see cref="UnitPrice"/> + <see cref="TaxClassId"/>.</summary>
public sealed class RecordManualChargeRequest
{
    public Guid CustomerId { get; init; }
    public Guid? CatId { get; init; }
    public Guid? ServiceId { get; init; }
    public string? Description { get; init; }
    public decimal? UnitPrice { get; init; }
    public Guid? TaxClassId { get; init; }
    public decimal Quantity { get; init; } = 1m;
    public decimal LineDiscount { get; init; }
    public DateOnly ServiceDate { get; init; }
    public string? BusinessLine { get; init; }
}

/// <summary><c>POST /api/staff/billing/charges/{chargeId}/void</c> (C3).</summary>
public sealed class VoidChargeRequest
{
    public string Reason { get; init; } = default!;
}
