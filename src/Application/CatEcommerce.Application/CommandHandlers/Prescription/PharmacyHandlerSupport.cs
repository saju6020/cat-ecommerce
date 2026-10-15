using CatEcommerce.Domain.Exceptions;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Prescription;

/// <summary>Shared <c>CommandResponse</c> shapes for every Pharmacy handler (ADR-014: an expected business outcome is <b>returned</b>, never thrown, and
/// never published as a rejection event).</summary>
public static class PharmacyHandlerSupport
{
    public const string Resource = "Pharmacy";

    public static CommandResponse Violation(string code, string property, string message, string resourceName = Resource)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: resourceName);
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }

    public static CommandResponse Violation(DomainRuleViolationException ex, string resourceName = Resource) =>
        Violation(ex.ErrorCode, ex.PropertyName, ex.Message, resourceName);

    public static CommandResponse Success(object? result = null) => new() { IsSuccess = true, Result = result! };
}
