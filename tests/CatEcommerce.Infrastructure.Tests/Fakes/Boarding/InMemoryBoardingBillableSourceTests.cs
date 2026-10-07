using CatEcommerce.Infrastructure.Fakes.Boarding;

namespace CatEcommerce.Infrastructure.Tests.Fakes.Boarding;

public sealed class InMemoryBoardingBillableSourceTests
{
    [Fact]
    public async Task GetUninvoicedLinesAsync_returns_no_lines()
    {
        var sut = new InMemoryBoardingBillableSource();

        var lines = await sut.GetUninvoicedLinesAsync(Guid.NewGuid(), upToNight: null, CancellationToken.None);

        Assert.Empty(lines);
    }
}
