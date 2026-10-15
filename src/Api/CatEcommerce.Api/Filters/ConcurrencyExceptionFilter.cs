using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Api.Filters;

/// <summary>
/// ADR-016: a conflicting save on the `xmin` row version raises DbUpdateConcurrencyException;
/// this maps it to 409 instead of an unhandled 500.
///
/// An MVC exception filter, not middleware registered in Program.cs: per ADR-342, this host no
/// longer calls Platform.Infrastructure.Host.WebApi's UseHttpPipeline() at all -- the single
/// exception boundary is now SafeExceptionHandlerMiddleware (Api/Hosting/), which never reads the
/// request body and does not rethrow. An exception filter runs during MVC's action invocation,
/// before the exception ever reaches that middleware, which is the only place in this stack
/// that can still observe and reclassify it as a 409 instead of the generic error shape.
/// </summary>
public sealed class ConcurrencyExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        // A save inside a nested in-process dispatch (a producer calling another area's command) arrives wrapped in a TargetInvocationException; the conflict is the same.
        var exception = context.Exception;
        while (exception is System.Reflection.TargetInvocationException { InnerException: { } inner }) exception = inner;

        if (exception is not DbUpdateConcurrencyException) return;

        // HO-ORD-001 §12.5: on the placement route a moved discount or cart counter means the quote the customer saw is stale.
        if (context.ActionDescriptor.EndpointMetadata.OfType<ConflictAsQuoteChangedAttribute>().Any())
        {
            var validation = new Platform.Infrastructure.Core.Validation.ValidationResponse();
            validation.AddError(
                errorMessage: CatEcommerce.Shared.Common.Constants.OrderMessages.CheckoutQuoteChanged, propertyName: "QuoteFingerprint",
                errorCode: CatEcommerce.Shared.Common.Constants.BusinessViolationCodes.CheckoutQuoteChanged, resourceName: "Order");
            context.Result = new ConflictObjectResult(validation);
            context.ExceptionHandled = true;
            return;
        }

        context.Result = new ConflictObjectResult(new
        {
            error = "This record was changed by someone else. Reload and try again."
        });
        context.ExceptionHandled = true;
    }
}
