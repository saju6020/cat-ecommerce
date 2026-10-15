using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Filters;

/// <summary>
/// HO-EMR-001 §8.11 / ADR-129 / NFR-SEC-8 -- runs <b>after</b> authorization and <b>before</b> the action,
/// and dispatches <see cref="RecordClinicalRecordAccessCommand"/> (a query never writes, so the log cannot
/// live in the query). <b>Fails closed:</b> if the audit write fails for any reason the action does not run
/// and the caller gets a bare <c>500</c> -- an unlogged read is the thing NFR-SEC-8 forbids. The failure is
/// logged with the kind and correlation id only; no clinical data, and no path (a path can carry an id).
///
/// What is recorded: the kind, the record id and cat id when the route carries them (<c>visitId</c>,
/// <c>orderId</c>, <c>consentId</c>, <c>catId</c>), the route path without its query string, the actor
/// (staff or customer) and the client IP. The signed file URL is not re-logged -- it was issued by a logged
/// read and lives minutes.
/// </summary>
public sealed class ClinicalRecordAccessFilter(
    ClinicalRecordKind kind,
    IInMemoryDispatcher dispatcher,
    IUserContextProvider userContextProvider,
    IClientIpAccessor clientIpAccessor,
    ILogger<ClinicalRecordAccessFilter> logger)
    : IAsyncActionFilter
{
    private static readonly string[] RecordIdRouteKeys = ["visitId", "orderId", "consentId"];

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var correlationId = Guid.NewGuid();
        try
        {
            var response = await dispatcher.SendAsync(new RecordClinicalRecordAccessCommand
            {
                AuditEntryId = Guid.NewGuid(),
                CatId = RouteGuid(context, "catId"),
                Kind = kind,
                RecordId = RecordIdRouteKeys.Select(key => RouteGuid(context, key)).FirstOrDefault(id => id is not null),
                Path = context.HttpContext.Request.Path.Value ?? "",
                IpAddress = clientIpAccessor.GetClientIpAddress()?.ToString(),
                UserContext = userContextProvider.GetUserContext(),
                CorrelationId = correlationId,
                ValidateCommand = true
            });

            if (response.IsSuccess)
            {
                // A clinical response is personal data: no shared or browser cache may keep it (the PDF and SVG
                // actions set the stricter "private, no-store" themselves).
                context.HttpContext.Response.Headers.CacheControl = "no-store";
                await next();
                return;
            }

            logger.LogError("Clinical record access log was refused for {Kind}. CorrelationId: {CorrelationId}", kind.DisplayName, correlationId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Clinical record access log failed for {Kind}. CorrelationId: {CorrelationId}", kind.DisplayName, correlationId);
        }

        // Fail closed (ADR-129): nothing clinical in the body, and the action never ran.
        context.Result = new ObjectResult(new { error = "The request could not be completed." }) { StatusCode = StatusCodes.Status500InternalServerError };
    }

    private static Guid? RouteGuid(ActionExecutingContext context, string key) =>
        context.RouteData.Values.TryGetValue(key, out var raw) && Guid.TryParse(raw?.ToString(), out var id) ? id : null;
}
