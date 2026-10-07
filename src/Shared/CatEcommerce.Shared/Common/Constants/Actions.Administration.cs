namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// Administration and Configuration (HO-ADM-001 §12.3) action names, guarded by
/// <c>[RequireAction(Actions.X.Y)]</c>. See ADR-341 for the per-area partial pattern. This file
/// also carries the Cat Profile and Vaccine actions closing HO-ACC-001's authorization gap
/// (§4.3 there, US-ADM-02 here) -- Cat Profiles and Vaccination are HO-CAT-001's own contexts, but
/// the action-to-role mapping that guards them is this handover's to complete (§12.3), and there is
/// only ever one <see cref="ActionRoles"/> table (never a second one per context).
/// </summary>
public static partial class Actions
{
    /// <summary>OQ-4: Appendix D gives two Cat Profile rows ("own", "any"); this is the
    /// inference onto the API's nineteen actions, recorded in ADR-043.</summary>
    public static class Cats
    {
        public const string View = "Cats.View";
        public const string Register = "Cats.Register";
        public const string EditProfile = "Cats.EditProfile";
        public const string RecordClinical = "Cats.RecordClinical";
        public const string ManageHighRiskFlag = "Cats.ManageHighRiskFlag";
        public const string MarkDeceased = "Cats.MarkDeceased";
        public const string TransferOwnership = "Cats.TransferOwnership";
        public const string PrintCageCard = "Cats.PrintCageCard";
    }

    public static class Vaccines
    {
        public const string View = "Vaccines.View";
        public const string Manage = "Vaccines.Manage";
    }

    public static class Branches
    {
        public const string View = "Branches.View";
        public const string Manage = "Branches.Manage";

        /// <summary>Any staff role reading their own branch assignment/picker (`GET
        /// /api/staff/branches/mine`) -- distinct from <see cref="View"/>, which is the
        /// administrator-facing "view anyone's assignment" action (gate fix 2026-09-26).</summary>
        public const string ViewOwn = "Branches.ViewOwn";
    }

    public static class Rosters
    {
        public const string View = "Rosters.View";
        public const string Manage = "Rosters.Manage";
    }

    public static class Leave
    {
        public const string Request = "Leave.Request";
        public const string Decide = "Leave.Decide";
    }

    public static class Services
    {
        public const string View = "Services.View";
        public const string Manage = "Services.Manage";
    }

    public static class TaxClasses
    {
        public const string View = "TaxClasses.View";
        public const string Manage = "TaxClasses.Manage";
    }

    public static class Settings
    {
        public const string View = "Settings.View";
        public const string Manage = "Settings.Manage";
    }

    public static class AuditLog
    {
        public const string View = "AuditLog.View";
        public const string Export = "AuditLog.Export";
    }

    public static class Content
    {
        public const string Manage = "Content.Manage";
    }

    /// <summary>Owner decision 2026-09-26 (supersedes decision (b)): every policy document's create
    /// and publish is Administrator-only, for any key, regardless of <c>RequiresAcceptance</c> --
    /// a distinct action from <see cref="Content"/>.<see cref="Content.Manage"/>, which a Shop
    /// Manager also holds and which still governs the policy list view
    /// (<c>AdminPoliciesController.Get</c> stays on <c>Content.Manage</c>).</summary>
    public static class Policies
    {
        public const string Manage = "Policies.Manage";
    }

    /// <summary>Named <c>SystemStatus</c>, not <c>System</c>, so the class does not shadow the
    /// <c>System</c> namespace anywhere this partial is in scope (gate fix 2026-09-26).</summary>
    public static class SystemStatus
    {
        public const string ManageMaintenance = "System.ManageMaintenance";
    }
}
