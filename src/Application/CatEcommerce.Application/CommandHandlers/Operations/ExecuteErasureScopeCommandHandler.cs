using CatEcommerce.Application.Commands.Operations;
using CatEcommerce.Domain.Aggregates.Operations;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Operations;

/// <summary>
/// HO-XCT-001 section 11.4 C2 / ADR-302 -- the recorded rule-1 exception. Runs the context eraser, then records its outcome on the ledger.
/// The eraser saves its own context aggregates first; the ledger save follows. Erasers are idempotent, so a crash between the two is safe:
/// the next pass repeats the scope. An unregistered key is <c>Failed("EraserNotRegistered")</c>; an eraser that throws is
/// <c>Failed("UnhandledEraserException")</c> -- logged with the customer id, the context key and the exception TYPE only, and never allowed to escape.
/// When an eraser threw, its unit of work may be half-saved, so the failure is recorded through a fresh scope rather than the poisoned one.
/// </summary>
public sealed class ExecuteErasureScopeCommandHandler(
    IAggregateRootRepository<ErasureRequestAggregate> ledgers,
    IEnumerable<IPersonalDataEraser> erasers,
    IServiceScopeFactory scopeFactory,
    IOperationsClock clock,
    ILogger<ExecuteErasureScopeCommandHandler> logger)
    : ICommandHandlerAsync<ExecuteErasureScopeCommand>
{
    public const string EraserNotRegistered = "EraserNotRegistered";
    public const string UnhandledEraserException = "UnhandledEraserException";

    public async Task<CommandResponse> HandleAsync(ExecuteErasureScopeCommand command)
    {
        var ledger = await ledgers.GetByIdAsync(command.CustomerAccountId);
        if (ledger is null) return LedgerNotFound(command);

        var scope = ledger.Scopes.FirstOrDefault(s => s.ContextKey == command.ContextKey);
        if (scope is null)
            return OperationsResponses.Violation(BusinessViolationCodes.ErasureScopeNotFound, nameof(command.ContextKey), "This erasure scope was not found.", nameof(ErasureRequestAggregate));

        if (scope.Status == ErasureScopeStatus.Completed) return OperationsResponses.Ok();

        var time = await clock.NowAsync(CancellationToken.None);
        var eraser = erasers.FirstOrDefault(e => e.ContextKey == command.ContextKey);

        ErasureOutcome outcome;
        var threw = false;
        if (eraser is null)
        {
            outcome = new ErasureOutcome.Failed(EraserNotRegistered);
        }
        else
        {
            try
            {
                outcome = await eraser.EraseAsync(command.CustomerAccountId, time.Utc, command.UserContext, CancellationToken.None);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Ids and the exception type only: the message may quote a personal value.
                logger.LogError("Erasure scope {ContextKey} for customer {CustomerAccountId} threw {ExceptionType}.", command.ContextKey, command.CustomerAccountId, ex.GetType().Name);
                outcome = new ErasureOutcome.Failed(UnhandledEraserException);
                threw = true;
            }
        }

        return threw
            ? await RecordInFreshScopeAsync(command, outcome, time)
            : await RecordAsync(ledger, ledgers, command, outcome, time);
    }

    private async Task<CommandResponse> RecordInFreshScopeAsync(ExecuteErasureScopeCommand command, ErasureOutcome outcome, MainBranchTime time)
    {
        await using var fresh = scopeFactory.CreateAsyncScope();
        var freshLedgers = fresh.ServiceProvider.GetRequiredService<IAggregateRootRepository<ErasureRequestAggregate>>();
        var ledger = await freshLedgers.GetByIdAsync(command.CustomerAccountId);
        return ledger is null ? LedgerNotFound(command) : await RecordAsync(ledger, freshLedgers, command, outcome, time);
    }

    private static CommandResponse LedgerNotFound(ExecuteErasureScopeCommand command) =>
        OperationsResponses.Violation(BusinessViolationCodes.ErasureRequestNotFound, nameof(command.CustomerAccountId), "This erasure request was not found.", nameof(ErasureRequestAggregate));

    private static async Task<CommandResponse> RecordAsync(
        ErasureRequestAggregate ledger, IAggregateRootRepository<ErasureRequestAggregate> repository,
        ExecuteErasureScopeCommand command, ErasureOutcome outcome, MainBranchTime time)
    {
        try
        {
            try
            {
                ledger.RecordScopeOutcome(new ErasureScopeOutcomeDto(command.ContextKey, outcome, time.Utc, time.LocalDate, command.UserContext, command.CorrelationId));
            }
            catch (DomainRuleViolationException ex) when (outcome is ErasureOutcome.Held && ex.ErrorCode == BusinessViolationCodes.InvalidHoldDate)
            {
                // An eraser that answers "held until a date that is not in the future" has a bug; record a failure instead of losing the scope.
                ledger.RecordScopeOutcome(new ErasureScopeOutcomeDto(
                    command.ContextKey, new ErasureOutcome.Failed(BusinessViolationCodes.InvalidHoldDate), time.Utc, time.LocalDate, command.UserContext, command.CorrelationId));
            }
        }
        catch (DomainRuleViolationException ex)
        {
            return OperationsResponses.Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(ErasureRequestAggregate));
        }

        await repository.UpdateAsync(ledger, EventPublishOption.InMemoryPublishOnly);
        return OperationsResponses.Ok();
    }
}
