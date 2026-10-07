using CatEcommerce.Read.QueryHandlers.Cat;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Application.Tests.QueryHandlers.Cat;

/// <summary>
/// U34-U38 (HO-CAT-001 section 15). CatVaccinationStatusCalculator is a pure function factored
/// out of GetCatVaccinationStatusQueryHandler specifically so US-CAT-07's algorithm can be
/// unit-tested without a database.
/// </summary>
public sealed class CatVaccinationStatusCalculatorTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private const int WindowDays = 30;

    // U34
    [Fact]
    public void Calculate_AllCoreInDate_ReturnsCurrent()
    {
        var catId = Guid.NewGuid();
        var vaccineId = Guid.NewGuid();
        var vaccines = new[] { new ActiveCoreVaccine(vaccineId, "RABIES", "Rabies") };
        var vaccinations = new[]
        {
            new CatVaccinationRecord(vaccineId, Today.AddMonths(-1), Today.AddMonths(11))
        };

        var result = CatVaccinationStatusCalculator.Calculate(
            catId, vaccines, vaccinations, Today, WindowDays);

        Assert.Equal(VaccinationStatus.Current.DisplayName, result.OverallStatus);
        Assert.True(result.BookingEligible);
        Assert.Equal(VaccinationStatus.Current.DisplayName, result.Vaccines[0].Status);
    }

    // U35
    [Fact]
    public void Calculate_OneCoreDueWithinWindow_ReturnsDueSoon()
    {
        var catId = Guid.NewGuid();
        var vaccineId = Guid.NewGuid();
        var vaccines = new[] { new ActiveCoreVaccine(vaccineId, "RABIES", "Rabies") };
        var vaccinations = new[]
        {
            new CatVaccinationRecord(vaccineId, Today.AddMonths(-11), Today.AddDays(10))
        };

        var result = CatVaccinationStatusCalculator.Calculate(
            catId, vaccines, vaccinations, Today, WindowDays);

        Assert.Equal(VaccinationStatus.DueSoon.DisplayName, result.OverallStatus);
        Assert.True(result.BookingEligible);
        Assert.Equal(Today.AddDays(10), result.EarliestNextDueOn);
    }

    // U36
    [Fact]
    public void Calculate_OneCorePastDue_ReturnsOverdueAndNotBookingEligible()
    {
        var catId = Guid.NewGuid();
        var vaccineId = Guid.NewGuid();
        var vaccines = new[] { new ActiveCoreVaccine(vaccineId, "RABIES", "Rabies") };
        var vaccinations = new[]
        {
            new CatVaccinationRecord(vaccineId, Today.AddYears(-2), Today.AddDays(-1))
        };

        var result = CatVaccinationStatusCalculator.Calculate(
            catId, vaccines, vaccinations, Today, WindowDays);

        Assert.Equal(VaccinationStatus.Overdue.DisplayName, result.OverallStatus);
        Assert.False(result.BookingEligible);
    }

    // U37
    [Fact]
    public void Calculate_CoreVaccineNeverAdministered_ReturnsOverdueWithNeverAdministeredReason()
    {
        var catId = Guid.NewGuid();
        var vaccineId = Guid.NewGuid();
        var vaccines = new[] { new ActiveCoreVaccine(vaccineId, "RABIES", "Rabies") };
        var vaccinations = Array.Empty<CatVaccinationRecord>();

        var result = CatVaccinationStatusCalculator.Calculate(
            catId, vaccines, vaccinations, Today, WindowDays);

        Assert.Equal(VaccinationStatus.Overdue.DisplayName, result.OverallStatus);
        Assert.False(result.BookingEligible);
        Assert.Equal("NeverAdministered", result.Vaccines[0].Reason);
        Assert.Null(result.Vaccines[0].NextDueOn);
    }

    // U38 -- boundary: exactly today + window is DueSoon, not Current.
    [Fact]
    public void Calculate_NextDueExactlyAtWindowBoundary_ReturnsDueSoonNotCurrent()
    {
        var catId = Guid.NewGuid();
        var vaccineId = Guid.NewGuid();
        var vaccines = new[] { new ActiveCoreVaccine(vaccineId, "RABIES", "Rabies") };
        var vaccinations = new[]
        {
            new CatVaccinationRecord(vaccineId, Today.AddMonths(-11), Today.AddDays(WindowDays))
        };

        var result = CatVaccinationStatusCalculator.Calculate(
            catId, vaccines, vaccinations, Today, WindowDays);

        Assert.Equal(VaccinationStatus.DueSoon.DisplayName, result.OverallStatus);
    }

    // CODE_REVIEW gate, MEDIUM (2026-09-26): §12.3 step 4's "worst status wins" is the one piece
    // of Q4 that needs more than a single vaccine to exercise meaningfully. U34-U38 all construct
    // exactly one core vaccine; this asserts the overall status picks the worse of two.
    [Fact]
    public void Calculate_OneCoreOverdueAndOneCoreCurrent_OverallStatusIsOverdue()
    {
        var catId = Guid.NewGuid();
        var overdueVaccineId = Guid.NewGuid();
        var currentVaccineId = Guid.NewGuid();
        var vaccines = new[]
        {
            new ActiveCoreVaccine(overdueVaccineId, "RABIES", "Rabies"),
            new ActiveCoreVaccine(currentVaccineId, "FVRCP", "Feline Distemper")
        };
        var vaccinations = new[]
        {
            new CatVaccinationRecord(overdueVaccineId, Today.AddYears(-2), Today.AddDays(-1)),
            new CatVaccinationRecord(currentVaccineId, Today.AddMonths(-1), Today.AddMonths(11))
        };

        var result = CatVaccinationStatusCalculator.Calculate(
            catId, vaccines, vaccinations, Today, WindowDays);

        Assert.Equal(VaccinationStatus.Overdue.DisplayName, result.OverallStatus);
        Assert.False(result.BookingEligible);
        Assert.Equal(VaccinationStatus.Overdue.DisplayName,
            result.Vaccines.Single(v => v.VaccineId == overdueVaccineId).Status);
        Assert.Equal(VaccinationStatus.Current.DisplayName,
            result.Vaccines.Single(v => v.VaccineId == currentVaccineId).Status);
    }

    // Same shape, DueSoon vs Current -- overall must be DueSoon, not silently Current.
    [Fact]
    public void Calculate_OneCoreDueSoonAndOneCoreCurrent_OverallStatusIsDueSoon()
    {
        var catId = Guid.NewGuid();
        var dueSoonVaccineId = Guid.NewGuid();
        var currentVaccineId = Guid.NewGuid();
        var vaccines = new[]
        {
            new ActiveCoreVaccine(dueSoonVaccineId, "RABIES", "Rabies"),
            new ActiveCoreVaccine(currentVaccineId, "FVRCP", "Feline Distemper")
        };
        var vaccinations = new[]
        {
            new CatVaccinationRecord(dueSoonVaccineId, Today.AddMonths(-11), Today.AddDays(10)),
            new CatVaccinationRecord(currentVaccineId, Today.AddMonths(-1), Today.AddMonths(11))
        };

        var result = CatVaccinationStatusCalculator.Calculate(
            catId, vaccines, vaccinations, Today, WindowDays);

        Assert.Equal(VaccinationStatus.DueSoon.DisplayName, result.OverallStatus);
        Assert.True(result.BookingEligible);
    }
}
