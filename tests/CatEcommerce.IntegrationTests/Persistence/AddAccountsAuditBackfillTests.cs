using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CatEcommerce.IntegrationTests.Persistence;

/// <summary>
/// HO-ACC-001 ARCHITECTURE gate (MEDIUM): the <c>AddAccounts</c> migration must backfill
/// pre-existing <c>audit.audit_entry</c> rows (written by Cat Profiles' own audit handlers before
/// this migration's <c>category</c> column existed) to a valid <see cref="CatEcommerce.Shared.Common.Enums.AuditCategory"/>
/// BEFORE the immutability triggers are installed -- once installed, no UPDATE is possible even
/// for that one-time fix. Runs against a real, disposable scratch database (not the shared
/// <c>catecommerce_dev</c>, which already has this migration applied) so the migration can be
/// exercised from the state immediately before it, exactly as the gate asked for.
/// </summary>
public sealed class AddAccountsAuditBackfillTests : IAsyncLifetime
{
    private const string AdminConnectionString =
        "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres";

    private readonly string _databaseName = $"catecommerce_migration_test_{Guid.NewGuid():N}";

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
    public async Task AddAccounts_BackfillsPreExistingAuditRows_ToBusinessEventAndIsRecordedTrue_BeforeTriggersAreInstalled()
    {
        var options = new DbContextOptionsBuilder<CatEcommerceDbContext>()
            .UseNpgsql(ScratchConnectionString)
            .Options;

        var preExistingAuditEntryId = Guid.NewGuid();

        await using (var context = new CatEcommerceDbContext(options))
        {
            var migrator = context.GetService<IMigrator>();

            // Migrate only as far as AddCatProfile -- audit.audit_entry exists, but none of
            // AddAccounts' four new columns (category, ip_address, correlation_id, is_recorded) do.
            await migrator.MigrateAsync("20260920110910_AddCatProfile");

            await using var connection = new NpgsqlConnection(ScratchConnectionString);
            await connection.OpenAsync();

            // Seeded exactly as Cat Profiles' own AuditCat*Handlers would have left a row before
            // this migration ever existed.
            await using (var insert = new NpgsqlCommand(
                """
                INSERT INTO audit.audit_entry
                    ("Id", "EntityType", "EntityId", "Action", "Reason", "ActorUserId", "OccurredOn",
                     "CreatedBy", "CreatedDate", "LastUpdatedBy", "LastUpdatedDate", "TenantId",
                     "VerticalId", "IsMarkedToDelete", "Version")
                VALUES
                    (@id, 'Cat', @id, 'FlaggedHighRisk', 'pre-existing row', @id, now(),
                     @id, now(), @id, now(), @id, @id, false, 1)
                """, connection))
            {
                insert.Parameters.AddWithValue("id", preExistingAuditEntryId);
                await insert.ExecuteNonQueryAsync();
            }

            // Now apply AddAccounts -- the backfill inside it must run before the trigger DDL.
            await migrator.MigrateAsync("20260924212111_AddAccounts");
        }

        await using var verifyConnection = new NpgsqlConnection(ScratchConnectionString);
        await verifyConnection.OpenAsync();

        await using (var select = new NpgsqlCommand(
            """SELECT category, is_recorded FROM audit.audit_entry WHERE "Id" = @id""", verifyConnection))
        {
            select.Parameters.AddWithValue("id", preExistingAuditEntryId);
            await using var reader = await select.ExecuteReaderAsync();
            await reader.ReadAsync();

            // AuditCategory.BusinessEvent (value 3) -- not 0, which Enumeration.FromValue<AuditCategory>
            // cannot materialise, and not DataChange/AuthenticationEvent, which this row is not.
            Assert.Equal(3, reader.GetInt32(0));

            // ARCHITECTURE gate, second pass, LOW: every row written since this migration is
            // `true`; a pre-existing row left at the column's `false` default is indistinguishable
            // from "not recorded" and can never be corrected once the triggers below exist.
            Assert.True(reader.GetBoolean(1));
        }

        // And the triggers really are active immediately afterwards -- the now-backfilled row is
        // already immutable in the same migration that backfilled it.
        await using var update = new NpgsqlCommand(
            """UPDATE audit.audit_entry SET "Reason" = 'changed' WHERE "Id" = @id""", verifyConnection);
        update.Parameters.AddWithValue("id", preExistingAuditEntryId);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => update.ExecuteNonQueryAsync());
        Assert.Contains("immutable", exception.MessageText, StringComparison.OrdinalIgnoreCase);
    }
}
