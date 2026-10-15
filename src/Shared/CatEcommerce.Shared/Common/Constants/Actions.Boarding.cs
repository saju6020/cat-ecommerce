namespace CatEcommerce.Shared.Common.Constants;

/// <summary>Boarding (HO-BRD-001 section 12.3) action names, guarded by <c>[RequireAction(Actions.Boarding.X)]</c>. ADR-341 per-area partial.</summary>
public static partial class Actions
{
    public static class Boarding
    {
        public const string ViewUnits = "Boarding.ViewUnits";
        public const string ManageUnits = "Boarding.ManageUnits";
        public const string ViewStays = "Boarding.ViewStays";
        public const string Book = "Boarding.Book";
        public const string OverrideVaccination = "Boarding.OverrideVaccination";
        public const string ChangeStay = "Boarding.ChangeStay";
        public const string CheckInOut = "Boarding.CheckInOut";
        public const string RecordCare = "Boarding.RecordCare";
        public const string RecordIncident = "Boarding.RecordIncident";
        public const string FollowUpIncident = "Boarding.FollowUpIncident";
        public const string Isolation = "Boarding.Isolation";
        public const string ClearIsolation = "Boarding.ClearIsolation";
        public const string Refer = "Boarding.Refer";
        public const string RequestVetApproval = "Boarding.RequestVetApproval";
    }
}
