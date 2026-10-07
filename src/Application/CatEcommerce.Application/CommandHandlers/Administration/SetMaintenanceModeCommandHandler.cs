using CatEcommerce.Application.Commands.Administration;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Administration;

public sealed class SetMaintenanceModeCommandHandler(IAggregateRootRepository<SystemStatusAggregate> repository, TimeProvider timeProvider)
    : ICommandHandlerAsync<SetMaintenanceModeCommand>
{
    public async Task<CommandResponse> HandleAsync(SetMaintenanceModeCommand command)
    {
        var status = await repository.GetByIdAsync(SystemStatusIds.Singleton);
        var isNew = status is null;
        status ??= new SystemStatusAggregate(SystemStatusIds.Singleton);

        status.SetMaintenanceMode(command.ToDomainDto(timeProvider.GetUtcNow()));

        if (isNew) await repository.SaveAsync(status, EventPublishOption.InMemoryPublishOnly);
        else await repository.UpdateAsync(status, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true };
    }
}
