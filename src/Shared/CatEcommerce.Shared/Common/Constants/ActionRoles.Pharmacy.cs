namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// Pharmacy and Prescriptions' own rows in the solution-wide action-to-role table (HO-PHM-001 §12.3, ADR-153 -- an inference from SRS Appendix D
/// "Prescribe" / "Dispense medicine" / "Controlled drug register", flagged OQ-1/OQ-8). Prescribing, renewing, cancelling, declining a repeat,
/// authorising a diet and correcting the register are Vet alone (BR-6, US-CLN-22, AC-18).
/// </summary>
public static partial class ActionRoles
{
    private static readonly (string Action, string[] Roles)[] PharmacyRows =
    [
        (Actions.Formulary.View, [StaffRoles.Reception, StaffRoles.Nurse, StaffRoles.Vet, StaffRoles.Inventory, StaffRoles.Administrator]),
        (Actions.Formulary.Manage, [StaffRoles.Administrator]),

        (Actions.Prescriptions.View, [StaffRoles.Reception, StaffRoles.Nurse, StaffRoles.Vet, StaffRoles.Administrator]),
        (Actions.Prescriptions.Issue, [StaffRoles.Vet]),

        // Customer-only (handler ownership -> 404); no staff role holds it, but it needs a row so a [RequireAction] naming it is a known action.
        (Actions.Prescriptions.ViewOwn, []),

        (Actions.Dispensing.View, [StaffRoles.Reception, StaffRoles.Nurse, StaffRoles.Vet, StaffRoles.Inventory, StaffRoles.Administrator]),
        (Actions.Dispensing.Dispense, [StaffRoles.Reception, StaffRoles.Nurse, StaffRoles.Vet, StaffRoles.Administrator]),
        (Actions.Dispensing.Reverse, [StaffRoles.Vet, StaffRoles.Administrator]),

        (Actions.ControlledDrugs.View, [StaffRoles.Nurse, StaffRoles.Vet, StaffRoles.Inventory, StaffRoles.Administrator]),
        (Actions.ControlledDrugs.Correct, [StaffRoles.Vet]),

        (Actions.DietAuthorisations.View, [StaffRoles.Reception, StaffRoles.Nurse, StaffRoles.Vet, StaffRoles.ShopManager, StaffRoles.Administrator]),
        (Actions.DietAuthorisations.Issue, [StaffRoles.Vet]),
        (Actions.DietAuthorisations.ViewOwn, []),
    ];
}
