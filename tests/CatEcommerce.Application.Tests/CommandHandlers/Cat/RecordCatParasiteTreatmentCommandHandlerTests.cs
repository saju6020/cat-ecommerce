using CatEcommerce.Application.CommandHandlers.Cat;
using CatEcommerce.Application.Commands.Cat;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace CatEcommerce.Application.Tests.CommandHandlers.Cat;

/// <summary>HO-CAT-001 §15 U41 -- owner decision 2026-09-25 (HO-CAT-002-ACCEPTANCE BLOCKER 1 / A13).</summary>
public sealed class RecordCatParasiteTreatmentCommandHandlerTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly TimeProvider FixedTimeProvider =
        new FakeTimeProvider(new DateTimeOffset(Today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));

    private static CatAggregate SeededCat(FakeAggregateRootRepository<CatAggregate> repository)
    {
        var cat = new CatAggregate(Guid.NewGuid());
        cat.Register(new CatRegistrationDto(
            Guid.NewGuid(), "Mittens", "Cat", CatSex.Female, Today.AddYears(-2),
            DateOfBirthPrecision.Exact, null, null, Today, TestUserContext.Create(), Guid.NewGuid()));
        repository.Seed(cat);
        return cat;
    }

    private static StaffAccountAggregate SeededStaff(FakeAggregateRootRepository<StaffAccountAggregate> repository, string role = StaffRoles.Nurse)
    {
        var staff = new StaffAccountAggregate(Guid.NewGuid());
        staff.Create(new StaffAccountCreationDto(
            "Staff Member", MobileNumber.Create("01712345678"), null, [role],
            TestUserContext.Create(), Guid.NewGuid()));
        repository.Seed(staff);
        return staff;
    }

    // U41
    [Fact]
    public async Task HandleAsync_Valid_ReturnsCreatedTreatmentIdAndAggregateComputedNextDueOn()
    {
        var catRepository = new FakeAggregateRootRepository<CatAggregate>();
        var cat = SeededCat(catRepository);

        var staffAccountRepository = new FakeAggregateRootRepository<StaffAccountAggregate>();
        var staff = SeededStaff(staffAccountRepository);

        var handler = new RecordCatParasiteTreatmentCommandHandler(
            NullLogger<RecordCatParasiteTreatmentCommandHandler>.Instance, catRepository, staffAccountRepository, FixedTimeProvider);

        var treatmentId = Guid.NewGuid();
        const int intervalDays = 90;
        var administeredOn = Today;

        var response = await handler.HandleAsync(new RecordCatParasiteTreatmentCommand
        {
            CatId = cat.Id,
            TreatmentId = treatmentId,
            TreatmentType = ParasiteTreatmentType.Deworming,
            ProductName = "Drontal",
            AdministeredOn = administeredOn,
            AdministeredByStaffId = staff.Id,
            IntervalDays = intervalDays,
            UserContext = TestUserContext.Create(),
            CorrelationId = Guid.NewGuid()
        });

        Assert.True(response.IsSuccess);
        var result = Assert.IsType<RecordCatParasiteTreatmentResult>(response.Result);
        Assert.Equal(treatmentId, result.TreatmentId);
        Assert.Equal(administeredOn.AddDays(intervalDays), result.NextDueOn);
        Assert.Single(catRepository.Updated);
    }

    // SECURITY gate, MEDIUM (2026-09-26): a spoofed/non-existent AdministeredByStaffId is refused.
    [Fact]
    public async Task HandleAsync_UnknownStaffMember_ReturnsStaffAccountNotFound()
    {
        var catRepository = new FakeAggregateRootRepository<CatAggregate>();
        var cat = SeededCat(catRepository);
        var staffAccountRepository = new FakeAggregateRootRepository<StaffAccountAggregate>();

        var handler = new RecordCatParasiteTreatmentCommandHandler(
            NullLogger<RecordCatParasiteTreatmentCommandHandler>.Instance, catRepository, staffAccountRepository, FixedTimeProvider);

        var response = await handler.HandleAsync(new RecordCatParasiteTreatmentCommand
        {
            CatId = cat.Id,
            TreatmentId = Guid.NewGuid(),
            TreatmentType = ParasiteTreatmentType.Deworming,
            ProductName = "Drontal",
            AdministeredOn = Today,
            AdministeredByStaffId = Guid.NewGuid(), // never seeded -- spoofed
            IntervalDays = 90,
            UserContext = TestUserContext.Create(),
            CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Equal(BusinessViolationCodes.StaffAccountNotFound, response.ValidationResult!.Errors[0].ErrorCode);
    }
}
