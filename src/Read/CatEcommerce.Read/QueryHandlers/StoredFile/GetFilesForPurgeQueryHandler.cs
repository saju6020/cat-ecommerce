using CatEcommerce.Domain.Aggregates.StoredFile;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.StoredFile;
using CatEcommerce.Read.ViewModels;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.StoredFile;

/// <summary>Gate-fix 2026-09-27 (TEST gate, U11 weak-test finding): the eligibility half of this
/// preview's own predicate is now <see cref="StoredFileAggregate.IsEligibleForPurge"/>, the same
/// expression <c>StoredFilePurgeSource</c> passes to EF Core for the real purge, rather than a
/// second, independently hand-duplicated copy of the same condition -- see that method's own doc
/// comment.</summary>
public sealed class GetFilesForPurgeQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetFilesForPurgeQuery, IReadOnlyList<PurgeEligibleFileViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<PurgeEligibleFileViewModel>>> HandleAsync(GetFilesForPurgeQuery query)
    {
        var today = DateOnly.FromDateTime(query.Cutoff.UtcDateTime);

        var items = await db.StoredFiles.AsNoTracking()
            .Where(f => f.UploadedOn <= query.Cutoff)
            .Where(StoredFileAggregate.IsEligibleForPurge(today))
            .OrderBy(f => f.UploadedOn)
            .Skip(query.Skip).Take(query.Take)
            .Select(f => new PurgeEligibleFileViewModel(f.Id, f.Purpose.DisplayName, f.SizeInBytes, f.UploadedOn))
            .ToListAsync();

        return new QueryResponse<IReadOnlyList<PurgeEligibleFileViewModel>> { Result = items };
    }
}
