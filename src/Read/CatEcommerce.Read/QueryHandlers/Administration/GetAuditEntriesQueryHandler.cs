using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Administration;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Administration;

/// <summary>HO-ADM-001 §12.6 -- filters in SQL on the indexed columns; actor names via a SQL-side
/// left join, projected directly (gate fix 2026-09-26: previously materialized every matching
/// aggregate, then ran two more queries in memory). Read-only.
///
/// Owner decision 2026-09-26: <c>BeforeValue</c>/<c>AfterValue</c>/<c>Reason</c> are redacted for a
/// clinical entity (<see cref="ClinicalAuditEntityTypes"/>, gate fix 2026-09-26 -- was a hard-coded
/// two-entry list that only covered the interceptor's own <c>DataChange</c> rows and never redacted
/// <c>Reason</c>, missing the four <c>Cat</c>-labelled <c>BusinessEvent</c> rows a high-risk flag,
/// clear, ownership transfer or merge reassignment writes) unless the caller also holds
/// <see cref="Actions.Cats.View"/> -- an Accountant using <c>AuditLog.View</c> sees who touched a
/// cat's record, when and from where, never the clinical content or the reason given for changing
/// it. The redaction is a placeholder string, not <c>null</c>, so it is never confused with a
/// genuinely absent value.</summary>
public sealed class GetAuditEntriesQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetAuditEntriesQuery, PagedResult<AuditEntryViewModel>>
{
    private const string RedactedPlaceholder = "[Redacted -- clinical record, requires Cats.View]";

    public async Task<QueryResponse<PagedResult<AuditEntryViewModel>>> HandleAsync(GetAuditEntriesQuery query)
    {
        var filtered = db.Set<AuditEntryAggregate>().AsNoTracking()
            .Where(e => e.OccurredOn >= query.From && e.OccurredOn <= query.To);

        if (query.ActorUserId is { } actorUserId) filtered = filtered.Where(e => e.ActorUserId == actorUserId);
        if (!string.IsNullOrWhiteSpace(query.EntityType)) filtered = filtered.Where(e => e.EntityType == query.EntityType);
        if (query.EntityId is { } entityId) filtered = filtered.Where(e => e.EntityId == entityId);
        if (!string.IsNullOrWhiteSpace(query.Action)) filtered = filtered.Where(e => e.Action == query.Action);
        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var category = Enumeration.FromDisplayName<AuditCategory>(query.Category);
            filtered = filtered.Where(e => e.Category == category);
        }

        var totalCount = await filtered.CountAsync();

        var projected =
            from e in filtered
            join s in db.StaffAccounts.AsNoTracking() on e.ActorUserId equals s.Id into staffJoin
            from s in staffJoin.DefaultIfEmpty()
            join c in db.CustomerAccounts.AsNoTracking() on e.ActorUserId equals c.Id into customerJoin
            from c in customerJoin.DefaultIfEmpty()
            orderby e.OccurredOn descending
            select new
            {
                e.OccurredOn,
                e.ActorUserId,
                StaffName = s != null ? s.FullName : null,
                CustomerName = c != null ? c.FullName : null,
                e.Action,
                e.Category,
                e.EntityType,
                e.EntityId,
                e.Reason,
                e.BeforeValue,
                e.AfterValue,
                e.IpAddress
            };

        var rows = await projected.Skip(query.Skip).Take(query.Take).ToListAsync();

        var canSeeClinicalValues = ActionRoles.TryGetRoles(Actions.Cats.View, out var catViewRoles) &&
            (query.UserContext.Roles ?? []).Intersect(catViewRoles, StringComparer.Ordinal).Any();

        var items = rows.Select(r =>
        {
            var redact = !canSeeClinicalValues && ClinicalAuditEntityTypes.Contains(r.EntityType);
            return new AuditEntryViewModel(
                r.OccurredOn,
                r.ActorUserId,
                ResolveName(r.ActorUserId, r.StaffName, r.CustomerName),
                r.Action,
                r.Category.DisplayName,
                r.EntityType,
                r.EntityId,
                redact && !string.IsNullOrEmpty(r.Reason) ? RedactedPlaceholder : r.Reason,
                redact && r.BeforeValue is not null ? RedactedPlaceholder : r.BeforeValue,
                redact && r.AfterValue is not null ? RedactedPlaceholder : r.AfterValue,
                r.IpAddress?.ToString());
        }).ToList();

        return new QueryResponse<PagedResult<AuditEntryViewModel>> { Result = new PagedResult<AuditEntryViewModel>(items, query.PageNumber, query.PageSize, totalCount) };
    }

    private static string ResolveName(Guid actorUserId, string? staffName, string? customerName)
    {
        if (actorUserId == Guid.Empty) return "Anonymous";
        return staffName ?? customerName ?? actorUserId.ToString();
    }
}
