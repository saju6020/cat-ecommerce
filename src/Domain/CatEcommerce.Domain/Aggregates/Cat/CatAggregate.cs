using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Cat;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Cat;

/// <summary>
/// The record every other business line hangs off (.ai/bounded-contexts.md). One aggregate
/// mutated per command, without exception -- HO-CAT-001 section 8.1.
/// </summary>
public sealed class CatAggregate : AggregateRoot
{
    private readonly List<CatPhoto> _photos = [];
    private readonly List<CatAllergy> _allergies = [];
    private readonly List<CatChronicCondition> _chronicConditions = [];
    private readonly List<CatMedication> _medications = [];
    private readonly List<CatVaccination> _vaccinations = [];
    private readonly List<CatParasiteTreatment> _parasiteTreatments = [];

    public Guid OwnerId { get; private set; }

    /// <summary>How many times the owner has changed (transfer or merge reassignment). Part of the
    /// care-reminder dedup key so an A-B-A round trip is a new key (NTF-001 backlog row 1).</summary>
    public int OwnershipGeneration { get; private set; }
    public string Name { get; private set; } = default!;
    public string Species { get; private set; } = "Cat";
    public CatSex Sex { get; private set; } = default!;
    public DateOnly? DateOfBirth { get; private set; }
    public DateOfBirthPrecision DateOfBirthPrecision { get; private set; } = default!;
    public string? Breed { get; private set; }
    public string? ColourAndMarkings { get; private set; }
    public NeuterStatus NeuterStatus { get; private set; } = NeuterStatus.Unknown;
    public DateOnly? NeuteredOn { get; private set; }
    public string? DietaryNotes { get; private set; }
    public string? BehaviouralNotes { get; private set; }
    public bool IsHighRisk { get; private set; }
    public string? HandlingNote { get; private set; }
    public CatLifeStatus LifeStatus { get; private set; } = CatLifeStatus.Alive;
    public DateOnly? DeceasedOn { get; private set; }
    public string? DeceasedNote { get; private set; }

    public IReadOnlyList<CatPhoto> Photos => _photos;
    public IReadOnlyList<CatAllergy> Allergies => _allergies;
    public IReadOnlyList<CatChronicCondition> ChronicConditions => _chronicConditions;
    public IReadOnlyList<CatMedication> Medications => _medications;
    public IReadOnlyList<CatVaccination> Vaccinations => _vaccinations;
    public IReadOnlyList<CatParasiteTreatment> ParasiteTreatments => _parasiteTreatments;

    private CatAggregate() { }

    public CatAggregate(Guid id) : base(id) { }

    public void Register(CatRegistrationDto dto)
    {
        ValidateName(dto.Name);
        ValidateDateOfBirth(dto.DateOfBirth, dto.DateOfBirthPrecision, dto.Today);

        OwnerId = dto.OwnerId;
        Name = dto.Name;
        Species = dto.Species;
        Sex = dto.Sex;
        DateOfBirth = dto.DateOfBirth;
        DateOfBirthPrecision = dto.DateOfBirthPrecision;
        Breed = dto.Breed;
        ColourAndMarkings = dto.ColourAndMarkings;

        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new CatRegisteredEvent
        {
            CatId = Id,
            OwnerId = OwnerId,
            Name = Name,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    public void UpdateProfile(CatProfileUpdateDto dto)
    {
        EnsureAlive();
        ValidateName(dto.Name);
        ValidateDateOfBirth(dto.DateOfBirth, dto.DateOfBirthPrecision, dto.Today);

        Name = dto.Name;
        Sex = dto.Sex;
        DateOfBirth = dto.DateOfBirth;
        DateOfBirthPrecision = dto.DateOfBirthPrecision;
        Breed = dto.Breed;
        ColourAndMarkings = dto.ColourAndMarkings;

        AddDomainEvent(new CatProfileUpdatedEvent
        {
            CatId = Id,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    public void RecordNeuterStatus(CatNeuterStatusDto dto)
    {
        EnsureAlive();

        if (dto.NeuterStatus == NeuterStatus.Neutered)
        {
            if (dto.NeuteredOn is null)
                throw new DomainRuleViolationException(
                    BusinessViolationCodes.NeuterDateRequired,
                    "A neuter date is required when neuter status is Neutered.",
                    nameof(dto.NeuteredOn));

            if (dto.NeuteredOn > dto.Today)
                throw new DomainRuleViolationException(
                    BusinessViolationCodes.NeuterDateInFuture,
                    "The neuter date cannot be in the future.",
                    nameof(dto.NeuteredOn));

            if (DateOfBirth is not null && dto.NeuteredOn < DateOfBirth)
                throw new DomainRuleViolationException(
                    BusinessViolationCodes.NeuterDateBeforeDateOfBirth,
                    "The neuter date cannot be before the cat's date of birth.",
                    nameof(dto.NeuteredOn));
        }

        NeuterStatus = dto.NeuterStatus;
        NeuteredOn = dto.NeuteredOn;

        AddDomainEvent(new CatNeuterStatusRecordedEvent
        {
            CatId = Id,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    public void UpdateMedicalProfile(CatMedicalProfileDto dto)
    {
        EnsureAlive();

        _allergies.Clear();
        _allergies.AddRange(dto.Allergies.Select(a =>
            new CatAllergy(Guid.NewGuid(), a.Allergen, a.Reaction, a.Severity, a.RecordedOn)));

        _chronicConditions.Clear();
        _chronicConditions.AddRange(dto.ChronicConditions.Select(c =>
            new CatChronicCondition(Guid.NewGuid(), c.Condition, c.DiagnosedOn, c.Notes)));

        _medications.Clear();
        _medications.AddRange(dto.Medications.Select(m =>
            new CatMedication(Guid.NewGuid(), m.MedicationName, m.Dosage, m.Frequency, m.StartedOn, m.EndsOn)));

        DietaryNotes = dto.DietaryNotes;
        BehaviouralNotes = dto.BehaviouralNotes;

        AddDomainEvent(new CatMedicalProfileUpdatedEvent
        {
            CatId = Id,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    /// <summary>
    /// Gate-fix 2026-09-27 (ARCHITECTURE HIGH): <paramref name="dto"/> carries only the already-
    /// verified <see cref="CatPhotoDto.FileId"/> -- content type and size are
    /// <c>StoredFileAggregate</c>'s own state now (checked there, at upload time and again inside
    /// its own <c>Register</c>), not copied here, so this aggregate has nothing left to re-check
    /// on that front. The one invariant that is still this aggregate's own state is the photo
    /// count.
    /// </summary>
    public void AddPhoto(CatPhotoDto dto)
    {
        EnsureAlive();

        if (_photos.Count >= CatPhotoRules.MaxPhotosPerCat)
            throw new DomainRuleViolationException(
                BusinessViolationCodes.PhotoLimitExceeded,
                $"A cat cannot have more than {CatPhotoRules.MaxPhotosPerCat} photos.",
                nameof(dto.PhotoId));

        var isPrimary = _photos.Count == 0 || dto.IsPrimary;
        if (isPrimary)
            foreach (var existing in _photos)
                existing.ClearPrimary();

        _photos.Add(new CatPhoto(dto.PhotoId, dto.FileId, isPrimary, dto.UploadedOn));

        AddDomainEvent(new CatPhotoAddedEvent
        {
            CatId = Id,
            PhotoId = dto.PhotoId,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    public void FlagHighRisk(CatHighRiskFlagDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.HandlingNote))
            throw new DomainRuleViolationException(
                BusinessViolationCodes.HandlingNoteRequired,
                "A handling note is required to flag a cat as high-risk.",
                nameof(dto.HandlingNote));

        if (string.IsNullOrWhiteSpace(dto.Reason))
            throw new DomainRuleViolationException(
                BusinessViolationCodes.ReasonRequired,
                "A reason is required to flag a cat as high-risk.",
                nameof(dto.Reason));

        IsHighRisk = true;
        HandlingNote = dto.HandlingNote;

        AddDomainEvent(new CatFlaggedHighRiskEvent
        {
            CatId = Id,
            HandlingNote = dto.HandlingNote,
            Reason = dto.Reason,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    public void ClearHighRiskFlag(CatHighRiskClearDto dto)
    {
        if (!IsHighRisk)
            throw new DomainRuleViolationException(
                BusinessViolationCodes.CatNotFlaggedHighRisk,
                "This cat is not currently flagged high-risk.",
                nameof(IsHighRisk));

        if (string.IsNullOrWhiteSpace(dto.Reason))
            throw new DomainRuleViolationException(
                BusinessViolationCodes.ReasonRequired,
                "A reason is required to clear the high-risk flag.",
                nameof(dto.Reason));

        IsHighRisk = false;
        HandlingNote = null;

        AddDomainEvent(new CatHighRiskFlagClearedEvent
        {
            CatId = Id,
            Reason = dto.Reason,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    /// <summary>
    /// HO-BRD-001 section 8.6 (ADR-222, renumbered ADR-355): the cat's core-vaccination status as of <paramref name="asOf"/>, by the shared
    /// <see cref="DomainServices.VaccinationStatusRule"/>. <b>Read-only</b>: no mutation, no event, no <c>UpdateVersion()</c>. This is
    /// ".ai/bounded-contexts.md": "CatAggregate exposes vaccination status; the boarding command handler checks it".
    /// </summary>
    public DomainServices.VaccinationEvaluation EvaluateVaccinationStatus(
        IReadOnlyList<DomainServices.VaccinationRuleVaccine> activeCoreVaccines, DateOnly asOf, int dueSoonDays) =>
        DomainServices.VaccinationStatusRule.Evaluate(
            activeCoreVaccines,
            _vaccinations.Select(v => new DomainServices.VaccinationRuleRecord(v.VaccineId, v.AdministeredOn, v.NextDueOn)).ToList(),
            asOf, dueSoonDays);

    public void RecordVaccination(CatVaccinationDto dto)
    {
        EnsureAlive();

        if (dto.AdministeredOn > dto.Today)
            throw new DomainRuleViolationException(
                BusinessViolationCodes.VaccinationDateInFuture,
                "The vaccination date cannot be in the future.",
                nameof(dto.AdministeredOn));

        if (DateOfBirth is not null && dto.AdministeredOn < DateOfBirth)
            throw new DomainRuleViolationException(
                BusinessViolationCodes.VaccinationDateBeforeDateOfBirth,
                "The vaccination date cannot be before the cat's date of birth.",
                nameof(dto.AdministeredOn));

        if (dto.BatchExpiresOn < dto.AdministeredOn)
            throw new DomainRuleViolationException(
                BusinessViolationCodes.ExpiredVaccineBatch,
                "This vaccine batch had already expired on the administration date.",
                nameof(dto.BatchExpiresOn));

        if (_vaccinations.Any(v => v.VaccineId == dto.VaccineId && v.AdministeredOn == dto.AdministeredOn))
            throw new DomainRuleViolationException(
                BusinessViolationCodes.DuplicateVaccinationOnDate,
                "This vaccine has already been recorded for this cat on this date.",
                nameof(dto.AdministeredOn));

        var nextDueOn = dto.AdministeredOn.AddMonths(dto.ValidityMonths);

        _vaccinations.Add(new CatVaccination(
            dto.VaccinationId, dto.VaccineId, dto.VaccineCode, dto.VaccineName, dto.IsCore,
            dto.BatchReference, dto.BatchExpiresOn, dto.AdministeredOn, dto.AdministeredByVetId,
            nextDueOn, dto.Notes));

        AddDomainEvent(new CatVaccinationRecordedEvent
        {
            CatId = Id,
            VaccinationId = dto.VaccinationId,
            VaccineId = dto.VaccineId,
            NextDueOn = nextDueOn,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    public void RecordParasiteTreatment(CatParasiteTreatmentDto dto)
    {
        EnsureAlive();

        if (dto.AdministeredOn > dto.Today)
            throw new DomainRuleViolationException(
                BusinessViolationCodes.ParasiteTreatmentDateInFuture,
                "The treatment date cannot be in the future.",
                nameof(dto.AdministeredOn));

        var nextDueOn = dto.AdministeredOn.AddDays(dto.IntervalDays);

        _parasiteTreatments.Add(new CatParasiteTreatment(
            dto.TreatmentId, dto.TreatmentType, dto.ProductName, dto.BatchReference,
            dto.AdministeredOn, dto.AdministeredByStaffId, nextDueOn, dto.Notes));

        AddDomainEvent(new CatParasiteTreatmentRecordedEvent
        {
            CatId = Id,
            TreatmentId = dto.TreatmentId,
            NextDueOn = nextDueOn,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    public void MarkDeceased(CatDeceasedDto dto)
    {
        if (LifeStatus == CatLifeStatus.Deceased)
            throw new DomainRuleViolationException(
                BusinessViolationCodes.CatAlreadyDeceased,
                "This cat is already recorded as deceased.",
                nameof(LifeStatus));

        if (dto.DeceasedOn > dto.Today)
            throw new DomainRuleViolationException(
                BusinessViolationCodes.DeceasedDateInFuture,
                "The deceased date cannot be in the future.",
                nameof(dto.DeceasedOn));

        if (DateOfBirth is not null && dto.DeceasedOn < DateOfBirth)
            throw new DomainRuleViolationException(
                BusinessViolationCodes.DeceasedDateBeforeDateOfBirth,
                "The deceased date cannot be before the cat's date of birth.",
                nameof(dto.DeceasedOn));

        LifeStatus = CatLifeStatus.Deceased;
        DeceasedOn = dto.DeceasedOn;
        DeceasedNote = dto.Note;

        AddDomainEvent(new CatMarkedDeceasedEvent
        {
            CatId = Id,
            DeceasedOn = dto.DeceasedOn,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    public void TransferOwnership(CatOwnershipTransferDto dto)
    {
        EnsureAlive();

        if (dto.NewOwnerId == OwnerId)
            throw new DomainRuleViolationException(
                BusinessViolationCodes.OwnerUnchanged,
                "The new owner is the same as the current owner.",
                nameof(dto.NewOwnerId));

        if (string.IsNullOrWhiteSpace(dto.Reason))
            throw new DomainRuleViolationException(
                BusinessViolationCodes.ReasonRequired,
                "A reason is required to transfer ownership.",
                nameof(dto.Reason));

        var previousOwnerId = OwnerId;
        OwnerId = dto.NewOwnerId;
        OwnershipGeneration++;

        AddDomainEvent(new CatOwnershipTransferredEvent
        {
            CatId = Id,
            PreviousOwnerId = previousOwnerId,
            NewOwnerId = dto.NewOwnerId,
            Reason = dto.Reason,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    /// <summary>
    /// HO-ACC-001 §8.9. The one Cat change this slice makes. Not <see cref="TransferOwnership"/>:
    /// a merge is not a change of person, and a future subscriber to
    /// <see cref="CatOwnershipTransferredEvent"/> ("notify the new owner") must not fire on one.
    /// Deliberately does not call <see cref="EnsureAlive"/> -- a deceased cat's history must move
    /// too (FR-CAT-9 "retained").
    /// </summary>
    public void ReassignOwnerOnCustomerMerge(CatOwnerMergeReassignmentDto dto)
    {
        if (OwnerId != dto.DuplicateCustomerId)
            throw new DomainRuleViolationException(
                BusinessViolationCodes.CatOwnerMismatch,
                "This cat is not currently owned by the duplicate customer record.",
                nameof(OwnerId));

        if (dto.SurvivingCustomerId == OwnerId)
            throw new DomainRuleViolationException(
                BusinessViolationCodes.OwnerUnchanged,
                "The surviving owner is the same as the current owner.",
                nameof(dto.SurvivingCustomerId));

        var previousOwnerId = OwnerId;
        OwnerId = dto.SurvivingCustomerId;
        OwnershipGeneration++;

        AddDomainEvent(new CatOwnerReassignedOnMergeEvent
        {
            CatId = Id,
            PreviousOwnerId = previousOwnerId,
            NewOwnerId = dto.SurvivingCustomerId,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    /// <summary>
    /// A deceased cat's record is read-only (FR-CAT-9): retained and viewable, never edited.
    /// One private guard, per HO-CAT-001 section 8.2.
    /// </summary>
    private void EnsureAlive()
    {
        if (LifeStatus == CatLifeStatus.Deceased)
            throw new DomainRuleViolationException(
                BusinessViolationCodes.CatIsDeceased,
                "This cat's record is deceased and cannot be edited.",
                nameof(LifeStatus));
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainRuleViolationException(
                BusinessViolationCodes.NameRequired,
                "A cat's name is required.",
                nameof(Name));
    }

    // Property name matches the DTO/Command's DateOfBirth (PascalCase), not the lowercase
    // parameter below -- this is what a caller's DomainRuleViolationException.PropertyName
    // must line up with.
    //
    // CODE_REVIEW gate, MEDIUM (2026-09-26): "today" is threaded in from the handler's
    // TimeProvider (the same seam HO-ACC-001 uses -- see MergeCustomerCommandHandler), never
    // read from the wall clock here. That is what makes this boundary pinnable in a test.
    private static void ValidateDateOfBirth(DateOnly? dateOfBirth, DateOfBirthPrecision precision, DateOnly today)
    {
        if (dateOfBirth is null && precision == DateOfBirthPrecision.Exact)
            throw new DomainRuleViolationException(
                BusinessViolationCodes.DateOfBirthRequired,
                "A date of birth is required when precision is Exact.",
                nameof(DateOfBirth));

        if (dateOfBirth > today)
            throw new DomainRuleViolationException(
                BusinessViolationCodes.DateOfBirthInFuture,
                "The date of birth cannot be in the future.",
                nameof(DateOfBirth));
    }
}
