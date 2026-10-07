using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Account;

public sealed class SignOutStaffCommandHandler(
    ILogger<SignOutStaffCommandHandler> logger,
    IAggregateRootRepository<StaffSessionAggregate> sessionRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<SignOutStaffCommand>
{
    public async Task<CommandResponse> HandleAsync(SignOutStaffCommand command)
    {
        logger.LogInformation("Staff sign-out. CorrelationId: {CorrelationId}", command.CorrelationId);

        var session = await sessionRepository.GetByFilterAsync(s => s.TokenHash == command.TokenHash);
        if (session is null) return new CommandResponse { IsSuccess = true };

        session.End(new SessionEndDto(timeProvider.GetUtcNow(), SessionEndReason.SignedOut, command.UserContext, command.CorrelationId));
        await sessionRepository.UpdateAsync(session, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true };
    }
}
