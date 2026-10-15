using CatEcommerce.Application.Commands.Boarding;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Boarding;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Boarding.BoardingHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Boarding;

/// <summary>C1 -- HO-BRD-001 section 11.4. The branch must exist and be active; the code is unique per branch (pre-checked here, raced by
/// <c>ix_boarding_unit_branch_code</c> through the constraint filter).</summary>
public sealed class CreateBoardingUnitCommandHandler(
    IAggregateRootRepository<BoardingUnitAggregate> unitRepository,
    IBoardingReferenceReader referenceReader)
    : ICommandHandlerAsync<CreateBoardingUnitCommand>
{
    public async Task<CommandResponse> HandleAsync(CreateBoardingUnitCommand command)
    {
        var branch = await referenceReader.GetBranchAsync(command.BranchId);
        if (branch is not { IsActive: true })
            return Violation(BusinessViolationCodes.BranchNotFound, nameof(command.BranchId), "Branch not found or inactive.");

        var code = command.Code.Trim();
        if (await unitRepository.ExistAsync(u => u.BranchId == command.BranchId && u.Code == code))
            return Violation(BusinessViolationCodes.UnitCodeAlreadyExists, nameof(command.Code), BoardingMessages.UnitCodeAlreadyExists);

        var unit = new BoardingUnitAggregate(command.UnitId);
        try
        {
            unit.Create(command.ToDomainDto());
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await unitRepository.SaveAsync(unit, EventPublishOption.InMemoryPublishOnly);
        return Success(unit.Id);
    }
}

/// <summary>C1 -- a unit of another branch is "not found". A type change while future stays hold the unit is the aggregate's rule
/// (<see cref="BoardingUnitAggregate.Update"/>), given the fact by <see cref="IBoardingUnitUsage"/>.</summary>
public sealed class UpdateBoardingUnitCommandHandler(
    IAggregateRootRepository<BoardingUnitAggregate> unitRepository,
    IBoardingUnitUsage unitUsage,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<UpdateBoardingUnitCommand>
{
    public async Task<CommandResponse> HandleAsync(UpdateBoardingUnitCommand command)
    {
        var unit = await unitRepository.GetByIdAsync(command.UnitId);
        if (unit is null || unit.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.BoardingUnitNotFound, nameof(command.UnitId), "Boarding unit not found.", nameof(BoardingUnitAggregate));

        var hasFuture = await unitUsage.HasFutureAllocationsAsync(unit.Id, timeProvider.GetUtcNow());
        try
        {
            unit.Update(command.ToDomainDto(hasFuture));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(BoardingUnitAggregate));
        }

        await unitRepository.UpdateAsync(unit, EventPublishOption.InMemoryPublishOnly);
        return Success(unit.Id);
    }
}

public sealed class SetBoardingUnitActivationCommandHandler(
    IAggregateRootRepository<BoardingUnitAggregate> unitRepository,
    IBoardingUnitUsage unitUsage,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<SetBoardingUnitActivationCommand>
{
    public async Task<CommandResponse> HandleAsync(SetBoardingUnitActivationCommand command)
    {
        var unit = await unitRepository.GetByIdAsync(command.UnitId);
        if (unit is null || unit.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.BoardingUnitNotFound, nameof(command.UnitId), "Boarding unit not found.", nameof(BoardingUnitAggregate));

        var hasFuture = !command.IsActive && await unitUsage.HasFutureAllocationsAsync(unit.Id, timeProvider.GetUtcNow());
        try
        {
            unit.SetActive(command.ToDomainDto(hasFuture));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(BoardingUnitAggregate));
        }

        await unitRepository.UpdateAsync(unit, EventPublishOption.InMemoryPublishOnly);
        return Success(unit.Id);
    }
}

/// <summary>C2 -- US-BRD-01 AC2. A block overlapping an active allocation is refused with the arrival dates of the stays in the way (the handler
/// pre-check, F32); another block on the unit is refused by the pre-check and, on a race, by <c>ex_unit_maintenance</c>.</summary>
public sealed class CreateMaintenanceBlockCommandHandler(
    IAggregateRootRepository<UnitMaintenanceBlockAggregate> blockRepository,
    IAggregateRootRepository<BoardingUnitAggregate> unitRepository,
    IBoardingReferenceReader referenceReader,
    IBoardingUnitUsage unitUsage)
    : ICommandHandlerAsync<CreateMaintenanceBlockCommand>
{
    public async Task<CommandResponse> HandleAsync(CreateMaintenanceBlockCommand command)
    {
        var unit = await unitRepository.GetByIdAsync(command.UnitId);
        if (unit is null || unit.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.BoardingUnitNotFound, nameof(command.UnitId), "Boarding unit not found.", nameof(BoardingUnitAggregate));

        var branch = await referenceReader.GetBranchAsync(command.BranchId);
        if (branch is null)
            return Violation(BusinessViolationCodes.BranchNotFound, nameof(command.BranchId), "Branch not found.");

        var range = BoardingOccupancyWindow.ForDays(command.StartsOn, command.EndsOn, branch.TimeZoneId);

        if (await blockRepository.ExistAsync(b => b.UnitId == command.UnitId && !b.IsCancelled && b.OccupiedFrom < range.To && range.From < b.OccupiedTo))
            return Violation(BusinessViolationCodes.MaintenanceOverlapsMaintenance, nameof(command.StartsOn), BoardingMessages.MaintenanceOverlapsMaintenance, nameof(UnitMaintenanceBlockAggregate));

        var stays = await unitUsage.GetOverlappingStayArrivalsAsync(command.UnitId, range);
        if (stays.Count > 0)
            return Violation(BusinessViolationCodes.MaintenanceOverlapsStays, nameof(command.StartsOn),
                "This unit has stays in that period (arriving " + string.Join(", ", stays.Select(d => d.ToString("yyyy-MM-dd"))) + "). Move or cancel them first.",
                nameof(UnitMaintenanceBlockAggregate));

        var block = new UnitMaintenanceBlockAggregate(command.BlockId);
        try
        {
            block.Create(new Domain.Models.MaintenanceBlockCreateDto(
                command.UnitId, command.BranchId, command.StartsOn, command.EndsOn, command.Reason, range.From, range.To, command.UserContext!, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(UnitMaintenanceBlockAggregate));
        }

        await blockRepository.SaveAsync(block, EventPublishOption.InMemoryPublishOnly);
        return Success(block.Id);
    }
}

public sealed class CancelMaintenanceBlockCommandHandler(
    IAggregateRootRepository<UnitMaintenanceBlockAggregate> blockRepository,
    IBoardingReferenceReader referenceReader,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<CancelMaintenanceBlockCommand>
{
    public async Task<CommandResponse> HandleAsync(CancelMaintenanceBlockCommand command)
    {
        var block = await blockRepository.GetByIdAsync(command.BlockId);
        if (block is null || block.UnitId != command.UnitId || block.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.MaintenanceBlockNotFound, nameof(command.BlockId), "Maintenance block not found.", nameof(UnitMaintenanceBlockAggregate));

        var today = await referenceReader.LocalDateAsync(command.BranchId, timeProvider.GetUtcNow());
        try
        {
            block.Cancel(new Domain.Models.MaintenanceBlockCancelDto(today ?? DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime), command.UserContext!, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(UnitMaintenanceBlockAggregate));
        }

        await blockRepository.UpdateAsync(block, EventPublishOption.InMemoryPublishOnly);
        return Success(block.Id);
    }
}

/// <summary>C3 -- US-BRD-03. The tax class must be active; the currency is <c>commerce.currencyCode</c>; a duplicate (type, effective-from) is a
/// clean 400 (raced by the unique index).</summary>
public sealed class SetBoardingRateCardCommandHandler(
    IAggregateRootRepository<BoardingRateCardAggregate> rateCardRepository,
    IBoardingReferenceReader referenceReader,
    IBusinessSettings businessSettings,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<SetBoardingRateCardCommand>
{
    public async Task<CommandResponse> HandleAsync(SetBoardingRateCardCommand command)
    {
        var branch = await referenceReader.GetBranchAsync(command.BranchId);
        if (branch is not { IsActive: true })
            return Violation(BusinessViolationCodes.BranchNotFound, nameof(command.BranchId), "Branch not found or inactive.");

        if (!await referenceReader.TaxClassIsActiveAsync(command.TaxClassId))
            return Violation(BusinessViolationCodes.TaxClassNotFound, nameof(command.TaxClassId), "Tax class not found or inactive.", nameof(BoardingRateCardAggregate));

        var unitType = BoardingUnitMapper.ParseEnum<Shared.Common.Enums.BoardingUnitType>(command.UnitType);
        if (await rateCardRepository.ExistAsync(c => c.BranchId == command.BranchId && c.UnitType == unitType && c.EffectiveFrom == command.EffectiveFrom))
            return Violation(BusinessViolationCodes.DuplicateRateCardEffectiveDate, nameof(command.EffectiveFrom), BoardingMessages.DuplicateRateCardEffectiveDate, nameof(BoardingRateCardAggregate));

        var card = new BoardingRateCardAggregate(command.RateCardId);
        try
        {
            card.Create(command.ToDomainDto(businessSettings.GetString(SettingKeys.CommerceCurrencyCode), timeProvider.GetUtcNow()));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(BoardingRateCardAggregate));
        }

        await rateCardRepository.SaveAsync(card, EventPublishOption.InMemoryPublishOnly);
        return Success(card.Id);
    }
}

/// <summary>C4 -- US-BRD-09. The routine's id is the branch id; it is created on the first write.</summary>
public sealed class SetBoardingCareRoutineCommandHandler(
    IAggregateRootRepository<BoardingCareRoutineAggregate> routineRepository,
    IBoardingReferenceReader referenceReader)
    : ICommandHandlerAsync<SetBoardingCareRoutineCommand>
{
    public async Task<CommandResponse> HandleAsync(SetBoardingCareRoutineCommand command)
    {
        var branch = await referenceReader.GetBranchAsync(command.BranchId);
        if (branch is not { IsActive: true })
            return Violation(BusinessViolationCodes.BranchNotFound, nameof(command.BranchId), "Branch not found or inactive.");

        var routine = await routineRepository.GetByIdAsync(command.BranchId);
        var isNew = routine is null;
        routine ??= new BoardingCareRoutineAggregate(command.BranchId);

        try
        {
            routine.SetTimes(command.ToDomainDto());
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(BoardingCareRoutineAggregate));
        }

        if (isNew) await routineRepository.SaveAsync(routine, EventPublishOption.InMemoryPublishOnly);
        else await routineRepository.UpdateAsync(routine, EventPublishOption.InMemoryPublishOnly);

        return Success(routine.Id);
    }
}
