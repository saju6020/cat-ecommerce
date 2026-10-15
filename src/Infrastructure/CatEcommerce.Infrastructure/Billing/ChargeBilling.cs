using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Billing;

/// <summary>
/// "Billed" is <b>derived</b> (HO-BIL-001 §8.1, ADR-178): a charge is billed when a line of a non-discarded
/// invoice references it. This is the one place that says so for write-side checks; the filtered unique index
/// <c>ix_invoice_line_charge</c> makes "billed at most once" true under concurrency.
/// </summary>
internal static class ChargeBilling
{
    public static Task<bool> IsBilledAsync(CatEcommerceDbContext db, Guid chargeId, CancellationToken ct) =>
        db.Set<InvoiceAggregate>().AsNoTracking()
            .Where(i => i.Status != InvoiceStatus.Discarded)
            .AnyAsync(i => i.Lines.Any(l => l.ChargeId == chargeId), ct);
}
