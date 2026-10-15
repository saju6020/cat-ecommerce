namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-IMP-001 §12.3 -- Inventory's action names, appended per ADR-341. Appendix D "Stock and
/// batches" and "Purchase orders / imports".</summary>
public static partial class Actions
{
    public static class Stock
    {
        public const string View = "Stock.View";
        public const string Receive = "Stock.Receive";
        public const string Transfer = "Stock.Transfer";
        public const string Adjust = "Stock.Adjust";
        public const string ApproveAdjustment = "Stock.ApproveAdjustment";
        public const string Count = "Stock.Count";
    }

    public static class Batches { public const string ManageStatus = "Batches.ManageStatus"; }
    public static class Recalls { public const string Manage = "Recalls.Manage"; }
    public static class StockAlerts { public const string Acknowledge = "StockAlerts.Acknowledge"; }

    public static class StorageBreaches
    {
        public const string Report = "StorageBreaches.Report";
        public const string Manage = "StorageBreaches.Manage";
    }

    public static class ProductStorage { public const string Manage = "ProductStorage.Manage"; }
    public static class StockLocations { public const string Manage = "StockLocations.Manage"; }
    public static class ReorderPolicies { public const string Manage = "ReorderPolicies.Manage"; }
    public static class InventoryReports { public const string View = "InventoryReports.View"; }

    public static class Procurement
    {
        public const string View = "Procurement.View";
        public const string Manage = "Procurement.Manage";
    }

    public static class Consignments { public const string ManageCosts = "Consignments.ManageCosts"; }
}
