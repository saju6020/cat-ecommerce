using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CatEcommerce.IntegrationTests.Persistence;

/// <summary>
/// TEST gate, R6: proves the `AddFiles` migration's leading <c>DELETE FROM clinic.cat_photo;</c>
/// actually runs against pre-existing rows written under the pre-HO-FIL-001 shape (a raw
/// <c>StorageKey</c>/<c>ContentType</c>/<c>SizeInBytes</c>/<c>FileName</c> column set, dropped by
/// this same migration) before the schema change, and that the migration then applies cleanly --
/// rather than either failing outright (the unique index on the new <c>FileId</c> column cannot be
/// built over two rows that would otherwise both default to <c>Guid.Empty</c>) or silently leaving
/// a corrupted row behind. Runs against a real, disposable scratch database, exactly like
/// <see cref="MigrationsOnAnEmptyDatabaseTests"/>'s own pattern.
/// </summary>
public sealed class AddFilesMigrationDeletesLegacyCatPhotoRowsTests : IAsyncLifetime
{
    private const string AdminConnectionString =
        "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres";

    private readonly string _databaseName = $"catecommerce_migration_addfiles_test_{Guid.NewGuid():N}";

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
    public async Task AddFiles_DeletesPreExistingCatPhotoRows_ThenAppliesCleanly()
    {
        var options = new DbContextOptionsBuilder<CatEcommerceDbContext>()
            .UseNpgsql(ScratchConnectionString)
            .Options;

        await using (var context = new CatEcommerceDbContext(options))
        {
            var migrator = context.GetService<IMigrator>();

            // Migrate up to, but not including, AddFiles -- resolved by name rather than hardcoding
            // its own timestamped id, since that id is regenerated whenever AddFiles itself is
            // (already happened twice in this handover's own history).
            var migrationIds = context.Database.GetMigrations().ToList();
            var widenMigrationId = migrationIds.Single(id => id.Contains("WidenAuditEntryValues"));
            await migrator.MigrateAsync(widenMigrationId);

            // A minimal, valid clinic.cat row (every NOT NULL column satisfied) to hang two
            // pre-HO-FIL-001-shaped cat_photo rows off of -- the exact shape AddFiles' own DropColumn
            // calls remove (StorageKey/ContentType/SizeInBytes/FileName), with no FileId column at
            // all yet.
            var catId = Guid.NewGuid();
            var systemId = Guid.NewGuid();

            await using (var connection = new NpgsqlConnection(ScratchConnectionString))
            {
                await connection.OpenAsync();

                await using (var insertCat = new NpgsqlCommand(
                    """
                    INSERT INTO clinic.cat (
                        "Id", "OwnerId", "Name", "Species", "Sex", "DateOfBirthPrecision", "NeuterStatus",
                        "IsHighRisk", "LifeStatus", "CreatedBy", "CreatedDate", "LastUpdatedDate",
                        "LastUpdatedBy", "TenantId", "VerticalId", "IsMarkedToDelete", "Version"
                    ) VALUES (
                        @catId, @systemId, 'Legacy Test Cat', 'Cat', 0, 0, 0,
                        false, 0, @systemId, now(), now(),
                        @systemId, @systemId, @systemId, false, 1
                    )
                    """, connection))
                {
                    insertCat.Parameters.AddWithValue("catId", catId);
                    insertCat.Parameters.AddWithValue("systemId", systemId);
                    await insertCat.ExecuteNonQueryAsync();
                }

                await using (var insertPhotos = new NpgsqlCommand(
                    """
                    INSERT INTO clinic.cat_photo ("Id", "StorageKey", "ContentType", "SizeInBytes", "FileName", "IsPrimary", "UploadedOn", "cat_id")
                    VALUES
                        (@photo1, 'CatProfilePhoto/legacy/photo-1.jpg', 'image/jpeg', 1024, 'photo-1.jpg', true, now(), @catId),
                        (@photo2, 'CatProfilePhoto/legacy/photo-2.jpg', 'image/jpeg', 2048, 'photo-2.jpg', false, now(), @catId)
                    """, connection))
                {
                    insertPhotos.Parameters.AddWithValue("photo1", Guid.NewGuid());
                    insertPhotos.Parameters.AddWithValue("photo2", Guid.NewGuid());
                    insertPhotos.Parameters.AddWithValue("catId", catId);
                    await insertPhotos.ExecuteNonQueryAsync();
                }

                await using (var countBefore = new NpgsqlCommand("""SELECT COUNT(*) FROM clinic.cat_photo""", connection))
                {
                    Assert.Equal(2L, (long)(await countBefore.ExecuteScalarAsync())!);
                }
            }

            // Migrate the rest of the way, through AddFiles -- must not throw (the unique index on
            // the new FileId column would fail to build over two rows both defaulting to
            // Guid.Empty, were the rows not deleted first) and must complete.
            await migrator.MigrateAsync();
        }

        await using var assertConnection = new NpgsqlConnection(ScratchConnectionString);
        await assertConnection.OpenAsync();

        await using (var countAfter = new NpgsqlCommand("""SELECT COUNT(*) FROM clinic.cat_photo""", assertConnection))
        {
            Assert.Equal(0L, (long)(await countAfter.ExecuteScalarAsync())!);
        }

        // The new schema is in place: FileId exists, the old columns are gone, the unique index
        // exists (and, since the table is now empty, building it cannot have been avoided merely by
        // there being nothing to violate it with row-uniqueness-wise for these two rows specifically).
        async Task<bool> ColumnExistsAsync(string column)
        {
            await using var command = new NpgsqlCommand(
                """SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'clinic' AND table_name = 'cat_photo' AND column_name = @column)""",
                assertConnection);
            command.Parameters.AddWithValue("column", column);
            return (bool)(await command.ExecuteScalarAsync())!;
        }

        Assert.True(await ColumnExistsAsync("FileId"));
        Assert.False(await ColumnExistsAsync("StorageKey"));
        Assert.False(await ColumnExistsAsync("ContentType"));
        Assert.False(await ColumnExistsAsync("SizeInBytes"));
        Assert.False(await ColumnExistsAsync("FileName"));

        await using (var indexCommand = new NpgsqlCommand(
            """SELECT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'clinic' AND tablename = 'cat_photo' AND indexname = 'ix_cat_photo_file_id')""",
            assertConnection))
        {
            Assert.True((bool)(await indexCommand.ExecuteScalarAsync())!);
        }
    }
}
