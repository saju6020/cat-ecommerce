namespace CatEcommerce.Shared.Common.Constants;

/// <summary>Clinic Appointments (HO-APT-001 §12.3) action names, guarded by
/// <c>[RequireAction(Actions.X.Y)]</c>. See ADR-341 for the per-area partial pattern.</summary>
public static partial class Actions
{
    public static class Appointments
    {
        public const string View = "Appointments.View";
        public const string Book = "Appointments.Book";
        public const string CheckIn = "Appointments.CheckIn";
        public const string Treat = "Appointments.Treat";
        public const string RecordNoShow = "Appointments.RecordNoShow";
    }
}
