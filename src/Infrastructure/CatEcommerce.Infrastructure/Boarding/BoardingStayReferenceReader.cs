using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Boarding;

/// <summary>
/// HO-BRD-001 section 12.8 P1/I1 (CONTRACTS #14): the port Billing and Inventory validate a boarding reference with -- replaces the fail-closed fake. A deposit or
/// stay reference exists for a customer when that customer owns the stay; a cat is "in house" while its stay is <c>CheckedIn</c>. Read-only, untracked.
/// </summary>
public sealed class BoardingStayReferenceReader(CatEcommerceDbContext db) : IBoardingStayReferenceReader
{
    public async Task<bool> ExistsForCustomer(BoardingPaymentReferenceKind kind, Guid id, Guid customerId, CancellationToken ct)
    {
        if (kind == BoardingPaymentReferenceKind.DaycareVisit)
            return false; // daycare visits arrive with Phase H

        return await db.BoardingStays.AsNoTracking().AnyAsync(s => s.Id == id && s.CustomerId == customerId, ct);
    }

    public Task<bool> IsInHouse(Guid stayId, Guid catId, CancellationToken ct) =>
        db.BoardingStays.AsNoTracking().AnyAsync(s => s.Id == stayId && s.Status == BoardingStayStatus.CheckedIn && s.Cats.Any(c => c.CatId == catId), ct);
}
