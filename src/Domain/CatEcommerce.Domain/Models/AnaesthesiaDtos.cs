using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

// HO-SRG-001 §11.3 -- the inputs of AnaesthesiaLogEntryAggregate. One entry is created once and never changes (ADR-162).

/// <summary>What every entry carries: the surgery, the cat, the branch, when it happened and when/who logged it. <paramref name="AdmittedOn"/> bounds how early an entry may be dated.</summary>
public sealed record AnaesthesiaEntryContext(
    Guid SurgeryId, Guid CatId, Guid BranchId, DateTimeOffset? AdmittedOn, DateTimeOffset OccurredOn, DateTimeOffset Now, UserContext UserContext, Guid CorrelationId);

/// <summary>A drug given. <paramref name="IsControlled"/> and <paramref name="MedicineName"/> are snapshots of the formulary at the time. The stock fields are what the handler's stock issue
/// returned; the controlled fields what its witness verification and register enlisting returned.</summary>
public sealed record AnaesthesiaDrugDto(
    Guid MedicineId, string MedicineName, bool IsControlled, decimal DoseAmount, DoseUnit DoseUnit, AdministrationRoute Route, Guid AdministeredById,
    Guid? SkuId, Guid? BatchId, string? BatchNumber, Guid? LocationId, decimal? QuantityDrawn, decimal? QuantityWasted, string? StockUnit,
    Guid? WitnessId, Guid? RegisterEntryId, bool StockShortfall);

public sealed record AnaesthesiaVitalsDto(
    int? HeartRateBpm, int? RespiratoryRatePerMin, int? SpO2Percent, int? EtCo2MmHg, decimal? TemperatureCelsius, int? SystolicMmHg, int? MeanArterialMmHg, string? Note,
    bool ConfirmImplausible);

public sealed record AnaesthesiaMilestoneDto(AnaesthesiaMilestone Milestone, string? Note);

/// <summary>A correction: a NEW entry linked to the one it corrects, carrying the corrected values for the corrected entry's kind. Exactly one of the three payloads is set.</summary>
public sealed record AnaesthesiaCorrectionDto(
    Guid CorrectsEntryId, AnaesthesiaEntryKind CorrectedKind, string Reason, AnaesthesiaDrugDto? Drug, AnaesthesiaVitalsDto? Vitals, AnaesthesiaMilestoneDto? Milestone);
