namespace CatEcommerce.Read.ViewModels;

// HO-IMP-001 §12.4 -- suppliers and purchase orders. Quantities are strings (InventoryViewFormat) with the stock unit beside them; money is
// { amount, currencyCode }; unit costs carry four decimals.

public sealed record SupplierSummaryViewModel(
    Guid Id, string Code, string Name, string CountryCode, string CurrencyCode, int LeadTimeDays, string PaymentTerms, bool IsActive);

public sealed record SupplierDetailViewModel(
    Guid Id, string Code, string Name, string CountryCode, string? ContactName, string? Phone, string? Email, string? Address,
    string CurrencyCode, int LeadTimeDays, string PaymentTerms, int? PaymentTermDays, string? Notes, bool IsActive, int Version);

public sealed record PurchaseOrderSummaryViewModel(
    Guid Id, string Number, Guid SupplierId, string SupplierName, string Status, string ReceiptState, DateOnly ExpectedDeliveryOn,
    MoneyViewModel TotalAmount);

/// <summary>One line with its DERIVED receipt progress (ADR-112): <c>outstanding = max(0, ordered - accepted)</c>; a rejected unit is still owed.</summary>
public sealed record PurchaseOrderLineViewModel(
    Guid LineId, Guid SkuId, string SkuCode, string DisplayName, string StockUnit, string OrderedQuantity, string AcceptedQuantity,
    string RejectedQuantity, string OutstandingQuantity, MoneyViewModel UnitCost, MoneyViewModel LineTotal);

public sealed record PurchaseOrderReceiptViewModel(Guid GoodsReceiptId, string Number, DateTimeOffset ReceivedOn);

public sealed record PurchaseOrderDetailViewModel(
    Guid Id, string Number, Guid SupplierId, string SupplierName, Guid BranchId, Guid DeliverToLocationId, string CurrencyCode,
    DateOnly ExpectedDeliveryOn, string? Notes, string Status, string? StatusReason, string ReceiptState, DateTimeOffset? IssuedOn,
    MoneyViewModel TotalAmount, IReadOnlyList<PurchaseOrderLineViewModel> Lines, IReadOnlyList<PurchaseOrderReceiptViewModel> Receipts,
    IReadOnlyList<string> AllowedActions, int Version);
