using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-IMP-001 §16 phase B -- I-B1 ... I-B3, I-B5, I-B6 (I-B4, the partial receipt, needs Phase D's receipts and is in
/// <c>InventoryGoodsReceiptEndpointTests</c>).</summary>
public sealed class InventoryProcurementEndpointTests(InventoryApiFactory factory) : InventoryIntegrationTestBase(factory)
{
    private static object NewSupplier(string code, string country = "DE", string currency = "EUR") => new
    {
        code, name = "Deutsche Feed GmbH " + code, countryCode = country, contactName = "Anna Schmidt", phone = "+49 30 1234567", email = "anna@example.test",
        address = "Berlin", currencyCode = currency, leadTimeDays = 21, paymentTerms = "30% advance, 70% against B/L", paymentTermDays = 30, notes = "Preferred"
    };

    private static async Task<Guid> CreateSupplierAsync(HttpClient client, string? code = null, string currency = "EUR")
    {
        var response = await client.PostAsJsonAsync("/api/staff/procurement/suppliers", NewSupplier(code ?? NewCode("SUP"), currency: currency), Json);
        return (await JsonOkAsync(response)).GetProperty("supplierId").GetGuid();
    }

    private static object OrderBody(Guid supplierId, Guid locationId, params (Guid SkuId, decimal Quantity, decimal Cost)[] lines) => new
    {
        supplierId, deliverToLocationId = locationId, currencyCode = "USD", expectedDeliveryOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7), notes = "Rush",
        lines = lines.Select(l => new { skuId = l.SkuId, orderedQuantity = l.Quantity, unitCostAmount = l.Cost }).ToArray()
    };

    // ── I-B1 / I-B2 ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_B1_ASupplierRecordsNameCountryContactCurrencyLeadTimeAndPaymentTerms()
    {
        var branch = await SeedBranchAsync(withLocations: false);
        var code = NewCode("SUP");

        var supplierId = await CreateSupplierAsync(branch.Admin.Client, code);

        var detail = await JsonOkAsync(await branch.Admin.Client.GetAsync($"/api/staff/procurement/suppliers/{supplierId}"));
        Assert.Equal(code, detail.GetProperty("code").GetString());
        Assert.Equal("DE", detail.GetProperty("countryCode").GetString());
        Assert.Equal("Anna Schmidt", detail.GetProperty("contactName").GetString());
        Assert.Equal("EUR", detail.GetProperty("currencyCode").GetString());
        Assert.Equal(21, detail.GetProperty("leadTimeDays").GetInt32());
        Assert.Equal("30% advance, 70% against B/L", detail.GetProperty("paymentTerms").GetString());
        Assert.True(detail.GetProperty("isActive").GetBoolean());

        var list = await JsonOkAsync(await branch.Admin.Client.GetAsync($"/api/staff/procurement/suppliers?search={code}&countryCode=DE&isActive=true"));
        Assert.Equal(1, list.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task I_B2_ADuplicateSupplierCodeInAnotherCaseIsRefused()
    {
        var branch = await SeedBranchAsync(withLocations: false);
        var code = NewCode("SUP");
        await CreateSupplierAsync(branch.Admin.Client, code);

        var duplicate = await branch.Admin.Client.PostAsJsonAsync("/api/staff/procurement/suppliers", NewSupplier(code.ToLowerInvariant()), Json);

        await AssertErrorAsync(duplicate, BusinessViolationCodes.SupplierCodeAlreadyExists);
    }

    [Fact]
    public async Task I_B1_ASupplierIsDeactivatedNeverDeleted_AndAnInvalidCountryIsRefused()
    {
        var branch = await SeedBranchAsync(withLocations: false);
        var supplierId = await CreateSupplierAsync(branch.Admin.Client);

        var deactivate = await branch.Admin.Client.PutAsJsonAsync($"/api/staff/procurement/suppliers/{supplierId}/activation", new { isActive = false }, Json);
        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);
        var detail = await JsonOkAsync(await branch.Admin.Client.GetAsync($"/api/staff/procurement/suppliers/{supplierId}"));
        Assert.False(detail.GetProperty("isActive").GetBoolean());

        var bad = await branch.Admin.Client.PostAsJsonAsync("/api/staff/procurement/suppliers", NewSupplier(NewCode("SUP"), country: "XX"), Json);
        await AssertErrorAsync(bad, BusinessViolationCodes.InvalidCountryCode);
    }

    // ── I-B3 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_B3_ACreatedPurchaseOrderIsIssuedWithAPONumberAndItsLines()
    {
        var branch = await SeedBranchAsync();
        var supplierId = await CreateSupplierAsync(branch.Admin.Client);
        var skuA = await CreateSkuAsync(branch.Admin.Client, food: true);
        var skuB = await CreateSkuAsync(branch.Admin.Client);

        var create = await branch.Admin.Client.PostAsJsonAsync("/api/staff/procurement/purchase-orders",
            OrderBody(supplierId, branch.WarehouseId, (skuA.SkuId, 100m, 12.3456m), (skuB.SkuId, 20m, 4m)), Json);
        var created = await JsonOkAsync(create);
        var orderId = created.GetProperty("purchaseOrderId").GetGuid();
        Assert.Matches(@"^PO-\d{4}-\d{6}$", created.GetProperty("number").GetString()!);

        var draft = await JsonOkAsync(await branch.Admin.Client.GetAsync($"/api/staff/procurement/purchase-orders/{orderId}"));
        Assert.Equal("Draft", draft.GetProperty("status").GetString());

        var issue = await branch.Admin.Client.PostAsync($"/api/staff/procurement/purchase-orders/{orderId}/issue", null);
        Assert.Equal(HttpStatusCode.NoContent, issue.StatusCode);

        var detail = await JsonOkAsync(await branch.Admin.Client.GetAsync($"/api/staff/procurement/purchase-orders/{orderId}"));
        Assert.Equal("Issued", detail.GetProperty("status").GetString());
        Assert.Equal("USD", detail.GetProperty("currencyCode").GetString());
        Assert.Equal("NotReceived", detail.GetProperty("receiptState").GetString());

        var lines = detail.GetProperty("lines").EnumerateArray().ToList();
        Assert.Equal(2, lines.Count);
        var lineA = lines.Single(l => l.GetProperty("skuId").GetGuid() == skuA.SkuId);
        Assert.Equal("100", lineA.GetProperty("orderedQuantity").GetString());
        Assert.Equal("100", lineA.GetProperty("outstandingQuantity").GetString());
        Assert.Equal("12.3456", lineA.GetProperty("unitCost").GetProperty("amount").GetString());
        Assert.Equal("1234.56", lineA.GetProperty("lineTotal").GetProperty("amount").GetString());
        Assert.Equal("1314.56", detail.GetProperty("totalAmount").GetProperty("amount").GetString());
        Assert.Equal(skuA.SkuCode, lineA.GetProperty("skuCode").GetString());
    }

    [Fact]
    public async Task I_B3_ADraftCanBeReplaced_ADuplicateSkuIsRefused_AndAnExpectedDeliveryInThePastCannotBeIssued()
    {
        var branch = await SeedBranchAsync();
        var supplierId = await CreateSupplierAsync(branch.Admin.Client);
        var sku = await CreateSkuAsync(branch.Admin.Client);

        var duplicate = await branch.Admin.Client.PostAsJsonAsync("/api/staff/procurement/purchase-orders",
            OrderBody(supplierId, branch.WarehouseId, (sku.SkuId, 5m, 1m), (sku.SkuId, 6m, 1m)), Json);
        await AssertErrorAsync(duplicate, BusinessViolationCodes.DuplicatePurchaseOrderSku);

        var created = await JsonOkAsync(await branch.Admin.Client.PostAsJsonAsync("/api/staff/procurement/purchase-orders",
            OrderBody(supplierId, branch.WarehouseId, (sku.SkuId, 5m, 1m)), Json));
        var orderId = created.GetProperty("purchaseOrderId").GetGuid();

        var replace = await branch.Admin.Client.PutAsJsonAsync($"/api/staff/procurement/purchase-orders/{orderId}", new
        {
            supplierId, deliverToLocationId = branch.WarehouseId, currencyCode = "USD", expectedDeliveryOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3),
            lines = new[] { new { skuId = sku.SkuId, orderedQuantity = 9m, unitCostAmount = 2m } }
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, replace.StatusCode);

        var issue = await branch.Admin.Client.PostAsync($"/api/staff/procurement/purchase-orders/{orderId}/issue", null);
        await AssertErrorAsync(issue, BusinessViolationCodes.ExpectedDeliveryInPast);

        var detail = await JsonOkAsync(await branch.Admin.Client.GetAsync($"/api/staff/procurement/purchase-orders/{orderId}"));
        Assert.Equal("9", detail.GetProperty("lines")[0].GetProperty("orderedQuantity").GetString());
        Assert.Equal("Draft", detail.GetProperty("status").GetString());
    }

    [Fact]
    public async Task I_B3_ADeliverToLocationThatCannotReceiveOrAnInactiveSupplierIsRefused()
    {
        var branch = await SeedBranchAsync();
        var supplierId = await CreateSupplierAsync(branch.Admin.Client);
        var sku = await CreateSkuAsync(branch.Admin.Client);

        var elsewhere = await SeedBranchAsync();
        var foreign = await branch.Admin.Client.PostAsJsonAsync("/api/staff/procurement/purchase-orders",
            OrderBody(supplierId, elsewhere.WarehouseId, (sku.SkuId, 5m, 1m)), Json);
        await AssertErrorAsync(foreign, BusinessViolationCodes.StockLocationNotFound);

        await branch.Admin.Client.PutAsJsonAsync($"/api/staff/procurement/suppliers/{supplierId}/activation", new { isActive = false }, Json);
        var inactive = await branch.Admin.Client.PostAsJsonAsync("/api/staff/procurement/purchase-orders",
            OrderBody(supplierId, branch.WarehouseId, (sku.SkuId, 5m, 1m)), Json);
        await AssertErrorAsync(inactive, BusinessViolationCodes.SupplierInactive);
    }

    // ── I-B5 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_B5_AnAccountantReadsPurchaseOrdersButCannotCreateThem()
    {
        var branch = await SeedBranchAsync();
        var supplierId = await CreateSupplierAsync(branch.Admin.Client);
        var sku = await CreateSkuAsync(branch.Admin.Client);
        var accountant = await StaffAsync(branch, StaffRoles.Accountant);

        var post = await accountant.Client.PostAsJsonAsync("/api/staff/procurement/purchase-orders",
            OrderBody(supplierId, branch.WarehouseId, (sku.SkuId, 5m, 1m)), Json);
        Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);

        var created = await JsonOkAsync(await branch.Admin.Client.PostAsJsonAsync("/api/staff/procurement/purchase-orders",
            OrderBody(supplierId, branch.WarehouseId, (sku.SkuId, 5m, 1m)), Json));
        var orderId = created.GetProperty("purchaseOrderId").GetGuid();

        var get = await accountant.Client.GetAsync($"/api/staff/procurement/purchase-orders/{orderId}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await accountant.Client.GetAsync("/api/staff/procurement/purchase-orders")).StatusCode);
    }

    [Fact]
    public async Task I_B5_APurchaseOrderOfAnotherBranchIsA404()
    {
        var branchA = await SeedBranchAsync();
        var branchB = await SeedBranchAsync();
        var supplierId = await CreateSupplierAsync(branchB.Admin.Client);
        var sku = await CreateSkuAsync(branchB.Admin.Client);
        var created = await JsonOkAsync(await branchB.Admin.Client.PostAsJsonAsync("/api/staff/procurement/purchase-orders",
            OrderBody(supplierId, branchB.WarehouseId, (sku.SkuId, 5m, 1m)), Json));
        var orderId = created.GetProperty("purchaseOrderId").GetGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await branchA.Admin.Client.GetAsync($"/api/staff/procurement/purchase-orders/{orderId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await branchA.Admin.Client.PostAsync($"/api/staff/procurement/purchase-orders/{orderId}/issue", null)).StatusCode);

        var listA = await JsonOkAsync(await branchA.Admin.Client.GetAsync("/api/staff/procurement/purchase-orders"));
        Assert.DoesNotContain(listA.GetProperty("items").EnumerateArray(), o => o.GetProperty("id").GetGuid() == orderId);
    }

    // ── I-B6 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_B6_AnIssuedOrderWithNoReceiptsCanBeCancelled_AndAnotherCanBeShortClosedWithAReason()
    {
        var branch = await SeedBranchAsync();
        var supplierId = await CreateSupplierAsync(branch.Admin.Client);
        var sku = await CreateSkuAsync(branch.Admin.Client);

        async Task<Guid> IssuedOrderAsync()
        {
            var created = await JsonOkAsync(await branch.Admin.Client.PostAsJsonAsync("/api/staff/procurement/purchase-orders",
                OrderBody(supplierId, branch.WarehouseId, (sku.SkuId, 50m, 3m)), Json));
            var id = created.GetProperty("purchaseOrderId").GetGuid();
            Assert.Equal(HttpStatusCode.NoContent, (await branch.Admin.Client.PostAsync($"/api/staff/procurement/purchase-orders/{id}/issue", null)).StatusCode);
            return id;
        }

        var toCancel = await IssuedOrderAsync();
        var cancel = await branch.Admin.Client.PostAsJsonAsync($"/api/staff/procurement/purchase-orders/{toCancel}/cancel", new { reason = "Supplier withdrew the offer." }, Json);
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        Assert.Equal("Cancelled", (await JsonOkAsync(await branch.Admin.Client.GetAsync($"/api/staff/procurement/purchase-orders/{toCancel}"))).GetProperty("status").GetString());

        var toClose = await IssuedOrderAsync();
        var noReason = await branch.Admin.Client.PostAsJsonAsync($"/api/staff/procurement/purchase-orders/{toClose}/close", new { reason = " " }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);

        var close = await branch.Admin.Client.PostAsJsonAsync($"/api/staff/procurement/purchase-orders/{toClose}/close", new { reason = "Supplier short-shipped; balance cancelled." }, Json);
        Assert.Equal(HttpStatusCode.NoContent, close.StatusCode);
        var closed = await JsonOkAsync(await branch.Admin.Client.GetAsync($"/api/staff/procurement/purchase-orders/{toClose}"));
        Assert.Equal("Closed", closed.GetProperty("status").GetString());
        Assert.Equal("50", closed.GetProperty("lines")[0].GetProperty("outstandingQuantity").GetString());

        var again = await branch.Admin.Client.PostAsJsonAsync($"/api/staff/procurement/purchase-orders/{toClose}/cancel", new { reason = "Too late." }, Json);
        await AssertErrorAsync(again, BusinessViolationCodes.InvalidPurchaseOrderTransition);
    }
}
