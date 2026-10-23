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
        /// <summary>Owner, 2026-10-19: an administrator sets a staff member's password and shares it with them.</summary>
        public const string SetPassword = "StaffAccounts.SetPassword";
        /// <summary>Owner, 2026-10-20: an administrator edits a staff member's name, mobile number and email address.</summary>
        public const string UpdateDetails = "StaffAccounts.UpdateDetails";
    }

    public static class Customers
    {
        public const string View = "Customers.View";
        public const string RegisterWalkIn = "Customers.RegisterWalkIn";
        public const string Merge = "Customers.Merge";
    }
}
