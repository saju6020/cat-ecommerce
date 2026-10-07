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

/// <summary>See <see cref="RefreshCustomerSessionCsrfTokenCommandHandler"/> -- same reasoning,
/// staff session.</summary>
public sealed class RefreshStaffSessionCsrfTokenCommandHandler(
    ILogger<RefreshStaffSessionCsrfTokenCommandHandler> logger,
    IAggregateRootRepository<StaffSessionAggregate> sessionRepository,
    ISessionTokenIssuer sessionTokenIssuer)
    : ICommandHandlerAsync<RefreshStaffSessionCsrfTokenCommand>
{
    public async Task<CommandResponse> HandleAsync(RefreshStaffSessionCsrfTokenCommand command)
    {
        var session = await sessionRepository.GetByFilterAsync(s => s.TokenHash == command.TokenHash);
        if (session is null)
        {
            logger.LogDebug("CSRF refresh requested for an unresolvable staff session. CorrelationId: {CorrelationId}", command.CorrelationId);
            return Violation();
        }

        var csrfMaterial = sessionTokenIssuer.Issue();

        try
        {
            // ARCHITECTURE gate fifth pass, LOW: see RefreshCustomerSessionCsrfTokenCommandHandler's
            // identical comment.
            session.RefreshCsrfToken(csrfMaterial.TokenHash);
        }
        catch (DomainRuleViolationException ex)
        {
            logger.LogDebug("CSRF refresh requested for an ended staff session. CorrelationId: {CorrelationId}", command.CorrelationId);
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
            errorCode: code, resourceName: nameof(StaffSessionAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}
