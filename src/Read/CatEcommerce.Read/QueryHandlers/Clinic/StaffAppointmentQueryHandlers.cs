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
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Clinic;

public sealed class GetAppointmentDetailQueryHandler(CatEcommerceDbContext db, IBusinessSettings businessSettings, TimeProvider timeProvider)
    : IQueryHandlerAsync<GetAppointmentDetailQuery, AppointmentDetailViewModel?>
{
    public async Task<QueryResponse<AppointmentDetailViewModel?>> HandleAsync(GetAppointmentDetailQuery query)
    {
        var result = await ClinicDetailProjection.BuildStaffAsync(db, businessSettings, timeProvider, query.AppointmentId, query.BranchId);
        return new QueryResponse<AppointmentDetailViewModel?> { Result = result };
    }
}

/// <summary>NFR-AVL-3's input -- sorted by start.</summary>
public sealed class GetDayListQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetDayListQuery, IReadOnlyList<CalendarAppointmentViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<CalendarAppointmentViewModel>>> HandleAsync(GetDayListQuery query)
    {
        var branch = await db.Set<BranchAggregate>().AsNoTracking().FirstOrDefaultAsync(b => b.Id == query.BranchId);
        var timeZoneId = branch?.TimeZoneId ?? "UTC";
        // ADR-045: local-to-instant conversion happens only through BranchCalendar's own wrapper.
        var rangeStart = AppointmentSlotFinder.InstantOf(query.Date, TimeOnly.MinValue, timeZoneId);
        var rangeEnd = AppointmentSlotFinder.InstantOf(query.Date.AddDays(1), TimeOnly.MinValue, timeZoneId);

        // HO-SRG-001 section 11.7 G5: a surgery is shown on the calendar, never on this day list (it has its own theatre day list).
        var result = await QueryAsync(db, query.BranchId, rangeStart, rangeEnd, includeSurgeries: false);
        return new QueryResponse<IReadOnlyList<CalendarAppointmentViewModel>> { Result = result };
    }

    /// <summary>Left-joined once (cat, customer; a missing one shows "", never drops the row -- backlog #20) instead of one lookup per row (.ai/read-side-pattern.md
    /// §4 rules 1-2). The enum-backed fields (<c>Status</c>) are materialised raw and converted to
    /// their <c>DisplayName</c> only after <c>ToListAsync</c> -- matching every other handler in
    /// this file, since a projection that calls <c>.DisplayName</c> inside the <c>Select</c> itself
    /// does not translate to SQL.</summary>
    internal static async Task<List<CalendarAppointmentViewModel>> QueryAsync(
        CatEcommerceDbContext db, Guid branchId, DateTimeOffset rangeStart, DateTimeOffset rangeEnd, bool includeSurgeries)
    {
        var rows = await (
            from a in db.Set<AppointmentAggregate>().AsNoTracking()
            where !a.IsMarkedToDelete && a.BranchId == branchId && a.StartsOn != null && a.StartsOn >= rangeStart && a.StartsOn < rangeEnd
            where a.Status != AppointmentStatus.Held && a.Status != AppointmentStatus.HoldExpired && a.Status != AppointmentStatus.HoldReleased
            where includeSurgeries || a.SurgeryId == null
            join cat in db.Set<CatAggregate>().AsNoTracking() on a.CatId equals cat.Id into cats
            from cat in cats.DefaultIfEmpty()
            join customer in db.Set<CustomerAccountAggregate>().AsNoTracking() on a.CustomerId equals customer.Id into customers
            from customer in customers.DefaultIfEmpty()
            orderby a.StartsOn
            select new CalendarRow(
                a.Id, a.Version, a.CatId, cat != null ? cat.Name : "", cat != null && cat.IsHighRisk, a.CustomerId, customer != null ? customer.FullName : "",
                a.ServiceSnapshot.Name, a.StartsOn, a.EndsOn, a.PerformerId, a.RoomId, a.Status, a.IsWalkIn, a.NeedsRescheduling, a.SurgeryId, a.AssistantId)
        ).ToListAsync();

        return rows.Select(r => new CalendarAppointmentViewModel(
            r.Id, r.Version, r.CatId, r.CatName, r.CustomerId, r.CustomerName, r.ServiceName,
            r.StartsOn, r.EndsOn, r.PerformerId, r.RoomId, r.Status.DisplayName, r.IsWalkIn, r.NeedsRescheduling, r.CatIsHighRisk, r.SurgeryId, r.AssistantId)).ToList();
    }

    private sealed record CalendarRow(
        Guid Id, int Version, Guid CatId, string CatName, bool CatIsHighRisk, Guid CustomerId, string CustomerName,
        string ServiceName, DateTimeOffset? StartsOn, DateTimeOffset? EndsOn, Guid? PerformerId, Guid? RoomId,
        AppointmentStatus Status, bool IsWalkIn, bool NeedsRescheduling, Guid? SurgeryId, Guid? AssistantId);
}

/// <summary>§12.4 -- ≤ 7 days (<c>AppointmentRules.MaxCalendarRangeDays</c>, enforced by the
/// controller).</summary>
public sealed class GetCalendarQueryHandler(CatEcommerceDbContext db, IBusinessSettings businessSettings, IBranchCalendar branchCalendar)
    : IQueryHandlerAsync<GetCalendarQuery, CalendarViewModel>
{
    public async Task<QueryResponse<CalendarViewModel>> HandleAsync(GetCalendarQuery query)
    {
        var branch = await db.Set<BranchAggregate>().AsNoTracking().FirstOrDefaultAsync(b => b.Id == query.BranchId);
        var timeZoneId = branch?.TimeZoneId ?? "UTC";
        // ADR-045: local-to-instant conversion happens only through BranchCalendar's own wrapper.
        var rangeStart = AppointmentSlotFinder.InstantOf(query.From, TimeOnly.MinValue, timeZoneId);
        var rangeEnd = AppointmentSlotFinder.InstantOf(query.To.AddDays(1), TimeOnly.MinValue, timeZoneId);

        var gridMinutes = businessSettings.GetInt(SettingKeys.BookingSlotIntervalMinutes, query.BranchId);

        var appointments = await GetDayListQueryHandler.QueryAsync(db, query.BranchId, rangeStart, rangeEnd, includeSurgeries: true);

        if (query.Ids is { Count: > 0 } ids)
        {
            appointments = string.Equals(query.View, "room", StringComparison.OrdinalIgnoreCase)
                ? appointments.Where(a => a.RoomId != null && ids.Contains(a.RoomId.Value)).ToList()
                : appointments.Where(a => (a.PerformerId != null && ids.Contains(a.PerformerId.Value)) || (a.AssistantId != null && ids.Contains(a.AssistantId.Value))).ToList();
        }

        var closures = await db.Set<BranchClosureAggregate>().AsNoTracking()
            .Where(c => c.BranchId == query.BranchId && !c.IsCancelled && c.StartsOn <= query.To && c.EndsOn >= query.From)
            .Select(c => new CalendarClosureViewModel(c.StartsOn, c.EndsOn, c.Kind.DisplayName, c.Reason))
            .ToListAsync();

        IReadOnlyList<CalendarResourceViewModel> resources;
        var availability = new List<CalendarAvailabilityViewModel>();

        if (string.Equals(query.View, "room", StringComparison.OrdinalIgnoreCase))
        {
            var rooms = await db.Set<RoomAggregate>().AsNoTracking().Where(r => r.BranchId == query.BranchId).OrderBy(r => r.Name).ToListAsync();
            resources = rooms.Select(r => new CalendarResourceViewModel(r.Id, r.Name)).ToList();
        }
        else
        {
            var staffIds = await db.Set<StaffBranchAssignmentAggregate>().AsNoTracking()
                .Where(a => a.BranchIds.Contains(query.BranchId)).Select(a => a.Id).ToListAsync();

            var staffNames = new List<CalendarResourceViewModel>();
            var openIntervals = await branchCalendar.OpenIntervalsAsync(query.BranchId, query.From, query.To, CancellationToken.None);

            foreach (var staffId in staffIds)
            {
                var staff = await db.Set<StaffAccountAggregate>().AsNoTracking().FirstOrDefaultAsync(s => s.Id == staffId);
                var roster = await db.Set<StaffRosterAggregate>().AsNoTracking().FirstOrDefaultAsync(r => r.StaffAccountId == staffId && r.BranchId == query.BranchId);
                if (staff is null || roster is null) continue;

                staffNames.Add(new CalendarResourceViewModel(staffId, staff.FullName));

                var exceptions = await db.Set<RosterExceptionAggregate>().AsNoTracking()
                    .Where(e => e.StaffAccountId == staffId && e.BranchId == query.BranchId && !e.IsCancelled && e.Date >= query.From && e.Date <= query.To)
                    .ToListAsync();
                var approvedLeave = await db.Set<LeaveRequestAggregate>().AsNoTracking()
                    .Where(l => l.StaffAccountId == staffId && l.Status == LeaveStatus.Approved && l.StartsOn <= query.To && l.EndsOn >= query.From)
                    .ToListAsync();

                var intervals = StaffAvailability.Intervals(roster, exceptions, approvedLeave, openIntervals, timeZoneId, query.From, query.To, false);
                availability.AddRange(intervals.Select(i => new CalendarAvailabilityViewModel(staffId, i.Start, i.End, i.OutsideOpeningHours)));
            }

            resources = staffNames;
        }

        return new QueryResponse<CalendarViewModel> { Result = new CalendarViewModel(gridMinutes, resources, availability, closures, appointments) };
    }
}

public sealed class GetFlaggedAppointmentsQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetFlaggedAppointmentsQuery, IReadOnlyList<FlaggedAppointmentViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<FlaggedAppointmentViewModel>>> HandleAsync(GetFlaggedAppointmentsQuery query)
    {
        var rows = await db.Set<AppointmentAggregate>().AsNoTracking()
            .Where(a => !a.IsMarkedToDelete && a.BranchId == query.BranchId && a.NeedsRescheduling && a.Status == AppointmentStatus.Booked)
            .OrderBy(a => a.StartsOn)
            .Select(a => new { a.Id, a.CatId, a.StartsOn, a.RescheduleFlagReason, a.RescheduleFlagSourceId })
            .ToListAsync();

        var result = new List<FlaggedAppointmentViewModel>();
        foreach (var row in rows)
        {
            var catName = await db.Set<CatAggregate>().AsNoTracking().Where(c => c.Id == row.CatId).Select(c => c.Name).FirstOrDefaultAsync() ?? "";
            result.Add(new FlaggedAppointmentViewModel(row.Id, row.CatId, catName, row.StartsOn!.Value, row.RescheduleFlagReason?.DisplayName ?? "", row.RescheduleFlagSourceId));
        }

        return new QueryResponse<IReadOnlyList<FlaggedAppointmentViewModel>> { Result = result };
    }
}
