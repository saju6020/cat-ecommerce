using System.Net;
using System.Net.Http.Json;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-ORD-001-FU1 D3 (owner decision 2026-10-10) -- the delivery charge's tax: configured, default 0 %, changed by an Administrator through the existing tax configuration
/// (the <c>DELIVERY</c> tax class, effective-dated rates). <c>DeliveryTaxClassNotConfigured</c> no longer exists.
/// <para>This class has the only test that changes a <b>global</b> rate (the shared database holds one <c>DELIVERY</c> class). The new rate is created through the Admin API
/// effective a few minutes ahead of this factory's own clock and the clock is then advanced, so the other test classes (their clocks are real time) never see it, and the rate
/// was deleted again in a <c>finally</c> until HO-XCT-001 made <c>admin.tax_rate</c> undeletable (see <c>SkipReason</c>).</para>
/// </summary>
public sealed class OrderingFollowUp1DeliveryTaxEndpointTests(OrderingApiFactory factory) : OrderingShopTestBase(factory)
{
    private static readonly Guid SeededRateId = new("d31e0a7c-5b1f-4c6e-9a42-0d0e11f00002");

    // HO-XCT-001 ADR-299 (Phase A): the two tests that add a 15 % rate to the one global DELIVERY class used to delete that rate again in a finally block; admin.tax_rate is a Financial,
    // Forbidden table (price in force, D-9) and the retention trigger now refuses the delete. Left in place the rate would apply to every other class (real-time clocks), so the two tests
    // are skipped until HO-ORD-001 redesigns them (e.g. a scratch database). Owner: Ordering. Recorded in docs/backlog/XCT.md.
    private const string SkipReason = "Needs an Ordering redesign: it adds a global 15 % delivery tax rate and used to delete it afterwards; admin.tax_rate rows are retained (HO-XCT-001 ADR-299). See docs/backlog/XCT.md.";

    private Task<Guid> DeliveryClassIdAsync() =>
        WithDbAsync(db => db.Set<TaxClassAggregate>().AsNoTracking().Where(c => c.Code == DeliveryTaxFacts.ClassCode).Select(c => c.Id).SingleAsync());

    private async Task RefillCartAsync(Shop shop)
    {
        await ExpectAsync(HttpStatusCode.Created, await shop.Customer.PostAsJsonAsync("/api/cart/lines", new { skuId = shop.Food.SkuId, quantity = 2 }, Json));
        await ExpectAsync(HttpStatusCode.Created, await shop.Customer.PostAsJsonAsync("/api/cart/lines", new { skuId = shop.Toy.SkuId, quantity = 1 }, Json));
    }

    [Fact(Skip = SkipReason)]
    public async Task D3_TheDeliveryTax_StartsAtZeroPercent_AnAdminCanChangeIt_AndTheNextOrderAppliesIt()
    {
        var shop = await SetUpShopAsync();
        var deliveryClassId = await DeliveryClassIdAsync();

        try
        {
            // 1. the seeded default: no configuration needed, delivery orders are accepted and the delivery charge carries no tax
            var first = await LoadOrderAsync((await PlaceCodOrderAsync(shop)).GetProperty("orderId").GetGuid());
            Assert.True(first.Totals.DeliveryCharge > 0m);
            Assert.Equal(first.Lines.Sum(l => l.TaxAmount), first.Totals.TaxTotal);                                  // lines only: 0 % on delivery
            var firstInvoice = await WithDbAsync(db => db.Invoices.AsNoTracking().Where(i => i.Id == first.InvoiceId).Select(i => new { i.Total, i.TaxTotal }).SingleAsync());
            Assert.Equal(first.Totals.GrandTotal, firstInvoice.Total);
            Assert.Equal(first.Totals.TaxTotal, firstInvoice.TaxTotal);

            // 2. an Administrator sets 15 % through the existing tax-rate endpoint (effective shortly ahead; this factory's clock is moved past it)
            var effective = Now.AddMinutes(2);
            await ExpectAsync(HttpStatusCode.Created, await shop.Context.Admin.PostAsJsonAsync($"/api/admin/tax-classes/{deliveryClassId}/rates", new { ratePercent = 15m, effectiveFrom = effective }, Json));
            Factory.Clock.Advance(TimeSpan.FromMinutes(3));

            // 3. the next order applies it: the quote shows it, the order stores it, the invoice carries it, and every total agrees
            await RefillCartAsync(shop);
            var quote = await QuoteAsync(shop.Customer, QuoteUrl(shop));
            var expectedDeliveryTax = decimal.Round(first.Totals.DeliveryCharge * 0.15m, 2, MidpointRounding.AwayFromZero);
            Assert.Equal(first.Totals.TaxTotal + expectedDeliveryTax, ReadMoney(quote.GetProperty("totals").GetProperty("taxTotal")));

            var second = await LoadOrderAsync((await PlaceCodOrderAsync(shop)).GetProperty("orderId").GetGuid());
            Assert.Equal(first.Totals.DeliveryCharge, second.Totals.DeliveryCharge);                                  // the charge is unchanged
            Assert.Equal(second.Lines.Sum(l => l.TaxAmount) + expectedDeliveryTax, second.Totals.TaxTotal);
            Assert.Equal(expectedDeliveryTax, second.DeliveryTaxAmount);                                               // ORD-56: stored on the order at placement
            Assert.Equal(0m, first.DeliveryTaxAmount);
            Assert.Equal(first.Totals.GrandTotal + expectedDeliveryTax, second.Totals.GrandTotal);
            var secondInvoice = await WithDbAsync(db => db.Invoices.AsNoTracking().Where(i => i.Id == second.InvoiceId).Select(i => new { i.Total, i.TaxTotal }).SingleAsync());
            Assert.Equal(second.Totals.GrandTotal, secondInvoice.Total);                                              // the invoice (Billing) and the order agree to the cent
            Assert.Equal(second.Totals.TaxTotal, secondInvoice.TaxTotal);
        }
        finally
        {
            // The added rate can no longer be deleted afterwards (HO-XCT-001 ADR-299), which is why this test is skipped: see SkipReason.
        }
    }

    [Fact]
    public async Task D3_ATransferOrder_IsInvoicedAtTheDeliveryTaxInForceWhenItIsConfirmed_NoConfigurationErrorExists()
    {
        var shop = await SetUpShopAsync();
        await SetBankTransferAccountAsync(shop.Context.Admin);
        var orderId = (await PlaceTransferOrderAsync(shop)).GetProperty("orderId").GetGuid();
        var (reception, _) = await StaffClientAsync(shop.Context, CatEcommerce.Shared.Common.Constants.StaffRoles.Reception);
        var order = await LoadOrderAsync(orderId);

        await ExpectAsync(HttpStatusCode.OK, await ConfirmTransferAsync(reception, orderId, order.Version, order.Totals.GrandTotal));

        var confirmed = await LoadOrderAsync(orderId);
        var invoice = await WithDbAsync(db => db.Invoices.AsNoTracking().Where(i => i.Id == confirmed.InvoiceId).Select(i => new { i.Total, i.TaxTotal }).SingleAsync());
        Assert.Equal(confirmed.Totals.GrandTotal, invoice.Total);
        Assert.Equal(confirmed.Totals.TaxTotal, invoice.TaxTotal);
    }

    // ORD-56: a transfer placed at one delivery tax and confirmed after the rate changed is invoiced at the tax the customer was quoted (the order's stored amount).
    [Fact(Skip = SkipReason)]
    public async Task D3_AHeldTransfer_ConfirmedAfterARateChange_IsInvoicedAtTheDeliveryTaxItWasQuotedAt()
    {
        var shop = await SetUpShopAsync();
        await SetBankTransferAccountAsync(shop.Context.Admin);
        var deliveryClassId = await DeliveryClassIdAsync();

        try
        {
            var orderId = (await PlaceTransferOrderAsync(shop)).GetProperty("orderId").GetGuid();
            var placed = await LoadOrderAsync(orderId);
            Assert.True(placed.Totals.DeliveryCharge > 0m);
            Assert.Equal(0m, placed.DeliveryTaxAmount);

            await ExpectAsync(HttpStatusCode.Created, await shop.Context.Admin.PostAsJsonAsync($"/api/admin/tax-classes/{deliveryClassId}/rates", new { ratePercent = 15m, effectiveFrom = Now.AddMinutes(2) }, Json));
            Factory.Clock.Advance(TimeSpan.FromMinutes(3));

            var (reception, _) = await StaffClientAsync(shop.Context, CatEcommerce.Shared.Common.Constants.StaffRoles.Reception);
            await ExpectAsync(HttpStatusCode.OK, await ConfirmTransferAsync(reception, orderId, placed.Version, placed.Totals.GrandTotal));

            var confirmed = await LoadOrderAsync(orderId);
            var invoice = await WithDbAsync(db => db.Invoices.AsNoTracking().Where(i => i.Id == confirmed.InvoiceId).Select(i => new { i.Total, i.TaxTotal }).SingleAsync());
            Assert.Equal(placed.Totals.GrandTotal, invoice.Total);                                                    // not the new 15 %
            Assert.Equal(placed.Totals.TaxTotal, invoice.TaxTotal);
        }
        finally
        {
            // The added rate can no longer be deleted afterwards (HO-XCT-001 ADR-299), which is why this test is skipped: see SkipReason.
        }
    }

    // ORD-54: the DELIVERY class cannot be deactivated (the other tax classes can).
    [Fact]
    public async Task D3_TheDeliveryTaxClass_CannotBeDeactivated()
    {
        var shop = await SetUpShopAsync();
        var deliveryClassId = await DeliveryClassIdAsync();

        var response = await shop.Context.Admin.PutAsJsonAsync($"/api/admin/tax-classes/{deliveryClassId}", new { name = "Delivery", isActive = false }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("DeliveryTaxClassRequired", await response.Content.ReadAsStringAsync());
        Assert.True(await WithDbAsync(db => db.Set<TaxClassAggregate>().AsNoTracking().Where(c => c.Id == deliveryClassId).Select(c => c.IsActive).SingleAsync()));
    }

    [Fact]
    public async Task D3_TheDeliveryTaxClass_IsSeededAtZeroPercent()
    {
        var classId = await DeliveryClassIdAsync();

        var rates = await WithDbAsync(db => db.Set<TaxRateAggregate>().AsNoTracking().Where(r => r.TaxClassId == classId).Select(r => r.RatePercent).ToListAsync());

        Assert.Equal([0m], rates);
    }

    private static decimal ReadMoney(System.Text.Json.JsonElement money) => decimal.Parse(money.GetProperty("amount").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
}
