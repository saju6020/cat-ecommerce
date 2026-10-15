namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-ORD-001 §12.3 -- Ordering's rows in the solution-wide action-to-role table (ADR-036/ADR-341). Appendix D "Orders &amp;
/// fulfilment": Customer R own, Reception W, Inventory R, Shop Mgr F, Accountant R, Admin F. Customer R (own) is a customer-session
/// read, not a staff action, so it carries no row. OQ-16: packing and dispatch are Reception, ShopManager and Administrator;
/// Inventory reads pick lists. <c>Refunds.Issue</c> is Billing's row and is not repeated.
/// </summary>
public static partial class ActionRoles
{
    private static readonly (string Action, string[] Roles)[] OrdersRows =
    [
        (Actions.Orders.View, [StaffRoles.Reception, StaffRoles.Inventory, StaffRoles.ShopManager, StaffRoles.Accountant, StaffRoles.Administrator]),
        (Actions.Orders.Fulfil, [StaffRoles.Reception, StaffRoles.ShopManager, StaffRoles.Administrator]),
        (Actions.Orders.RecordDelivery, [StaffRoles.Reception, StaffRoles.ShopManager, StaffRoles.Administrator]),
        (Actions.Orders.ConfirmPayment, [StaffRoles.Reception, StaffRoles.ShopManager, StaffRoles.Accountant, StaffRoles.Administrator]),
        (Actions.Orders.ApprovePayment, [StaffRoles.Accountant, StaffRoles.Administrator]),
        (Actions.Orders.Cancel, [StaffRoles.Reception, StaffRoles.ShopManager, StaffRoles.Administrator]),
        (Actions.Orders.Reallocate, [StaffRoles.Inventory, StaffRoles.ShopManager, StaffRoles.Administrator]),
        (Actions.Orders.Reconcile, [StaffRoles.Accountant, StaffRoles.ShopManager, StaffRoles.Administrator]),
        (Actions.Returns.Manage, [StaffRoles.Reception, StaffRoles.ShopManager, StaffRoles.Administrator]),
        (Actions.Pos.Sell, [StaffRoles.Reception, StaffRoles.ShopManager, StaffRoles.Administrator]),
        (Actions.DeliveryZones.View, [StaffRoles.ShopManager, StaffRoles.Accountant, StaffRoles.Administrator]),
        (Actions.DeliveryZones.Manage, [StaffRoles.ShopManager, StaffRoles.Administrator]),
        (Actions.Couriers.Manage, [StaffRoles.ShopManager, StaffRoles.Administrator]),
        (Actions.Discounts.View, [StaffRoles.ShopManager, StaffRoles.Accountant, StaffRoles.Administrator]),
        (Actions.Discounts.Manage, [StaffRoles.ShopManager, StaffRoles.Administrator]),
        (Actions.Orders.ManagePaymentSettings, [StaffRoles.Administrator]),
    ];
}
