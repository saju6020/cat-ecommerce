using System.Reflection;
using System.Text.RegularExpressions;
using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Infrastructure.Tests.Persistence;

/// <summary>
/// HO-IMP-001 ADR-012 -- a read handler never materialises an aggregate. A statement over an aggregate DbSet that ends in <c>ToListAsync</c>, <c>ToDictionaryAsync</c>,
/// <c>FirstOrDefaultAsync</c>, <c>SingleOrDefaultAsync</c> or <c>ToArrayAsync</c> must project with <c>.Select(...)</c> (or aggregate with <c>GroupBy</c>) first:
/// <c>ToDictionaryAsync(k =&gt; k.Id, v =&gt; v.X)</c> straight on the set takes a Func, so the whole entity and its owned rows load and the projection runs in memory.
/// </summary>
public sealed class InventoryReadSideConventionTests
{
    private static readonly Regex Materialiser = new(@"\.(ToListAsync|ToDictionaryAsync|FirstOrDefaultAsync|SingleOrDefaultAsync|ToArrayAsync|FirstAsync|SingleAsync)\(", RegexOptions.Compiled);

    private static string HandlersDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "Read"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", "Read", "CatEcommerce.Read", "QueryHandlers", "Inventory");
    }

    [Fact]
    public void ReadHandlersNeverMaterialiseAnAggregateWithoutAProjection()
    {
        var aggregateSets = typeof(CatEcommerceDbContext).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>) && typeof(AggregateRoot).IsAssignableFrom(p.PropertyType.GetGenericArguments()[0]))
            .Select(p => p.Name).ToList();
        Assert.Contains("Batches", aggregateSets);

        var setUse = new Regex(@"\bdb\.(" + string.Join("|", aggregateSets) + @")\b");
        var offenders = new List<string>();

        foreach (var file in Directory.GetFiles(HandlersDirectory(), "*.cs"))
        {
            foreach (var statement in File.ReadAllText(file).Split(';'))
            {
                if (!setUse.IsMatch(statement) || !Materialiser.IsMatch(statement)) continue;
                if (statement.Contains(".Select(") || statement.Contains(".GroupBy(") || Regex.IsMatch(statement, @"\bselect\s")) continue;

                offenders.Add($"{Path.GetFileName(file)}: {statement.Trim().Replace("\r", "").Replace("\n", " ")[..Math.Min(140, statement.Trim().Length)]}");
            }
        }

        Assert.True(offenders.Count == 0, "Aggregate materialised without a Select:\n" + string.Join("\n", offenders));
    }
}
