using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-RPT-001 section 8.3 phase C -- R5 to R8: stock, expiry, reorder and import consignments (US-RPT-02, SRS section 8.1).</summary>
public static partial class ReportCatalogue
{
    public const string StockPositionKey = "stock-position";
    public const string StockNearExpiryKey = "stock-near-expiry";
    public const string StockLowReorderKey = "stock-low-reorder";
    public const string ImportConsignmentsKey = "import-consignments";

    private static readonly IReadOnlyList<ReportDefinition> StockDefinitions =
    [
        // R5 -- the roll-forward of the immutable ledger; valuation only where a landed cost exists (ADR-284, OQ-4).
        new ReportDefinition(
            StockPositionKey, "Stock on hand and movement",
            "Per SKU, batch and location: opening stock, what came in and went out, adjustments and closing stock at the end of the range, valued at landed cost where one is known.",
            ReportCategory.Stock, ReportKind.AsAt, ReportRelativePeriod.MonthToDate, 366,
            [new ReportParameter("includeZero", "Include lines with no stock and no movement", ReportParameterType.Boolean, "false")],
            [
                new ReportColumn("skuCode", "SKU", ReportColumnType.Text),
                new ReportColumn("productName", "Product", ReportColumnType.Text),
                new ReportColumn("category", "Category", ReportColumnType.Text),
                new ReportColumn("lotNumber", "Batch", ReportColumnType.Text),
                new ReportColumn("expiryOn", "Expiry", ReportColumnType.Date),
                new ReportColumn("locationName", "Location", ReportColumnType.Text),
                new ReportColumn("batchStatus", "Batch status", ReportColumnType.Text),
                new ReportColumn("openingQuantity", "Opening", ReportColumnType.Decimal),
                new ReportColumn("receivedQuantity", "In", ReportColumnType.Decimal),
                new ReportColumn("issuedQuantity", "Out", ReportColumnType.Decimal),
                new ReportColumn("adjustedQuantity", "Adjustments", ReportColumnType.Decimal),
                new ReportColumn("closingQuantity", "Closing", ReportColumnType.Decimal),
                new ReportColumn("landedUnitCost", "Landed unit cost", ReportColumnType.Money),
                new ReportColumn("valuation", "Valuation", ReportColumnType.Money),
            ],
            "productName", true, false, ["IMP", "SHP"]),

        // R6 -- the expiry window; the bands come from the thresholds HO-IMP-001 configures (default 90 / 60 / 30).
        new ReportDefinition(
            StockNearExpiryKey, "Near-expiry and expired stock",
            "Batches with stock that expire in the window, or already have, with the value at risk at landed cost and at today's retail price.",
            ReportCategory.Stock, ReportKind.Period, ReportRelativePeriod.Next90Days, 366,
            [new ReportParameter("includeExpired", "Include batches already past expiry", ReportParameterType.Boolean, "true")],
            [
                new ReportColumn("skuCode", "SKU", ReportColumnType.Text),
                new ReportColumn("productName", "Product", ReportColumnType.Text),
                new ReportColumn("lotNumber", "Batch", ReportColumnType.Text),
                new ReportColumn("locationName", "Location", ReportColumnType.Text),
                new ReportColumn("expiryOn", "Expiry", ReportColumnType.Date),
                new ReportColumn("daysToExpiry", "Days to expiry", ReportColumnType.Integer, "days"),
                new ReportColumn("band", "Band", ReportColumnType.Text),
                new ReportColumn("quantity", "Quantity", ReportColumnType.Decimal),
                new ReportColumn("landedUnitCost", "Landed unit cost", ReportColumnType.Money),
                new ReportColumn("valueAtRiskCost", "Value at risk (cost)", ReportColumnType.Money),
                new ReportColumn("valueAtRiskRetail", "Value at risk (retail)", ReportColumnType.Money),
            ],
            "expiryOn", true, false, ["IMP", "SHP"]),

        // R7 -- the suggestion is HO-IMP-001's own calculation, called and not re-implemented (U-C4).
        new ReportDefinition(
            StockLowReorderKey, "Low stock and reorder suggestions",
            "SKUs below their reorder point per location, with the average daily consumption over the window and the suggested order quantity from the inventory rules.",
            ReportCategory.Stock, ReportKind.Period, ReportRelativePeriod.Last30Days, 365, [],
            [
                new ReportColumn("skuCode", "SKU", ReportColumnType.Text),
                new ReportColumn("productName", "Product", ReportColumnType.Text),
                new ReportColumn("locationName", "Location", ReportColumnType.Text),
                new ReportColumn("onHand", "On hand", ReportColumnType.Decimal),
                new ReportColumn("reorderPoint", "Reorder point", ReportColumnType.Decimal),
                new ReportColumn("averageDailyConsumption", "Average daily use", ReportColumnType.Decimal),
                new ReportColumn("supplierName", "Supplier", ReportColumnType.Text),
                new ReportColumn("leadTimeDays", "Lead time (days)", ReportColumnType.Integer, "days"),
                new ReportColumn("suggestedQuantity", "Suggested order", ReportColumnType.Decimal),
            ],
            "productName", false, false, ["IMP"]),

        // R8 -- SRS section 8.1's import consignment report.
        new ReportDefinition(
            ImportConsignmentsKey, "Import consignments",
            "Consignments created in the range: origin, supplier, purchase orders, clearance status, landed cost and which clearance documents are on record.",
            ReportCategory.Stock, ReportKind.Period, ReportRelativePeriod.Last30Days, 366, [],
            [
                new ReportColumn("reference", "Reference", ReportColumnType.Text),
                new ReportColumn("shipmentReference", "Shipment", ReportColumnType.Text),
                new ReportColumn("originCountry", "Origin", ReportColumnType.Text),
                new ReportColumn("supplierName", "Supplier", ReportColumnType.Text),
                new ReportColumn("poCount", "Purchase orders", ReportColumnType.Integer),
                new ReportColumn("clearanceStatus", "Clearance", ReportColumnType.Text),
                new ReportColumn("landedCostTotal", "Landed cost", ReportColumnType.Money),
                new ReportColumn("hasPermit", "Import permit", ReportColumnType.Boolean),
                new ReportColumn("hasHealthCertificate", "Health certificate", ReportColumnType.Boolean),
                new ReportColumn("hasCustomsDeclaration", "Customs declaration", ReportColumnType.Boolean),
                new ReportColumn("batchesReceived", "Batches received", ReportColumnType.Integer),
                new ReportColumn("createdOn", "Created", ReportColumnType.Instant),
                new ReportColumn("clearedOn", "Cleared", ReportColumnType.Date),
            ],
            "createdOn", false, false, ["IMP"]),
    ];
}
