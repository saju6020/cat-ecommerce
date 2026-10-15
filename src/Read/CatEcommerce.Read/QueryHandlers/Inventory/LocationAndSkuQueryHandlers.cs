using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Inventory;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Inventory;

/// <summary>HO-IMP-001 §12.4 -- the active branch's locations with what each kind permits (<c>StockLocationRules</c>' row).</summary>
public sealed class GetStockLocationsQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetStockLocationsQuery, IReadOnlyList<StockLocationViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<StockLocationViewModel>>> HandleAsync(GetStockLocationsQuery query)
    {
        var rows = await db.StockLocations.AsNoTracking()
            .Where(l => l.BranchId == query.BranchId)
            .OrderBy(l => l.Code)
            .Select(l => new { l.Id, l.Code, l.Name, l.Kind, l.IsActive })
            .ToListAsync();

        IReadOnlyList<StockLocationViewModel> items = rows
            .Select(r => new StockLocationViewModel(
                r.Id, r.Code, r.Name, r.Kind.DisplayName, r.IsActive,
                StockLocationRules.PermittedMovements(r.Kind).Select(m => m.DisplayName).ToList()))
            .ToList();

        return new QueryResponse<IReadOnlyList<StockLocationViewModel>> { Result = items };
    }
}

/// <summary>HO-IMP-001 §12.4 -- by barcode (exact, at most one) or by text (at most 20), each with its <c>storage</c>.</summary>
public sealed class GetSkuLookupQueryHandler(ISkuDirectory skus, CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetSkuLookupQuery, IReadOnlyList<SkuLookupViewModel>>
{
    private const int MaxRows = 20;

    public async Task<QueryResponse<IReadOnlyList<SkuLookupViewModel>>> HandleAsync(GetSkuLookupQuery query)
    {
        IReadOnlyList<SkuStockProfile> found;
        if (!string.IsNullOrWhiteSpace(query.Barcode))
        {
            var byBarcode = await skus.FindByBarcodeAsync(query.Barcode, CancellationToken.None);
            found = byBarcode is null ? [] : [byBarcode];
        }
        else
        {
            found = await skus.SearchAsync(query.Search ?? string.Empty, MaxRows, CancellationToken.None);
        }

        var productIds = found.Select(s => s.ProductId).Distinct().ToList();
        var storage = await StorageLookup.ForProductsAsync(db, productIds);

        IReadOnlyList<SkuLookupViewModel> items = found
            .Select(s => new SkuLookupViewModel(
                s.SkuId, s.ProductId, s.SkuCode, s.DisplayName, s.Barcode, s.StockClass.DisplayName, s.ExpiryRequired, s.StockUnit,
                s.AllowsFractionalQuantity, s.CountryOfOriginCode, s.IsActive, storage.GetValueOrDefault(s.ProductId)))
            .ToList();

        return new QueryResponse<IReadOnlyList<SkuLookupViewModel>> { Result = items };
    }
}

public sealed class GetStorageProfileQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetStorageProfileQuery, StorageProfileViewModel?>
{
    public async Task<QueryResponse<StorageProfileViewModel?>> HandleAsync(GetStorageProfileQuery query)
    {
        var row = await db.ProductStorageProfiles.AsNoTracking()
            .Where(p => p.Id == query.ProductId)
            .Select(p => new { p.Id, p.Condition, p.MinTemperatureC, p.MaxTemperatureC, p.HandlingNotes, p.Version })
            .FirstOrDefaultAsync();

        return new QueryResponse<StorageProfileViewModel?>
        {
            Result = row is null ? null
                : new StorageProfileViewModel(row.Id, new StorageViewModel(row.Condition.DisplayName, row.MinTemperatureC, row.MaxTemperatureC, row.HandlingNotes), row.Version)
        };
    }
}

/// <summary>The <c>storage</c> object every SKU, receipt, batch and stock read carries (US-IMP-16 AC1) -- null when no profile exists.</summary>
internal static class StorageLookup
{
    public static async Task<IReadOnlyDictionary<Guid, StorageViewModel>> ForProductsAsync(CatEcommerceDbContext db, IReadOnlyCollection<Guid> productIds)
    {
        if (productIds.Count == 0) return new Dictionary<Guid, StorageViewModel>();

        var ids = productIds.ToList();
        var rows = await db.ProductStorageProfiles.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Condition, p.MinTemperatureC, p.MaxTemperatureC, p.HandlingNotes })
            .ToListAsync();

        return rows.ToDictionary(r => r.Id, r => new StorageViewModel(r.Condition.DisplayName, r.MinTemperatureC, r.MaxTemperatureC, r.HandlingNotes));
    }
}
