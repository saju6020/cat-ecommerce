namespace CatEcommerce.Read.ViewModels;

// HO-IMP-001 section 8.12 / 12.4 -- the recall register and the trace (US-IMP-11).

/// <summary><c>Status</c> is <c>Active</c> (the batch is <c>Recalled</c> and the recall is not closed), <c>Closed</c> (completion recorded) or <c>Withdrawn</c>
/// (declared by mistake; the batch is no longer <c>Recalled</c>).</summary>
public sealed record RecallSummaryViewModel(
    Guid BatchId, string RecallNumber, string Status, Guid SkuId, string SkuCode, string DisplayName, string LotNumber, DateOnly? ExpiresOn, DateTimeOffset DeclaredOn,
    int NotifiedRecipientCount, int NotificationRuns, string HeldQuantity);

public sealed record RecallDetailViewModel(
    RecallSummaryViewModel Summary, BatchRecallViewModel Recall, IReadOnlyList<RecallHeldViewModel> Held);

public sealed record RecallHeldViewModel(Guid LocationId, string? LocationCode, Guid BranchId, string OnHand);

/// <summary>A customer as the recall trace shows them, resolved through the merge chain at READ time. An anonymised customer is "Anonymised customer" with no
/// contact details. <c>OriginalCustomerId</c> is set when the ledger recorded a customer who has since been merged into this one.</summary>
public sealed record RecallTraceCustomerViewModel(
    Guid CustomerId, string Name, string? MobileNumber, string? Email, bool IsAnonymised, Guid? OriginalCustomerId);

public sealed record RecallTraceCatViewModel(Guid CatId, string Name, RecallTraceCustomerViewModel? CurrentOwner);

/// <summary>One consumption of the recalled batch. For an entry that names a customer but no cat (an ordinary food order) <c>HouseholdCats</c> lists the
/// customer's living cats, as "household cats -- not specifically recorded" (OQ-18).</summary>
public sealed record RecallTraceReceivedViewModel(
    DateTimeOffset OccurredOn, string Kind, string Quantity, Guid LocationId, string? LocationCode, string? ReferenceLabel, RecallTraceCustomerViewModel? Customer,
    RecallTraceCatViewModel? Cat, IReadOnlyList<string> HouseholdCats);

public sealed record RecallTraceReturnedViewModel(string ReferenceKind, Guid ReferenceId, string? ReferenceLabel, string Quantity, RecallTraceCustomerViewModel? Customer);

/// <summary>Stock of the recalled batch that is reserved for an order and has not shipped: the orders HO-ORD-001 must not dispatch.</summary>
public sealed record RecallTraceUnshippedViewModel(
    string ReferenceKind, Guid ReferenceId, string Quantity, RecallTraceCustomerViewModel? Customer, RecallTraceCatViewModel? Cat);

public sealed record RecallTraceUnidentifiedViewModel(int Count, string Quantity);

public sealed record RecallTraceViewModel(
    Guid BatchId, string RecallNumber, string SkuCode, string DisplayName, string LotNumber, string BatchStatus, IReadOnlyList<RecallTraceReceivedViewModel> Received,
    IReadOnlyList<RecallTraceReturnedViewModel> Returned, IReadOnlyList<RecallTraceUnshippedViewModel> UnshippedDispatchBlocked, RecallTraceUnidentifiedViewModel Unidentified,
    IReadOnlyList<RecallHeldViewModel> StockHeld);
