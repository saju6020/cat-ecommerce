using CatEcommerce.Infrastructure.Reporting;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CatEcommerce.Api.Filters;

/// <summary>
/// HO-RPT-001 section 12.5 -- a report statement that ran past the statement timeout (SQLSTATE 57014, raised by <see cref="IReportingReadScope"/> as <see cref="ReportTimedOutException"/>)
/// answers <c>400 ReportTimedOut</c> with a plain-language next step. A <c>PostgresException</c> 57014 raised anywhere else is <b>not</b> mapped here -- it rethrows. A nested in-process dispatch
/// wraps the exception in a <c>TargetInvocationException</c>; the cause is the same.
/// </summary>
public sealed class ReportTimedOutExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var exception = context.Exception;
        while (exception is System.Reflection.TargetInvocationException { InnerException: { } inner }) exception = inner;

        if (exception is not ReportTimedOutException) return;

        context.HttpContext.Response.Headers.CacheControl = "no-store";
        context.Result = new BadRequestObjectResult(new
        {
            errors = new[] { new { errorCode = BusinessViolationCodes.ReportTimedOut, errorMessage = ReportingMessages.ReportTimedOut, propertyName = "Request" } }
        });
        context.ExceptionHandled = true;
    }
}
