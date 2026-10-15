namespace CatEcommerce.Read.ViewModels;

// HO-PHM-001 §12.4 -- the controlled-drug register as the staff read it. Every quantity carries its unit; the register's hashes appear ONLY on register reads.

public sealed record RegisterMedicineViewModel(Guid Id, string Name, string StrengthText, string Form, string? ControlledSchedule);

public sealed record RegisterSourceViewModel(string Type, Guid Id, string Label);

/// <summary>One register row, newest first. <c>RunningBalance</c> is the register's balance for this medicine and batch AS OF this entry (a window sum over
/// <c>entry_number</c>). <c>CustomerName</c> follows a merged customer to the survivor (ADR-152) -- the entry itself is never rewritten.</summary>
public record RegisterEntryViewModel(
    Guid Id,
    long EntryNumber,
    DateTimeOffset RecordedOn,
    DateTimeOffset OccurredOn,
    string Kind,
    RegisterMedicineViewModel Medicine,
    Guid? BatchId,
    string? LotNumber,
    DateOnly? BatchExpiresOn,
    QuantityViewModel QuantityDelta,
    QuantityViewModel RunningBalance,
    string? DoseText,
    QuantityViewModel? Wastage,
    string? CatName,
    string? CustomerName,
    string ActorName,
    string? WitnessName,
    RegisterSourceViewModel Source,
    string? CorrectionReason,
    string? Notes,
    long? CorrectsEntryNumber,
    IReadOnlyList<long> CorrectedByEntryNumbers,
    bool IsSealed);

public sealed record RegisterSealViewModel(long ChainPosition, string EntryHash, DateTimeOffset SealedOn);

/// <summary>An entry, its correction chain (every entry that corrects it, oldest first) and its seal.</summary>
public sealed record RegisterEntryDetailViewModel(RegisterEntryViewModel Entry, IReadOnlyList<RegisterEntryViewModel> Corrections, RegisterSealViewModel? Seal);

/// <summary>Per medicine and batch: the register's balance against the branch's stock (all locations, OQ-10). A difference is reported, never corrected.</summary>
public sealed record RegisterBalanceViewModel(
    Guid MedicineId, string MedicineName, Guid? BatchId, string? LotNumber, QuantityViewModel RegisterBalance, QuantityViewModel? StockBalance, bool IsMismatch);

public sealed record UnsealedEntryViewModel(long EntryNumber, DateTimeOffset RecordedOn);

public sealed record RegisterVerificationViewModel(
    bool IsIntact, int EntriesChecked, long? FirstBrokenPosition, IReadOnlyList<UnsealedEntryViewModel> StaleUnsealed, DateTimeOffset CheckedOn);

/// <summary>One CSV row of the inspection export: the entry, its running balance, its seal and its corrections' entry numbers.</summary>
public sealed record RegisterExportRow(
    long EntryNumber, DateTimeOffset RecordedOn, DateTimeOffset OccurredOn, string Kind, string MedicineName, string StrengthText, string? LotNumber, DateOnly? BatchExpiresOn,
    decimal QuantityDelta, decimal RunningBalance, string Unit, string? DoseText, decimal? Wastage, string? CatName, string ActorName, string? WitnessName, string SourceType,
    string SourceLabel, string? CorrectionReason, string? Notes, long? CorrectsEntryNumber, string CorrectedByEntryNumbers, long? ChainPosition, string? EntryHash);
