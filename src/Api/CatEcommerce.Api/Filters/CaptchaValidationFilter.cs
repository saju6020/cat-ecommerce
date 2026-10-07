using CatEcommerce.Api.Contracts;
using CatEcommerce.Infrastructure.Security.Captcha;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Api.Filters;

/// <summary>
/// HO-ACC-001 §12.6 / ADR-029. Scoped, via <c>[ServiceFilter]</c>. Finds the one
/// <see cref="ICaptchaProtectedRequest"/> action argument -- none found is a programming error,
/// never a silent pass (§20.14). Consumes the challenge on every call that reaches here, including
/// one that then fails a later check -- the client must fetch a new one after any failure.
/// </summary>
public sealed class CaptchaValidationFilter(ICaptchaService captchaService, ILogger<CaptchaValidationFilter> logger) : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var request = context.ActionArguments.Values.OfType<ICaptchaProtectedRequest>().FirstOrDefault();
        if (request is null)
            throw new InvalidOperationException(
                $"{context.ActionDescriptor.DisplayName} is guarded by {nameof(CaptchaValidationFilter)} but has no {nameof(ICaptchaProtectedRequest)} argument.");

        if (string.IsNullOrWhiteSpace(request.CaptchaId) || string.IsNullOrWhiteSpace(request.CaptchaAnswer))
        {
            logger.LogInformation("CAPTCHA missing on {Path}. CorrelationId not yet assigned.", context.HttpContext.Request.Path);
            context.Result = Violation(BusinessViolationCodes.CaptchaRequired, "A CAPTCHA answer is required.");
            return;
        }

        if (!captchaService.Verify(request.CaptchaId, request.CaptchaAnswer))
        {
            logger.LogInformation("CAPTCHA invalid on {Path}.", context.HttpContext.Request.Path);
            context.Result = Violation(BusinessViolationCodes.CaptchaInvalid, AccountMessages.CaptchaInvalidMessage);
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        // No-op -- verification happens before the action, never after.
    }

    private static BadRequestObjectResult Violation(string code, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: "CaptchaAnswer", errorCode: code, resourceName: "Captcha");
        return new BadRequestObjectResult(validationResponse);
    }
}
