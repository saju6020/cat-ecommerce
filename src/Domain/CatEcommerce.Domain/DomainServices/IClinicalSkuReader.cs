using CatEcommerce.Domain.Models;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-EMR-001 §8.12 SHP-1 -- the one question Phase F asks about a catalogue SKU: its name, stock unit, whether it is active, its tax class and
/// whether it is a controlled drug. HO-SHP-001 publishes no such port, so the single adapter (<c>ClinicalSkuReader</c>, Infrastructure) composes
/// the owners of each fact: Inventory's <c>ISkuDirectory</c> (name, active, unit, fractional), Shop's published <c>SkuRows</c> projection (tax
/// class) and HO-PHM-001's <c>IControlledDrugDirectory</c> (the only controlled flag that exists -- a formulary medicine linked to the SKU).
/// Read-only; an unknown SKU is null.
/// </summary>
public interface IClinicalSkuReader
{
    Task<ClinicalSku?> GetAsync(Guid skuId, CancellationToken ct);
}
