using Npgsql;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// The one place an integration test learns which database to use. <c>CATECOMMERCE_TEST_DB</c> wins; otherwise the shared
/// <c>catecommerce_test</c>. <b><c>catecommerce_dev</c> is refused outright</b>, whatever the variable says: it is the database
/// the running app shows, and test rows left there are what broke the shop's banners, brand filter, policy prompt and
/// checkout prices (docs/backlog/Issues.md #2, #3, #7, #9). Area factories that also refuse <c>catecommerce_test</c> keep
/// their own stricter checks on top of this.
/// </summary>
public static class TestDatabase
{
    public const string DevDatabaseName = "catecommerce_dev";

    private const string SharedTestDatabase =
        "Host=localhost;Port=5432;Database=catecommerce_test;Username=postgres;Password=postgres";

    public static string ConnectionString { get; } = Resolve(Environment.GetEnvironmentVariable("CATECOMMERCE_TEST_DB"));

    /// <summary>Public for its own test: the configured value, or the shared test database, never the dev one.</summary>
    public static string Resolve(string? configured)
    {
        var connection = string.IsNullOrWhiteSpace(configured) ? SharedTestDatabase : configured;
        var database = new NpgsqlConnectionStringBuilder(connection).Database;
        if (string.Equals(database, DevDatabaseName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Integration tests must never run against {DevDatabaseName} (the running app's database). " +
                "Point CATECOMMERCE_TEST_DB at catecommerce_test or this branch's own database.");
        return connection;
    }
}
