using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Api.Branches;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-IMP-001 §16.1 -- the rules, as helpers. Every test creates its OWN branch (HO-ADM-001 endpoints) and its OWN stock locations in it
/// (C1), and mints its own SKUs through HO-SHP-001's endpoints, so no stock, FEFO, availability or alert assertion can see another test's
/// rows. Codes, lots and document numbers are minted per test. The only direct database write a test makes is setting a batch's
/// <c>expires_on</c> into the past (<see cref="SetBatchExpiryDirectlyAsync"/>) -- the batch table is not immutable.
/// </summary>
public abstract class InventoryIntegrationTestBase(InventoryApiFactory factory) : CatEcommerceIntegrationTestBase<InventoryApiFactory>(factory)
{
    protected override HttpClient CreateClient() => CreateClientOn(Factory);

    protected static HttpClient CreateClientOn(WebApplicationFactory<Program> apiFactory) =>
        apiFactory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost/"), HandleCookies = true });

    protected static string NewMobile() => "01" + Random.Shared.NextInt64(100_000_000, 999_999_999);

    protected static string NewCode(string prefix = "T") => $"{prefix}{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    protected static string NewLot() => "LOT-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();

    protected static string ExtractCookieValue(HttpResponseMessage response, string cookieName)
    {
        var setCookies = response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies.ToList() : [];
        var header = setCookies.First(c => c.StartsWith(cookieName + "=", StringComparison.Ordinal));
        return Uri.UnescapeDataString(header[(cookieName.Length + 1)..].Split(';')[0]);
    }

    // ── staff and branches ───────────────────────────────────────────────────────────────────────────────────────────────────────

    protected async Task<(Guid StaffAccountId, string Mobile, string Password)> SeedStaffAsync(params string[] roles) =>
        await SeedStaffOnAsync(Factory, roles);

    protected static async Task<(Guid StaffAccountId, string Mobile, string Password)> SeedStaffOnAsync(WebApplicationFactory<Program> apiFactory, params string[] roles)
    {
        await using var scope = apiFactory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<StaffAccountAggregate>>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var mobile = NewMobile();
        var staff = new StaffAccountAggregate(Guid.NewGuid());
        staff.Create(new StaffAccountCreationDto("Test Staff", MobileNumber.Create(mobile), null, roles, new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.SaveAsync(staff, EventPublishOption.InMemoryPublishOnly);

        const string password = "staffpassword1";
        staff.SetPassword(new PasswordSetDto(hasher.Hash(password), new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.UpdateAsync(staff, EventPublishOption.InMemoryPublishOnly);

        return (staff.Id, mobile, password);
    }

    protected static async Task<HttpClient> SignInStaffOnAsync(WebApplicationFactory<Program> apiFactory, string mobile, string password)
    {
        var client = CreateClientOn(apiFactory);
        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));
        return client;
    }

    /// <summary>Re-signs in after a clock jump (staff sessions idle out after 30 minutes).</summary>
    protected static async Task ReauthenticateAsync(HttpClient client, string mobile, string password)
    {
        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-Token");
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));
    }

    protected static void UseBranch(HttpClient client, Guid branchId)
    {
        client.DefaultRequestHeaders.Remove(HttpContextActiveBranchAccessor.BranchHeaderName);
        client.DefaultRequestHeaders.Add(HttpContextActiveBranchAccessor.BranchHeaderName, branchId.ToString());
    }

    /// <summary>One signed-in staff client of a role, assigned to the branch and naming it in <c>X-Branch-Id</c>.</summary>
    protected sealed record StaffClient(HttpClient Client, Guid StaffAccountId, string Mobile, string Password);

    /// <summary>A test's own branch: an administrator assigned to it (the <see cref="Admin"/> client) and the five locations it created.</summary>
    protected sealed record BranchContext(
        Guid BranchId, StaffClient Admin, Guid ShopId, Guid WarehouseId, Guid DispensaryId, Guid BoardingId, Guid QuarantineId,
        WebApplicationFactory<Program> ApiFactory);

    protected async Task<BranchContext> SeedBranchAsync(WebApplicationFactory<Program>? apiFactory = null, bool withLocations = true)
    {
        apiFactory ??= Factory;
        var admin = await SeedStaffOnAsync(apiFactory, StaffRoles.Administrator);
        var client = await SignInStaffOnAsync(apiFactory, admin.Mobile, admin.Password);

        var code = NewCode("B");
        var create = await client.PostAsJsonAsync("/api/admin/branches", new { code, name = "Test Branch " + code, timeZoneId = "Asia/Dhaka" }, Json);
        Assert.True(create.StatusCode == HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var branchId = (await create.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("branchId").GetGuid();

        var assign = await client.PutAsJsonAsync($"/api/admin/staff/{admin.StaffAccountId}/branches", new { branchIds = new[] { branchId }, primaryBranchId = branchId }, Json);
        Assert.True(assign.StatusCode == HttpStatusCode.NoContent, await assign.Content.ReadAsStringAsync());
        UseBranch(client, branchId);

        var adminClient = new StaffClient(client, admin.StaffAccountId, admin.Mobile, admin.Password);
        if (!withLocations) return new BranchContext(branchId, adminClient, default, default, default, default, default, apiFactory);

        return new BranchContext(
            branchId, adminClient,
            await CreateLocationAsync(client, "ShopFloor"), await CreateLocationAsync(client, "Warehouse"),
            await CreateLocationAsync(client, "ClinicDispensary"), await CreateLocationAsync(client, "BoardingStore"),
            await CreateLocationAsync(client, "Quarantine"), apiFactory);
    }

    protected static async Task<Guid> CreateLocationAsync(HttpClient admin, string kind, string? code = null)
    {
        var response = await admin.PostAsJsonAsync("/api/staff/inventory/locations", new { code = code ?? NewCode("L"), name = $"{kind} {Guid.NewGuid():N}"[..20], kind }, Json);
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("locationId").GetGuid();
    }

    /// <summary>A staff member of <paramref name="role"/>, assigned to the branch by its administrator, signed in with the branch header.</summary>
    protected async Task<StaffClient> StaffAsync(BranchContext branch, string role)
    {
        var staff = await SeedStaffOnAsync(branch.ApiFactory, role);
        var assign = await branch.Admin.Client.PutAsJsonAsync($"/api/admin/staff/{staff.StaffAccountId}/branches", new { branchIds = new[] { branch.BranchId }, primaryBranchId = branch.BranchId }, Json);
        Assert.True(assign.StatusCode == HttpStatusCode.NoContent, await assign.Content.ReadAsStringAsync());

        var client = await SignInStaffOnAsync(branch.ApiFactory, staff.Mobile, staff.Password);
        UseBranch(client, branch.BranchId);
        return new StaffClient(client, staff.StaffAccountId, staff.Mobile, staff.Password);
    }

    // ── SKUs (through HO-SHP-001's real endpoints) ───────────────────────────────────────────────────────────────────────────────

    protected sealed record SeededSku(Guid ProductId, Guid SkuId, string SkuCode, string? Barcode, string Name);

    /// <summary>Mints a product and one SKU through the shop endpoints. A <paramref name="food"/> product is Cat Food (expiry always
    /// tracked, D-2); <paramref name="expiryTracked"/> makes a non-food product expiry-tracked too (a stand-in for a medicine).</summary>
    protected async Task<SeededSku> CreateSkuAsync(HttpClient admin, bool food = false, bool expiryTracked = false, string? barcode = null, string? name = null)
    {
        var taxCode = NewCode("TAX");
        var taxClass = await admin.PostAsJsonAsync("/api/admin/tax-classes", new { code = taxCode, name = "Standard " + taxCode }, Json);
        Assert.True(taxClass.StatusCode == HttpStatusCode.Created, await taxClass.Content.ReadAsStringAsync());
        var taxClassId = (await taxClass.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("taxClassId").GetGuid();

        var productName = name ?? "Inventory test product " + Guid.NewGuid().ToString("N")[..8];
        var category = food ? "CatFood" : "HealthAndGrooming";
        var create = await admin.PostAsJsonAsync("/api/staff/shop/products",
            new { kind = "Standard", name = productName, brand = "Brand-" + Guid.NewGuid().ToString("N")[..6], category, taxClassId, slug = "p-" + Guid.NewGuid().ToString("N") }, Json);
        Assert.True(create.StatusCode == HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var productId = (await create.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("productId").GetGuid();

        if (expiryTracked)
        {
            var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/staff/shop/products/{productId}", Json);
            var details = await admin.PutAsJsonAsync($"/api/staff/shop/products/{productId}", new
            {
                expectedVersion = detail.GetProperty("version").GetInt32(), name = productName, brand = detail.GetProperty("brand").GetString(), category, taxClassId,
                isExpiryTracked = true, listedOnStorefront = true
            }, Json);
            Assert.True(details.StatusCode == HttpStatusCode.NoContent, await details.Content.ReadAsStringAsync());
        }

        var version = (await admin.GetFromJsonAsync<JsonElement>($"/api/staff/shop/products/{productId}", Json)).GetProperty("version").GetInt32();
        var skuCode = NewCode("SKU");
        var addSku = await admin.PostAsJsonAsync($"/api/staff/shop/products/{productId}/skus", new { expectedVersion = version, code = skuCode, barcode }, Json);
        Assert.True(addSku.StatusCode == HttpStatusCode.Created, await addSku.Content.ReadAsStringAsync());
        var skuId = (await addSku.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("skuId").GetGuid();

        return new SeededSku(productId, skuId, skuCode, barcode, productName);
    }

    // ── procurement fixtures (through the real endpoints) ───────────────────────────────────────────────────────────────────────

    protected static async Task<Guid> SeedSupplierAsync(HttpClient client, string? code = null, string currency = "EUR")
    {
        var supplierCode = code ?? NewCode("SUP");
        var response = await client.PostAsJsonAsync("/api/staff/procurement/suppliers", new
        {
            code = supplierCode, name = "Supplier " + supplierCode, countryCode = "DE", contactName = "Anna", phone = "+49 30 1234", email = "anna@example.test",
            address = "Berlin", currencyCode = currency, leadTimeDays = 21, paymentTerms = "30 days", paymentTermDays = 30
        }, Json);
        return (await JsonOkAsync(response)).GetProperty("supplierId").GetGuid();
    }

    /// <summary>An ISSUED purchase order (expected a week from now) with one line per tuple. Returns the order, its number and its line ids by SKU.</summary>
    protected static async Task<(Guid OrderId, string Number, IReadOnlyDictionary<Guid, Guid> LineIds)> SeedIssuedOrderAsync(
        HttpClient client, Guid supplierId, Guid locationId, string currency, params (Guid SkuId, decimal Quantity, decimal Cost)[] lines)
    {
        var create = await client.PostAsJsonAsync("/api/staff/procurement/purchase-orders", new
        {
            supplierId, deliverToLocationId = locationId, currencyCode = currency, expectedDeliveryOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7),
            lines = lines.Select(l => new { skuId = l.SkuId, orderedQuantity = l.Quantity, unitCostAmount = l.Cost }).ToArray()
        }, Json);
        var created = await JsonOkAsync(create);
        var orderId = created.GetProperty("purchaseOrderId").GetGuid();

        var issue = await client.PostAsync($"/api/staff/procurement/purchase-orders/{orderId}/issue", null);
        Assert.True(issue.StatusCode == HttpStatusCode.NoContent, await issue.Content.ReadAsStringAsync());

        var detail = await JsonOkAsync(await client.GetAsync($"/api/staff/procurement/purchase-orders/{orderId}"));
        var lineIds = detail.GetProperty("lines").EnumerateArray().ToDictionary(l => l.GetProperty("skuId").GetGuid(), l => l.GetProperty("lineId").GetGuid());
        return (orderId, created.GetProperty("number").GetString()!, lineIds);
    }

    // ── receiving (through the real endpoints) ──────────────────────────────────────────────────────────────────────────────────

    protected static DateOnly UtcToday => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>One receipt line body. Cost defaults to 5.00 BDT (the seeded base currency), so no rate is needed.</summary>
    protected static object ReceiptLine(
        Guid skuId, Guid locationId, decimal accepted, string? lot = null, DateOnly? expires = null, decimal rejected = 0m, string? rejectionReason = null,
        Guid? purchaseOrderLineId = null, decimal? cost = 5m, string? currency = "BDT", bool hold = false, decimal? rate = null) => new
    {
        skuId, locationId, lotNumber = lot ?? NewLot(), expiresOn = expires, quantityAccepted = accepted, quantityRejected = rejected, rejectionReason,
        purchaseOrderLineId, unitCostAmount = purchaseOrderLineId is null ? cost : null, unitCostCurrency = purchaseOrderLineId is null ? currency : null,
        holdForInspection = hold, exchangeRateToBase = rate
    };

    protected sealed record PostedReceipt(Guid ReceiptId, string Number, IReadOnlyList<Guid> BatchIds);

    protected static async Task<PostedReceipt> PostReceiptAsync(HttpClient client, object[] lines, Guid? purchaseOrderId = null, Guid? consignmentId = null, Guid? supplierId = null)
    {
        var response = await client.PostAsJsonAsync("/api/staff/inventory/goods-receipts", new { purchaseOrderId, consignmentId, supplierId, lines }, Json);
        var body = await JsonOkAsync(response);
        return new PostedReceipt(
            body.GetProperty("goodsReceiptId").GetGuid(), body.GetProperty("number").GetString()!,
            body.GetProperty("batchIds").EnumerateArray().Select(b => b.GetGuid()).ToList());
    }

    /// <summary>Receives <paramref name="quantity"/> of a new SKU's single new batch at <paramref name="locationId"/> and returns the batch id.</summary>
    protected static async Task<Guid> ReceiveAsync(HttpClient client, Guid skuId, Guid locationId, decimal quantity, DateOnly? expires = null, string? lot = null)
    {
        var receipt = await PostReceiptAsync(client, [ReceiptLine(skuId, locationId, quantity, lot, expires)]);
        return Assert.Single(receipt.BatchIds);
    }

    /// <summary>A CLEARED consignment containing <paramref name="orderId"/> (permit, certificate and declaration numbers set; planned, in transit,
    /// at customs, cleared).</summary>
    protected static async Task<Guid> SeedClearedConsignmentAsync(HttpClient client, Guid orderId)
    {
        var plan = await client.PostAsJsonAsync("/api/staff/procurement/consignments", new
        {
            originCountryCode = "DE", shipmentReference = "BL-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), purchaseOrderIds = new[] { orderId },
            importPermitNumber = "IP-1", healthCertificateNumber = "HC-1", customsDeclarationNumber = "CD-1"
        }, Json);
        var id = (await JsonOkAsync(plan)).GetProperty("consignmentId").GetGuid();

        foreach (var to in new[] { "InTransit", "AtCustoms", "Cleared" })
        {
            var advance = await client.PostAsJsonAsync($"/api/staff/procurement/consignments/{id}/status", new { to, on = UtcToday }, Json);
            Assert.True(advance.StatusCode == HttpStatusCode.NoContent, await advance.Content.ReadAsStringAsync());
        }

        return id;
    }

    // ── reading the projection and the ledger straight from the database (assertions only) ─────────────────────────────────────────

    protected static async Task<(decimal OnHand, decimal Reserved)> PositionAsync(Guid batchId, Guid locationId)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand("SELECT on_hand, reserved FROM inventory.stock_position WHERE batch_id = @b AND location_id = @l", connection);
        command.Parameters.AddWithValue("b", batchId);
        command.Parameters.AddWithValue("l", locationId);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? (reader.GetDecimal(0), reader.GetDecimal(1)) : (0m, 0m);
    }

    protected static async Task<(decimal LedgerSum, decimal PositionSum)> LedgerVersusPositionAsync(IReadOnlyCollection<Guid> batchIds)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT COALESCE((SELECT SUM(quantity_delta) FROM inventory.stock_ledger_entry WHERE batch_id = ANY(@ids)), 0), " +
            "COALESCE((SELECT SUM(on_hand) FROM inventory.stock_position WHERE batch_id = ANY(@ids)), 0)", connection);
        command.Parameters.AddWithValue("ids", batchIds.ToArray());
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (reader.GetDecimal(0), reader.GetDecimal(1));
    }

    protected static async Task<int> LedgerRowCountAsync(IReadOnlyCollection<Guid> batchIds)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand("SELECT COUNT(*) FROM inventory.stock_ledger_entry WHERE batch_id = ANY(@ids)", connection);
        command.Parameters.AddWithValue("ids", batchIds.ToArray());
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    // ── the port contract (IStockLedger exactly as a consumer handler uses it) ──────────────────────────────────────────────────

    /// <summary>A fresh DI scope of the port host (a fixed acting user): the ledger and the scoped <see cref="CatEcommerceDbContext"/> a consumer
    /// handler would be given. <see cref="CommitAsync"/> is the consumer's own repository save -- the port itself never saves (ADR-103).</summary>
    protected sealed class Port : IAsyncDisposable
    {
        private readonly IServiceScope _scope;

        public Port(IServiceScope scope, Guid userId)
        {
            _scope = scope;
            UserId = userId;
            Ledger = scope.ServiceProvider.GetRequiredService<IStockLedger>();
            Db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        }

        public Guid UserId { get; }
        public IStockLedger Ledger { get; }
        public CatEcommerceDbContext Db { get; }

        public Task<int> CommitAsync() => Db.SaveChangesAsync();

        /// <summary>How many rows the port has enlisted but not saved.</summary>
        public int Enlisted => Db.ChangeTracker.Entries().Count(e => e.State == EntityState.Added);

        public ValueTask DisposeAsync() { _scope.Dispose(); return ValueTask.CompletedTask; }
    }

    protected Port OpenPort() => new(Factory.PortHost.Services.CreateScope(), Factory.PortUserId);

    protected static StockDemand Demand(
        Guid branchId, Guid skuId, decimal quantity, MovementKind? purpose = null, IReadOnlyList<(Guid BatchId, Guid LocationId, decimal Quantity)>? lines = null,
        string? reason = null, IReadOnlyList<Guid>? locations = null) =>
        new(branchId, skuId, quantity, purpose ?? MovementKind.Sale, locations, lines, reason);

    protected static StockReference OrderReference(Guid? orderId = null, StockReferenceKind? kind = null) =>
        new(kind ?? StockReferenceKind.CustomerOrder, orderId ?? Guid.NewGuid(), null, "Order PC-" + Random.Shared.Next(100000, 999999));

    protected static StockParty Customer(Guid? customerId = null, Guid? catId = null) => new(customerId ?? Guid.NewGuid(), catId);

    protected static void AssertPort<T>(StockResult<T> result, string expectedCode)
    {
        Assert.False(result.IsSuccess, $"expected {expectedCode} but the port succeeded");
        Assert.Equal(expectedCode, result.ViolationCode);
    }

    /// <summary>The commit a consumer handler would do when the port enlisted ONLY reservation or ledger rows: the outcome as a Postgres error
    /// (<c>23514</c> and its constraint) or success.</summary>
    protected static async Task<string> CommitOutcomeAsync(Port port)
    {
        try { await port.CommitAsync(); return "committed"; }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg) { return $"{pg.SqlState}:{pg.ConstraintName}"; }
    }

    /// <summary>Inserts a ledger row straight through Npgsql -- the defence-in-depth tests (I-E8, I-I3): the usability trigger must refuse it whatever
    /// the application would have said. The only thing the tests do to the ledger directly, and it is always REFUSED.</summary>
    protected static async Task<PostgresException> InsertLedgerRowDirectlyAsync(Guid branchId, Guid locationId, Guid skuId, Guid batchId, MovementKind kind, decimal delta)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            INSERT INTO inventory.stock_ledger_entry
              ("Id", occurred_on, branch_id, location_id, sku_id, batch_id, kind, quantity_delta, consumes_reservation, reference_kind, reference_id, fefo_overridden, performed_by,
               "CreatedBy", "CreatedDate", "LastUpdatedDate", "LastUpdatedBy", "TenantId", "VerticalId", "IsMarkedToDelete", "Version")
            VALUES (@id, now(), @branch, @location, @sku, @batch, @kind, @delta, false, @refKind, @ref, false, @by, @by, now(), now(), @by, @by, @by, false, 1)
            """, connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("branch", branchId);
        command.Parameters.AddWithValue("location", locationId);
        command.Parameters.AddWithValue("sku", skuId);
        command.Parameters.AddWithValue("batch", batchId);
        command.Parameters.AddWithValue("kind", kind.Value);
        command.Parameters.AddWithValue("delta", delta);
        command.Parameters.AddWithValue("refKind", StockReferenceKind.CustomerOrder.Value);
        command.Parameters.AddWithValue("ref", Guid.NewGuid());
        command.Parameters.AddWithValue("by", Guid.NewGuid());
        return await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
    }

    // ── small HTTP helpers ───────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Reads a successful response as JSON; a failure carries the status and body so a flaky 4xx/5xx is diagnosable.</summary>
    protected static async Task<JsonElement> JsonOkAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<JsonElement>(body, Json);
    }

    /// <summary>The <c>errorCode</c>s (and their property names) of a 400/409 validation response body.</summary>
    protected static List<(string Code, string Property)> ParseErrors(string body)
    {
        var errors = new List<(string, string)>();
        var json = JsonSerializer.Deserialize<JsonElement>(body, Json);
        if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("errors", out var array) && array.ValueKind == JsonValueKind.Array)
        {
            foreach (var error in array.EnumerateArray())
            {
                var code = error.TryGetProperty("errorCode", out var c) ? c.GetString() ?? string.Empty : string.Empty;
                var property = error.TryGetProperty("propertyName", out var p) ? p.GetString() ?? string.Empty : string.Empty;
                errors.Add((code, property));
            }
        }

        return errors;
    }

    protected static async Task AssertErrorAsync(HttpResponseMessage response, string expectedCode, HttpStatusCode status = HttpStatusCode.BadRequest, string? expectedProperty = null)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status, $"expected {(int)status} but got {(int)response.StatusCode}: {body}");
        var errors = ParseErrors(body);
        Assert.True(errors.Any(e => e.Code == expectedCode && (expectedProperty is null || e.Property == expectedProperty)),
            $"expected error {expectedCode}{(expectedProperty is null ? "" : " on " + expectedProperty)} in: {body}");
    }

    // ── direct database access (setup only -- §16.1 rule 3) ──────────────────────────────────────────────────────────────────────

    protected static string ConnectionString { get; } = InventoryApiFactory.RequireBranchDatabase();

    protected static async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    /// <summary>The ONE direct write any test makes: a batch is not an immutable table, so a test that needs an already-expired batch sets
    /// <c>expires_on</c> into the past here -- a real receipt refuses to accept expired goods (BR-3).</summary>
    protected async Task SetBatchExpiryDirectlyAsync(Guid batchId, DateOnly expiresOn)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand("UPDATE inventory.batch SET expires_on = @on WHERE \"Id\" = @id", connection);
        command.Parameters.AddWithValue("on", expiresOn);
        command.Parameters.AddWithValue("id", batchId);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }
}
