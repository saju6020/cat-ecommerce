namespace CatEcommerce.Shared.Common.Constants;

/// <summary>Consultation and Medical Records (HO-EMR-001 §12.3) action names, guarded by
/// <c>[RequireAction(Actions.X.Y)]</c>. See ADR-341 for the per-area partial pattern.</summary>
public static partial class Actions
{
    public static class Visits
    {
        public const string View = "Visits.View";
        public const string Open = "Visits.Open";
        public const string RecordVitals = "Visits.RecordVitals";
        public const string WriteNote = "Visits.WriteNote";

        /// <summary>Vet alone -- Administrator is read-only on clinical notes (Appendix D, BR-6, ADR-119).</summary>
        public const string Finalise = "Visits.Finalise";
        public const string AddAddendum = "Visits.AddAddendum";
        public const string PrintSummary = "Visits.PrintSummary";

        /// <summary>A customer reading their own cat's finalised visits (FR-POR-3). Listed in
        /// <c>CustomerActions.Cat</c>; no staff role holds it.</summary>
        public const string ViewOwn = "Visits.ViewOwn";
    }

    public static class ClinicalCatalogue
    {
        public const string View = "ClinicalCatalogue.View";
        public const string Manage = "ClinicalCatalogue.Manage";
    }

    public static class Consents
    {
        public const string View = "Consents.View";
        public const string Record = "Consents.Record";
    }
}
