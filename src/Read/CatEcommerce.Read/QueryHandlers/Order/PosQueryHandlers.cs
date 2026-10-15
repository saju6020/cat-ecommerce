using System.Globalization;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Ordering;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Order;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Order;

/// <summary>Shared by the barcode and search lookups: price and sellable quantity at the branch, and the FEFO batch the next unit would come from (Inventory's own plan).</summary>
internal static class PosSkuLookup
{
    public static async Task<IReadOnlyList<PosSkuViewModel>> ViewsAsync(
        CatEcommerceDbContext db, IStockLedger ledger, IReadOnlyList<OrderCatalogueSku> skus, Guid branchId, CancellationToken ct)
    {
        if (skus.Count == 0) return [];

        var sellable = await SellableQuantities.ForAsync(db, skus, branchId, ct);
        var views = new List<PosSkuViewModel>();
        foreach (var sku in skus)
        {
            var quantity = (int)Math.Floor(Math.Min(sellable.GetValueOrDefault(sku.SkuId), int.MaxValue));
            var batches = new List<PosFefoBatchViewModel>();
            if (!sku.IsBundle && quantity > 0)
            {
                var plan = await ledger.PlanAsync(new StockDemand(branchId, sku.SkuId, 1m, MovementKind.Sale, null, null, null), ct);
                if (plan.IsSuccess) batches.AddRange(plan.Value!.Lines.Select(l => new PosFefoBatchViewModel(l.BatchId, l.LotNumber, l.ExpiresOn, l.LocationId, l.Quantity.ToString("0.###", CultureInfo.InvariantCulture))));
            }

            views.Add(new PosSkuViewModel(
                sku.SkuId, sku.ProductId, sku.ProductName, sku.VariantLabel, sku.Barcode,
                sku.CurrentPrice is { } price ? OrderMoney.Of(price, sku.CurrencyCode ?? string.Empty) : null, quantity, sku.RequiresPrescription, sku.IsBundle, batches));
        }

        return views;
    }
}

public sealed class GetPosSkuQueryHandler(CatEcommerceDbContext db, ICatalogueLookup catalogue, IStockLedger ledger, TimeProvider timeProvider)
    : IQueryHandlerAsync<GetPosSkuQuery, PosSkuViewModel?>
{
    public async Task<QueryResponse<PosSkuViewModel?>> HandleAsync(GetPosSkuQuery query)
    {
        var sku = await catalogue.GetByBarcodeAsync(query.Barcode.Trim(), timeProvider.GetUtcNow(), default);
        if (sku is not { IsPurchasable: true }) return new QueryResponse<PosSkuViewModel?> { Result = null };

        return new QueryResponse<PosSkuViewModel?> { Result = (await PosSkuLookup.ViewsAsync(db, ledger, [sku], query.BranchId, default)).Single() };
    }
}

public sealed class SearchPosSkusQueryHandler(CatEcommerceDbContext db, ICatalogueLookup catalogue, IStockLedger ledger, TimeProvider timeProvider)
    : IQueryHandlerAsync<SearchPosSkusQuery, IReadOnlyList<PosSkuViewModel>>
{
    private const int Take = 20;

    public async Task<QueryResponse<IReadOnlyList<PosSkuViewModel>>> HandleAsync(SearchPosSkusQuery query)
    {
        var found = await catalogue.SearchSellableAsync(query.Text.Trim(), Take, timeProvider.GetUtcNow(), default);
        return new QueryResponse<IReadOnlyList<PosSkuViewModel>> { Result = await PosSkuLookup.ViewsAsync(db, ledger, found, query.BranchId, default) };
    }
}

public sealed class GetCounterQuoteQueryHandler(ICheckoutInputsLoader loader, TimeProvider timeProvider) : IQueryHandlerAsync<GetCounterQuoteQuery, CounterQuoteViewModel>
{
    public async Task<QueryResponse<CounterQuoteViewModel>> HandleAsync(GetCounterQuoteQuery query)
    {
        var inputs = await loader.LoadCounterAsync(
            query.BranchId, query.CustomerId, query.Lines.Select(l => new CounterLineRequest(l.SkuId, l.Quantity, l.CatId)).ToList(), query.DiscountCode, timeProvider.GetUtcNow(), default);
        var quote = CheckoutPricing.Quote(inputs);
        var currency = quote.CurrencyCode;
        MoneyViewModel Money(MinorUnits amount) => OrderMoney.Of(amount.ToAmount(), currency);

        var view = new CounterQuoteViewModel(
            quote.Lines.Select(l => new CheckoutQuoteLineViewModel(
                l.LineRef, l.SkuId, l.ProductName, l.VariantLabel, null, l.Quantity, Money(l.UnitPrice), Money(l.Subtotal), Money(l.LineDiscount), Money(l.OrderDiscountShare),
                l.TaxRatePercent, Money(l.TaxAmount), Money(l.LineTotal))).ToList(),
            quote.ExcludedLines.Select(l => new ExcludedLineViewModel(l.LineRef, l.SkuId, l.ProductName, null, l.CartQuantity, l.ReasonCode)).ToList(),
            quote.Changes.Select(c => new CheckoutChangeViewModel(c.Kind.DisplayName, c.LineRef, c.ProductName, c.Before, c.After, c.Message)).ToList(),
            quote.Discount is null ? null : new QuoteDiscountViewModel(quote.Discount.Code, quote.Discount.Applied, quote.Discount.ReasonCode, quote.Discount.Message),
            new QuoteTotalsViewModel(
                Money(quote.Totals.Subtotal), Money(quote.Totals.LineDiscountTotal), Money(quote.Totals.OrderDiscount), Money(quote.Totals.DeliveryCharge), Money(quote.Totals.TaxTotal),
                Money(quote.Totals.GrandTotal)),
            quote.CanPlace && quote.ExcludedLines.Count == 0 && inputs.Lines.Count == query.Lines.Count, quote.BlockingReasons, currency);

        return new QueryResponse<CounterQuoteViewModel> { Result = view };
    }
}

/// <summary>HO-ORD-001 §8.11 step 4 (US-ORD-16 AC1) -- the receipt. Everything is read back from the finished sale and from Billing's invoice (the seller's legal identifiers
/// are frozen there, NFR-LEG-3); nothing is recomputed. A reprint is the same read.</summary>
public sealed class GetReceiptQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetReceiptQuery, ReceiptViewModel?>
{
    public async Task<QueryResponse<ReceiptViewModel?>> HandleAsync(GetReceiptQuery query)
    {
        var order = await db.Orders.AsNoTracking().Where(o => o.Id == query.OrderId && o.BranchId == query.BranchId && o.Channel == OrderChannel.Counter && o.Status == OrderStatus.Delivered)
            .Select(o => new
            {
                o.Id, o.OrderNumber, o.CurrencyCode, o.InvoiceId, o.InvoiceNumber, o.CustomerId, o.ChangeGiven, o.DeliveredOn, o.DeliveryRecordedBy, o.Fulfilment.RecipientName,
                Subtotal = o.Totals.Subtotal, LineDiscountTotal = o.Totals.LineDiscountTotal, OrderDiscount = o.Totals.OrderDiscount, DeliveryCharge = o.Totals.DeliveryCharge,
                TaxTotal = o.Totals.TaxTotal, GrandTotal = o.Totals.GrandTotal,
                Lines = o.Lines.OrderBy(l => l.LineNumber).Select(l => new { l.ProductName, l.VariantLabel, l.Quantity, l.UnitPrice, l.LineDiscount, l.OrderDiscountShare, l.TaxRatePercent, l.TaxAmount, l.LineTotal }).ToList(),
                Payments = o.Payments.OrderBy(p => p.RecordedOn).Select(p => new { p.Method, p.Amount }).ToList(),
            })
            .FirstOrDefaultAsync();

        if (order is null || order.DeliveredOn is null) return new QueryResponse<ReceiptViewModel?> { Result = null };

        var branch = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == query.BranchId).Select(b => new { b.Name, b.AddressLine, b.PhoneNumber, b.TimeZoneId }).FirstAsync();
        var seller = order.InvoiceId is { } invoiceId
            ? await db.Invoices.AsNoTracking().Where(i => i.Id == invoiceId).Select(i => new { i.Seller!.LegalName, i.Seller.TaxRegistrationNumber, i.Seller.AddressText, i.Seller.InvoiceFooter }).FirstOrDefaultAsync()
            : null;
        var cashier = order.DeliveryRecordedBy is { } staffId ? await db.StaffAccounts.AsNoTracking().Where(s => s.Id == staffId).Select(s => s.FullName).FirstOrDefaultAsync() : null;

        var currency = order.CurrencyCode;
        MoneyViewModel Money(decimal amount) => OrderMoney.Of(amount, currency);
        string Plain(decimal amount) => amount.ToString("N2", CultureInfo.InvariantCulture);

        var soldOn = order.DeliveredOn.Value;
        var local = TimeZoneInfo.ConvertTime(soldOn, TimeZoneInfo.FindSystemTimeZoneById(branch.TimeZoneId));
        var localText = local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        var tenderViews = order.Payments.Select(p => new ReceiptTenderViewModel(p.Method, Money(p.Amount))).ToList();
        var cashierName = cashier ?? "Staff";

        var textLines = order.Lines.Select(l =>
        {
            var description = string.IsNullOrWhiteSpace(l.VariantLabel) ? l.ProductName : $"{l.ProductName} - {l.VariantLabel}";
            var discount = l.LineDiscount + l.OrderDiscountShare;
            return new ReceiptTextLine(
                description, l.Quantity, Plain(l.UnitPrice), Plain(l.LineTotal), discount > 0m ? Plain(discount) : null,
                $"Tax {l.TaxRatePercent.ToString("0.##", CultureInfo.InvariantCulture)}%: {Plain(l.TaxAmount)}");
        }).ToList();

        var totals = new List<(string, string)> { ("Subtotal", Plain(order.Subtotal)) };
        if (order.LineDiscountTotal + order.OrderDiscount > 0m) totals.Add(("Discount", "-" + Plain(order.LineDiscountTotal + order.OrderDiscount)));
        totals.Add(("Tax", Plain(order.TaxTotal)));
        totals.Add(($"TOTAL {currency}", Plain(order.GrandTotal)));

        var legal = new List<string>();
        if (seller is not null)
        {
            if (!string.IsNullOrWhiteSpace(seller.LegalName)) legal.Add(seller.LegalName);
            if (!string.IsNullOrWhiteSpace(seller.TaxRegistrationNumber)) legal.Add("Tax reg: " + seller.TaxRegistrationNumber);
        }

        var text = ReceiptTextRenderer.Render(new ReceiptText(
            branch.Name, string.IsNullOrWhiteSpace(branch.AddressLine) ? [] : [branch.AddressLine], legal, order.InvoiceNumber ?? string.Empty, order.OrderNumber, localText, cashierName,
            currency, textLines, totals, order.Payments.Select(p => (p.Method, Plain(p.Amount))).ToList(), order.ChangeGiven > 0m ? Plain(order.ChangeGiven) : null,
            string.IsNullOrWhiteSpace(seller?.InvoiceFooter) ? "Thank you!" : seller!.InvoiceFooter!));

        return new QueryResponse<ReceiptViewModel?>
        {
            Result = new ReceiptViewModel(
                order.Id, order.OrderNumber, order.InvoiceNumber ?? string.Empty, branch.Name, branch.AddressLine, branch.PhoneNumber, seller?.LegalName, seller?.TaxRegistrationNumber,
                soldOn, localText, branch.TimeZoneId, cashierName, order.CustomerId is null ? null : order.RecipientName,
                order.Lines.Select(l => new ReceiptLineViewModel(
                    string.IsNullOrWhiteSpace(l.VariantLabel) ? l.ProductName : $"{l.ProductName} - {l.VariantLabel}", l.Quantity, Money(l.UnitPrice), Money(l.LineDiscount + l.OrderDiscountShare),
                    l.TaxRatePercent, Money(l.TaxAmount), Money(l.LineTotal))).ToList(),
                new QuoteTotalsViewModel(
                    Money(order.Subtotal), Money(order.LineDiscountTotal), Money(order.OrderDiscount), Money(order.DeliveryCharge), Money(order.TaxTotal), Money(order.GrandTotal)),
                tenderViews, Money(order.ChangeGiven), text),
        };
    }
}
