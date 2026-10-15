using CatEcommerce.Application.Commands.Operations;
using CatEcommerce.Domain.Aggregates.Operations;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Operations;

/// <summary>
/// HO-XCT-001 section 11.4 C12. A correction names the record it supersedes, which must exist. The aggregate computes RPO, RTO and the outcome; the
/// performer is the signed-in caller. The data change is audited by change capture (the record is not excluded).
/// </summary>
public sealed class RecordRestoreTestCommandHandler(IAggregateRootRepository<RestoreTestRecordAggregate> tests, IOperationsClock clock)
    : ICommandHandlerAsync<RecordRestoreTestCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordRestoreTestCommand command)
    {
        if (command.SupersedesId is { } supersedes && !await tests.ExistAsync(t => t.Id == supersedes))
            return OperationsResponses.Violation(
                BusinessViolationCodes.SupersededRestoreTestNotFound, nameof(command.SupersedesId), "The restore test you are correcting was not found.", nameof(RestoreTestRecordAggregate));

        var today = (await clock.NowAsync(CancellationToken.None)).LocalDate;
        var record = new RestoreTestRecordAggregate(command.RestoreTestId);
        try
        {
            record.Record(new RestoreTestDto(
                command.PerformedOn, command.UserContext.UserId, command.Environment, command.BackupSetLabel, command.SimulatedFailureAt, command.RecoveredTo,
                command.RestoreStartedAt, command.RestoreCompletedAt, command.Checks.Select(c => new RestoreCheckDto(c.Name, c.Passed, c.Detail)).ToList(),
                command.DrillOutput, command.Notes, command.SupersedesId, today, command.UserContext, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return OperationsResponses.Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(RestoreTestRecordAggregate));
        }

        await tests.SaveAsync(record, EventPublishOption.InMemoryPublishOnly);
        return OperationsResponses.Ok(new
        {
            restoreTestId = record.Id, outcome = record.Outcome.DisplayName,
            achievedRpoMinutes = record.AchievedRpoMinutes, achievedRtoMinutes = record.AchievedRtoMinutes,
        });
    }
}
