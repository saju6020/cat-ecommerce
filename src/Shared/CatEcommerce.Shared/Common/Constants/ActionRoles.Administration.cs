namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// Administration and Configuration's own rows in the solution-wide action-to-role table
/// (HO-ADM-001 §12.3, ADR-043). Picked up automatically by <see cref="ActionRoles.BuildTable"/>.
/// Also carries the Cat Profile / Vaccine rows that close HO-ACC-001's authorization gap -- see
/// <c>Actions.Administration.cs</c>.
/// </summary>
public static partial class ActionRoles
{
    private static readonly (string Action, string[] Roles)[] AdministrationRows =
    [
        (Actions.Cats.View, [StaffRoles.Reception, StaffRoles.Nurse, StaffRoles.Vet, StaffRoles.Caretaker, StaffRoles.Administrator]),
        (Actions.Cats.Register, [StaffRoles.Reception, StaffRoles.Nurse, StaffRoles.Vet, StaffRoles.Administrator]),
        (Actions.Cats.EditProfile, [StaffRoles.Reception, StaffRoles.Nurse, StaffRoles.Vet, StaffRoles.Administrator]),
        (Actions.Cats.RecordClinical, [StaffRoles.Nurse, StaffRoles.Vet, StaffRoles.Administrator]),
        (Actions.Cats.ManageHighRiskFlag, [StaffRoles.Nurse, StaffRoles.Vet, StaffRoles.Administrator]),
        (Actions.Cats.MarkDeceased, [StaffRoles.Vet, StaffRoles.Administrator]),
        (Actions.Cats.TransferOwnership, [StaffRoles.Reception, StaffRoles.Administrator]),
        (Actions.Cats.PrintCageCard, [StaffRoles.Reception, StaffRoles.Nurse, StaffRoles.Vet, StaffRoles.Caretaker, StaffRoles.Administrator]),

        (Actions.Vaccines.View, StaffRoles.All.ToArray()),
        (Actions.Vaccines.Manage, [StaffRoles.Vet, StaffRoles.Administrator]),

        (Actions.Branches.View, StaffRoles.All.ToArray()),
        (Actions.Branches.Manage, [StaffRoles.Administrator]),
        (Actions.Branches.ViewOwn, StaffRoles.All.ToArray()),

        (Actions.Rosters.View, [StaffRoles.Reception, StaffRoles.Nurse, StaffRoles.Vet, StaffRoles.Caretaker, StaffRoles.Administrator]),
        (Actions.Rosters.Manage, [StaffRoles.Administrator]),

        (Actions.Leave.Request, StaffRoles.All.ToArray()),
        (Actions.Leave.Decide, [StaffRoles.Administrator]),

        (Actions.Services.View, StaffRoles.All.ToArray()),
        (Actions.Services.Manage, [StaffRoles.Administrator]),

        (Actions.TaxClasses.View, [StaffRoles.Administrator, StaffRoles.Accountant, StaffRoles.ShopManager]),
        (Actions.TaxClasses.Manage, [StaffRoles.Administrator]),

        (Actions.Settings.View, [StaffRoles.Administrator]),
        (Actions.Settings.Manage, [StaffRoles.Administrator]),

        (Actions.AuditLog.View, [StaffRoles.Administrator, StaffRoles.Accountant]),
        (Actions.AuditLog.Export, [StaffRoles.Administrator, StaffRoles.Accountant]),

        (Actions.Content.Manage, [StaffRoles.Administrator, StaffRoles.ShopManager]),
        (Actions.Policies.Manage, [StaffRoles.Administrator]),

        (Actions.SystemStatus.ManageMaintenance, [StaffRoles.Administrator]),
    ];
}
