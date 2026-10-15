using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Read.QueryHandlers.Billing;

/// <summary>A payment header as the read side needs it: scalars, the collector and earmark value objects, and what has been allocated and released --
/// all selected in SQL. The read side never rehydrates a <c>PaymentAggregate</c> (ADR-012, read-side-pattern section 4).</summary>
internal sealed record PaymentHeader(
    Guid Id, long? ReceiptNumber, Guid CustomerId, Guid BranchId, PaymentMethod Method, PaymentStatus Status, decimal Amount, string CurrencyCode,
    DateOnly ReceivedOn, string? ExternalReference, string? Notes, Guid RecordedBy, DateTimeOffset RecordedOn, Guid? ApprovedBy, DateTimeOffset? ApprovedOn,
    Guid? RejectedBy, DateTimeOffset? RejectedOn, string? RejectionReason, DateTimeOffset? AcceptedOn, DateOnly? BusinessDate, Guid? ReversedBy,
    DateTimeOffset? ReversedOn, string? ReversalReason, decimal RefundedTotal, int Version, decimal Allocated, decimal Released,
    PaymentCollector? Collector, PaymentEarmark? Earmark)
{
    /// <summary>What can still be applied or refunded (the aggregate's own <c>Unallocated</c>).</summary>
    public decimal Unallocated => Amount - (Allocated - Released) - RefundedTotal;
}

internal sealed record PaymentEntryRow(Guid Id, AllocationEntryKind Kind, Guid InvoiceId, decimal Amount, DateTimeOffset OccurredOn, Guid By, AllocationReleaseReason? ReleaseReason);

internal sealed record RequestedAllocationRow(Guid InvoiceId, decimal Amount);

internal static class PaymentProjections
{
    public static Task<PaymentHeader?> HeaderAsync(IQueryable<PaymentAggregate> payments) =>
        payments.Select(p => new PaymentHeader(
            p.Id, p.ReceiptNumber, p.CustomerId, p.BranchId, p.Method, p.Status, p.Amount, p.CurrencyCode, p.ReceivedOn, p.ExternalReference, p.Notes, p.RecordedBy,
            p.RecordedOn, p.ApprovedBy, p.ApprovedOn, p.RejectedBy, p.RejectedOn, p.RejectionReason, p.AcceptedOn, p.BusinessDate, p.ReversedBy, p.ReversedOn,
            p.ReversalReason, p.RefundedTotal, p.Version,
            p.Entries.Where(e => e.Kind == AllocationEntryKind.Allocate).Sum(e => e.Amount),
            p.Entries.Where(e => e.Kind == AllocationEntryKind.Release).Sum(e => e.Amount),
            p.Collector, p.Earmark)).FirstOrDefaultAsync();

    public static async Task<IReadOnlyList<PaymentEntryRow>> EntriesAsync(CatEcommerceDbContext db, Guid paymentId) =>
        await db.Set<PaymentAggregate>().AsNoTracking().Where(p => p.Id == paymentId).SelectMany(p => p.Entries)
            .OrderBy(e => e.OccurredOn).ThenBy(e => e.Id)
            .Select(e => new PaymentEntryRow(e.Id, e.Kind, e.InvoiceId, e.Amount, e.OccurredOn, e.By, e.ReleaseReason))
            .ToListAsync();

    public static async Task<IReadOnlyList<RequestedAllocationRow>> RequestedAsync(CatEcommerceDbContext db, Guid paymentId) =>
        await db.Set<PaymentAggregate>().AsNoTracking().Where(p => p.Id == paymentId).SelectMany(p => p.RequestedAllocations)
            .Select(r => new RequestedAllocationRow(r.InvoiceId, r.Amount)).ToListAsync();

    /// <summary>Σ Allocate − Σ Release per invoice, positive only -- what a payment currently covers.</summary>
    public static IReadOnlyList<(Guid InvoiceId, decimal Amount)> Effective(IEnumerable<PaymentEntryRow> entries) =>
        entries.GroupBy(e => e.InvoiceId)
            .Select(g => (InvoiceId: g.Key, Amount: g.Sum(e => e.Kind == AllocationEntryKind.Allocate ? e.Amount : -e.Amount)))
            .Where(x => x.Amount > 0m)
            .ToList();

    public static async Task<Dictionary<Guid, string>> StaffNamesAsync(CatEcommerceDbContext db, IEnumerable<Guid?> ids)
    {
        var wanted = ids.Where(i => i is not null).Select(i => i!.Value).Distinct().ToList();
        if (wanted.Count == 0) return [];

        var rows = await db.Set<StaffAccountAggregate>().AsNoTracking().Where(s => wanted.Contains(s.Id)).Select(s => new { s.Id, s.FullName }).ToListAsync();
        return rows.ToDictionary(r => r.Id, r => r.FullName);
    }

    public static async Task<Dictionary<Guid, string?>> InvoiceNumbersAsync(CatEcommerceDbContext db, IEnumerable<Guid> ids)
    {
        var wanted = ids.Distinct().ToList();
        if (wanted.Count == 0) return [];

        var rows = await db.Set<InvoiceAggregate>().AsNoTracking().Where(i => wanted.Contains(i.Id)).Select(i => new { i.Id, i.Number }).ToListAsync();
        return rows.ToDictionary(r => r.Id, r => r.Number);
    }

    public static async Task<Dictionary<Guid, string>> CustomerNamesAsync(CatEcommerceDbContext db, IEnumerable<Guid> ids)
    {
        var wanted = ids.Distinct().ToList();
        if (wanted.Count == 0) return [];

        var rows = await db.Set<CustomerAccountAggregate>().AsNoTracking().Where(c => wanted.Contains(c.Id)).Select(c => new { c.Id, c.FullName }).ToListAsync();
        return rows.ToDictionary(r => r.Id, r => r.FullName);
    }
}
