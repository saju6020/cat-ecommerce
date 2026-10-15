using System.Net;
using System.Net.Http.Json;
using CatEcommerce.Api.Background;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Xunit.Abstractions;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-IMP-001 I-I4 and I-I6 against HO-NTF-001's REAL delivery pass (TEST gate addition; the implementer's sheet could not run them): the recall's notice reaches each
/// recipient's log and in-app inbox, and a withdrawn recall's pending notices are cancelled. The delivery pass is shared with the whole database, so it is drained
/// (bounded) until THIS test's own requests have settled, never asserted on global counts.
/// </summary>
public sealed class InventoryRecallDeliveryEndpointTests(InventoryApiFactory factory, ITestOutputHelper output) : InventoryIntegrationTestBase(factory)
{
    private async Task<Guid> SeedCustomerAsync(string name)
    {
        using var scope = Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CustomerAccountAggregate>>();
        var customer = new CustomerAccountAggregate(Guid.NewGuid());
        customer.RegisterWalkIn(new WalkInCustomerRegistrationDto(name, MobileNumber.Create($"018{Random.Shared.Next(10000000, 99999999)}"), new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.SaveAsync(customer, EventPublishOption.InMemoryPublishOnly);
        return customer.Id;
    }

    private sealed record Row(int RequestStatus, int? NotificationStatus, int? Channel, int? CloseReason, Guid RecipientId);

    private static async Task<List<Row>> RowsAsync(Guid batchId)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            SELECT r.status, n.status, n.channel, n.close_reason, r.recipient_id
              FROM notify.notification_request r LEFT JOIN notify.notification n ON n.request_id = r."Id"
             WHERE r.deduplication_key LIKE @prefix
            """, connection);
        command.Parameters.AddWithValue("prefix", $"recall:{batchId}:%");
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<Row>();
        while (await reader.ReadAsync())
            rows.Add(new Row(reader.GetInt32(0), reader.IsDBNull(1) ? null : reader.GetInt32(1), reader.IsDBNull(2) ? null : reader.GetInt32(2), reader.IsDBNull(3) ? null : reader.GetInt32(3), reader.GetGuid(4)));
        return rows;
    }

    private async Task DrainAsync(Guid batchId, int recipients)
    {
        for (var pass = 0; pass < 300; pass++)
        {
            using (var scope = Factory.Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<NotificationWorkCycle>().RunDeliveryPassAsync();

            var rows = await RowsAsync(batchId);
            var settled = rows.Count >= recipients && rows.All(r => r.RequestStatus != NotificationRequestStatus.Pending.Value
                && (r.NotificationStatus is null || (r.NotificationStatus != NotificationStatus.Queued.Value && r.NotificationStatus != NotificationStatus.Sending.Value && r.NotificationStatus != NotificationStatus.RetryPending.Value)));
            if (settled) return;
        }

        Assert.Fail("this test's recall notices never settled within 300 delivery passes");
    }

    [Fact]
    public async Task I_I4_TheDeliveryPassTurnsEachRecallRequestIntoANotificationInTheLog_WithTheBatchAsSubject_AndAnInAppCopy()
    {
        var branch = await SeedBranchAsync();
        var keeper = await StaffAsync(branch, StaffRoles.Inventory);
        var sku = await CreateSkuAsync(branch.Admin.Client);
        var lot = NewLot();
        var batchId = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 10m, lot: lot);
        var customers = new[] { await SeedCustomerAsync("Recall Reader One"), await SeedCustomerAsync("Recall Reader Two") };
        foreach (var customer in customers)
        {
            await using var port = OpenPort();
            Assert.True((await port.Ledger.DeductAsync(Demand(branch.BranchId, sku.SkuId, 1m, MovementKind.Sale, locations: [branch.WarehouseId]), OrderReference(), Customer(customer), default)).IsSuccess);
            await port.CommitAsync();
        }

        Assert.Equal(HttpStatusCode.Created, (await keeper.Client.PostAsJsonAsync($"/api/staff/inventory/batches/{batchId}/recall", new { confirmLotNumber = lot, reason = "Salmonella.", customerAdvice = "Stop feeding it." }, Json)).StatusCode);
        Assert.Equal(2, (await JsonOkAsync(await keeper.Client.PostAsync($"/api/staff/inventory/recalls/{batchId}/notify", null))).GetProperty("requested").GetInt32());

        await DrainAsync(batchId, 2);

        var rows = await RowsAsync(batchId);
        foreach (var row in rows) output.WriteLine($"request {row.RequestStatus}, notification {row.NotificationStatus}, channel {row.Channel}, closeReason {row.CloseReason}");
        Assert.Equal(customers.Order(), rows.Select(r => r.RecipientId).Distinct().Order());
        Assert.All(rows, r => Assert.Equal(NotificationRequestStatus.Expanded.Value, r.RequestStatus));
        foreach (var customer in customers)
        {
            var mine = rows.Where(r => r.RecipientId == customer).ToList();
            Assert.Contains(mine, r => r.NotificationStatus == NotificationStatus.InboxDelivered.Value); // the in-app copy
            Assert.All(mine, r => Assert.NotNull(r.NotificationStatus));                                   // each is in the HO-NTF-001 log
        }

        await using var connection = await OpenConnectionAsync();
        await using var subjects = new NpgsqlCommand("SELECT COUNT(*) FROM notify.notification n JOIN notify.notification_request r ON r.\"Id\" = n.request_id WHERE r.deduplication_key LIKE @p AND n.subject_id = @b", connection);
        subjects.Parameters.AddWithValue("p", $"recall:{batchId}:%");
        subjects.Parameters.AddWithValue("b", batchId);
        Assert.Equal((long)rows.Count(r => r.NotificationStatus is not null), (long)(await subjects.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task I_I6_WithdrawingTheRecallBeforeDeliveryCancelsThePendingNotices_NothingIsSent()
    {
        var branch = await SeedBranchAsync();
        var keeper = await StaffAsync(branch, StaffRoles.Inventory);
        var sku = await CreateSkuAsync(branch.Admin.Client);
        var lot = NewLot();
        var batchId = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 10m, lot: lot);
        var customer = await SeedCustomerAsync("Recall Reader Withdrawn");
        await using (var port = OpenPort())
        {
            Assert.True((await port.Ledger.DeductAsync(Demand(branch.BranchId, sku.SkuId, 1m, MovementKind.Sale, locations: [branch.WarehouseId]), OrderReference(), Customer(customer), default)).IsSuccess);
            await port.CommitAsync();
        }

        await keeper.Client.PostAsJsonAsync($"/api/staff/inventory/batches/{batchId}/recall", new { confirmLotNumber = lot, reason = "Declared on the wrong lot.", customerAdvice = "Stop." }, Json);
        Assert.Equal(1, (await JsonOkAsync(await keeper.Client.PostAsync($"/api/staff/inventory/recalls/{batchId}/notify", null))).GetProperty("requested").GetInt32());
        Assert.Equal(HttpStatusCode.NoContent, (await keeper.Client.PostAsJsonAsync($"/api/staff/inventory/recalls/{batchId}/withdraw", new { reason = "Declared on the wrong lot." }, Json)).StatusCode);

        await DrainAsync(batchId, 1);

        var rows = await RowsAsync(batchId);
        foreach (var row in rows) output.WriteLine($"request {row.RequestStatus}, notification {row.NotificationStatus}, channel {row.Channel}, closeReason {row.CloseReason}");
        var only = Assert.Single(rows);
        Assert.Equal(NotificationRequestStatus.Suppressed.Value, only.RequestStatus);
        Assert.Null(only.NotificationStatus); // no notification was even created
        await using (var connection = await OpenConnectionAsync())
        await using (var reason = new NpgsqlCommand("SELECT suppression_reason FROM notify.notification_request WHERE deduplication_key LIKE @p", connection))
        {
            reason.Parameters.AddWithValue("p", $"recall:{batchId}:%");
            Assert.Equal(NotificationSuppressionReason.SubjectNoLongerApplies.Value, (int)(await reason.ExecuteScalarAsync())!);
        }

        Assert.DoesNotContain(rows, r => r.NotificationStatus is { } s && (s == NotificationStatus.Sent.Value || s == NotificationStatus.InboxDelivered.Value)); // nothing reached anyone
    }
}
