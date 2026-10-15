namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-IMP-001 §12.3 -- Inventory's rows in the solution-wide action-to-role table. Appendix D
/// "Stock and batches": Reception R, Nurse R, Vet R, Caretaker R, Inventory F, Shop Mgr R, Admin F;
/// "Purchase orders / imports": Inventory F, Shop Mgr R, Accountant R, Admin F. OQ-4/OQ-7/OQ-8 decide
/// the approver, the recall roles and the accountant's cost rights.
/// </summary>
public static partial class ActionRoles
{
    private static readonly (string Action, string[] Roles)[] InventoryRows =
    [
        (Actions.Stock.View, [StaffRoles.Reception, StaffRoles.Nurse, StaffRoles.Vet, StaffRoles.Caretaker, StaffRoles.Inventory, StaffRoles.ShopManager, StaffRoles.Administrator]),
        (Actions.Stock.Receive, [StaffRoles.Inventory, StaffRoles.Administrator]),
        (Actions.Stock.Transfer, [StaffRoles.Inventory, StaffRoles.Administrator]),
        (Actions.Stock.Adjust, [StaffRoles.Inventory, StaffRoles.Administrator]),
        (Actions.Stock.ApproveAdjustment, [StaffRoles.Administrator]),
        (Actions.Stock.Count, [StaffRoles.Inventory, StaffRoles.Administrator]),
        (Actions.Batches.ManageStatus, [StaffRoles.Inventory, StaffRoles.Administrator]),
        (Actions.Recalls.Manage, [StaffRoles.Inventory, StaffRoles.Administrator]),
        (Actions.StockAlerts.Acknowledge, [StaffRoles.Inventory, StaffRoles.ShopManager, StaffRoles.Administrator]),
        (Actions.StorageBreaches.Report, [StaffRoles.Reception, StaffRoles.Nurse, StaffRoles.Vet, StaffRoles.Caretaker, StaffRoles.Inventory, StaffRoles.ShopManager, StaffRoles.Administrator]),
        (Actions.StorageBreaches.Manage, [StaffRoles.Inventory, StaffRoles.Administrator]),
        (Actions.ProductStorage.Manage, [StaffRoles.Inventory, StaffRoles.Administrator]),
        (Actions.StockLocations.Manage, [StaffRoles.Administrator]),
        (Actions.ReorderPolicies.Manage, [StaffRoles.Inventory, StaffRoles.Administrator]),
        (Actions.InventoryReports.View, [StaffRoles.Inventory, StaffRoles.ShopManager, StaffRoles.Accountant, StaffRoles.Administrator]),
        (Actions.Procurement.View, [StaffRoles.Inventory, StaffRoles.ShopManager, StaffRoles.Accountant, StaffRoles.Administrator]),
        (Actions.Procurement.Manage, [StaffRoles.Inventory, StaffRoles.Administrator]),
        (Actions.Consignments.ManageCosts, [StaffRoles.Inventory, StaffRoles.Accountant, StaffRoles.Administrator]),
    ];
}
