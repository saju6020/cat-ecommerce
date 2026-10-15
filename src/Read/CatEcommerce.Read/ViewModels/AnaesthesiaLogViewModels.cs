namespace CatEcommerce.Read.ViewModels;

// HO-SRG-001 §12.4 -- anaesthesia log (US-SRG-04, US-SRG-08).

/// <summary>The values an entry carries, whichever its kind: a drug, a set of vitals or a milestone. Fields of the other kinds are null.</summary>
public sealed record AnaesthesiaValuesViewModel(
    Guid? MedicineId, string? MedicineName, bool IsControlled, decimal? DoseAmount, string? DoseUnit, string? Route, Guid? AdministeredById, string? AdministeredByName,
    string? BatchNumber, decimal? QuantityDrawn, decimal? QuantityWasted, string? StockUnit, Guid? WitnessId, string? WitnessName, Guid? RegisterEntryId, bool StockShortfall,
    int? HeartRateBpm, int? RespiratoryRatePerMin, int? SpO2Percent, int? EtCo2MmHg, decimal? TemperatureCelsius, int? SystolicMmHg, int? MeanArterialMmHg, bool ImplausibleConfirmed,
    string? Milestone, string? Note);

/// <summary><c>Values</c> are what this entry itself recorded; <c>Effective</c> are the values after every correction of it (the leaf of its chain). A corrected entry stays visible,
/// marked <c>isCorrected</c>, pointing at <c>correctedByEntryId</c>; a correction points back with <c>correctsEntryId</c>.</summary>
public sealed record AnaesthesiaEntryViewModel(
    Guid Id, string Kind, DateTimeOffset OccurredOn, DateTimeOffset LoggedOn, Guid LoggedBy, string LoggedByName, AnaesthesiaValuesViewModel Values, AnaesthesiaValuesViewModel Effective,
    bool IsCorrected, Guid? CorrectedByEntryId, Guid? CorrectsEntryId, string? CorrectedKind, string? CorrectionReason, decimal? EffectiveDoseMgPerKg);

public sealed record AnaesthesiaLogViewModel(
    Guid SurgeryId, Guid CatId, string BranchTimeZoneId, bool IsOpen, SurgeryLatestWeightViewModel? LatestWeight, IReadOnlyList<AnaesthesiaEntryViewModel> Entries);
