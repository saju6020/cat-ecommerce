using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Inventory;

/// <summary>HO-IMP-001 §12.4 (<c>Procurement.View</c>) -- paged, filterable by text, country and active flag.</summary>
public sealed class GetSuppliersQuery : Query<PagedResult<SupplierSummaryViewModel>>
{
    public string? Search { get; init; }
    public string? CountryCode { get; init; }
    public bool? IsActive { get; init; }
}

public sealed class GetSupplierQuery : Query<SupplierDetailViewModel?>
{
    public required Guid SupplierId { get; init; }
}

/// <summary>Paged purchase orders of the active branch; <c>ReceiptState</c> is derived (ADR-112), never stored.</summary>
public sealed class GetPurchaseOrdersQuery : Query<PagedResult<PurchaseOrderSummaryViewModel>>
{
    public required Guid BranchId { get; init; }
    public string? Status { get; init; }
    public Guid? SupplierId { get; init; }
    public string? ReceiptState { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}

public sealed class GetPurchaseOrderQuery : Query<PurchaseOrderDetailViewModel?>
{
    public required Guid PurchaseOrderId { get; init; }
    public required Guid BranchId { get; init; }
}
