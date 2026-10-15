using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Billing.ItemSources;

/// <summary>
/// HO-BIL-001 Phase J -- a <c>Discharged</c> surgery becomes its bill: the <b>procedure fee</b> at the price snapshot the surgery was booked at (campaign
/// price included -- HO-SRG-001 ADR-170; the appointment's fee is NOT charged for a surgery, because the appointment is managed by the surgery and never
/// completes on its own) plus one <c>Medicine</c> charge per anaesthesia-log drug, as the log stands after corrections (the leaf of each correction chain,
/// exactly the rule of SRG's <c>GetSurgeryChargeableItemsQuery</c>). A drug entry has no price recorded on it, so it is priced at the SKU price in force
/// when it was given (<see cref="ISkuPriceReader"/>, PHM's published port); a drug with no price then is skipped and reported (<c>SkuNotPriced</c>).
/// <list type="bullet">
/// <item>The procedure fee is charged only for a procedure that ended <c>Completed</c>. An abandoned procedure, or a cat closed out as deceased before the
/// procedure ended, is billed for the drugs it used and not the fee. This is a rule neither handover states; it is recorded as an owner question.</item>
/// <item>Service date = the branch-local date of the discharge.</item>
/// <item>Boarding after surgery (US-SRG-09) is not built; nothing here refers to a stay.</item>
/// </list>
/// </summary>
public sealed class SurgeryBillableItemSource(CatEcommerceDbContext db, IBillingPricingInputs pricingInputs, ISkuPriceReader skuPrices) : IBillableItemSource
{
    public const string ProcedureLineRef = "procedure";

    public ChargeSourceKind Kind => ChargeSourceKind.Surgery;

    public async Task<BillableSourceResult> GetItemsAsync(Guid sourceId, CancellationToken ct = default)
    {
        var surgery = await db.Surgeries.AsNoTracking()
            .Where(s => s.Id == sourceId && !s.IsMarkedToDelete)
            .Select(s => new
            {
                s.Id, s.BranchId, s.CatId, s.OwnerAtDischargeId, s.ClinicServiceId, s.Status, s.DischargedOn, s.Outcome,
                ProcedureName = s.Procedure.ServiceName,
                PriceAmount = s.PriceSnapshot.Amount.Amount, PriceCurrency = s.PriceSnapshot.Amount.CurrencyCode,
            })
            .FirstOrDefaultAsync(ct);
        if (surgery is null) return BillableSourceResult.NotFound;
        if (surgery.Status != SurgeryStatus.Discharged || surgery.DischargedOn is not { } dischargedOn) return BillableSourceResult.NotBillable("SurgeryNotDischarged").InBranch(surgery.BranchId);

        // Owner decision 2026-10-14, "owner at discharge": the cat's owner when the surgery was discharged, as a visit bills the owner at finalising. A surgery
        // discharged before that was stored (or whose cat record was missing) has none; Billing does not fall back to the booking customer.
        if (surgery.OwnerAtDischargeId is not { } recordedCustomerId)
            return BillableSourceResult.NotBillable("SurgeryCustomerNotRecorded").InBranch(surgery.BranchId);
        var customerId = await BillableSourceSupport.BillableCustomerAsync(db, recordedCustomerId, ct);
        var serviceDate = await BillableSourceSupport.ServiceDateAsync(db, surgery.BranchId, dischargedOn, ct);

        var items = new List<BillableSourceItem>();
        var skipped = new List<string>();

        if (surgery.Outcome == ProcedureOutcome.Completed)
        {
            var taxClassId = await pricingInputs.GetServiceTaxClassIdAsync(surgery.ClinicServiceId, ct);
            items.Add(new BillableSourceItem(
                customerId, surgery.BranchId, surgery.CatId, ItemKind.Service, Truncate(surgery.ProcedureName), 1m,
                new Money(surgery.PriceAmount, surgery.PriceCurrency), new Money(0m, surgery.PriceCurrency), taxClassId, serviceDate, ProcedureLineRef, null, null));
        }

        var drugs = await DrugsAsync(surgery.Id, ct);
        var taxClasses = await pricingInputs.GetSkuTaxClassIdsAsync(drugs.Select(d => d.SkuId).ToList(), ct);
        foreach (var drug in drugs)
        {
            var price = await skuPrices.PriceAtAsync(drug.SkuId, drug.OccurredOn, ct);
            if (price is null)
            {
                skipped.Add(BusinessViolationCodes.SkuNotPriced);
                continue;
            }

            Guid? taxClassId = taxClasses.TryGetValue(drug.SkuId, out var found) ? found : null;
            items.Add(new BillableSourceItem(
                customerId, surgery.BranchId, surgery.CatId, ItemKind.Medicine, Truncate(drug.Name), drug.Quantity,
                price, new Money(0m, price.CurrencyCode), taxClassId, serviceDate, DrugLineRef(drug.EntryId), null, null));
        }

        return BillableSourceResult.Billable(items, skipped).InBranch(surgery.BranchId);
    }

    public static string DrugLineRef(Guid originalEntryId) => "drug:" + originalEntryId.ToString("N");

    private static string Truncate(string text) => text.Length > BillingRules.MaxDescriptionLength ? text[..BillingRules.MaxDescriptionLength] : text;

    /// <summary>The billable drugs: each original drug entry resolved to the leaf of its correction chain, kept only when the leaf names a SKU and a positive
    /// quantity drawn (a shortfall, or a drug corrected away, has neither). Keyed by the ORIGINAL entry's id, which never changes however often it is corrected.</summary>
    private async Task<List<SurgeryDrug>> DrugsAsync(Guid surgeryId, CancellationToken ct)
    {
        var rows = await db.AnaesthesiaLogEntries.AsNoTracking()
            .Where(e => e.SurgeryId == surgeryId && !e.IsMarkedToDelete
                        && (e.Kind == AnaesthesiaEntryKind.Drug || (e.Kind == AnaesthesiaEntryKind.Correction && e.CorrectedKind == AnaesthesiaEntryKind.Drug)))
            .Select(e => new LogRow(e.Id, e.Kind, e.OccurredOn, e.LoggedOn, e.CorrectsEntryId, e.MedicineName, e.SkuId, e.QuantityDrawn))
            .ToListAsync(ct);

        var corrected = rows.Where(r => r.CorrectsEntryId is not null).GroupBy(r => r.CorrectsEntryId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.LoggedOn).ThenByDescending(r => r.Id).First());

        var drugs = new List<SurgeryDrug>();
        foreach (var original in rows.Where(r => r.Kind == AnaesthesiaEntryKind.Drug).OrderBy(r => r.OccurredOn).ThenBy(r => r.LoggedOn).ThenBy(r => r.Id))
        {
            var leaf = original;
            for (var hops = 0; corrected.TryGetValue(leaf.Id, out var next) && hops < 100; hops++) leaf = next;
            if (leaf.SkuId is not { } skuId || leaf.QuantityDrawn is not { } quantity || quantity <= 0m) continue;

            drugs.Add(new SurgeryDrug(original.Id, skuId, leaf.MedicineName ?? original.MedicineName ?? "Medicine used in surgery", quantity, original.OccurredOn));
        }

        return drugs;
    }

    private sealed record LogRow(Guid Id, AnaesthesiaEntryKind Kind, DateTimeOffset OccurredOn, DateTimeOffset LoggedOn, Guid? CorrectsEntryId, string? MedicineName, Guid? SkuId, decimal? QuantityDrawn);

    private sealed record SurgeryDrug(Guid EntryId, Guid SkuId, string Name, decimal Quantity, DateTimeOffset OccurredOn);
}
