using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Cat;
using CatEcommerce.Read.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Cat;

/// <summary>
/// Q7 -- feeds the QR endpoint (US-CAT-11).
///
/// Owner decision, 2026-09-27 (HO-CAT-001 §1 Amended, ACCEPTANCE gate A22): the QR target is
/// `/cats/{id}`, a "smart redirect" page the web site will build after HO-ACC-002 merges (staff ->
/// staff profile, the owner -> portal profile, anyone else -> sign-in) -- not built in this round.
/// The path itself is unchanged from before this fix; what changed is the base URL it is prefixed
/// with, now <see cref="PublicSiteOptions.WebBaseUrl"/> (was <c>CatProfileOptions.PublicWebBaseUrl</c>,
/// which nothing ever set).
/// </summary>
public sealed class GetCatCageCardQueryHandler(
    ILogger<GetCatCageCardQueryHandler> logger,
    CatEcommerceDbContext db,
    IOptions<PublicSiteOptions> options)
    : IQueryHandlerAsync<GetCatCageCardQuery, CatCageCardViewModel?>
{
    public async Task<QueryResponse<CatCageCardViewModel?>> HandleAsync(GetCatCageCardQuery query)
    {
        logger.LogInformation("Reading cage card for cat {CatId}", query.CatId);
        var row = await db.Cats.AsNoTracking()
            .Where(c => c.Id == query.CatId)
            .Select(c => new { c.Id, c.Name, c.OwnerId, c.IsHighRisk, c.HandlingNote })
            .FirstOrDefaultAsync();

        if (row is null)
            return new QueryResponse<CatCageCardViewModel?> { Result = null };

        var basePath = $"/cats/{row.Id}";
        var profileUrl = string.IsNullOrWhiteSpace(options.Value.WebBaseUrl)
            ? basePath
            : options.Value.WebBaseUrl.TrimEnd('/') + basePath;

        var viewModel = new CatCageCardViewModel(
            row.Id, row.Name, row.OwnerId, row.IsHighRisk, row.HandlingNote, profileUrl);

        return new QueryResponse<CatCageCardViewModel?> { Result = viewModel };
    }
}
