using CatEcommerce.Api.Authorization;
using CatEcommerce.Read.Queries.Administration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Filters;

/// <summary>
/// HO-ADM-001 §12.5 / ADR-053. A global action filter that only acts on an action carrying
/// <see cref="StorefrontEndpointAttribute"/> (method or controller level) -- every other action,
/// administrative or otherwise, is untouched, so an administrator can always turn maintenance mode
/// off regardless of its own state. While <c>SystemStatusAggregate.MaintenanceMode</c> is on, a
/// marked action answers <c>503</c> with <c>{ "error": "maintenance", "message" }</c> and no
/// <c>Retry-After</c> header (the duration is operator-controlled, not predictable).
///
/// Gate fix 2026-09-26 (ARCH LOW / CR LOW): dispatches the existing, projected
/// <see cref="GetSystemStatusQuery"/> through <see cref="IInMemoryDispatcher"/> instead of reading
/// <c>CatEcommerceDbContext</c> directly -- the same precedent <c>RequireActionFilter</c> already
/// set with <see cref="GetCatOwnerQuery"/>, and it stops this filter from materialising the whole
/// aggregate for two columns.
/// </summary>
public sealed class MaintenanceModeFilter(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor || !IsStorefrontEndpoint(descriptor))
        {
            await next();
            return;
        }

        var status = await dispatcher.GetResultAsync(new GetSystemStatusQuery { UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        if (status.Result.MaintenanceMode)
        {
            context.Result = new ObjectResult(new { error = "maintenance", message = status.Result.Message }) { StatusCode = 503 };
            return;
        }

        await next();
    }

    private static bool IsStorefrontEndpoint(ControllerActionDescriptor descriptor) =>
        descriptor.MethodInfo.GetCustomAttributes(typeof(StorefrontEndpointAttribute), inherit: true).Length > 0 ||
        descriptor.ControllerTypeInfo.GetCustomAttributes(typeof(StorefrontEndpointAttribute), inherit: true).Length > 0;
}
