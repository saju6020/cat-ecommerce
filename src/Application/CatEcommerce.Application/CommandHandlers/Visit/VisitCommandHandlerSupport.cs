using CatEcommerce.Domain.Aggregates.Visit;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Visit;

/// <summary>Shared <c>CommandResponse.Failure</c> shape for every Consultation handler -- the same
/// pattern <c>ClinicCommandHandlerSupport</c> established for Appointments.</summary>
public static class VisitCommandHandlerSupport
{
    public static CommandResponse Violation(string code, string property, string message, string resourceName = nameof(VisitAggregate))
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: resourceName);
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }

    public static CommandResponse Success(object? result = null) => new() { IsSuccess = true, Result = result! };
}
