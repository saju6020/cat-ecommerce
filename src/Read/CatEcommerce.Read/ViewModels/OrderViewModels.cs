namespace CatEcommerce.Read.ViewModels;

// HO-ORD-001 §12.4 -- the order views. Money is { amount: "1250.00", currencyCode }, instants carry their offset and every order view carries the branch's
// time-zone id so the frontend can show local times. No ViewModel carries a staff name outside the staff views.

/// <summary>List item. <c>TrackingNumber</c> and <c>CanReorder</c> were added additively for the portal (owner decision 2026-10-10); <c>CanReorder</c> is the one rule
/// <see cref="CustomerOrderRules.CanReorder"/> that the order detail uses too.</summary>
public sealed record CustomerOrderSummaryViewModel(
    Guid Id, string OrderNumber, DateTimeOffset PlacedOn, string Status, string FulfilmentMethod, string PaymentMethod, MoneyViewModel GrandTotal, int ItemCount, string BranchTimeZoneId,
    string? TrackingNumber, bool CanReorder);

public sealed record OrderLineViewModel(
    Guid LineId, int LineNumber, Guid SkuId, string ProductName, string VariantLabel, CartCatViewModel? Cat, int Quantity, MoneyViewModel UnitPrice, MoneyViewModel LineSubtotal,
    MoneyViewModel LineDiscount, MoneyViewModel OrderDiscountShare, decimal TaxRatePercent, MoneyViewModel TaxAmount, MoneyViewModel LineTotal,
    string? ImagePath = null);

public sealed record OrderTotalsViewModel(
    MoneyViewModel Subtotal, MoneyViewModel LineDiscountTotal, MoneyViewModel OrderDiscount, MoneyViewModel DeliveryCharge, MoneyViewModel TaxTotal, MoneyViewModel GrandTotal);

public sealed record OrderFulfilmentViewModel(
    string RecipientName, string? RecipientPhone, string? Line1, string? Line2, string? City, string? District, string? PostalCode, string? DeliveryInstructions,
    string? ZoneName, string? ServiceLevel, string? SlotDate, string? SlotWindow, Guid? PickupBranchId, string? PickupBranchName, string? PickupBranchAddress);

/// <summary>What a customer sees of the history: the status and when -- never who changed it.</summary>
public sealed record CustomerStatusChangeViewModel(string To, DateTimeOffset ChangedOn);

public sealed record OrderTransferInstructionsViewModel(
    string AccountName, string AccountNumber, string BankName, string BranchName, string? RoutingNumber, string PaymentReference, MoneyViewModel AmountDue);

public sealed record CustomerShipmentViewModel(string CourierName, string? TrackingNumber, string? TrackingUrl, string? LastTrackingStatus);

public sealed record ReturnableLineViewModel(Guid LineId, int MaxQuantity, string ReturnClass);

public sealed record CustomerRefundViewModel(string Method, MoneyViewModel Amount, DateTimeOffset IssuedOn);

public sealed record CustomerReturnSummaryViewModel(Guid Id, string Status);

public sealed record CustomerOrderDetailViewModel(
    Guid Id, string OrderNumber, DateTimeOffset PlacedOn, string Status, string FulfilmentMethod, string PaymentMethod, string BranchTimeZoneId,
    IReadOnlyList<OrderLineViewModel> Lines, OrderTotalsViewModel Totals, OrderFulfilmentViewModel Fulfilment, IReadOnlyList<CustomerStatusChangeViewModel> StatusHistory,
    DateTimeOffset? PaymentDueBy, OrderTransferInstructionsViewModel? TransferInstructions, string? CustomerTransferReference, CustomerShipmentViewModel? Shipment,
    DateOnly? CollectBy, string? InvoiceNumber, MoneyViewModel AmountPaid, MoneyViewModel AmountRefunded, MoneyViewModel RefundDue,
    IReadOnlyList<CustomerRefundViewModel> Refunds, IReadOnlyList<CustomerReturnSummaryViewModel> Returns,
    bool CanCancel, bool CanRequestReturn, DateOnly? ReturnWindowEndsOn, IReadOnlyList<ReturnableLineViewModel> ReturnableLines, bool CanReorder);

public sealed record HeldPaymentViewModel(MoneyViewModel Amount, Guid RecordedBy, DateTimeOffset RecordedOn);

public sealed record AwaitingPaymentOrderViewModel(
    Guid Id, string OrderNumber, string CustomerName, MoneyViewModel Amount, DateTimeOffset? PaymentDueBy, string? CustomerTransferReference, DateTimeOffset? TransferReportedOn,
    HeldPaymentViewModel? HeldPayment, int Version, string BranchTimeZoneId);

public sealed record ReconciliationIssueViewModel(
    Guid Id, Guid? OrderId, string Kind, string PaymentMethod, MoneyViewModel Amount, string? PaymentReference, DateTimeOffset? PaymentReceivedOn, string ErrorSummary,
    Guid RaisedBy, DateTimeOffset RaisedOn, string Status, Guid? ResolvedBy, DateTimeOffset? ResolvedOn, string? ResolutionNote);

public sealed record RefundDueOrderViewModel(
    Guid OrderId, string OrderNumber, string CustomerName, string Status, MoneyViewModel AmountPaid, MoneyViewModel AmountRefunded, MoneyViewModel RefundDue,
    DateTimeOffset? CancelledOn, string BranchTimeZoneId, IReadOnlyList<string> Reasons);
