namespace CatEcommerce.Shared.Common.Constants;

/// <summary>Surgery (HO-SRG-001 §12.3, ADR-173) action names, guarded by <c>[RequireAction(Actions.Surgery.X)]</c>. Appendix D has no Surgery row, so the
/// mapping is an inference from "Appointments", "Clinical notes", "Controlled drug register" and "System configuration" (OQ-6).</summary>
public static partial class Actions
{
    public static class Surgery
    {
        public const string View = "Surgery.View";
        public const string Schedule = "Surgery.Schedule";
        public const string Admit = "Surgery.Admit";
        public const string ManageChecklist = "Surgery.ManageChecklist";
        public const string AssessRisk = "Surgery.AssessRisk";
        public const string Start = "Surgery.Start";
        public const string OverrideConsent = "Surgery.OverrideConsent";
        public const string LogVitals = "Surgery.LogVitals";
        public const string LogDrug = "Surgery.LogDrug";
        public const string EndProcedure = "Surgery.EndProcedure";
        public const string WriteReport = "Surgery.WriteReport";
        public const string ManageRecovery = "Surgery.ManageRecovery";
        public const string ManageProtocols = "Surgery.ManageProtocols";
        public const string ManageCampaigns = "Surgery.ManageCampaigns";
    }
}
