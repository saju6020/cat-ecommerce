using CatEcommerce.Application.Commands.Cat;
using CatEcommerce.Domain.Aggregates.Vaccination;
using CatEcommerce.Domain.Models;

namespace CatEcommerce.Application.DataMappers;

/// <summary>
/// command.UserContext and command.CorrelationId are always passed through -- that is how the
/// audit trail attributes a change (FR-ACC-12). HO-CAT-001 section 11.4.
///
/// CODE_REVIEW gate, MEDIUM (2026-09-26): every "now"/"today" value is a parameter here, threaded
/// in by the command handler from its own injected <see cref="TimeProvider"/> -- this mapper
/// never reads the wall clock itself.
/// </summary>
public static class CatMapper
{
    public static CatRegistrationDto ToDomainDto(this RegisterCatCommand command, DateOnly today) =>
        new(command.OwnerId, command.Name, command.Species, command.Sex, command.DateOfBirth,
            command.DateOfBirthPrecision, command.Breed, command.ColourAndMarkings, today,
            command.UserContext, command.CorrelationId);

    public static CatProfileUpdateDto ToDomainDto(this UpdateCatProfileCommand command, DateOnly today) =>
        new(command.Name, command.Sex, command.DateOfBirth, command.DateOfBirthPrecision,
            command.Breed, command.ColourAndMarkings, today, command.UserContext, command.CorrelationId);

    public static CatNeuterStatusDto ToDomainDto(this RecordCatNeuterStatusCommand command, DateOnly today) =>
        new(command.NeuterStatus, command.NeuteredOn, today, command.UserContext, command.CorrelationId);

    public static CatWeightRecordDto ToDomainDto(this RecordCatWeightCommand command, DateOnly today) =>
        new(command.CatId, command.Kilograms, command.RecordedOn, command.RecordedByStaffId,
            command.Notes, today, command.UserContext, command.CorrelationId);

    public static CatMedicalProfileDto ToDomainDto(this UpdateCatMedicalProfileCommand command) =>
        new(command.Allergies, command.ChronicConditions, command.Medications,
            command.DietaryNotes, command.BehaviouralNotes, command.UserContext, command.CorrelationId);

    /// <summary>HO-FIL-001 §1 Amended, gate-fix 2026-09-27: carries only the already-attached
    /// <c>FileId</c> -- content type/size/name are <c>StoredFileAggregate</c>'s own state, read at
    /// serve time, never copied here (closes the HO-CAT-002-SECURITY carried item and the
    /// ARCHITECTURE HIGH finding that this DTO used to copy them).</summary>
    public static CatPhotoDto ToDomainDto(this AddCatPhotoCommand command, DateTimeOffset uploadedOn) =>
        new(command.PhotoId, command.FileId, command.IsPrimary, uploadedOn,
            command.UserContext, command.CorrelationId);

    public static CatHighRiskFlagDto ToDomainDto(this FlagCatHighRiskCommand command) =>
        new(command.HandlingNote, command.Reason, command.UserContext, command.CorrelationId);

    public static CatHighRiskClearDto ToDomainDto(this ClearCatHighRiskFlagCommand command) =>
        new(command.Reason, command.UserContext, command.CorrelationId);

    /// <summary>vaccine is the read-only VaccineAggregate lookup -- IsCore/ValidityMonths travel
    /// in the DTO; the CatAggregate computes NextDueOn.</summary>
    public static CatVaccinationDto ToDomainDto(this RecordCatVaccinationCommand command, VaccineAggregate vaccine, DateOnly today) =>
        new(command.VaccinationId, command.VaccineId, vaccine.Code, vaccine.Name, vaccine.IsCore,
            vaccine.ValidityMonths, command.BatchReference, command.BatchExpiresOn,
            command.AdministeredOn, command.AdministeredByVetId, command.Notes, today,
            command.UserContext, command.CorrelationId);

    public static CatParasiteTreatmentDto ToDomainDto(this RecordCatParasiteTreatmentCommand command, DateOnly today) =>
        new(command.TreatmentId, command.TreatmentType, command.ProductName, command.BatchReference,
            command.AdministeredOn, command.AdministeredByStaffId, command.IntervalDays,
            command.Notes, today, command.UserContext, command.CorrelationId);

    public static CatDeceasedDto ToDomainDto(this MarkCatDeceasedCommand command, DateOnly today) =>
        new(command.DeceasedOn, command.Note, today, command.UserContext, command.CorrelationId);

    public static CatOwnershipTransferDto ToDomainDto(this TransferCatOwnershipCommand command) =>
        new(command.NewOwnerId, command.Reason, command.UserContext, command.CorrelationId);
}
