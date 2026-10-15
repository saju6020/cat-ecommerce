using CatEcommerce.Domain.DomainServices;

namespace CatEcommerce.Application.Tests.TestSupport;

public sealed class FakeCatRecordClock(DateOnly today) : ICatRecordClock
{
    public Task<DateOnly> LocalTodayAsync(CancellationToken ct) => Task.FromResult(today);
}
