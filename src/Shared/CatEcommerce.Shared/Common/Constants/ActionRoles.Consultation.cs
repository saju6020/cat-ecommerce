namespace CatEcommerce.Shared.Common.Constants;

/// <summary>Consultation and Medical Records' own rows in the solution-wide action-to-role table
/// (HO-EMR-001 §12.3, Appendix D "Clinical notes": Customer R own/finalised, Reception none, Nurse
/// W vitals, Vet F, Admin R). <c>Visits.Finalise</c> is Vet alone (ADR-119).</summary>
public static partial class ActionRoles
{
    private static readonly (string Action, string[] Roles)[] ConsultationRows =
    [
        (Actions.Visits.View, [StaffRoles.Nurse, StaffRoles.Vet, StaffRoles.Administrator]),
        (Actions.Visits.Open, [StaffRoles.Nurse, StaffRoles.Vet]),
        (Actions.Visits.RecordVitals, [StaffRoles.Nurse, StaffRoles.Vet]),
        (Actions.Visits.WriteNote, [StaffRoles.Vet]),
        (Actions.Visits.Finalise, [StaffRoles.Vet]),
        (Actions.Visits.AddAddendum, [StaffRoles.Vet]),
        (Actions.Visits.PrintSummary, [StaffRoles.Nurse, StaffRoles.Vet, StaffRoles.Administrator]),

        // Customer-only (OwnerScope.Cat / handler ownership); no staff role holds it, but it needs a row
        // so a [RequireAction] naming it is a known action and a staff caller is denied explicitly.
        (Actions.Visits.ViewOwn, []),

        (Actions.ClinicalCatalogue.View, [StaffRoles.Nurse, StaffRoles.Vet, StaffRoles.Administrator]),
        (Actions.ClinicalCatalogue.Manage, [StaffRoles.Vet, StaffRoles.Administrator]),

        (Actions.Consents.View, [StaffRoles.Reception, StaffRoles.Nurse, StaffRoles.Vet, StaffRoles.Administrator]),
        (Actions.Consents.Record, [StaffRoles.Reception, StaffRoles.Nurse, StaffRoles.Vet, StaffRoles.Administrator]),
    ];
}
