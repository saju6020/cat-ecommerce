using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Models;

// HO-PHM-001 §8.9 -- the controlled-drug register's DTOs and the witness result.

/// <summary>
/// What a caller hands <c>IControlledDrugRegister.Enlist</c> -- the contract HO-SRG-001, HO-EMR-001 and HO-IMP-001 write through. The caller has
/// already verified the witness (the register cannot check a password it was never given); it checks only that a witness id is present and differs
/// from the actor where the kind demands one.
/// </summary>
public sealed record ControlledDrugRegisterEntryDto(
    Guid BranchId,
    Guid MedicineId,
    string MedicineName,
    string MedicineStrengthText,
    MedicineForm MedicineForm,
    string? MedicineControlledSchedule,
    DispensingUnit MedicineDispensingUnit,
    Guid? SkuId,
    Guid? BatchId,
    string? LotNumber,
    DateOnly? BatchExpiresOn,
    ControlledDrugEntryKind Kind,
    decimal QuantityDelta,
    string? DoseText,
    decimal? WastageQuantity,
    Guid? CatId,
    Guid? CustomerId,
    Guid ActorId,
    Guid? WitnessId,
    ControlledDrugSourceType SourceType,
    Guid SourceId,
    Guid? CorrectsEntryId,
    string? CorrectionReason,
    Guid? ReturnedEntryId,
    DateTimeOffset OccurredOn,
    string? Notes);

/// <summary>The recorded-on instant and the user the entry is stamped with -- supplied by the implementation, never by the caller.</summary>
public sealed record RegisterStamp(DateTimeOffset RecordedOn, PharmacyActor Actor);

/// <summary>ADR-146 -- the generic outcome of verifying a witness: never says which part failed.</summary>
public sealed record WitnessVerification(bool IsVerified, Guid? WitnessStaffId);

/// <summary>HO-PHM-001 §8.9 -- the chain check: recomputed from the entries, not trusted from the seals.</summary>
public sealed record RegisterVerification(
    bool IsIntact, int EntriesChecked, long? FirstBrokenPosition, IReadOnlyList<UnsealedEntryFact> StaleUnsealed, DateTimeOffset CheckedOn);

public sealed record UnsealedEntryFact(long EntryNumber, DateTimeOffset RecordedOn);
