using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Clinic;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Queries;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// Backlog rows #9 (finders and Clinic reads skip soft-deleted rows) and #20 (a row whose cat,
/// customer or branch is missing is still listed, with safe fallbacks, instead of being dropped by
/// an inner join) -- docs/backlog/APT.md, HO-APT-001. Rows are seeded straight through the
/// repositories with fresh random ids, so nothing here depends on, or disturbs, other tests' data;
/// the queries are dispatched in-process, as the controllers do.
/// </summary>
public sealed class AppointmentReadResilienceTests(AppointmentsApiFactory factory) : AppointmentsIntegrationTestBase(factory)
{
    private static readonly UserContext Caller = new(Guid.NewGuid(), Guid.NewGuid());

    private static ServiceSnapshot Snapshot() => new("Consultation", ServiceCategory.Consultation, 30, new Money(500m, "BDT"));

    private static async Task<Guid> SeedBookedAsync(IServiceProvider services, Guid branchId, Guid customerId, Guid catId, DateTimeOffset startsOn)
    {
        var repository = services.GetRequiredService<IAggregateRootRepository<AppointmentAggregate>>();
        var appointment = new AppointmentAggregate(Guid.NewGuid());
        appointment.BookDirect(new DirectBookingDto(
            branchId, customerId, catId, Guid.NewGuid(), Snapshot(), Guid.NewGuid(), Guid.NewGuid(),
            startsOn, startsOn.AddMinutes(30), "Checkup", Caller, Guid.NewGuid()));
        await repository.SaveAsync(appointment, EventPublishOption.InMemoryPublishOnly);
        return appointment.Id;
    }

    private static async Task MarkDeletedAsync<TAggregate>(IServiceProvider services, Guid id) where TAggregate : AggregateRoot
    {
        var db = services.GetRequiredService<CatEcommerceDbContext>();
        var aggregate = await db.Set<TAggregate>().FirstAsync(a => a.Id == id);
        db.Entry(aggregate).Property(nameof(AggregateRoot.IsMarkedToDelete)).CurrentValue = true;
        await db.SaveChangesAsync();
    }

    private static async Task<TResult> AskAsync<TResult>(IServiceProvider services, Query<TResult> query)
    {
        var dispatcher = services.GetRequiredService<IInMemoryDispatcher>();
        return (await dispatcher.GetResultAsync(query)).Result!;
    }

    /// <summary>#20 -- the day list/calendar keeps an appointment whose cat and customer are gone.</summary>
    [Fact]
    public async Task DayList_KeepsAnAppointmentWhoseCatAndCustomerAreMissing_WithEmptyNames()
    {
        var branchId = Guid.NewGuid();
        var startsOn = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(120).AddHours(9), TimeSpan.Zero);

        await using var scope = Factory.Services.CreateAsyncScope();
        var appointmentId = await SeedBookedAsync(scope.ServiceProvider, branchId, Guid.NewGuid(), Guid.NewGuid(), startsOn);

        var list = await AskAsync(scope.ServiceProvider, new GetDayListQuery
        { BranchId = branchId, Date = DateOnly.FromDateTime(startsOn.UtcDateTime), UserContext = Caller, CorrelationId = Guid.NewGuid() });

        var row = Assert.Single(list);
        Assert.Equal(appointmentId, row.Id);
        Assert.Equal("", row.CatName);
        Assert.Equal("", row.CustomerName);
        Assert.False(row.CatIsHighRisk);
    }

    /// <summary>#9 -- the day list never shows a soft-deleted appointment.</summary>
    [Fact]
    public async Task DayList_OmitsSoftDeletedAppointments()
    {
        var branchId = Guid.NewGuid();
        var startsOn = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(121).AddHours(9), TimeSpan.Zero);

        await using var scope = Factory.Services.CreateAsyncScope();
        var kept = await SeedBookedAsync(scope.ServiceProvider, branchId, Guid.NewGuid(), Guid.NewGuid(), startsOn);
        var deleted = await SeedBookedAsync(scope.ServiceProvider, branchId, Guid.NewGuid(), Guid.NewGuid(), startsOn.AddHours(1));
        await MarkDeletedAsync<AppointmentAggregate>(scope.ServiceProvider, deleted);

        var list = await AskAsync(scope.ServiceProvider, new GetDayListQuery
        { BranchId = branchId, Date = DateOnly.FromDateTime(startsOn.UtcDateTime), UserContext = Caller, CorrelationId = Guid.NewGuid() });

        Assert.Equal([kept], list.Select(a => a.Id));
    }

    /// <summary>#20 -- the clinic queue keeps a checked-in visit whose cat and customer are missing.</summary>
    [Fact]
    public async Task Queue_KeepsAWalkInWhoseCatAndCustomerAreMissing_WithEmptyNames()
    {
        var branchId = Guid.NewGuid();

        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<AppointmentAggregate>>();
        var walkIn = new AppointmentAggregate(Guid.NewGuid());
        walkIn.RegisterWalkIn(new WalkInRegisterDto(
            branchId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Snapshot(), TriagePriority.Routine, null,
            DateTimeOffset.UtcNow, Caller, Guid.NewGuid()));
        await repository.SaveAsync(walkIn, EventPublishOption.InMemoryPublishOnly);

        var queue = await AskAsync(scope.ServiceProvider, new GetClinicQueueQuery
        { BranchId = branchId, UserContext = Caller, CorrelationId = Guid.NewGuid() });

        var entry = Assert.Single(queue.Entries);
        Assert.Equal(walkIn.Id, entry.AppointmentId);
        Assert.Equal("", entry.CatName);
        Assert.Equal("", entry.CustomerName);
    }

    /// <summary>#9 -- the queue never shows a soft-deleted visit.</summary>
    [Fact]
    public async Task Queue_OmitsSoftDeletedVisits()
    {
        var branchId = Guid.NewGuid();

        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<AppointmentAggregate>>();
        var walkIn = new AppointmentAggregate(Guid.NewGuid());
        walkIn.RegisterWalkIn(new WalkInRegisterDto(
            branchId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Snapshot(), TriagePriority.Routine, null,
            DateTimeOffset.UtcNow, Caller, Guid.NewGuid()));
        await repository.SaveAsync(walkIn, EventPublishOption.InMemoryPublishOnly);
        await MarkDeletedAsync<AppointmentAggregate>(scope.ServiceProvider, walkIn.Id);

        var queue = await AskAsync(scope.ServiceProvider, new GetClinicQueueQuery
        { BranchId = branchId, UserContext = Caller, CorrelationId = Guid.NewGuid() });

        Assert.Empty(queue.Entries);
    }

    /// <summary>#20 -- the customer's "visible at booking" summary keeps an upcoming appointment whose
    /// cat and branch are missing (branch name empty, zone UTC).</summary>
    [Fact]
    public async Task CustomerSummary_KeepsAnUpcomingAppointmentWhoseCatAndBranchAreMissing()
    {
        var customerId = Guid.NewGuid();

        await using var scope = Factory.Services.CreateAsyncScope();
        var appointmentId = await SeedBookedAsync(scope.ServiceProvider, Guid.NewGuid(), customerId, Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(122));

        var summary = await AskAsync(scope.ServiceProvider, new GetCustomerAppointmentSummaryQuery
        { CustomerId = customerId, UserContext = Caller, CorrelationId = Guid.NewGuid() });

        var upcoming = Assert.Single(summary.Upcoming);
        Assert.Equal(appointmentId, upcoming.Id);
        Assert.Equal("", upcoming.CatName);
        Assert.Equal("", upcoming.Branch.Name);
        Assert.Equal("UTC", upcoming.BranchTimeZoneId);
    }

    /// <summary>#9 -- the customer's list, summary and the customer-id finder all skip a soft-deleted
    /// appointment.</summary>
    [Fact]
    public async Task CustomerReads_AndFinder_SkipSoftDeletedAppointments()
    {
        var customerId = Guid.NewGuid();

        await using var scope = Factory.Services.CreateAsyncScope();
        var kept = await SeedBookedAsync(scope.ServiceProvider, Guid.NewGuid(), customerId, Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(123));
        var deleted = await SeedBookedAsync(scope.ServiceProvider, Guid.NewGuid(), customerId, Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(124));
        await MarkDeletedAsync<AppointmentAggregate>(scope.ServiceProvider, deleted);

        var list = await AskAsync(scope.ServiceProvider, new GetCustomerAppointmentsQuery
        { CustomerId = customerId, Scope = "upcoming", UserContext = Caller, CorrelationId = Guid.NewGuid() });
        Assert.Equal([kept], list.Items.Select(a => a.Id));

        var summary = await AskAsync(scope.ServiceProvider, new GetCustomerAppointmentSummaryQuery
        { CustomerId = customerId, UserContext = Caller, CorrelationId = Guid.NewGuid() });
        Assert.Equal([kept], summary.Upcoming.Select(a => a.Id));

        var finder = scope.ServiceProvider.GetRequiredService<IFlaggableAppointmentFinder>();
        Assert.Equal([kept], await finder.FindByCustomerAsync(customerId));
    }

    /// <summary>#9 -- the other flagging finders skip a soft-deleted appointment too.</summary>
    [Fact]
    public async Task FlaggingFinders_SkipSoftDeletedAppointments()
    {
        var branchId = Guid.NewGuid();
        var catId = Guid.NewGuid();
        var startsOn = DateTimeOffset.UtcNow.AddDays(125);

        await using var scope = Factory.Services.CreateAsyncScope();
        var kept = await SeedBookedAsync(scope.ServiceProvider, branchId, Guid.NewGuid(), catId, startsOn);
        var deleted = await SeedBookedAsync(scope.ServiceProvider, branchId, Guid.NewGuid(), catId, startsOn.AddHours(2));
        await MarkDeletedAsync<AppointmentAggregate>(scope.ServiceProvider, deleted);

        var finder = scope.ServiceProvider.GetRequiredService<IFlaggableAppointmentFinder>();
        var localDate = DateOnly.FromDateTime(startsOn.UtcDateTime);

        Assert.Equal([kept], await finder.FindFutureByCatAsync(catId, DateTimeOffset.UtcNow));

        var byBranch = await finder.FindByBranchInRangeAsync(branchId, localDate.AddDays(-1), localDate.AddDays(1), null, null);
        Assert.Equal([kept], byBranch);
    }

    /// <summary>#9 and #20 -- waitlist: the customer's list and the finder skip a soft-deleted entry;
    /// the worker's past-date pass still sees an entry whose branch is missing (UTC fallback).</summary>
    [Fact]
    public async Task Waitlist_ReadsAndFinder_SkipSoftDeleted_AndPastPassKeepsAMissingBranchsEntry()
    {
        var customerId = Guid.NewGuid();
        var pastDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30);

        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<WaitlistEntryAggregate>>();

        async Task<Guid> JoinAsync()
        {
            var entry = new WaitlistEntryAggregate(Guid.NewGuid());
            entry.Join(new WaitlistJoinDto(Guid.NewGuid(), customerId, Guid.NewGuid(), Guid.NewGuid(), pastDate, null, DateTimeOffset.UtcNow, Caller, Guid.NewGuid()));
            await repository.SaveAsync(entry, EventPublishOption.InMemoryPublishOnly);
            return entry.Id;
        }

        var kept = await JoinAsync();
        var deleted = await JoinAsync();
        await MarkDeletedAsync<WaitlistEntryAggregate>(scope.ServiceProvider, deleted);

        var mine = await AskAsync(scope.ServiceProvider, new GetCustomerWaitlistQuery
        { CustomerId = customerId, UserContext = Caller, CorrelationId = Guid.NewGuid() });
        Assert.Equal([kept], mine.Select(w => w.Id));

        var finder = scope.ServiceProvider.GetRequiredService<IWaitlistEntryFinder>();
        Assert.Equal([kept], await finder.FindByCustomerAsync(customerId));

        // The entry's branch does not exist; an inner join would never have offered it for closing.
        // Other tests' rows share this global query, so look for ours among a generous page.
        var past = await AskAsync(scope.ServiceProvider, new GetPastWaitlistEntriesQuery
        { Now = DateTimeOffset.UtcNow, UserContext = Caller, CorrelationId = Guid.NewGuid() });
        Assert.DoesNotContain(deleted, past);

        // Another test's ClinicWorkCycle (the suite runs test classes in parallel against one
        // database) may already have closed our entry -- which could only happen if this very query
        // had offered it. Either way it proves the branchless entry is not dropped.
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var status = await db.Set<WaitlistEntryAggregate>().AsNoTracking().Where(w => w.Id == kept).Select(w => w.Status).SingleAsync();
        Assert.True(past.Contains(kept) || status == WaitlistStatus.Closed, $"The past-date pass skipped the branchless entry (status {status}).");
    }
}
