using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Inventory;

/// <summary>
/// HO-IMP-001 §11.4 -- how an Inventory handler REPORTS an expected outcome: it returns a <see cref="CommandResponse"/> failure and never
/// throws or publishes a rejection (ADR-014). One place, so every handler builds the same body.
/// </summary>
internal static class InventoryResponses
{
    public const string Resource = "Inventory";

    public static CommandResponse Ok(object? result = null) => result is null ? new() { IsSuccess = true } : new() { IsSuccess = true, Result = result };

    public static CommandResponse Fail(string code, string property, string message, string resource = Resource)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: resource);
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }

    /// <summary>The addressed record does not exist or belongs to another branch -- the endpoint answers 404, never revealing which.</summary>
    public static CommandResponse NotFound(string code, string property, string message) =>
        Fail(code, property, message, InventoryRules.NotFoundResource);

    public static CommandResponse Fail(DomainRuleViolationException ex, string resource = Resource) =>
        Fail(ex.ErrorCode, ex.PropertyName, ex.Message, resource);

    /// <summary>A port failure (<c>StockResult</c>) becomes the command's own failure -- the port never throws for a business rule (§8.8).</summary>
    public static CommandResponse Fail<T>(StockResult<T> result, string resource = Resource) =>
        Fail(result.ViolationCode ?? "InsufficientStock", result.PropertyName ?? "Quantity", result.Message ?? "The stock movement was refused.", resource);

    /// <summary>Several violations at once (a goods receipt returns EVERY line violation, §11.4 C8).</summary>
    public static CommandResponse Fail(IEnumerable<GoodsReceiptViolation> violations, string resource = Resource)
    {
        var validationResponse = new ValidationResponse();
        foreach (var violation in violations)
            validationResponse.AddError(errorMessage: violation.Message, propertyName: violation.PropertyName, errorCode: violation.Code, resourceName: resource);
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}
