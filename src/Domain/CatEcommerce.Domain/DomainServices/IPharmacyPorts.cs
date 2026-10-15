using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-PHM-001 §8.13 -- loads, for ONE cat, everything the pure safety assessor needs: the latest weight (the newest <c>cat_weight_record</c> row,
/// ADR-023), the recorded allergies, the active prescriptions with their formulary data, and the profile medications resolved against the formulary.
/// One place, used by the check query, issue and renew, so the screen and the command can never load different facts. Read-only, no tracking.
/// </summary>
public interface ISafetyInputLoader
{
    /// <param name="excludePrescriptionId">A prescription being RENEWED is not an "already active" duplicate of its own renewal; it is left out of the active medicines.</param>
    Task<SafetyInput> LoadAsync(
        Guid catId, string catName, FormularyMedicineSnapshot medicine, Dose dose, AdministrationRoute route, DoseFrequency frequency, DateOnly today, CancellationToken ct,
        Guid? excludePrescriptionId = null);
}

/// <summary>HO-PHM-001 §8.3 -- <c>SELECT nextval('pharmacy.prescription_number_seq')</c>, formatted <c>RX-{yyyy}-{000001}</c>, inside the current transaction.</summary>
public interface IPrescriptionNumberAllocator
{
    Task<string> AllocateAsync(DateTimeOffset issuedOn, CancellationToken ct);
}

/// <summary>HO-PHM-001 §8.12 -- the branch's "today". Zone conversion stays in HO-ADM-001's <c>BranchCalendar</c> (ADR-045).</summary>
public interface IPharmacyClock
{
    Task<DateOnly> LocalTodayAsync(Guid branchId, CancellationToken ct);

    Task<DateOnly> LocalDateOfAsync(Guid branchId, DateTimeOffset instant, CancellationToken ct);

    /// <summary>The main branch's local date -- the rule HO-NTF-001 §8.12 sets for timed producers and for the gate.</summary>
    Task<DateOnly> MainBranchTodayAsync(CancellationToken ct);
}

/// <summary>
/// HO-PHM-001 §8.12 -- Pharmacy's single read adapter over HO-SHP-001's catalogue (the same published <c>SkuRows</c> projection <c>CatalogueSkuDirectory</c>
/// uses): SKUs and products by id, search (clinic-only SKUs included), and the prescription-required flag. The only file that knows the catalogue's
/// schema; read-only.
/// </summary>
public interface ICatalogueReader
{
    Task<IReadOnlyList<CatalogueSku>> GetSkusAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken ct);

    Task<CatalogueProduct?> GetProductAsync(Guid productId, CancellationToken ct);

    Task<CatalogueProduct?> FindProductBySkuAsync(Guid skuId, CancellationToken ct);

    Task<IReadOnlyList<CatalogueSku>> SearchSkusAsync(string? term, bool includeClinicOnly, int take, CancellationToken ct);

    Task<IReadOnlyList<CatalogueProduct>> SearchPrescriptionProductsAsync(string? term, int take, CancellationToken ct);
}

/// <summary>HO-PHM-001 §8.12 -- the SKU price in force at an instant (<c>SkuPricing.PriceAt</c>); null when the SKU has no price yet (OQ-6).</summary>
public interface ISkuPriceReader
{
    Task<Money?> PriceAtAsync(Guid skuId, DateTimeOffset at, CancellationToken ct);
}
