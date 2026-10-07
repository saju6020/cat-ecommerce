using CatEcommerce.Application.Commands.Administration;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Administration;

/// <summary>Branch must exist and be active; name unique per branch (§11.4) -- the latter enforced
/// by the DB unique index + <c>UniqueConstraintExceptionFilter</c>, pre-checked here too.</summary>
public sealed class CreateRoomCommandHandler(
    IAggregateRootRepository<RoomAggregate> roomRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository)
    : ICommandHandlerAsync<CreateRoomCommand>
{
    public async Task<CommandResponse> HandleAsync(CreateRoomCommand command)
    {
        var branch = await branchRepository.GetByIdAsync(command.BranchId);
        if (branch is null || !branch.IsActive)
            return CreateBranchCommandHandler.Violation(BusinessViolationCodes.BranchNotFound, nameof(command.BranchId), "Branch not found or inactive.");

        if (await roomRepository.ExistAsync(r => r.BranchId == command.BranchId && r.Name == command.Name))
            return CreateBranchCommandHandler.Violation(BusinessViolationCodes.RoomNameAlreadyExists, nameof(command.Name), $"A room named '{command.Name}' already exists at this branch.");

        var room = new RoomAggregate(command.RoomId);
        try
        {
            room.Create(command.ToDomainDto());
        }
        catch (DomainRuleViolationException ex)
        {
            return CreateBranchCommandHandler.Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await roomRepository.SaveAsync(room, EventPublishOption.InMemoryPublishOnly);
        return new CommandResponse { IsSuccess = true, Result = room.Id };
    }
}

public sealed class UpdateRoomCommandHandler(IAggregateRootRepository<RoomAggregate> roomRepository) : ICommandHandlerAsync<UpdateRoomCommand>
{
    public async Task<CommandResponse> HandleAsync(UpdateRoomCommand command)
    {
        var room = await roomRepository.GetByIdAsync(command.RoomId);
        if (room is null || room.BranchId != command.BranchId)
            return CreateBranchCommandHandler.Violation(BusinessViolationCodes.RoomNotFound, nameof(command.RoomId), "Room not found.");

        if (await roomRepository.ExistAsync(r => r.BranchId == command.BranchId && r.Name == command.Name && r.Id != command.RoomId))
            return CreateBranchCommandHandler.Violation(BusinessViolationCodes.RoomNameAlreadyExists, nameof(command.Name), $"A room named '{command.Name}' already exists at this branch.");

        try
        {
            room.Update(command.ToDomainDto());
        }
        catch (DomainRuleViolationException ex)
        {
            return CreateBranchCommandHandler.Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await roomRepository.UpdateAsync(room, EventPublishOption.InMemoryPublishOnly);
        return new CommandResponse { IsSuccess = true, Result = room.Id };
    }
}
