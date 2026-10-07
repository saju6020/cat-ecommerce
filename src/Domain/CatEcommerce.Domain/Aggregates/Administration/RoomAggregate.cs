using CatEcommerce.Domain.Events.Administration;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Administration;

/// <summary>HO-ADM-001 §8.2. Capacity 1 -- multi-occupancy is not modelled.</summary>
public sealed class RoomAggregate : AggregateRoot
{
    public Guid BranchId { get; private set; }
    public string Name { get; private set; } = default!;
    public RoomKind Kind { get; private set; } = default!;
    public bool IsActive { get; private set; }

    private RoomAggregate() { }

    public RoomAggregate(Guid id) : base(id) { }

    public void Create(RoomCreateDto dto)
    {
        ValidateName(dto.Name);

        BranchId = dto.BranchId;
        Name = dto.Name;
        Kind = dto.Kind;
        IsActive = true;

        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new RoomCreatedEvent { RoomId = Id, BranchId = BranchId, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    public void Update(RoomUpdateDto dto)
    {
        ValidateName(dto.Name);

        Name = dto.Name;
        Kind = dto.Kind;
        IsActive = dto.IsActive;

        AddDomainEvent(new RoomUpdatedEvent { RoomId = Id, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > CatEcommerce.Shared.Common.Constants.BranchRules.RoomNameMaxLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidClosure, "A room name is required.", nameof(Name));
    }
}
