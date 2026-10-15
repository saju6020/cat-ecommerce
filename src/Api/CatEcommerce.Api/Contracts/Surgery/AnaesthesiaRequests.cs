namespace CatEcommerce.Api.Contracts.Surgery;

/// <summary>HO-SRG-001 §12.4 request bodies -- the anaesthesia log. A witness types their OWN identifier and password into the same request (HO-PHM-001 ADR-146); the password is never echoed,
/// logged or stored.</summary>
public sealed class LogAnaesthesiaDrugRequest
{
    /// <summary>Optional idempotency key: a client-generated GUID for the entry. Send the SAME id when retrying (a double click, a lost response) and the first result is returned instead of a second entry.</summary>
    public Guid? EntryId { get; init; }

    public required Guid MedicineId { get; init; }
    public required decimal DoseAmount { get; init; }
    public required string DoseUnit { get; init; }
    public required string Route { get; init; }
    public required DateTimeOffset OccurredOn { get; init; }
    public required Guid AdministeredById { get; init; }
    public Guid? BatchId { get; init; }
    public decimal? QuantityDrawn { get; init; }
    public decimal? QuantityWasted { get; init; }
    public string? WitnessIdentifier { get; init; }
    public string? WitnessPassword { get; init; }
    public string? FefoOverrideReason { get; init; }
}

public sealed class LogAnaesthesiaVitalsRequest
{
    public required DateTimeOffset OccurredOn { get; init; }
    public int? HeartRateBpm { get; init; }
    public int? RespiratoryRatePerMin { get; init; }
    public int? SpO2Percent { get; init; }
    public int? EtCo2MmHg { get; init; }
    public decimal? TemperatureCelsius { get; init; }
    public int? SystolicMmHg { get; init; }
    public int? MeanArterialMmHg { get; init; }
    public string? Note { get; init; }
    public bool ConfirmImplausible { get; init; }
}

public sealed class LogAnaesthesiaMilestoneRequest
{
    public required string Milestone { get; init; }
    public required DateTimeOffset OccurredOn { get; init; }
    public string? Note { get; init; }
}

public sealed class CorrectAnaesthesiaEntryRequest
{
    public required string Reason { get; init; }
    public required DateTimeOffset OccurredOn { get; init; }
    public AnaesthesiaDrugCorrectionRequest? Drug { get; init; }
    public AnaesthesiaVitalsCorrectionRequest? Vitals { get; init; }
    public AnaesthesiaMilestoneCorrectionRequest? Milestone { get; init; }
    public string? WitnessIdentifier { get; init; }
    public string? WitnessPassword { get; init; }
    public string? FefoOverrideReason { get; init; }
}

public sealed class AnaesthesiaDrugCorrectionRequest
{
    public required decimal DoseAmount { get; init; }
    public required string DoseUnit { get; init; }
    public required string Route { get; init; }
    public required Guid AdministeredById { get; init; }
    public decimal? QuantityDrawn { get; init; }
    public decimal? QuantityWasted { get; init; }
}

public sealed class AnaesthesiaVitalsCorrectionRequest
{
    public int? HeartRateBpm { get; init; }
    public int? RespiratoryRatePerMin { get; init; }
    public int? SpO2Percent { get; init; }
    public int? EtCo2MmHg { get; init; }
    public decimal? TemperatureCelsius { get; init; }
    public int? SystolicMmHg { get; init; }
    public int? MeanArterialMmHg { get; init; }
    public string? Note { get; init; }
    public bool ConfirmImplausible { get; init; }
}

public sealed class AnaesthesiaMilestoneCorrectionRequest
{
    public required string Milestone { get; init; }
    public string? Note { get; init; }
}
