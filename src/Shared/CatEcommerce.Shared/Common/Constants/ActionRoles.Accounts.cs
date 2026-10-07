namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// Accounts' own rows in the solution-wide action-to-role table (HO-ACC-001 §12.8, ADR-036).
/// Picked up automatically by <see cref="ActionRoles.BuildTable"/>'s reflection-based merge in
/// <c>ActionRoles.cs</c> -- this file never needs editing by any later area, and never edits
/// another area's own <c>ActionRoles.[Area].cs</c> file (ADR-341).
///
/// <b>The naming convention is load-bearing, copy it exactly.</b> Your own
/// <c>ActionRoles.[Area].cs</c> file needs exactly one member shaped like <c>AccountsRows</c>
/// below: a <c>private static readonly (string Action, string[] Roles)[]</c> field whose name
/// **must end with the literal word <c>Rows</c>**. <c>BuildTable</c> finds candidates by that name
/// first, then checks the shape -- so a field of the right shape but the wrong name (e.g.
/// <c>BillingTable</c>), or a right-named member of the wrong shape (a property, a public field,
/// a `Dictionary`), throws immediately naming the offending member. Either mistake, left
/// undetected, would silently drop your area's rows and fail every one of its
/// <c>[RequireAction]</c> endpoints closed with a 403 nobody could explain.
/// </summary>
public static partial class ActionRoles
{
    /// <summary>Accounts' own rows (HO-ACC-001 §12.8).</summary>
    private static readonly (string Action, string[] Roles)[] AccountsRows =
    [
        (Actions.StaffAccounts.View, [StaffRoles.Administrator]),
        (Actions.StaffAccounts.Create, [StaffRoles.Administrator]),
        (Actions.StaffAccounts.ChangeRoles, [StaffRoles.Administrator]),
        (Actions.Customers.View, [StaffRoles.Reception, StaffRoles.Nurse, StaffRoles.Vet, StaffRoles.Administrator]),
        (Actions.Customers.RegisterWalkIn, [StaffRoles.Reception, StaffRoles.Administrator]),
        (Actions.Customers.Merge, [StaffRoles.Administrator]),
    ];
}
