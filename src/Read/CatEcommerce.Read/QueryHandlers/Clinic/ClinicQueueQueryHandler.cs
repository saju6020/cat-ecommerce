using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Clinic;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Clinic;

/// <summary>HO-APT-001 §8.7 -- the queue is a read; nothing is stored. Recomputed on every call
/// (US-CLN-05 AC2).</summary>
public sealed class GetClinicQueueQueryHandler(CatEcommerceDbContext db, IBranchCalendar branchCalendar, TimeProvider timeProvider)
    : IQueryHandlerAsync<GetClinicQueueQuery, ClinicQueueViewModel>
{
    private static readonly string[] QueueRoles = [StaffRoles.Vet, StaffRoles.Nurse];

    public async Task<QueryResponse<ClinicQueueViewModel>> HandleAsync(GetClinicQueueQuery query)
    {
        var now = timeProvider.GetUtcNow();

        // Read side projects, never rehydrates (.ai/read-side-pattern.md §4): only the columns the
        // queue needs, left-joined once instead of one lookup per row (a missing cat or customer shows "", it never drops the row, backlog #20).
        var rows = await (
            from a in db.Set<AppointmentAggregate>().AsNoTracking()
            where !a.IsMarkedToDelete && a.BranchId == query.BranchId && (a.Status == AppointmentStatus.CheckedIn || a.Status == AppointmentStatus.InConsultation)
            join cat in db.Set<CatAggregate>().AsNoTracking() on a.CatId equals cat.Id into cats
            from cat in cats.DefaultIfEmpty()
            join customer in db.Set<CustomerAccountAggregate>().AsNoTracking() on a.CustomerId equals customer.Id into customers
            from customer in customers.DefaultIfEmpty()
            join performer in db.Set<StaffAccountAggregate>().AsNoTracking() on a.PerformerId equals performer.Id into performers
            from performer in performers.DefaultIfEmpty()
            select new QueueRow(
                a.Id, a.TriagePriority, a.IsWalkIn, a.StartsOn, a.CheckedInOn, a.PerformerId,
                a.ServiceSnapshot.DurationMinutes, a.Status, a.StartedOn, cat != null ? cat.Name : "", cat != null && cat.IsHighRisk,
                customer != null ? customer.FullName : "", a.ServiceSnapshot.Name, performer != null ? performer.FullName : null)
        ).ToListAsync();

        var byId = rows.ToDictionary(r => r.AppointmentId);
        var entries = rows.Select(r => new QueueEntryInput(
            r.AppointmentId, r.TriagePriority, r.IsWalkIn, r.StartsOn, r.CheckedInOn, r.PerformerId, r.DurationMinutes, r.Status, r.StartedOn)).ToList();

        var ordered = ClinicQueueOrdering.Order(entries);
        var filtered = query.PerformerId is { } performerId ? ClinicQueueOrdering.ForPerformer(ordered, performerId) : ordered;

        var performersOnShift = await CountPerformersOnShiftAsync(query.BranchId, now);
        var waits = ClinicQueueEstimator.EstimateWaitMinutes(ordered, performersOnShift, now);

        var viewEntries = new List<ClinicQueueEntryViewModel>();
        var position = 0;
        foreach (var entry in filtered)
        {
            position++;
            var row = byId[entry.AppointmentId];

            viewEntries.Add(new ClinicQueueEntryViewModel(
                row.AppointmentId, position, row.CatName, row.CatIsHighRisk, row.CustomerName, row.ServiceName,
                row.TriagePriority.DisplayName, row.IsWalkIn, ClinicQueueOrdering.DueOn(entry), row.CheckedInOn,
                row.Status.DisplayName, row.PerformerName, waits.TryGetValue(row.AppointmentId, out var wait) ? wait : null));
        }

        return new QueryResponse<ClinicQueueViewModel> { Result = new ClinicQueueViewModel(now, performersOnShift, viewEntries) };
    }

    private sealed record QueueRow(
        Guid AppointmentId, TriagePriority TriagePriority, bool IsWalkIn, DateTimeOffset? StartsOn,
        DateTimeOffset? CheckedInOn, Guid? PerformerId, int DurationMinutes, AppointmentStatus Status,
        DateTimeOffset? StartedOn, string CatName, bool CatIsHighRisk, string CustomerName, string ServiceName,
        string? PerformerName);

    private async Task<int> CountPerformersOnShiftAsync(Guid branchId, DateTimeOffset now)
    {
        var branch = await db.Set<BranchAggregate>().AsNoTracking().FirstOrDefaultAsync(b => b.Id == branchId);
        if (branch is null) return 0;

        var today = BranchCalendar.LocalDateOf(now, branch.TimeZoneId);
        var staffIds = await db.Set<StaffBranchAssignmentAggregate>().AsNoTracking().Where(a => a.BranchIds.Contains(branchId)).Select(a => a.Id).ToListAsync();

        var openIntervals = await branchCalendar.OpenIntervalsAsync(branchId, today, today, CancellationToken.None);
        var count = 0;

        foreach (var staffId in staffIds)
        {
            var staff = await db.Set<StaffAccountAggregate>().AsNoTracking().FirstOrDefaultAsync(s => s.Id == staffId);
            if (staff is null || !staff.RoleIds.Any(r => QueueRoles.Contains(r, StringComparer.Ordinal))) continue;

            var roster = await db.Set<StaffRosterAggregate>().AsNoTracking().FirstOrDefaultAsync(r => r.StaffAccountId == staffId && r.BranchId == branchId);
            if (roster is null) continue;

            var exceptions = await db.Set<RosterExceptionAggregate>().AsNoTracking()
                .Where(e => e.StaffAccountId == staffId && e.BranchId == branchId && !e.IsCancelled && e.Date == today).ToListAsync();
            var approvedLeave = await db.Set<LeaveRequestAggregate>().AsNoTracking()
                .Where(l => l.StaffAccountId == staffId && l.Status == LeaveStatus.Approved && l.StartsOn <= today && today <= l.EndsOn).ToListAsync();

            var intervals = StaffAvailability.Intervals(roster, exceptions, approvedLeave, openIntervals, branch.TimeZoneId, today, today, false);
            if (intervals.Any(i => i.Start <= now && now < i.End)) count++;
        }

        return count;
    }
}
