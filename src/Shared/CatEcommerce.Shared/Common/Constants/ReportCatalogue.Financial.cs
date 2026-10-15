using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-RPT-001 section 8.3 phase D -- R9 to R17: revenue, margin and payments (US-RPT-03, SRS section 8.2). There is no payment gateway: the payment reports cover exactly the four launch methods.</summary>
public static partial class ReportCatalogue
{
    public const string RevenueByBusinessLineKey = "revenue-by-business-line";
    public const string RevenueShopByCategoryKey = "revenue-shop-by-category";
    public const string RevenueShopByOriginKey = "revenue-shop-by-origin";
    public const string RevenueByServiceKey = "revenue-by-service";
    /// <summary>R13 (HO-RPT-001-FU1). The direction is read off Received-or-counted against Expected, never off the variance sign: cash on delivery and bank transfer rows show expected minus received, the cash row shows counted minus expected.</summary>
    public const string VarianceDirectionR13 = "Over: more was received or counted than expected. Short: less was received or counted than expected. Matched: equal. Empty when there is nothing to compare against (card in store, a cash day not yet counted). Use this, not the sign of Variance: cash on delivery and bank transfer rows show expected minus received, the cash row shows counted minus expected.";

    /// <summary>R14 (HO-RPT-001-FU1). The variance here is counted minus expected, so a positive variance is Over.</summary>
    public const string VarianceDirectionR14 = "Over: more cash was counted than expected (positive variance). Short: less was counted than expected (negative variance). Matched: equal. Empty for a day not closed or not yet counted.";

    public const string PaymentReconciliationKey = "payment-reconciliation";
    public const string DailyCashUpKey = "daily-cash-up";
    public const string ReceivablesAgeingKey = "receivables-ageing";
    public const string RefundsCreditNotesKey = "refunds-credit-notes";
    public const string TaxSummaryKey = "tax-summary";

    private static readonly IReadOnlyList<ReportDefinition> FinancialDefinitions =
    [
        // R9 -- ADR-283 (revenue) and ADR-284 (cost and margin).
        new ReportDefinition(
            RevenueByBusinessLineKey, "Revenue by business line",
            "Net revenue (after discounts, before tax, less credit notes) for clinic, shop and boarding, with the cost of goods and margin where the landed cost is known, and how much of the revenue that margin covers.",
            ReportCategory.Financial, ReportKind.Period, ReportRelativePeriod.PreviousMonth, 731, [],
            [
                new ReportColumn("bucket", "Period", ReportColumnType.Text),
                new ReportColumn("businessLine", "Business line", ReportColumnType.Text),
                new ReportColumn("gross", "Gross", ReportColumnType.Money),
                new ReportColumn("discounts", "Discounts", ReportColumnType.Money),
                new ReportColumn("creditNotes", "Credit notes", ReportColumnType.Money),
                new ReportColumn("netRevenue", "Net revenue", ReportColumnType.Money),
                new ReportColumn("tax", "Tax", ReportColumnType.Money),
                new ReportColumn("cogs", "Cost of goods", ReportColumnType.Money),
                new ReportColumn("margin", "Margin", ReportColumnType.Money),
                new ReportColumn("marginPercent", "Margin %", ReportColumnType.Percent),
                new ReportColumn("coveragePercent", "Margin coverage %", ReportColumnType.Percent),
            ],
            "bucket", true, true, ["BIL", "IMP", "ORD"]),

        // R10 -- cost and margin columns are removed from the payload for anyone without Reports.Financial.View (OQ-5, ADR-281).
        new ReportDefinition(
            RevenueShopByCategoryKey, "Shop revenue by category",
            "Shop net revenue by product category and sub-category. Cost and margin are shown to those who may see finances.",
            ReportCategory.Sales, ReportKind.Period, ReportRelativePeriod.PreviousMonth, 731, [],
            [
                new ReportColumn("bucket", "Period", ReportColumnType.Text),
                new ReportColumn("category", "Category", ReportColumnType.Text),
                new ReportColumn("subCategory", "Sub-category", ReportColumnType.Text),
                new ReportColumn("units", "Units", ReportColumnType.Decimal),
                new ReportColumn("netRevenue", "Net revenue", ReportColumnType.Money),
                new ReportColumn("cogs", "Cost of goods", ReportColumnType.Money, RestrictedToAction: Actions.Reports.FinancialView),
                new ReportColumn("margin", "Margin", ReportColumnType.Money, RestrictedToAction: Actions.Reports.FinancialView),
                new ReportColumn("marginPercent", "Margin %", ReportColumnType.Percent, RestrictedToAction: Actions.Reports.FinancialView),
                new ReportColumn("coveragePercent", "Margin coverage %", ReportColumnType.Percent, RestrictedToAction: Actions.Reports.FinancialView),
            ],
            "bucket", true, true, ["BIL", "SHP", "IMP"]),

        new ReportDefinition(
            RevenueShopByOriginKey, "Shop revenue by country of origin",
            "Shop net revenue by the country of origin of the product. Products with no origin, delivery and other charges are grouped as 'Not applicable'.",
            ReportCategory.Sales, ReportKind.Period, ReportRelativePeriod.PreviousMonth, 731, [],
            [
                new ReportColumn("bucket", "Period", ReportColumnType.Text),
                new ReportColumn("origin", "Country of origin", ReportColumnType.Text),
                new ReportColumn("units", "Units", ReportColumnType.Decimal),
                new ReportColumn("netRevenue", "Net revenue", ReportColumnType.Money),
                new ReportColumn("cogs", "Cost of goods", ReportColumnType.Money, RestrictedToAction: Actions.Reports.FinancialView),
                new ReportColumn("margin", "Margin", ReportColumnType.Money, RestrictedToAction: Actions.Reports.FinancialView),
                new ReportColumn("marginPercent", "Margin %", ReportColumnType.Percent, RestrictedToAction: Actions.Reports.FinancialView),
                new ReportColumn("coveragePercent", "Margin coverage %", ReportColumnType.Percent, RestrictedToAction: Actions.Reports.FinancialView),
            ],
            "bucket", true, true, ["BIL", "SHP", "IMP"]),

        new ReportDefinition(
            RevenueByServiceKey, "Revenue by service and product group",
            "Net revenue by business line and service or product group, with cost of goods and margin where known.",
            ReportCategory.Financial, ReportKind.Period, ReportRelativePeriod.PreviousMonth, 731, [],
            [
                new ReportColumn("bucket", "Period", ReportColumnType.Text),
                new ReportColumn("businessLine", "Business line", ReportColumnType.Text),
                new ReportColumn("group", "Service or product group", ReportColumnType.Text),
                new ReportColumn("quantity", "Quantity", ReportColumnType.Decimal),
                new ReportColumn("netRevenue", "Net revenue", ReportColumnType.Money),
                new ReportColumn("cogs", "Cost of goods", ReportColumnType.Money),
                new ReportColumn("margin", "Margin", ReportColumnType.Money),
                new ReportColumn("coveragePercent", "Margin coverage %", ReportColumnType.Percent),
            ],
            "bucket", true, true, ["BIL", "IMP", "SHP"]),

        // R13 -- the four launch methods and nothing else; no gateway (FR-ORD-8).
        new ReportDefinition(
            PaymentReconciliationKey, "Payment reconciliation",
            "Per day and payment method: what was expected, what has been received or counted, and the variance. Cash on delivery against courier remittances, bank transfers against the bank statement, cash against the till count.",
            ReportCategory.Financial, ReportKind.Period, ReportRelativePeriod.PreviousDay, 366, [],
            [
                new ReportColumn("businessDate", "Business date", ReportColumnType.Date),
                new ReportColumn("branchName", "Branch", ReportColumnType.Text),
                new ReportColumn("method", "Method", ReportColumnType.Text),
                new ReportColumn("expected", "Expected", ReportColumnType.Money),
                new ReportColumn("received", "Received or counted", ReportColumnType.Money),
                new ReportColumn("variance", "Variance", ReportColumnType.Money),
                new ReportColumn("varianceDirection", "Variance direction", ReportColumnType.Text, Description: VarianceDirectionR13),
                new ReportColumn("matched", "Matched", ReportColumnType.Integer),
                new ReportColumn("mismatchesOpen", "Mismatches open", ReportColumnType.Integer),
            ],
            "businessDate", false, false, ["BIL"]),

        new ReportDefinition(
            DailyCashUpKey, "Daily cash-up",
            "The cash-up of each business day as recorded: takings by method, the expected and counted cash, the variance and who closed the day. A day not yet closed says so.",
            ReportCategory.Financial, ReportKind.Period, ReportRelativePeriod.Today, 366, [],
            [
                new ReportColumn("businessDate", "Business date", ReportColumnType.Date),
                new ReportColumn("branchName", "Branch", ReportColumnType.Text),
                new ReportColumn("status", "Status", ReportColumnType.Text),
                new ReportColumn("cash", "Cash", ReportColumnType.Money),
                new ReportColumn("cardInStore", "Card in store", ReportColumnType.Money),
                new ReportColumn("bankTransfer", "Bank transfer", ReportColumnType.Money),
                new ReportColumn("expected", "Expected cash", ReportColumnType.Money),
                new ReportColumn("counted", "Counted cash", ReportColumnType.Money),
                new ReportColumn("variance", "Variance", ReportColumnType.Money),
                new ReportColumn("varianceDirection", "Variance direction", ReportColumnType.Text, Description: VarianceDirectionR14),
                new ReportColumn("explanation", "Explanation", ReportColumnType.Text, Sortable: false),
                new ReportColumn("closedBy", "Closed by", ReportColumnType.Text),
            ],
            "businessDate", false, false, ["BIL"]),

        new ReportDefinition(
            ReceivablesAgeingKey, "Receivables ageing",
            "Who owes what, by customer, aged from the invoice issue date to the end of the range. Balances are as they stand when the report is run.",
            ReportCategory.Financial, ReportKind.AsAt, ReportRelativePeriod.Today, 366, [],
            [
                new ReportColumn("customerName", "Customer", ReportColumnType.Text),
                new ReportColumn("invoicesOutstanding", "Invoices outstanding", ReportColumnType.Integer),
                new ReportColumn("balance", "Balance", ReportColumnType.Money),
                new ReportColumn("age0To30", "0-30 days", ReportColumnType.Money),
                new ReportColumn("age31To60", "31-60 days", ReportColumnType.Money),
                new ReportColumn("age61To90", "61-90 days", ReportColumnType.Money),
                new ReportColumn("ageOver90", "Over 90 days", ReportColumnType.Money),
            ],
            "-balance", true, false, ["BIL"]),

        new ReportDefinition(
            RefundsCreditNotesKey, "Refunds and credit notes",
            "Credit notes issued in the range by reason, and how each was settled: refunded in cash in store, refunded by bank transfer, kept as customer credit or applied to the invoice balance.",
            ReportCategory.Financial, ReportKind.Period, ReportRelativePeriod.PreviousMonth, 731, [],
            [
                new ReportColumn("reason", "Reason", ReportColumnType.Text),
                new ReportColumn("refundMethod", "Settled by", ReportColumnType.Text),
                new ReportColumn("count", "Count", ReportColumnType.Integer),
                new ReportColumn("amount", "Amount", ReportColumnType.Money),
            ],
            "reason", true, false, ["BIL", "ORD"]),

        new ReportDefinition(
            TaxSummaryKey, "Tax summary",
            "Output tax by tax class and rate: the taxable net and the tax on issued invoices, with credit notes as negative lines.",
            ReportCategory.Financial, ReportKind.Period, ReportRelativePeriod.PreviousMonth, 731, [],
            [
                new ReportColumn("bucket", "Period", ReportColumnType.Text),
                new ReportColumn("source", "Source", ReportColumnType.Text),
                new ReportColumn("taxClass", "Tax class", ReportColumnType.Text),
                new ReportColumn("ratePercent", "Rate %", ReportColumnType.Percent),
                new ReportColumn("taxableNet", "Taxable net", ReportColumnType.Money),
                new ReportColumn("outputTax", "Output tax", ReportColumnType.Money),
            ],
            "bucket", true, true, ["BIL"]),
    ];
}
