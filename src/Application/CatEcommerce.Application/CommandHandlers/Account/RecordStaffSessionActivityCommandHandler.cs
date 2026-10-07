using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Account;

/// <summary>
/// HO-ACC-001 §11.4 (C14) / §12.5 / ADR-037. Dispatched by the session middleware in its own DI
/// scope, with one retry on <c>DbUpdateConcurrencyException</c> handled by the caller.
/// </summary>
public sealed class RecordStaffSessionActivityCommandHandler(
    ILogger<RecordStaffSessionActivityCommandHandler> logger,
    IAggregateRootRepository<StaffSessionAggregate> sessionRepository,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository,
    ISessionPolicy sessionPolicy,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordStaffSessionActivityCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordStaffSessionActivityCommand command)
    {
        logger.LogDebug("Staff session activity heartbeat. CorrelationId: {CorrelationId}", command.CorrelationId);

        var session = await sessionRepository.GetByFilterAsync(s => s.TokenHash == command.TokenHash);
        if (session is null) return Violation(BusinessViolationCodes.SessionInvalid, "This session is not valid.");

        var staff = await staffRepository.GetByIdAsync(session.StaffAccountId);
        if (staff is null) return Violation(BusinessViolationCodes.SessionInvalid, "This session is not valid.");

        var asOf = timeProvider.GetUtcNow();
        var state = session.StateAt(asOf, staff.SecurityStamp, sessionPolicy.StaffIdleTimeout);

        if (state == SessionState.IdleExpired)
        {
            session.EndForIdleTimeout(new StaffSessionIdleTimeoutDto(asOf, sessionPolicy.StaffIdleTimeout, command.UserContext, command.CorrelationId));
            await sessionRepository.UpdateAsync(session, EventPublishOption.InMemoryPublishOnly);
            return Violation(BusinessViolationCodes.SessionIdleTimeout, "The session has been idle too long. Sign in again.");
        }

        if (state != SessionState.Active)
            return Violation(BusinessViolationCodes.SessionInvalid, "This session is not valid.");

        session.RecordActivity(new StaffSessionActivityDto(asOf));
        // RecordActivity only mutates when the 60s throttle allows it -- Version is unchanged
        // otherwise, so calling UpdateAsync unconditionally would still be a correct but slightly
        // wasteful write; kept simple and correct rather than tracking a "did it change" flag
        // that would duplicate the aggregate's own internal check.
        await sessionRepository.UpdateAsync(session, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true };
    }

    private static CommandResponse Violation(string code, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: "TokenHash", errorCode: code, resourceName: nameof(StaffSessionAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}
