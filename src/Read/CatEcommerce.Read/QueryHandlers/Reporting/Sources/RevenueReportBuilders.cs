using System.Globalization;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence.ReportingViews;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Read.QueryHandlers.Reporting.Sources;

/// <summary>One movement of the stock ledger that touched an invoice line, with the batch's landed unit cost (null when the batch has none).</summary>
public sealed record LedgerCostEntry(int MovementKind, decimal QuantityDelta, DateTimeOffset OccurredOn, decimal? LandedUnitCost);

/// <summary>The cost of one invoice line: its cost of goods, whether the line is costed at all, and the cost of any returns (each with its own date).</summary>
public sealed record LineCost(decimal SaleCogs, bool Costed, IReadOnlyList<(DateTimeOffset At, decimal Cost)> Returns)
{
    public static readonly LineCost Uncosted = new(0m, false, []);
}

/// <summary>
/// HO-RPT-001 section 8.6 / ADR-284 -- the cost of goods behind an invoice line, <b>pure</b>. The stock ledger entries that carry the line's id as their source line are the sale-type movements
/// (<c>Sale</c>, <c>Dispense</c>, <c>BoardingUse</c>, cost = -delta x landed unit cost) and the <c>Return</c>s (cost = delta x landed unit cost, subtracted on the return's own date). A line is
/// <b>costed</b> only if it has sale-type entries and <b>every</b> contributing batch has a landed cost; a partly costed line is treated as uncosted. There is no fallback to the purchase-order cost and no estimate.
/// </summary>
public static class LineCosting
{
    private static readonly int[] SaleKinds = [MovementKind.Sale.Value, MovementKind.Dispense.Value, MovementKind.BoardingUse.Value];

    public static LineCost Cost(IEnumerable<LedgerCostEntry> entries)
    {
        var all = entries.Where(e => SaleKinds.Contains(e.MovementKind) || e.MovementKind == MovementKind.Return.Value).ToList();
        var sales = all.Where(e => SaleKinds.Contains(e.MovementKind)).ToList();

        if (sales.Count == 0 || all.Any(e => e.LandedUnitCost is null)) return LineCost.Uncosted;

        var cogs = sales.Sum(e => -e.QuantityDelta * e.LandedUnitCost!.Value);
        var returns = all.Where(e => e.MovementKind == MovementKind.Return.Value).Select(e => (e.OccurredOn, e.QuantityDelta * e.LandedUnitCost!.Value)).ToList();
        return new LineCost(cogs, true, returns);
    }
}

public enum RevenueFactKind
{
    /// <summary>An invoice line in the report's range: revenue, tax, discounts, and (if costed) its cost of goods.</summary>
    Invoice,

    /// <summary>A credit note line: reduces net revenue and tax in ITS OWN period.</summary>
    Credit,

    /// <summary>The cost of goods of a returned item: reduces cost in the period of the return.</summary>
    ReturnCost,
}

/// <summary>
/// One revenue event, ready to be grouped. <c>Date</c> is the branch-local date that decides its period: the invoice issue date, the credit note date or the return date. The dimension labels are
/// resolved once, by <see cref="RevenueFacts"/>, so a report only chooses which of them to group by.
/// </summary>
public sealed record RevenueFact(
    DateOnly Date, string Currency, RevenueFactKind Kind, string Line, string Category, string SubCategory, string Origin, string ServiceGroup,
    decimal Quantity, decimal Gross, decimal Discounts, decimal Net, decimal Tax, decimal Cogs, bool Costed);

/// <summary>Builds <see cref="RevenueFact"/>s from the view rows and the cost of each line.</summary>
public static class RevenueFacts
{
    public const string DeliveryAndFees = "Delivery and fees";
    public const string NotApplicable = "Not applicable";

    public static (string Line, string Category, string SubCategory, string Origin, string ServiceGroup) Dimensions(int businessLine, int itemKind, int? serviceCategory, RvSkuRow? sku)
    {
        var line = BusinessLineRules.Classify(businessLine);
        var category = sku is null ? DeliveryAndFees : ReportLabels.Of<ProductCategory>(sku.Category);
        var subCategory = sku?.SubCategory is { } sub ? ReportLabels.Of<ProductSubcategory>(sub) : "—";
        var origin = string.IsNullOrWhiteSpace(sku?.CountryOfOrigin) ? NotApplicable : sku!.CountryOfOrigin!.Trim();

        var group = businessLine == BusinessLine.Shop.Value ? category
            : businessLine == BusinessLine.Clinic.Value && serviceCategory is { } sc ? ReportLabels.Of<ServiceCategory>(sc)
            : ReportLabels.Of<ItemKind>(itemKind);

        return (line, category, subCategory, origin, group);
    }

    public static RevenueFact FromInvoiceLine(RvInvoiceLineRow row, RvSkuRow? sku, LineCost cost, DateOnly date)
    {
        var d = Dimensions(row.BusinessLine, row.ItemKind, row.ServiceCategory, sku);
        return new RevenueFact(
            date, row.CurrencyCode, RevenueFactKind.Invoice, d.Line, d.Category, d.SubCategory, d.Origin, d.ServiceGroup,
            row.Quantity, row.Quantity * row.UnitPrice, row.LineDiscount + row.InvoiceDiscountShare, row.NetAmount, row.TaxAmount,
            cost.Costed ? cost.SaleCogs : 0m, cost.Costed);
    }

    public static RevenueFact FromCreditLine(RvCreditNoteLineRow row, RvSkuRow? sku, LineCost cost, DateOnly date)
    {
        var d = Dimensions(row.BusinessLine, row.ItemKind, row.ServiceCategory, sku);
        return new RevenueFact(date, row.CurrencyCode, RevenueFactKind.Credit, d.Line, d.Category, d.SubCategory, d.Origin, d.ServiceGroup, 0m, 0m, 0m, row.NetAmount, row.TaxAmount, 0m, cost.Costed);
    }

    public static RevenueFact ReturnCost(RvInvoiceLineRow row, RvSkuRow? sku, decimal cost, DateOnly date, string currency)
    {
        var d = Dimensions(row.BusinessLine, row.ItemKind, row.ServiceCategory, sku);
        return new RevenueFact(date, currency, RevenueFactKind.ReturnCost, d.Line, d.Category, d.SubCategory, d.Origin, d.ServiceGroup, 0m, 0m, 0m, 0m, 0m, cost, true);
    }
}

/// <summary>One group of the revenue aggregation. Margin is null when no revenue of the group is costed (never zero); coverage is costed revenue over net revenue.</summary>
public sealed record RevenueAggregate(
    string Bucket, IReadOnlyList<string> Dimensions, string Currency, decimal Quantity, decimal Gross, decimal Discounts, decimal CreditNotes, decimal Net, decimal Tax,
    decimal Cogs, decimal CostedNet)
{
    public decimal? Margin => CostedNet > 0m ? CostedNet - Cogs : null;

    public decimal? MarginPercent => Margin is { } margin ? ReportMath.Percent(margin, CostedNet) : null;

    public decimal? CoveragePercent => Net > 0m ? ReportMath.Percent(CostedNet, Net) : null;
}

/// <summary>
/// HO-RPT-001 section 8.6 / ADR-283 / ADR-284 -- revenue, cost and margin, <b>pure</b>. <b>Net revenue</b> = the invoice lines' net (after line discounts and the order discount share, excluding tax) issued
/// in the period minus the credit note lines dated in the period; a credit note counts on its own date, so a closed period never changes. <b>Margin</b> is the net revenue of costed lines minus their cost
/// of goods; the cost of a return is subtracted on the return's date. <b>Coverage</b> = costed net revenue over total net revenue. Nothing is ever summed across currencies: each currency is its own group.
/// </summary>
public static class RevenueBuilder
{
    public static IReadOnlyList<RevenueAggregate> Aggregate(IEnumerable<RevenueFact> facts, TimeBucket bucket, Func<RevenueFact, string[]> dimensions)
    {
        var groups = facts.GroupBy(f => (Bucket: ReportBuckets.Label(f.Date, bucket), Currency: f.Currency, Dims: string.Join("\u001f", dimensions(f))));
        var result = new List<RevenueAggregate>();

        foreach (var group in groups)
        {
            var invoices = group.Where(f => f.Kind == RevenueFactKind.Invoice).ToList();
            var credits = group.Where(f => f.Kind == RevenueFactKind.Credit).ToList();
            var returns = group.Where(f => f.Kind == RevenueFactKind.ReturnCost).ToList();

            var creditNet = credits.Sum(f => f.Net);
            result.Add(new RevenueAggregate(
                group.Key.Bucket, dimensions(group.First()), group.Key.Currency,
                invoices.Sum(f => f.Quantity), invoices.Sum(f => f.Gross), invoices.Sum(f => f.Discounts), creditNet,
                invoices.Sum(f => f.Net) - creditNet, invoices.Sum(f => f.Tax) - credits.Sum(f => f.Tax),
                invoices.Sum(f => f.Cogs) - returns.Sum(f => f.Cogs),
                invoices.Where(f => f.Costed).Sum(f => f.Net) - credits.Where(f => f.Costed).Sum(f => f.Net)));
        }

        return result.OrderBy(a => a.Bucket, StringComparer.Ordinal).ThenBy(a => string.Join("\u001f", a.Dimensions), StringComparer.OrdinalIgnoreCase).ThenBy(a => a.Currency, StringComparer.Ordinal).ToList();
    }

    /// <summary>The margin coverage over everything in <paramref name="aggregates"/>, per currency; one currency gives the headline <see cref="ReportCoverage"/>, several give none (never added up).</summary>
    public static ReportCoverage? Coverage(IReadOnlyList<RevenueAggregate> aggregates)
    {
        var currencies = aggregates.Select(a => a.Currency).Distinct().ToList();
        if (currencies.Count != 1) return null;

        var net = aggregates.Sum(a => a.Net);
        var costed = aggregates.Sum(a => a.CostedNet);
        return net > 0m ? new ReportCoverage(new ReportMoney(costed, currencies[0]), new ReportMoney(net, currencies[0]), ReportMath.Percent(costed, net)) : null;
    }

    /// <summary>The note that states how much of the revenue the margin covers (ADR-284); none when the margin covers everything or there is no revenue.</summary>
    public static ReportNote? CoverageNote(ReportCoverage? coverage, string scope)
    {
        if (coverage is null) return null;

        if (coverage.CostedRevenue.Amount <= 0m)
            return new ReportNote("MarginCoverage", $"Margin is not available: none of the {scope} revenue has a landed cost yet.");

        if (coverage.Percent >= 100m) return null;

        var percent = coverage.Percent.ToString("0.0", CultureInfo.InvariantCulture);
        var rest = (100m - coverage.Percent).ToString("0.0", CultureInfo.InvariantCulture);
        return new ReportNote("MarginCoverage", $"Margin covers {percent} % of {scope} revenue. {rest} % came from batches with no landed cost yet.");
    }

    public static ReportMoney? Money(decimal? value, string currency) => value is { } v ? new ReportMoney(ReportMath.Money(v), currency) : null;

    /// <summary>A totals cell summed only when every row is in one currency; otherwise omitted (never added across currencies).</summary>
    public static ReportMoney? SumIfOneCurrency(IEnumerable<ReportMoney?> values)
    {
        var present = values.OfType<ReportMoney>().ToList();
        return present.Count > 0 && present.Select(m => m.CurrencyCode).Distinct().Count() == 1 ? new ReportMoney(present.Sum(m => m.Amount), present[0].CurrencyCode) : null;
    }
}
