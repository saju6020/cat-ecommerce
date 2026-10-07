using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>Shop (HO-SHP-001) table classifications -- HO-XCT-001 §5.2 C-ALL-1, added in the same
/// change that adds each table (ADR-341, one partial per area).</summary>
public static partial class RecordClassification
{
    private static readonly IReadOnlyList<TableClassification> ShopEntries =
    [
        new TableClassification("product", "product", RecordClass.Operational, HardDeletePolicy.Forbidden, "Catalogue record; never deleted (ADR-095)"),

        // Operational, not CurrentState: individual SKUs are added/discontinued, never wholesale
        // replaced, and other contexts (Inventory, Orders, Pharmacy) hold a SKU id for years
        // (ADR-079) -- the same "referenced by other tables, permanent identifier" basis as
        // clinic.vaccine.
        new TableClassification("product", "sku", RecordClass.Operational, HardDeletePolicy.Forbidden, "ADR-079: other contexts hold SKU ids for years; never reused or deleted"),

        // CurrentState: ProductAggregate.SetImages wholesale-replaces the owned collection
        // (_images.Clear() then re-add), the same carve-out as admin.branch_opening_interval.
        new TableClassification("product", "product_image", RecordClass.CurrentState, HardDeletePolicy.AggregateOnly, "SetImages replaces the collection wholesale"),

        // Financial: "a price ... in force" (HO-XCT-001 §8.2's ordered rule), append-only and
        // effective-dated (ADR-080, HO-ADM-001 ADR-049 applied to goods) -- never edited or
        // deleted, mirroring admin.tax_rate/admin.service_price.
        new TableClassification("product", "sku_price", RecordClass.Financial, HardDeletePolicy.Forbidden, "D-9 (price in force); append-only (ADR-080)"),

        new TableClassification("product", "sourcing_origin", RecordClass.Operational, HardDeletePolicy.Forbidden, "Shop-by-Country reference data (US-SHP-05)"),

        // CurrentState: a system-written copy another context pushes on its own schedule
        // (ADR-082, narrowed) -- not history, the current 90-day figure only.
        new TableClassification("product", "product_sales_stats", RecordClass.CurrentState, HardDeletePolicy.AggregateOnly, "System-written copy, display only (ADR-082)"),

        // CustomerPersonal: customer-authored content tied to an identity (the same basis as
        // clinic.cat_photo) -- but never erased on anonymisation by explicit design (ADR-096: a
        // published review stays public, credited to "A verified buyer"; only the display name
        // changes, derived at read time from account.customer_account, never stored here).
        new TableClassification("product", "product_review", RecordClass.CustomerPersonal, HardDeletePolicy.Forbidden, "ADR-096: published reviews stay after anonymisation"),

        // CurrentState: owned collections of ProductAggregate that SetBundleComponents /
        // SetRelatedProducts / SetTranslation replace through the aggregate (same carve-out as
        // product_image). Created by Phase A's migration; classified with the phase that owns them.
        new TableClassification("product", "bundle_component", RecordClass.CurrentState, HardDeletePolicy.AggregateOnly, "SetBundleComponents replaces the collection wholesale"),
        new TableClassification("product", "related_product", RecordClass.CurrentState, HardDeletePolicy.AggregateOnly, "SetRelatedProducts replaces the collection wholesale"),
        new TableClassification("product", "product_translation", RecordClass.CurrentState, HardDeletePolicy.AggregateOnly, "A translation is replaced or removed through the product aggregate"),

        // CustomerPersonal: a customer's wishlist and alerts. The wishlist row is never deleted; its
        // items are removed through the aggregate (remove, anonymise, merge -- ADR-096). An alert is
        // closed, never deleted (its status is the history of "told once").
        new TableClassification("product", "wishlist", RecordClass.CustomerPersonal, HardDeletePolicy.Forbidden, "Id == CustomerId; emptied, never deleted (ADR-096)"),
        new TableClassification("product", "wishlist_item", RecordClass.CustomerPersonal, HardDeletePolicy.AggregateOnly, "Removed through WishlistAggregate (remove, anonymise, merge)"),
        new TableClassification("product", "stock_alert", RecordClass.CustomerPersonal, HardDeletePolicy.Forbidden, "Closed, never deleted; Notified is the once-only record (ADR-087)"),
    ];
}
