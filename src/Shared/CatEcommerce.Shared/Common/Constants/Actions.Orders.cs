namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-ORD-001 §12.3 -- Ordering's action names, appended per ADR-341. <c>Refunds.Issue</c> already exists
/// (<see cref="Refunds"/>, Billing) with the same roles Appendix D gives "Refunds &amp; credit notes" and is reused, not redeclared.</summary>
public static partial class Actions
{
    public static class Orders
    {
        public const string View = "Orders.View";
        public const string Fulfil = "Orders.Fulfil";
        public const string RecordDelivery = "Orders.RecordDelivery";
        public const string ConfirmPayment = "Orders.ConfirmPayment";
        public const string ApprovePayment = "Orders.ApprovePayment";
        public const string Cancel = "Orders.Cancel";
        public const string Reallocate = "Orders.Reallocate";
        public const string Reconcile = "Orders.Reconcile";
        public const string ManagePaymentSettings = "Orders.ManagePaymentSettings";
    }

    public static class Returns
    {
        public const string Manage = "Returns.Manage";
    }

    public static class Pos { public const string Sell = "Pos.Sell"; }

    public static class DeliveryZones
    {
        public const string View = "DeliveryZones.View";
        public const string Manage = "DeliveryZones.Manage";
    }

    public static class Couriers { public const string Manage = "Couriers.Manage"; }

    public static class Discounts
    {
        public const string View = "Discounts.View";
        public const string Manage = "Discounts.Manage";
    }
}
