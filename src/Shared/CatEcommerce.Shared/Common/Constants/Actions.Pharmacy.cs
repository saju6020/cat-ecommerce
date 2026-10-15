namespace CatEcommerce.Shared.Common.Constants;

/// <summary>Pharmacy and Prescriptions (HO-PHM-001 §12.3, ADR-153) action names, guarded by <c>[RequireAction(Actions.X.Y)]</c>.
/// See ADR-341 for the per-area partial pattern.</summary>
public static partial class Actions
{
    public static class Formulary
    {
        public const string View = "Formulary.View";
        public const string Manage = "Formulary.Manage";
    }

    public static class Prescriptions
    {
        public const string View = "Prescriptions.View";

        /// <summary>Vet alone (BR-6, US-CLN-22, AC-18): the safety check, issue, cancel, decline a repeat and renew.</summary>
        public const string Issue = "Prescriptions.Issue";

        /// <summary>A customer reading or repeating their own cat's prescriptions (FR-POR-3). No staff role holds it.</summary>
        public const string ViewOwn = "Prescriptions.ViewOwn";
    }

    public static class Dispensing
    {
        public const string View = "Dispensing.View";
        public const string Dispense = "Dispensing.Dispense";
        public const string Reverse = "Dispensing.Reverse";
    }

    public static class ControlledDrugs
    {
        public const string View = "ControlledDrugs.View";

        /// <summary>Vet alone (OQ-8) -- Appendix D gives only the Vet a write on the register.</summary>
        public const string Correct = "ControlledDrugs.Correct";
    }

    public static class DietAuthorisations
    {
        public const string View = "DietAuthorisations.View";

        /// <summary>Vet alone (BR-6).</summary>
        public const string Issue = "DietAuthorisations.Issue";
        public const string ViewOwn = "DietAuthorisations.ViewOwn";
    }
}
