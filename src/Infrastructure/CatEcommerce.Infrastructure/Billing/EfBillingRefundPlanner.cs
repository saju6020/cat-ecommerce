using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Billing;

/// <summary>HO-BIL-001 §8.13 (Amended 2026-10-09) -- <see cref="IBillingRefundPlanner"/> over Billing's own records, <c>AsNoTracking</c>; nothing is tracked or
/// written, so it can be called inside a producer's transaction without affecting the change tracker.</summary>
public sealed class EfBillingRefundPlanner(CatEcommerceDbContext db, IBillingFinder finder) : IBillingRefundPlanner
{
    public async Task<CreditPlan> PlanCreditAsync(Guid invoiceId, decimal amount, CancellationToken ct = default)
    {
        var invoiceLines = await db.Set<InvoiceAggregate>().AsNoTracking()
            .Where(i => i.Id == invoiceId)
            .SelectMany(i => i.Lines)
            .Select(l => new { l.Id, l.SortOrder, l.GrossAmount })
            .ToListAsync(ct);

        if (invoiceLines.Count == 0)
            return new CreditPlan(false, [], BusinessViolationCodes.InvoiceNotFound, "The invoice was not found.");

        var credited = await finder.GetCreditedSoFarAsync(invoiceId, ct);
        var lines = new List<CreditLinePlan>();
        var left = amount;
        var remainingTotal = 0m;
        foreach (var line in invoiceLines.OrderBy(l => l.SortOrder))
        {
            var remaining = line.GrossAmount - (credited.TryGetValue(line.Id, out var so) ? so.Gross : 0m);
            remainingTotal += remaining;
            if (remaining <= 0m || left <= 0m) continue;

            var take = Math.Min(remaining, left);
            lines.Add(new CreditLinePlan(line.Id, take));
            left -= take;
        }

        return left > 0m
            ? new CreditPlan(false, [], BusinessViolationCodes.CreditExceedsInvoice, "That is more than is left to credit on the invoice.")
            : new CreditPlan(amount == remainingTotal, lines, null, null);
    }

    public async Task<RefundDrawPlan> PlanRefundAsync(IReadOnlyList<Guid> paymentIdsInOrder, decimal amount, CancellationToken ct = default)
    {
        var draws = new List<RefundDraw>();
        var remaining = amount;
        foreach (var paymentId in paymentIdsInOrder)
        {
            if (remaining <= 0m) break;

            var payment = await db.Set<PaymentAggregate>().AsNoTracking().FirstOrDefaultAsync(p => p.Id == paymentId, ct);
            var take = Math.Min(remaining, Math.Max(0m, payment?.Unallocated ?? 0m));
            if (take <= 0m) continue;

            draws.Add(new RefundDraw(paymentId, take));
            remaining -= take;
        }

        return new RefundDrawPlan(draws, remaining);
    }
}
