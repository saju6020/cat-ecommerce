using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Read.QueryHandlers.Billing;

/// <summary>HO-BIL-001 ADR-192 -- the customer a customer-facing read is for: the caller <b>and every customer merged
/// into them</b>. Immutable financial records keep the original customer id, so reads fold the merged ones in.</summary>
internal static class CustomerSet
{
    public static IQueryable<Guid> Of(CatEcommerceDbContext db, Guid customerId) =>
        db.Set<CustomerAccountAggregate>().AsNoTracking()
            .Where(c => c.Id == customerId || c.MergedIntoCustomerId == customerId)
            .Select(c => c.Id);
}

/// <summary>The states a customer (and the portal) may see: issued, part-paid, paid and credited -- never a draft or a
/// discarded one (§12.3).</summary>
internal static class VisibleToCustomer
{
    public static IQueryable<InvoiceAggregate> Invoices(IQueryable<InvoiceAggregate> invoices) =>
        invoices.Where(i => i.Status != InvoiceStatus.Draft && i.Status != InvoiceStatus.Discarded);
}

/// <summary>
/// "What would this look like at <i>today's</i> rates?" -- used by the preview and by a draft's detail, which both show
/// an <b>estimate</b> (§8.6: on drafts the totals are an estimate until issue freezes them). Pure arithmetic through
/// <see cref="InvoiceCalculator"/>; a line whose class has no rate today is listed as a problem and costed at 0 %.
/// </summary>
internal static class InvoiceEstimator
{
    public sealed record Input(
        Guid Key, string BusinessLine, string ItemKind, string Description, decimal Quantity, decimal UnitPrice, decimal LineDiscount, Guid TaxClassId,
        bool QualifiesForLoyalty);

    public sealed record LineEstimate(Input Input, CalcLineResult Result, string? TaxClassCode, decimal? RatePercent);

    public sealed record Estimate(IReadOnlyList<LineEstimate> Lines, CalcTotals Totals, IReadOnlyList<string> Problems);

    public static async Task<Estimate> EstimateAsync(
        IBillingPricingInputs pricing, string currencyCode, IReadOnlyList<Input> inputs, decimal loyaltyValue, DateTimeOffset now)
    {
        var rates = await pricing.GetTaxRatesAsync(inputs.Select(i => i.TaxClassId).Distinct().ToList(), now);
        var problems = new List<string>();

        var calcInputs = new List<CalcLineInput>();
        foreach (var input in inputs)
        {
            decimal rate = 0m;
            if (!rates.TryGetValue(input.TaxClassId, out var info)) problems.Add(BusinessViolationCodes.TaxClassNotFound);
            else if (info.RatePercent is null) problems.Add(BusinessViolationCodes.TaxRateNotConfigured + ":" + info.Code);
            else rate = info.RatePercent.Value;

            calcInputs.Add(new CalcLineInput(input.Key, currencyCode, input.Quantity, input.UnitPrice, input.LineDiscount, rate, input.QualifiesForLoyalty));
        }

        try
        {
            var totals = InvoiceCalculator.Calculate(calcInputs, loyaltyValue);
            var lines = inputs.Select(input =>
            {
                rates.TryGetValue(input.TaxClassId, out var info);
                return new LineEstimate(input, totals.Lines.Single(l => l.Key == input.Key), info?.Code, info?.RatePercent);
            }).ToList();

            return new Estimate(lines, totals, problems.Distinct().ToList());
        }
        catch (DomainRuleViolationException ex)
        {
            problems.Add(ex.ErrorCode);
            var totals = InvoiceCalculator.Calculate(calcInputs);
            var lines = inputs.Select(input => new LineEstimate(input, totals.Lines.Single(l => l.Key == input.Key), null, null)).ToList();
            return new Estimate(lines, totals, problems.Distinct().ToList());
        }
    }

    public static InvoiceTotalsViewModel TotalsOf(CalcTotals totals, string currencyCode) => new(
        BillingMoney.Of(totals.NetTotal, currencyCode), BillingMoney.Of(totals.DiscountTotal, currencyCode),
        BillingMoney.Of(totals.TaxTotal, currencyCode), BillingMoney.Of(totals.Total, currencyCode));
}

/// <summary>Maps an <see cref="InvoiceHeader"/> and its <see cref="InvoiceLineRow"/>s (both selected in SQL) to the one detail shape both the staff
/// and the customer endpoints return.</summary>
internal static class InvoiceViewMapper
{
    public static MoneyViewModel Money(decimal amount, string currencyCode) => BillingMoney.Of(amount, currencyCode);

    public static InvoiceLineViewModel ToView(this InvoiceLineRow l, string currency) => new(
        l.Id, l.ChargeId, l.BusinessLine.DisplayName, l.ItemKind.DisplayName, l.SourceKind.DisplayName, l.SourceId, l.CatId, l.Description,
        BillingMoney.Quantity(l.Quantity), Money(l.UnitPrice, currency), Money(l.LineDiscount, currency), Money(l.InvoiceDiscountShare, currency),
        l.TaxClassId, l.TaxClassCode, l.TaxRatePercent.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
        Money(l.NetAmount, currency), Money(l.TaxAmount, currency), Money(l.GrossAmount, currency), l.SortOrder);

    public static IReadOnlyList<BillingBankAccountViewModel> BankAccounts(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];

        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateArray().Select(a => new BillingBankAccountViewModel(
            a.GetProperty("bankName").GetString() ?? "", a.GetProperty("accountName").GetString() ?? "", a.GetProperty("accountNumber").GetString() ?? "",
            a.TryGetProperty("branchName", out var branch) ? branch.GetString() : null,
            a.TryGetProperty("routingNumber", out var routing) ? routing.GetString() : null)).ToList();
    }

    public static SellerViewModel? SellerOf(InvoiceHeader i) => i.Seller is null ? null : new SellerViewModel(
        i.Seller.LegalName, i.Seller.TaxRegistrationNumber, i.Seller.AddressText, i.Seller.Phone, i.Seller.Email, i.Seller.InvoiceFooter,
        BankAccounts(i.Seller.BankAccountsJson));

    public static BillToViewModel? BillToOf(InvoiceHeader i) =>
        i.BillTo is null ? null : new BillToViewModel(i.BillTo.FullName, i.BillTo.Mobile, i.BillTo.Email, i.BillTo.AddressText);
}
