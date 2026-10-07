using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CatEcommerce.IntegrationTests.Persistence;

/// <summary>
/// HO-ACC-001 §16, I-A3: every migration applied in order, from empty, produces the full `account`
/// schema and the audit-trail additions -- not previously exercised by any committed test (the
/// shared `catecommerce_dev` already has every migration applied, so nothing in the rest of this
/// suite runs the migrations themselves). Runs against a real, disposable scratch database, exactly
/// like <see cref="AddAccountsAuditBackfillTests"/>'s own pattern.
/// </summary>
public sealed class MigrationsOnAnEmptyDatabaseTests : IAsyncLifetime
{
    private const string AdminConnectionString =
        "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres";

    private readonly string _databaseName = $"catecommerce_migration_schema_test_{Guid.NewGuid():N}";

    private string ScratchConnectionString =>
        $"Host=localhost;Port=5432;Database={_databaseName};Username=postgres;Password=postgres";

    public async Task InitializeAsync()
    {
        await using var admin = new NpgsqlConnection(AdminConnectionString);
        await admin.OpenAsync();
        await using var create = new NpgsqlCommand($"""CREATE DATABASE "{_databaseName}" """, admin);
        await create.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        // Unlike AddAccountsAuditBackfillTests, this test's own EF DbContext AND its separate raw
        // Npgsql connection for the information_schema checks both return to Npgsql's client-side
        // connection pool on disposal rather than being physically closed immediately -- clearing
        // the pool first guarantees pg_terminate_backend below has an actual, still-open backend
        // for THIS database to terminate, rather than racing a pooled connection's own idle timeout.
        NpgsqlConnection.ClearAllPools();

        await using var admin = new NpgsqlConnection(AdminConnectionString);
        await admin.OpenAsync();

        await using (var terminate = new NpgsqlCommand(
            """SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @name AND pid <> pg_backend_pid()""", admin))
        {
            terminate.Parameters.AddWithValue("name", _databaseName);
            await terminate.ExecuteNonQueryAsync();
        }

        await using var drop = new NpgsqlCommand($"""DROP DATABASE IF EXISTS "{_databaseName}" """, admin);
        await drop.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task EveryMigration_AppliedInOrderFromEmpty_ProducesTheFullAccountSchemaAndAuditAdditions()
    {
        var options = new DbContextOptionsBuilder<CatEcommerceDbContext>()
            .UseNpgsql(ScratchConnectionString)
            .Options;

        await using (var context = new CatEcommerceDbContext(options))
        {
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync(); // every migration, in order, from a genuinely empty database
        }

        await using var connection = new NpgsqlConnection(ScratchConnectionString);
        await connection.OpenAsync();

        async Task<bool> TableExistsAsync(string schema, string table)
        {
            await using var command = new NpgsqlCommand(
                """SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = @schema AND table_name = @table)""",
                connection);
            command.Parameters.AddWithValue("schema", schema);
            command.Parameters.AddWithValue("table", table);
            return (bool)(await command.ExecuteScalarAsync())!;
        }

        async Task<bool> ColumnExistsAsync(string schema, string table, string column)
        {
            await using var command = new NpgsqlCommand(
                """SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = @schema AND table_name = @table AND column_name = @column)""",
                connection);
            command.Parameters.AddWithValue("schema", schema);
            command.Parameters.AddWithValue("table", table);
            command.Parameters.AddWithValue("column", column);
            return (bool)(await command.ExecuteScalarAsync())!;
        }

        // §8.1's seven account-schema tables.
        foreach (var table in new[]
        {
            "customer_account", "customer_session", "password_reset_token", "customer_address_book",
            "customer_delivery_address", "customer_notification_preferences", "customer_marketing_consent",
            "staff_account", "staff_session"
        })
        {
            Assert.True(await TableExistsAsync("account", table), $"Expected table account.{table} to exist after migrating from empty.");
        }

        // §12.1's audit-trail additions on the table Cat Profiles shipped first.
        foreach (var column in new[] { "category", "ip_address", "correlation_id", "is_recorded" })
        {
            Assert.True(await ColumnExistsAsync("audit", "audit_entry", column),
                $"Expected audit.audit_entry.{column} to exist after migrating from empty.");
        }

        // clinic.cat (HO-CAT-001) must be unaffected -- still present, unrelated to this schema.
        Assert.True(await TableExistsAsync("clinic", "cat"));

        // The two immutability triggers (INSERT-only enforcement) named in §12.1.
        await using var triggerCommand = new NpgsqlCommand(
            """SELECT COUNT(*) FROM pg_trigger WHERE tgrelid = 'audit.audit_entry'::regclass AND NOT tgisinternal""",
            connection);
        var triggerCount = (long)(await triggerCommand.ExecuteScalarAsync())!;
        Assert.True(triggerCount >= 2, $"Expected at least 2 immutability triggers on audit.audit_entry, found {triggerCount}.");
    }
}
