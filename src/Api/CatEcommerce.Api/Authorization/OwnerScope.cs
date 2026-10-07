namespace CatEcommerce.Api.Authorization;

/// <summary>
/// HO-ADM-001 §12.3 / ADR-043. Whether a <c>[RequireAction]</c> action additionally scopes a
/// customer caller to a resource they own, checked against
/// <see cref="CatEcommerce.Shared.Common.Constants.CustomerActions"/> via
/// <see cref="RequireActionFilter"/>.
///
/// <c>Cat</c> is a single-resource action: the route MUST carry a valid <c>catId</c> value, checked
/// against <see cref="CatEcommerce.Read.Queries.Cat.GetCatOwnerQuery"/>. <c>CatCollection</c> is the
/// explicit opt-in for the two actions that legitimately have no id in the route at all (list,
/// register) -- the controller forces <c>OwnerId</c>/the query filter to the caller (F19). Gate fix
/// 2026-09-26: previously <c>Cat</c> alone covered both cases by testing for a missing
/// <c>catId</c> route value, so an action wrongly missing/misnaming its id parameter -- or, invoked
/// directly bypassing routing, one with a malformed value -- fell through to "allow" instead of
/// being refused; <c>CatCollection</c> must now be requested explicitly.
/// </summary>
public enum OwnerScope
{
    None,
    Cat,
    CatCollection
}
