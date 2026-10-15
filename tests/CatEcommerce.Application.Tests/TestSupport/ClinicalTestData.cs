using System.Linq.Expressions;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.Tests.TestSupport;

/// <summary>HO-EMR-001 -- builders for the cross-aggregate facts a clinical handler loads (branch, cat,
/// appointment, open visit), always through each aggregate's own public methods.</summary>
internal static class ClinicalTestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero); // 15:00 in Dhaka

    public static BranchAggregate Branch(string timeZoneId = "Asia/Dhaka")
    {
        var branch = new BranchAggregate(Guid.NewGuid());
        branch.Create(new BranchCreateDto("B" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(), "Test branch", timeZoneId, "1 Road", "01700000000", null, TestUserContext.Create(), Guid.NewGuid()));
        return branch;
    }

    public static CatAggregate Cat(Guid? ownerId = null, bool deceased = false)
    {
        var cat = new CatAggregate(Guid.NewGuid());
        cat.Register(new CatRegistrationDto(
            ownerId ?? Guid.NewGuid(), "Mitu", "Cat", CatSex.Female, new DateOnly(2024, 1, 1), DateOfBirthPrecision.Exact,
            "Domestic Shorthair", "Tabby", new DateOnly(2026, 10, 7), TestUserContext.Create(), Guid.NewGuid()));

        if (deceased)
            cat.MarkDeceased(new CatDeceasedDto(new DateOnly(2026, 10, 6), null, new DateOnly(2026, 10, 7), TestUserContext.Create(), Guid.NewGuid()));

        return cat;
    }

    /// <summary>A walk-in appointment, which is created directly as <c>CheckedIn</c> (HO-APT-001 §8.3).</summary>
    public static AppointmentAggregate CheckedInAppointment(Guid branchId, CatAggregate cat)
    {
        var appointment = new AppointmentAggregate(Guid.NewGuid());
        appointment.RegisterWalkIn(new WalkInRegisterDto(
            branchId, cat.OwnerId, cat.Id, Guid.NewGuid(), new ServiceSnapshot("Consultation", ServiceCategory.Consultation, 30, new Money(500m, "BDT")),
            TriagePriority.Routine, null, Now, TestUserContext.Create(), Guid.NewGuid()));
        return appointment;
    }

    public static AppointmentAggregate BookedAppointment(Guid branchId, CatAggregate cat)
    {
        var appointment = new AppointmentAggregate(Guid.NewGuid());
        appointment.PlaceHold(new SlotHoldDto(
            branchId, cat.OwnerId, cat.Id, Guid.NewGuid(), new ServiceSnapshot("Consultation", ServiceCategory.Consultation, 30, new Money(500m, "BDT")),
            Guid.NewGuid(), Guid.NewGuid(), Now.AddHours(3), Now.AddHours(3.5), BookingChannel.Online, null, Now.AddMinutes(10),
            TestUserContext.Create(), Guid.NewGuid()));
        appointment.Confirm(new AppointmentConfirmDto(Now, "Annual check", TestUserContext.Create(), Guid.NewGuid()));
        return appointment;
    }

    public static VisitAggregate OpenVisit(Guid branchId, Guid appointmentId, Guid catId, UserContext? user = null)
    {
        var visit = new VisitAggregate(Guid.NewGuid());
        visit.Open(new VisitOpeningDto(
            branchId, appointmentId, catId, AppointmentStatus.CheckedIn, CatLifeStatus.Alive, Guid.NewGuid(), "Consultation",
            ServiceCategory.Consultation, Now, user ?? TestUserContext.Create(), Guid.NewGuid()));
        visit.ClearEvents();
        return visit;
    }
}

/// <summary>A repository that forwards to a <see cref="FakeAggregateRootRepository{T}"/> and appends each
/// call to a log shared across repositories, so a test can assert the <i>order</i> of the companion commit
/// (§8.1: the companion root is saved first; the visit is updated after).</summary>
internal sealed class LoggingRepository<TAggregate>(string name, List<string> log, FakeAggregateRootRepository<TAggregate> inner)
    : IAggregateRootRepository<TAggregate> where TAggregate : AggregateRoot
{
    public FakeAggregateRootRepository<TAggregate> Inner => inner;

    public Task<TAggregate> GetByIdAsync(Guid id) => inner.GetByIdAsync(id);
    public Task<TAggregate> GetByFilterAsync(Expression<Func<TAggregate, bool>> dataFilters) => inner.GetByFilterAsync(dataFilters);
    public Task<bool> ExistAsync(Expression<Func<TAggregate, bool>> dataFilters) => inner.ExistAsync(dataFilters);

    public Task SaveAsync(TAggregate aggregate, EventPublishOption eventPublishOption)
    {
        log.Add($"save:{name}");
        return inner.SaveAsync(aggregate, eventPublishOption);
    }

    public Task UpdateAsync(TAggregate aggregate, EventPublishOption eventPublishOption)
    {
        log.Add($"update:{name}");
        return inner.UpdateAsync(aggregate, eventPublishOption);
    }

    public Task UpdateAsync(TAggregate aggregate, int expectedVersion, EventPublishOption eventPublishOption)
    {
        log.Add($"update:{name}");
        return inner.UpdateAsync(aggregate, expectedVersion, eventPublishOption);
    }
}
