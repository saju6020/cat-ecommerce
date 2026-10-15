using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Product;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Consultation;

/// <summary>
/// HO-EMR-001 section 8.12 SHP-1 -- the one adapter through which Phase F reads a catalogue SKU. HO-SHP-001 published no <c>ISkuReader</c>, so this
/// composes the owners of each fact, each through its own published port or projection and none by reading another area's tables ad hoc:
/// <list type="bullet">
/// <item>name, active, stock unit and fractional flag -- Inventory's <see cref="ISkuDirectory"/> (HO-IMP-001 section 8.2; the unit is always
/// <c>each</c> and no SKU is fractional today, OQ-19);</item>
/// <item>the tax class -- Shop's published <c>SkuRows</c> projection (the same one <c>CatalogueSkuDirectory</c> reads);</item>
/// <item>the controlled flag -- HO-PHM-001's <see cref="IControlledDrugDirectory"/>: a SKU is a controlled drug when an active-or-not formulary
/// medicine flagged controlled is linked to it. Shop has no such flag (HO-EMR-001 OQ-12 named both candidate sources).</item>
/// </list>
/// Read-only. An unknown SKU is null.
/// </summary>
public sealed class ClinicalSkuReader(CatEcommerceDbContext db, ISkuDirectory skus, IControlledDrugDirectory controlledDrugs) : IClinicalSkuReader
{
    public async Task<ClinicalSku?> GetAsync(Guid skuId, CancellationToken ct)
    {
        var profile = await skus.GetAsync(skuId, ct);
        if (profile is null) return null;

        var taxClassId = await db.SkuRows().Where(s => s.SkuId == skuId).Select(s => s.TaxClassId).FirstOrDefaultAsync(ct);
        var controlled = await controlledDrugs.FindBySkuAsync(skuId, ct) is not null;

        return new ClinicalSku(profile.SkuId, profile.DisplayName, profile.StockUnit, profile.IsActive, profile.AllowsFractionalQuantity, taxClassId, controlled);
    }
}
