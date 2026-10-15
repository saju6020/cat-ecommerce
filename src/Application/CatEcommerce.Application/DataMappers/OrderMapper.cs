using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Application.DataMappers;

/// <summary>HO-ORD-001 §11.3 -- the priced quote to the order aggregate's DTOs. Amounts leave minor units for the aggregate's <c>decimal(12,2)</c> exactly
/// (ADR-198); the aggregate re-sums them and refuses a mismatch.</summary>
public static class OrderMapper
{
    public static OrderLineDto ToDomainDto(this QuoteLine line, int lineNumber) =>
        new(Guid.NewGuid(), lineNumber, line.SkuId, line.ProductId, line.ProductName, line.VariantLabel, line.Barcode, line.ReturnClass, line.IsBundle, line.CatId,
            line.Quantity, line.UnitPrice.ToAmount(), line.Subtotal.ToAmount(), line.LineDiscount.ToAmount(), line.OrderDiscountShare.ToAmount(), line.TaxClassId,
            line.TaxRatePercent, line.TaxAmount.ToAmount(), line.LineTotal.ToAmount(),
            line.BundleComponents.Select(c => new OrderLineComponent(c.ComponentSkuId, c.Quantity)).ToList());

    public static IReadOnlyList<OrderLineDto> ToDomainDtos(this IReadOnlyList<QuoteLine> lines) =>
        lines.Select((line, index) => line.ToDomainDto(index + 1)).ToList();

    /// <summary>The tax on the delivery charge (FU1 D3): the quote's tax total less the lines' own tax. Zero while the delivery tax is 0 %.</summary>
    public static decimal DeliveryTaxOf(this CheckoutQuote quote) =>
        (quote.Totals.TaxTotal - new MinorUnits(quote.Lines.Sum(l => l.TaxAmount.Value))).ToAmount();

    public static OrderTotals ToOrderTotals(this QuoteTotals totals) =>
        new(totals.Subtotal.ToAmount(), totals.LineDiscountTotal.ToAmount(), totals.OrderDiscount.ToAmount(), totals.DeliveryCharge.ToAmount(), totals.TaxTotal.ToAmount(),
            totals.GrandTotal.ToAmount());
}
