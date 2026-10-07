using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.SharedDto.Account;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Account;

/// <summary>
/// HO-ACC-001 §12.5 Amended (18) / ADR-342 (owner decision A, 2026-09-25). Dispatched by the
/// session middleware on a safe (GET) request when the resolved session's <c>pc_csrf</c> cookie is
/// missing or stale, so a legitimate browser recovers on its own instead of being stuck for the
/// life of the session. Not an audited change (see <c>CustomerSessionAggregate.RefreshCsrfToken</c>)
/// -- this is bookkeeping, not a business event.
/// </summary>
public sealed class RefreshCustomerSessionCsrfTokenCommandHandler(
    ILogger<RefreshCustomerSessionCsrfTokenCommandHandler> logger,
    IAggregateRootRepository<CustomerSessionAggregate> sessionRepository,
    ISessionTokenIssuer sessionTokenIssuer)
    : ICommandHandlerAsync<RefreshCustomerSessionCsrfTokenCommand>
{
    public async Task<CommandResponse> HandleAsync(RefreshCustomerSessionCsrfTokenCommand command)
    {
        var session = await sessionRepository.GetByFilterAsync(s => s.TokenHash == command.TokenHash);
        if (session is null)
        {
            logger.LogDebug("CSRF refresh requested for an unresolvable customer session. CorrelationId: {CorrelationId}", command.CorrelationId);
            return Violation();
        }

        var csrfMaterial = sessionTokenIssuer.Issue();

        try
        {
            // ARCHITECTURE gate fifth pass, LOW: RefreshCsrfToken now guards against an ended
            // session (the same shape RecordActivity already uses) -- this is reported back as a
            // violation, not an unhandled throw, matching every other handler's ADR-014 shape.
            session.RefreshCsrfToken(csrfMaterial.TokenHash);
        }
        catch (DomainRuleViolationException ex)
        {
            logger.LogDebug("CSRF refresh requested for an ended customer session. CorrelationId: {CorrelationId}", command.CorrelationId);
            return Violation(ex.ErrorCode, ex.Message);
        }

        await sessionRepository.UpdateAsync(session, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true, Result = new CsrfRefreshResult(csrfMaterial.Token) };
    }

    private static CommandResponse Violation(
        string code = BusinessViolationCodes.SessionInvalid, string message = "This session is not valid.")
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(
            errorMessage: message, propertyName: "TokenHash",
            errorCode: code, resourceName: nameof(CustomerSessionAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}
