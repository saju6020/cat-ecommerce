using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Boarding;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Boarding;

public sealed class GetReservationsReadyToConfirmQueryHandler(CatEcommerceDbContext db, IBoardingDepositLedger depositLedger)
    : IQueryHandlerAsync<GetReservationsReadyToConfirmQuery, IReadOnlyList<Guid>>
{
    public async Task<QueryResponse<IReadOnlyList<Guid>>> HandleAsync(GetReservationsReadyToConfirmQuery query)
    {
        // The ready test needs the ledger, which is not in this table: page the reserved stays (oldest first, ordered) and filter each page, so a paid reservation cannot starve behind
        // five hundred unpaid ones. Bounded: at most 20 pages of 500 per pass.
        var ready = new List<Guid>();
        for (var page = 0; page < 20 && ready.Count < 500; page++)
        {
            var reserved = await db.BoardingStays.AsNoTracking()
                .Where(s => s.Status == BoardingStayStatus.Reserved && s.DepositRequired > 0m)
                .OrderBy(s => s.ReservedOn).ThenBy(s => s.Id).Skip(page * 500).Take(500)
                .Select(s => new { s.Id, s.DepositRequired })
                .ToListAsync();
            if (reserved.Count == 0) break;

            var ledgers = await depositLedger.GetManyAsync(reserved.Select(r => r.Id).ToList());
            ready.AddRange(reserved
                .Where(r => ledgers.TryGetValue(r.Id, out var ledger) && (ledger.Accepted >= r.DepositRequired || ledger.PendingApproval > 0m))
                .Select(r => r.Id));
        }

        return new QueryResponse<IReadOnlyList<Guid>> { Result = ready };
    }
}

public sealed class GetExpiredReservationsQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetExpiredReservationsQuery, IReadOnlyList<Guid>>
{
    public async Task<QueryResponse<IReadOnlyList<Guid>>> HandleAsync(GetExpiredReservationsQuery query)
    {
        IReadOnlyList<Guid> ids = await db.BoardingStays.AsNoTracking()
            .Where(s => s.Status == BoardingStayStatus.Reserved && s.DepositDueBy != null && s.DepositDueBy <= query.Now)
            .OrderBy(s => s.DepositDueBy).Take(500).Select(s => s.Id).ToListAsync();

        return new QueryResponse<IReadOnlyList<Guid>> { Result = ids };
    }
}

public sealed class GetPastEnquiriesQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetPastEnquiriesQuery, IReadOnlyList<Guid>>
{
    public async Task<QueryResponse<IReadOnlyList<Guid>>> HandleAsync(GetPastEnquiriesQuery query)
    {
        // A day of slack either side of the UTC / branch-local difference; ExpireEnquiry decides on the branch-local date.
        var cutoff = DateOnly.FromDateTime(query.Now.UtcDateTime).AddDays(-1);
        IReadOnlyList<Guid> ids = await db.BoardingStays.AsNoTracking()
            .Where(s => s.Status == BoardingStayStatus.Enquiry && s.ArrivalOn <= cutoff)
            .OrderBy(s => s.ArrivalOn).Take(500).Select(s => s.Id).ToListAsync();

        return new QueryResponse<IReadOnlyList<Guid>> { Result = ids };
    }
}
