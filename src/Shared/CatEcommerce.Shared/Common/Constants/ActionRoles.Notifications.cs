namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// Notifications' own rows in the solution-wide action-to-role table (HO-NTF-001 §12.3, OQ-4).
/// Picked up automatically by <see cref="ActionRoles.BuildTable"/>'s reflection-based merge -- see
/// <c>ActionRoles.Accounts.cs</c> for the naming convention this file follows exactly.
/// </summary>
public static partial class ActionRoles
{
    private static readonly (string Action, string[] Roles)[] NotificationsRows =
    [
        (Actions.NotificationTemplates.View, [StaffRoles.Administrator, StaffRoles.ShopManager]),
        (Actions.NotificationTemplates.Manage, [StaffRoles.Administrator]),
        (Actions.NotificationLog.View, [StaffRoles.Reception, StaffRoles.Administrator]),
        (Actions.NotificationLog.Resend, [StaffRoles.Reception, StaffRoles.Administrator]),
    ];
}
