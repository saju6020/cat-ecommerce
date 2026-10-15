using System.Net;
using System.Net.Http.Json;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-ORD-001 ADR-215 (accepted by the owner 2026-10-09) -- the numbering race the ARCHITECTURE gate asked to see under real concurrency: invoices issued INSIDE Billing's published
/// transaction scope (Ordering's counter sales) and invoices issued the ordinary way (staff, no explicit transaction) at the same branch, at the same instant, from a cold series.
/// Billing's <c>DocumentNumbering</c> reads the series row <c>FOR UPDATE</c> when a transaction is open, so nobody collides: every number is distinct and the sequence has no gap.
/// </summary>
public sealed class OrderingBillingNumberingEndpointTests(OrderingApiFactory factory) : OrderingShopTestBase(factory)
{
    [Fact]
    public async Task ScopedCounterSales_AndStaffIssuedInvoices_AtOneBranch_GetDistinctContiguousNumbers()
    {
        const int sales = 8;
        const int staffInvoices = 6;

        var shop = await SetUpShopAsync(fillCart: false);
        var food = await CreateSkuAsync(shop.Context, price: 100m, stock: 100m, taxClassId: shop.Context.ZeroTaxClassId, name: "Numbering food " + Guid.NewGuid().ToString("N")[..6]);
        var (cashier, _) = await StaffClientAsync(shop.Context, StaffRoles.Reception);

        // charges for the staff-issued invoices are recorded first (the race is about the issuing)
        var chargeIds = new List<Guid>();
        for (var i = 0; i < staffInvoices; i++) chargeIds.Add(await RecordStaffChargeAsync(shop.CustomerId, shop.Context));

        Task<HttpResponseMessage> Sale(int n) => StaffPostAsync(cashier, "/api/staff/pos/sales", new
        {
            customerName = "Numbering Buyer " + n, customerMobile = NewMobile(),
            lines = new[] { new { skuId = food.SkuId, quantity = 1 } },
            tenders = new object[] { new { method = "Cash", amount = 100m } },
        });

        Task<HttpResponseMessage> Staff(Guid chargeId) => shop.Context.Admin.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/staff/billing/invoices/issue-from-charges")
        {
            Content = JsonContent.Create(new { customerId = shop.CustomerId, selector = new { chargeIds = new[] { chargeId } } }, options: Json),
            Headers = { { "Idempotency-Key", Guid.NewGuid().ToString() } },
        });

        var calls = Enumerable.Range(0, sales).Select(Sale).Concat(chargeIds.Select(Staff)).ToArray();
        var responses = await Task.WhenAll(calls);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));

        var sequences = await WithDbAsync(db => db.Invoices.AsNoTracking().Where(i => i.BranchId == shop.Context.BranchId && i.SequenceNumber != null)
            .OrderBy(i => i.SequenceNumber).Select(i => i.SequenceNumber!.Value).ToListAsync());
        Assert.Equal(sales + staffInvoices, sequences.Count);
        Assert.Equal(Enumerable.Range(1, sales + staffInvoices).Select(n => (long)n), sequences);   // distinct, no gap, no reuse
    }

    private async Task<Guid> RecordStaffChargeAsync(Guid customerId, OrderingContext context)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var recorder = scope.ServiceProvider.GetRequiredService<IBillableChargeRecorder>();
        var id = await recorder.RecordAsync(new ChargeRequestDto(
            customerId, context.BranchId, null, BusinessLine.Shop, ItemKind.Product, ChargeSourceKind.Order, Guid.NewGuid(), "main", null, null, "Staff-issued item", 1m,
            new Money(100m, "BDT"), Money.Zero("BDT"), context.ZeroTaxClassId, DateOnly.FromDateTime(DateTime.UtcNow)));
        await scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>().SaveChangesAsync();
        return id;
    }
}
