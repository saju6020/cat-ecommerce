using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Infrastructure.Billing.ItemSources;

/// <summary>
/// HO-BIL-001 Phase J (owner decisions 2026-10-14) -- a <c>Finalised</c> visit becomes its own bill: one charge per billable treatment line and one per
/// diagnostic test ordered. It reads the visit <b>only</b> through EMR's typed <see cref="IVisitBillingReader"/> (backlog EMR 43) and maps EMR's kinds to
/// Billing's with <see cref="VisitBillableLineKindMap"/>. <b>Not</b> the consultation fee: the appointment source charges that, here and only there
/// (§8.5, OQ-14).
/// <list type="bullet">
/// <item>Who is billed: the cat's owner at finalising, stored on the visit. A visit finalised before that was stored has none and is <i>not billable</i>
/// (<c>VisitCustomerNotRecorded</c>) -- Billing does not fall back to the appointment's customer, which is a different fact.</item>
/// <item>Prices and tax classes are EMR's snapshots, never re-read; a null tax class reaches the capture, which reports it (<c>TaxClassNotFound</c>).</item>
/// <item>Service date = the branch-local date of <c>FinalisedOn</c> (EMR's decision, HO-EMR-001 Amended 2026-10-13).</item>
/// <item>Every visit is billed on its own. There is no boarding-stay link: it is deferred to HO-BRD-001 (owner decision 3) and nothing is stubbed.</item>
/// </list>
/// </summary>
public sealed class VisitBillableItemSource(IVisitBillingReader reader, CatEcommerceDbContext db) : IBillableItemSource
{
    public ChargeSourceKind Kind => ChargeSourceKind.Visit;

    public async Task<BillableSourceResult> GetItemsAsync(Guid sourceId, CancellationToken ct = default)
    {
        var visit = await reader.GetAsync(sourceId, ct);
        if (visit is null) return BillableSourceResult.NotFound;
        if (!visit.IsFinalised || visit.FinalisedOn is not { } finalisedOn) return BillableSourceResult.NotBillable("VisitNotFinalised").InBranch(visit.BranchId);
        if (visit.CustomerId is not { } recordedCustomerId) return BillableSourceResult.NotBillable("VisitCustomerNotRecorded").InBranch(visit.BranchId);

        var customerId = await BillableSourceSupport.BillableCustomerAsync(db, recordedCustomerId, ct);
        var serviceDate = await BillableSourceSupport.ServiceDateAsync(db, visit.BranchId, finalisedOn, ct);

        var items = visit.Lines.Select(line => new BillableSourceItem(
            customerId, visit.BranchId, visit.CatId, VisitBillableLineKindMap.ItemKindFor(line.Kind), line.Description, line.Quantity,
            line.UnitPrice, new Money(0m, line.UnitPrice.CurrencyCode), line.TaxClassId, serviceDate, line.LineRef, null, null)).ToList();

        return BillableSourceResult.Billable(items).InBranch(visit.BranchId);
    }
}
