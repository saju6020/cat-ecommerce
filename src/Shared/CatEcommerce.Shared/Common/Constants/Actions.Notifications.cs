namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// Notifications (HO-NTF-001 §12.3) action names, guarded by <c>[RequireAction(Actions.X.Y)]</c>.
/// See ADR-341 for the per-area partial pattern.
/// </summary>
public static partial class Actions
{
    public static class NotificationTemplates
    {
        public const string View = "NotificationTemplates.View";
        public const string Manage = "NotificationTemplates.Manage";
    }

    public static class NotificationLog
    {
        public const string View = "NotificationLog.View";
        public const string Resend = "NotificationLog.Resend";
    }
}
