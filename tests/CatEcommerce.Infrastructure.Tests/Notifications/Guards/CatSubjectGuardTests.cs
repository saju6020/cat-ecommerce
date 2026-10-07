using System.Linq.Expressions;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Notifications.Guards;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Xunit;

namespace CatEcommerce.Infrastructure.Tests.Notifications.Guards;

/// <summary>
/// HO-NTF-001 §15/§8.7, U-F1. TEST gate addition: neither guard had any dedicated test before this
/// -- CareReminderEndpointTests.cs only exercised "deceased" (I-F4/I-F7), never "transferred" or
/// "renewed" (I-F5/I-F6's own domain-level half). A guard reads; it never writes (§8.7), so it is
/// exercised directly here against a real <see cref="CatAggregate"/> built in memory, no database.
/// </summary>
public sealed class CatSubjectGuardTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private static UserContext Ctx => new(Guid.NewGuid(), Guid.NewGuid());

    /// <summary>Hand-written in-memory test double -- matches
    /// CatEcommerce.Application.Tests.TestSupport.FakeAggregateRootRepository's own shape, kept
    /// local here since Infrastructure.Tests does not reference the Application test project.</summary>
    private sealed class FakeCatRepository : IAggregateRootRepository<CatAggregate>
    {
        private readonly Dictionary<Guid, CatAggregate> _store = [];

        public void Seed(CatAggregate cat) => _store[cat.Id] = cat;

        public Task<CatAggregate> GetByIdAsync(Guid id) =>
            Task.FromResult(_store.TryGetValue(id, out var value) ? value : null!);

        public Task<CatAggregate> GetByFilterAsync(Expression<Func<CatAggregate, bool>> dataFilters) =>
            Task.FromResult(_store.Values.AsQueryable().FirstOrDefault(dataFilters)!);

        public Task<bool> ExistAsync(Expression<Func<CatAggregate, bool>> dataFilters) =>
            Task.FromResult(_store.Values.AsQueryable().Any(dataFilters));

        public Task SaveAsync(CatAggregate aggregate, EventPublishOption eventPublishOption)
        {
            _store[aggregate.Id] = aggregate;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(CatAggregate aggregate, EventPublishOption eventPublishOption)
        {
            _store[aggregate.Id] = aggregate;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(CatAggregate aggregate, int expectedVersion, EventPublishOption eventPublishOption) =>
            UpdateAsync(aggregate, eventPublishOption);
    }

    private static CatAggregate NewCat(Guid ownerId)
    {
        var cat = new CatAggregate(Guid.NewGuid());
        cat.Register(new CatRegistrationDto(
            ownerId, "Mitu", "Cat", CatSex.Female, Today.AddYears(-2), DateOfBirthPrecision.Exact,
            "Domestic Shorthair", "Tabby", Today, Ctx, Guid.NewGuid()));
        return cat;
    }

    private static Guid Vaccinate(CatAggregate cat, Guid vaccineId, DateOnly administeredOn, int validityMonths = 12)
    {
        var vaccinationId = Guid.NewGuid();
        cat.RecordVaccination(new CatVaccinationDto(
            vaccinationId, vaccineId, "RABIES", "Rabies", true, validityMonths, "BATCH-1",
            Today.AddYears(1), administeredOn, Guid.NewGuid(), null, Today, Ctx, Guid.NewGuid()));
        return vaccinationId;
    }

    private static void Deworm(CatAggregate cat, DateOnly administeredOn, int intervalDays = 90)
    {
        cat.RecordParasiteTreatment(new CatParasiteTreatmentDto(
            Guid.NewGuid(), ParasiteTreatmentType.Combined, "Broad-spectrum", "BATCH-1", administeredOn,
            Guid.NewGuid(), intervalDays, null, Today, Ctx, Guid.NewGuid()));
    }

    // --- CatVaccinationDueGuard ---

    [Fact]
    public async Task VaccinationGuard_AliveOwnerMatchesSameDueDate_IsValid()
    {
        var owner = Guid.NewGuid();
        var cat = NewCat(owner);
        var vaccineId = Guid.NewGuid();
        Vaccinate(cat, vaccineId, Today.AddMonths(-12));
        var repository = new FakeCatRepository();
        repository.Seed(cat);
        var guard = new CatVaccinationDueGuard(repository);

        var valid = await guard.IsStillValidAsync(owner, cat.Id, vaccineId, cat.Vaccinations.Single().NextDueOn);

        Assert.True(valid);
    }

    [Fact]
    public async Task VaccinationGuard_CatDeceased_IsNotValid()
    {
        var owner = Guid.NewGuid();
        var cat = NewCat(owner);
        var vaccineId = Guid.NewGuid();
        Vaccinate(cat, vaccineId, Today.AddMonths(-12));
        var dueOn = cat.Vaccinations.Single().NextDueOn;
        cat.MarkDeceased(new CatDeceasedDto(Today, null, Today, Ctx, Guid.NewGuid()));
        var repository = new FakeCatRepository();
        repository.Seed(cat);
        var guard = new CatVaccinationDueGuard(repository);

        var valid = await guard.IsStillValidAsync(owner, cat.Id, vaccineId, dueOn);

        Assert.False(valid);
    }

    /// <summary>I-F5's domain-level half: the cat has since been transferred to a different owner --
    /// the ORIGINAL recipient (who requested/was requested-for the reminder) no longer owns the cat,
    /// so the guard must fail even though the vaccination record itself is untouched.</summary>
    [Fact]
    public async Task VaccinationGuard_CatTransferredToAnotherOwner_IsNotValidForTheOriginalOwner()
    {
        var originalOwner = Guid.NewGuid();
        var newOwner = Guid.NewGuid();
        var cat = NewCat(originalOwner);
        var vaccineId = Guid.NewGuid();
        Vaccinate(cat, vaccineId, Today.AddMonths(-12));
        var dueOn = cat.Vaccinations.Single().NextDueOn;
        cat.TransferOwnership(new CatOwnershipTransferDto(newOwner, "Sold to a new owner", Ctx, Guid.NewGuid()));
        var repository = new FakeCatRepository();
        repository.Seed(cat);
        var guard = new CatVaccinationDueGuard(repository);

        var validForOriginalOwner = await guard.IsStillValidAsync(originalOwner, cat.Id, vaccineId, dueOn);
        var validForNewOwner = await guard.IsStillValidAsync(newOwner, cat.Id, vaccineId, dueOn);

        Assert.False(validForOriginalOwner);
        Assert.True(validForNewOwner); // the guard itself is satisfied for whoever owns the cat now
    }

    /// <summary>I-F6's domain-level half: a later vaccination for the SAME vaccine moves
    /// NextDueOn -- the request's own SubjectDueOn (captured at enqueue time) no longer matches the
    /// latest record, so the stale reminder must not fire.</summary>
    [Fact]
    public async Task VaccinationGuard_VaccinationRenewedWithADifferentDueDate_IsNotValidForTheOldDueDate()
    {
        var owner = Guid.NewGuid();
        var cat = NewCat(owner);
        var vaccineId = Guid.NewGuid();
        Vaccinate(cat, vaccineId, Today.AddMonths(-12));
        var oldDueOn = cat.Vaccinations.Single().NextDueOn;
        Vaccinate(cat, vaccineId, Today); // renewed today -- NextDueOn moves a year out
        var repository = new FakeCatRepository();
        repository.Seed(cat);
        var guard = new CatVaccinationDueGuard(repository);

        var validForOldDueDate = await guard.IsStillValidAsync(owner, cat.Id, vaccineId, oldDueOn);
        var validForNewDueDate = await guard.IsStillValidAsync(owner, cat.Id, vaccineId, cat.Vaccinations.OrderByDescending(v => v.AdministeredOn).First().NextDueOn);

        Assert.False(validForOldDueDate);
        Assert.True(validForNewDueDate);
    }

    [Fact]
    public async Task VaccinationGuard_CatNoLongerExists_IsNotValid()
    {
        var repository = new FakeCatRepository();
        var guard = new CatVaccinationDueGuard(repository);

        var valid = await guard.IsStillValidAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Today);

        Assert.False(valid);
    }

    // --- CatDewormingDueGuard ---

    [Fact]
    public async Task DewormingGuard_AliveOwnerMatchesSameDueDate_IsValid()
    {
        var owner = Guid.NewGuid();
        var cat = NewCat(owner);
        Deworm(cat, Today.AddDays(-90));
        var repository = new FakeCatRepository();
        repository.Seed(cat);
        var guard = new CatDewormingDueGuard(repository);

        var valid = await guard.IsStillValidAsync(owner, cat.Id, null, cat.ParasiteTreatments.Single().NextDueOn);

        Assert.True(valid);
    }

    [Fact]
    public async Task DewormingGuard_CatDeceased_IsNotValid()
    {
        var owner = Guid.NewGuid();
        var cat = NewCat(owner);
        Deworm(cat, Today.AddDays(-90));
        var dueOn = cat.ParasiteTreatments.Single().NextDueOn;
        cat.MarkDeceased(new CatDeceasedDto(Today, null, Today, Ctx, Guid.NewGuid()));
        var repository = new FakeCatRepository();
        repository.Seed(cat);
        var guard = new CatDewormingDueGuard(repository);

        var valid = await guard.IsStillValidAsync(owner, cat.Id, null, dueOn);

        Assert.False(valid);
    }

    [Fact]
    public async Task DewormingGuard_CatTransferredToAnotherOwner_IsNotValidForTheOriginalOwner()
    {
        var originalOwner = Guid.NewGuid();
        var newOwner = Guid.NewGuid();
        var cat = NewCat(originalOwner);
        Deworm(cat, Today.AddDays(-90));
        var dueOn = cat.ParasiteTreatments.Single().NextDueOn;
        cat.TransferOwnership(new CatOwnershipTransferDto(newOwner, "Sold to a new owner", Ctx, Guid.NewGuid()));
        var repository = new FakeCatRepository();
        repository.Seed(cat);
        var guard = new CatDewormingDueGuard(repository);

        var validForOriginalOwner = await guard.IsStillValidAsync(originalOwner, cat.Id, null, dueOn);
        var validForNewOwner = await guard.IsStillValidAsync(newOwner, cat.Id, null, dueOn);

        Assert.False(validForOriginalOwner);
        Assert.True(validForNewOwner);
    }

    [Fact]
    public async Task DewormingGuard_TreatmentRenewedWithADifferentDueDate_IsNotValidForTheOldDueDate()
    {
        var owner = Guid.NewGuid();
        var cat = NewCat(owner);
        Deworm(cat, Today.AddDays(-90));
        var oldDueOn = cat.ParasiteTreatments.Single().NextDueOn;
        Deworm(cat, Today); // renewed today -- NextDueOn moves 90 days out
        var repository = new FakeCatRepository();
        repository.Seed(cat);
        var guard = new CatDewormingDueGuard(repository);

        var validForOldDueDate = await guard.IsStillValidAsync(owner, cat.Id, null, oldDueOn);

        Assert.False(validForOldDueDate);
    }
}
