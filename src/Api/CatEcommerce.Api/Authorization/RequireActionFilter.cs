using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Hosting;
using CatEcommerce.Read.Queries.Cat;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Authorization;

/// <summary>
/// HO-ACC-001 §12.8 / ADR-036, extended by HO-ADM-001 §12.3 / ADR-043 (customer ownership scoping).
///
/// 1. Anonymous caller -&gt; 401 <c>session_invalid</c> (unchanged, no audit -- there is no identity
///    to attribute the denial to).
/// 2. Customer caller, <see cref="OwnerScope.CatCollection"/>, action in
///    <see cref="CustomerActions.Cat"/> -&gt; allow unconditionally (list, register) -- the controller
///    forces <c>OwnerId</c> to the caller (F19). No route id is ever consulted for this scope.
/// 3. Customer caller, <see cref="OwnerScope.Cat"/>, action in <see cref="CustomerActions.Cat"/>:
///    the route MUST carry a <c>catId</c> value that parses as a <c>Guid</c> -- a missing or
///    unparsable value fails closed via the same 403-plus-audit path as step 5 (gate fix
///    2026-09-26: previously this fell through to an unconditional allow). A valid id -&gt;
///    dispatch <see cref="GetCatOwnerQuery"/>; owner == caller -&gt; allow; not found or owned by
///    someone else -&gt; <b>404</b>, never 403 -- a customer must not learn that a cat id exists
///    (U-A5).
/// 4. Any other customer call (an action outside their scope, or an in-scope action with
///    <c>OwnerScope.None</c>) -&gt; the same 403-plus-audit path as a staff caller with no matching
///    role (U-A6) -- a customer IS authenticated, so a blanket 401 would be wrong here.
/// 5. Staff caller -&gt; role check against <see cref="ActionRoles"/> (unchanged): permitted -&gt;
///    allow; else -&gt; 403 + audit.
/// </summary>
public sealed class RequireActionFilter(
    string action,
    OwnerScope ownerScope,
    IInMemoryDispatcher dispatcher,
    IUserContextProvider userContextProvider,
    IClientIpAccessor clientIpAccessor,
    ILogger<RequireActionFilter> logger)
    : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var caller = context.HttpContext.Items[ResolvedCaller.HttpContextItemsKey] as ResolvedCaller ?? ResolvedCaller.Anonymous;

        if (caller.Kind == ResolvedCallerKind.Anonymous)
        {
            Unauthorized(context);
            return;
        }

        if (caller.Kind == ResolvedCallerKind.Customer)
        {
            if (CustomerActions.Cat.Contains(action, StringComparer.Ordinal))
            {
                if (ownerScope == OwnerScope.CatCollection)
                {
                    // No id in the route at all (list, register) -> allow; the controller forces
                    // OwnerId = caller.
                    return;
                }

                if (ownerScope == OwnerScope.Cat)
                {
                    if (!TryGetCatId(context, out var catId))
                    {
                        // Missing or unparsable catId -- fail closed, never allow (gate fix
                        // 2026-09-26).
                        await DenyAsync(context);
                        return;
                    }

                    var ownerResponse = await dispatcher.GetResultAsync(new GetCatOwnerQuery
                    {
                        CatId = catId,
                        UserContext = userContextProvider.GetUserContext(),
                        CorrelationId = Guid.NewGuid()
                    });

                    if (ownerResponse.Result == caller.AccountId) return;

                    // Not found or owned by someone else -- 404, never 403 (U-A5): a customer must
                    // not be able to distinguish "not yours" from "does not exist".
                    context.Result = new NotFoundResult();
                    return;
                }
            }

            await DenyAsync(context);
            return;
        }

        // caller.Kind == Staff
        var permitted = ActionRoles.TryGetRoles(action, out var allowedRoles) &&
                         allowedRoles.Intersect(caller.RoleIds, StringComparer.Ordinal).Any();

        if (permitted) return;

        await DenyAsync(context);
    }

    private static bool TryGetCatId(AuthorizationFilterContext context, out Guid catId)
    {
        catId = Guid.Empty;
        if (!context.RouteData.Values.TryGetValue("catId", out var raw) || raw is null) return false;
        return Guid.TryParse(raw.ToString(), out catId);
    }

    private static void Unauthorized(AuthorizationFilterContext context)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        context.Result = new ObjectResult(new { error = "session_invalid" }) { StatusCode = 401 };
    }

    private async Task DenyAsync(AuthorizationFilterContext context)
    {
        try
        {
            var command = new RecordAuthorizationDenialCommand
            {
                Action = action,
                Method = context.HttpContext.Request.Method,
                Path = context.HttpContext.Request.Path.Value ?? "",
                IpAddress = clientIpAccessor.GetClientIpAddress()?.ToString(),
                UserContext = userContextProvider.GetUserContext(),
                CorrelationId = Guid.NewGuid(),
                ValidateCommand = true
            };

            await dispatcher.SendAsync(command);
        }
        catch (Exception ex)
        {
            // Never an allow, never a 500 -- the denial itself must still be returned (§20.14).
            logger.LogError(ex, "Failed to record authorization denial for action {Action}", action);
        }

        context.Result = new ObjectResult(new
        {
            errors = new[] { new { errorCode = BusinessViolationCodes.ActionNotPermitted, errorMessage = "You are not permitted to perform this action." } }
        })
        { StatusCode = 403 };
    }
}
