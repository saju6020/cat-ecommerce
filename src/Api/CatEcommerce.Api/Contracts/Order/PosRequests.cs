namespace CatEcommerce.Api.Contracts.Order;

public sealed class PosLineRequest
{
    public Guid SkuId { get; init; }
    public int Quantity { get; init; }
    public Guid? CatId { get; init; }
}

public sealed class PosTenderRequest
{
    public string Method { get; init; } = default!;
    public decimal Amount { get; init; }
    public string? Reference { get; init; }
}

/// <summary><c>POST /api/staff/pos/quote</c> -- the sale priced without any state change.</summary>
public sealed class PosQuoteRequest
{
    public Guid? CustomerId { get; init; }
    public IReadOnlyList<PosLineRequest> Lines { get; init; } = [];
    public string? DiscountCode { get; init; }
}

/// <summary><c>POST /api/staff/pos/sales</c>. A <c>Cash</c> tender's amount is the money handed over (the change is worked out); an <c>InStoreCard</c> tender is charged at face
/// value and carries the terminal slip reference, never a card number.</summary>
public sealed class PosSaleRequest
{
    /// <summary>Both required (ADR-216): an active customer with this primary mobile is the buyer, else a passwordless customer is registered with this name.</summary>
    public string CustomerName { get; init; } = default!;
    public string CustomerMobile { get; init; } = default!;
    public IReadOnlyList<PosLineRequest> Lines { get; init; } = [];
    public string? DiscountCode { get; init; }
    public IReadOnlyList<PosTenderRequest> Tenders { get; init; } = [];
}
