using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Files;

namespace CatEcommerce.Infrastructure.Tests.Files;

public sealed class EicarTestScannerTests
{
    [Fact]
    public void Constructor_throws_outside_Testing()
    {
        Assert.Throws<InvalidOperationException>(() => new EicarTestScanner(new FakeHostEnvironment("Development")));
        Assert.Throws<InvalidOperationException>(() => new EicarTestScanner(new FakeHostEnvironment("Production")));
    }

    // I2's malware-rejection scenario, at the scanner level.
    [Fact]
    public async Task ScanAsync_reports_Infected_for_the_eicar_string()
    {
        var sut = new EicarTestScanner(new FakeHostEnvironment("Testing"));
        var content = System.Text.Encoding.ASCII.GetBytes(EicarTestScanner.EicarSignature);

        var result = await sut.ScanAsync(new MemoryStream(content), CancellationToken.None);

        Assert.Equal(ScanOutcome.Infected, result.Outcome);
        Assert.NotNull(result.ScannerName);
    }

    [Fact]
    public async Task ScanAsync_reports_Clean_for_ordinary_content()
    {
        var sut = new EicarTestScanner(new FakeHostEnvironment("Testing"));

        var result = await sut.ScanAsync(new MemoryStream("just a perfectly ordinary file"u8.ToArray()), CancellationToken.None);

        Assert.Equal(ScanOutcome.Clean, result.Outcome);
    }

    // Gate-fix 2026-09-27 (SECURITY LOW): the whole stream is scanned, not only the first 4096
    // bytes -- this places the signature well past the old sample window.
    [Fact]
    public async Task ScanAsync_finds_the_eicar_string_placed_after_five_thousand_bytes_of_leading_text()
    {
        var sut = new EicarTestScanner(new FakeHostEnvironment("Testing"));
        var leading = new string('a', 5000);
        var content = System.Text.Encoding.ASCII.GetBytes(leading + EicarTestScanner.EicarSignature);

        var result = await sut.ScanAsync(new MemoryStream(content), CancellationToken.None);

        Assert.Equal(ScanOutcome.Infected, result.Outcome);
    }

    [Fact]
    public async Task ScanAsync_finds_the_eicar_string_split_across_a_chunk_boundary()
    {
        var sut = new EicarTestScanner(new FakeHostEnvironment("Testing"));
        // 81920 is the scanner's own chunk size -- place the signature straddling that boundary.
        var leading = new string('a', 81920 - 10);
        var content = System.Text.Encoding.ASCII.GetBytes(leading + EicarTestScanner.EicarSignature);

        var result = await sut.ScanAsync(new MemoryStream(content), CancellationToken.None);

        Assert.Equal(ScanOutcome.Infected, result.Outcome);
    }
}
