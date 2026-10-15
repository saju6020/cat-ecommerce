namespace CatEcommerce.Read.ViewModels;

// HO-ORD-001 §12.4 (Phase F) -- the staff views of orders and fulfilment. Unlike the customer's, these name the staff who acted and show batches, locations and
// the money held back for a second person.

/// <summary>One row of the staff order queue. <c>Flags</c> are the highlights staff act on: <c>AwaitingApproval</c>, <c>TransferReported</c>,
/// <c>CourierReportsDelivered</c> (a webhook said delivered but no person recorded the money), <c>CollectionOverdue</c> and <c>CustomerAnonymised</c>.</summary>
public sealed record StaffOrderRowViewModel(
    Guid Id, string OrderNumber, DateTimeOffset PlacedOn, string CustomerName, string Status, string FulfilmentMethod, string PaymentMethod, string Channel,
    MoneyViewModel GrandTotal, long AgeMinutes, IReadOnlyList<string> Flags, string BranchTimeZoneId);

public sealed record StaffAllocationViewModel(Guid BatchId, string BatchNumber, DateOnly? ExpiresOn, Guid LocationId, string LocationName, decimal Quantity);

public sealed record StaffOrderLineViewModel(
    Guid LineId, int LineNumber, Guid SkuId, string ProductName, string VariantLabel, CartCatViewModel? Cat, int Quantity, MoneyViewModel UnitPrice, MoneyViewModel LineTotal,
    int QuantityReturned, IReadOnlyList<StaffAllocationViewModel> Allocations);

public sealed record StaffStatusChangeViewModel(string? From, string To, DateTimeOffset ChangedOn, Guid ChangedBy, string? ChangedByName, string? Note);

public sealed record StaffPaymentRefViewModel(Guid PaymentId, string Method, MoneyViewModel Amount, DateTimeOffset RecordedOn);

public sealed record StaffCodCollectionViewModel(MoneyViewModel Amount, string CollectorKind, string CollectorName, DateTimeOffset CollectedOn, Guid RecordedBy);

public sealed record StaffShipmentViewModel(Guid CourierId, string CourierName, string? TrackingNumber, string? TrackingUrl, DateTimeOffset DispatchedOn, string? LastTrackingStatus, DateTimeOffset? LastTrackingEventOn);

public sealed record StaffOrderDetailViewModel(
    Guid Id, int Version, string OrderNumber, DateTimeOffset PlacedOn, string Channel, Guid? CustomerId, string Status, string FulfilmentMethod, string PaymentMethod,
    string BranchTimeZoneId, IReadOnlyList<StaffOrderLineViewModel> Lines, OrderTotalsViewModel Totals, OrderFulfilmentViewModel Fulfilment,
    IReadOnlyList<StaffStatusChangeViewModel> StatusHistory, DateTimeOffset? PaymentDueBy, string? CustomerTransferReference, HeldPaymentViewModel? HeldPayment,
    StaffCodCollectionViewModel? CodCollection, StaffShipmentViewModel? Shipment, DateOnly? CollectBy, Guid? InvoiceId, string? InvoiceNumber,
    IReadOnlyList<StaffPaymentRefViewModel> Payments, IReadOnlyList<Guid> CreditNoteIds, MoneyViewModel AmountPaid, MoneyViewModel AmountRefunded, MoneyViewModel RefundDue,
    string? DiscountCode, bool CustomerAnonymised, IReadOnlyList<string> AllowedActions);

public sealed record PickListOrderViewModel(string OrderNumber, int Quantity);

public sealed record PickListBatchViewModel(
    Guid BatchId, string BatchNumber, DateOnly? ExpiresOn, Guid SkuId, string ProductName, string VariantLabel, decimal Quantity, IReadOnlyList<PickListOrderViewModel> Orders);

public sealed record PickListGroupViewModel(Guid LocationId, string LocationName, IReadOnlyList<PickListBatchViewModel> Batches);

public sealed record PickListViewModel(DateTimeOffset GeneratedOn, IReadOnlyList<PickListGroupViewModel> Groups);

public sealed record PackingSlipLineViewModel(int LineNumber, string ProductName, string VariantLabel, int Quantity, string? CatName, IReadOnlyList<string> BatchNumbers);

public sealed record PackingSlipViewModel(
    Guid OrderId, string OrderNumber, DateTimeOffset PlacedOn, string CustomerName, string RecipientName, string? RecipientPhone, string FulfilmentMethod,
    string? AddressText, string? PickupBranchName, string? Slot, IReadOnlyList<PackingSlipLineViewModel> Lines, MoneyViewModel? CodAmountDue, string? DeliveryInstructions,
    string BranchTimeZoneId);

/// <summary>One manifest row (CSV): the order dispatched with a courier on a branch-local date.</summary>
public sealed record ManifestRowViewModel(
    string OrderNumber, string RecipientName, string? Phone, string? Address, string? District, string? PostalCode, int Parcels, int WeightGrams, decimal CodAmount, string? TrackingNumber);

/// <summary>One row of the customer page's orders panel (HO-ORD-002 §11.14): number, date, status and total, with the id for the link to the order.</summary>
public sealed record StaffCustomerOrderRowViewModel(
    Guid Id, string OrderNumber, DateTimeOffset PlacedOn, string Status, string FulfilmentMethod, string PaymentMethod, string Channel, MoneyViewModel GrandTotal,
    string BranchTimeZoneId);
