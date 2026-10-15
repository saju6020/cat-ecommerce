using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Operations;

/// <summary>The single way an Operations handler reports a business-rule violation: returned, never thrown (ADR-014).</summary>
internal static class OperationsResponses
{
    public static CommandResponse Violation(string code, string property, string message, string resource)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: resource);
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }

    public static CommandResponse Ok(object? result = null) => new() { IsSuccess = true, Result = result! };
}
