using Microsoft.AspNetCore.Mvc;

namespace CatEcommerce.Api.Authorization;

/// <summary>
/// HO-ACC-001 §12.8 / ADR-036. Extended by HO-ADM-001 §12.3 / ADR-043 with an optional
/// <paramref name="ownerScope"/>: when <see cref="OwnerScope.Cat"/>, a customer caller is checked
/// against <see cref="CatEcommerce.Shared.Common.Constants.CustomerActions"/> and the route's
/// <c>catId</c> instead of being refused outright -- see <see cref="RequireActionFilter"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequireActionAttribute : TypeFilterAttribute
{
    public RequireActionAttribute(string action, OwnerScope ownerScope = OwnerScope.None) : base(typeof(RequireActionFilter))
    {
        Arguments = [action, ownerScope];
    }
}
