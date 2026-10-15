using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.Domain.Tests.Boarding;

/// <summary>HO-BRD-001 Phase E -- U-E1 .. U-E5, U-E11: the pure care services and the signature rule.</summary>
public sealed class BoardingCareDomainTests
{
    private const string Dhaka = "Asia/Dhaka";
    private static readonly TimeRange CheckOut = new(new TimeOnly(8, 0), new TimeOnly(11, 0));
    private static readonly DateOnly Arrival = new(2026, 11, 2);

    private static CareProfile Profile(DateOnly? medFrom = null, DateOnly? medTo = null) => new(
        [new FeedingPlan(new TimeOnly(8, 0), "1/3 cup", FoodSource.OwnFood, "Dry", null), new FeedingPlan(new TimeOnly(18, 0), "1/3 cup", FoodSource.HouseFood, "Wet", null)],
        [new MedicationPlan("Metacam", "0.5 ml", "oral", [new TimeOnly(9, 0), new TimeOnly(21, 0)], null, medFrom, medTo)],
        LitterType.Clumping, null, true, 5000m, new EmergencyContact("May", "017", null), null);

    private static IReadOnlyList<RoutineTimeInput> Routine() =>
    [
        new(RoutineTaskKind.LitterChange, new TimeOnly(9, 0)), new(RoutineTaskKind.LitterChange, new TimeOnly(17, 0)),
        new(RoutineTaskKind.Enrichment, new TimeOnly(15, 0)),
        new(RoutineTaskKind.ObservationCheck, new TimeOnly(10, 0)), new(RoutineTaskKind.ObservationCheck, new TimeOnly(20, 0)),
    ];

    private static IReadOnlyList<CareTaskSeed> Generate(DateOnly date, DateOnly departure, DateTimeOffset checkedIn, CareProfile profile) =>
        CareTaskGenerator.Generate(date, Dhaka, Arrival, departure, checkedIn, CheckOut, [new CareTaskCatInput(Guid.NewGuid(), "Mitu", Guid.NewGuid(), profile)], Routine());

    private static DateTimeOffset At(DateOnly date, int hour) => BoardingOccupancyWindow.InstantOf(date, new TimeOnly(hour, 0), Dhaka);

    [Fact]
    public void U_E1_AFullDayOfTwoFeedingsTwoDosesAndTheRoutine_IsNineTasks_WithTheirDueTimesAndTitles()
    {
        var day = Arrival.AddDays(1);

        var seeds = Generate(day, Arrival.AddDays(5), At(Arrival, 14), Profile());

        Assert.Equal(9, seeds.Count); // 2 feeds + 2 doses + 2 litter + 1 enrichment + 2 observation checks
        Assert.Equal(2, seeds.Count(s => s.Kind == CareTaskKind.Feed));
        Assert.Equal(2, seeds.Count(s => s.Kind == CareTaskKind.Medication));
        Assert.Contains(seeds, s => s.Title == "Give Metacam 0.5 ml oral" && s.DueLocalTime == new TimeOnly(9, 0) && s.DueOn == At(day, 9));
        Assert.Contains(seeds, s => s.Title == "Feed 1/3 cup - house food" && s.SourceKey == "feed:18:00");
    }

    [Fact]
    public void U_E2_TheSameInputsTwice_GiveTheSameKeys()
    {
        var first = Generate(Arrival.AddDays(1), Arrival.AddDays(5), At(Arrival, 14), Profile()).Select(s => s.SourceKey).ToArray();
        var second = Generate(Arrival.AddDays(1), Arrival.AddDays(5), At(Arrival, 14), Profile()).Select(s => s.SourceKey).ToArray();

        Assert.Equal(first, second);
        Assert.Equal(first.Length, first.Distinct().Count());
    }

    [Fact]
    public void U_E3_AMedicationNarrowerThanTheStay_OnlyGeneratesInsideItsDates()
    {
        var profile = Profile(medFrom: Arrival.AddDays(2), medTo: Arrival.AddDays(3));

        Assert.DoesNotContain(Generate(Arrival.AddDays(1), Arrival.AddDays(5), At(Arrival, 14), profile), s => s.Kind == CareTaskKind.Medication);
        Assert.Contains(Generate(Arrival.AddDays(2), Arrival.AddDays(5), At(Arrival, 14), profile), s => s.Kind == CareTaskKind.Medication);
        Assert.DoesNotContain(Generate(Arrival.AddDays(4), Arrival.AddDays(5), At(Arrival, 14), profile), s => s.Kind == CareTaskKind.Medication);
    }

    [Fact]
    public void U_E4_AnExtendedStay_GeneratesTheAddedDays()
    {
        Assert.Empty(Generate(Arrival.AddDays(6), Arrival.AddDays(5), At(Arrival, 14), Profile()));
        Assert.NotEmpty(Generate(Arrival.AddDays(6), Arrival.AddDays(7), At(Arrival, 14), Profile()));
    }

    [Fact]
    public void TheArrivalDayStartsAtCheckIn_AndTheDepartureDayEndsAtTheCheckOutWindow()
    {
        var arrivalDay = Generate(Arrival, Arrival.AddDays(2), At(Arrival, 14), Profile());
        var departureDay = Generate(Arrival.AddDays(2), Arrival.AddDays(2), At(Arrival, 14), Profile());

        Assert.All(arrivalDay, s => Assert.True(s.DueOn >= At(Arrival, 14)));
        Assert.All(departureDay, s => Assert.True(s.DueOn <= At(Arrival.AddDays(2), 11)));
        Assert.Contains(departureDay, s => s.DueLocalTime == new TimeOnly(9, 0));
    }

    [Fact]
    public void U_E5_TwoCaretakersOnShift_SplitFourUnitsRoundRobin_AndNoneOnShiftIsUnassigned()
    {
        var a = Guid.Parse("00000000-0000-0000-0000-00000000000a");
        var b = Guid.Parse("00000000-0000-0000-0000-00000000000b");
        var due = At(Arrival, 10);
        var shift = new[] { new StaffAvailableInterval(a, Guid.Empty, At(Arrival, 6), At(Arrival, 22), null, false) };
        var shifts = new[]
        {
            new CaretakerShifts(a, shift),
            new CaretakerShifts(b, [new StaffAvailableInterval(b, Guid.Empty, At(Arrival, 6), At(Arrival, 22), null, false)]),
        };
        var units = Enumerable.Range(1, 4).Select(i => new AssignableUnit(Guid.NewGuid(), "A", "U" + i)).ToList();

        var assigned = units.Select(u => CareTaskAssigner.Assign(due, u.UnitId, units, shifts)).ToList();

        Assert.Equal([a, b, a, b], assigned);
        Assert.Equal(assigned[0], CareTaskAssigner.Assign(At(Arrival, 12), units[0].UnitId, units, shifts)); // one unit, one caretaker
        Assert.Null(CareTaskAssigner.Assign(At(Arrival, 23), units[0].UnitId, units, shifts));
        Assert.Equal(b, CareTaskAssigner.Assign(due, units[0].UnitId, units, shifts, new HashSet<Guid> { a }));
    }

    [Fact]
    public void U_E11_Signatures_AreBoundedVectorDataOrATypedConfirmation()
    {
        var now = DateTimeOffset.UtcNow;
        var huge = "[[" + string.Join(",", Enumerable.Range(0, 5000).Select(i => "[1000,2000]")) + "]]";

        Assert.Null(new SignatureCapture("Owner", SignatureMethod.Drawn, "[[[10,20],[30,40]]]", true, now).Validate());
        Assert.NotNull(new SignatureCapture("Owner", SignatureMethod.Drawn, huge, true, now).Validate()); // over 32 KB
        Assert.NotNull(new SignatureCapture("Owner", SignatureMethod.Drawn, "[[[\"a\",1]]]", true, now).Validate()); // not numbers
        Assert.NotNull(new SignatureCapture("Owner", SignatureMethod.Drawn, "[[[10,20000]]]", true, now).Validate()); // out of range
        Assert.NotNull(new SignatureCapture("Owner", SignatureMethod.Typed, null, false, now).Validate()); // typed without confirmation
        Assert.Null(new SignatureCapture("Owner", SignatureMethod.Typed, null, true, now).Validate());
    }
}
