using CatEcommerce.Domain.Events.Cat;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Cat;

/// <summary>
/// Separate from CatAggregate -- .ai/bounded-contexts.md rule 3 names weight history as
/// unbounded. "Current weight" is resolved read-side from the latest row; never denormalised
/// onto the cat -- HO-CAT-001 section 8.1 / ADR-023.
/// </summary>
public sealed class CatWeightRecordAggregate : AggregateRoot
{
    public Guid CatId { get; private set; }
    public Weight Weight { get; private set; } = default!;
    public DateOnly RecordedOn { get; private set; }
    public Guid RecordedByStaffId { get; private set; }
    public string? Notes { get; private set; }

    private CatWeightRecordAggregate() { }

    public CatWeightRecordAggregate(Guid id) : base(id) { }

    // CODE_REVIEW gate, MEDIUM (2026-09-26): "today" is threaded in from the handler's
    // TimeProvider, never read from the wall clock here.
    public void Record(CatWeightRecordDto dto)
    {
        if (dto.RecordedOn > dto.Today)
            throw new DomainRuleViolationException(
                BusinessViolationCodes.WeightRecordDateInFuture,
                "The weight record date cannot be in the future.",
                nameof(dto.RecordedOn));

        CatId = dto.CatId;
        Weight = Weight.Create(dto.Kilograms);
        RecordedOn = dto.RecordedOn;
        RecordedByStaffId = dto.RecordedByStaffId;
        Notes = dto.Notes;

        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new CatWeightRecordedEvent
        {
            CatId = CatId,
            WeightRecordId = Id,
            Kilograms = dto.Kilograms,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }
}
