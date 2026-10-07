using CatEcommerce.Domain.Events.Administration;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Administration;

/// <summary>HO-ADM-001 §8.2. Cancelled closures are kept (<see cref="IsCancelled"/>) -- the
/// calendar ignores them, never deletes them (C-ALL-4).</summary>
public sealed class BranchClosureAggregate : AggregateRoot
{
    public Guid BranchId { get; private set; }
    public DateOnly StartsOn { get; private set; }
    public DateOnly EndsOn { get; private set; }
    public ClosureKind Kind { get; private set; } = default!;
    public string Reason { get; private set; } = default!;
    public TimeOnly? PartialFrom { get; private set; }
    public TimeOnly? PartialTo { get; private set; }
    public bool IsCancelled { get; private set; }

    private BranchClosureAggregate() { }

    public BranchClosureAggregate(Guid id) : base(id) { }

    public void Create(BranchClosureCreateDto dto)
    {
        if (dto.EndsOn < dto.StartsOn) throw Invalid(nameof(dto.EndsOn));
        if (string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Length > 200) throw Invalid(nameof(dto.Reason));

        if (dto.PartialFrom is not null || dto.PartialTo is not null)
        {
            if (dto.StartsOn != dto.EndsOn) throw InvalidWindow();
            if (dto.PartialFrom is null || dto.PartialTo is null) throw InvalidWindow();
            if (dto.PartialFrom >= dto.PartialTo) throw InvalidWindow();
        }

        BranchId = dto.BranchId;
        StartsOn = dto.StartsOn;
        EndsOn = dto.EndsOn;
        Kind = dto.Kind;
        Reason = dto.Reason;
        PartialFrom = dto.PartialFrom;
        PartialTo = dto.PartialTo;
        IsCancelled = false;

        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new BranchClosureCreatedEvent
        {
            BranchId = BranchId, StartsOn = StartsOn, EndsOn = EndsOn, Kind = Kind,
            CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        UpdateVersion();
    }

    public void Cancel(BranchClosureCancelDto dto)
    {
        if (StartsOn <= dto.TodayLocal)
            throw new DomainRuleViolationException(BusinessViolationCodes.PastClosureCannotBeCancelled,
                "A closure that has already started or passed cannot be cancelled.", nameof(StartsOn));

        IsCancelled = true;

        AddDomainEvent(new BranchClosureCancelledEvent { BranchId = BranchId, ClosureId = Id, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    private static DomainRuleViolationException Invalid(string property) =>
        new(BusinessViolationCodes.InvalidClosure, "This closure is invalid.", property);

    private static DomainRuleViolationException InvalidWindow() =>
        new(BusinessViolationCodes.InvalidClosureWindow, "A partial window requires a single-day closure with Opens before Closes.", nameof(PartialFrom));
}
