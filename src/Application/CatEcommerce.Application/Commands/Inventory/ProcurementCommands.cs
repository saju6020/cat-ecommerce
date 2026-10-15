using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Inventory;

// HO-IMP-001 §11.1 C3-C4.

/// <summary>C3 -- <c>Procurement.Manage</c>.</summary>
public sealed class CreateSupplierCommand : Command
{
    public required Guid SupplierId { get; init; }
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

/// <summary>The code is immutable once created.</summary>
public sealed class UpdateSupplierCommand : Command
{
    public required Guid SupplierId { get; init; }
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

public sealed class SetSupplierActivationCommand : Command
{
    public required Guid SupplierId { get; init; }
    public required bool IsActive { get; init; }
}

public sealed record PurchaseOrderLineInput(Guid LineId, Guid SkuId, decimal OrderedQuantity, decimal UnitCostAmount);

/// <summary>C4. <see cref="CurrencyCode"/> defaults to the supplier's when omitted.</summary>
public sealed class CreatePurchaseOrderCommand : Command
{
    public required Guid PurchaseOrderId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid SupplierId { get; init; }
    public required Guid DeliverToLocationId { get; init; }
    public string? CurrencyCode { get; init; }
    public required DateOnly ExpectedDeliveryOn { get; init; }
    public string? Notes { get; init; }
    public required IReadOnlyList<PurchaseOrderLineInput> Lines { get; init; }
}

public sealed class ReplacePurchaseOrderDraftCommand : Command
{
    public required Guid PurchaseOrderId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid SupplierId { get; init; }
    public required Guid DeliverToLocationId { get; init; }
    public string? CurrencyCode { get; init; }
    public required DateOnly ExpectedDeliveryOn { get; init; }
    public string? Notes { get; init; }
    public required IReadOnlyList<PurchaseOrderLineInput> Lines { get; init; }
}

public sealed class IssuePurchaseOrderCommand : Command
{
    public required Guid PurchaseOrderId { get; init; }
    public required Guid BranchId { get; init; }
}

public sealed class CancelPurchaseOrderCommand : Command
{
    public required Guid PurchaseOrderId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Reason { get; init; }
}

public sealed class ClosePurchaseOrderCommand : Command
{
    public required Guid PurchaseOrderId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Reason { get; init; }
}
