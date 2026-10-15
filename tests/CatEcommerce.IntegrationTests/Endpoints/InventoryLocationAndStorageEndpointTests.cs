using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Application.CommandHandlers.Inventory;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-IMP-001 §16 phase A -- I-A2 ... I-A5 (I-A1, the schema, is <c>InventorySchemaTests</c>).</summary>
public sealed class InventoryLocationAndStorageEndpointTests(InventoryApiFactory factory) : InventoryIntegrationTestBase(factory)
{
    // ── I-A2 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_A2_HostStartsWithScopeValidation_AndEveryInventoryHandlerValidatorAndQueryHandlerResolves()
    {
        // WebApplicationFactory builds the host with ValidateScopes and ValidateOnBuild on (Program.cs) -- reaching here proves it started.
        await using var scope = Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var commandHandlers = typeof(CreateStockLocationCommandHandler).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.Namespace == typeof(CreateStockLocationCommandHandler).Namespace)
            .SelectMany(t => t.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommandHandlerAsync<>)).Select(i => (Handler: t, Service: i)))
            .ToList();

        Assert.NotEmpty(commandHandlers);
        foreach (var (handler, service) in commandHandlers)
        {
            Assert.IsType(handler, services.GetRequiredService(service));
            var validator = typeof(FluentValidation.IValidator<>).MakeGenericType(service.GetGenericArguments()[0]);
            Assert.NotNull(services.GetService(validator));
        }

        var queryHandlers = typeof(CatEcommerce.Read.QueryHandlers.Inventory.GetStockLocationsQueryHandler).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.Namespace == typeof(CatEcommerce.Read.QueryHandlers.Inventory.GetStockLocationsQueryHandler).Namespace)
            .SelectMany(t => t.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQueryHandlerAsync<,>)).Select(i => (Handler: t, Service: i)))
            .ToList();

        Assert.NotEmpty(queryHandlers);
        foreach (var (handler, service) in queryHandlers)
            Assert.IsType(handler, services.GetRequiredService(service));

        Assert.NotNull(services.GetRequiredService<ISkuDirectory>());
        Assert.NotNull(services.GetRequiredService<IInventoryClock>());
        Assert.NotNull(services.GetRequiredService<IDocumentNumberGenerator>());
        Assert.NotNull(services.GetRequiredService<IStockPositionReader>());
        Assert.NotNull(services.GetRequiredService<IBatchRegistry>());

        // ADR-103 / §12.7: ONE EfStockLedger per scope behind BOTH stock ports, so a unit of work shares one view of the rows it has enlisted.
        var ledger = services.GetRequiredService<IStockLedger>();
        Assert.IsType<CatEcommerce.Infrastructure.Inventory.EfStockLedger>(ledger);
        Assert.Same(ledger, services.GetRequiredService<IInventoryLedgerWriter>());
        Assert.Same(ledger, services.GetRequiredService<CatEcommerce.Infrastructure.Inventory.EfStockLedger>());

        await using var other = Factory.Services.CreateAsyncScope();
        Assert.NotSame(ledger, other.ServiceProvider.GetRequiredService<IStockLedger>());
    }

    // ── I-A3 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_A3_ALocationOfAnotherBranchIsA404_AndIsNotInThisBranchsList()
    {
        var branchA = await SeedBranchAsync(withLocations: false);
        var branchB = await SeedBranchAsync(withLocations: false);
        var inB = await CreateLocationAsync(branchB.Admin.Client, "ShopFloor");

        var update = await branchA.Admin.Client.PutAsJsonAsync($"/api/staff/inventory/locations/{inB}", new { name = "Hijacked" }, Json);
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);

        var list = await JsonOkAsync(await branchA.Admin.Client.GetAsync("/api/staff/inventory/locations"));
        Assert.DoesNotContain(list.EnumerateArray(), l => l.GetProperty("id").GetGuid() == inB);

        var listB = await JsonOkAsync(await branchB.Admin.Client.GetAsync("/api/staff/inventory/locations"));
        Assert.Contains(listB.EnumerateArray(), l => l.GetProperty("id").GetGuid() == inB);
    }

    // ── I-A4 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_A4_ASecondActiveQuarantineLocationInABranchIsRefused()
    {
        var branch = await SeedBranchAsync(withLocations: false);
        await CreateLocationAsync(branch.Admin.Client, "Quarantine");

        var second = await branch.Admin.Client.PostAsJsonAsync("/api/staff/inventory/locations", new { code = NewCode("Q"), name = "Second quarantine", kind = "Quarantine" }, Json);

        await AssertErrorAsync(second, BusinessViolationCodes.QuarantineLocationAlreadyExists);
    }

    [Fact]
    public async Task I_A4_ADuplicateLocationCodeInABranchIsRefused_AndTheListCarriesPermittedMovements()
    {
        var branch = await SeedBranchAsync(withLocations: false);
        var code = NewCode("D");
        await CreateLocationAsync(branch.Admin.Client, "Warehouse", code);

        var duplicate = await branch.Admin.Client.PostAsJsonAsync("/api/staff/inventory/locations", new { code = code.ToLowerInvariant(), name = "Again", kind = "ShopFloor" }, Json);
        await AssertErrorAsync(duplicate, BusinessViolationCodes.StockLocationCodeAlreadyExists);

        var list = await JsonOkAsync(await branch.Admin.Client.GetAsync("/api/staff/inventory/locations"));
        var warehouse = Assert.Single(list.EnumerateArray());
        var movements = warehouse.GetProperty("permittedMovements").EnumerateArray().Select(m => m.GetString()).ToList();
        Assert.Contains("Sale", movements);
        Assert.Contains("Receipt", movements);
        Assert.DoesNotContain("Dispense", movements);
    }

    [Fact]
    public async Task I_A4_OnlyAnAdministratorMaintainsLocations_AndReceptionMayReadThem()
    {
        var branch = await SeedBranchAsync(withLocations: false);
        var reception = await StaffAsync(branch, StaffRoles.Reception);
        var inventory = await StaffAsync(branch, StaffRoles.Inventory);

        var create = await reception.Client.PostAsJsonAsync("/api/staff/inventory/locations", new { code = NewCode("R"), name = "Nope", kind = "ShopFloor" }, Json);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);

        var createAsInventory = await inventory.Client.PostAsJsonAsync("/api/staff/inventory/locations", new { code = NewCode("R"), name = "Nope", kind = "ShopFloor" }, Json);
        Assert.Equal(HttpStatusCode.Forbidden, createAsInventory.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await reception.Client.GetAsync("/api/staff/inventory/locations")).StatusCode);
    }

    // ── I-A5 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_A5_SkuLookupByBarcodeAndByText_ReturnsTheSkuWithItsStorageAfterC2()
    {
        var branch = await SeedBranchAsync(withLocations: false);
        var barcode = "BAR-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        var sku = await CreateSkuAsync(branch.Admin.Client, food: true, barcode: barcode);

        var before = await JsonOkAsync(await branch.Admin.Client.GetAsync($"/api/staff/inventory/skus?barcode={barcode}"));
        var plain = Assert.Single(before.EnumerateArray());
        Assert.Equal(sku.SkuId, plain.GetProperty("skuId").GetGuid());
        Assert.Equal(JsonValueKind.Null, plain.GetProperty("storage").ValueKind);
        Assert.Equal("Food", plain.GetProperty("stockClass").GetString());
        Assert.True(plain.GetProperty("expiryRequired").GetBoolean());
        Assert.Equal("each", plain.GetProperty("stockUnit").GetString());

        var set = await branch.Admin.Client.PutAsJsonAsync($"/api/staff/inventory/storage-profiles/{sku.ProductId}",
            new { condition = "Refrigerated", minTemperatureC = 2, maxTemperatureC = 8, handlingNotes = "Keep closed." }, Json);
        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);

        var byText = await JsonOkAsync(await branch.Admin.Client.GetAsync($"/api/staff/inventory/skus?search={sku.SkuCode.ToLowerInvariant()}"));
        var found = Assert.Single(byText.EnumerateArray());
        var storage = found.GetProperty("storage");
        Assert.Equal("Refrigerated", storage.GetProperty("condition").GetString());
        Assert.Equal(2m, storage.GetProperty("minTemperatureC").GetDecimal());
        Assert.Equal(8m, storage.GetProperty("maxTemperatureC").GetDecimal());

        var profile = await JsonOkAsync(await branch.Admin.Client.GetAsync($"/api/staff/inventory/storage-profiles/{sku.ProductId}"));
        Assert.Equal("Keep closed.", profile.GetProperty("storage").GetProperty("handlingNotes").GetString());
    }

    [Fact]
    public async Task I_A5_AStorageProfileWithAnInvertedRangeIsRefused_AndALookupWithNoArgumentIsA400()
    {
        var branch = await SeedBranchAsync(withLocations: false);
        var sku = await CreateSkuAsync(branch.Admin.Client);

        var inverted = await branch.Admin.Client.PutAsJsonAsync($"/api/staff/inventory/storage-profiles/{sku.ProductId}",
            new { condition = "Refrigerated", minTemperatureC = 8, maxTemperatureC = 2 }, Json);
        await AssertErrorAsync(inverted, BusinessViolationCodes.InvalidTemperatureRange);

        var unknown = await branch.Admin.Client.PutAsJsonAsync($"/api/staff/inventory/storage-profiles/{Guid.NewGuid()}", new { condition = "Ambient" }, Json);
        await AssertErrorAsync(unknown, BusinessViolationCodes.SkuNotFound);

        var empty = await branch.Admin.Client.GetAsync("/api/staff/inventory/skus");
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }
}
