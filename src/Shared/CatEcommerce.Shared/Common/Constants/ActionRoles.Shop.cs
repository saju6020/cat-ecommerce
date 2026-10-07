namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-SHP-001 §12.3 -- Shop's own row in the solution-wide action-to-role table (ADR-036/ADR-341).
/// Appendix D "Product catalogue": Customer R, Reception R, Inventory R, Shop Mgr F, Admin F; Nurse,
/// Vet, Caretaker, Accountant absent (U-X4). "Customer R" is a public/customer-session read, not a
/// staff action, so it carries no row here -- see the public/customer routes (§12.4).
/// </summary>
public static partial class ActionRoles
{
    private static readonly (string Action, string[] Roles)[] ShopRows =
    [
        (Actions.Shop.ProductsView, [StaffRoles.Reception, StaffRoles.Inventory, StaffRoles.ShopManager, StaffRoles.Administrator]),
        (Actions.Shop.ProductsManage, [StaffRoles.ShopManager, StaffRoles.Administrator]),
        (Actions.Shop.ProductsManagePrices, [StaffRoles.ShopManager, StaffRoles.Administrator]),
        (Actions.Shop.SourcingOriginsManage, [StaffRoles.ShopManager, StaffRoles.Administrator]),
        (Actions.Shop.ReviewsModerate, [StaffRoles.ShopManager, StaffRoles.Administrator]),
    ];
}
