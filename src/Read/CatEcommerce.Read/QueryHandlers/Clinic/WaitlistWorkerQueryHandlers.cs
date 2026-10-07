using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Clinic;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Clinic;

/// <summary>HO-APT-001 §8.11/§12.6 -- <c>ClinicWorker</c>'s own reads.</summary>
public sealed class GetWaitlistOfferCandidatesQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetWaitlistOfferCandidatesQuery, IReadOnlyList<Guid>>
{
    public async Task<QueryResponse<IReadOnlyList<Guid>>> HandleAsync(GetWaitlistOfferCandidatesQuery query)
    {
        var rows = await db.Set<WaitlistEntryAggregate>().AsNoTracking()
            .Where(w => !w.IsMarkedToDelete && (w.Status == WaitlistStatus.Waiting || w.Status == WaitlistStatus.Offered))
            .Select(w => new { w.BranchId, w.ClinicServiceId, w.LocalDate, w.Status, w.Id, w.CreatedOn })
            .ToListAsync();

        // For each branch/service/date group with no live offer, the oldest Waiting entry (queue order).
        var candidates = rows
            .GroupBy(w => (w.BranchId, w.ClinicServiceId, w.LocalDate))
            .Where(g => g.All(w => w.Status != WaitlistStatus.Offered))
            .Select(g => g.Where(w => w.Status == WaitlistStatus.Waiting).OrderBy(w => w.CreatedOn).FirstOrDefault())
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!.Id)
            .Take(WaitlistRules.MaxOfferGroupsPerPass)
            .ToList();

        return new QueryResponse<IReadOnlyList<Guid>> { Result = candidates };
    }
}

public sealed class GetLapsedWaitlistOffersQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetLapsedWaitlistOffersQuery, IReadOnlyList<Guid>>
{
    public async Task<QueryResponse<IReadOnlyList<Guid>>> HandleAsync(GetLapsedWaitlistOffersQuery query)
    {
        var ids = await db.Set<WaitlistEntryAggregate>().AsNoTracking()
            .Where(w => !w.IsMarkedToDelete && w.Status == WaitlistStatus.Offered && w.OfferExpiresOn != null && w.OfferExpiresOn <= query.Now)
            .Select(w => w.Id)
            .ToListAsync();

        return new QueryResponse<IReadOnlyList<Guid>> { Result = ids };
    }
}

/// <summary>CODE_REVIEW gate fix (2026-09-28, HIGH): "the day passed" (§8.11) is branch-local, not
/// a single global UTC date -- comparing every row's <c>LocalDate</c> against one UTC "today"
/// closed a UTC-10 branch's entries up to 10 h before its local day ended, and left a UTC+6
/// branch's entries open for up to 6 h after theirs genuinely had (ADR-045's own discipline: local
/// -&gt; instant/date conversion happens through <c>BranchCalendar</c>, nowhere else).
/// Left-joins each <c>Waiting</c> row to its branch's time zone in one round trip (no N+1 over rows or
/// branches; a missing branch falls back to UTC rather than dropping the row, backlog #20), then compares
/// each against <em>that</em> branch's own local date.</summary>
public sealed class GetPastWaitlistEntriesQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetPastWaitlistEntriesQuery, IReadOnlyList<Guid>>
{
    public async Task<QueryResponse<IReadOnlyList<Guid>>> HandleAsync(GetPastWaitlistEntriesQuery query)
    {
        var candidates = await (
            from w in db.Set<WaitlistEntryAggregate>().AsNoTracking()
            where !w.IsMarkedToDelete && w.Status == WaitlistStatus.Waiting
            join b in db.Set<BranchAggregate>().AsNoTracking() on w.BranchId equals b.Id into branches
            from b in branches.DefaultIfEmpty()
            select new { w.Id, w.LocalDate, TimeZoneId = b != null ? b.TimeZoneId : "UTC" }
        ).ToListAsync();

        var ids = candidates
            .Where(c => c.LocalDate < BranchCalendar.LocalDateOf(query.Now, c.TimeZoneId))
            .Select(c => c.Id)
            .Take(WaitlistRules.MaxClosedPerPass)
            .ToList();

        return new QueryResponse<IReadOnlyList<Guid>> { Result = ids };
    }
}
