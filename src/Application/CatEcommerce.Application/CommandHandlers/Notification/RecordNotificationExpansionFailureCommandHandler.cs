using CatEcommerce.Application.Commands.Notification;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Notification;

/// <summary>
/// HO-NTF-001 SECURITY/CODE_REVIEW gate fix (2026-09-27, second pass, MEDIUM, "one bad row blocks
/// the whole queue"). Idempotent, like every other terminal-transition handler in this area: a
/// second run (or a run against a request that already left <c>Pending</c>) writes nothing.
/// Dispatched from a FRESH scope by <c>NotificationWorkCycle</c> -- never the scope whose
/// <c>ExpandNotificationRequestCommand</c> dispatch just failed, because once a Postgres command
/// fails inside an implicit transaction, that same connection refuses every further command until
/// the transaction ends.
/// </summary>
public sealed class RecordNotificationExpansionFailureCommandHandler(
    IAggregateRootRepository<NotificationRequestAggregate> requestRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordNotificationExpansionFailureCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordNotificationExpansionFailureCommand command)
    {
        var request = await requestRepository.GetByIdAsync(command.RequestId);
        if (request is null || request.Status != NotificationRequestStatus.Pending)
            return new CommandResponse { IsSuccess = true };

        request.RecordFailedAttempt(timeProvider.GetUtcNow(), NotificationRequestRules.MaxExpansionAttempts);
        await requestRepository.UpdateAsync(request, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true };
    }
}
