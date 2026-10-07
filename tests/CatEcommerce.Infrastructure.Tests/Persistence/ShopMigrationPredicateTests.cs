using System.Text.RegularExpressions;
using CatEcommerce.Infrastructure.Persistence.Migrations;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace CatEcommerce.Infrastructure.Tests.Persistence;

/// <summary>
/// HO-SHP-001 §15 U-X5 -- status values in filtered-index predicates and in the view SQL are the
/// enumerations' own <c>Value</c>s, written from the enumeration in migration code, never typed as
/// literals. Proven two ways: the SQL the migrations actually emit carries the enumeration values,
/// and no hand-written migration or entity configuration contains a numeric status literal.
/// </summary>
public sealed partial class ShopMigrationPredicateTests
{
    private static string AllSql(Migration migration, bool up) =>
        string.Join("\n", (up ? migration.UpOperations : migration.DownOperations).OfType<SqlOperation>().Select(o => o.Sql));

    [Fact]
    public void U_X5_TheStorefrontViews_AreBuiltFromTheEnumerationValues()
    {
        var sql = AllSql(new AddStorefrontCatalogue(), up: true);

        Assert.Contains($"status = {ProductReviewStatus.Published.Value}", sql);   // the rating view
        Assert.Contains($"status = {ProductStatus.Active.Value}", sql);            // the catalogue views
        Assert.Contains($"s2.status = {SkuStatus.Active.Value}", sql);
    }

    [Fact]
    public void U_X5_TheBundleClause_IsBuiltFromTheEnumerationValues_AndDownRestoresTheEarlierView()
    {
        var migration = new AddBundlesAndRelated();
        var up = AllSql(migration, up: true);
        var down = AllSql(migration, up: false);

        Assert.Contains($"p.kind = {ProductKind.Bundle.Value}", up);
        Assert.Contains($"cs.status = {SkuStatus.Active.Value}", up);
        Assert.Contains($"cp.status = {ProductStatus.Active.Value}", up);
        Assert.Contains($"p.status = {ProductStatus.Active.Value}", up);

        Assert.DoesNotContain("bundle_component", down);
        Assert.Contains("CREATE VIEW product.storefront_catalogue_entry", down);
        Assert.Contains("DROP VIEW IF EXISTS product.storefront_catalogue_entry", down);
    }

    [Fact]
    public void U_X5_TheFilteredIndexPredicates_AreTheEnumerationValues()
    {
        var filters = new Migration[] { new AddProductCatalogue(), new AddProductReviews(), new AddWishlistAndStockAlerts(), new ProductFilterIndexesPerHandoverSection12() }
            .SelectMany(m => m.UpOperations.OfType<CreateIndexOperation>())
            .Where(i => i.Filter is not null)
            .ToDictionary(i => i.Name, i => i.Filter!);

        Assert.Equal("barcode IS NOT NULL", filters["ix_sku_barcode"]);
        Assert.Equal($"status <> {ProductReviewStatus.Withdrawn.Value}", filters["ix_product_review_active_per_customer_product"]);
        Assert.Equal($"status = {StockAlertStatus.Pending.Value}", filters["ix_stock_alert_pending_per_customer_sku"]);

        // SHP backlog row 44: HO-SHP-001 section 12.1's filtered (WHERE status = Active) filter indexes.
        foreach (var name in new[] { "ix_product_brand", "ix_product_country_of_origin", "ix_product_food_life_stage", "ix_product_food_protein_source" })
            Assert.Equal($"status = {ProductStatus.Active.Value}", filters[name]);
    }

    [Fact]
    public void U_X5_NoHandWrittenShopMigrationOrConfiguration_TypesANumericStatusLiteral()
    {
        var infrastructure = FindInfrastructureDirectory();
        var files = Directory.EnumerateFiles(Path.Combine(infrastructure, "Persistence", "Configurations"), "*.cs")
            .Concat(Directory.EnumerateFiles(Path.Combine(infrastructure, "Persistence", "Migrations"), "*.cs")
                // EF-generated migrations bake the configuration's interpolated HasFilter value into
                // their own text (`filter: "status = 1"`); only a migration with hand-written SQL can
                // introduce a literal of its own.
                .Where(f => !f.EndsWith(".Designer.cs") && !f.EndsWith("Snapshot.cs") && File.ReadAllText(f).Contains("migrationBuilder.Sql(")))
            .Where(f => File.ReadAllText(f).Contains("product", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(files);

        var offenders = files.Where(f => StatusLiteral().IsMatch(File.ReadAllText(f))).Select(Path.GetFileName).ToList();
        Assert.True(offenders.Count == 0, "A numeric status literal is typed in: " + string.Join(", ", offenders));
    }

    private static string FindInfrastructureDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Infrastructure", "CatEcommerce.Infrastructure");
            if (Directory.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate src/Infrastructure/CatEcommerce.Infrastructure from " + AppContext.BaseDirectory);
    }

    /// <summary><c>status = 1</c>, <c>status &lt;&gt; 5</c>, <c>kind = 2</c> typed as digits (a
    /// <c>{Interpolated}</c> value is not a digit run and does not match).</summary>
    [GeneratedRegex(@"\b(status|kind)\s*(=|<>|!=)\s*\d+")]
    private static partial Regex StatusLiteral();
}
