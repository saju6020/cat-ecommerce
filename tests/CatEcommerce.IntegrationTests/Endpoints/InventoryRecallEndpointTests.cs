using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Notifications.Guards;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-IMP-001 section 16 phase I -- I-I1 ... I-I9: declaring a recall, the trace across every consumption path (merges followed at READ time), the notification
/// requests, withdrawal, closing, who may do any of it, and the recall-versus-reservation race. Customers, cats and merges are seeded through the account
/// repositories, as the other areas' tests do.
/// </summary>
public sealed class InventoryRecallEndpointTests(InventoryApiFactory factory) : InventoryIntegrationTestBase(factory)
{
    private static readonly UserContext Seeder = new(Guid.NewGuid(), Guid.NewGuid());

    private async Task<(Guid Id, MobileNumber Mobile)> SeedCustomerAsync(string name)
    {
        using var scope = Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CustomerAccountAggregate>>();
        var customer = new CustomerAccountAggregate(Guid.NewGuid());
        var mobile = MobileNumber.Create($"018{Random.Shared.Next(10000000, 99999999)}");
        customer.RegisterWalkIn(new WalkInCustomerRegistrationDto(name, mobile, Seeder, Guid.NewGuid()));
        await repository.SaveAsync(customer, EventPublishOption.InMemoryPublishOnly);
        return (customer.Id, mobile);
    }

    private async Task MergeAsync(Guid duplicateId, Guid survivorId, MobileNumber sharedMobile)
    {
        using var scope = Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CustomerAccountAggregate>>();
        var duplicate = await repository.GetByIdAsync(duplicateId);
        duplicate.MergeInto(new CustomerMergeDto(survivorId, "Same person.", sharedMobile, [], null, DateTimeOffset.UtcNow, Seeder, Guid.NewGuid()));
        await repository.UpdateAsync(duplicate, EventPublishOption.InMemoryPublishOnly);
    }

    private async Task AnonymiseAsync(Guid customerId)
    {
        using var scope = Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CustomerAccountAggregate>>();
        var customer = await repository.GetByIdAsync(customerId);
        customer.Anonymise(new CustomerAnonymisationDto(DateTimeOffset.UtcNow, Seeder, Guid.NewGuid()));
        await repository.UpdateAsync(customer, EventPublishOption.InMemoryPublishOnly);
    }

    private static async Task<Guid> RegisterCatAsync(HttpClient admin, Guid ownerId, string name)
    {
        var response = await admin.PostAsJsonAsync("/api/cats", new
        { ownerId, name, species = "Cat", sex = "Female", dateOfBirth = UtcToday.AddYears(-2), dateOfBirthPrecision = "Exact" }, Json);
        return (await JsonOkAsync(response)).GetProperty("catId").GetGuid();
    }

    private sealed record Scenario(
        BranchContext Branch, StaffClient Keeper, SeededSku Sku, Guid BatchId, string Lot, Guid A, Guid ASurvivor, Guid B, Guid C, Guid D, Guid CatX, Guid CatY, Guid OrderD);

    /// <summary>One batch of 100 at the warehouse (10 moved to the dispensary and 10 to the boarding store), then every way of consuming it: a sale to customer A
    /// (later merged into A'), a dispense to cat X of owner B, a boarding use for cat Y of owner C, a counter sale with no customer, and a reservation for the order
    /// of customer D. The recall is NOT yet declared.</summary>
    private async Task<Scenario> ConsumeAsync()
    {
        var branch = await SeedBranchAsync();
        var keeper = await StaffAsync(branch, StaffRoles.Inventory);
        var sku = await CreateSkuAsync(branch.Admin.Client);
        var lot = NewLot();
        var batchId = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 100m, lot: lot);

        foreach (var to in new[] { branch.DispensaryId, branch.BoardingId })
        {
            var transfer = await keeper.Client.PostAsJsonAsync("/api/staff/inventory/transfers", new
            { fromLocationId = branch.WarehouseId, toLocationId = to, reason = "Stock the unit", lines = new[] { new { skuId = sku.SkuId, batchId, quantity = 10m } } }, Json);
            Assert.Equal(HttpStatusCode.Created, transfer.StatusCode);
        }

        var (a, aMobile) = await SeedCustomerAsync("Customer A");
        var (aSurvivor, _) = await SeedCustomerAsync("Customer A Prime");
        var (b, _) = await SeedCustomerAsync("Owner B");
        var (c, _) = await SeedCustomerAsync("Owner C");
        var (d, _) = await SeedCustomerAsync("Customer D");
        var catX = await RegisterCatAsync(branch.Admin.Client, b, "Xena");
        var catY = await RegisterCatAsync(branch.Admin.Client, c, "Yuki");
        var orderD = Guid.NewGuid();

        async Task CommitAsync(Func<Port, Task<bool>> act)
        {
            await using var port = OpenPort();
            Assert.True(await act(port));
            await port.CommitAsync();
        }

        await CommitAsync(async p => (await p.Ledger.DeductAsync(Demand(branch.BranchId, sku.SkuId, 2m, MovementKind.Sale, locations: [branch.WarehouseId]), OrderReference(), Customer(a), default)).IsSuccess);
        await CommitAsync(async p => (await p.Ledger.DeductAsync(Demand(branch.BranchId, sku.SkuId, 1m, MovementKind.Dispense, locations: [branch.DispensaryId]),
            OrderReference(kind: StockReferenceKind.Prescription), Customer(null, catX), default)).IsSuccess);
        await CommitAsync(async p => (await p.Ledger.DeductAsync(Demand(branch.BranchId, sku.SkuId, 1m, MovementKind.BoardingUse, locations: [branch.BoardingId]),
            OrderReference(kind: StockReferenceKind.BoardingStay), Customer(null, catY), default)).IsSuccess);
        await CommitAsync(async p => (await p.Ledger.DeductAsync(Demand(branch.BranchId, sku.SkuId, 3m, MovementKind.Sale, locations: [branch.WarehouseId]),
            new StockReference(StockReferenceKind.PosSale, Guid.NewGuid(), null, "POS-7"), new StockParty(null, null), default)).IsSuccess);
        await CommitAsync(async p => (await p.Ledger.ReserveAsync(Demand(branch.BranchId, sku.SkuId, 4m, locations: [branch.WarehouseId]), OrderReference(orderD), Customer(d), default)).IsSuccess);

        await MergeAsync(a, aSurvivor, aMobile);
        return new Scenario(branch, keeper, sku, batchId, lot, a, aSurvivor, b, c, d, catX, catY, orderD);
    }

    private static Task<HttpResponseMessage> RecallAsync(HttpClient client, Guid batchId, string lot) =>
        client.PostAsJsonAsync($"/api/staff/inventory/batches/{batchId}/recall", new { confirmLotNumber = lot, reason = "Salmonella found in the lot.", customerAdvice = "Stop feeding it and keep the packaging." }, Json);

    // ── I-I1 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_I1_ARecallMakesTheBatchUnusableEverywhereAtOnce_AndTheLotMustBeTypedExactly()
    {
        var s = await ConsumeAsync();

        await AssertErrorAsync(await RecallAsync(s.Keeper.Client, s.BatchId, s.Lot.ToLowerInvariant()), BusinessViolationCodes.RecallConfirmationMismatch);
        Assert.Equal("Available", (await JsonOkAsync(await s.Keeper.Client.GetAsync($"/api/staff/inventory/batches/{s.BatchId}"))).GetProperty("status").GetString());

        var response = await RecallAsync(s.Keeper.Client, s.BatchId, s.Lot);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Matches(@"^RCL-\d{4}-\d{6}$", (await JsonOkAsync(response)).GetProperty("recallNumber").GetString());
        var batch = await JsonOkAsync(await s.Keeper.Client.GetAsync($"/api/staff/inventory/batches/{s.BatchId}"));
        Assert.Equal("Recalled", batch.GetProperty("status").GetString());
        Assert.Equal("Salmonella found in the lot.", batch.GetProperty("recall").GetProperty("reason").GetString());

        var preview = await JsonOkAsync(await s.Keeper.Client.GetAsync($"/api/staff/inventory/fefo-preview?skuId={s.Sku.SkuId}&quantity=1&purpose=Sale"));
        Assert.True(preview.TryGetProperty("shortfall", out var shortfall) && shortfall.ValueKind != JsonValueKind.Null, preview.ToString());

        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand("SELECT COALESCE(SUM(sellable_quantity), 0) FROM inventory.v_sellable_stock WHERE sku_id = @s", connection);
        command.Parameters.AddWithValue("s", s.Sku.SkuId);
        Assert.Equal(0m, (decimal)(await command.ExecuteScalarAsync())!);

        await AssertErrorAsync(await RecallAsync(s.Keeper.Client, s.BatchId, s.Lot), BusinessViolationCodes.InvalidBatchTransition); // already recalled
    }

    // ── I-I2 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_I2_TheTraceListsEveryConsumptionPath_FollowsTheMerge_AndNamesTheUnidentifiedAndTheUnshipped()
    {
        var s = await ConsumeAsync();
        await RecallAsync(s.Keeper.Client, s.BatchId, s.Lot);

        var trace = await JsonOkAsync(await s.Keeper.Client.GetAsync($"/api/staff/inventory/recalls/{s.BatchId}/trace"));

        var received = trace.GetProperty("received").EnumerateArray().ToList();
        Assert.Equal(3, received.Count); // the counter sale is "unidentified", not "received by"

        var sale = received.Single(r => r.GetProperty("kind").GetString() == "Sale");
        Assert.Equal(s.ASurvivor, sale.GetProperty("customer").GetProperty("customerId").GetGuid()); // A, merged into A', resolved at READ time
        Assert.Equal(s.A, sale.GetProperty("customer").GetProperty("originalCustomerId").GetGuid());
        Assert.Equal("Customer A Prime", sale.GetProperty("customer").GetProperty("name").GetString());

        var dispense = received.Single(r => r.GetProperty("kind").GetString() == "Dispense");
        Assert.Equal("Xena", dispense.GetProperty("cat").GetProperty("name").GetString());
        Assert.Equal(s.B, dispense.GetProperty("cat").GetProperty("currentOwner").GetProperty("customerId").GetGuid());

        var boarding = received.Single(r => r.GetProperty("kind").GetString() == "BoardingUse");
        Assert.Equal(s.C, boarding.GetProperty("cat").GetProperty("currentOwner").GetProperty("customerId").GetGuid());

        var unidentified = trace.GetProperty("unidentified");
        Assert.Equal(1, unidentified.GetProperty("count").GetInt32());
        Assert.Equal("3", unidentified.GetProperty("quantity").GetString());

        var unshipped = Assert.Single(trace.GetProperty("unshippedDispatchBlocked").EnumerateArray());
        Assert.Equal(s.OrderD, unshipped.GetProperty("referenceId").GetGuid());
        Assert.Equal(s.D, unshipped.GetProperty("customer").GetProperty("customerId").GetGuid());
        Assert.Equal("4", unshipped.GetProperty("quantity").GetString());

        Assert.True(trace.GetProperty("stockHeld").GetArrayLength() >= 3); // warehouse, dispensary, boarding store
    }

    [Fact]
    public async Task I_I2_AnonymisedCustomersAppearWithoutContactDetails_AndANonRecallBatchHasNoTrace()
    {
        var s = await ConsumeAsync();
        await AnonymiseAsync(s.D);
        await RecallAsync(s.Keeper.Client, s.BatchId, s.Lot);

        var trace = await JsonOkAsync(await s.Keeper.Client.GetAsync($"/api/staff/inventory/recalls/{s.BatchId}/trace"));
        var customer = Assert.Single(trace.GetProperty("unshippedDispatchBlocked").EnumerateArray()).GetProperty("customer");
        Assert.True(customer.GetProperty("isAnonymised").GetBoolean());
        Assert.Equal("Anonymised customer", customer.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, customer.GetProperty("mobileNumber").ValueKind);

        var other = await ConsumeAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await other.Keeper.Client.GetAsync($"/api/staff/inventory/recalls/{other.BatchId}/trace")).StatusCode);
    }

    [Fact]
    public async Task I_I2_AReleasedReservationIsNotUnshipped_AndItsCustomerIsNotNotified()
    {
        var branch = await SeedBranchAsync();
        var keeper = await StaffAsync(branch, StaffRoles.Inventory);
        var sku = await CreateSkuAsync(branch.Admin.Client);
        var lot = NewLot();
        var batchId = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 20m, lot: lot);
        var (customer, _) = await SeedCustomerAsync("Released Customer");
        var (held, _) = await SeedCustomerAsync("Still Waiting");
        var released = OrderReference();
        var open = OrderReference();

        await using (var port = OpenPort())
        {
            Assert.True((await port.Ledger.ReserveAsync(Demand(branch.BranchId, sku.SkuId, 3m, locations: [branch.WarehouseId]), released, Customer(customer), default)).IsSuccess);
            Assert.True((await port.Ledger.ReserveAsync(Demand(branch.BranchId, sku.SkuId, 2m, locations: [branch.WarehouseId]), open, Customer(held), default)).IsSuccess);
            await port.CommitAsync();
        }

        await using (var port = OpenPort())
        {
            Assert.True((await port.Ledger.ReleaseAsync(released, default)).IsSuccess);
            await port.CommitAsync();
        }

        await RecallAsync(keeper.Client, batchId, lot);

        var trace = await JsonOkAsync(await keeper.Client.GetAsync($"/api/staff/inventory/recalls/{batchId}/trace"));
        var unshipped = Assert.Single(trace.GetProperty("unshippedDispatchBlocked").EnumerateArray());
        Assert.Equal(open.ReferenceId, unshipped.GetProperty("referenceId").GetGuid()); // the released order is gone; the open one stays
        Assert.Equal("2", unshipped.GetProperty("quantity").GetString());

        var run = await JsonOkAsync(await keeper.Client.PostAsync($"/api/staff/inventory/recalls/{batchId}/notify", null));
        Assert.Equal(1, run.GetProperty("requested").GetInt32());
        var requests = await RequestsAsync(batchId);
        Assert.Equal(held, Assert.Single(requests).RecipientId);
        Assert.DoesNotContain(requests, r => r.RecipientId == customer);
    }

    // ── I-I3 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_I3_ARecalledBatchCannotBeConsumedOrReserved_EvenThroughTheDatabaseDirectly()
    {
        var s = await ConsumeAsync();
        await RecallAsync(s.Keeper.Client, s.BatchId, s.Lot);

        await using (var port = OpenPort())
        {
            var consumed = await port.Ledger.ConsumeReservationAsync(OrderReference(s.OrderD), Customer(s.D), default);
            AssertPort(consumed, BusinessViolationCodes.BatchNotUsable);
        }

        var refused = await InsertLedgerRowDirectlyAsync(s.Branch.BranchId, s.Branch.WarehouseId, s.Sku.SkuId, s.BatchId, MovementKind.Sale, -1m);
        Assert.Contains("tg_stock_batch_not_usable", refused.MessageText + refused.ConstraintName + refused.Where);
    }

    // ── I-I4 / I-I5 ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

    private static async Task<List<(Guid RecipientId, string Key, Guid? SubjectId)>> RequestsAsync(Guid batchId)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT recipient_id, deduplication_key, subject_id FROM notify.notification_request WHERE deduplication_key LIKE @prefix", connection);
        command.Parameters.AddWithValue("prefix", $"recall:{batchId}:%");
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<(Guid, string, Guid?)>();
        while (await reader.ReadAsync()) rows.Add((reader.GetGuid(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetGuid(2)));
        return rows;
    }

    [Fact]
    public async Task I_I4_NotifyEnqueuesOneRequestPerRecipient_WithTheBatchAsSubject_AndASecondRunAddsNone()
    {
        var s = await ConsumeAsync();
        await RecallAsync(s.Keeper.Client, s.BatchId, s.Lot);

        var first = await JsonOkAsync(await s.Keeper.Client.PostAsync($"/api/staff/inventory/recalls/{s.BatchId}/notify", null));

        Assert.Equal(4, first.GetProperty("requested").GetInt32()); // A', B, C and D's customer
        Assert.Equal(0, first.GetProperty("remaining").GetInt32());
        var requests = await RequestsAsync(s.BatchId);
        Assert.Equal(4, requests.Count);
        Assert.Equivalent(new[] { s.ASurvivor, s.B, s.C, s.D }, requests.Select(r => r.RecipientId));
        Assert.All(requests, r => Assert.Equal(s.BatchId, r.SubjectId));

        var detail = await JsonOkAsync(await s.Keeper.Client.GetAsync($"/api/staff/inventory/recalls/{s.BatchId}"));
        Assert.Equal(4, detail.GetProperty("recall").GetProperty("notifiedRecipientCount").GetInt32());

        await using (var connection = await OpenConnectionAsync())
        await using (var command = new NpgsqlCommand("SELECT \"AfterValue\" FROM audit.audit_entry WHERE \"Action\" = 'RecallNotified' AND \"EntityId\" = @id", connection))
        {
            command.Parameters.AddWithValue("id", s.BatchId);
            var after = (string)(await command.ExecuteScalarAsync())!;
            Assert.Contains("\"Requested\":4", after); // the BusinessEvent row records the run and its count
        }

        var second = await JsonOkAsync(await s.Keeper.Client.PostAsync($"/api/staff/inventory/recalls/{s.BatchId}/notify", null));
        Assert.Equal(0, second.GetProperty("requested").GetInt32());
        Assert.Equal(4, second.GetProperty("alreadyNotified").GetInt32());
        Assert.Equal(4, (await RequestsAsync(s.BatchId)).Count);
    }

    [Fact]
    public async Task I_I4_AnAnonymisedCustomerIsNotNotified_AndANonRecalledBatchCannotBeNotified()
    {
        var s = await ConsumeAsync();
        await AnonymiseAsync(s.D);
        await RecallAsync(s.Keeper.Client, s.BatchId, s.Lot);

        var run = await JsonOkAsync(await s.Keeper.Client.PostAsync($"/api/staff/inventory/recalls/{s.BatchId}/notify", null));
        Assert.Equal(3, run.GetProperty("requested").GetInt32());
        Assert.DoesNotContain(await RequestsAsync(s.BatchId), r => r.RecipientId == s.D);

        var other = await ConsumeAsync();
        await AssertErrorAsync(await other.Keeper.Client.PostAsync($"/api/staff/inventory/recalls/{other.BatchId}/notify", null), BusinessViolationCodes.RecallNotActive);
    }

    /// <summary>An audit enlister whose entry reuses an existing audit row's id, so the single commit of the notify run fails at the database.</summary>
    private sealed class ConflictingAuditEnlister(CatEcommerce.Infrastructure.Persistence.CatEcommerceDbContext db, Guid existingAuditId)
        : CatEcommerce.Infrastructure.Inventory.EfInventoryAuditEnlister(db)
    {
        public override void Enlist(CatEcommerce.Domain.Aggregates.Audit.AuditEntryAggregate entry)
        {
            var clash = new CatEcommerce.Domain.Aggregates.Audit.AuditEntryAggregate(existingAuditId);
            clash.Record(new AuditEntryDto("Batch", Guid.NewGuid(), "RecallNotified", "RecallNotified", Seeder.UserId, DateTimeOffset.UtcNow, null, "{}", Seeder, Guid.NewGuid(),
                AuditCategory.BusinessEvent, null));
            base.Enlist(clash);
        }
    }

    [Fact]
    public async Task I_I4_AFailingAuditWriteRollsBackTheWholeNotifyRun_SoARetryStillFindsEveryoneWaiting()
    {
        var s = await ConsumeAsync();
        await RecallAsync(s.Keeper.Client, s.BatchId, s.Lot);

        Guid existingAuditId;
        await using (var connection = await OpenConnectionAsync())
        await using (var command = new NpgsqlCommand("SELECT \"Id\" FROM audit.audit_entry LIMIT 1", connection))
            existingAuditId = (Guid)(await command.ExecuteScalarAsync())!;

        using var broken = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<CatEcommerce.Domain.DomainServices.IInventoryAuditEnlister>(sp => new ConflictingAuditEnlister(
                sp.GetRequiredService<CatEcommerce.Infrastructure.Persistence.CatEcommerceDbContext>(), existingAuditId))));
        var client = CreateClientOn(broken);
        await ReauthenticateAsync(client, s.Keeper.Mobile, s.Keeper.Password);
        UseBranch(client, s.Branch.BranchId);

        var failed = await client.PostAsync($"/api/staff/inventory/recalls/{s.BatchId}/notify", null);

        Assert.False(failed.IsSuccessStatusCode);
        Assert.Empty(await RequestsAsync(s.BatchId)); // no notification request survived
        var detail = await JsonOkAsync(await s.Keeper.Client.GetAsync($"/api/staff/inventory/recalls/{s.BatchId}"));
        Assert.Equal(0, detail.GetProperty("recall").GetProperty("notifiedRecipientCount").GetInt32()); // the batch update rolled back too

        var retry = await JsonOkAsync(await s.Keeper.Client.PostAsync($"/api/staff/inventory/recalls/{s.BatchId}/notify", null));
        Assert.Equal(4, retry.GetProperty("requested").GetInt32());
    }

    // ── I-I6 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_I6_AWithdrawnRecallGoesToQuarantine_AndTheNoticeGuardNoLongerHoldsSoPendingNoticesAreCancelled()
    {
        var s = await ConsumeAsync();
        await RecallAsync(s.Keeper.Client, s.BatchId, s.Lot);
        await s.Keeper.Client.PostAsync($"/api/staff/inventory/recalls/{s.BatchId}/notify", null);

        async Task<bool> GuardAsync()
        {
            using var scope = Factory.Services.CreateScope();
            return await scope.ServiceProvider.GetServices<INotificationSubjectGuard>().Single(g => g.Kind == NotificationSubjectKind.BatchRecall)
                .IsStillValidAsync(s.B, s.BatchId, null, null);
        }

        Assert.True(await GuardAsync());

        var withdrawn = await s.Keeper.Client.PostAsJsonAsync($"/api/staff/inventory/recalls/{s.BatchId}/withdraw", new { reason = "Declared on the wrong lot." }, Json);

        Assert.Equal(HttpStatusCode.NoContent, withdrawn.StatusCode);
        Assert.Equal("Quarantined", (await JsonOkAsync(await s.Keeper.Client.GetAsync($"/api/staff/inventory/batches/{s.BatchId}"))).GetProperty("status").GetString());
        Assert.False(await GuardAsync());
        Assert.Equal("Withdrawn", (await JsonOkAsync(await s.Keeper.Client.GetAsync($"/api/staff/inventory/recalls?status=Withdrawn"))).GetProperty("items")
            .EnumerateArray().Single(i => i.GetProperty("batchId").GetGuid() == s.BatchId).GetProperty("status").GetString());
    }

    // ── I-I7: the race ──────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_I7_ARecallAndAReservationOnTheSameBatchAtTheSameTime_NeverLeaveAReservationCommittedAfterTheRecall()
    {
        var branch = await SeedBranchAsync();
        var keeper = await StaffAsync(branch, StaffRoles.Inventory);

        for (var round = 0; round < 20; round++)
        {
            var sku = await CreateSkuAsync(branch.Admin.Client);
            var lot = NewLot();
            var batchId = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 10m, lot: lot);
            var orderId = Guid.NewGuid();
            var gate = new TaskCompletionSource();

            var reserve = Task.Run(async () =>
            {
                await gate.Task;
                await using var port = OpenPort();
                var plan = await port.Ledger.ReserveAsync(Demand(branch.BranchId, sku.SkuId, 2m, locations: [branch.WarehouseId]), OrderReference(orderId), Customer(), default);
                if (!plan.IsSuccess) return plan.ViolationCode ?? "refused";
                try { await port.CommitAsync(); return "committed"; }
                catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException is PostgresException pg) { return $"{pg.SqlState}:{pg.MessageText}"; }
            });
            var recall = Task.Run(async () => { await gate.Task; return await RecallAsync(keeper.Client, batchId, lot); });

            gate.SetResult();
            var outcome = await reserve;
            Assert.Equal(HttpStatusCode.Created, (await recall).StatusCode);

            // either the reservation won (and is then on the trace as unshipped), or it was refused -- the plan found nothing usable (InsufficientStock) or the trigger refused the commit -- never a third thing
            Assert.True(outcome == "committed" || outcome == BusinessViolationCodes.BatchNotUsable || outcome == BusinessViolationCodes.InsufficientStock || outcome.Contains("no longer be used", StringComparison.Ordinal), $"round {round}: {outcome}");

            var reserved = (await PositionAsync(batchId, branch.WarehouseId)).Reserved;
            var trace = await JsonOkAsync(await keeper.Client.GetAsync($"/api/staff/inventory/recalls/{batchId}/trace"));
            Assert.Equal(outcome == "committed" ? 1 : 0, trace.GetProperty("unshippedDispatchBlocked").GetArrayLength());
            Assert.Equal(outcome == "committed" ? 2m : 0m, reserved);

            // once recalled, nothing more can be reserved
            await using var late = OpenPort();
            AssertPort(await late.Ledger.ReserveAsync(Demand(branch.BranchId, sku.SkuId, 1m, locations: [branch.WarehouseId]), OrderReference(), Customer(), default), BusinessViolationCodes.InsufficientStock);
        }
    }

    // ── I-I8 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_I8_OnlyRecallManagersMayRecallOrReadTheTrace()
    {
        var s = await ConsumeAsync();
        var nurse = await StaffAsync(s.Branch, StaffRoles.Nurse);
        await RecallAsync(s.Keeper.Client, s.BatchId, s.Lot);

        Assert.Equal(HttpStatusCode.Forbidden, (await RecallAsync(nurse.Client, s.BatchId, s.Lot)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await nurse.Client.GetAsync($"/api/staff/inventory/recalls/{s.BatchId}/trace")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await nurse.Client.GetAsync($"/api/staff/inventory/recalls/{s.BatchId}/trace.csv")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await nurse.Client.PostAsync($"/api/staff/inventory/recalls/{s.BatchId}/notify", null)).StatusCode);
    }

    [Fact]
    public async Task I_I8_TheTraceCsvIsAuditedAsRecallTraceExported_AndCarriesTheContactDetails()
    {
        var s = await ConsumeAsync();
        await RecallAsync(s.Keeper.Client, s.BatchId, s.Lot);

        var csv = await s.Keeper.Client.GetAsync($"/api/staff/inventory/recalls/{s.BatchId}/trace.csv");

        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.Equal("text/csv", csv.Content.Headers.ContentType!.MediaType);
        var text = await csv.Content.ReadAsStringAsync();
        Assert.Contains("Customer A Prime", text);
        Assert.Contains("Xena", text);

        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand("SELECT COUNT(*) FROM audit.audit_entry WHERE \"Action\" = 'RecallTraceExported' AND \"EntityId\" = @id", connection);
        command.Parameters.AddWithValue("id", s.BatchId);
        Assert.Equal(1L, (long)(await command.ExecuteScalarAsync())!);
    }

    // ── I-I9 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_I9_ARecallCannotBeClosedWhileStockIsHeld_UntilItIsWrittenOffAsRecallDisposal()
    {
        var branch = await SeedBranchAsync();
        var keeper = await StaffAsync(branch, StaffRoles.Inventory);
        var sku = await CreateSkuAsync(branch.Admin.Client);
        var lot = NewLot();
        var batchId = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 6m, lot: lot);
        await RecallAsync(keeper.Client, batchId, lot);

        await AssertErrorAsync(await keeper.Client.PostAsJsonAsync($"/api/staff/inventory/recalls/{batchId}/close", new { note = "All handled." }, Json), BusinessViolationCodes.RecallStockStillHeld);

        var disposal = await keeper.Client.PostAsJsonAsync("/api/staff/inventory/adjustments", new
        {
            locationId = branch.WarehouseId, reason = "RecallDisposal", note = "Destroyed under the recall.",
            lines = new[] { new { skuId = sku.SkuId, batchId, quantityDelta = -6m } }
        }, Json);
        Assert.Equal(HttpStatusCode.Created, disposal.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await keeper.Client.PostAsJsonAsync($"/api/staff/inventory/recalls/{batchId}/close", new { note = "All stock destroyed." }, Json)).StatusCode);
        var detail = await JsonOkAsync(await keeper.Client.GetAsync($"/api/staff/inventory/recalls/{batchId}"));
        Assert.Equal("Closed", detail.GetProperty("summary").GetProperty("status").GetString());
        Assert.Equal("Recalled", (await JsonOkAsync(await keeper.Client.GetAsync($"/api/staff/inventory/batches/{batchId}"))).GetProperty("status").GetString());
    }
}
