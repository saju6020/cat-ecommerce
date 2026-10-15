using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Prescription;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Prescription;

/// <summary>
/// HO-PHM-001 §8.2 / US-PHM-04 (ADR-138) -- one medicine of the practice's feline formulary. Practice data, maintained by an administrator;
/// prescriptions snapshot it at issue so an edit here never rewrites a past prescription. Owns its interactions and linked SKUs (bounded, owned
/// collections). The controlled flag has its own method so changing it is a deliberate act with its own audit action.
///
/// Invariants (rule 20): a per-kg medicine has a dose range and a fixed one has none; the default route is an allowed route; a controlled medicine
/// names its schedule; an interaction names exactly one target. The unique code, SKU existence and "a SKU belongs to one medicine" are handler
/// checks backed by unique indexes.
/// </summary>
public sealed class FormularyMedicineAggregate : AggregateRoot
{
    private readonly List<FormularyInteraction> _interactions = [];
    private readonly List<LinkedSku> _linkedSkus = [];

    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string[] ActiveIngredients { get; private set; } = [];
    public string[] DrugClasses { get; private set; } = [];
    public MedicineForm Form { get; private set; } = default!;
    public Strength Strength { get; private set; } = default!;
    public AdministrationRoute DefaultRoute { get; private set; } = default!;
    public int[] AllowedRouteValues { get; private set; } = [];
    public DosingBasis DosingBasis { get; private set; } = default!;
    public DoseRange? DoseRange { get; private set; }
    public string Contraindications { get; private set; } = "";
    public bool IsControlled { get; private set; }
    public string? ControlledSchedule { get; private set; }

    /// <summary>Why the flag last changed, when a reason was given (it is required to clear the flag on a medicine that has register entries). Kept so
    /// the audit row carries it -- an event carries no strings.</summary>
    public string? ControlledChangeReason { get; private set; }
    public DispensingUnit DispensingUnit { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;

    public IReadOnlyList<FormularyInteraction> Interactions => _interactions;
    public IReadOnlyList<LinkedSku> LinkedSkus => _linkedSkus;

    public IReadOnlyList<AdministrationRoute> AllowedRoutes => AllowedRouteValues.Select(Enumeration.FromValue<AdministrationRoute>).ToList();

    private FormularyMedicineAggregate() { }

    public FormularyMedicineAggregate(Guid id) : base(id) { }

    public void Create(string code, FormularyMedicineDto dto)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > FormularyRules.MaxCodeLength || !code.All(c => c is (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '-'))
            throw Violation(BusinessViolationCodes.NameRequired, "A code of capital letters, digits and hyphens (at most 30) is required.", nameof(Code));

        Code = code;
        Apply(dto);
        IsControlled = false;
        ControlledSchedule = null;
        IsActive = true;

        SetDefaultValue(dto.Actor.UserContext);
        AddDomainEvent(new FormularyMedicineCreatedEvent { MedicineId = Id, CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext });
        UpdateVersion();
    }

    public void Update(FormularyMedicineDto dto)
    {
        Apply(dto);

        AddDomainEvent(new FormularyMedicineUpdatedEvent { MedicineId = Id, CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext });
        UpdateVersion();
    }

    /// <summary>ADR-138 -- the controlled flag. Clearing it on a medicine that has register entries needs a reason, because the register history
    /// stays and the change must be explicable to an inspector.</summary>
    public void SetControlled(bool isControlled, string? schedule, string? reason, bool hasRegisterEntries, PharmacyActor actor)
    {
        var trimmedSchedule = schedule?.Trim();
        if (isControlled)
        {
            if (string.IsNullOrWhiteSpace(trimmedSchedule) || trimmedSchedule.Length > ControlledDrugRules.MaxScheduleLength)
                throw Violation(BusinessViolationCodes.ControlledScheduleRequired, "A controlled medicine needs its schedule (at most 30 characters).", nameof(ControlledSchedule));
        }
        else if (IsControlled && hasRegisterEntries && (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > FormularyRules.MaxReasonLength))
        {
            throw Violation(BusinessViolationCodes.ControlledFlagChangeReasonRequired, "Explain why a medicine with register entries is no longer controlled (at most 500 characters).", nameof(reason));
        }

        if (IsControlled == isControlled && ControlledSchedule == (isControlled ? trimmedSchedule : null)) return;

        IsControlled = isControlled;
        ControlledSchedule = isControlled ? trimmedSchedule : null;
        ControlledChangeReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        AddDomainEvent(new FormularyMedicineControlledStatusChangedEvent { MedicineId = Id, IsControlled = IsControlled, CorrelationId = actor.CorrelationId, UserContext = actor.UserContext });
        UpdateVersion();
    }

    public void SetActive(bool isActive, PharmacyActor actor)
    {
        if (IsActive == isActive) return;

        IsActive = isActive;
        AddDomainEvent(new FormularyMedicineActivationChangedEvent { MedicineId = Id, IsActive = IsActive, CorrelationId = actor.CorrelationId, UserContext = actor.UserContext });
        UpdateVersion();
    }

    /// <summary>The value the rest of the solution reads (never the aggregate).</summary>
    public FormularyMedicineSnapshot ToSnapshot() => new(
        Id, Code, Name, Form, Strength, DefaultRoute, AllowedRoutes, DosingBasis, DoseRange, Contraindications,
        ActiveIngredients, DrugClasses,
        _interactions.Select(i => new InteractionSnapshot(i.InteractsWithMedicineId, i.InteractsWithTerm, i.Severity, i.Note)).ToList(),
        IsControlled, ControlledSchedule, DispensingUnit, _linkedSkus.Select(l => l.SkuId).ToList(), IsActive);

    private void Apply(FormularyMedicineDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Trim().Length > FormularyRules.MaxNameLength)
            throw Violation(BusinessViolationCodes.NameRequired, "A name of at most 200 characters is required.", nameof(Name));

        var ingredients = NormaliseTerms(dto.ActiveIngredients, nameof(ActiveIngredients));
        if (ingredients.Count is < 1 or > FormularyRules.MaxActiveIngredients)
            throw Violation(BusinessViolationCodes.InvalidInteraction, "A medicine has between one and five active ingredients.", nameof(ActiveIngredients));

        var classes = NormaliseTerms(dto.DrugClasses, nameof(DrugClasses));
        if (classes.Count > FormularyRules.MaxDrugClasses)
            throw Violation(BusinessViolationCodes.InvalidInteraction, "A medicine has at most ten drug classes.", nameof(DrugClasses));

        var strength = new Strength(dto.Strength.Amount, dto.Strength.Unit, dto.Strength.PerQuantity, dto.Strength.PerUnit);

        var routeValues = dto.AllowedRoutes.Select(r => r.Value).Distinct().OrderBy(v => v).ToArray();
        if (routeValues.Length == 0 || !routeValues.Contains(dto.DefaultRoute.Value))
            throw Violation(BusinessViolationCodes.RouteNotAllowedForMedicine, "The default route must be one of the allowed routes.", nameof(DefaultRoute));

        DoseRange? range = null;
        if (dto.DosingBasis == DosingBasis.PerKg)
        {
            if (dto.DoseRange is null)
                throw Violation(BusinessViolationCodes.DoseRangeRequired, "A per-kg medicine needs its mg/kg dose range.", nameof(DoseRange));
            range = new DoseRange(dto.DoseRange.MinMgPerKg, dto.DoseRange.MaxMgPerKg);
        }
        else if (dto.DoseRange is not null)
        {
            throw Violation(BusinessViolationCodes.InvalidDoseRange, "A fixed-dose medicine has no mg/kg range.", nameof(DoseRange));
        }

        if ((dto.Contraindications ?? "").Length > FormularyRules.MaxContraindicationsLength)
            throw Violation(BusinessViolationCodes.NameRequired, "Contraindications are at most 2000 characters.", nameof(Contraindications));

        if (dto.Interactions.Count > FormularyRules.MaxInteractions)
            throw Violation(BusinessViolationCodes.InvalidInteraction, "A medicine lists at most 50 interactions.", nameof(Interactions));

        var interactions = dto.Interactions.Select(i => BuildInteraction(i)).ToList();

        var skuIds = dto.LinkedSkuIds.ToList();
        if (skuIds.Count > FormularyRules.MaxLinkedSkus || skuIds.Distinct().Count() != skuIds.Count)
            throw Violation(BusinessViolationCodes.SkuAlreadyLinkedToMedicine, "A medicine links at most ten distinct SKUs.", nameof(dto.LinkedSkuIds));

        Name = dto.Name.Trim();
        ActiveIngredients = ingredients.ToArray();
        DrugClasses = classes.ToArray();
        Form = dto.Form;
        Strength = strength;
        DefaultRoute = dto.DefaultRoute;
        AllowedRouteValues = routeValues;
        DosingBasis = dto.DosingBasis;
        DoseRange = range;
        Contraindications = (dto.Contraindications ?? "").Trim();
        DispensingUnit = dto.DispensingUnit;

        ReplaceInteractions(interactions);
        ReplaceLinkedSkus(skuIds);
    }

    private FormularyInteraction BuildInteraction(InteractionInput input)
    {
        var hasMedicine = input.InteractsWithMedicineId is { } id && id != Guid.Empty;
        var term = MedicineTermNormaliser.Normalise(input.InteractsWithTerm);
        var hasTerm = term.Length > 0;

        if (hasMedicine == hasTerm)
            throw Violation(BusinessViolationCodes.InvalidInteraction, "An interaction names exactly one other medicine or one ingredient or class.", nameof(Interactions));

        if (hasMedicine && input.InteractsWithMedicineId == Id)
            throw Violation(BusinessViolationCodes.InvalidInteraction, "A medicine cannot interact with itself.", nameof(Interactions));

        if (term.Length > FormularyRules.MaxTermLength || string.IsNullOrWhiteSpace(input.Note) || input.Note.Trim().Length > FormularyRules.MaxInteractionNoteLength)
            throw Violation(BusinessViolationCodes.InvalidInteraction, "An interaction needs a note of at most 500 characters and a term of at most 100.", nameof(Interactions));

        return new FormularyInteraction(Guid.NewGuid(), hasMedicine ? input.InteractsWithMedicineId : null, hasTerm ? term : null, input.Severity, input.Note.Trim());
    }

    private List<string> NormaliseTerms(IEnumerable<string> terms, string property)
    {
        var normalised = terms.Select(MedicineTermNormaliser.Normalise).Where(t => t.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        if (normalised.Any(t => t.Length > FormularyRules.MaxTermLength))
            throw Violation(BusinessViolationCodes.InvalidInteraction, "Each ingredient or class is at most 100 characters.", property);
        return normalised;
    }

    private void ReplaceInteractions(List<FormularyInteraction> interactions)
    {
        _interactions.Clear();
        _interactions.AddRange(interactions);
    }

    /// <summary>Keeps the rows of SKUs that stay linked (so an edit that touches nothing rewrites nothing) and adds the new ones.</summary>
    private void ReplaceLinkedSkus(List<Guid> skuIds)
    {
        _linkedSkus.RemoveAll(l => !skuIds.Contains(l.SkuId));
        foreach (var skuId in skuIds.Where(id => _linkedSkus.All(l => l.SkuId != id)))
            _linkedSkus.Add(new LinkedSku(Guid.NewGuid(), skuId));
    }

    private static DomainRuleViolationException Violation(string code, string message, string property) => new(code, message, property);
}
