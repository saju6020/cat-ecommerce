using CatEcommerce.Domain.Events.Boarding;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Boarding;

/// <summary>
/// HO-BRD-001 section 8.2 (US-BRD-01, ADR-217 as renumbered ADR-350). A boarding unit: type, capacity in cats, location block. The nightly rate is NOT
/// here -- it is the unit TYPE's rate card (ADR-221, renumbered ADR-354). A unit with future stays keeps its type and stays active: both rules are this
/// aggregate's, given the fact by the handler (<see cref="BoardingUnitUpdateDto.HasFutureAllocations"/>).
/// </summary>
public sealed class BoardingUnitAggregate : AggregateRoot
{
    public Guid BranchId { get; private set; }
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public BoardingUnitType Type { get; private set; } = default!;
    public int CapacityCats { get; private set; }
    public string? Block { get; private set; }
    public bool IsActive { get; private set; }
    public int SortOrder { get; private set; }

    private BoardingUnitAggregate() { }

    public BoardingUnitAggregate(Guid id) : base(id) { }

    public void Create(BoardingUnitCreateDto dto)
    {
        ValidateCode(dto.Code);
        ValidateShape(dto.Name, dto.Type, dto.CapacityCats, dto.Block);

        BranchId = dto.BranchId;
        Code = dto.Code.Trim();
        Name = dto.Name.Trim();
        Type = dto.Type;
        CapacityCats = dto.CapacityCats;
        Block = string.IsNullOrWhiteSpace(dto.Block) ? null : dto.Block.Trim();
        SortOrder = dto.SortOrder;
        IsActive = true;

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new BoardingUnitCreatedEvent { UnitId = Id, BranchId = BranchId, Type = Type, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    public void Update(BoardingUnitUpdateDto dto)
    {
        ValidateShape(dto.Name, dto.Type, dto.CapacityCats, dto.Block);

        if (dto.HasFutureAllocations && dto.Type != Type)
            throw new DomainRuleViolationException(BusinessViolationCodes.UnitHasFutureStays, "This unit has upcoming stays, so its type cannot change.", nameof(dto.Type));

        Name = dto.Name.Trim();
        Type = dto.Type;
        CapacityCats = dto.CapacityCats;
        Block = string.IsNullOrWhiteSpace(dto.Block) ? null : dto.Block.Trim();
        SortOrder = dto.SortOrder;

        AddDomainEvent(new BoardingUnitUpdatedEvent { UnitId = Id, Type = Type, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    /// <summary>Deactivate refuses while future stays hold the unit; reactivate always succeeds. A no-op change raises nothing.</summary>
    public void SetActive(BoardingUnitActivationDto dto)
    {
        if (IsActive == dto.IsActive) return;

        if (!dto.IsActive && dto.HasFutureAllocations)
            throw new DomainRuleViolationException(BusinessViolationCodes.UnitHasFutureStays, "This unit has upcoming stays, so it cannot be deactivated.", nameof(IsActive));

        IsActive = dto.IsActive;
        AddDomainEvent(new BoardingUnitActivationChangedEvent { UnitId = Id, IsActive = IsActive, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    private static void ValidateCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length > StayRules.MaxUnitCodeLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidBoardingUnit, $"A unit code is required and cannot exceed {StayRules.MaxUnitCodeLength} characters.", nameof(Code));
    }

    private static void ValidateShape(string name, BoardingUnitType type, int capacityCats, string? block)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > StayRules.MaxUnitNameLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidBoardingUnit, $"A unit name is required and cannot exceed {StayRules.MaxUnitNameLength} characters.", nameof(Name));

        if (block is { Length: > StayRules.MaxUnitBlockLength })
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidBoardingUnit, $"The block cannot exceed {StayRules.MaxUnitBlockLength} characters.", nameof(Block));

        var max = type == BoardingUnitType.Isolation ? StayRules.MaxIsolationCapacity : StayRules.MaxUnitCapacity;
        if (capacityCats < 1 || capacityCats > max)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidBoardingUnit, $"A {type.DisplayName} unit holds between 1 and {max} cats.", nameof(CapacityCats));
    }
}
