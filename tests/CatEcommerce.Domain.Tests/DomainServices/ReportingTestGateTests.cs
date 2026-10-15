using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.Domain.Tests.DomainServices;

/// <summary>
/// HO-RPT-001 TEST gate (2026-10-13) -- T-U1 and T-U2: the Asia/Dhaka day and month boundaries, tick by tick. A report day is half-open in the branch zone, so a sale at 23:59:59.9999999 on the 13th
/// belongs to the 13th and one at 00:00:00 to the 14th, however the UTC date reads.
/// </summary>
public sealed class ReportingTestGateTests
{
    private static readonly TimeZoneInfo Dhaka = TimeZoneInfo.FindSystemTimeZoneById("Asia/Dhaka");
    private static readonly TimeSpan Plus6 = TimeSpan.FromHours(6);

    // ── T-U1 ────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void T_U1_ADayInDhaka_IsHalfOpen_AtMidnight_AndTheLastTickBelongsToTheDay()
    {
        var thirteenth = new DateOnly(2026, 10, 13);
        var (start, end) = ReportPeriodResolver.ToInstants(thirteenth, thirteenth, Dhaka);

        Assert.Equal(DateTimeOffset.Parse("2026-10-12T18:00:00Z"), start);
        Assert.Equal(DateTimeOffset.Parse("2026-10-13T18:00:00Z"), end);

        var lastTick = end.AddTicks(-1);                       // 23:59:59.9999999 Dhaka on the 13th
        Assert.True(lastTick >= start && lastTick < end);
        Assert.Equal(thirteenth, ReportPeriodResolver.LocalDate(lastTick, Dhaka));

        Assert.Equal(new DateOnly(2026, 10, 14), ReportPeriodResolver.LocalDate(end, Dhaka));        // 00:00:00 Dhaka is the 14th
        Assert.Equal(thirteenth, ReportPeriodResolver.LocalDate(start, Dhaka));                      // 00:00:00 Dhaka on the 13th is the 13th
        Assert.Equal(new DateOnly(2026, 10, 12), ReportPeriodResolver.LocalDate(start.AddTicks(-1), Dhaka));

        // A two-day range is exactly two 24-hour days: Dhaka has no daylight saving.
        var (twoStart, twoEnd) = ReportPeriodResolver.ToInstants(thirteenth, thirteenth.AddDays(1), Dhaka);
        Assert.Equal(TimeSpan.FromHours(48), twoEnd - twoStart);
        Assert.Equal(end, twoStart.AddDays(1));
    }

    // ── T-U2 ────────────────────────────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("2026-10-31T23:59:59.9999999+06:00", "Today", "2026-10-31", "2026-10-31")]
    [InlineData("2026-10-31T23:59:59.9999999+06:00", "PreviousDay", "2026-10-30", "2026-10-30")]
    [InlineData("2026-10-31T23:59:59.9999999+06:00", "MonthToDate", "2026-10-01", "2026-10-31")]
    [InlineData("2026-10-31T23:59:59.9999999+06:00", "PreviousMonth", "2026-09-01", "2026-09-30")]
    [InlineData("2026-11-01T00:00:00+06:00", "Today", "2026-11-01", "2026-11-01")]
    [InlineData("2026-11-01T00:00:00+06:00", "PreviousDay", "2026-10-31", "2026-10-31")]
    [InlineData("2026-11-01T00:00:00+06:00", "MonthToDate", "2026-11-01", "2026-11-01")]
    [InlineData("2026-11-01T00:00:00+06:00", "PreviousMonth", "2026-10-01", "2026-10-31")]
    public void T_U2_RelativePeriods_FlipExactlyAtMidnightInDhaka(string asOf, string period, string from, string to)
    {
        var instant = DateTimeOffset.Parse(asOf);
        var relative = Enumeration.GetAll<ReportRelativePeriod>().Single(p => p.DisplayName == period);

        var (resolvedFrom, resolvedTo) = ReportPeriodResolver.Resolve(relative, instant, Dhaka);

        Assert.Equal(DateOnly.Parse(from), resolvedFrom);
        Assert.Equal(DateOnly.Parse(to), resolvedTo);
    }

    [Fact]
    public void T_U2_TheUtcDateLagsSixHours_SoTheSameInstantIsTwoDifferentDays()
    {
        // 2026-10-31T18:00:00Z is 2026-11-01T00:00 in Dhaka: the UTC date is still October, the clinic's is November.
        var instant = new DateTimeOffset(2026, 10, 31, 18, 0, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 11, 1), ReportPeriodResolver.LocalDate(instant, Dhaka));
        Assert.Equal(new DateOnly(2026, 10, 31), ReportPeriodResolver.LocalDate(instant, TimeZoneInfo.Utc));
        Assert.Equal(new DateOnly(2026, 11, 1), ReportPeriodResolver.LocalDate(instant.ToOffset(Plus6), Dhaka));
    }
}
