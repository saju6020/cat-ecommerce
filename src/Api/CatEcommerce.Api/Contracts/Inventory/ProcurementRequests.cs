namespace CatEcommerce.Api.Contracts.Inventory;

/// <summary>HO-IMP-001 §12.4 request bodies -- suppliers and purchase orders. Ids are minted in the controller; the branch is the caller's
/// ACTIVE branch (<c>X-Branch-Id</c>), never a body field.</summary>
public sealed class CreateSupplierRequest
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string CountryCode { get; init; }
    public string? ContactName { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
    public required string CurrencyCode { get; init; }
    public required int LeadTimeDays { get; init; }
    public required string PaymentTerms { get; init; }
    public int? PaymentTermDays { get; init; }
    public string? Notes { get; init; }
}

public sealed class UpdateSupplierRequest
{
    public required string Name { get; init; }
    public required string CountryCode { get; init; }
    public string? ContactName { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
    public required string CurrencyCode { get; init; }
    public required int LeadTimeDays { get; init; }
    public required string PaymentTerms { get; init; }
    public int? PaymentTermDays { get; init; }
    public string? Notes { get; init; }
}

public sealed class SetSupplierActivationRequest
{
    public required bool IsActive { get; init; }
}

public sealed class PurchaseOrderLineRequest
{
    public required Guid SkuId { get; init; }
    public required decimal OrderedQuantity { get; init; }
    public required decimal UnitCostAmount { get; init; }
}

public sealed class PurchaseOrderRequest
{
    public required Guid SupplierId { get; init; }
    public required Guid DeliverToLocationId { get; init; }
    public string? CurrencyCode { get; init; }
    public required DateOnly ExpectedDeliveryOn { get; init; }
    public string? Notes { get; init; }
    public required IReadOnlyList<PurchaseOrderLineRequest> Lines { get; init; }
}

public sealed class PurchaseOrderReasonRequest
{
    public required string Reason { get; init; }
}
