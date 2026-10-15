using CatEcommerce.Application.CommandHandlers.Visit;
using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace CatEcommerce.Application.Tests.CommandHandlers.Visit;

/// <summary>HO-EMR-001 §15 Phase B -- U-B1/U-B2 at handler level, U-B8, U-B9 and the branch rule.</summary>
public sealed class VisitHandlerTests
{
    private static readonly DateTimeOffset Now = ClinicalTestData.Now;

    private sealed class World
    {
        public BranchAggregate Branch { get; } = ClinicalTestData.Branch();
        public FakeAggregateRootRepository<VisitAggregate> Visits { get; } = new();
        public FakeAggregateRootRepository<AppointmentAggregate> Appointments { get; } = new();
        public FakeAggregateRootRepository<CatAggregate> Cats { get; } = new();
        public FakeAggregateRootRepository<BranchAggregate> Branches { get; } = new();
        public FakeAggregateRootRepository<CatWeightRecordAggregate> Weights { get; } = new();
        public List<string> Log { get; } = [];
        public FakeTimeProvider Clock { get; } = new(Now);

        public World() => Branches.Seed(Branch);

        public OpenVisitCommandHandler OpenHandler() => new(Visits, Appointments, Cats, Clock);

        public RecordVitalsCommandHandler VitalsHandler() => new(
            new LoggingRepository<VisitAggregate>("visit", Log, Visits),
            new LoggingRepository<CatWeightRecordAggregate>("weight", Log, Weights),
            Branches, Clock);

        public (CatAggregate Cat, AppointmentAggregate Appointment) CheckedIn(bool deceased = false)
        {
            var cat = ClinicalTestData.Cat(deceased: deceased);
            var appointment = ClinicalTestData.CheckedInAppointment(Branch.Id, cat);
            Cats.Seed(cat);
            Appointments.Seed(appointment);
            return (cat, appointment);
        }

        public VisitAggregate OpenVisitFor(CatAggregate cat, AppointmentAggregate appointment)
        {
            var visit = ClinicalTestData.OpenVisit(Branch.Id, appointment.Id, cat.Id);
            Visits.Seed(visit);
            return visit;
        }
    }

    private static OpenVisitCommand OpenCommand(Guid appointmentId, Guid branchId) => new()
    {
        VisitId = Guid.NewGuid(), AppointmentId = appointmentId, BranchId = branchId, UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
    };

    private static RecordVitalsCommand VitalsCommand(Guid visitId, Guid branchId, decimal? weight = null, decimal? temperature = null,
        DateTimeOffset? measuredOn = null, params string[] confirmed) => new()
    {
        VisitId = visitId, VitalsEntryId = Guid.NewGuid(), BranchId = branchId, WeightKg = weight, TemperatureC = temperature,
        MeasuredOn = measuredOn, ConfirmedOutOfRange = confirmed, UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
    };

    // ── C5 Open ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Open_ACheckedInAppointment_SavesTheVisit_WithTheAppointmentsCatAndBranch() // U-B1
    {
        var world = new World();
        var (cat, appointment) = world.CheckedIn();

        var response = await world.OpenHandler().HandleAsync(OpenCommand(appointment.Id, world.Branch.Id));

        Assert.True(response.IsSuccess);
        var result = Assert.IsType<OpenVisitResult>(response.Result);
        Assert.False(result.AlreadyExisted);
        var visit = Assert.Single(world.Visits.Saved);
        Assert.Equal(result.VisitId, visit.Id);
        Assert.Equal(cat.Id, visit.CatId);
        Assert.Equal(world.Branch.Id, visit.BranchId);
        Assert.Equal(appointment.ServiceSnapshot.Name, visit.ServiceName);
    }

    [Fact]
    public async Task Open_ABookedAppointment_IsRefused() // U-B1
    {
        var world = new World();
        var cat = ClinicalTestData.Cat();
        var booked = ClinicalTestData.BookedAppointment(world.Branch.Id, cat);
        world.Cats.Seed(cat);
        world.Appointments.Seed(booked);

        var response = await world.OpenHandler().HandleAsync(OpenCommand(booked.Id, world.Branch.Id));

        Assert.Equal(BusinessViolationCodes.AppointmentNotReadyForVisit, response.ValidationResult!.Errors.Single().ErrorCode);
        Assert.Empty(world.Visits.Saved);
    }

    [Fact]
    public async Task Open_ForADeceasedCat_IsRefused() // U-B2
    {
        var world = new World();
        var (_, appointment) = world.CheckedIn(deceased: true);

        var response = await world.OpenHandler().HandleAsync(OpenCommand(appointment.Id, world.Branch.Id));

        Assert.Equal(BusinessViolationCodes.CatIsDeceased, response.ValidationResult!.Errors.Single().ErrorCode);
        Assert.Empty(world.Visits.Saved);
    }

    [Fact]
    public async Task Open_Twice_ReturnsTheSameVisit_AndSavesOnce() // I-B1's idempotency
    {
        var world = new World();
        var (cat, appointment) = world.CheckedIn();
        var existing = world.OpenVisitFor(cat, appointment);

        var response = await world.OpenHandler().HandleAsync(OpenCommand(appointment.Id, world.Branch.Id));

        var result = Assert.IsType<OpenVisitResult>(response.Result);
        Assert.True(result.AlreadyExisted);
        Assert.Equal(existing.Id, result.VisitId);
        Assert.Empty(world.Visits.Saved);
    }

    [Fact]
    public async Task Open_AnAppointmentOfAnotherBranch_IsNotFound()
    {
        var world = new World();
        var (_, appointment) = world.CheckedIn();

        var response = await world.OpenHandler().HandleAsync(OpenCommand(appointment.Id, Guid.NewGuid()));

        Assert.Equal(BusinessViolationCodes.AppointmentNotFound, response.ValidationResult!.Errors.Single().ErrorCode);
        Assert.Empty(world.Visits.Saved);
    }

    // ── C6 Vitals ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RecordVitals_WithAWeight_SavesTheWeightRecord_WithTheEntryId_BeforeTheVisitIsUpdated() // U-B8, ADR-121
    {
        var world = new World();
        var (cat, appointment) = world.CheckedIn();
        var visit = world.OpenVisitFor(cat, appointment);
        // 2026-10-07 20:30 UTC is already 2026-10-08 02:30 in Dhaka: the weight is dated by the BRANCH's day.
        var measuredOn = new DateTimeOffset(2026, 10, 7, 20, 30, 0, TimeSpan.Zero);
        world.Clock.SetUtcNow(measuredOn.AddMinutes(1));
        var command = VitalsCommand(visit.Id, world.Branch.Id, weight: 4.2m, measuredOn: measuredOn);

        var response = await world.VitalsHandler().HandleAsync(command);

        Assert.True(response.IsSuccess);
        var entry = Assert.Single(visit.Vitals);
        Assert.Equal(command.VitalsEntryId, entry.Id);

        var weight = Assert.Single(world.Weights.Saved);
        Assert.Equal(entry.Id, weight.Id);
        Assert.Equal(cat.Id, weight.CatId);
        Assert.Equal(4.2m, weight.Weight.Kilograms);
        Assert.Equal(new DateOnly(2026, 10, 8), weight.RecordedOn);
        Assert.Equal(command.UserContext.UserId, weight.RecordedByStaffId);

        // Companion commit order (§8.1): the new root is saved, then the visit is updated; the cat is never saved.
        Assert.Equal(["save:weight", "update:visit"], world.Log);
        Assert.Empty(world.Cats.Saved);
        Assert.Empty(world.Cats.Updated);
    }

    [Fact]
    public async Task RecordVitals_WithoutAWeight_WritesNoWeightRecord() // U-B9
    {
        var world = new World();
        var (cat, appointment) = world.CheckedIn();
        var visit = world.OpenVisitFor(cat, appointment);

        var response = await world.VitalsHandler().HandleAsync(VitalsCommand(visit.Id, world.Branch.Id, temperature: 38.6m));

        Assert.True(response.IsSuccess);
        Assert.Empty(world.Weights.Saved);
        Assert.Equal(["update:visit"], world.Log);
        Assert.Single(visit.Vitals);
    }

    [Fact]
    public async Task RecordVitals_OutsideThePlausibleRange_IsRefusedWithTheStructuredDetail_ThenSavedWhenConfirmed() // U-B5
    {
        var world = new World();
        var (cat, appointment) = world.CheckedIn();
        var visit = world.OpenVisitFor(cat, appointment);

        var refused = await world.VitalsHandler().HandleAsync(VitalsCommand(visit.Id, world.Branch.Id, temperature: 42.0m));

        Assert.False(refused.IsSuccess);
        Assert.Equal(BusinessViolationCodes.VitalsOutsidePlausibleRange, refused.ValidationResult!.Errors.Single().ErrorCode);
        var detail = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<OutOfRangeSign>>(refused.Result));
        Assert.Equal(("Temperature", "42.0", "°C", "35.0", "41.5"), (detail.Sign, detail.Value, detail.Unit, detail.PlausibleMin, detail.PlausibleMax));
        Assert.Empty(visit.Vitals);

        var confirmed = await world.VitalsHandler().HandleAsync(VitalsCommand(visit.Id, world.Branch.Id, temperature: 42.0m, confirmed: nameof(VitalSign.Temperature)));

        Assert.True(confirmed.IsSuccess);
        Assert.Equal([VitalSign.Temperature], Assert.Single(visit.Vitals).ConfirmedSigns());
    }

    [Fact]
    public async Task RecordVitals_AbsurdValue_IsRefused_EvenWhenConfirmed() // U-B4
    {
        var world = new World();
        var (cat, appointment) = world.CheckedIn();
        var visit = world.OpenVisitFor(cat, appointment);

        var response = await world.VitalsHandler().HandleAsync(VitalsCommand(visit.Id, world.Branch.Id, temperature: 46.0m, confirmed: nameof(VitalSign.Temperature)));

        Assert.Equal(BusinessViolationCodes.VitalSignOutOfRange, response.ValidationResult!.Errors.Single().ErrorCode);
        Assert.Empty(visit.Vitals);
        Assert.Empty(world.Weights.Saved);
    }

    [Fact]
    public async Task RecordVitals_ForAVisitOfAnotherBranch_IsNotFound()
    {
        var world = new World();
        var (cat, appointment) = world.CheckedIn();
        var visit = world.OpenVisitFor(cat, appointment);

        var response = await world.VitalsHandler().HandleAsync(VitalsCommand(visit.Id, Guid.NewGuid(), temperature: 38m));

        Assert.Equal(BusinessViolationCodes.VisitNotFound, response.ValidationResult!.Errors.Single().ErrorCode);
        Assert.Empty(visit.Vitals);
    }

    [Fact]
    public async Task RecordVitals_AClientMeasuredOnWithANonUtcOffset_IsStoredAsTheSameInstantInUtc()
    {
        var world = new World();
        var (cat, appointment) = world.CheckedIn();
        var visit = world.OpenVisitFor(cat, appointment);
        var measuredOn = new DateTimeOffset(2026, 10, 7, 14, 0, 0, TimeSpan.FromHours(6)); // 08:00 UTC

        var response = await world.VitalsHandler().HandleAsync(VitalsCommand(visit.Id, world.Branch.Id, temperature: 38.5m, measuredOn: measuredOn));

        Assert.True(response.IsSuccess);
        var stored = Assert.Single(visit.Vitals).MeasuredOn;
        Assert.Equal(TimeSpan.Zero, stored.Offset);
        Assert.Equal(measuredOn, stored);
    }
}
