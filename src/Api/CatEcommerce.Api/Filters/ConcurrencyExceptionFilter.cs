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
        if (context.Exception is not DbUpdateConcurrencyException) return;

        context.Result = new ConflictObjectResult(new
        {
            error = "This record was changed by someone else. Reload and try again."
        });
        context.ExceptionHandled = true;
    }
}
