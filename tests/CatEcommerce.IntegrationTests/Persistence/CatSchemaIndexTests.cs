using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CatEcommerce.IntegrationTests.Persistence;

/// <summary>
/// TEST gate (2026-09-26). HO-CAT-001 §16 I22 -- every migration, applied in order from an empty
/// database, must produce every §12.1 index, including the unique <c>vaccine.code</c>.
/// <c>MigrationsOnAnEmptyDatabaseTests</c> (HO-ACC-001) already proves the tables exist from empty;
/// it does not check the index set (ARCHITECTURE gate's own note, ho/CAT-001-gates: "I22 is partly
/// covered... not the §12.1 index set"). Same scratch-database pattern, dedicated to the Cat/Vaccine
/// indexes §12.1 actually names.
/// </summary>
public sealed class CatSchemaIndexTests : IAsyncLifetime
{
    private const string AdminConnectionString =
        "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres";

    private readonly string _databaseName = $"catecommerce_cat_index_test_{Guid.NewGuid():N}";

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
    public async Task EveryMigration_AppliedFromEmpty_ProducesEverySpecifiedCatAndVaccineIndex()
    {
        var options = new DbContextOptionsBuilder<CatEcommerceDbContext>()
            .UseNpgsql(ScratchConnectionString)
            .Options;

        await using (var context = new CatEcommerceDbContext(options))
        {
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync();
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

        // §8.1's clinic-schema tables.
        foreach (var table in new[]
        {
            "cat", "cat_photo", "cat_allergy", "cat_chronic_condition", "cat_medication",
            "cat_vaccination", "cat_parasite_treatment", "cat_weight_record", "vaccine"
        })
        {
            Assert.True(await TableExistsAsync("clinic", table), $"Expected table clinic.{table} to exist after migrating from empty.");
        }

        async Task<string> IndexDefsAsync(string schema, string table)
        {
            await using var command = new NpgsqlCommand(
                """SELECT string_agg(indexdef, ' ;; ') FROM pg_indexes WHERE schemaname = @schema AND tablename = @table""",
                connection);
            command.Parameters.AddWithValue("schema", schema);
            command.Parameters.AddWithValue("table", table);
            var result = await command.ExecuteScalarAsync();
            return result as string ?? "";
        }

        // §12.1's named indexes -- checked by which column(s) each covers, not by a guessed
        // EF-generated name, since EF's own naming convention is an implementation detail this
        // test should not have to hard-code.
        var catIndexes = await IndexDefsAsync("clinic", "cat");
        Assert.Contains("\"OwnerId\"", catIndexes);
        Assert.Contains("\"LifeStatus\"", catIndexes);

        var vaccinationIndexes = await IndexDefsAsync("clinic", "cat_vaccination");
        Assert.Contains("\"NextDueOn\"", vaccinationIndexes);

        var weightIndexes = await IndexDefsAsync("clinic", "cat_weight_record");
        Assert.Contains("\"CatId\"", weightIndexes);
        Assert.Contains("\"RecordedOn\"", weightIndexes);

        var vaccineIndexes = await IndexDefsAsync("clinic", "vaccine");
        Assert.Contains("\"IsCore\"", vaccineIndexes);
        Assert.Contains("\"Code\"", vaccineIndexes);
        // The vaccine.code index must specifically be UNIQUE (§12.1) -- not merely present.
        var codeIndexLine = vaccineIndexes.Split(" ;; ").Single(l => l.Contains("\"Code\""));
        Assert.Contains("CREATE UNIQUE INDEX", codeIndexLine);

        var auditIndexes = await IndexDefsAsync("audit", "audit_entry");
        Assert.Contains("\"EntityType\"", auditIndexes);
        Assert.Contains("\"EntityId\"", auditIndexes);
        Assert.Contains("\"OccurredOn\"", auditIndexes);
    }
}
