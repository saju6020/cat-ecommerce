using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Models;

// HO-PHM-001 §8.7 -- dispensing: the planner's inputs and outputs (pure), and the dispensing aggregate's DTOs.

/// <summary>One batch offered at the branch's clinic dispensary, as the planner sees it (Inventory's facts, never Pharmacy's own).</summary>
public sealed record BatchOffer(
    Guid SkuId, Guid BatchId, string LotNumber, DateOnly? ExpiresOn, decimal Available, bool IsDispensable, string? NotDispensableReason, Guid LocationId);

/// <summary>A line the dispenser chose (or the server suggested).</summary>
public sealed record DispenseLineInput(Guid SkuId, Guid BatchId, decimal Quantity);

public sealed record DispensePlan(
    IReadOnlyList<DispenseLineInput> SuggestedLines,
    bool OnlyExpiredStockAvailable,
    decimal TotalDispensable,
    decimal Shortfall,
    BatchOffer? ExpiredExample);

/// <summary>One reason a requested dispensing cannot go ahead -- the first is returned to the caller.</summary>
public sealed record DispenseViolation(string Code, string PropertyName, string Message);

public sealed record MedicineLabelInput(
    string CatName,
    string OwnerName,
    string MedicineName,
    string StrengthText,
    bool IsControlled,
    Dose Dose,
    AdministrationRoute Route,
    DoseFrequency Frequency,
    int DurationDays,
    string Instructions,
    DispensingUnit DispensingUnit,
    decimal Quantity,
    DateOnly DispensedOnLocal,
    IReadOnlyList<DateOnly?> LineExpiries,
    string PrescriptionNumber,
    string PrescriberName,
    string ClinicName,
    string ClinicPhone,
    string? ClinicAddress);

public sealed record DispensingLineInput(
    Guid LineId, Guid SkuId, Guid BatchId, string LotNumber, DateOnly? ExpiresOn, decimal Quantity, Guid? RegisterEntryId, PriceSnapshot? Price);

public sealed record DispensingCreateDto(
    Guid PrescriptionId,
    string PrescriptionNumber,
    Guid BranchId,
    Guid CatId,
    Guid CustomerId,
    Guid VisitId,
    int SupplyNumber,
    bool IsRepeat,
    Guid? RepeatRequestId,
    Guid DispensedBy,
    DateTimeOffset DispensedOn,
    DateOnly DispensedOnLocal,
    Guid? WitnessId,
    string? FefoOverrideReason,
    IReadOnlyList<DispensingLineInput> Lines,
    MedicineLabelSnapshot Label,
    PharmacyActor Actor);

/// <summary>HO-PHM-001 §8.11 -- the pure refill schedule: when the next supply falls due and from when the owner is reminded.</summary>
public sealed record RefillSchedule(DateOnly RefillDueOn, DateOnly RemindFrom);

/// <summary>One line of <c>PrescriptionDispensedEvent</c>: the batch given and the price snapshot for Billing (HO-BIL-001's contract). Lives here, not in the
/// events namespace, because an event itself carries no strings (U-X1) and this nested record carries the currency code.</summary>
public sealed record DispensedLineFact(Guid SkuId, Guid BatchId, decimal Quantity, decimal? UnitPrice, string? CurrencyCode);
