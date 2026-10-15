namespace CatEcommerce.Read.ViewModels;

// HO-ORD-001 §12.4 (Phase G) -- returns and refunds, for the customer and for staff.

/// <summary>One line of a return as the customer sees it.</summary>
public sealed record CustomerReturnLineViewModel(
    Guid OrderLineId, string ProductName, string VariantLabel, int Quantity, string Reason, string Condition, string? CustomerNote, int QuantityReceived);

public sealed record CustomerReturnViewModel(
    Guid Id, Guid OrderId, string OrderNumber, string Kind, string Status, DateTimeOffset RequestedOn, string? DecisionNote, IReadOnlyList<CustomerReturnLineViewModel> Lines);

public sealed record StaffReturnRowViewModel(
    Guid Id, Guid OrderId, string OrderNumber, string CustomerName, string Kind, string Status, DateTimeOffset RequestedOn, int LineCount, int Quantity);

public sealed record StaffReturnLineViewModel(
    Guid OrderLineId, string ProductName, string VariantLabel, int Quantity, string Reason, string Condition, string? CustomerNote, bool RequiresDefectConfirmation, bool DefectConfirmed,
    string? DefectNote, int QuantityReceived, bool AnyBatchRecalled, IReadOnlyList<string> BatchNumbers);

public sealed record StaffReturnDetailViewModel(
    Guid Id, int Version, Guid OrderId, string OrderNumber, string CustomerName, string Kind, string Status, DateTimeOffset RequestedOn, Guid RequestedBy, DateTimeOffset? DecidedOn,
    Guid? DecidedBy, string? DecisionNote, DateTimeOffset? ReceivedOn, IReadOnlyList<StaffReturnLineViewModel> Lines, string BranchTimeZoneId);

public sealed record RefundMethodOptionViewModel(string Method, bool Allowed, bool Available, string? UnavailableReason);

public sealed record SuggestedRefundViewModel(Guid ReturnRequestId, MoneyViewModel SuggestedAmount);

/// <summary>What a refund screen needs: the methods by how the customer paid, the most that can still be refunded and, for each received return not yet refunded, the amount to suggest
/// (the value of the units that came back, never the delivery charge).</summary>
public sealed record RefundOptionsViewModel(
    Guid OrderId, IReadOnlyList<RefundMethodOptionViewModel> Methods, MoneyViewModel MaxRefundable, IReadOnlyList<SuggestedRefundViewModel> SuggestedForReturns);
