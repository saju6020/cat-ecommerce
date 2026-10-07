using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Product;
using CatEcommerce.Read.Queries.Product;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Product;

/// <summary>HO-SHP-001 section 8.9 -- the one contract HO-ORD-001 calls at add-to-cart and at
/// checkout (US-SHP-10 AC2/AC3). Both calls land here, so they go through the same gate and the
/// same outcome mapping and cannot drift. Ownership, liveness and expiry are the gate rules
/// (HO-PHM-001 ADR-149). A bundle with a prescription component is never eligible (OQ-14 / BR-2),
/// without asking the gate: a kit cannot carry an authorisation per cat. Projections only.</summary>
public sealed class CheckPrescriptionEligibilityQueryHandler(CatEcommerceDbContext db, IDietAuthorisationGate dietGate)
    : IQueryHandlerAsync<CheckPrescriptionEligibilityQuery, PrescriptionEligibilityViewModel?>
{
    public async Task<QueryResponse<PrescriptionEligibilityViewModel?>> HandleAsync(CheckPrescriptionEligibilityQuery query)
    {
        var row = await db.SkuRows().Where(r => r.SkuId == query.SkuId)
            .Select(r => new { r.ProductId, r.Kind, r.PrescriptionRequired }).FirstOrDefaultAsync();

        if (row is null) return new QueryResponse<PrescriptionEligibilityViewModel?> { Result = null };

        if (row.Kind == ProductKind.Bundle && (await db.BundlesWithPrescriptionComponentAsync([row.ProductId])).Contains(row.ProductId))
        {
            return new QueryResponse<PrescriptionEligibilityViewModel?>
            { Result = new PrescriptionEligibilityViewModel(false, PurchaseBlockReason.PrescriptionAuthorisationRequired.DisplayName) };
        }

        if (!row.PrescriptionRequired) return new QueryResponse<PrescriptionEligibilityViewModel?> { Result = new PrescriptionEligibilityViewModel(true, null) };

        var gateResult = await dietGate.CheckAsync(query.CustomerId, query.CatId, query.SkuId, CancellationToken.None);
        var decision = PrescriptionPurchaseGate.FromOutcome(gateResult.Outcome);

        return new QueryResponse<PrescriptionEligibilityViewModel?>
        { Result = new PrescriptionEligibilityViewModel(decision.IsPermitted, decision.BlockReason?.DisplayName) };
    }
}
