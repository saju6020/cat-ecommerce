using CatEcommerce.Application.Commands.Order;

namespace CatEcommerce.Api.Contracts.Order;

/// <summary>HO-ORD-001 §12.4 -- the body of <c>POST /api/admin/delivery-zones</c> and <c>PUT …/{zoneId}</c>. Money is a plain decimal in the
/// zone's currency (<c>commerce.currencyCode</c>); the server never trusts a client currency.</summary>
public sealed class DeliveryZoneRequest
{
    public Guid BranchId { get; init; }
    public string Name { get; init; } = default!;
    public int Priority { get; init; }
    public bool IsActive { get; init; } = true;
    public bool CodEnabled { get; init; }
    public decimal? CodMaxOrderValue { get; init; }
    public IReadOnlyList<CoverageInput> Coverage { get; init; } = [];
    public IReadOnlyList<ZoneServiceInput> Services { get; init; } = [];
}

public sealed class SetActivationRequest
{
    public bool IsActive { get; init; }
}

public sealed class CourierRequest
{
    public string Name { get; init; } = default!;
    public string? TrackingUrlTemplate { get; init; }
    public bool SupportsCod { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class BankTransferAccountRequest
{
    public string AccountName { get; init; } = default!;
    public string AccountNumber { get; init; } = default!;
    public string BankName { get; init; } = default!;
    public string BranchName { get; init; } = default!;
    public string? RoutingNumber { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class CreateDiscountCodeRequest
{
    public string Code { get; init; } = default!;
    public string? Description { get; init; }
    public string Kind { get; init; } = default!;
    public decimal? Percent { get; init; }
    public decimal? Amount { get; init; }
    public decimal? MinimumSpend { get; init; }
    public string Scope { get; init; } = "Order";
    public IReadOnlyList<Guid> ProductIds { get; init; } = [];
    public IReadOnlyList<int> CategoryIds { get; init; } = [];
    public bool FirstOrderOnly { get; init; }
    public int? UsageLimit { get; init; }
    public int? PerCustomerLimit { get; init; }
    public DateTimeOffset? ValidFrom { get; init; }
    public DateTimeOffset? ValidTo { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class UpdateDiscountCodeRequest
{
    public string? Description { get; init; }
    public string Kind { get; init; } = default!;
    public decimal? Percent { get; init; }
    public decimal? Amount { get; init; }
    public decimal? MinimumSpend { get; init; }
    public string Scope { get; init; } = "Order";
    public IReadOnlyList<Guid> ProductIds { get; init; } = [];
    public IReadOnlyList<int> CategoryIds { get; init; } = [];
    public bool FirstOrderOnly { get; init; }
    public int? UsageLimit { get; init; }
    public int? PerCustomerLimit { get; init; }
    public DateTimeOffset? ValidFrom { get; init; }
    public DateTimeOffset? ValidTo { get; init; }
    public bool IsActive { get; init; } = true;
}

/// <summary>HO-ORD-001 §12.4 -- <c>POST /api/cart/lines</c>. <c>CatId</c> is for a prescription SKU only; the caller is never in the body.</summary>
public sealed class AddCartLineRequest
{
    public Guid SkuId { get; init; }
    public int Quantity { get; init; } = 1;
    public Guid? CatId { get; init; }
}

/// <summary>HO-ORD-001 §12.4 -- <c>PUT /api/cart/lines/{lineId}</c>.</summary>
public sealed class UpdateCartLineRequest
{
    public int? Quantity { get; init; }
    public Guid? CatId { get; init; }
}
