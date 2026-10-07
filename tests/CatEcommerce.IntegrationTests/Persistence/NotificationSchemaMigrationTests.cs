using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CatEcommerce.IntegrationTests.Persistence;

/// <summary>
/// HO-NTF-001 §16, "migration apply from empty". <see cref="MigrationsOnAnEmptyDatabaseTests"/> only
/// asserts the `account`/`audit`/`clinic` schemas it was written for -- it never checks that the
/// squashed `AddNotificationsModule` migration produces the `notify` schema at all. TEST gate
/// addition, mirroring both that file's and <see cref="AddFilesMigrationDeletesLegacyCatPhotoRowsTests"/>'s
/// own scratch-database pattern.
/// </summary>
public sealed class NotificationSchemaMigrationTests : IAsyncLifetime
{
    private const string AdminConnectionString =
        "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres";

    private readonly string _databaseName = $"catecommerce_migration_notify_test_{Guid.NewGuid():N}";

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
        // See MigrationsOnAnEmptyDatabaseTests.DisposeAsync's own comment: clearing pools first
        // guarantees pg_terminate_backend below has an actual, still-open backend to terminate.
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
    public async Task EveryMigration_AppliedInOrderFromEmpty_ProducesTheFullNotifySchema()
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

        // §8.1's five notify-schema tables (three roots + two owned-collection tables).
        foreach (var table in new[]
        {
            "notification", "notification_request", "notification_template",
            "notification_attempt", "notification_template_content"
        })
        {
            Assert.True(await TableExistsAsync("notify", table), $"Expected table notify.{table} to exist after migrating from empty.");
        }

        // The gate-fix columns squashed into the one AddNotificationsModule migration (§1 Amended
        // (35), (42), (43), (25)) -- proves the squash did not silently drop a later round's column.
        Assert.True(await ColumnExistsAsync("notify", "notification", "resend_ordinal"));
        Assert.True(await ColumnExistsAsync("notify", "notification", "redacted_reason"));
        Assert.True(await ColumnExistsAsync("notify", "notification", "anonymised_on"));
        Assert.True(await ColumnExistsAsync("notify", "notification_request", "attempt_count"));
        Assert.True(await ColumnExistsAsync("notify", "notification_request", "last_attempt_on"));

        // The account-schema amendment (§12.1) this handover carries.
        Assert.True(await ColumnExistsAsync("account", "customer_notification_preferences", "preferred_channel"));

        // Unrelated schemas from earlier/later handovers must be unaffected.
        Assert.True(await TableExistsAsync("clinic", "cat"));
        Assert.True(await TableExistsAsync("account", "customer_account"));
    }
}
