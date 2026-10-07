using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Administration;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Administration;

/// <summary>A raw, unformatted opening interval row -- a named type (not an anonymous one) so the
/// projection below can be shared across handlers via <see cref="GetBranchesQueryHandler.ToOpeningIntervals"/>.</summary>
internal sealed record RawOpeningInterval(DayOfWeek DayOfWeek, TimeOnly Opens, TimeOnly Closes);

/// <summary>
/// Gate fix 2026-09-26: every handler below previously loaded the full <c>BranchAggregate</c> (or
/// list of them) via <c>ToListAsync</c>/<c>FirstOrDefaultAsync</c> with no projection, then mapped
/// to a ViewModel afterwards -- fetching every column the aggregate has, not just the ones a
/// ViewModel needs. Each now projects to a narrow, anonymous "raw row" shape (a database concern,
/// including the nested <c>WeeklyHours</c> owned collection, which EF translates to a correlated
/// subquery) in the same <c>Select</c> the query runs, and only formats (<c>TimeOnly.ToString</c>,
/// <c>DayOfWeek.ToString</c> -- neither translatable to SQL) in a second, already-narrow in-memory
/// step. Response shape is unchanged.
/// </summary>
public sealed class GetBranchesQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetBranchesQuery, IReadOnlyList<BranchSummaryViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<BranchSummaryViewModel>>> HandleAsync(GetBranchesQuery query)
    {
        var rows = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.IsActive).OrderBy(b => b.Name)
            .Select(b => new
            {
                b.Id, b.Name, b.AddressLine, b.PhoneNumber, b.Email, b.TimeZoneId,
                Hours = b.WeeklyHours.OrderBy(h => h.DayOfWeek).ThenBy(h => h.Opens)
                    .Select(h => new RawOpeningInterval(h.DayOfWeek, h.Opens, h.Closes))
                    .ToList()
            })
            .ToListAsync();

        var result = rows.Select(b => new BranchSummaryViewModel(
            b.Id, b.Name, b.AddressLine, b.PhoneNumber, b.Email, b.TimeZoneId, ToOpeningIntervals(b.Hours))).ToList();

        return new QueryResponse<IReadOnlyList<BranchSummaryViewModel>> { Result = result };
    }

    internal static IReadOnlyList<OpeningIntervalViewModel> ToOpeningIntervals(IEnumerable<RawOpeningInterval> hours) =>
        hours.Select(h => new OpeningIntervalViewModel(h.DayOfWeek.ToString(), h.Opens.ToString("HH:mm"), h.Closes.ToString("HH:mm"))).ToList();
}

public sealed class GetAdminBranchesQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetAdminBranchesQuery, IReadOnlyList<AdminBranchViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<AdminBranchViewModel>>> HandleAsync(GetAdminBranchesQuery query)
    {
        var rows = await db.Set<BranchAggregate>().AsNoTracking().OrderBy(b => b.Name)
            .Select(b => new
            {
                b.Id, b.Code, b.Name, b.TimeZoneId, b.AddressLine, b.PhoneNumber, b.Email, b.IsActive,
                Hours = b.WeeklyHours.OrderBy(h => h.DayOfWeek).ThenBy(h => h.Opens)
                    .Select(h => new RawOpeningInterval(h.DayOfWeek, h.Opens, h.Closes))
                    .ToList()
            })
            .ToListAsync();

        var result = rows.Select(b => new AdminBranchViewModel(
            b.Id, b.Code, b.Name, b.TimeZoneId, b.AddressLine, b.PhoneNumber, b.Email, b.IsActive,
            GetBranchesQueryHandler.ToOpeningIntervals(b.Hours))).ToList();

        return new QueryResponse<IReadOnlyList<AdminBranchViewModel>> { Result = result };
    }
}

public sealed class GetAdminBranchQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetAdminBranchQuery, AdminBranchViewModel?>
{
    public async Task<QueryResponse<AdminBranchViewModel?>> HandleAsync(GetAdminBranchQuery query)
    {
        var row = await db.Set<BranchAggregate>().AsNoTracking()
            .Where(b => b.Id == query.BranchId)
            .Select(b => new
            {
                b.Id, b.Code, b.Name, b.TimeZoneId, b.AddressLine, b.PhoneNumber, b.Email, b.IsActive,
                Hours = b.WeeklyHours.OrderBy(h => h.DayOfWeek).ThenBy(h => h.Opens)
                    .Select(h => new RawOpeningInterval(h.DayOfWeek, h.Opens, h.Closes))
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (row is null) return new QueryResponse<AdminBranchViewModel?> { Result = null };

        var result = new AdminBranchViewModel(
            row.Id, row.Code, row.Name, row.TimeZoneId, row.AddressLine, row.PhoneNumber, row.Email, row.IsActive,
            GetBranchesQueryHandler.ToOpeningIntervals(row.Hours));

        return new QueryResponse<AdminBranchViewModel?> { Result = result };
    }
}

public sealed class GetBranchClosuresQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetBranchClosuresQuery, IReadOnlyList<BranchClosureViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<BranchClosureViewModel>>> HandleAsync(GetBranchClosuresQuery query)
    {
        var filtered = db.Set<BranchClosureAggregate>().AsNoTracking().Where(c => c.BranchId == query.BranchId);
        if (query.From is { } from) filtered = filtered.Where(c => c.EndsOn >= from);
        if (query.To is { } to) filtered = filtered.Where(c => c.StartsOn <= to);

        var rows = await filtered.OrderBy(c => c.StartsOn).Take(AdministrationListCaps.MaxUnboundedListSize)
            .Select(c => new { c.Id, c.BranchId, c.StartsOn, c.EndsOn, c.Kind, c.Reason, c.PartialFrom, c.PartialTo, c.IsCancelled })
            .ToListAsync();

        var result = rows.Select(c => new BranchClosureViewModel(
            c.Id, c.BranchId, c.StartsOn, c.EndsOn, c.Kind.DisplayName, c.Reason,
            c.PartialFrom?.ToString("HH:mm"), c.PartialTo?.ToString("HH:mm"), c.IsCancelled)).ToList();

        return new QueryResponse<IReadOnlyList<BranchClosureViewModel>> { Result = result };
    }
}

public sealed class GetRoomsQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetRoomsQuery, IReadOnlyList<RoomViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<RoomViewModel>>> HandleAsync(GetRoomsQuery query)
    {
        var rooms = await db.Set<RoomAggregate>().AsNoTracking().Where(r => r.BranchId == query.BranchId).OrderBy(r => r.Name)
            .Select(r => new RoomViewModel(r.Id, r.BranchId, r.Name, r.Kind.DisplayName, r.IsActive))
            .ToListAsync();

        return new QueryResponse<IReadOnlyList<RoomViewModel>> { Result = rooms };
    }
}

public sealed class GetBranchCalendarQueryHandler(CatEcommerce.Domain.DomainServices.IBranchCalendar branchCalendar)
    : IQueryHandlerAsync<GetBranchCalendarQuery, IReadOnlyList<TimeIntervalViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<TimeIntervalViewModel>>> HandleAsync(GetBranchCalendarQuery query)
    {
        var intervals = await branchCalendar.OpenIntervalsAsync(query.BranchId, query.From, query.To, CancellationToken.None);
        var result = intervals.Select(i => new TimeIntervalViewModel(i.Start, i.End)).ToList();
        return new QueryResponse<IReadOnlyList<TimeIntervalViewModel>> { Result = result };
    }
}

public sealed class GetStaffBranchAssignmentQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetStaffBranchAssignmentQuery, StaffBranchAssignmentViewModel?>
{
    public async Task<QueryResponse<StaffBranchAssignmentViewModel?>> HandleAsync(GetStaffBranchAssignmentQuery query)
    {
        var result = await db.Set<StaffBranchAssignmentAggregate>().AsNoTracking()
            .Where(a => a.Id == query.StaffAccountId)
            .Select(a => new StaffBranchAssignmentViewModel(a.Id, a.BranchIds, a.PrimaryBranchId))
            .FirstOrDefaultAsync();

        return new QueryResponse<StaffBranchAssignmentViewModel?> { Result = result };
    }
}

public sealed class GetMyBranchesQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetMyBranchesQuery, StaffBranchAssignmentViewModel?>
{
    public async Task<QueryResponse<StaffBranchAssignmentViewModel?>> HandleAsync(GetMyBranchesQuery query)
    {
        var result = await db.Set<StaffBranchAssignmentAggregate>().AsNoTracking()
            .Where(a => a.Id == query.StaffAccountId)
            .Select(a => new StaffBranchAssignmentViewModel(a.Id, a.BranchIds, a.PrimaryBranchId))
            .FirstOrDefaultAsync();

        return new QueryResponse<StaffBranchAssignmentViewModel?> { Result = result };
    }
}
