using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Billing.ItemSources;

/// <summary>
/// HO-BIL-001 Phase J -- a <c>Dispensed</c> supply of a prescription is billed once: one <c>Medicine</c> charge per batch handed over, at the unit price
/// snapshot the dispensing recorded (HO-PHM-001 ADR-147; never re-read) and the SKU's tax class. A supply that was reversed is not billable. A line
/// with no price recorded (the SKU had none then -- PHM OQ-6) is <i>skipped and reported</i> as <c>SkuNotPriced</c>; Billing does not look a price up
/// for it. Service date = the dispensing's own branch-local date (PHM computes it with the branch zone).
/// Reads the dispensing's own table through untracked projections, as the appointment source does; PHM publishes no typed billing port (decision
/// recorded in HO-BIL-001 §1 Amended).
/// </summary>
public sealed class DispenseBillableItemSource(CatEcommerceDbContext db, IBillingPricingInputs pricingInputs) : IBillableItemSource
{
    public ChargeSourceKind Kind => ChargeSourceKind.Dispense;

    public async Task<BillableSourceResult> GetItemsAsync(Guid sourceId, CancellationToken ct = default)
    {
        var dispensing = await db.Dispensings.AsNoTracking()
            .Where(d => d.Id == sourceId && !d.IsMarkedToDelete)
            .Select(d => new { d.Id, d.BranchId, d.CatId, d.CustomerId, d.Status, d.DispensedOnLocal, MedicineLine = d.Label.MedicineLine })
            .FirstOrDefaultAsync(ct);
        if (dispensing is null) return BillableSourceResult.NotFound;
        if (dispensing.Status != DispensingStatus.Dispensed) return BillableSourceResult.NotBillable("DispensingReversed").InBranch(dispensing.BranchId);

        var lines = await db.Dispensings.AsNoTracking().Where(d => d.Id == sourceId)
            .SelectMany(d => d.Lines)
            .OrderBy(l => l.LotNumber).ThenBy(l => l.Id)
            .Select(l => new
            {
                l.Id, l.SkuId, l.Quantity,
                UnitPrice = l.PriceSnapshot == null ? (decimal?)null : l.PriceSnapshot.UnitPrice,
                Currency = l.PriceSnapshot == null ? null : l.PriceSnapshot.CurrencyCode,
            })
            .ToListAsync(ct);

        var customerId = await BillableSourceSupport.BillableCustomerAsync(db, dispensing.CustomerId, ct);
        var taxClasses = await pricingInputs.GetSkuTaxClassIdsAsync(lines.Select(l => l.SkuId).ToList(), ct);
        var description = dispensing.MedicineLine.Length > BillingRules.MaxDescriptionLength ? dispensing.MedicineLine[..BillingRules.MaxDescriptionLength] : dispensing.MedicineLine;

        var items = new List<BillableSourceItem>(lines.Count);
        var skipped = new List<string>();
        foreach (var line in lines)
        {
            if (line.UnitPrice is not { } unitPrice || line.Currency is null)
            {
                skipped.Add(BusinessViolationCodes.SkuNotPriced);
                continue;
            }

            Guid? taxClassId = taxClasses.TryGetValue(line.SkuId, out var found) ? found : null;
            items.Add(new BillableSourceItem(
                customerId, dispensing.BranchId, dispensing.CatId, ItemKind.Medicine, description, line.Quantity,
                new Money(unitPrice, line.Currency), new Money(0m, line.Currency), taxClassId, dispensing.DispensedOnLocal,
                LineRefOf(line.Id), null, null));
        }

        return BillableSourceResult.Billable(items, skipped).InBranch(dispensing.BranchId);
    }

    /// <summary>The charge's line reference: the dispensing line's own id (a fresh id per ledger movement, PHM Amended 2026-10-09), so a re-read never doubles a line.</summary>
    public static string LineRefOf(Guid dispensingLineId) => "line:" + dispensingLineId.ToString("N");
}
