namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-ADM-001 §12.3 / ADR-043. The actions a customer caller may perform on their **own** resource,
/// checked by <c>RequireActionFilter</c> when an action carries <c>OwnerScope.Cat</c>. A customer is
/// never granted an action simply by holding the "Customer" role in <see cref="ActionRoles"/> --
/// that table is staff-only; this list is the separate, explicit customer allow-list OQ-4's mapping
/// produces (Appendix D's "own" column).
/// </summary>
public static class CustomerActions
{
    /// <summary>Actions a customer may perform against their own cat (or, for list/register, with
    /// no <c>catId</c> in the route at all -- the controller forces <c>OwnerId</c> to the caller).</summary>
    public static readonly IReadOnlyList<string> Cat =
    [
        Actions.Cats.View,
        Actions.Cats.Register,
        Actions.Cats.EditProfile,
    ];
}
