using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Pharmacy;
using CatEcommerce.Read.Queries.Prescription;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Prescription;

/// <summary>HO-PHM-001 §12.6 -- the formulary list: explicit projection, <c>AsNoTracking</c>, paged in SQL (ADR-012: no aggregate is rehydrated).</summary>
public sealed class GetFormularyMedicinesQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetFormularyMedicinesQuery, PagedResult<FormularyMedicineSummaryViewModel>>
{
    public async Task<QueryResponse<PagedResult<FormularyMedicineSummaryViewModel>>> HandleAsync(GetFormularyMedicinesQuery query)
    {
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 50 : query.PageSize, 1, 100);
        var pageNumber = Math.Max(query.PageNumber, 1);

        var medicines = db.FormularyMedicines.AsNoTracking().Where(m => !m.IsMarkedToDelete);
        if (query.IsActive is { } active) medicines = medicines.Where(m => m.IsActive == active);
        if (query.IsControlled is { } controlled) medicines = medicines.Where(m => m.IsControlled == controlled);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = "%" + query.Search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            medicines = medicines.Where(m => EF.Functions.ILike(m.Name, pattern, "\\") || EF.Functions.ILike(m.Code, pattern, "\\"));
        }

        var total = await medicines.CountAsync();
        var rows = await medicines.OrderBy(m => m.Name).ThenBy(m => m.Id).Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(m => new
            {
                m.Id, m.Code, m.Name, m.Form, m.Strength.Amount, m.Strength.Unit, m.Strength.PerQuantity, m.Strength.PerUnit, m.DosingBasis,
                m.IsControlled, m.IsActive, IsStocked = m.LinkedSkus.Any()
            })
            .ToListAsync();

        var items = rows.Select(r => new FormularyMedicineSummaryViewModel(
            r.Id, r.Code, r.Name, r.Form.DisplayName, new Strength(r.Amount, r.Unit, r.PerQuantity, r.PerUnit).Text(), r.DosingBasis.DisplayName,
            r.IsControlled, r.IsActive, r.IsStocked)).ToList();

        return new QueryResponse<PagedResult<FormularyMedicineSummaryViewModel>> { Result = new PagedResult<FormularyMedicineSummaryViewModel>(items, pageNumber, pageSize, total) };
    }
}

public sealed class GetFormularyMedicineQueryHandler(CatEcommerceDbContext db, ICatalogueReader catalogue)
    : IQueryHandlerAsync<GetFormularyMedicineQuery, FormularyMedicineDetailViewModel?>
{
    public async Task<QueryResponse<FormularyMedicineDetailViewModel?>> HandleAsync(GetFormularyMedicineQuery query)
    {
        var row = await db.FormularyMedicines.Where(m => m.Id == query.MedicineId && !m.IsMarkedToDelete).AsRows().FirstOrDefaultAsync();
        if (row is null) return new QueryResponse<FormularyMedicineDetailViewModel?> { Result = null };

        var interactionRows = await db.FormularyMedicines.AsNoTracking().Where(m => m.Id == query.MedicineId)
            .SelectMany(m => m.Interactions.Select(i => new { i.Id, i.InteractsWithMedicineId, i.InteractsWithTerm, i.Severity, i.Note }))
            .ToListAsync();

        var otherIds = interactionRows.Where(i => i.InteractsWithMedicineId != null).Select(i => i.InteractsWithMedicineId!.Value).Distinct().ToList();
        var otherNames = otherIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await db.FormularyMedicines.AsNoTracking().Where(m => otherIds.Contains(m.Id)).Select(m => new { m.Id, m.Name }).ToListAsync()).ToDictionary(m => m.Id, m => m.Name);

        var skus = (await catalogue.GetSkusAsync(row.LinkedSkuIds, CancellationToken.None)).ToDictionary(s => s.SkuId);

        var snapshot = row.ToSnapshot();
        var strength = snapshot.Strength;

        var detail = new FormularyMedicineDetailViewModel(
            row.MedicineId, row.Code, row.Name, row.ActiveIngredients, row.DrugClasses, row.Form.DisplayName,
            new StrengthViewModel(
                QuantityViewModel.Of(strength.Amount, strength.Unit.DisplayName), QuantityViewModel.Of(strength.PerQuantity, strength.PerUnit.DisplayName), strength.Text()),
            row.DefaultRoute.DisplayName, snapshot.AllowedRoutes.Select(r => r.DisplayName).ToList(), row.DosingBasis.DisplayName,
            snapshot.DoseRange is null ? null : new DoseRangeViewModel(snapshot.DoseRange.MinMgPerKg.ToString("0.00##", System.Globalization.CultureInfo.InvariantCulture), snapshot.DoseRange.MaxMgPerKg.ToString("0.00##", System.Globalization.CultureInfo.InvariantCulture)),
            row.Contraindications,
            interactionRows.Select(i => new FormularyInteractionViewModel(
                i.Id, i.InteractsWithMedicineId, i.InteractsWithMedicineId is { } other && otherNames.TryGetValue(other, out var name) ? name : null,
                i.InteractsWithTerm, i.Severity.DisplayName, i.Note)).OrderByDescending(i => i.Severity == "Contraindicated").ThenBy(i => i.Note).ToList(),
            row.DispensingUnit.DisplayName, row.IsControlled, row.ControlledSchedule, row.IsActive,
            row.LinkedSkuIds.Where(skus.ContainsKey).Select(id => new FormularyLinkedSkuViewModel(id, skus[id].ProductName, skus[id].VariantLabel)).ToList(),
            row.Version);

        return new QueryResponse<FormularyMedicineDetailViewModel?> { Result = detail };
    }
}

public sealed class SearchFormularySkusQueryHandler(CatEcommerceDbContext db, ICatalogueReader catalogue)
    : IQueryHandlerAsync<SearchFormularySkusQuery, IReadOnlyList<FormularySkuSearchResultViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<FormularySkuSearchResultViewModel>>> HandleAsync(SearchFormularySkusQuery query)
    {
        var skus = await catalogue.SearchSkusAsync(query.Term, includeClinicOnly: true, take: 25, CancellationToken.None);
        var ids = skus.Select(s => s.SkuId).ToList();

        var links = ids.Count == 0
            ? []
            : await db.FormularyMedicines.AsNoTracking()
                .SelectMany(m => m.LinkedSkus.Where(l => ids.Contains(l.SkuId)).Select(l => new { l.SkuId, MedicineId = m.Id }))
                .ToListAsync();
        var linked = links.ToDictionary(l => l.SkuId, l => l.MedicineId);

        IReadOnlyList<FormularySkuSearchResultViewModel> result = skus
            .Select(s => new FormularySkuSearchResultViewModel(s.SkuId, s.ProductName, s.VariantLabel, s.Barcode, s.IsClinicOnly, linked.TryGetValue(s.SkuId, out var medicineId) ? medicineId : null))
            .ToList();

        return new QueryResponse<IReadOnlyList<FormularySkuSearchResultViewModel>> { Result = result };
    }
}
