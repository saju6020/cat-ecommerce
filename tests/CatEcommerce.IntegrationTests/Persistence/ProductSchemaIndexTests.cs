using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CatEcommerce.IntegrationTests.Persistence;

/// <summary>
/// SHP backlog row 44 -- HO-SHP-001 §12.1: the storefront filter indexes cover Active products only,
/// food life-stage/protein have indexes, and the "newest" index is DESC. Applies every migration to a
/// scratch database (same pattern as <c>CatSchemaIndexTests</c>) and reads <c>pg_indexes</c>.
/// </summary>
public sealed class ProductSchemaIndexTests : IAsyncLifetime
{
    private const string AdminConnectionString =
        "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres";

    private readonly string _databaseName = $"catecommerce_product_index_test_{Guid.NewGuid():N}";

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
    public async Task EveryMigration_AppliedFromEmpty_ProducesTheSpecifiedProductFilterIndexes()
    {
        var options = new DbContextOptionsBuilder<CatEcommerceDbContext>().UseNpgsql(ScratchConnectionString).Options;
        await using (var context = new CatEcommerceDbContext(options))
            await context.GetService<IMigrator>().MigrateAsync();

        await using var connection = new NpgsqlConnection(ScratchConnectionString);
        await connection.OpenAsync();

        async Task<string> IndexDefAsync(string name)
        {
            await using var command = new NpgsqlCommand("SELECT indexdef FROM pg_indexes WHERE indexname = @name", connection);
            command.Parameters.AddWithValue("name", name);
            return await command.ExecuteScalarAsync() as string ?? "";
        }

        foreach (var name in new[] { "ix_product_brand", "ix_product_country_of_origin", "ix_product_food_life_stage", "ix_product_food_protein_source" })
        {
            var def = await IndexDefAsync(name);
            Assert.True(def.Length > 0, $"Expected index {name}.");
            Assert.Contains("WHERE", def);
            Assert.Contains("status", def);
        }

        Assert.Contains("food_life_stage", await IndexDefAsync("ix_product_food_life_stage"));
        Assert.Contains("food_protein_source", await IndexDefAsync("ix_product_food_protein_source"));

        var newest = await IndexDefAsync("ix_product_first_published_on_desc");
        Assert.Contains("first_published_on DESC", newest);
    }
}
