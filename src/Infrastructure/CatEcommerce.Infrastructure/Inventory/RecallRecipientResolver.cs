using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Inventory;

/// <summary>
/// HO-IMP-001 section 8.12 / ADR-104 -- read-only. The customers to notify of a recall: the PURCHASERS of the batch (consuming ledger entries and the orders still
/// holding a reservation of it) plus the CURRENT owners of the cats it was dispensed or used on, each resolved through HO-ACC-001's merge chain at READ time
/// (the ledger is immutable, so nothing rewrites it when two customers merge) and cut off after <see cref="MaxMergeHops"/> hops. Anonymised customers and ids
/// that were merged away are excluded; so is anyone whose request with the key <c>recall:{batchId}:{customerId}</c> already exists.
/// </summary>
public sealed class RecallRecipientResolver(CatEcommerceDbContext db) : IRecallRecipientResolver
{
    private const int MaxMergeHops = 5;

    private static readonly MovementKind[] Consuming = [MovementKind.Sale, MovementKind.Dispense, MovementKind.ClinicalUse, MovementKind.BoardingUse];

    public async Task<IReadOnlyList<RecallRecipient>> ResolveAsync(Guid batchId, CancellationToken ct)
    {
        var batchBranch = await db.Batches.AsNoTracking().Where(b => b.Id == batchId).Select(b => (Guid?)b.BranchId).FirstOrDefaultAsync(ct);
        if (batchBranch is null) return [];

        var survivors = await SurvivingRecipientsAsync(batchId, ct);
        if (survivors.Count == 0) return [];

        var keys = survivors.ToDictionary(id => RecallRules.DeduplicationKey(batchId, id), id => id);
        var keyList = keys.Keys.ToList();
        var alreadyKeys = (await db.NotificationRequests.AsNoTracking()
            .Where(r => r.DeduplicationKey != null && keyList.Contains(r.DeduplicationKey)).Select(r => r.DeduplicationKey!).ToListAsync(ct)).ToHashSet();

        return survivors.Where(id => !alreadyKeys.Contains(RecallRules.DeduplicationKey(batchId, id)))
            .OrderBy(id => id).Select(id => new RecallRecipient(id, batchBranch.Value)).ToList();
    }

    public async Task<int> CountAlreadyNotifiedAsync(Guid batchId, CancellationToken ct)
    {
        var prefix = $"recall:{batchId}:";
        return await db.NotificationRequests.AsNoTracking().CountAsync(r => r.DeduplicationKey != null && r.DeduplicationKey.StartsWith(prefix), ct);
    }

    /// <summary>The distinct customers who should hear: purchasers and current cat owners, followed through merges, minus the anonymised and merged-away.</summary>
    private async Task<HashSet<Guid>> SurvivingRecipientsAsync(Guid batchId, CancellationToken ct)
    {
        var purchasers = await db.StockLedgerEntries.AsNoTracking()
            .Where(e => e.BatchId == batchId && Consuming.Contains(e.Kind) && e.CustomerId != null).Select(e => e.CustomerId!.Value).Distinct().ToListAsync(ct);

        var open = await OpenReservations.ForBatchAsync(db, batchId, ct); // released and consumed reservations are not unshipped any more
        var reservers = open.Where(r => r.CustomerId != null).Select(r => r.CustomerId!.Value).Distinct().ToList();

        var catIds = await db.StockLedgerEntries.AsNoTracking()
            .Where(e => e.BatchId == batchId && Consuming.Contains(e.Kind) && e.CatId != null).Select(e => e.CatId!.Value).Distinct().ToListAsync(ct);
        var catIdsReserved = open.Where(r => r.CatId != null).Select(r => r.CatId!.Value).Distinct().ToList();
        var owners = await db.Cats.AsNoTracking().Where(c => catIds.Contains(c.Id) || catIdsReserved.Contains(c.Id)).Select(c => c.OwnerId).Distinct().ToListAsync(ct);

        var start = purchasers.Concat(reservers).Concat(owners).Distinct().ToList();
        var chain = await db.CustomerAccounts.AsNoTracking()
            .Where(c => start.Contains(c.Id)).Select(c => new { c.Id, c.MergedIntoCustomerId, c.Status }).ToDictionaryAsync(c => c.Id, ct);

        var result = new HashSet<Guid>();
        foreach (var id in start)
        {
            var resolved = await FollowMergesAsync(id, chain.GetValueOrDefault(id)?.MergedIntoCustomerId, ct);
            if (resolved is { } survivor) result.Add(survivor);
        }

        return result;
    }

    private async Task<Guid?> FollowMergesAsync(Guid id, Guid? mergedInto, CancellationToken ct)
    {
        var current = id;
        var next = mergedInto;
        for (var hop = 0; hop < MaxMergeHops && next is not null; hop++)
        {
            current = next.Value;
            next = await db.CustomerAccounts.AsNoTracking().Where(c => c.Id == current).Select(c => c.MergedIntoCustomerId).FirstOrDefaultAsync(ct);
        }

        var status = await db.CustomerAccounts.AsNoTracking().Where(c => c.Id == current).Select(c => c.Status).FirstOrDefaultAsync(ct);
        return status is null || status == CustomerAccountStatus.Anonymised || status == CustomerAccountStatus.Merged ? null : current;
    }
}
