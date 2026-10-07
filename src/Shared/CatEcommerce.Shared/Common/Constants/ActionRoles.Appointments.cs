namespace CatEcommerce.Shared.Common.Constants;

/// <summary>Clinic Appointments' own rows in the solution-wide action-to-role table (HO-APT-001
/// §12.3, Appendix D "Appointments"). Picked up automatically by <see cref="ActionRoles.BuildTable"/>.
/// Nurses are read-only (OQ-3, Appendix D as written).</summary>
public static partial class ActionRoles
{
    private static readonly (string Action, string[] Roles)[] AppointmentsRows =
    [
        (Actions.Appointments.View, [StaffRoles.Reception, StaffRoles.Nurse, StaffRoles.Vet, StaffRoles.Administrator]),
        (Actions.Appointments.Book, [StaffRoles.Reception, StaffRoles.Vet, StaffRoles.Administrator]),
        (Actions.Appointments.CheckIn, [StaffRoles.Reception, StaffRoles.Vet, StaffRoles.Administrator]),
        (Actions.Appointments.Treat, [StaffRoles.Vet, StaffRoles.Administrator]),
        (Actions.Appointments.RecordNoShow, [StaffRoles.Reception, StaffRoles.Administrator]),
    ];
}
