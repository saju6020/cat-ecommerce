using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Administration;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Administration;

/// <summary>A raw, unformatted content-item row -- a named type (not an anonymous one) so it can
/// cross the method boundary between the SQL-side projection and the in-memory formatting step
/// (gate fix 2026-09-26; every handler below previously loaded the full <c>ContentItemAggregate</c>
/// before mapping).</summary>
internal sealed record RawContentItem(
    Guid Id, ContentKind Kind, string Slug, string Title, string DraftBody, string? PublishedBody,
    DateTimeOffset? PublishedOn, ContentStatus Status, BannerPlacement? Placement, string? LinkUrl,
    DateTimeOffset? VisibleFrom, DateTimeOffset? VisibleTo, int? SortOrder);

public sealed class GetAdminContentQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetAdminContentQuery, IReadOnlyList<ContentItemViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<ContentItemViewModel>>> HandleAsync(GetAdminContentQuery query)
    {
        var filtered = db.Set<ContentItemAggregate>().AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Kind))
        {
            var kind = Enumeration.FromDisplayName<ContentKind>(query.Kind);
            filtered = filtered.Where(c => c.Kind == kind);
        }

        var rows = await filtered.OrderBy(c => c.Slug).Select(ToRaw).ToListAsync();
        return new QueryResponse<IReadOnlyList<ContentItemViewModel>> { Result = rows.Select(ToViewModel).ToList() };
    }

    internal static readonly System.Linq.Expressions.Expression<Func<ContentItemAggregate, RawContentItem>> ToRaw = c =>
        new RawContentItem(c.Id, c.Kind, c.Slug, c.Title, c.DraftBody, c.PublishedBody, c.PublishedOn, c.Status, c.Placement, c.LinkUrl, c.VisibleFrom, c.VisibleTo, c.SortOrder);

    internal static ContentItemViewModel ToViewModel(RawContentItem c) => new(
        c.Id, c.Kind.DisplayName, c.Slug, c.Title, c.DraftBody, c.PublishedBody, c.PublishedOn, c.Status.DisplayName,
        c.Placement?.DisplayName, c.LinkUrl, c.VisibleFrom, c.VisibleTo, c.SortOrder);
}

public sealed class GetAdminContentItemQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetAdminContentItemQuery, ContentItemViewModel?>
{
    public async Task<QueryResponse<ContentItemViewModel?>> HandleAsync(GetAdminContentItemQuery query)
    {
        var row = await db.Set<ContentItemAggregate>().AsNoTracking()
            .Where(c => c.Id == query.ContentItemId)
            .Select(GetAdminContentQueryHandler.ToRaw)
            .FirstOrDefaultAsync();

        return new QueryResponse<ContentItemViewModel?> { Result = row is null ? null : GetAdminContentQueryHandler.ToViewModel(row) };
    }
}

/// <summary>Published only -- returns the published body, never the draft (U-F1).</summary>
public sealed class GetPublishedContentQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetPublishedContentQuery, PublishedContentViewModel?>
{
    public async Task<QueryResponse<PublishedContentViewModel?>> HandleAsync(GetPublishedContentQuery query)
    {
        // The {kind} route segment is conventionally lower-case (e.g. "page"); ContentKind's
        // DisplayName is PascalCase ("Page") -- match case-insensitively rather than 500 on an
        // exact-match miss.
        var kind = Enumeration.GetAll<ContentKind>().FirstOrDefault(k => string.Equals(k.DisplayName, query.Kind, StringComparison.OrdinalIgnoreCase));
        if (kind is null) return new QueryResponse<PublishedContentViewModel?> { Result = null };

        var row = await db.Set<ContentItemAggregate>().AsNoTracking()
            .Where(c => c.Kind == kind && c.Slug == query.Slug && c.Status == ContentStatus.Published)
            .Select(c => new { c.Kind, c.Slug, c.Title, c.PublishedBody, c.PublishedOn, c.Placement, c.LinkUrl, c.SortOrder })
            .FirstOrDefaultAsync();

        if (row?.PublishedBody is null || row.PublishedOn is null) return new QueryResponse<PublishedContentViewModel?> { Result = null };

        return new QueryResponse<PublishedContentViewModel?>
        {
            Result = new PublishedContentViewModel(row.Kind.DisplayName, row.Slug, row.Title, row.PublishedBody, row.PublishedOn.Value, row.Placement?.DisplayName, row.LinkUrl, row.SortOrder)
        };
    }
}

public sealed class GetPublishedBannersQueryHandler(CatEcommerceDbContext db, TimeProvider timeProvider) : IQueryHandlerAsync<GetPublishedBannersQuery, IReadOnlyList<PublishedContentViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<PublishedContentViewModel>>> HandleAsync(GetPublishedBannersQuery query)
    {
        var now = timeProvider.GetUtcNow();
        var filtered = db.Set<ContentItemAggregate>().AsNoTracking()
            .Where(c => c.Kind == ContentKind.Banner && c.Status == ContentStatus.Published &&
                        (c.VisibleFrom == null || c.VisibleFrom <= now) && (c.VisibleTo == null || c.VisibleTo >= now));

        if (!string.IsNullOrWhiteSpace(query.Placement))
        {
            var placement = Enumeration.FromDisplayName<BannerPlacement>(query.Placement);
            filtered = filtered.Where(c => c.Placement == placement);
        }

        var rows = await filtered.OrderBy(c => c.SortOrder)
            .Select(c => new { c.Kind, c.Slug, c.Title, c.PublishedBody, c.PublishedOn, c.Placement, c.LinkUrl, c.SortOrder })
            .ToListAsync();

        var result = rows.Select(c => new PublishedContentViewModel(c.Kind.DisplayName, c.Slug, c.Title, c.PublishedBody!, c.PublishedOn!.Value, c.Placement?.DisplayName, c.LinkUrl, c.SortOrder)).ToList();

        return new QueryResponse<IReadOnlyList<PublishedContentViewModel>> { Result = result };
    }
}

public sealed class GetPublishedFaqsQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetPublishedFaqsQuery, IReadOnlyList<PublishedContentViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<PublishedContentViewModel>>> HandleAsync(GetPublishedFaqsQuery query)
    {
        var rows = await db.Set<ContentItemAggregate>().AsNoTracking()
            .Where(c => c.Kind == ContentKind.Faq && c.Status == ContentStatus.Published)
            .OrderBy(c => c.SortOrder)
            .Select(c => new { c.Kind, c.Slug, c.Title, c.PublishedBody, c.PublishedOn, c.SortOrder })
            .ToListAsync();

        var result = rows.Select(c => new PublishedContentViewModel(c.Kind.DisplayName, c.Slug, c.Title, c.PublishedBody!, c.PublishedOn!.Value, null, null, c.SortOrder)).ToList();
        return new QueryResponse<IReadOnlyList<PublishedContentViewModel>> { Result = result };
    }
}

public sealed class GetAdminPoliciesQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetAdminPoliciesQuery, IReadOnlyList<PolicyDocumentViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<PolicyDocumentViewModel>>> HandleAsync(GetAdminPoliciesQuery query)
    {
        // Gate fix 2026-09-26: previously ran one extra query per document (N+1) to fetch its
        // versions. A single query now projects each document's version list as a correlated
        // subquery, translated by EF into one SQL statement.
        var result = await db.Set<PolicyDocumentAggregate>().AsNoTracking().OrderBy(d => d.Key)
            .Take(AdministrationListCaps.MaxUnboundedListSize)
            .Select(d => new PolicyDocumentViewModel(
                d.Id, d.Key, d.Title, d.RequiresAcceptance, d.CurrentVersionNumber,
                db.Set<PolicyVersionAggregate>()
                    .Where(v => v.PolicyKey == d.Key)
                    .OrderByDescending(v => v.VersionNumber)
                    .Take(AdministrationListCaps.MaxUnboundedListSize)
                    .Select(v => new PolicyVersionSummaryViewModel(v.VersionNumber, v.PublishedOn, v.ChangeSummary))
                    .ToList()))
            .ToListAsync();

        return new QueryResponse<IReadOnlyList<PolicyDocumentViewModel>> { Result = result };
    }
}

public sealed class GetPublicPolicyQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetPublicPolicyQuery, PolicyVersionViewModel?>
{
    public async Task<QueryResponse<PolicyVersionViewModel?>> HandleAsync(GetPublicPolicyQuery query)
    {
        var title = await db.Set<PolicyDocumentAggregate>().AsNoTracking()
            .Where(d => d.Key == query.Key && d.CurrentVersionNumber != null)
            .Select(d => d.Title)
            .FirstOrDefaultAsync();

        if (title is null) return new QueryResponse<PolicyVersionViewModel?> { Result = null };

        // The read query uses MAX(version_number) so a lag in the document's pointer is never
        // visible (ADR-052).
        var result = await db.Set<PolicyVersionAggregate>().AsNoTracking()
            .Where(v => v.PolicyKey == query.Key)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new PolicyVersionViewModel(v.PolicyKey, v.VersionNumber, v.Body, v.PublishedOn, title))
            .FirstOrDefaultAsync();

        return new QueryResponse<PolicyVersionViewModel?> { Result = result };
    }
}

public sealed class GetPendingPolicyAcceptancesQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetPendingPolicyAcceptancesQuery, IReadOnlyList<PolicyVersionViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<PolicyVersionViewModel>>> HandleAsync(GetPendingPolicyAcceptancesQuery query)
    {
        // Gate fix 2026-09-26: previously ran two extra queries per RequiresAcceptance document
        // (an AnyAsync, then a FirstOrDefaultAsync) in a foreach loop. A single query now filters
        // out already-accepted documents with a correlated Any() and projects the pending
        // version's raw fields via a correlated subquery; the final ViewModel (and the null
        // filter for a document whose current version row does not exist) stays in-memory over
        // that already-narrow row set.
        var rows = await db.Set<PolicyDocumentAggregate>().AsNoTracking()
            .Where(d => d.RequiresAcceptance && d.CurrentVersionNumber != null)
            .Where(d => !db.Set<PolicyAcceptanceAggregate>().Any(a =>
                a.CustomerAccountId == query.CustomerAccountId && a.PolicyKey == d.Key && a.VersionNumber == d.CurrentVersionNumber))
            .Select(d => db.Set<PolicyVersionAggregate>()
                .Where(v => v.PolicyKey == d.Key && v.VersionNumber == d.CurrentVersionNumber)
                .Select(v => new { v.PolicyKey, v.VersionNumber, v.Body, v.PublishedOn, d.Title })
                .FirstOrDefault())
            .ToListAsync();

        var result = rows
            .Where(v => v is not null)
            .Select(v => new PolicyVersionViewModel(v!.PolicyKey, v.VersionNumber, v.Body, v.PublishedOn, v.Title))
            .ToList();

        return new QueryResponse<IReadOnlyList<PolicyVersionViewModel>> { Result = result };
    }
}

public sealed class GetSystemStatusQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetSystemStatusQuery, SystemStatusViewModel>
{
    public async Task<QueryResponse<SystemStatusViewModel>> HandleAsync(GetSystemStatusQuery query)
    {
        var status = await db.Set<SystemStatusAggregate>().AsNoTracking()
            .Where(s => s.Id == CatEcommerce.Shared.Common.Constants.SystemStatusIds.Singleton)
            .Select(s => new { s.MaintenanceMode, s.MaintenanceMessage })
            .FirstOrDefaultAsync();

        return new QueryResponse<SystemStatusViewModel> { Result = new SystemStatusViewModel(status?.MaintenanceMode ?? false, status?.MaintenanceMessage) };
    }
}
