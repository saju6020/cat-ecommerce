using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Billing;

/// <summary>Shared <c>CommandResponse</c> shapes for every Billing handler (ADR-014: an expected business
/// outcome is <b>returned</b>, never thrown, and never published as a rejection event).</summary>
public static class BillingHandlerSupport
{
    public static CommandResponse Violation(string code, string property, string message, string resourceName = "Billing")
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: resourceName);
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }

    public static CommandResponse Success(object? result = null) => new() { IsSuccess = true, Result = result! };
}
