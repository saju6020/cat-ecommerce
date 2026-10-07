using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Account;

/// <summary>HO-ACC-001 §11.4 (C3). Missing or already-ended -> success (idempotent).</summary>
public sealed class SignOutCustomerCommandHandler(
    ILogger<SignOutCustomerCommandHandler> logger,
    IAggregateRootRepository<CustomerSessionAggregate> sessionRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<SignOutCustomerCommand>
{
    public async Task<CommandResponse> HandleAsync(SignOutCustomerCommand command)
    {
        logger.LogInformation("Customer sign-out. CorrelationId: {CorrelationId}", command.CorrelationId);

        var session = await sessionRepository.GetByFilterAsync(s => s.TokenHash == command.TokenHash);
        if (session is null) return new CommandResponse { IsSuccess = true };

        session.End(new SessionEndDto(timeProvider.GetUtcNow(), SessionEndReason.SignedOut, command.UserContext, command.CorrelationId));
        await sessionRepository.UpdateAsync(session, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true };
    }
}
