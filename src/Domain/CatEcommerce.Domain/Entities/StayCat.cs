using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Entities;

/// <summary>One scheduled feed of a cat's care profile. Owned by <see cref="StayCat"/>; the list is replaced wholesale by <c>SetCareProfile</c>.</summary>
public sealed class StayCatFeeding
{
    public Guid Id { get; private set; }
    public TimeOnly Time { get; private set; }
    public string Portion { get; private set; } = default!;
    public FoodSource FoodSource { get; private set; } = default!;
    public string FoodDescription { get; private set; } = default!;
    public Guid? HouseFoodSkuId { get; private set; }

    private StayCatFeeding() { }

    public StayCatFeeding(Guid id, FeedingPlan plan)
    {
        Id = id;
        Time = plan.Time;
        Portion = plan.Portion;
        FoodSource = plan.FoodSource;
        FoodDescription = plan.FoodDescription;
        HouseFoodSkuId = plan.HouseFoodSkuId;
    }

    public FeedingPlan ToPlan() => new(Time, Portion, FoodSource, FoodDescription, HouseFoodSkuId);
}

public sealed class StayCatMedication
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string Dose { get; private set; } = default!;
    public string Route { get; private set; } = default!;
    public TimeOnly[] Times { get; private set; } = [];
    public string? Instructions { get; private set; }
    public DateOnly? FromOn { get; private set; }
    public DateOnly? ToOn { get; private set; }

    private StayCatMedication() { }

    public StayCatMedication(Guid id, MedicationPlan plan)
    {
        Id = id;
        Name = plan.Name;
        Dose = plan.Dose;
        Route = plan.Route;
        Times = plan.Times.OrderBy(t => t).ToArray();
        Instructions = plan.Instructions;
        FromOn = plan.FromOn;
        ToOn = plan.ToOn;
    }

    public MedicationPlan ToPlan() => new(Name, Dose, Route, Times, Instructions, FromOn, ToOn);
}

/// <summary>
/// HO-BRD-001 section 8.7 -- one cat on a reservation. Owned by the stay (at most six). It carries the cat's own occupancy range and <see cref="IsActive"/>
/// denormalised from the stay (F31: the exclusion constraint <c>ex_stay_cat</c> sees only its own row), the vaccination gate outcome, and the care profile
/// (flattened here; <see cref="Feedings"/> and <see cref="Medications"/> are owned lists). A cat's profile is <b>complete</b> once one has been submitted --
/// its parts are validated by the stay when set.
/// </summary>
public sealed class StayCat
{
    private readonly List<StayCatFeeding> _feedings = [];
    private readonly List<StayCatMedication> _medications = [];
    private readonly List<StayCatBelonging> _belongings = [];

    public Guid Id { get; private set; }
    public Guid CatId { get; private set; }

    /// <summary>The cat's name at the time of booking -- a snapshot for lists, messages and the dashboard.</summary>
    public string CatName { get; private set; } = default!;

    public DateTimeOffset OccupiedFrom { get; private set; }
    public DateTimeOffset OccupiedTo { get; private set; }
    public bool IsActive { get; private set; }

    public VaccinationGateStatus GateStatus { get; private set; } = VaccinationGateStatus.Passed;
    public IReadOnlyList<GateReason> GateReasons { get; private set; } = [];
    public DateOnly? GateEvaluatedOn { get; private set; }
    public Guid? GateOverriddenBy { get; private set; }
    public DateTimeOffset? GateOverriddenOn { get; private set; }
    public string? GateOverrideReason { get; private set; }

    public bool HasCareProfile { get; private set; }
    public LitterType? Litter { get; private set; }
    public string? LitterNote { get; private set; }
    public bool VetVisitAuthorised { get; private set; }
    public decimal VetSpendingLimit { get; private set; }
    public string? EmergencyContactName { get; private set; }
    public string? EmergencyContactPhone { get; private set; }
    public string? EmergencyContactRelationship { get; private set; }
    public string? CareNotes { get; private set; }

    /// <summary>The reason staff gave for the LAST change of the vet authorisation or limit after reservation; the audit handler reads it back (an event carries no text).</summary>
    public string? VetAuthorityChangeReason { get; private set; }

    public IReadOnlyList<StayCatFeeding> Feedings => _feedings;
    public IReadOnlyList<StayCatMedication> Medications => _medications;

    // ── check-in (US-BRD-08) ────────────────────────────────────────────────────────────────────────────────────────
    public DateTimeOffset? CheckedInOn { get; private set; }
    public decimal? CheckInWeightKg { get; private set; }
    public string? CheckInHealthObservation { get; private set; }
    public bool CheckInIdentityConfirmed { get; private set; }
    public bool CheckInVaccinationProofVerified { get; private set; }
    public bool NoBelongings { get; private set; }
    public string? CheckInSignerName { get; private set; }
    public SignatureMethod? CheckInSignatureMethod { get; private set; }
    public string? CheckInSignatureStrokes { get; private set; }
    public DateTimeOffset? CheckInSignedOn { get; private set; }
    public IReadOnlyList<StayCatBelonging> Belongings => _belongings;

    // ── check-out (US-BRD-17) ───────────────────────────────────────────────────────────────────────────────────────
    public DateTimeOffset? CheckedOutOn { get; private set; }
    public decimal? CheckOutWeightKg { get; private set; }
    public string? CheckOutSignerName { get; private set; }
    public SignatureMethod? CheckOutSignatureMethod { get; private set; }
    public string? CheckOutSignatureStrokes { get; private set; }
    public DateTimeOffset? CheckOutSignedOn { get; private set; }

    // ── isolation (US-BRD-18) ───────────────────────────────────────────────────────────────────────────────────────
    public bool SuspectedInfectious { get; private set; }
    public Guid? InfectiousFlaggedBy { get; private set; }
    public DateTimeOffset? InfectiousFlaggedOn { get; private set; }
    public string? InfectiousNote { get; private set; }

    public bool IsOverridden => GateOverriddenBy is not null;

    private StayCat() { }

    public StayCat(Guid id, Guid catId, string catName)
    {
        Id = id;
        CatId = catId;
        CatName = catName;
    }

    public void Rename(string catName) => CatName = catName;

    internal void SetOccupancy(OccupancyRange range, bool isActive)
    {
        OccupiedFrom = range.From;
        OccupiedTo = range.To;
        IsActive = isActive;
    }

    /// <summary>Applies a fresh evaluation. An override survives re-evaluation (US-BRD-04 AC3): a blocked cat with an override stays Overridden.</summary>
    internal void ApplyGate(GateEvaluation evaluation, DateOnly evaluatedOn)
    {
        GateReasons = evaluation.Reasons;
        GateEvaluatedOn = evaluatedOn;
        GateStatus = evaluation.Passed ? VaccinationGateStatus.Passed
            : IsOverridden ? VaccinationGateStatus.Overridden
            : VaccinationGateStatus.Blocked;
    }

    internal void Override(Guid by, DateTimeOffset on, string reason)
    {
        GateOverriddenBy = by;
        GateOverriddenOn = on;
        GateOverrideReason = reason;
        if (GateStatus == VaccinationGateStatus.Blocked) GateStatus = VaccinationGateStatus.Overridden;
    }

    internal void RecordCheckIn(CheckInCatDto dto, DateTimeOffset now)
    {
        CheckedInOn = now;
        CheckInWeightKg = dto.WeightKg;
        CheckInHealthObservation = dto.ArrivalHealthObservation?.Trim();
        CheckInIdentityConfirmed = dto.IdentityConfirmed;
        CheckInVaccinationProofVerified = dto.VaccinationProofVerified;
        NoBelongings = dto.NoBelongings;
        CheckInSignerName = dto.Signature?.SignerName.Trim();
        CheckInSignatureMethod = dto.Signature?.Method;
        CheckInSignatureStrokes = dto.Signature?.StrokesJson;
        CheckInSignedOn = dto.Signature?.SignedOn;

        _belongings.Clear();
        _belongings.AddRange(dto.Belongings.Select(b => new StayCatBelonging(Guid.NewGuid(), b.Kind, b.Description.Trim(), b.FileIds)));
    }

    internal void RecordCheckOut(CheckOutCatDto dto, DateTimeOffset now)
    {
        CheckedOutOn = now;
        CheckOutWeightKg = dto.WeightKg;
        CheckOutSignerName = dto.Signature!.SignerName.Trim();
        CheckOutSignatureMethod = dto.Signature.Method;
        CheckOutSignatureStrokes = dto.Signature.StrokesJson;
        CheckOutSignedOn = dto.Signature.SignedOn;

        foreach (var item in dto.Belongings)
            _belongings.FirstOrDefault(b => b.Id == item.BelongingId)?.MarkReturned(item.Returned, item.Note?.Trim());
    }

    internal void FlagInfectious(Guid by, DateTimeOffset on, string note)
    {
        SuspectedInfectious = true;
        InfectiousFlaggedBy = by;
        InfectiousFlaggedOn = on;
        InfectiousNote = note;
    }

    internal void ClearInfectious(string note)
    {
        SuspectedInfectious = false;
        InfectiousNote = note;
    }

    internal void RecordVetAuthorityChange(string reason) => VetAuthorityChangeReason = reason;

    internal void SetProfile(CareProfile profile)
    {
        _feedings.Clear();
        _feedings.AddRange(profile.Feedings.OrderBy(f => f.Time).Select(f => new StayCatFeeding(Guid.NewGuid(), f)));
        _medications.Clear();
        _medications.AddRange(profile.Medications.Select(m => new StayCatMedication(Guid.NewGuid(), m)));

        HasCareProfile = true;
        Litter = profile.Litter;
        LitterNote = profile.LitterNote;
        VetVisitAuthorised = profile.VetVisitAuthorised;
        VetSpendingLimit = profile.VetSpendingLimit;
        EmergencyContactName = profile.EmergencyContact.Name;
        EmergencyContactPhone = profile.EmergencyContact.Phone;
        EmergencyContactRelationship = profile.EmergencyContact.Relationship;
        CareNotes = profile.Notes;
    }

    /// <summary>The care profile as submitted, or <c>null</c> until one has been.</summary>
    public CareProfile? Profile => !HasCareProfile
        ? null
        : new CareProfile(
            _feedings.Select(f => f.ToPlan()).ToList(), _medications.Select(m => m.ToPlan()).ToList(), Litter!, LitterNote, VetVisitAuthorised, VetSpendingLimit,
            new EmergencyContact(EmergencyContactName!, EmergencyContactPhone!, EmergencyContactRelationship), CareNotes);
}
