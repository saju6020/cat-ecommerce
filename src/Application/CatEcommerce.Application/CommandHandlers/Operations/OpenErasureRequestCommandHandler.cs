using CatEcommerce.Application.Commands.Operations;
using CatEcommerce.Domain.Aggregates.Operations;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Operations;

/// <summary>
/// HO-XCT-001 section 11.4 C1 / ADR-302. Ledger exists: add a scope for every registered eraser it does not have yet (a context added since),
/// saving only when something was added. Otherwise open it with one scope per registered <see cref="IPersonalDataEraser"/>.
/// <b>A concurrent open</b> (the ledger-opening event handler and the worker self-heal racing; the primary key is the customer id, so the loser insert fails) is
/// "treat as exists, reload, top up" (gate fix 2026-10-12, CODE_REVIEW 17 / ARCHITECTURE 42):
/// the loser re-resolves the REPOSITORY in a fresh scope (never another handler: the failed insert may leave its own unit of work holding the unsaved ledger)
/// and finishes inline. The Application layer does not depend on the database driver, so a lost race is recognised by the ledger existing afterwards, not by
/// an error code. Cancellation is never treated as a lost race.
/// </summary>
public sealed class OpenErasureRequestCommandHandler(
    IAggregateRootRepository<ErasureRequestAggregate> ledgers,
    IEnumerable<IPersonalDataEraser> erasers,
    IServiceScopeFactory scopeFactory,
    ILogger<OpenErasureRequestCommandHandler> logger)
    : ICommandHandlerAsync<OpenErasureRequestCommand>
{
    public Task<CommandResponse> HandleAsync(OpenErasureRequestCommand command) => OpenAsync(command, allowRetry: true);

    private async Task<CommandResponse> OpenAsync(OpenErasureRequestCommand command, bool allowRetry)
    {
        var keys = erasers.Select(e => e.ContextKey).Distinct(StringComparer.Ordinal).ToList();

        try
        {
            var ledger = await ledgers.GetByIdAsync(command.CustomerAccountId);

            if (ledger is null)
            {
                ledger = new ErasureRequestAggregate(command.CustomerAccountId);
                ledger.Open(new ErasureOpenDto(
                    command.RequestedOn, Enumeration.FromDisplayName<ErasureRequestSource>(command.Source), keys, command.UserContext, command.CorrelationId));
                await SaveNewAsync(ledger, command, allowRetry);   // a lost race is finished in a fresh scope inside
            }
            else if (ledger.AddMissingScopes(new ErasureAddScopesDto(keys, command.UserContext, command.CorrelationId)))
            {
                await ledgers.UpdateAsync(ledger, EventPublishOption.InMemoryPublishOnly);
            }
        }
        catch (DomainRuleViolationException ex)
        {
            return OperationsResponses.Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(ErasureRequestAggregate));
        }

        return OperationsResponses.Ok();
    }

    private async Task SaveNewAsync(ErasureRequestAggregate ledger, OpenErasureRequestCommand command, bool allowRetry)
    {
        try
        {
            await ledgers.SaveAsync(ledger, EventPublishOption.InMemoryPublishOnly);
        }
        catch (Exception ex) when (allowRetry && ex is not DomainRuleViolationException and not OperationCanceledException)
        {
            logger.LogWarning("Opening the erasure ledger of {CustomerAccountId} failed ({ExceptionType}); reloading it in a fresh scope.", command.CustomerAccountId, ex.GetType().Name);

            // The shape of ExecuteErasureScopeCommandHandler.RecordInFreshScopeAsync: re-resolve the REPOSITORY (never another handler) and finish inline.
            await using var fresh = scopeFactory.CreateAsyncScope();
            var freshLedgers = fresh.ServiceProvider.GetRequiredService<IAggregateRootRepository<ErasureRequestAggregate>>();
            var winner = await freshLedgers.GetByIdAsync(command.CustomerAccountId);
            if (winner is null) throw;                                                    // not a lost race: a real failure

            var keys = erasers.Select(e => e.ContextKey).Distinct(StringComparer.Ordinal).ToList();
            if (winner.AddMissingScopes(new ErasureAddScopesDto(keys, command.UserContext, command.CorrelationId)))
                await freshLedgers.UpdateAsync(winner, EventPublishOption.InMemoryPublishOnly);
        }
    }
}
