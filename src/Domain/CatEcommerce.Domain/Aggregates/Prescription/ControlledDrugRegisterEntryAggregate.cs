using CatEcommerce.Domain.Events.Prescription;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Prescription;

/// <summary>
/// HO-PHM-001 §8.9 / FR-PHM-7 (ADR-144, C-2, BR-7, D-5) -- ONE entry of the controlled-drug register. <b>Append-only: no method mutates an existing entry,
/// no property has a setter outside this class, and there is no Update, Delete or Void.</b> The only way to make one is <see cref="Record"/>; a
/// correction is a NEW entry linked to the one it corrects. Three layers keep it so: this aggregate, the EF immutability interceptor, and the
/// PostgreSQL triggers (U-D3, U-D4, I-D1). A worker later seals each entry into a per-branch hash chain (ADR-145).
///
/// <see cref="EntryNumber"/> is database-generated from a sequence (the order entries were recorded in); everything else is the caller's. The caller
/// has already verified the witness through <c>IStaffWitnessVerifier</c> -- this class checks that a witness id is present and differs from the actor
/// where the kind demands one, and that the quantity's sign matches the kind.
///
/// U-D6 pins the persisted property list against <see cref="ControlledDrugEntryCanonicalForm"/>: a new field here without a matching field there
/// fails the build.
/// </summary>
public sealed class ControlledDrugRegisterEntryAggregate : AggregateRoot
{
    public long EntryNumber { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid MedicineId { get; private set; }
    public string MedicineName { get; private set; } = default!;
    public string MedicineStrengthText { get; private set; } = default!;
    public MedicineForm MedicineForm { get; private set; } = default!;
    public string? MedicineControlledSchedule { get; private set; }
    public DispensingUnit MedicineDispensingUnit { get; private set; } = default!;
    public Guid? SkuId { get; private set; }
    public Guid? BatchId { get; private set; }
    public string? LotNumber { get; private set; }
    public DateOnly? BatchExpiresOn { get; private set; }
    public ControlledDrugEntryKind Kind { get; private set; } = default!;
    public decimal QuantityDelta { get; private set; }
    public string? DoseText { get; private set; }
    public decimal? WastageQuantity { get; private set; }
    public Guid? CatId { get; private set; }
    public Guid? CustomerId { get; private set; }
    public Guid ActorId { get; private set; }
    public Guid? WitnessId { get; private set; }
    public ControlledDrugSourceType SourceType { get; private set; } = default!;
    public Guid SourceId { get; private set; }
    public Guid? CorrectsEntryId { get; private set; }
    public string? CorrectionReason { get; private set; }
    public Guid? ReturnedEntryId { get; private set; }
    public DateTimeOffset OccurredOn { get; private set; }
    public DateTimeOffset RecordedOn { get; private set; }
    public string? Notes { get; private set; }

    private ControlledDrugRegisterEntryAggregate() { }

    private ControlledDrugRegisterEntryAggregate(Guid id) : base(id) { }

    public static ControlledDrugRegisterEntryAggregate Record(ControlledDrugRegisterEntryDto dto, RegisterStamp stamp)
    {
        ValidateQuantity(dto);
        ValidateWitness(dto);
        ValidateText(dto);
        ValidateTime(dto, stamp);

        if (dto.Kind == ControlledDrugEntryKind.Correction)
        {
            if (dto.CorrectsEntryId is null)
                throw Violation(BusinessViolationCodes.CorrectionTargetMismatch, "A correction names the entry it corrects.", nameof(dto.CorrectsEntryId));
            if (string.IsNullOrWhiteSpace(dto.CorrectionReason))
                throw Violation(BusinessViolationCodes.CorrectionReasonRequired, "A correction needs a reason.", nameof(dto.CorrectionReason));
        }

        if ((dto.Kind == ControlledDrugEntryKind.Dispensed || dto.Kind == ControlledDrugEntryKind.Administered) && (dto.CatId is null || dto.CustomerId is null))
            throw Violation(BusinessViolationCodes.InvalidRegisterEntry, "A dispensed or administered entry names the cat and its owner.", nameof(dto.CatId));

        if (dto.WastageQuantity is < 0m)
            throw Violation(BusinessViolationCodes.InvalidRegisterQuantity, "Wastage cannot be negative.", nameof(dto.WastageQuantity));

        var entry = new ControlledDrugRegisterEntryAggregate(Guid.NewGuid())
        {
            BranchId = dto.BranchId,
            MedicineId = dto.MedicineId,
            MedicineName = dto.MedicineName,
            MedicineStrengthText = dto.MedicineStrengthText,
            MedicineForm = dto.MedicineForm,
            MedicineControlledSchedule = dto.MedicineControlledSchedule,
            MedicineDispensingUnit = dto.MedicineDispensingUnit,
            SkuId = dto.SkuId,
            BatchId = dto.BatchId,
            LotNumber = dto.LotNumber,
            BatchExpiresOn = dto.BatchExpiresOn,
            Kind = dto.Kind,
            QuantityDelta = dto.QuantityDelta,
            DoseText = dto.DoseText?.Trim(),
            WastageQuantity = dto.WastageQuantity,
            CatId = dto.CatId,
            CustomerId = dto.CustomerId,
            ActorId = dto.ActorId,
            WitnessId = dto.WitnessId,
            SourceType = dto.SourceType,
            SourceId = dto.SourceId,
            CorrectsEntryId = dto.CorrectsEntryId,
            CorrectionReason = dto.CorrectionReason?.Trim(),
            ReturnedEntryId = dto.ReturnedEntryId,
            OccurredOn = dto.OccurredOn,
            RecordedOn = stamp.RecordedOn,
            Notes = dto.Notes?.Trim()
        };

        entry.SetDefaultValue(stamp.Actor.UserContext);
        entry.AddDomainEvent(new ControlledDrugRegisterEntryRecordedEvent
        {
            EntryId = entry.Id, BranchId = entry.BranchId, MedicineId = entry.MedicineId, Kind = entry.Kind,
            CorrelationId = stamp.Actor.CorrelationId, UserContext = stamp.Actor.UserContext
        });
        entry.UpdateVersion();
        return entry;
    }

    private static void ValidateQuantity(ControlledDrugRegisterEntryDto dto)
    {
        var delta = dto.QuantityDelta;
        var valid = dto.Kind == ControlledDrugEntryKind.OpeningBalance || dto.Kind == ControlledDrugEntryKind.Receipt || dto.Kind == ControlledDrugEntryKind.Returned
            ? delta > 0m
            : dto.Kind == ControlledDrugEntryKind.Dispensed || dto.Kind == ControlledDrugEntryKind.Administered || dto.Kind == ControlledDrugEntryKind.Wastage
                ? delta < 0m
                : dto.Kind == ControlledDrugEntryKind.Adjustment
                    ? delta != 0m
                    : true; // Correction: may be zero (it fixes a description, not a quantity)

        if (!valid || decimal.Round(delta, 2) != delta)
            throw Violation(BusinessViolationCodes.InvalidRegisterQuantity, $"A {dto.Kind.DisplayName} entry has the wrong sign or more than two decimal places.", nameof(dto.QuantityDelta));
    }

    private static void ValidateWitness(ControlledDrugRegisterEntryDto dto)
    {
        if (!ControlledDrugRules.RequiresWitness(dto.Kind)) return;

        if (dto.WitnessId is null || dto.WitnessId == Guid.Empty)
            throw Violation(BusinessViolationCodes.WitnessRequired, PharmacyMessages.WitnessRequired, nameof(dto.WitnessId));

        if (dto.WitnessId == dto.ActorId)
            throw Violation(BusinessViolationCodes.WitnessMustDifferFromDispenser, PharmacyMessages.WitnessMustDiffer, nameof(dto.WitnessId));
    }

    private static void ValidateText(ControlledDrugRegisterEntryDto dto)
    {
        if ((dto.DoseText?.Length ?? 0) > ControlledDrugRules.MaxDoseTextLength || (dto.Notes?.Length ?? 0) > ControlledDrugRules.MaxNotesLength
            || (dto.CorrectionReason?.Length ?? 0) > ControlledDrugRules.MaxReasonLength)
            throw Violation(BusinessViolationCodes.InvalidRegisterEntry, "A dose text is at most 200 characters; notes and reasons at most 500.", nameof(dto.Notes));
    }

    private static void ValidateTime(ControlledDrugRegisterEntryDto dto, RegisterStamp stamp)
    {
        var earliest = stamp.RecordedOn.AddHours(-ControlledDrugRules.EnlistedOccurredWindowHours);
        var latest = stamp.RecordedOn.AddMinutes(1);
        if (dto.OccurredOn < earliest || dto.OccurredOn > latest)
            throw Violation(BusinessViolationCodes.InvalidRegisterEntry, "An entry is recorded within a day of when it happened, and not before.", nameof(dto.OccurredOn));
    }

    private static DomainRuleViolationException Violation(string code, string message, string property) => new(code, message, property);
}
