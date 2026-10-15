namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// Surgery's own rows in the solution-wide action-to-role table (HO-SRG-001 §12.3, ADR-173). Administrators read and configure but do not perform
/// clinical writes (Appendix D gives them "R" on clinical notes and on the controlled drug register). <c>OverrideConsent</c> and <c>LogDrug</c> are Vet
/// alone (BR-5, BR-6, Appendix D controlled register W = Vet).
/// </summary>
public static partial class ActionRoles
{
    private static readonly (string Action, string[] Roles)[] SurgeryRows =
    [
        (Actions.Surgery.View, [StaffRoles.Reception, StaffRoles.Nurse, StaffRoles.Vet, StaffRoles.Administrator]),
        (Actions.Surgery.Schedule, [StaffRoles.Reception, StaffRoles.Vet, StaffRoles.Administrator]),
        (Actions.Surgery.Admit, [StaffRoles.Reception, StaffRoles.Nurse, StaffRoles.Vet]),
        (Actions.Surgery.ManageChecklist, [StaffRoles.Nurse, StaffRoles.Vet]),
        (Actions.Surgery.AssessRisk, [StaffRoles.Vet]),
        (Actions.Surgery.Start, [StaffRoles.Nurse, StaffRoles.Vet]),
        (Actions.Surgery.OverrideConsent, [StaffRoles.Vet]),
        (Actions.Surgery.LogVitals, [StaffRoles.Nurse, StaffRoles.Vet]),
        (Actions.Surgery.LogDrug, [StaffRoles.Vet]),
        (Actions.Surgery.EndProcedure, [StaffRoles.Vet]),
        (Actions.Surgery.WriteReport, [StaffRoles.Vet]),
        (Actions.Surgery.ManageRecovery, [StaffRoles.Nurse, StaffRoles.Vet]),
        (Actions.Surgery.ManageProtocols, [StaffRoles.Vet, StaffRoles.Administrator]),
        (Actions.Surgery.ManageCampaigns, [StaffRoles.Administrator]),
    ];
}
