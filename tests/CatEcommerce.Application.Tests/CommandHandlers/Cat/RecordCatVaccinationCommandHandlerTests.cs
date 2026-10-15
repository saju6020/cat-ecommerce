using CatEcommerce.Application.CommandHandlers.Cat;
using CatEcommerce.Application.Commands.Cat;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Vaccination;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace CatEcommerce.Application.Tests.CommandHandlers.Cat;

public sealed class RecordCatVaccinationCommandHandlerTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static CatAggregate SeededCat(FakeAggregateRootRepository<CatAggregate> repository)
    {
        var cat = new CatAggregate(Guid.NewGuid());
        cat.Register(new CatRegistrationDto(
            Guid.NewGuid(), "Mittens", "Cat", CatSex.Female, Today.AddYears(-2),
            DateOfBirthPrecision.Exact, null, null, Today, TestUserContext.Create(), Guid.NewGuid()));
        repository.Seed(cat);
        return cat;
    }

    private static StaffAccountAggregate SeededVet(FakeAggregateRootRepository<StaffAccountAggregate> repository)
    {
        var vet = new StaffAccountAggregate(Guid.NewGuid());
        vet.Create(new StaffAccountCreationDto(
            "Dr. Vet", MobileNumber.Create("01712345678"), null, [StaffRoles.Vet],
            TestUserContext.Create(), Guid.NewGuid()));
        repository.Seed(vet);
        return vet;
    }

    // U33
    [Fact]
    public async Task HandleAsync_InactiveVaccine_ReturnsInactiveVaccine()
    {
        var catRepository = new FakeAggregateRootRepository<CatAggregate>();
        var cat = SeededCat(catRepository);

        var vaccineRepository = new FakeAggregateRootRepository<VaccineAggregate>();
        var vaccine = new VaccineAggregate(Guid.NewGuid());
        vaccine.Register(new VaccineRegistrationDto("RABIES", "Rabies", true, 12, null,
            TestUserContext.Create(), Guid.NewGuid()));
        typeof(VaccineAggregate).GetProperty(nameof(VaccineAggregate.IsActive))!
            .SetValue(vaccine, false, null); // exercised through reflection: no Deactivate() method in this slice
        vaccineRepository.Seed(vaccine);

        var staffAccountRepository = new FakeAggregateRootRepository<StaffAccountAggregate>();

        var handler = new RecordCatVaccinationCommandHandler(
            NullLogger<RecordCatVaccinationCommandHandler>.Instance, catRepository, vaccineRepository,
            staffAccountRepository, new FakeCatRecordClock(Today));

        var response = await handler.HandleAsync(new RecordCatVaccinationCommand
        {
            CatId = cat.Id,
            VaccinationId = Guid.NewGuid(),
            VaccineId = vaccine.Id,
            BatchReference = "BATCH-1",
            BatchExpiresOn = Today.AddMonths(6),
            AdministeredOn = Today,
            AdministeredByVetId = Guid.NewGuid(),
            UserContext = TestUserContext.Create(),
            CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Equal(BusinessViolationCodes.InactiveVaccine, response.ValidationResult!.Errors[0].ErrorCode);
    }

    [Fact]
    public async Task HandleAsync_VaccineNotFound_ReturnsVaccineNotFound()
    {
        var catRepository = new FakeAggregateRootRepository<CatAggregate>();
        var cat = SeededCat(catRepository);
        var vaccineRepository = new FakeAggregateRootRepository<VaccineAggregate>();
        var staffAccountRepository = new FakeAggregateRootRepository<StaffAccountAggregate>();

        var handler = new RecordCatVaccinationCommandHandler(
            NullLogger<RecordCatVaccinationCommandHandler>.Instance, catRepository, vaccineRepository,
            staffAccountRepository, new FakeCatRecordClock(Today));

        var response = await handler.HandleAsync(new RecordCatVaccinationCommand
        {
            CatId = cat.Id,
            VaccinationId = Guid.NewGuid(),
            VaccineId = Guid.NewGuid(),
            BatchReference = "BATCH-1",
            BatchExpiresOn = Today.AddMonths(6),
            AdministeredOn = Today,
            AdministeredByVetId = Guid.NewGuid(),
            UserContext = TestUserContext.Create(),
            CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Equal(BusinessViolationCodes.VaccineNotFound, response.ValidationResult!.Errors[0].ErrorCode);
    }

    // SECURITY gate, MEDIUM (2026-09-26): a spoofed/non-existent AdministeredByVetId is refused.
    [Fact]
    public async Task HandleAsync_UnknownVet_ReturnsStaffAccountNotFound()
    {
        var catRepository = new FakeAggregateRootRepository<CatAggregate>();
        var cat = SeededCat(catRepository);

        var vaccineRepository = new FakeAggregateRootRepository<VaccineAggregate>();
        var vaccine = new VaccineAggregate(Guid.NewGuid());
        vaccine.Register(new VaccineRegistrationDto("RABIES", "Rabies", true, 12, null,
            TestUserContext.Create(), Guid.NewGuid()));
        vaccineRepository.Seed(vaccine);

        var staffAccountRepository = new FakeAggregateRootRepository<StaffAccountAggregate>();

        var handler = new RecordCatVaccinationCommandHandler(
            NullLogger<RecordCatVaccinationCommandHandler>.Instance, catRepository, vaccineRepository,
            staffAccountRepository, new FakeCatRecordClock(Today));

        var response = await handler.HandleAsync(new RecordCatVaccinationCommand
        {
            CatId = cat.Id,
            VaccinationId = Guid.NewGuid(),
            VaccineId = vaccine.Id,
            BatchReference = "BATCH-1",
            BatchExpiresOn = Today.AddMonths(6),
            AdministeredOn = Today,
            AdministeredByVetId = Guid.NewGuid(), // never seeded -- spoofed
            UserContext = TestUserContext.Create(),
            CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Equal(BusinessViolationCodes.StaffAccountNotFound, response.ValidationResult!.Errors[0].ErrorCode);
    }

    // SECURITY gate, MEDIUM (2026-09-26): an existing but non-vet staff account is refused.
    [Fact]
    public async Task HandleAsync_VetFieldNamesNonVetStaff_ReturnsStaffAccountNotVeterinarian()
    {
        var catRepository = new FakeAggregateRootRepository<CatAggregate>();
        var cat = SeededCat(catRepository);

        var vaccineRepository = new FakeAggregateRootRepository<VaccineAggregate>();
        var vaccine = new VaccineAggregate(Guid.NewGuid());
        vaccine.Register(new VaccineRegistrationDto("RABIES", "Rabies", true, 12, null,
            TestUserContext.Create(), Guid.NewGuid()));
        vaccineRepository.Seed(vaccine);

        var staffAccountRepository = new FakeAggregateRootRepository<StaffAccountAggregate>();
        var nurse = new StaffAccountAggregate(Guid.NewGuid());
        nurse.Create(new StaffAccountCreationDto(
            "Nurse Joy", MobileNumber.Create("01798765432"), null, [StaffRoles.Nurse],
            TestUserContext.Create(), Guid.NewGuid()));
        staffAccountRepository.Seed(nurse);

        var handler = new RecordCatVaccinationCommandHandler(
            NullLogger<RecordCatVaccinationCommandHandler>.Instance, catRepository, vaccineRepository,
            staffAccountRepository, new FakeCatRecordClock(Today));

        var response = await handler.HandleAsync(new RecordCatVaccinationCommand
        {
            CatId = cat.Id,
            VaccinationId = Guid.NewGuid(),
            VaccineId = vaccine.Id,
            BatchReference = "BATCH-1",
            BatchExpiresOn = Today.AddMonths(6),
            AdministeredOn = Today,
            AdministeredByVetId = nurse.Id,
            UserContext = TestUserContext.Create(),
            CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Equal(BusinessViolationCodes.StaffAccountNotVeterinarian, response.ValidationResult!.Errors[0].ErrorCode);
    }

    [Fact]
    public async Task HandleAsync_ValidVet_Succeeds()
    {
        var catRepository = new FakeAggregateRootRepository<CatAggregate>();
        var cat = SeededCat(catRepository);

        var vaccineRepository = new FakeAggregateRootRepository<VaccineAggregate>();
        var vaccine = new VaccineAggregate(Guid.NewGuid());
        vaccine.Register(new VaccineRegistrationDto("RABIES", "Rabies", true, 12, null,
            TestUserContext.Create(), Guid.NewGuid()));
        vaccineRepository.Seed(vaccine);

        var staffAccountRepository = new FakeAggregateRootRepository<StaffAccountAggregate>();
        var vet = SeededVet(staffAccountRepository);

        var handler = new RecordCatVaccinationCommandHandler(
            NullLogger<RecordCatVaccinationCommandHandler>.Instance, catRepository, vaccineRepository,
            staffAccountRepository, new FakeCatRecordClock(Today));

        var response = await handler.HandleAsync(new RecordCatVaccinationCommand
        {
            CatId = cat.Id,
            VaccinationId = Guid.NewGuid(),
            VaccineId = vaccine.Id,
            BatchReference = "BATCH-1",
            BatchExpiresOn = Today.AddMonths(6),
            AdministeredOn = Today,
            AdministeredByVetId = vet.Id,
            UserContext = TestUserContext.Create(),
            CorrelationId = Guid.NewGuid()
        });

        Assert.True(response.IsSuccess);
    }
}
