using CatEcommerce.Infrastructure.Fakes.Administration;

namespace CatEcommerce.Infrastructure.Tests.Fakes.Administration;

public sealed class InMemoryBranchCalendarTests
{
    [Fact]
    public async Task OpenIntervalsAsync_returns_no_open_intervals()
    {
        var sut = new InMemoryBranchCalendar();

        var intervals = await sut.OpenIntervalsAsync(
            Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)), CancellationToken.None);

        Assert.Empty(intervals);
    }
}
