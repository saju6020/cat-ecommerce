namespace CatEcommerce.Shared.Common.Constants;

/// <summary>Cross-cutting / Operations rows in the solution-wide action-to-role table (HO-XCT-001 §12.3, ADR-315).</summary>
public static partial class ActionRoles
{
    private static readonly (string Action, string[] Roles)[] OperationsRows =
    [
        (Actions.Offline.FrontDeskSnapshot, [StaffRoles.Reception, StaffRoles.Nurse, StaffRoles.Vet, StaffRoles.Caretaker, StaffRoles.Administrator]),
        (Actions.Retention.View, [StaffRoles.Administrator, StaffRoles.Accountant]),
        (Actions.Retention.Run, [StaffRoles.Administrator]),
        (Actions.CustomerErasure.Erase, [StaffRoles.Administrator]),
        (Actions.Operations.View, [StaffRoles.Administrator]),
        (Actions.Operations.RecordRestoreTest, [StaffRoles.Administrator]),
        (Actions.Telemetry.View, [StaffRoles.Administrator, StaffRoles.ShopManager]),
    ];
}
