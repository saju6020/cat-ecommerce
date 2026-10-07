using CatEcommerce.Infrastructure.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CatEcommerce.Api.Authorization;

/// <summary>HO-ACC-001 §12.4. 401 session_invalid if the caller is not a customer.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class RequireCustomerSessionAttribute : TypeFilterAttribute
{
    public RequireCustomerSessionAttribute() : base(typeof(RequireCustomerSessionFilter))
    {
    }
}

public sealed class RequireCustomerSessionFilter : IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var caller = context.HttpContext.Items[ResolvedCaller.HttpContextItemsKey] as ResolvedCaller ?? ResolvedCaller.Anonymous;

        if (caller.Kind != ResolvedCallerKind.Customer)
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            context.Result = new ObjectResult(new { error = "session_invalid" }) { StatusCode = 401 };
        }
    }
}
