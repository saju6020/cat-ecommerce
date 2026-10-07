using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Product;
using CatEcommerce.Read.Queries.Product;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Product;

/// <summary>HO-SHP-001 section 8.12/12.4/12.6 -- HW-2. Contract query; also the staff barcode-lookup
/// read. One projected row, no aggregate.</summary>
public sealed class FindSkuByBarcodeQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<FindSkuByBarcodeQuery, SkuByBarcodeViewModel?>
{
    public async Task<QueryResponse<SkuByBarcodeViewModel?>> HandleAsync(FindSkuByBarcodeQuery query)
    {
        var row = await db.SkuRows().Where(r => r.Barcode == query.Barcode).FirstOrDefaultAsync();
        SkuByBarcodeViewModel? result = row is null
            ? null
            : new SkuByBarcodeViewModel(row.SkuId, row.SkuCode, row.Label, row.ProductId, row.ProductName, row.SkuStatus.DisplayName);

        return new QueryResponse<SkuByBarcodeViewModel?> { Result = result };
    }
}
