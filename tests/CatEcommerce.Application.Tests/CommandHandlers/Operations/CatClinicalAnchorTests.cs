using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Retention;

namespace CatEcommerce.Application.Tests.CommandHandlers.Operations;

/// <summary>HO-XCT-001 U-C6 (CODE_REVIEW 19) -- the anchor a cat clinical period is counted from, and the clinic-local date it is read on.</summary>
public sealed class CatClinicalAnchorTests
{
    private static readonly DateOnly Created = new(2017, 3, 1);

    [Fact]
    public void ADeceasedCat_IsDatedFromItsDeath_WhenNoVisitCameLater() =>
        Assert.Equal(new DateOnly(2018, 5, 5), CatClinicalReviewSource.SelectAnchor(Created, new DateOnly(2018, 5, 5), new DateOnly(2018, 1, 1)));

    [Fact]
    public void ADeceasedCat_IsDatedFromItsLastVisit_WhenThatIsLaterThanTheDeathDate() =>
        Assert.Equal(new DateOnly(2019, 2, 2), CatClinicalReviewSource.SelectAnchor(Created, new DateOnly(2018, 5, 5), new DateOnly(2019, 2, 2)));

    [Fact]
    public void ALivingCat_IsDatedFromItsLastCompletedVisit() =>
        Assert.Equal(new DateOnly(2023, 7, 7), CatClinicalReviewSource.SelectAnchor(Created, null, new DateOnly(2023, 7, 7)));

    [Fact]
    public void ACatWithNeitherDeathNorVisit_IsDatedFromTheDayItsProfileWasCreated() =>
        Assert.Equal(Created, CatClinicalReviewSource.SelectAnchor(Created, null, null));

    [Fact]
    public void AnInstantBetweenMidnightAndSixAmInDhaka_IsTheNextCalendarDayLocally_NotTheUtcDay()
    {
        // 2026-10-12 20:30 UTC is 2026-10-13 02:30 in Asia/Dhaka: the clinic's date, which is what the period is counted on.
        var instant = new DateTimeOffset(2026, 10, 12, 20, 30, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 10, 13), BranchCalendar.LocalDateOf(instant, "Asia/Dhaka"));
        Assert.Equal(new DateOnly(2026, 10, 12), DateOnly.FromDateTime(instant.UtcDateTime));
    }
}
