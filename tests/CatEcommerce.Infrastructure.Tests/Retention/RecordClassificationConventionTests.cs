using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CatEcommerce.Infrastructure.Tests.Retention;

/// <summary>
/// HO-XCT-001 U-A1 / ADR-298. "Never hard-deleted" has to be true for tables that do not exist yet,
/// so every table in the EF model -- owned types included, views excluded -- must have exactly one
/// <see cref="RecordClassification"/> entry, and every entry must name a table that exists.
/// </summary>
public sealed class RecordClassificationConventionTests
{
    /// <summary>The model is built without a connection: nothing here touches a database.</summary>
    internal static IReadOnlyModel BuildModel()
    {
        var options = new DbContextOptionsBuilder<CatEcommerceDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=model_only")
            .Options;
        using var db = new CatEcommerceDbContext(options);
        return ((IInfrastructure<IServiceProvider>)db).Instance.GetRequiredService<IDesignTimeModel>().Model;
    }

    internal static IReadOnlyList<(string Schema, string Table)> ModelTables()
    {
        return BuildModel().GetEntityTypes()
            .Where(entityType => entityType.GetTableName() is not null)
            .Select(entityType => (Schema: entityType.GetSchema() ?? "public", Table: entityType.GetTableName()!))
            .Where(table => table.Table != "__EFMigrationsHistory")
            .Distinct()
            .OrderBy(table => table.Schema, StringComparer.Ordinal)
            .ThenBy(table => table.Table, StringComparer.Ordinal)
            .ToList();
    }

    [Fact]
    public void U_A1_EveryTableInTheModelIsClassified_AndEveryEntryIsATable()
    {
        var model = ModelTables().ToHashSet();
        var classified = RecordClassification.All.Select(entry => (entry.Schema, entry.Table)).ToHashSet();

        var unclassified = model.Except(classified).OrderBy(t => t.Item1).ThenBy(t => t.Item2).Select(t => $"{t.Item1}.{t.Item2}").ToList();
        var stale = classified.Except(model).OrderBy(t => t.Item1).ThenBy(t => t.Item2).Select(t => $"{t.Item1}.{t.Item2}").ToList();

        Assert.True(unclassified.Count == 0, "Unclassified tables (add them to the owning area's RecordClassification.[Area].cs): " + string.Join(", ", unclassified));
        Assert.True(stale.Count == 0, "Classified but not in the EF model (stale or misspelt): " + string.Join(", ", stale));
    }

    [Fact]
    public void U_A1_NoTableIsClassifiedTwice()
    {
        var duplicates = RecordClassification.All
            .GroupBy(entry => (entry.Schema, entry.Table))
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key.Schema}.{group.Key.Table}")
            .ToList();

        Assert.Empty(duplicates);
    }
}
