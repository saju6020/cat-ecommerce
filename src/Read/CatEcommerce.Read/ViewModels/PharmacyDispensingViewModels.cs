namespace CatEcommerce.Read.ViewModels;

// HO-PHM-001 §12.4 -- dispensing. Every quantity carries its unit (UI-8).

public sealed record PlanCatViewModel(Guid Id, string Name, bool IsHighRisk, string? HandlingNote);

/// <summary>One batch at the branch's clinic dispensary as the dispense screen shows it. An expired batch is listed (so the dispenser sees why) but is never suggested and never
/// accepted -- there is no override anywhere (BR-3).</summary>
public sealed record DispensePlanBatchViewModel(
    Guid SkuId, string ProductName, string VariantLabel, Guid BatchId, string LotNumber, string? Barcode, DateOnly? ExpiresOn, QuantityViewModel Available,
    bool IsExpired, bool IsDispensable, string? NotDispensableReason);

public sealed record SuggestedLineViewModel(Guid SkuId, Guid BatchId, QuantityViewModel Quantity);

/// <summary>The server's FEFO plan for a prescription (§12.4): the client adds nothing to it. <c>CanDispense</c> and <c>BlockingReason</c> come from the same rules the command
/// applies, so the screen never offers a button the command would refuse.</summary>
public sealed record DispensePlanViewModel(
    PrescriptionSummaryViewModel Prescription,
    PlanCatViewModel Cat,
    QuantityViewModel Quantity,
    bool IsControlled,
    IReadOnlyList<string> WitnessRoles,
    bool CanDispense,
    string? BlockingReason,
    IReadOnlyList<DispensePlanBatchViewModel> Batches,
    IReadOnlyList<SuggestedLineViewModel> SuggestedLines,
    bool OnlyExpiredStockAvailable,
    QuantityViewModel? Shortfall);

public sealed record MedicineLabelViewModel(
    string CatName, string OwnerName, string MedicineLine, string DoseLine, string Instructions, DateOnly DispensedOn, DateOnly? ExpiresOn, string QuantityLine,
    string PrescriptionNumber, string PrescriberName, string ClinicName, string ClinicPhone, string? ClinicAddress, string? ControlledNotice, string StandardWarnings);

public sealed record DispensingLineViewModel(
    Guid LineId, Guid SkuId, Guid BatchId, string LotNumber, DateOnly? ExpiresOn, QuantityViewModel Quantity, QuantityViewModel? UnitPrice, string? CurrencyCode, bool IsPriced);

public sealed record DispensingViewModel(
    Guid Id,
    Guid PrescriptionId,
    Guid CatId,
    string PrescriptionNumber,
    int SupplyNumber,
    bool IsRepeat,
    string Status,
    DateTimeOffset DispensedOn,
    DateOnly DispensedOnLocal,
    string DispensedByName,
    string? WitnessName,
    string? FefoOverrideReason,
    IReadOnlyList<DispensingLineViewModel> Lines,
    MedicineLabelViewModel Label,
    int LabelPrintCount,
    DateTimeOffset? ReversedOn,
    string? ReversalReason,
    IReadOnlyList<string> AllowedActions);

/// <summary>A prescription waiting at the counter (the pharmacy queue): the summary and who it is for.</summary>
public sealed record PharmacyQueueItemViewModel(PrescriptionSummaryViewModel Prescription, string CatName, bool CatIsHighRisk, string CustomerName);

public sealed record PharmacyQueueRepeatViewModel(
    Guid RequestId, Guid PrescriptionId, string PrescriptionNumber, string CatName, string CustomerName, PrescriptionMedicineViewModel Medicine, DateTimeOffset RequestedOn, string? Note);

public sealed record PharmacyQueueViewModel(
    DateTimeOffset GeneratedOn, IReadOnlyList<PharmacyQueueItemViewModel> AwaitingFirstSupply, IReadOnlyList<PharmacyQueueRepeatViewModel> RepeatRequests);

public sealed record PrescriptionLookupViewModel(Guid PrescriptionId);
