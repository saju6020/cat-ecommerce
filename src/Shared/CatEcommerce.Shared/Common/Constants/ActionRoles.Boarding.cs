namespace CatEcommerce.Shared.Common.Constants;

/// <summary>Boarding's rows in the solution-wide action-to-role table (HO-BRD-001 section 12.3; Appendix D "Boarding bookings",
/// "Care logs and tasks", "Incidents"; ADR-233 as renumbered ADR-366). There is no "boarding manager" role: the manager is
/// <c>Administrator</c> (plus <c>Reception</c> for booking changes). Check-in and check-out also go to <c>Caretaker</c>, a recorded
/// deviation from Appendix D's "Caretaker R" on bookings. Picked up by <see cref="ActionRoles.BuildTable"/> through the <c>Rows</c> suffix.</summary>
public static partial class ActionRoles
{
    private static readonly (string Action, string[] Roles)[] BoardingRows =
    [
        (Actions.Boarding.ViewUnits, [StaffRoles.Reception, StaffRoles.Nurse, StaffRoles.Vet, StaffRoles.Caretaker, StaffRoles.Administrator]),
        (Actions.Boarding.ManageUnits, [StaffRoles.Administrator]),
        (Actions.Boarding.ViewStays, [StaffRoles.Reception, StaffRoles.Nurse, StaffRoles.Vet, StaffRoles.Caretaker, StaffRoles.Administrator]),
        (Actions.Boarding.Book, [StaffRoles.Reception, StaffRoles.Administrator]),
        (Actions.Boarding.OverrideVaccination, [StaffRoles.Administrator]),
        (Actions.Boarding.ChangeStay, [StaffRoles.Reception, StaffRoles.Administrator]),
        (Actions.Boarding.CheckInOut, [StaffRoles.Reception, StaffRoles.Caretaker, StaffRoles.Administrator]),
        (Actions.Boarding.RecordCare, [StaffRoles.Nurse, StaffRoles.Caretaker, StaffRoles.Administrator]),
        (Actions.Boarding.RecordIncident, [StaffRoles.Nurse, StaffRoles.Vet, StaffRoles.Caretaker, StaffRoles.Administrator]),
        (Actions.Boarding.FollowUpIncident, [StaffRoles.Reception, StaffRoles.Administrator]),
        (Actions.Boarding.Isolation, [StaffRoles.Nurse, StaffRoles.Vet, StaffRoles.Caretaker, StaffRoles.Administrator]),
        (Actions.Boarding.ClearIsolation, [StaffRoles.Vet, StaffRoles.Administrator]),
        (Actions.Boarding.Refer, [StaffRoles.Nurse, StaffRoles.Vet, StaffRoles.Caretaker, StaffRoles.Administrator]),
        (Actions.Boarding.RequestVetApproval, [StaffRoles.Reception, StaffRoles.Vet, StaffRoles.Administrator]),
    ];
}
