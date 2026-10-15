using CatEcommerce.Domain.Events.Boarding;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Boarding;

/// <summary>
/// HO-BRD-001 section 8.9 (US-BRD-11, ADR-225 as renumbered ADR-358) -- one observation of one cat: portion eaten, water, litter output, mood and a note. Its OWN root, append-only
/// (a correction is a later entry). "Not eating" is derived from the last two entries by <see cref="DomainServices.NotEatingRule"/> and never stored.
/// </summary>
public sealed class CareObservationAggregate : AggregateRoot
{
    public Guid BranchId { get; private set; }
    public Guid StayId { get; private set; }
    public Guid CatId { get; private set; }
    public DateTimeOffset RecordedOn { get; private set; }
    public DateOnly LocalDate { get; private set; }
    public Guid RecordedBy { get; private set; }
    public PortionEaten PortionEaten { get; private set; } = default!;
    public WaterIntake WaterIntake { get; private set; } = default!;
    public LitterOutput LitterOutput { get; private set; } = default!;
    public CatMood Mood { get; private set; } = default!;
    public string? Notes { get; private set; }

    private CareObservationAggregate() { }

    public CareObservationAggregate(Guid id) : base(id) { }

    public void Record(
        Guid branchId, Guid stayId, Guid catId, DateTimeOffset now, DateOnly localDate, PortionEaten portion, WaterIntake water, LitterOutput litter, CatMood mood,
        string? notes, UserContext userContext, Guid correlationId)
    {
        if (notes is { Length: > IncidentRules.MaxObservationNotesLength })
            throw new DomainRuleViolationException(BusinessViolationCodes.ObservationNotAllowed, "The notes are too long.", nameof(notes));

        BranchId = branchId;
        StayId = stayId;
        CatId = catId;
        RecordedOn = now;
        LocalDate = localDate;
        RecordedBy = userContext.UserId;
        PortionEaten = portion;
        WaterIntake = water;
        LitterOutput = litter;
        Mood = mood;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        SetDefaultValue(userContext);
        AddDomainEvent(new CareObservationRecordedEvent { ObservationId = Id, StayId = StayId, CatId = CatId, Portion = portion, CorrelationId = correlationId, UserContext = userContext });
        UpdateVersion();
    }
}
