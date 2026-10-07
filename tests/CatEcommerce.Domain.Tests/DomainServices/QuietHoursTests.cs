using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.ValueObjects;
using Xunit;

namespace CatEcommerce.Domain.Tests.DomainServices;

/// <summary>HO-NTF-001 §15 U-C1..U-C3. Pure -- no database.</summary>
public sealed class QuietHoursTests
{
    private static readonly TimeZoneInfo Dhaka = TimeZoneInfo.FindSystemTimeZoneById("Asia/Dhaka");
    private static readonly TimeRange Wrapping = new(new TimeOnly(21, 0), new TimeOnly(8, 0));

    private static DateTimeOffset DhakaInstant(int year, int month, int day, int hour, int minute) =>
        new DateTimeOffset(new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified), Dhaka.GetUtcOffset(new DateTime(year, month, day, hour, minute, 0)));

    [Fact]
    public void JustBeforeWindow_ReturnsNull()
    {
        var result = QuietHours.NextAllowedInstant(DhakaInstant(2026, 10, 12, 20, 59), Wrapping, Dhaka);
        Assert.Null(result);
    }

    [Fact]
    public void AtWindowStart_ReturnsNextMorning8am()
    {
        var now = DhakaInstant(2026, 10, 12, 21, 0);
        var result = QuietHours.NextAllowedInstant(now, Wrapping, Dhaka);

        Assert.NotNull(result);
        var local = TimeZoneInfo.ConvertTime(result!.Value, Dhaka);
        Assert.Equal(new DateTime(2026, 10, 13, 8, 0, 0), local.DateTime);
    }

    [Fact]
    public void PastMidnight_ReturnsSameMorning8am()
    {
        var now = DhakaInstant(2026, 10, 13, 2, 0);
        var result = QuietHours.NextAllowedInstant(now, Wrapping, Dhaka);

        Assert.NotNull(result);
        var local = TimeZoneInfo.ConvertTime(result!.Value, Dhaka);
        Assert.Equal(new DateTime(2026, 10, 13, 8, 0, 0), local.DateTime);
    }

    [Fact]
    public void AtWindowEnd_ReturnsNull()
    {
        var result = QuietHours.NextAllowedInstant(DhakaInstant(2026, 10, 13, 8, 0), Wrapping, Dhaka);
        Assert.Null(result);
    }

    [Fact]
    public void NonWrappingRange_WithinWindow_ReturnsWindowEndSameDay()
    {
        var nonWrapping = new TimeRange(new TimeOnly(13, 0), new TimeOnly(14, 0));
        var now = DhakaInstant(2026, 10, 12, 13, 30);

        var result = QuietHours.NextAllowedInstant(now, nonWrapping, Dhaka);

        Assert.NotNull(result);
        var local = TimeZoneInfo.ConvertTime(result!.Value, Dhaka);
        Assert.Equal(new DateTime(2026, 10, 12, 14, 0, 0), local.DateTime);
    }

    [Fact]
    public void LondonBranch_AcrossDstChange_WindowEndCorrectInUtc()
    {
        var london = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        // 29 Mar 2026 clocks go forward at 01:00 -> 02:00 BST. 21:00 local on 28 Mar is GMT (+00:00);
        // the window end (08:00 on 29 Mar) is already BST (+01:00).
        var localNow = new DateTime(2026, 3, 28, 22, 0, 0, DateTimeKind.Unspecified);
        var now = new DateTimeOffset(localNow, london.GetUtcOffset(localNow));

        var result = QuietHours.NextAllowedInstant(now, Wrapping, london);

        Assert.NotNull(result);
        // Normalised to UTC (Offset=0) so it can be written to a `timestamptz` column via Npgsql,
        // which refuses a non-zero-offset DateTimeOffset outright -- the underlying instant is
        // still correct, which the local-time conversion below (across the DST change) proves.
        Assert.Equal(TimeSpan.Zero, result!.Value.Offset);
        var local = TimeZoneInfo.ConvertTime(result.Value, london);
        Assert.Equal(new DateTime(2026, 3, 29, 8, 0, 0), local.DateTime);
        Assert.Equal(TimeSpan.FromHours(1), london.GetUtcOffset(result.Value)); // BST by the morning
    }
}
