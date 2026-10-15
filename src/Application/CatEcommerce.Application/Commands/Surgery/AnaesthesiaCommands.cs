using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Surgery;

// HO-SRG-001 §11.1 C8-C10 (US-SRG-04, US-SRG-08). Ids are minted in the controller (the entry id, the register entry and the ledger entry are all derived from it). A witness is a PERSON:
// the second member of staff types their OWN identifier and password into this request (HO-PHM-001 ADR-146). The password lives on the command only -- never in a log, an event, audit capture
// or the outbox.

/// <summary>C8 -- a drug in the anaesthesia log. For a medicine with a stocked SKU the batch and the quantity drawn are required, and the stock is taken from THAT batch in the same save.</summary>
public sealed class LogAnaesthesiaDrugCommand : Command
{
    public required Guid EntryId { get; init; }
    public required Guid SurgeryId { get; init; }
    public required Guid BranchId { get; init; }
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

    /// <summary>Required (5+ characters) when the named batch is not the first-expired-first-out choice (HO-IMP-001 rule 5).</summary>
    public string? FefoOverrideReason { get; init; }

    /// <summary>Never includes the witness identifier or password: this object can reach a log.</summary>
    public override string ToString() => $"{nameof(LogAnaesthesiaDrugCommand)} {{ EntryId = {EntryId}, SurgeryId = {SurgeryId}, BranchId = {BranchId} }}";
}

public sealed class LogAnaesthesiaVitalsCommand : Command
{
    public required Guid EntryId { get; init; }
    public required Guid SurgeryId { get; init; }
    public required Guid BranchId { get; init; }
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

public sealed class LogAnaesthesiaMilestoneCommand : Command
{
    public required Guid EntryId { get; init; }
    public required Guid SurgeryId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Milestone { get; init; }
    public required DateTimeOffset OccurredOn { get; init; }
    public string? Note { get; init; }
}

/// <summary>C10 -- a correction of an entry: a NEW entry linked to it. For a drug, the stock follows the change in quantity (a further issue or a return, same batch) and a controlled drug
/// writes a NEW register entry linked to the original, with a NEW witness.</summary>
public sealed class CorrectAnaesthesiaEntryCommand : Command
{
    public required Guid EntryId { get; init; }
    public required Guid SurgeryId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid CorrectsEntryId { get; init; }
    public required string Reason { get; init; }
    public required DateTimeOffset OccurredOn { get; init; }
    public AnaesthesiaDrugCorrectionInput? Drug { get; init; }
    public AnaesthesiaVitalsCorrectionInput? Vitals { get; init; }
    public AnaesthesiaMilestoneCorrectionInput? Milestone { get; init; }
    public string? WitnessIdentifier { get; init; }
    public string? WitnessPassword { get; init; }
    public string? FefoOverrideReason { get; init; }

    /// <summary>Never includes the witness identifier or password: this object can reach a log.</summary>
    public override string ToString() => $"{nameof(CorrectAnaesthesiaEntryCommand)} {{ EntryId = {EntryId}, SurgeryId = {SurgeryId}, CorrectsEntryId = {CorrectsEntryId}, BranchId = {BranchId} }}";
}

public sealed record AnaesthesiaDrugCorrectionInput(
    decimal DoseAmount, string DoseUnit, string Route, Guid AdministeredById, decimal? QuantityDrawn, decimal? QuantityWasted);

public sealed record AnaesthesiaVitalsCorrectionInput(
    int? HeartRateBpm, int? RespiratoryRatePerMin, int? SpO2Percent, int? EtCo2MmHg, decimal? TemperatureCelsius, int? SystolicMmHg, int? MeanArterialMmHg, string? Note, bool ConfirmImplausible);

public sealed record AnaesthesiaMilestoneCorrectionInput(string Milestone, string? Note);
