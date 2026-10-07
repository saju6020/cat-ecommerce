using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Files;

namespace CatEcommerce.Infrastructure.Tests.Files;

/// <summary>U9.</summary>
public sealed class DevelopmentNoOpScannerTests
{
    [Fact]
    public void Constructor_throws_outside_Development()
    {
        Assert.Throws<InvalidOperationException>(() => new DevelopmentNoOpScanner(new FakeHostEnvironment("Testing")));
        Assert.Throws<InvalidOperationException>(() => new DevelopmentNoOpScanner(new FakeHostEnvironment("Production")));
        Assert.Throws<InvalidOperationException>(() => new DevelopmentNoOpScanner(new FakeHostEnvironment("Staging")));
    }

    [Fact]
    public async Task ScanAsync_always_reports_Clean_in_Development()
    {
        var sut = new DevelopmentNoOpScanner(new FakeHostEnvironment("Development"));

        var result = await sut.ScanAsync(new MemoryStream("hello"u8.ToArray()), CancellationToken.None);

        Assert.Equal(ScanOutcome.Clean, result.Outcome);
    }
}
