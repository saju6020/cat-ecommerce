using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-IMP-001 §16 phase C -- I-C1 ... I-C3 and I-C5 (I-C4, the receipt against an uncleared consignment, is in
/// <c>InventoryGoodsReceiptEndpointTests</c>).</summary>
public sealed class InventoryConsignmentEndpointTests(InventoryApiFactory factory) : InventoryIntegrationTestBase(factory)
{
    private sealed record Seeded(BranchContext Branch, Guid SupplierId, Guid OrderId, string OrderNumber);

    private async Task<Seeded> SeedAsync()
    {
        var branch = await SeedBranchAsync();
        var supplierId = await SeedSupplierAsync(branch.Admin.Client);
        var sku = await CreateSkuAsync(branch.Admin.Client, food: true);
        var order = await SeedIssuedOrderAsync(branch.Admin.Client, supplierId, branch.WarehouseId, "USD", (sku.SkuId, 100m, 2.5m));
        return new Seeded(branch, supplierId, order.OrderId, order.Number);
    }

    private static object PlanBody(Guid orderId, bool withClearanceNumbers = true) => new
    {
        originCountryCode = "DE", shipmentReference = "BL-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), purchaseOrderIds = new[] { orderId },
        hsCodes = new[] { new { code = "2309.10", description = (string?)"Dog or cat food, retail" }, new { code = "230990", description = (string?)null } },
        importPermitNumber = withClearanceNumbers ? "IP-2026-001" : null, healthCertificateNumber = withClearanceNumbers ? "VC-778" : null,
        customsDeclarationNumber = withClearanceNumbers ? "CD-9001" : null, expectedArrivalOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(20)
    };

    private static async Task<(Guid Id, string Reference)> PlanAsync(HttpClient client, object body)
    {
        var created = await JsonOkAsync(await client.PostAsJsonAsync("/api/staff/procurement/consignments", body, Json));
        return (created.GetProperty("consignmentId").GetGuid(), created.GetProperty("reference").GetString()!);
    }

    private static async Task<JsonElement> DetailAsync(HttpClient client, Guid id) =>
        await JsonOkAsync(await client.GetAsync($"/api/staff/procurement/consignments/{id}"));

    private static object Doc(string kind, string number) => new
    {
        kind, documentNumber = number, issuedOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), fileName = $"{kind}.pdf",
        storageKey = $"consignments/{Guid.NewGuid():N}.pdf", contentType = "application/pdf", sizeInBytes = 20_480
    };

    // ── I-C1 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_C1_APlannedConsignmentRecordsEveryFR_IMP_3FieldWithItsCostsAndRates()
    {
        var s = await SeedAsync();
        var (id, reference) = await PlanAsync(s.Branch.Admin.Client, PlanBody(s.OrderId));
        Assert.Matches(@"^CN-\d{4}-\d{6}$", reference);

        var version = (await DetailAsync(s.Branch.Admin.Client, id)).GetProperty("version").GetInt32();
        var costs = await s.Branch.Admin.Client.PutAsJsonAsync($"/api/staff/procurement/consignments/{id}/costs", new
        {
            expectedVersion = version,
            costComponents = new[]
            {
                new { kind = "Freight", amount = 1500.00m, currencyCode = "BDT", note = (string?)"Sea freight" },
                new { kind = "Duty", amount = 100.50m, currencyCode = "USD", note = (string?)null },
            },
            exchangeRates = new[] { new { currencyCode = "USD", rateToBase = 118.5m, recordedOn = DateOnly.FromDateTime(DateTime.UtcNow) } }
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, costs.StatusCode);

        var detail = await DetailAsync(s.Branch.Admin.Client, id);
        Assert.Equal("Planned", detail.GetProperty("status").GetString());
        Assert.Equal("DE", detail.GetProperty("originCountryCode").GetString());
        Assert.StartsWith("BL-", detail.GetProperty("shipmentReference").GetString());
        Assert.Equal("IP-2026-001", detail.GetProperty("importPermitNumber").GetString());
        Assert.Equal("VC-778", detail.GetProperty("healthCertificateNumber").GetString());
        Assert.Equal("CD-9001", detail.GetProperty("customsDeclarationNumber").GetString());
        Assert.Equal(2, detail.GetProperty("hsCodes").GetArrayLength());
        Assert.Equal(s.OrderNumber, detail.GetProperty("purchaseOrders")[0].GetProperty("number").GetString());

        var components = detail.GetProperty("costComponents").EnumerateArray().ToList();
        Assert.Equal(2, components.Count);
        Assert.Contains(components, c => c.GetProperty("kind").GetString() == "Duty" && c.GetProperty("amount").GetProperty("amount").GetString() == "100.50");
        var rate = Assert.Single(detail.GetProperty("exchangeRates").EnumerateArray());
        Assert.Equal("118.5", rate.GetProperty("rateToBase").GetString());

        var list = await JsonOkAsync(await s.Branch.Admin.Client.GetAsync("/api/staff/procurement/consignments?status=Planned&originCountryCode=DE"));
        Assert.Contains(list.GetProperty("items").EnumerateArray(), c => c.GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task I_C1_AStaleCostVersionIsA409_AndAnInvalidHsCodeOrAnotherBranchsOrderIsRefused()
    {
        var s = await SeedAsync();
        var (id, _) = await PlanAsync(s.Branch.Admin.Client, PlanBody(s.OrderId));

        var stale = await s.Branch.Admin.Client.PutAsJsonAsync($"/api/staff/procurement/consignments/{id}/costs",
            new { expectedVersion = 999, costComponents = Array.Empty<object>(), exchangeRates = Array.Empty<object>() }, Json);
        await AssertErrorAsync(stale, BusinessViolationCodes.ConsignmentVersionConflict, HttpStatusCode.Conflict);

        var badHs = await s.Branch.Admin.Client.PostAsJsonAsync("/api/staff/procurement/consignments", new
        {
            originCountryCode = "DE", shipmentReference = "BL-1", purchaseOrderIds = new[] { s.OrderId }, hsCodes = new[] { new { code = "ABCD", description = (string?)null } }
        }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, badHs.StatusCode);

        var other = await SeedAsync();
        var foreign = await s.Branch.Admin.Client.PostAsJsonAsync("/api/staff/procurement/consignments", PlanBody(other.OrderId), Json);
        await AssertErrorAsync(foreign, BusinessViolationCodes.ConsignmentPurchaseOrderMismatch);

        Assert.Equal(HttpStatusCode.NotFound, (await other.Branch.Admin.Client.GetAsync($"/api/staff/procurement/consignments/{id}")).StatusCode);
    }

    [Fact]
    public async Task I_C1_ADraftPurchaseOrderCannotBeGroupedIntoAConsignment()
    {
        var s = await SeedAsync();
        var sku = await CreateSkuAsync(s.Branch.Admin.Client);
        var draft = await JsonOkAsync(await s.Branch.Admin.Client.PostAsJsonAsync("/api/staff/procurement/purchase-orders", new
        {
            supplierId = s.SupplierId, deliverToLocationId = s.Branch.WarehouseId, expectedDeliveryOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3),
            lines = new[] { new { skuId = sku.SkuId, orderedQuantity = 1m, unitCostAmount = 1m } }
        }, Json));

        var response = await s.Branch.Admin.Client.PostAsJsonAsync("/api/staff/procurement/consignments", PlanBody(draft.GetProperty("purchaseOrderId").GetGuid()), Json);

        await AssertErrorAsync(response, BusinessViolationCodes.ConsignmentPurchaseOrderMismatch);
    }

    // ── I-C2 / I-C3 ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_C2_FourDocumentsAreRetrievableTogetherInTheInspectionPack()
    {
        var s = await SeedAsync();
        var (id, reference) = await PlanAsync(s.Branch.Admin.Client, PlanBody(s.OrderId));

        foreach (var (kind, number) in new[] { ("ImportPermit", "IP-2026-001"), ("HealthCertificate", "VC-778"), ("CustomsDeclaration", "CD-9001"), ("BillOfLading", "BL-1") })
        {
            var add = await s.Branch.Admin.Client.PostAsJsonAsync($"/api/staff/procurement/consignments/{id}/documents", Doc(kind, number), Json);
            Assert.Equal(HttpStatusCode.Created, add.StatusCode);
        }

        var pack = await JsonOkAsync(await s.Branch.Admin.Client.GetAsync($"/api/staff/procurement/consignments/{id}/inspection-pack"));

        Assert.Equal(reference, pack.GetProperty("reference").GetString());
        Assert.Equal("DE", pack.GetProperty("originCountryCode").GetString());
        Assert.Equal("IP-2026-001", pack.GetProperty("importPermitNumber").GetString());
        Assert.Equal("VC-778", pack.GetProperty("healthCertificateNumber").GetString());
        Assert.Equal("CD-9001", pack.GetProperty("customsDeclarationNumber").GetString());
        Assert.Equal(2, pack.GetProperty("hsCodes").GetArrayLength());
        Assert.Equal(s.OrderNumber, Assert.Single(pack.GetProperty("purchaseOrders").EnumerateArray()).GetProperty("number").GetString());
        Assert.Equal(4, pack.GetProperty("documents").GetArrayLength());
        Assert.Equal(["BillOfLading", "CustomsDeclaration", "HealthCertificate", "ImportPermit"],
            pack.GetProperty("documents").EnumerateArray().Select(d => d.GetProperty("kind").GetString()!).OrderBy(k => k));
        Assert.Equal(0, pack.GetProperty("batches").GetArrayLength());
    }

    [Fact]
    public async Task I_C3_ASupersededDocumentStaysInThePack_FlaggedSuperseded()
    {
        var s = await SeedAsync();
        var (id, _) = await PlanAsync(s.Branch.Admin.Client, PlanBody(s.OrderId));
        var added = await JsonOkAsync(await s.Branch.Admin.Client.PostAsJsonAsync($"/api/staff/procurement/consignments/{id}/documents", Doc("ImportPermit", "IP-OLD"), Json));
        var oldId = added.GetProperty("documentId").GetGuid();

        var supersede = await s.Branch.Admin.Client.PostAsJsonAsync($"/api/staff/procurement/consignments/{id}/documents/{oldId}/supersede", Doc("ImportPermit", "IP-NEW"), Json);
        Assert.Equal(HttpStatusCode.NoContent, supersede.StatusCode);

        var pack = await JsonOkAsync(await s.Branch.Admin.Client.GetAsync($"/api/staff/procurement/consignments/{id}/inspection-pack"));
        var documents = pack.GetProperty("documents").EnumerateArray().ToList();
        Assert.Equal(2, documents.Count);
        Assert.True(documents.Single(d => d.GetProperty("documentId").GetGuid() == oldId).GetProperty("isSuperseded").GetBoolean());
        Assert.False(documents.Single(d => d.GetProperty("documentId").GetGuid() != oldId).GetProperty("isSuperseded").GetBoolean());

        var again = await s.Branch.Admin.Client.PostAsJsonAsync($"/api/staff/procurement/consignments/{id}/documents/{oldId}/supersede", Doc("ImportPermit", "IP-NEWER"), Json);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }

    [Fact]
    public async Task I_C2_ADocumentOfAnUnsupportedTypeOrSizeIsRefused_AndThereIsNoUploadRoute()
    {
        var s = await SeedAsync();
        var (id, _) = await PlanAsync(s.Branch.Admin.Client, PlanBody(s.OrderId));

        var html = await s.Branch.Admin.Client.PostAsJsonAsync($"/api/staff/procurement/consignments/{id}/documents", new
        { kind = "Other", fileName = "x.html", storageKey = "k", contentType = "text/html", sizeInBytes = 10 }, Json);
        await AssertErrorAsync(html, BusinessViolationCodes.InvalidDocumentContentType);

        var big = await s.Branch.Admin.Client.PostAsJsonAsync($"/api/staff/procurement/consignments/{id}/documents", new
        { kind = "Other", fileName = "x.pdf", storageKey = "k", contentType = "application/pdf", sizeInBytes = 21L * 1024 * 1024 }, Json);
        await AssertErrorAsync(big, BusinessViolationCodes.DocumentTooLarge);

        using var multipart = new MultipartFormDataContent { { new ByteArrayContent([1, 2, 3]), "file", "x.pdf" } };
        var upload = await s.Branch.Admin.Client.PostAsync($"/api/staff/procurement/consignments/{id}/documents/upload", multipart);
        Assert.True(upload.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed or HttpStatusCode.UnsupportedMediaType);
    }

    // ── I-C5 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_C5_ClearingWithoutAPermitNumberIsRefused_AndWithAllThreeNumbersTheSequenceCompletes()
    {
        var s = await SeedAsync();
        var (without, _) = await PlanAsync(s.Branch.Admin.Client, PlanBody(s.OrderId, withClearanceNumbers: false));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var to in new[] { "InTransit", "AtCustoms" })
            Assert.Equal(HttpStatusCode.NoContent, (await s.Branch.Admin.Client.PostAsJsonAsync($"/api/staff/procurement/consignments/{without}/status", new { to, on = today }, Json)).StatusCode);

        var clear = await s.Branch.Admin.Client.PostAsJsonAsync($"/api/staff/procurement/consignments/{without}/status", new { to = "Cleared", on = today }, Json);
        await AssertErrorAsync(clear, BusinessViolationCodes.ClearanceDocumentsMissing);

        var skip = await s.Branch.Admin.Client.PostAsJsonAsync($"/api/staff/procurement/consignments/{without}/status", new { to = "InTransit", on = today }, Json);
        await AssertErrorAsync(skip, BusinessViolationCodes.InvalidConsignmentTransition);

        var (with, _) = await PlanAsync(s.Branch.Admin.Client, PlanBody(s.OrderId));
        foreach (var to in new[] { "InTransit", "AtCustoms", "Cleared" })
            Assert.Equal(HttpStatusCode.NoContent, (await s.Branch.Admin.Client.PostAsJsonAsync($"/api/staff/procurement/consignments/{with}/status", new { to, on = today }, Json)).StatusCode);

        var cleared = await DetailAsync(s.Branch.Admin.Client, with);
        Assert.Equal("Cleared", cleared.GetProperty("status").GetString());
        Assert.Equal(3, cleared.GetProperty("statusHistory").GetArrayLength());

        var cancelCleared = await s.Branch.Admin.Client.PostAsJsonAsync($"/api/staff/procurement/consignments/{with}/cancel", new { reason = "Too late." }, Json);
        await AssertErrorAsync(cancelCleared, BusinessViolationCodes.InvalidConsignmentTransition);
    }

    [Fact]
    public async Task I_C5_AnAccountantMayEditCostsButNotDetails_AndReceptionNeitherReadsNorWrites()
    {
        var s = await SeedAsync();
        var (id, _) = await PlanAsync(s.Branch.Admin.Client, PlanBody(s.OrderId));
        var accountant = await StaffAsync(s.Branch, StaffRoles.Accountant);
        var reception = await StaffAsync(s.Branch, StaffRoles.Reception);
        var version = (await DetailAsync(s.Branch.Admin.Client, id)).GetProperty("version").GetInt32();

        var costs = await accountant.Client.PutAsJsonAsync($"/api/staff/procurement/consignments/{id}/costs",
            new { expectedVersion = version, costComponents = new[] { new { kind = "Freight", amount = 10m, currencyCode = "BDT", note = (string?)null } }, exchangeRates = Array.Empty<object>() }, Json);
        Assert.Equal(HttpStatusCode.NoContent, costs.StatusCode);

        var cancel = await accountant.Client.PostAsJsonAsync($"/api/staff/procurement/consignments/{id}/cancel", new { reason = "No." }, Json);
        Assert.Equal(HttpStatusCode.Forbidden, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await accountant.Client.GetAsync($"/api/staff/procurement/consignments/{id}")).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await reception.Client.GetAsync($"/api/staff/procurement/consignments/{id}")).StatusCode);
    }
}
