using System.Globalization;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Visit;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Visit;

// HO-EMR-001 §12.6 -- catalogue reads. AsNoTracking + explicit projection; enumeration-backed
// columns are materialised raw and mapped to their DisplayName afterwards (a DisplayName call
// inside Select does not translate to SQL).

internal static class CatalogueFormat
{
    public static string? Decimal(decimal? value) => value?.ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary>Escapes LIKE wildcards so a search for a percent sign is literal.</summary>
    public static string LikePattern(string search) =>
        "%" + search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
}

public sealed class GetDiagnosisCodesQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetDiagnosisCodesQuery, PagedResult<DiagnosisCodeViewModel>>
{
    public async Task<QueryResponse<PagedResult<DiagnosisCodeViewModel>>> HandleAsync(GetDiagnosisCodesQuery query)
    {
        var filtered = db.Set<DiagnosisCodeAggregate>().AsNoTracking().Where(c => !c.IsMarkedToDelete);
        if (query.ActiveOnly) filtered = filtered.Where(c => c.IsActive);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = CatalogueFormat.LikePattern(query.Search);
            filtered = filtered.Where(c => EF.Functions.ILike(c.Code, pattern, "\\") || EF.Functions.ILike(c.Name, pattern, "\\"));
        }

        var total = await filtered.CountAsync();
        var rows = await filtered.OrderBy(c => c.Code).Skip(query.Skip).Take(query.Take)
            .Select(c => new { c.Id, c.Code, c.Name, c.Category, c.ExternalSystem, c.ExternalCode, c.IsActive, c.Version })
            .ToListAsync();

        var items = rows.Select(r => new DiagnosisCodeViewModel(
            r.Id, r.Code, r.Name, r.Category, r.ExternalSystem, r.ExternalCode, r.IsActive, DiagnosisCodeSeed.IsSystemCode(r.Code), r.Version)).ToList();

        return new QueryResponse<PagedResult<DiagnosisCodeViewModel>> { Result = new PagedResult<DiagnosisCodeViewModel>(items, query.PageNumber, query.PageSize, total) };
    }
}

public sealed class GetDiagnosisCodeQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetDiagnosisCodeQuery, DiagnosisCodeViewModel?>
{
    public async Task<QueryResponse<DiagnosisCodeViewModel?>> HandleAsync(GetDiagnosisCodeQuery query)
    {
        var r = await db.Set<DiagnosisCodeAggregate>().AsNoTracking().Where(c => c.Id == query.DiagnosisCodeId && !c.IsMarkedToDelete)
            .Select(c => new { c.Id, c.Code, c.Name, c.Category, c.ExternalSystem, c.ExternalCode, c.IsActive, c.Version })
            .FirstOrDefaultAsync();

        return new QueryResponse<DiagnosisCodeViewModel?>
        {
            Result = r is null ? null : new DiagnosisCodeViewModel(r.Id, r.Code, r.Name, r.Category, r.ExternalSystem, r.ExternalCode, r.IsActive, DiagnosisCodeSeed.IsSystemCode(r.Code), r.Version)
        };
    }
}

public sealed class GetLabAnalytesQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetLabAnalytesQuery, PagedResult<LabAnalyteViewModel>>
{
    public async Task<QueryResponse<PagedResult<LabAnalyteViewModel>>> HandleAsync(GetLabAnalytesQuery query)
    {
        var filtered = db.Set<LabAnalyteAggregate>().AsNoTracking().Where(a => !a.IsMarkedToDelete);
        if (query.ActiveOnly) filtered = filtered.Where(a => a.IsActive);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = CatalogueFormat.LikePattern(query.Search);
            filtered = filtered.Where(a => EF.Functions.ILike(a.Code, pattern, "\\") || EF.Functions.ILike(a.Name, pattern, "\\"));
        }

        var total = await filtered.CountAsync();
        var rows = await filtered.OrderBy(a => a.Code).Skip(query.Skip).Take(query.Take)
            .Select(a => new { a.Id, a.Code, a.Name, a.ValueKind, a.DiagnosticKind, a.Unit, a.ReferenceLow, a.ReferenceHigh, a.NormalText, a.IsActive, a.Version })
            .ToListAsync();

        var items = rows.Select(r => new LabAnalyteViewModel(
            r.Id, r.Code, r.Name, r.ValueKind.DisplayName, r.DiagnosticKind.DisplayName, r.Unit,
            CatalogueFormat.Decimal(r.ReferenceLow), CatalogueFormat.Decimal(r.ReferenceHigh), r.NormalText, r.IsActive, r.Version)).ToList();

        return new QueryResponse<PagedResult<LabAnalyteViewModel>> { Result = new PagedResult<LabAnalyteViewModel>(items, query.PageNumber, query.PageSize, total) };
    }
}

public sealed class GetLabAnalyteQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetLabAnalyteQuery, LabAnalyteViewModel?>
{
    public async Task<QueryResponse<LabAnalyteViewModel?>> HandleAsync(GetLabAnalyteQuery query)
    {
        var r = await db.Set<LabAnalyteAggregate>().AsNoTracking().Where(a => a.Id == query.LabAnalyteId && !a.IsMarkedToDelete)
            .Select(a => new { a.Id, a.Code, a.Name, a.ValueKind, a.DiagnosticKind, a.Unit, a.ReferenceLow, a.ReferenceHigh, a.NormalText, a.IsActive, a.Version })
            .FirstOrDefaultAsync();

        return new QueryResponse<LabAnalyteViewModel?>
        {
            Result = r is null ? null : new LabAnalyteViewModel(
                r.Id, r.Code, r.Name, r.ValueKind.DisplayName, r.DiagnosticKind.DisplayName, r.Unit,
                CatalogueFormat.Decimal(r.ReferenceLow), CatalogueFormat.Decimal(r.ReferenceHigh), r.NormalText, r.IsActive, r.Version)
        };
    }
}

public sealed class GetNoteTemplatesQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetNoteTemplatesQuery, PagedResult<NoteTemplateViewModel>>
{
    public async Task<QueryResponse<PagedResult<NoteTemplateViewModel>>> HandleAsync(GetNoteTemplatesQuery query)
    {
        var filtered = db.Set<NoteTemplateAggregate>().AsNoTracking().Where(t => !t.IsMarkedToDelete);
        if (query.ActiveOnly) filtered = filtered.Where(t => t.IsActive);
        if (query.ServiceId is { } serviceId) filtered = filtered.Where(t => t.ClinicServiceId == serviceId);

        var total = await filtered.CountAsync();
        var items = await (
            from t in filtered
            join s in db.Set<ClinicServiceAggregate>().AsNoTracking() on t.ClinicServiceId equals s.Id into services
            from s in services.DefaultIfEmpty()
            orderby s.Name, t.Name
            select new NoteTemplateViewModel(
                t.Id, t.ClinicServiceId, s != null ? s.Name : "", t.Name, t.Subjective, t.Objective, t.Assessment, t.Plan,
                t.DischargeInstructions, t.TemplateVersion, t.IsActive, t.Version))
            .Skip(query.Skip).Take(query.Take).ToListAsync();

        return new QueryResponse<PagedResult<NoteTemplateViewModel>> { Result = new PagedResult<NoteTemplateViewModel>(items, query.PageNumber, query.PageSize, total) };
    }
}

public sealed class GetNoteTemplateQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetNoteTemplateQuery, NoteTemplateViewModel?>
{
    public async Task<QueryResponse<NoteTemplateViewModel?>> HandleAsync(GetNoteTemplateQuery query)
    {
        var result = await (
            from t in db.Set<NoteTemplateAggregate>().AsNoTracking()
            where t.Id == query.NoteTemplateId && !t.IsMarkedToDelete
            join s in db.Set<ClinicServiceAggregate>().AsNoTracking() on t.ClinicServiceId equals s.Id into services
            from s in services.DefaultIfEmpty()
            select new NoteTemplateViewModel(
                t.Id, t.ClinicServiceId, s != null ? s.Name : "", t.Name, t.Subjective, t.Objective, t.Assessment, t.Plan,
                t.DischargeInstructions, t.TemplateVersion, t.IsActive, t.Version))
            .FirstOrDefaultAsync();

        return new QueryResponse<NoteTemplateViewModel?> { Result = result };
    }
}
