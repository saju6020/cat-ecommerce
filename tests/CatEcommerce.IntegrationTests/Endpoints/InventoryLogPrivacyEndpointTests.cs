using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-IMP-001 I-X1 (TEST gate addition): across a recall, its trace (JSON and CSV) and the notify run, everything logged at Information and above carries ids
/// and codes only -- no customer name, mobile number, cat name or advice text. The captured log is asserted non-empty AND to contain the recall's own line, so an
/// empty capture cannot pass for a clean one.
/// </summary>
public sealed class InventoryLogPrivacyEndpointTests(InventoryApiFactory factory) : InventoryIntegrationTestBase(factory)
{
    private sealed class CapturingProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Lines { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Capture(this, categoryName);
        public void Dispose() { }

        private sealed class Capture(CapturingProvider owner, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (logLevel < LogLevel.Information) return;
                owner.Lines.Enqueue($"{category}: {formatter(state, exception)} {exception}");
            }
        }
    }

    [Fact]
    public async Task I_X1_LogsAcrossRecallTraceAndNotifyCarryNoCustomerNamesMobilesCatNamesOrAdviceText()
    {
        var capture = new CapturingProvider();
        using var logged = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddLogging(l => l.AddProvider(capture))));
        var branch = await SeedBranchAsync(logged);
        var keeper = await StaffAsync(branch, StaffRoles.Inventory);
        var sku = await CreateSkuAsync(branch.Admin.Client);
        var lot = NewLot();
        var batchId = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 20m, lot: lot);

        const string customerName = "Zephyrine Quillfeather";
        const string ownerName = "Barnaby Thistlewick";
        const string catName = "Mr Snufflepuss";
        const string advice = "Burn the remaining sacks behind the barn ZQX";
        var customerMobile = MobileNumber.Create($"017{Random.Shared.Next(10000000, 99999999)}");
        var ownerMobile = MobileNumber.Create($"019{Random.Shared.Next(10000000, 99999999)}");
        var seeder = new UserContext(Guid.NewGuid(), Guid.NewGuid());

        async Task<Guid> SeedAsync(string name, MobileNumber mobile)
        {
            using var scope = Factory.Services.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CustomerAccountAggregate>>();
            var account = new CustomerAccountAggregate(Guid.NewGuid());
            account.RegisterWalkIn(new WalkInCustomerRegistrationDto(name, mobile, seeder, Guid.NewGuid()));
            await repository.SaveAsync(account, EventPublishOption.InMemoryPublishOnly);
            return account.Id;
        }

        var customerId = await SeedAsync(customerName, customerMobile);
        var ownerId = await SeedAsync(ownerName, ownerMobile);

        var cat = await branch.Admin.Client.PostAsJsonAsync("/api/cats",
            new { ownerId, name = catName, species = "Cat", sex = "Male", dateOfBirth = UtcToday.AddYears(-3), dateOfBirthPrecision = "Exact" }, Json);
        var catId = (await JsonOkAsync(cat)).GetProperty("catId").GetGuid();

        await using (var port = OpenPort())
        {
            Assert.True((await port.Ledger.DeductAsync(Demand(branch.BranchId, sku.SkuId, 2m, MovementKind.Sale, locations: [branch.WarehouseId]), OrderReference(), Customer(customerId), default)).IsSuccess);
            await port.CommitAsync();
        }

        await keeper.Client.PostAsJsonAsync("/api/staff/inventory/transfers", new
        { fromLocationId = branch.WarehouseId, toLocationId = branch.DispensaryId, reason = "Stock the unit", lines = new[] { new { skuId = sku.SkuId, batchId, quantity = 5m } } }, Json);
        await using (var port = OpenPort())
        {
            Assert.True((await port.Ledger.DeductAsync(Demand(branch.BranchId, sku.SkuId, 1m, MovementKind.Dispense, locations: [branch.DispensaryId]),
                OrderReference(kind: StockReferenceKind.Prescription), Customer(null, catId), default)).IsSuccess);
            await port.CommitAsync();
        }

        var recall = await keeper.Client.PostAsJsonAsync($"/api/staff/inventory/batches/{batchId}/recall", new { confirmLotNumber = lot, reason = "Salmonella found in the lot.", customerAdvice = advice }, Json);
        Assert.Equal(HttpStatusCode.Created, recall.StatusCode);
        var trace = await JsonOkAsync(await keeper.Client.GetAsync($"/api/staff/inventory/recalls/{batchId}/trace"));
        Assert.Contains(customerName, trace.ToString()); // the trace DOES carry them (that is its job) -- only the log must not
        Assert.Equal(HttpStatusCode.OK, (await keeper.Client.GetAsync($"/api/staff/inventory/recalls/{batchId}/trace.csv")).StatusCode);
        var notify = await JsonOkAsync(await keeper.Client.PostAsync($"/api/staff/inventory/recalls/{batchId}/notify", null));
        Assert.Equal(2, notify.GetProperty("requested").GetInt32()); // the customer and the cat's owner

        var text = string.Join('\n', capture.Lines);
        Assert.Contains($"Declaring a recall of batch {batchId}", text); // the capture works and saw the recall
        Assert.Contains($"Notifying the recipients of the recall of batch {batchId}", text);
        foreach (var secret in new[] { customerName, ownerName, catName, advice, "Quillfeather", "Thistlewick", "Snufflepuss", customerMobile.Value, ownerMobile.Value, customerMobile.Value[^8..], ownerMobile.Value[^8..] })
            Assert.DoesNotContain(secret, text, StringComparison.OrdinalIgnoreCase);
    }
}
