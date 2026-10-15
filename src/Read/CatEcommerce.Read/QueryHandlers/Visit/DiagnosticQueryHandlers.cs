using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Visit;
using CatEcommerce.Read.QueryHandlers.StoredFile;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Visit;

/// <summary>One result value as read: the columns only, projected from the owned row.</summary>
internal sealed record ResultValueRow(
    Guid SetId, Guid Id, string Name, ResultValueKind ValueKind, decimal? NumericValue, string? TextValue, string? Unit,
    decimal? ReferenceLow, decimal? ReferenceHigh, string? NormalText, ResultFlag Flag, int Position);

/// <summary>One result set as read, with its values already in <c>position</c> order.</summary>
internal sealed record ResultSetRow(
    Guid OrderId, Guid Id, DateOnly ReportedOn, string? Note, Guid? SupersedesResultSetId, Guid RecordedBy, DateTimeOffset RecordedOn)
{
    public List<ResultValueRow> Values { get; } = [];
}

/// <summary>One attachment as read.</summary>
internal sealed record AttachmentRow(Guid OrderId, Guid Id, Guid FileId, DateOnly ResultDate, string? Note, Guid AttachedBy, DateTimeOffset AttachedOn);

/// <summary>
/// Reads the result sets, result values and attachments of a set of orders as <b>projected rows</b> -- three bounded queries, never
/// the orders as aggregates (.ai/read-side-pattern.md rules 1-2, ADR-012). Shared by the staff order, the customer lab results
/// and the clinical-history lab section.
/// </summary>
internal static class DiagnosticRows
{
    public static async Task<Dictionary<Guid, List<ResultSetRow>>> ResultSetsAsync(CatEcommerceDbContext db, IReadOnlyCollection<Guid> orderIds, CancellationToken ct = default)
    {
        var result = orderIds.ToDictionary(id => id, _ => new List<ResultSetRow>());
        if (orderIds.Count == 0) return result;

        var orders = db.Set<DiagnosticOrderAggregate>().AsNoTracking().Where(o => orderIds.Contains(o.Id));

        var sets = await orders
            .SelectMany(o => o.ResultSets.Select(s => new ResultSetRow(o.Id, s.Id, s.ReportedOn, s.Note, s.SupersedesResultSetId, s.RecordedBy, s.RecordedOn)))
            .ToListAsync(ct);

        var values = await orders
            .SelectMany(o => o.ResultSets.SelectMany(s => s.Values.Select(v => new ResultValueRow(
                s.Id, v.Id, v.Name, v.ValueKind, v.NumericValue, v.TextValue, v.Unit, v.ReferenceLow, v.ReferenceHigh, v.NormalText, v.Flag, v.Position))))
            .ToListAsync(ct);

        var bySet = sets.ToDictionary(s => s.Id);
        foreach (var value in values.OrderBy(v => v.Position).ThenBy(v => v.Id))
        {
            if (bySet.TryGetValue(value.SetId, out var set)) set.Values.Add(value);
        }

        // Oldest first; a correction recorded in the same instant as the set it supersedes follows it; Id makes the order total.
        foreach (var set in sets.OrderBy(s => s.RecordedOn).ThenBy(s => s.SupersedesResultSetId != null).ThenBy(s => s.Id))
            result[set.OrderId].Add(set);

        return result;
    }

    public static async Task<Dictionary<Guid, List<AttachmentRow>>> AttachmentsAsync(CatEcommerceDbContext db, IReadOnlyCollection<Guid> orderIds, CancellationToken ct = default)
    {
        var result = orderIds.ToDictionary(id => id, _ => new List<AttachmentRow>());
        if (orderIds.Count == 0) return result;

        var rows = await db.Set<DiagnosticOrderAggregate>().AsNoTracking().Where(o => orderIds.Contains(o.Id))
            .SelectMany(o => o.Attachments.Select(a => new AttachmentRow(o.Id, a.Id, a.FileId, a.ResultDate, a.Note, a.AttachedBy, a.AttachedOn)))
            .ToListAsync(ct);

        foreach (var row in rows.OrderBy(a => a.AttachedOn).ThenBy(a => a.Id)) result[row.OrderId].Add(row);
        return result;
    }

    public static DiagnosticResultSetViewModel ToViewModel(ResultSetRow set, IReadOnlyDictionary<Guid, string> names) => new(
        set.Id, set.ReportedOn, set.Note, set.SupersedesResultSetId, StaffNames.Of(names, set.RecordedBy), set.RecordedOn,
        set.Values.Select(v => new DiagnosticResultValueViewModel(
            v.Id, v.Name, v.ValueKind.DisplayName, CatalogueFormat.Decimal(v.NumericValue), v.TextValue, v.Unit,
            CatalogueFormat.Decimal(v.ReferenceLow), CatalogueFormat.Decimal(v.ReferenceHigh), v.NormalText, v.Flag.DisplayName)).ToList());
}

/// <summary>§12.4 <c>GET /api/staff/diagnostics/{orderId}</c> -- the order, what was requested, every result set (a correction
/// is a later set naming the one it supersedes; the earlier one stays visible) and the attached files as signed,
/// short-lived URLs (NFR-SEC-5).</summary>
public sealed class GetDiagnosticOrderQueryHandler(CatEcommerceDbContext db, IStoredFileSigningBatcher signer)
    : IQueryHandlerAsync<GetDiagnosticOrderQuery, DiagnosticOrderViewModel?>
{
    public async Task<QueryResponse<DiagnosticOrderViewModel?>> HandleAsync(GetDiagnosticOrderQuery query)
    {
        var order = await db.Set<DiagnosticOrderAggregate>().AsNoTracking()
            .Where(o => o.Id == query.DiagnosticOrderId && o.BranchId == query.BranchId && !o.IsMarkedToDelete)
            .Select(o => new { o.Id, o.Version, o.VisitId, o.CatId, o.BranchId, o.Kind, o.Description, o.Status, o.OrderedOn, o.OrderedBy, o.CancelledReason })
            .FirstOrDefaultAsync();
        if (order is null) return new QueryResponse<DiagnosticOrderViewModel?> { Result = null };

        var catName = await db.Set<CatAggregate>().AsNoTracking().Where(c => c.Id == order.CatId).Select(c => c.Name).FirstOrDefaultAsync() ?? "";
        var timeZoneId = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == order.BranchId).Select(b => b.TimeZoneId).FirstOrDefaultAsync() ?? "UTC";

        var analytes = await db.Set<DiagnosticOrderAggregate>().AsNoTracking().Where(o => o.Id == order.Id)
            .SelectMany(o => o.RequestedAnalytes).OrderBy(a => a.Name).ThenBy(a => a.Id)
            .Select(a => new { a.AnalyteId, a.Code, a.Name }).ToListAsync();
        var orderIds = new[] { order.Id };
        var resultSets = (await DiagnosticRows.ResultSetsAsync(db, orderIds))[order.Id];
        var attachments = (await DiagnosticRows.AttachmentsAsync(db, orderIds))[order.Id];

        var staffIds = new List<Guid?> { order.OrderedBy };
        staffIds.AddRange(resultSets.Select(s => (Guid?)s.RecordedBy));
        staffIds.AddRange(attachments.Select(a => (Guid?)a.AttachedBy));
        var names = await StaffNames.LookupAsync(db, staffIds);

        var files = await signer.SignManyAsync(attachments.Select(a => a.FileId).ToList(), "inline");

        return new QueryResponse<DiagnosticOrderViewModel?>
        {
            Result = new DiagnosticOrderViewModel(
                order.Id, order.Version, order.VisitId, order.CatId, catName, order.Kind.DisplayName, order.Description, order.Status.DisplayName,
                order.OrderedOn, StaffNames.Of(names, order.OrderedBy), order.CancelledReason, timeZoneId,
                analytes.Select(a => new DiagnosticAnalyteViewModel(a.AnalyteId, a.Code, a.Name)).ToList(),
                resultSets.Select(s => DiagnosticRows.ToViewModel(s, names)).ToList(),
                attachments.Select(a => new DiagnosticAttachmentViewModel(a.Id, a.ResultDate, a.Note, StaffNames.Of(names, a.AttachedBy), a.AttachedOn,
                    files.TryGetValue(a.FileId, out var file) ? file : null)).ToList())
        };
    }
}

/// <summary>§12.4 <c>GET /api/staff/diagnostics?status=Pending</c> -- the active branch's orders by status, oldest first.</summary>
public sealed class GetPendingDiagnosticsQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetPendingDiagnosticsQuery, IReadOnlyList<DiagnosticOrderListItemViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<DiagnosticOrderListItemViewModel>>> HandleAsync(GetPendingDiagnosticsQuery query)
    {
        var status = EnumerationParsing.TryParse<DiagnosticOrderStatus>(query.Status, out var parsed) && parsed is not null ? parsed : DiagnosticOrderStatus.Pending;

        var rows = await (
            from o in db.Set<DiagnosticOrderAggregate>().AsNoTracking()
            where o.BranchId == query.BranchId && o.Status == status && !o.IsMarkedToDelete
            join c in db.Set<CatAggregate>().AsNoTracking() on o.CatId equals c.Id into cats
            from c in cats.DefaultIfEmpty()
            orderby o.OrderedOn
            select new { o.Id, o.VisitId, o.CatId, CatName = c != null ? c.Name : "", o.Kind, o.Status, o.OrderedOn, o.OrderedBy })
            .Take(500).ToListAsync();

        var names = await StaffNames.LookupAsync(db, rows.Select(r => (Guid?)r.OrderedBy));

        IReadOnlyList<DiagnosticOrderListItemViewModel> items = rows
            .Select(r => new DiagnosticOrderListItemViewModel(r.Id, r.VisitId, r.CatId, r.CatName, r.Kind.DisplayName, r.Status.DisplayName, r.OrderedOn, StaffNames.Of(names, r.OrderedBy)))
            .ToList();

        return new QueryResponse<IReadOnlyList<DiagnosticOrderListItemViewModel>> { Result = items };
    }
}
