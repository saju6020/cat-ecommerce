using CatEcommerce.Domain.Aggregates.Order;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Ordering;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.IntegrationTests;

/// <summary>HO-ORD-001 §12.1 / V1 / I-A1 -- the Ordering schema as the database enforces it: the order round-trips through EF, the number sequence only moves
/// forward and survives a rollback, and the financial tables refuse delete, update and truncate.</summary>
public sealed class OrderingPersistenceEndpointTests(OrderingApiFactory factory) : OrderingIntegrationTestBase(factory)
{
    private static readonly UserContext Staff = new(Guid.NewGuid(), Guid.NewGuid()) { Roles = ["Reception"] };

    private static OrderAggregate NewCounterOrder(Guid branchId, Guid skuId, string number)
    {
        var lines = new List<OrderLineDto>
        {
            new(Guid.NewGuid(), 1, skuId, Guid.NewGuid(), "Food", "Food", null, ProductReturnClass.Food, false, null, 2, 850m, 1700m, 0m, 0m, Guid.NewGuid(), 15m, 255m, 1955m, []),
        };
        var order = new OrderAggregate(Guid.NewGuid());
        order.Place(new PlaceOrderDto(
            number, OrderChannel.Counter, null, branchId, FulfilmentMethod.Counter, OrderPaymentMethod.InStore, "BDT",
            new FulfilmentDetails("Walk-in", "01712345678", "Counter", null, "Dhaka", "Dhaka", "1212", null, null, null, null, null, null, null, null, null),
            lines, new OrderTotals(1700m, 0m, 0m, 0m, 255m, 1955m), null, null, new string('a', 64), Guid.NewGuid(), DateTimeOffset.UtcNow, Staff, Guid.NewGuid()));
        return order;
    }

    private static async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(OrderingApiFactory.RequireOrderingDatabase());
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<T?> ScalarAsync<T>(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? default : (T)Convert.ChangeType(value, typeof(T));
    }

    private static async Task<string?> RefusalAsync(NpgsqlConnection connection, string sql)
    {
        try
        {
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
            return null;
        }
        catch (PostgresException ex)
        {
            return ex.SqlState;
        }
    }

    [Fact]
    public async Task I_A1_AnOrder_RoundTripsThroughTheDatabase_WithItsLinesAndHistory()
    {
        var context = await CreateContextAsync();
        var sku = await CreateSkuAsync(context, stock: 3m);
        var order = NewCounterOrder(context.BranchId, sku.SkuId, "ORD-RT-" + Guid.NewGuid().ToString("N")[..8]);

        await WithDbAsync(async db => { db.Add(order); await db.SaveChangesAsync(); return 0; });

        var loaded = await WithDbAsync(db => db.Set<OrderAggregate>().AsNoTracking()
            .Where(o => o.Id == order.Id)
            .Select(o => new { o.OrderNumber, o.Status, o.Totals.GrandTotal, Lines = o.Lines.Count, History = o.StatusHistory.Count, o.PlacedOn })
            .SingleAsync());

        Assert.Equal(order.OrderNumber, loaded.OrderNumber);
        Assert.Equal(order.Status, loaded.Status);
        Assert.Equal(1955m, loaded.GrandTotal);
        Assert.Equal(1, loaded.Lines);
        Assert.True(loaded.History >= 1);
    }

    [Fact]
    public async Task V1_TheOrderNumberSequence_OnlyMovesForward_AndARolledBackNumberIsNotReused()
    {
        await using var connection = await OpenAsync();

        var first = await ScalarAsync<long>(connection, "SELECT nextval('ordering.order_number_seq')");

        await using (var transaction = await connection.BeginTransactionAsync())
        {
            var inside = await ScalarAsync<long>(connection, "SELECT nextval('ordering.order_number_seq')");
            Assert.True(inside > first);
            await transaction.RollbackAsync();
        }

        var after = await ScalarAsync<long>(connection, "SELECT nextval('ordering.order_number_seq')");
        Assert.True(after > first + 1, "a rolled-back number leaves a gap; it is never handed out again");
    }

    [Fact]
    public async Task V1_EveryFinancialTable_HasItsDeleteTruncateAndUpdateGuards_InThePgCatalogue()
    {
        await using var connection = await OpenAsync();

        foreach (var table in OrderingMigrationSql.NoDeleteTables)
        {
            Assert.Equal(1, await ScalarAsync<int>(connection, $"SELECT count(*) FROM pg_trigger WHERE tgrelid = 'ordering.{table}'::regclass AND tgname = 'trg_{table}_no_delete'"));
            Assert.Equal(1, await ScalarAsync<int>(connection, $"SELECT count(*) FROM pg_trigger WHERE tgrelid = 'ordering.{table}'::regclass AND tgname = 'trg_{table}_no_truncate' AND (tgtype & 32) = 32"));
        }

        foreach (var table in OrderingMigrationSql.AppendOnlyTables)
            Assert.Equal(1, await ScalarAsync<int>(connection, $"SELECT count(*) FROM pg_trigger WHERE tgrelid = 'ordering.{table}'::regclass AND tgname = 'trg_{table}_no_update'"));
    }

    [Fact]
    public async Task V1_DeleteAndUpdate_OfTheAppendOnlyHistory_AreRefusedByTheDatabase()
    {
        var context = await CreateContextAsync();
        var sku = await CreateSkuAsync(context, stock: 3m);
        var order = NewCounterOrder(context.BranchId, sku.SkuId, "ORD-TR-" + Guid.NewGuid().ToString("N")[..8]);
        await WithDbAsync(async db => { db.Add(order); await db.SaveChangesAsync(); return 0; });

        await using var connection = await OpenAsync();
        Assert.Equal("P0001", await RefusalAsync(connection, $"DELETE FROM ordering.sales_order WHERE \"Id\" = '{order.Id}'"));
        Assert.Equal("P0001", await RefusalAsync(connection, $"DELETE FROM ordering.order_status_change WHERE order_id = '{order.Id}'"));
        Assert.Equal("P0001", await RefusalAsync(connection, $"UPDATE ordering.order_status_change SET note = note WHERE order_id = '{order.Id}'"));
    }
}
