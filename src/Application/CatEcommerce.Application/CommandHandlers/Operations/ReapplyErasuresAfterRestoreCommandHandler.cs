using System.Text.Json;
using CatEcommerce.Application.Commands.Operations;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Operations;

/// <summary>
/// HO-XCT-001 section 11.4 C4 / ADR-314 -- the second recorded rule-1 exception (a loop over accounts). After a restore to a point earlier than
/// the latest WAL, every customer who had asked to be forgotten since that point is anonymised again, idempotently: an account already erased is
/// counted, an unknown or merged one is counted, and a failure on one account is counted and never stops the rest. Each account runs in its
/// own scope, so a database error on one cannot poison the others. Every account change carries a correlation stamped by
/// <see cref="RetentionCorrelation.NewReapply"/>, which is how the ledger-opening handler records the source <c>RestoreReconciliation</c>.
/// ONE business-event audit row summarises the call.
/// </summary>
public sealed class ReapplyErasuresAfterRestoreCommandHandler(
    IServiceScopeFactory scopeFactory,
    IAggregateRootRepository<AuditEntryAggregate> audit,
    IClientIpAccessor clientIp,
    TimeProvider timeProvider,
    ILogger<ReapplyErasuresAfterRestoreCommandHandler> logger)
    : ICommandHandlerAsync<ReapplyErasuresAfterRestoreCommand>
{
    public async Task<CommandResponse> HandleAsync(ReapplyErasuresAfterRestoreCommand command)
    {
        if (command.CustomerAccountIds.Count > RetentionRules.ReapplyMaxIds)
            return OperationsResponses.Violation(BusinessViolationCodes.ReapplyListTooLarge, nameof(command.CustomerAccountIds), OperationsMessages.ReapplyListTooLarge, "Operations");

        var now = timeProvider.GetUtcNow();
        int processed = 0, alreadyErased = 0, notFound = 0, merged = 0, failed = 0;

        foreach (var customerAccountId in command.CustomerAccountIds.Distinct())
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var customers = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CustomerAccountAggregate>>();

                switch (await CustomerErasureSteps.AnonymiseAsync(customers, customerAccountId, now, command.UserContext, RetentionCorrelation.NewReapply()))
                {
                    case CustomerErasureStep.Erased: processed++; break;
                    case CustomerErasureStep.AlreadyErased: alreadyErased++; break;
                    case CustomerErasureStep.NotFound: notFound++; break;
                    case CustomerErasureStep.Merged: merged++; break;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                logger.LogError("Re-applying the erasure of customer {CustomerAccountId} failed ({ExceptionType}).", customerAccountId, ex.GetType().Name);
            }
        }

        var result = new ReapplyErasuresResult(processed, alreadyErased, notFound, merged, failed);

        await CustomerErasureSteps.WriteBusinessEventAsync(
            audit, clientIp, now, command.UserContext, command.CorrelationId,
            entityType: "Operations", entityId: command.CorrelationId, action: "ErasuresReappliedAfterRestore",
            reason: command.RestoreReference, afterValue: JsonSerializer.Serialize(result));

        return OperationsResponses.Ok(result);
    }
}
