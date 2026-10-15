using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Boarding;

/// <summary>
/// HO-BRD-001 section 12.2 / 12.8 P1: read-only over <c>billing.payment</c> by earmark. A deposit counts while its earmark is unreleased: Accepted payments are
/// "received", PendingApproval payments are "awaiting a second approver" (they suspend the lapse). A rejected or reversed payment counts for nothing.
/// </summary>
public sealed class BillingDepositLedger(CatEcommerceDbContext db) : IBoardingDepositLedger
{
    public async Task<DepositLedgerEntry> GetAsync(Guid stayId, CancellationToken ct = default) =>
        (await GetManyAsync([stayId], ct)).GetValueOrDefault(stayId, DepositLedgerEntry.None);

    public async Task<IReadOnlyDictionary<Guid, DepositLedgerEntry>> GetManyAsync(IReadOnlyCollection<Guid> stayIds, CancellationToken ct = default)
    {
        var ids = stayIds.ToList();
        // SECURITY (2026-10-15): only a payment of the stay's OWN customer at the stay's OWN branch counts -- Billing does not check an earmark's source id when a payment is recorded,
        // so without this a payment of customer A earmarked to customer B's stay would confirm B's stay.
        var rows = await (
                from p in db.Set<PaymentAggregate>().AsNoTracking()
                join s in db.BoardingStays.AsNoTracking() on p.Earmark!.SourceId equals s.Id
                where p.Earmark != null && p.Earmark.Kind == EarmarkKind.BoardingStay && ids.Contains(p.Earmark.SourceId) && p.Earmark.ReleasedOn == null
                      && p.CustomerId == s.CustomerId && p.BranchId == s.BranchId
                      && (p.Status == PaymentStatus.Accepted || p.Status == PaymentStatus.PendingApproval)
                select new { StayId = s.Id, p.Status, p.Amount })
            .ToListAsync(ct);

        return rows.GroupBy(r => r.StayId).ToDictionary(
            g => g.Key,
            g => new DepositLedgerEntry(
                g.Where(r => r.Status == PaymentStatus.Accepted).Sum(r => r.Amount),
                g.Where(r => r.Status == PaymentStatus.PendingApproval).Sum(r => r.Amount)));
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetRefundableAsync(Guid branchId, CancellationToken ct = default)
    {
        var rows = await (
                from p in db.Set<PaymentAggregate>().AsNoTracking()
                join s in db.BoardingStays.AsNoTracking() on p.Earmark!.SourceId equals s.Id
                where p.Earmark != null && p.Earmark.Kind == EarmarkKind.BoardingStay && p.Earmark.ReleasedOn == null && p.Status == PaymentStatus.Accepted
                      && s.BranchId == branchId && s.Status == BoardingStayStatus.Cancelled && !s.DepositForfeited
                      && p.CustomerId == s.CustomerId && p.BranchId == s.BranchId
                select new { StayId = s.Id, p.Amount })
            .ToListAsync(ct);

        return rows.GroupBy(r => r.StayId).ToDictionary(g => g.Key, g => g.Sum(r => r.Amount));
    }

    public Task<Guid?> GetStayIdOfPaymentAsync(Guid paymentId, CancellationToken ct = default) =>
        db.Set<PaymentAggregate>().AsNoTracking()
            .Where(p => p.Id == paymentId && p.Earmark != null && p.Earmark.Kind == EarmarkKind.BoardingStay)
            .Select(p => (Guid?)p.Earmark!.SourceId)
            .FirstOrDefaultAsync(ct);
}
