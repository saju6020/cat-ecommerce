using CatEcommerce.Application.Commands.Operations;
using CatEcommerce.Domain.Aggregates.Operations;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Operations;

/// <summary>
/// HO-XCT-001 section 11.4 C5 -- any run Requested or Running refuses a new request (<c>RetentionRunAlreadyInProgress</c>). A run that has been
/// Running for more than <see cref="RetentionRules.StaleRunHours"/> hours was abandoned by a crashed worker and no longer blocks.
/// </summary>
public sealed class RequestRetentionRunCommandHandler(IAggregateRootRepository<RetentionRunAggregate> runs, TimeProvider timeProvider)
    : ICommandHandlerAsync<RequestRetentionRunCommand>
{
    public async Task<CommandResponse> HandleAsync(RequestRetentionRunCommand command)
    {
        var now = timeProvider.GetUtcNow();
        var staleBefore = now.AddHours(-RetentionRules.StaleRunHours);

        var inProgress = await runs.ExistAsync(r =>
            r.Status == RetentionRunStatus.Requested ||
            (r.Status == RetentionRunStatus.Running && r.StartedOn != null && r.StartedOn > staleBefore));
        if (inProgress)
            return OperationsResponses.Violation(BusinessViolationCodes.RetentionRunAlreadyInProgress, nameof(command.RetentionRunId), OperationsMessages.RetentionRunAlreadyInProgress, nameof(RetentionRunAggregate));

        var run = new RetentionRunAggregate(command.RetentionRunId);
        try
        {
            run.Request(new RetentionRunRequestDto(
                Enumeration.FromDisplayName<RetentionRunTrigger>(command.Trigger), command.UserContext.UserId, now, command.UserContext, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return OperationsResponses.Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(RetentionRunAggregate));
        }

        await runs.SaveAsync(run, EventPublishOption.InMemoryPublishOnly);
        return OperationsResponses.Ok(new { retentionRunId = run.Id, status = run.Status.DisplayName });
    }
}

public sealed class StartRetentionRunCommandHandler(IAggregateRootRepository<RetentionRunAggregate> runs, TimeProvider timeProvider)
    : ICommandHandlerAsync<StartRetentionRunCommand>
{
    public async Task<CommandResponse> HandleAsync(StartRetentionRunCommand command)
    {
        var run = await runs.GetByIdAsync(command.RetentionRunId);
        if (run is null) return NotFound(command.RetentionRunId);

        try { run.Start(new RetentionRunStartDto(timeProvider.GetUtcNow(), command.UserContext, command.CorrelationId)); }
        catch (DomainRuleViolationException ex) { return OperationsResponses.Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(RetentionRunAggregate)); }

        await runs.UpdateAsync(run, EventPublishOption.InMemoryPublishOnly);
        return OperationsResponses.Ok();
    }

    internal static CommandResponse NotFound(Guid id) =>
        OperationsResponses.Violation(BusinessViolationCodes.InvalidRetentionRun, nameof(id), "This retention run was not found.", nameof(RetentionRunAggregate));
}

public sealed class RecordRetentionPassCommandHandler(IAggregateRootRepository<RetentionRunAggregate> runs)
    : ICommandHandlerAsync<RecordRetentionPassCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordRetentionPassCommand command)
    {
        var run = await runs.GetByIdAsync(command.RetentionRunId);
        if (run is null) return StartRetentionRunCommandHandler.NotFound(command.RetentionRunId);

        try
        {
            run.RecordPass(new RetentionPassDto(
                Enumeration.FromDisplayName<RetentionPassKind>(command.PassKind), command.Cutoff, command.Examined, command.Affected, command.Held, command.Failed,
                command.ErrorCodes, command.DurationMs, command.UserContext, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex) { return OperationsResponses.Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(RetentionRunAggregate)); }

        await runs.UpdateAsync(run, EventPublishOption.InMemoryPublishOnly);
        return OperationsResponses.Ok();
    }
}

public sealed class FinishRetentionRunCommandHandler(IAggregateRootRepository<RetentionRunAggregate> runs, TimeProvider timeProvider)
    : ICommandHandlerAsync<FinishRetentionRunCommand>
{
    public async Task<CommandResponse> HandleAsync(FinishRetentionRunCommand command)
    {
        var run = await runs.GetByIdAsync(command.RetentionRunId);
        if (run is null) return StartRetentionRunCommandHandler.NotFound(command.RetentionRunId);

        try { run.Finish(new RetentionRunFinishDto(timeProvider.GetUtcNow(), command.UserContext, command.CorrelationId)); }
        catch (DomainRuleViolationException ex) { return OperationsResponses.Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(RetentionRunAggregate)); }

        await runs.UpdateAsync(run, EventPublishOption.InMemoryPublishOnly);
        return OperationsResponses.Ok(new { status = run.Status.DisplayName });
    }
}
