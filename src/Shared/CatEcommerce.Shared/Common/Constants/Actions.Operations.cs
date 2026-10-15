namespace CatEcommerce.Shared.Common.Constants;

/// <summary>Cross-cutting / Operations (HO-XCT-001 §12.3, ADR-315) action names. "Compliance officer" and
/// "security officer" are not Appendix D roles, so they map to Administrator (plus Accountant for read-only
/// retention views), as the audit log does.</summary>
public static partial class Actions
{
    public static class Offline
    {
        public const string FrontDeskSnapshot = "Offline.FrontDeskSnapshot";
    }

    public static class Retention
    {
        public const string View = "Retention.View";
        public const string Run = "Retention.Run";
    }

    /// <summary>The action string is "Customers.Erase" (ADR-315); the nested class has its own name because Actions.Customers belongs to Accounts.</summary>
    public static class CustomerErasure
    {
        public const string Erase = "Customers.Erase";
    }

    public static class Operations
    {
        public const string View = "Operations.View";
        public const string RecordRestoreTest = "Operations.RecordRestoreTest";
    }

    public static class Telemetry
    {
        public const string View = "Telemetry.View";
    }
}
