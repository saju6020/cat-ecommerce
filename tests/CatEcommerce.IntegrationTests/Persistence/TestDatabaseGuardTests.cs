namespace CatEcommerce.IntegrationTests.Persistence;

/// <summary>Issues.md #2: no integration test may ever reach catecommerce_dev, the running app's database.</summary>
public sealed class TestDatabaseGuardTests
{
    [Theory]
    [InlineData("Host=localhost;Port=5432;Database=catecommerce_dev;Username=postgres;Password=postgres")]
    [InlineData("Host=localhost;Database=CATECOMMERCE_DEV;Username=postgres")]
    public void TheDevDatabase_IsRefused(string configured) =>
        Assert.Throws<InvalidOperationException>(() => TestDatabase.Resolve(configured));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NothingConfigured_UsesTheSharedTestDatabase(string? configured) =>
        Assert.Contains("Database=catecommerce_test", TestDatabase.Resolve(configured));

    [Fact]
    public void ABranchDatabase_IsUsedAsGiven()
    {
        const string branch = "Host=localhost;Database=catecommerce_brd;Username=postgres;Password=postgres";
        Assert.Equal(branch, TestDatabase.Resolve(branch));
    }

    [Fact]
    public void TheResolvedConnection_IsNeverTheDevDatabase() =>
        Assert.DoesNotContain(TestDatabase.DevDatabaseName, TestDatabase.ConnectionString, StringComparison.OrdinalIgnoreCase);
}
