using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Inventory;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Inventory;

internal static class RecallReads
{
    public const string Active = "Active";
    public const string Closed = "Closed";
    public const string Withdrawn = "Withdrawn";

    public static string StatusOf(BatchStatus batchStatus, DateTimeOffset? closedOn) =>
        batchStatus != BatchStatus.Recalled ? Withdrawn : closedOn is not null ? Closed : Active;

    public static async Task<List<RecallHeldViewModel>> HeldAsync(CatEcommerceDbContext db, Guid batchId) =>
        (await (from p in db.StockPositions.AsNoTracking()
                join l in db.StockLocations.AsNoTracking() on p.LocationId equals l.Id
                where p.BatchId == batchId && p.OnHand > 0m
                orderby l.Code
                select new { l.Id, l.Code, l.BranchId, p.OnHand }).ToListAsync())
        .Select(h => new RecallHeldViewModel(h.Id, h.Code, h.BranchId, InventoryViewFormat.Quantity(h.OnHand))).ToList();
}

/// <summary>The recall register -- every batch that has a recall record, newest first.</summary>
public sealed class GetRecallsQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetRecallsQuery, PagedResult<RecallSummaryViewModel>>
{
    public async Task<QueryResponse<PagedResult<RecallSummaryViewModel>>> HandleAsync(GetRecallsQuery query)
    {
        var rows = await db.Batches.AsNoTracking().Where(b => b.CurrentRecall != null)
            .Select(b => new
            {
                b.Id, b.SkuId, b.SkuCode, b.DisplayName, b.LotNumber, b.ExpiresOn, b.Status, Number = b.CurrentRecall!.RecallNumber, Declared = b.CurrentRecall.DeclaredOn,
                Notified = b.CurrentRecall.NotifiedRecipientCount, Runs = b.CurrentRecall.NotificationRuns, Closed = b.CurrentRecall.ClosedOn
            }).ToListAsync();

        var held = (await db.StockPositions.AsNoTracking().Where(p => p.OnHand > 0m && rows.Select(r => r.Id).Contains(p.BatchId))
            .GroupBy(p => p.BatchId).Select(g => new { BatchId = g.Key, Quantity = g.Sum(p => p.OnHand) }).ToListAsync()).ToDictionary(h => h.BatchId, h => h.Quantity);

        var all = rows.OrderByDescending(r => r.Declared)
            .Select(r => new RecallSummaryViewModel(
                r.Id, r.Number, RecallReads.StatusOf(r.Status, r.Closed), r.SkuId, r.SkuCode, r.DisplayName, r.LotNumber, r.ExpiresOn, r.Declared, r.Notified, r.Runs,
                InventoryViewFormat.Quantity(held.GetValueOrDefault(r.Id))))
            .Where(r => string.IsNullOrWhiteSpace(query.Status) || string.Equals(r.Status, query.Status, StringComparison.OrdinalIgnoreCase)).ToList();

        return new QueryResponse<PagedResult<RecallSummaryViewModel>>
        { Result = new PagedResult<RecallSummaryViewModel>(all.Skip(query.Skip).Take(query.Take).ToList(), query.PageNumber, query.PageSize, all.Count) };
    }
}

public sealed class GetRecallQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetRecallQuery, RecallDetailViewModel?>
{
    public async Task<QueryResponse<RecallDetailViewModel?>> HandleAsync(GetRecallQuery query)
    {
        var batch = await db.Batches.AsNoTracking().Where(b => b.Id == query.BatchId && b.CurrentRecall != null)
            .Select(b => new { b.Id, b.SkuId, b.SkuCode, b.DisplayName, b.LotNumber, b.ExpiresOn, b.Status, Recall = b.CurrentRecall! }).FirstOrDefaultAsync();
        if (batch is null) return new QueryResponse<RecallDetailViewModel?> { Result = null };

        var held = await RecallReads.HeldAsync(db, batch.Id);
        var r = batch.Recall;
        var summary = new RecallSummaryViewModel(
            batch.Id, r.RecallNumber, RecallReads.StatusOf(batch.Status, r.ClosedOn), batch.SkuId, batch.SkuCode, batch.DisplayName, batch.LotNumber, batch.ExpiresOn, r.DeclaredOn,
            r.NotifiedRecipientCount, r.NotificationRuns, InventoryViewFormat.Quantity(held.Sum(h => decimal.Parse(h.OnHand, System.Globalization.CultureInfo.InvariantCulture))));

        return new QueryResponse<RecallDetailViewModel?>
        {
            Result = new RecallDetailViewModel(
                summary, new BatchRecallViewModel(
                    r.RecallNumber, r.Reason, r.CustomerAdvice, r.DeclaredOn, r.DeclaredBy, r.NotifiedOn, r.NotifiedBy, r.NotifiedRecipientCount, r.NotificationRuns, r.ClosedOn, r.ClosingNote),
                held)
        };
    }
}

/// <summary>
/// HO-IMP-001 section 8.12 (US-IMP-11 AC2, ADR-104) -- the recall trace, read from the immutable ledger. Customers are resolved through the merge chain AT READ
/// TIME (at most five hops), cats show their CURRENT owner, anonymised customers show no contact details, and an entry that names a customer but no cat lists the
/// customer's living cats as household cats. The sections: received, returned (netted per reference), unshipped with dispatch blocked (reservations still open),
/// unidentified sales, and stock held.
/// </summary>
public sealed class GetRecallTraceQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetRecallTraceQuery, RecallTraceViewModel?>
{
    private const int MaxMergeHops = 5;
    private static readonly MovementKind[] Consuming = [MovementKind.Sale, MovementKind.Dispense, MovementKind.ClinicalUse, MovementKind.BoardingUse];

    private sealed record Account(Guid Id, string Name, string? Mobile, string? Email, Guid? MergedInto, CustomerAccountStatus Status);

    public async Task<QueryResponse<RecallTraceViewModel?>> HandleAsync(GetRecallTraceQuery query)
    {
        var batch = await db.Batches.AsNoTracking().Where(b => b.Id == query.BatchId && b.CurrentRecall != null)
            .Select(b => new { b.Id, b.SkuCode, b.DisplayName, b.LotNumber, b.Status, Number = b.CurrentRecall!.RecallNumber }).FirstOrDefaultAsync();
        if (batch is null) return new QueryResponse<RecallTraceViewModel?> { Result = null };

        var entries = await (
            from e in db.StockLedgerEntries.AsNoTracking()
            join l in db.StockLocations.AsNoTracking() on e.LocationId equals l.Id
            where e.BatchId == batch.Id && (Consuming.Contains(e.Kind) || e.Kind == MovementKind.Return)
            orderby e.OccurredOn, e.Sequence
            select new { e.OccurredOn, e.Kind, e.QuantityDelta, e.LocationId, LocationCode = l.Code, e.ReferenceKind, e.ReferenceId, e.ReferenceLabel, e.CustomerId, e.CatId }).ToListAsync();

        // Open reservations are netted per (reference, line) before the party is looked at: a Release carries no party (see OpenReservations).
        var unshipped = await CatEcommerce.Infrastructure.Inventory.OpenReservations.ForBatchAsync(db, batch.Id);

        var catIds = entries.Where(e => e.CatId != null).Select(e => e.CatId!.Value).Concat(unshipped.Where(u => u.CatId != null).Select(u => u.CatId!.Value)).Distinct().ToList();
        var cats = await db.Cats.AsNoTracking().Where(c => catIds.Contains(c.Id)).Select(c => new { c.Id, c.Name, c.OwnerId }).ToDictionaryAsync(c => c.Id);

        var customerIds = entries.Where(e => e.CustomerId != null).Select(e => e.CustomerId!.Value)
            .Concat(unshipped.Where(u => u.CustomerId != null).Select(u => u.CustomerId!.Value)).Concat(cats.Values.Select(c => c.OwnerId)).Distinct().ToList();
        var accounts = await LoadChainAsync(customerIds);
        RecallTraceCustomerViewModel? Customer(Guid? id) => id is null ? null : Resolve(id.Value, accounts);

        var household = new Dictionary<Guid, IReadOnlyList<string>>();
        foreach (var customerId in entries.Where(e => e.CustomerId != null && e.CatId == null && Consuming.Contains(e.Kind)).Select(e => Resolve(e.CustomerId!.Value, accounts)).Where(c => !c.IsAnonymised).Select(c => c.CustomerId).Distinct())
            household[customerId] = await db.Cats.AsNoTracking().Where(c => c.OwnerId == customerId && c.LifeStatus == CatLifeStatus.Alive).OrderBy(c => c.Name).Select(c => c.Name).ToListAsync();

        RecallTraceCatViewModel? Cat(Guid? id) => id is { } cid && cats.TryGetValue(cid, out var c) ? new RecallTraceCatViewModel(cid, c.Name, Customer(c.OwnerId)) : null;

        var received = entries.Where(e => Consuming.Contains(e.Kind) && (e.CustomerId != null || e.CatId != null)).Select(e =>
        {
            var customer = Customer(e.CustomerId);
            return new RecallTraceReceivedViewModel(
                e.OccurredOn, e.Kind.DisplayName, InventoryViewFormat.Quantity(-e.QuantityDelta), e.LocationId, e.LocationCode, e.ReferenceLabel, customer, Cat(e.CatId),
                e.CatId is null && customer is { IsAnonymised: false } ? household.GetValueOrDefault(customer.CustomerId) ?? [] : []);
        }).ToList();

        var returned = entries.Where(e => e.Kind == MovementKind.Return).GroupBy(e => new { e.ReferenceKind, e.ReferenceId, e.CustomerId }).Select(g => new RecallTraceReturnedViewModel(
            g.Key.ReferenceKind.DisplayName, g.Key.ReferenceId, g.Select(e => e.ReferenceLabel).FirstOrDefault(l => l != null), InventoryViewFormat.Quantity(g.Sum(e => e.QuantityDelta)),
            Customer(g.Key.CustomerId))).ToList();

        var unshippedRows = unshipped.GroupBy(u => new { u.ReferenceKind, u.ReferenceId, u.CustomerId, u.CatId }).Select(g => new RecallTraceUnshippedViewModel(
            g.Key.ReferenceKind.DisplayName, g.Key.ReferenceId, InventoryViewFormat.Quantity(g.Sum(u => u.Remaining)), Customer(g.Key.CustomerId), Cat(g.Key.CatId))).ToList();

        var unidentified = entries.Where(e => Consuming.Contains(e.Kind) && e.CustomerId == null && e.CatId == null).ToList();

        return new QueryResponse<RecallTraceViewModel?>
        {
            Result = new RecallTraceViewModel(
                batch.Id, batch.Number, batch.SkuCode, batch.DisplayName, batch.LotNumber, batch.Status.DisplayName, received, returned, unshippedRows,
                new RecallTraceUnidentifiedViewModel(unidentified.Count, InventoryViewFormat.Quantity(unidentified.Sum(e => -e.QuantityDelta))), await RecallReads.HeldAsync(db, batch.Id))
        };
    }

    /// <summary>The accounts of <paramref name="ids"/> and, hop by hop (at most five), every account they were merged into.</summary>
    private async Task<Dictionary<Guid, Account>> LoadChainAsync(IReadOnlyCollection<Guid> ids)
    {
        var loaded = new Dictionary<Guid, Account>();
        var pending = ids.ToList();
        for (var hop = 0; hop <= MaxMergeHops && pending.Count > 0; hop++)
        {
            var rows = await db.CustomerAccounts.AsNoTracking().Where(c => pending.Contains(c.Id))
                .Select(c => new { c.Id, c.FullName, Mobile = c.MobileNumber != null ? c.MobileNumber.Value : null, Email = c.EmailAddress != null ? c.EmailAddress.Value : null, c.MergedIntoCustomerId, c.Status })
                .ToListAsync();
            foreach (var row in rows) loaded[row.Id] = new Account(row.Id, row.FullName, row.Mobile, row.Email, row.MergedIntoCustomerId, row.Status);
            pending = rows.Where(r => r.MergedIntoCustomerId != null && !loaded.ContainsKey(r.MergedIntoCustomerId.Value)).Select(r => r.MergedIntoCustomerId!.Value).Distinct().ToList();
        }

        return loaded;
    }

    private static RecallTraceCustomerViewModel Resolve(Guid recordedId, IReadOnlyDictionary<Guid, Account> accounts)
    {
        var current = recordedId;
        for (var hop = 0; hop < MaxMergeHops && accounts.TryGetValue(current, out var account) && account.MergedInto is { } next; hop++) current = next;

        if (!accounts.TryGetValue(current, out var survivor) || survivor.Status == CustomerAccountStatus.Anonymised)
            return new RecallTraceCustomerViewModel(current, "Anonymised customer", null, null, true, current == recordedId ? null : recordedId);

        return new RecallTraceCustomerViewModel(current, survivor.Name, survivor.Mobile, survivor.Email, false, current == recordedId ? null : recordedId);
    }
}
