namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// Accounts and Identity (HO-ACC-001 §12.8) action names, guarded by
/// <c>[RequireAction(Actions.X.Y)]</c>. See ADR-341 for the per-area partial pattern -- append
/// your own nested class here only if you own Accounts; every other area adds its own
/// <c>Actions.[Area].cs</c> file instead.
/// </summary>
public static partial class Actions
{
    public static class StaffAccounts
    {
        public const string View = "StaffAccounts.View";
        public const string Create = "StaffAccounts.Create";
        public const string ChangeRoles = "StaffAccounts.ChangeRoles";
    }

    public static class Customers
    {
        public const string View = "Customers.View";
        public const string RegisterWalkIn = "Customers.RegisterWalkIn";
        public const string Merge = "Customers.Merge";
    }
}
