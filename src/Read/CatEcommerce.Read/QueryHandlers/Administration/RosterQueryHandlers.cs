using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Administration;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Administration;

/// <summary>A raw, unformatted shift-slot row -- a named type (not an anonymous one) so it can
/// cross the method boundary between the SQL-side projection and the in-memory formatting step.</summary>
internal sealed record RawShiftSlot(DayOfWeek DayOfWeek, TimeOnly Starts, TimeOnly Ends, Guid? DefaultRoomId);

public sealed class GetBranchRostersQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetBranchRostersQuery, IReadOnlyList<StaffRosterViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<StaffRosterViewModel>>> HandleAsync(GetBranchRostersQuery query)
    {
        // Gate fix 2026-09-26: previously loaded the full StaffRosterAggregate (both owned shift
        // collections plus every base-aggregate column) before mapping. Projects both nested
        // collections (EF translates each to its own correlated subquery) in the same query.
        // AsSplitQuery(): two sibling collections in one projection would otherwise LEFT JOIN
        // against each other (a cross-product EF itself warns about), not just against the root.
        var rows = await db.Set<StaffRosterAggregate>().AsNoTracking().AsSplitQuery().Where(r => r.BranchId == query.BranchId)
            .Select(r => new
            {
                r.StaffAccountId, r.BranchId, r.NextPatternEffectiveFrom,
                CurrentPattern = r.CurrentPattern.Select(s => new RawShiftSlot(s.DayOfWeek, s.Starts, s.Ends, s.DefaultRoomId)).ToList(),
                NextPattern = r.NextPattern.Select(s => new RawShiftSlot(s.DayOfWeek, s.Starts, s.Ends, s.DefaultRoomId)).ToList()
            })
            .ToListAsync();

        var result = rows.Select(r => new StaffRosterViewModel(
            r.StaffAccountId, r.BranchId, ToSlots(r.CurrentPattern),
            r.NextPatternEffectiveFrom is null ? null : ToSlots(r.NextPattern), r.NextPatternEffectiveFrom)).ToList();

        return new QueryResponse<IReadOnlyList<StaffRosterViewModel>> { Result = result };
    }

    private static IReadOnlyList<ShiftSlotViewModel> ToSlots(IEnumerable<RawShiftSlot> slots) =>
        slots.Select(s => new ShiftSlotViewModel(s.DayOfWeek.ToString(), s.Starts.ToString("HH:mm"), s.Ends.ToString("HH:mm"), s.DefaultRoomId)).ToList();
}

public sealed class GetRosterExceptionsQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetRosterExceptionsQuery, IReadOnlyList<RosterExceptionViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<RosterExceptionViewModel>>> HandleAsync(GetRosterExceptionsQuery query)
    {
        var filtered = db.Set<RosterExceptionAggregate>().AsNoTracking().Where(e => e.BranchId == query.BranchId);
        if (query.From is { } from) filtered = filtered.Where(e => e.Date >= from);
        if (query.To is { } to) filtered = filtered.Where(e => e.Date <= to);

        var rows = await filtered.OrderBy(e => e.Date).Take(AdministrationListCaps.MaxUnboundedListSize)
            .Select(e => new { e.Id, e.StaffAccountId, e.BranchId, e.Date, e.Kind, e.Starts, e.Ends, e.Reason, e.IsCancelled })
            .ToListAsync();

        var result = rows.Select(e => new RosterExceptionViewModel(
            e.Id, e.StaffAccountId, e.BranchId, e.Date, e.Kind.DisplayName, e.Starts.ToString("HH:mm"), e.Ends.ToString("HH:mm"), e.Reason, e.IsCancelled)).ToList();

        return new QueryResponse<IReadOnlyList<RosterExceptionViewModel>> { Result = result };
    }
}

/// <summary>Loads inputs, calls the pure domain services (§12.6) -- never re-implements the
/// calendar or availability rules in SQL.</summary>
public sealed class GetStaffAvailabilityQueryHandler(CatEcommerceDbContext db, IBranchCalendar branchCalendar)
    : IQueryHandlerAsync<GetStaffAvailabilityQuery, IReadOnlyList<StaffAvailableIntervalViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<StaffAvailableIntervalViewModel>>> HandleAsync(GetStaffAvailabilityQuery query)
    {
        // Gate fix 2026-09-26 (CR LOW): only TimeZoneId is ever used below -- no need to load the
        // rest of BranchAggregate (weekly hours, code, contact details, base-aggregate columns).
        var timeZoneId = await db.Set<BranchAggregate>().AsNoTracking()
            .Where(b => b.Id == query.BranchId)
            .Select(b => b.TimeZoneId)
            .FirstOrDefaultAsync();
        if (timeZoneId is null) return new QueryResponse<IReadOnlyList<StaffAvailableIntervalViewModel>> { Result = [] };

        var openIntervals = await branchCalendar.OpenIntervalsAsync(query.BranchId, query.From, query.To, CancellationToken.None);

        var rosters = await db.Set<StaffRosterAggregate>().AsNoTracking().Where(r => r.BranchId == query.BranchId &&
                (query.StaffAccountId == null || r.StaffAccountId == query.StaffAccountId)).ToListAsync();

        var result = new List<StaffAvailableIntervalViewModel>();
        foreach (var roster in rosters)
        {
            var exceptions = await db.Set<RosterExceptionAggregate>().AsNoTracking()
                .Where(e => e.StaffAccountId == roster.StaffAccountId && e.BranchId == query.BranchId && !e.IsCancelled && e.Date >= query.From && e.Date <= query.To)
                .ToListAsync();

            var approvedLeave = await db.Set<LeaveRequestAggregate>().AsNoTracking()
                .Where(l => l.StaffAccountId == roster.StaffAccountId && l.Status == LeaveStatus.Approved && l.StartsOn <= query.To && query.From <= l.EndsOn)
                .ToListAsync();

            var intervals = CatEcommerce.Domain.DomainServices.StaffAvailability.Intervals(
                roster, exceptions, approvedLeave, openIntervals, timeZoneId, query.From, query.To, query.RestrictToOpeningHours);

            result.AddRange(intervals.Select(i => new StaffAvailableIntervalViewModel(i.StaffAccountId, i.BranchId, i.Start, i.End, i.DefaultRoomId, i.OutsideOpeningHours)));
        }

        return new QueryResponse<IReadOnlyList<StaffAvailableIntervalViewModel>> { Result = result };
    }
}

public sealed class GetMyLeaveRequestsQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetMyLeaveRequestsQuery, IReadOnlyList<LeaveRequestViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<LeaveRequestViewModel>>> HandleAsync(GetMyLeaveRequestsQuery query)
    {
        var leaves = await db.Set<LeaveRequestAggregate>().AsNoTracking()
            .Where(l => l.StaffAccountId == query.StaffAccountId)
            .OrderByDescending(l => l.StartsOn)
            .Take(AdministrationListCaps.MaxUnboundedListSize)
            .Select(ToViewModelExpression())
            .ToListAsync();

        return new QueryResponse<IReadOnlyList<LeaveRequestViewModel>> { Result = leaves };
    }

    internal static System.Linq.Expressions.Expression<Func<LeaveRequestAggregate, LeaveRequestViewModel>> ToViewModelExpression() => l =>
        new LeaveRequestViewModel(l.Id, l.StaffAccountId, l.StartsOn, l.EndsOn, l.Kind.DisplayName, l.Note, l.Status.DisplayName, l.DecidedBy, l.DecidedOn, l.DecisionNote);
}

public sealed class GetLeaveRequestsQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetLeaveRequestsQuery, PagedResult<LeaveRequestViewModel>>
{
    public async Task<QueryResponse<PagedResult<LeaveRequestViewModel>>> HandleAsync(GetLeaveRequestsQuery query)
    {
        var filtered = db.Set<LeaveRequestAggregate>().AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = Enumeration.FromDisplayName<LeaveStatus>(query.Status);
            filtered = filtered.Where(l => l.Status == status);
        }
        if (query.StaffAccountId is { } staffId) filtered = filtered.Where(l => l.StaffAccountId == staffId);
        if (query.From is { } from) filtered = filtered.Where(l => l.EndsOn >= from);
        if (query.To is { } to) filtered = filtered.Where(l => l.StartsOn <= to);

        var totalCount = await filtered.CountAsync();
        var items = await filtered.OrderByDescending(l => l.StartsOn).Skip(query.Skip).Take(query.Take)
            .Select(GetMyLeaveRequestsQueryHandler.ToViewModelExpression())
            .ToListAsync();

        return new QueryResponse<PagedResult<LeaveRequestViewModel>> { Result = new PagedResult<LeaveRequestViewModel>(items, query.PageNumber, query.PageSize, totalCount) };
    }
}
