using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Application.Tests.TestSupport;

/// <summary>An <see cref="IOperationsClock"/> that stands still at a given instant, in a given zone.</summary>
public sealed class FakeOperationsClock(DateTimeOffset utc, string zoneId = "Asia/Dhaka") : IOperationsClock
{
    public DateTimeOffset Utc { get; set; } = utc;

    public Task<MainBranchTime> NowAsync(CancellationToken ct)
    {
        var local = TimeZoneInfo.ConvertTime(Utc, TimeZoneInfo.FindSystemTimeZoneById(zoneId));
        return Task.FromResult(new MainBranchTime(Utc, zoneId, DateOnly.FromDateTime(local.DateTime), local.Hour));
    }

    public Task<DateOnly> LocalDateOfAsync(DateTimeOffset instant, CancellationToken ct) =>
        Task.FromResult(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById(zoneId)).DateTime));
}

/// <summary>A scripted <see cref="IPersonalDataEraser"/>: returns a fixed outcome, or throws.</summary>
public sealed class FakeEraser(string contextKey, ErasureOutcome? outcome = null, Exception? toThrow = null) : IPersonalDataEraser
{
    public string ContextKey => contextKey;
    public int Calls { get; private set; }

    public Task<ErasureOutcome> EraseAsync(Guid customerAccountId, DateTimeOffset asOf, UserContext actor, CancellationToken ct)
    {
        Calls++;
        if (toThrow is not null) throw toThrow;
        return Task.FromResult(outcome ?? new ErasureOutcome.Completed());
    }
}
