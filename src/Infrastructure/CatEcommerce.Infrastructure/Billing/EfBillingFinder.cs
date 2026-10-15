using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Billing;

/// <summary>HO-BIL-001 §11.4 -- the cross-record lookups of the Billing handlers, as <c>AsNoTracking</c> id/value
/// queries. See <see cref="IBillingFinder"/>.</summary>
public sealed class EfBillingFinder(CatEcommerceDbContext db) : IBillingFinder
{
    public async Task<IReadOnlySet<Guid>> GetBilledChargeIdsAsync(IReadOnlyCollection<Guid> chargeIds, CancellationToken ct = default)
    {
        if (chargeIds.Count == 0) return new HashSet<Guid>();

        var ids = chargeIds.Distinct().ToList();
        var billed = await db.Set<InvoiceAggregate>().AsNoTracking()
            .Where(i => i.Status != InvoiceStatus.Discarded)
            .SelectMany(i => i.Lines)
            .Where(l => l.ChargeId != null && ids.Contains(l.ChargeId.Value))
            .Select(l => l.ChargeId!.Value)
            .ToListAsync(ct);

        return billed.ToHashSet();
    }

    public async Task<IReadOnlyList<Guid>> FindUnbilledChargeIdsAsync(UnbilledChargeCriteria criteria, CancellationToken ct = default)
    {
        var charges = db.Charges.AsNoTracking()
            .Where(c => !c.IsMarkedToDelete && c.Status == ChargeStatus.Active && c.CustomerId == criteria.CustomerId);

        if (criteria.BranchId is { } branchId) charges = charges.Where(c => c.BranchId == branchId);

        if (criteria.SourceKind is { } kind && criteria.SourceId is { } sourceId)
            charges = charges.Where(c => c.SourceKind == kind && c.SourceId == sourceId);

        if (criteria.GroupKind is { } groupKind && criteria.GroupId is { } groupId)
            charges = charges.Where(c => c.BillingGroupKind == groupKind && c.BillingGroupId == groupId);

        if (criteria.ServiceDateTo is { } to)
            charges = charges.Where(c => c.ServiceDate <= to);

        var unbilled = charges.Where(c => !db.Set<InvoiceAggregate>()
            .Any(i => i.Status != InvoiceStatus.Discarded && i.Lines.Any(l => l.ChargeId == c.Id)));

        return await unbilled.OrderBy(c => c.ServiceDate).ThenBy(c => c.RecordedOn).ThenBy(c => c.Id).Select(c => c.Id).ToListAsync(ct);
    }

    public async Task<IssuedInvoiceRef?> FindInvoiceByIdempotencyKeyAsync(Guid issuedBy, string key, CancellationToken ct = default) =>
        await db.Set<InvoiceAggregate>().AsNoTracking()
            .Where(i => i.IssuedBy == issuedBy && i.IssueIdempotencyKey == key && i.Number != null)
            .Select(i => new IssuedInvoiceRef(i.Id, i.CustomerId, i.Number!))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Guid>> FindChargeIdsBySourceAsync(ChargeSourceKind kind, Guid sourceId, CancellationToken ct = default) =>
        await db.Charges.AsNoTracking().Where(c => c.SourceKind == kind && c.SourceId == sourceId).OrderBy(c => c.Id).Select(c => c.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> FindMovableChargeIdsAsync(Guid customerId, CancellationToken ct = default)
    {
        var active = db.Charges.AsNoTracking().Where(c => c.CustomerId == customerId && c.Status == ChargeStatus.Active);
        var unbilled = active.Where(c => !db.Set<InvoiceAggregate>()
            .Any(i => i.Status != InvoiceStatus.Discarded && i.Lines.Any(l => l.ChargeId == c.Id)));

        return await unbilled.Select(c => c.Id).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> FindDraftInvoiceIdsAsync(Guid customerId, CancellationToken ct = default) =>
        await db.Set<InvoiceAggregate>().AsNoTracking()
            .Where(i => i.CustomerId == customerId && i.Status == InvoiceStatus.Draft)
            .Select(i => i.Id)
            .ToListAsync(ct);

    public async Task<RecordedPaymentRef?> FindPaymentByIdempotencyKeyAsync(Guid recordedBy, string key, CancellationToken ct = default) =>
        await db.Set<PaymentAggregate>().AsNoTracking()
            .Where(p => p.RecordedBy == recordedBy && p.IdempotencyKey == key)
            .Select(p => new RecordedPaymentRef(p.Id, p.CustomerId))
            .FirstOrDefaultAsync(ct);

    public async Task<long> NextReceiptNumberAsync(CancellationToken ct = default) =>
        await db.Database.SqlQueryRaw<long>("SELECT nextval('billing.receipt_number_seq') AS \"Value\"").SingleAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, CreditedSoFar>> GetCreditedSoFarAsync(Guid invoiceId, CancellationToken ct = default)
    {
        var rows = await db.Set<CreditNoteAggregate>().AsNoTracking()
            .Where(c => c.InvoiceId == invoiceId)
            .SelectMany(c => c.Lines)
            .Select(l => new { l.InvoiceLineId, l.Quantity, l.NetAmount, l.TaxAmount, l.GrossAmount })
            .ToListAsync(ct);

        return rows.GroupBy(r => r.InvoiceLineId).ToDictionary(
            g => g.Key, g => new CreditedSoFar(g.Key, g.Sum(r => r.Quantity ?? 0m), g.Sum(r => r.NetAmount), g.Sum(r => r.TaxAmount), g.Sum(r => r.GrossAmount)));
    }

    public async Task<IReadOnlyList<Guid>> FindPaymentIdsAllocatedToInvoiceAsync(Guid invoiceId, CancellationToken ct = default)
    {
        var rows = await db.Set<PaymentAggregate>().AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Accepted && p.Entries.Any(e => e.InvoiceId == invoiceId))
            .Select(p => new
            {
                p.Id, p.ReceiptNumber,
                Net = p.Entries.Where(e => e.InvoiceId == invoiceId && e.Kind == AllocationEntryKind.Allocate).Sum(e => e.Amount)
                      - p.Entries.Where(e => e.InvoiceId == invoiceId && e.Kind == AllocationEntryKind.Release).Sum(e => e.Amount),
                Newest = p.Entries.Where(e => e.InvoiceId == invoiceId && e.Kind == AllocationEntryKind.Allocate).Max(e => e.OccurredOn),
            })
            .ToListAsync(ct);

        return rows.Where(r => r.Net > 0m).OrderByDescending(r => r.Newest).ThenByDescending(r => r.ReceiptNumber).ThenBy(r => r.Id).Select(r => r.Id).ToList();
    }

    public async Task<IssuedCreditNoteRef?> FindCreditNoteByIdempotencyKeyAsync(Guid issuedBy, string key, CancellationToken ct = default) =>
        await db.Set<CreditNoteAggregate>().AsNoTracking()
            .Where(c => c.IssuedBy == issuedBy && c.IdempotencyKey == key)
            .Select(c => new IssuedCreditNoteRef(c.Id, c.InvoiceId, c.CustomerId, c.Number, c.ReleasedToCustomerCredit))
            .FirstOrDefaultAsync(ct);

    public async Task<IssuedRefundRef?> FindRefundByIdempotencyKeyAsync(Guid issuedBy, string key, CancellationToken ct = default) =>
        await db.Set<RefundAggregate>().AsNoTracking()
            .Where(r => r.IssuedBy == issuedBy && r.IdempotencyKey == key)
            .Select(r => new IssuedRefundRef(r.Id, r.PaymentId, r.CustomerId))
            .FirstOrDefaultAsync(ct);

    public async Task<Guid?> FindCorrectingInvoiceIdAsync(Guid creditNoteId, CancellationToken ct = default) =>
        await db.Set<InvoiceAggregate>().AsNoTracking()
            .Where(i => i.CorrectsCreditNoteId == creditNoteId && i.Status != InvoiceStatus.Discarded)
            .Select(i => (Guid?)i.Id)
            .FirstOrDefaultAsync(ct);

    public Task<IReadOnlyList<CashUpRow>> GetCashUpRowsAsync(Guid branchId, DateOnly businessDate, CancellationToken ct = default) =>
        CashUpRows.LoadAsync(db, branchId, businessDate, ct);

    public Task<int> CountPendingApprovalsAsync(Guid branchId, DateOnly businessDate, string timeZoneId, CancellationToken ct = default) =>
        CashUpRows.CountPendingApprovalsAsync(db, branchId, businessDate, timeZoneId, ct);

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetRemittedTotalsAsync(IReadOnlyCollection<Guid> paymentIds, CancellationToken ct = default)
    {
        var rows = await db.Set<CodRemittanceAggregate>().AsNoTracking()
            .SelectMany(r => r.Lines)
            .Where(l => paymentIds.Contains(l.PaymentId))
            .Select(l => new { l.PaymentId, l.AmountRemitted })
            .ToListAsync(ct);

        return rows.GroupBy(r => r.PaymentId).ToDictionary(g => g.Key, g => g.Sum(r => r.AmountRemitted));
    }

    public async Task<bool> StatementExistsAsync(string bankAccountLabel, string sha256, CancellationToken ct = default) =>
        await db.Set<BankStatementAggregate>().AsNoTracking().AnyAsync(s => s.BankAccountLabel == bankAccountLabel && s.SourceSha256 == sha256, ct);

    public async Task<IReadOnlyList<TransferPaymentCandidate>> FindUnmatchedTransferPaymentsAsync(Guid branchId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var matched = db.Set<BankMatchAggregate>().AsNoTracking().Where(m => m.UnmatchedOn == null).Select(m => m.PaymentId);

        return await db.Set<PaymentAggregate>().AsNoTracking()
            .Where(p => p.BranchId == branchId && p.Status == PaymentStatus.Accepted && p.Method == PaymentMethod.BankTransfer
                        && p.ReceivedOn >= from && p.ReceivedOn <= to && !matched.Contains(p.Id))
            .OrderBy(p => p.ReceivedOn).ThenBy(p => p.Id)
            .Select(p => new TransferPaymentCandidate(p.Id, p.ReceivedOn, p.Amount, p.ExternalReference))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlySet<Guid>> GetActivelyMatchedLineIdsAsync(Guid statementId, CancellationToken ct = default) =>
        (await db.Set<BankMatchAggregate>().AsNoTracking().Where(m => m.StatementId == statementId && m.UnmatchedOn == null).Select(m => m.StatementLineId).ToListAsync(ct)).ToHashSet();

    public async Task<bool> IsPaymentActivelyMatchedAsync(Guid paymentId, CancellationToken ct = default) =>
        await db.Set<BankMatchAggregate>().AsNoTracking().AnyAsync(m => m.PaymentId == paymentId && m.UnmatchedOn == null, ct);

    public async Task<bool> LoyaltyEntryExistsAsync(LoyaltyEntryKind kind, Guid? invoiceId, Guid? creditNoteId, Guid? sourceCustomerId, CancellationToken ct = default) =>
        await db.Set<LoyaltyEntryAggregate>().AsNoTracking().AnyAsync(e => e.Kind == kind && e.IdempotentSource
            && ((invoiceId != null && e.InvoiceId == invoiceId) || (creditNoteId != null && e.CreditNoteId == creditNoteId)
                || (sourceCustomerId != null && e.SourceCustomerId == sourceCustomerId)), ct);

    public async Task<int> GetAccruedNotReversedAsync(Guid invoiceId, CancellationToken ct = default)
    {
        var accrued = await db.Set<LoyaltyEntryAggregate>().AsNoTracking()
            .Where(e => e.Kind == LoyaltyEntryKind.Accrual && e.InvoiceId == invoiceId).SumAsync(e => (int?)e.Points, ct) ?? 0;

        var creditNotes = db.Set<CreditNoteAggregate>().AsNoTracking().Where(c => c.InvoiceId == invoiceId).Select(c => c.Id);
        var reversed = await db.Set<LoyaltyEntryAggregate>().AsNoTracking()
            .Where(e => e.Kind == LoyaltyEntryKind.AccrualReversal && e.CreditNoteId != null && creditNotes.Contains(e.CreditNoteId.Value))
            .SumAsync(e => (int?)e.Points, ct) ?? 0;

        return Math.Max(0, accrued - reversed);
    }

    public async Task<IReadOnlyList<Guid>> FindFreeCreditPaymentIdsAsync(Guid customerId, CancellationToken ct = default)
    {
        var customers = db.Set<CustomerAccountAggregate>().AsNoTracking()
            .Where(c => c.Id == customerId || c.MergedIntoCustomerId == customerId).Select(c => c.Id);

        var rows = await db.Set<PaymentAggregate>().AsNoTracking()
            .Where(p => customers.Contains(p.CustomerId) && p.Status == PaymentStatus.Accepted && (p.Earmark == null || p.Earmark.ReleasedOn != null))
            .Select(p => new
            {
                p.Id, p.AcceptedOn, p.ReceiptNumber,
                Unallocated = p.Amount
                    - p.Entries.Where(e => e.Kind == AllocationEntryKind.Allocate).Sum(e => e.Amount)
                    + p.Entries.Where(e => e.Kind == AllocationEntryKind.Release).Sum(e => e.Amount)
                    - p.RefundedTotal,
            })
            .ToListAsync(ct);

        return rows.Where(r => r.Unallocated > 0m).OrderBy(r => r.AcceptedOn).ThenBy(r => r.ReceiptNumber).ThenBy(r => r.Id).Select(r => r.Id).ToList();
    }

    public async Task<IReadOnlyList<Guid>> FindCreditNoteIdsAsync(Guid invoiceId, CancellationToken ct = default) =>
        await db.Set<CreditNoteAggregate>().AsNoTracking().Where(c => c.InvoiceId == invoiceId).OrderBy(c => c.IssuedOn).Select(c => c.Id).ToListAsync(ct);
}
