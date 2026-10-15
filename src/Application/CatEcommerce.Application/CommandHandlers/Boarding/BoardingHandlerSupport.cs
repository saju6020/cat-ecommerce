using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Boarding;

/// <summary>Shared <c>CommandResponse</c> shapes for every Boarding handler (the pattern <c>ClinicCommandHandlerSupport</c> established): an expected
/// outcome is RETURNED, never thrown and never published as a rejection event (ADR-014).</summary>
public static class BoardingHandlerSupport
{
    public static CommandResponse Violation(string code, string property, string message, string resourceName = "BoardingStayAggregate")
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: resourceName);
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }

    public static CommandResponse Success(object? result = null) => new() { IsSuccess = true, Result = result! };
}
