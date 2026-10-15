using System.Runtime.ExceptionServices;
using CatEcommerce.Domain.DomainServices;
using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.CommandHandlers.Boarding;

/// <summary>
/// Runs ONE attempt of a handler and, when it loses a race the database decides (an exclusion constraint or the daycare slot index for a claim; a row-version conflict for a status flip),
/// repeats it in a FRESH DI scope: a new DbContext, so the failed save leaves nothing tracked, the inputs are re-read and the deterministic pick lands on the NEXT free unit or slot.
/// Bounded; when the attempts run out the honest refusal is returned (UnitNoLongerAvailable, DaycareFull). No application lock: the constraints are the control (section 4.4, ADR-352).
/// </summary>
public static class ClaimRetry
{
    public const int MaxAttempts = 4;

    public static async Task<CommandResponse> RunAsync<TCommand, THandler>(
        THandler first, TCommand command, Func<THandler, TCommand, Task<CommandResponse>> attempt, IFreshScopeExecutor scopes,
        Func<Exception, bool> lost, Func<CommandResponse>? exhausted)
        where TCommand : Command
        where THandler : class, ICommandHandlerAsync<TCommand>
    {
        Exception last;
        try
        {
            return await attempt(first, command);
        }
        catch (Exception ex) when (lost(ex))
        {
            last = ex;
            scopes.DiscardRequestScopeChanges(); // the failed attempt left its entities and outbox rows tracked here
        }

        for (var n = 2; n <= MaxAttempts; n++)
        {
            try
            {
                return await scopes.ExecuteAsync<ICommandHandlerAsync<TCommand>, CommandResponse>(handler => attempt((THandler)handler, command));
            }
            catch (Exception ex) when (lost(ex))
            {
                last = ex;
            }
        }

        if (exhausted is null) ExceptionDispatchInfo.Capture(last).Throw();
        return exhausted!();
    }
}
