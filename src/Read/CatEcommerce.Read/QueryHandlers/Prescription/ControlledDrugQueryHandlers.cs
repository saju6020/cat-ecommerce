using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Pharmacy;
using CatEcommerce.Read.Queries.Prescription;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Prescription;

/// <summary>The register, newest first (§12.4): explicit projection, <c>AsNoTracking</c>, paged in SQL, with each entry's running balance as a correlated window sum
/// over <c>entry_number</c> per medicine and batch.</summary>
public sealed class GetControlledDrugRegisterQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetControlledDrugRegisterQuery, PagedResult<RegisterEntryViewModel>>
{
    public async Task<QueryResponse<PagedResult<RegisterEntryViewModel>>> HandleAsync(GetControlledDrugRegisterQuery query)
    {
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 50 : query.PageSize, 1, 100);
        var pageNumber = Math.Max(query.PageNumber, 1);

        var entries = ControlledDrugReads.Filtered(db, query.BranchId, query.MedicineId, query.BatchId, query.Kind, query.From, query.To);
        var total = await entries.CountAsync();

        var ids = await entries.OrderByDescending(e => e.EntryNumber).Skip((pageNumber - 1) * pageSize).Take(pageSize).Select(e => e.Id).ToListAsync();
        var items = await RegisterEntryViews.LoadAsync(db, ids);

        return new QueryResponse<PagedResult<RegisterEntryViewModel>> { Result = new PagedResult<RegisterEntryViewModel>(items.OrderByDescending(i => i.EntryNumber).ToList(), pageNumber, pageSize, total) };
    }
}

/// <summary>Builds the register view models for a set of entry ids -- rows, running balances, names, source labels, correction links and seal flags in a handful of
/// queries (never one per row).</summary>
internal static class RegisterEntryViews
{
    public static async Task<List<RegisterEntryViewModel>> LoadAsync(CatEcommerceDbContext db, IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return [];
        var idList = ids.ToList();

        var rows = await db.ControlledDrugRegisterEntries.Where(e => idList.Contains(e.Id)).AsRows().ToListAsync(ct);

        // Running balance as of each entry: Σ delta of the same medicine and batch up to and including this entry number.
        var balances = await db.ControlledDrugRegisterEntries.AsNoTracking().Where(e => idList.Contains(e.Id))
            .Select(e => new
            {
                e.Id,
                Balance = db.ControlledDrugRegisterEntries
                    .Where(o => o.BranchId == e.BranchId && o.MedicineId == e.MedicineId && o.BatchId == e.BatchId && o.EntryNumber <= e.EntryNumber)
                    .Sum(o => o.QuantityDelta),
                IsSealed = db.ControlledDrugRegisterSeals.Any(s => s.Id == e.Id)
            })
            .ToListAsync(ct);
        var balanceById = balances.ToDictionary(b => b.Id);

        var corrected = await db.ControlledDrugRegisterEntries.AsNoTracking()
            .Where(e => e.CorrectsEntryId != null && idList.Contains(e.CorrectsEntryId.Value))
            .Select(e => new { Target = e.CorrectsEntryId!.Value, e.EntryNumber })
            .ToListAsync(ct);
        var correctedBy = corrected.GroupBy(c => c.Target).ToDictionary(g => g.Key, g => (IReadOnlyList<long>)g.Select(c => c.EntryNumber).OrderBy(n => n).ToList());

        var targetIds = rows.Where(r => r.CorrectsEntryId != null).Select(r => r.CorrectsEntryId!.Value).Distinct().ToList();
        var targetNumbers = targetIds.Count == 0
            ? new Dictionary<Guid, long>()
            : (await db.ControlledDrugRegisterEntries.AsNoTracking().Where(e => targetIds.Contains(e.Id)).Select(e => new { e.Id, e.EntryNumber }).ToListAsync(ct))
                .ToDictionary(e => e.Id, e => e.EntryNumber);

        var staff = await PrescriptionReads.StaffNamesAsync(db, rows.SelectMany(r => new Guid?[] { r.ActorId, r.WitnessId }), ct);
        var cats = await ControlledDrugReads.CatNamesAsync(db, rows.Select(r => r.CatId), ct);
        var customers = await ControlledDrugReads.CustomerNamesAsync(db, rows.Select(r => r.CustomerId), ct);
        var labels = await ControlledDrugReads.SourceLabelsAsync(db, rows.Select(r => (r.SourceType, r.SourceId)), ct);

        return rows.Select(r =>
        {
            var unit = r.MedicineDispensingUnit.DisplayName;
            return new RegisterEntryViewModel(
                r.Id, r.EntryNumber, r.RecordedOn, r.OccurredOn, r.Kind.DisplayName,
                new RegisterMedicineViewModel(r.MedicineId, r.MedicineName, r.MedicineStrengthText, r.MedicineForm.DisplayName, r.MedicineControlledSchedule),
                r.BatchId, r.LotNumber, r.BatchExpiresOn, QuantityViewModel.Of(r.QuantityDelta, unit), QuantityViewModel.Of(balanceById[r.Id].Balance, unit), r.DoseText,
                r.WastageQuantity is { } wastage ? QuantityViewModel.Of(wastage, unit) : null,
                r.CatId is { } cat && cats.TryGetValue(cat, out var catName) ? catName : null,
                r.CustomerId is { } customer && customers.TryGetValue(customer, out var customerName) ? customerName : null,
                PrescriptionReads.NameOf(staff, r.ActorId), r.WitnessId is null ? null : PrescriptionReads.NameOf(staff, r.WitnessId),
                new RegisterSourceViewModel(r.SourceType.DisplayName, r.SourceId, labels[(r.SourceType, r.SourceId)]), r.CorrectionReason, r.Notes,
                r.CorrectsEntryId is { } target && targetNumbers.TryGetValue(target, out var targetNumber) ? targetNumber : null,
                correctedBy.GetValueOrDefault(r.Id) ?? [], balanceById[r.Id].IsSealed);
        }).ToList();
    }
}

public sealed class GetControlledDrugEntryQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetControlledDrugEntryQuery, RegisterEntryDetailViewModel?>
{
    public async Task<QueryResponse<RegisterEntryDetailViewModel?>> HandleAsync(GetControlledDrugEntryQuery query)
    {
        var inBranch = await db.ControlledDrugRegisterEntries.AsNoTracking().AnyAsync(e => e.Id == query.EntryId && e.BranchId == query.BranchId);
        if (!inBranch) return new QueryResponse<RegisterEntryDetailViewModel?> { Result = null };

        var correctionIds = await db.ControlledDrugRegisterEntries.AsNoTracking().Where(e => e.CorrectsEntryId == query.EntryId).OrderBy(e => e.EntryNumber).Select(e => e.Id).ToListAsync();
        var views = await RegisterEntryViews.LoadAsync(db, [query.EntryId, .. correctionIds]);

        var seal = await db.ControlledDrugRegisterSeals.AsNoTracking().Where(s => s.Id == query.EntryId)
            .Select(s => new RegisterSealViewModel(s.ChainPosition, s.EntryHash, s.SealedOn)).FirstOrDefaultAsync();

        return new QueryResponse<RegisterEntryDetailViewModel?>
        {
            Result = new RegisterEntryDetailViewModel(views.First(v => v.Id == query.EntryId), views.Where(v => v.Id != query.EntryId).OrderBy(v => v.EntryNumber).ToList(), seal)
        };
    }
}

/// <summary>Register balance against stock, per medicine and batch (HO-PHM-001 §8.9, OQ-10 -- all locations of the branch). A difference is a <c>Mismatch</c> with both
/// figures; nothing is auto-corrected.</summary>
public sealed class GetControlledDrugBalancesQueryHandler(CatEcommerceDbContext db, IStockLedger ledger)
    : IQueryHandlerAsync<GetControlledDrugBalancesQuery, IReadOnlyList<RegisterBalanceViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<RegisterBalanceViewModel>>> HandleAsync(GetControlledDrugBalancesQuery query)
    {
        var register = await db.ControlledDrugRegisterEntries.AsNoTracking().Where(e => e.BranchId == query.BranchId)
            .GroupBy(e => new { e.MedicineId, e.BatchId })
            .Select(g => new { g.Key.MedicineId, g.Key.BatchId, Balance = g.Sum(e => e.QuantityDelta) })
            .ToListAsync();

        var labels = await db.ControlledDrugRegisterEntries.AsNoTracking().Where(e => e.BranchId == query.BranchId)
            .Select(e => new { e.MedicineId, e.MedicineName, e.MedicineDispensingUnit, e.BatchId, e.LotNumber, e.SkuId }).Distinct().ToListAsync();

        var skuIds = labels.Where(l => l.SkuId != null).Select(l => l.SkuId!.Value).Distinct().ToList();
        var stock = skuIds.Count == 0 ? null : await ledger.GetBranchBalancesAsync(query.BranchId, skuIds, CancellationToken.None);
        var onHandByBatch = (stock?.Value ?? []).GroupBy(b => b.BatchId).ToDictionary(g => g.Key, g => g.Sum(b => b.OnHand));

        var rows = register.Select(r =>
        {
            var label = labels.First(l => l.MedicineId == r.MedicineId && l.BatchId == r.BatchId);
            var unit = label.MedicineDispensingUnit.DisplayName;
            decimal? stockBalance = r.BatchId is { } batch ? onHandByBatch.GetValueOrDefault(batch, 0m) : null;
            return new RegisterBalanceViewModel(
                r.MedicineId, label.MedicineName, r.BatchId, label.LotNumber, QuantityViewModel.Of(r.Balance, unit),
                stockBalance is { } onHand ? QuantityViewModel.Of(onHand, unit) : null, stockBalance is { } expected && expected != r.Balance);
        }).OrderBy(r => r.MedicineName).ThenBy(r => r.LotNumber).ToList();

        return new QueryResponse<IReadOnlyList<RegisterBalanceViewModel>> { Result = rows };
    }
}

/// <summary>
/// Recomputes the branch's chain from the ENTRIES (HO-PHM-001 §8.9, ADR-145): for each seal in position order, the previous hash must be the previous recomputed hash
/// and the seal's hash must equal <c>SHA-256(previous || canonical form of the entry as stored now)</c>. The first position that disagrees is reported; an entry whose
/// seal is missing from the chain is not a break, but one unsealed for over <c>UnsealedAlertMinutes</c> is listed (a stuck worker is itself a finding). Read-only; walks the
/// chain in bounded chunks so a long register never sits in memory whole.
/// </summary>
public sealed class VerifyControlledDrugRegisterQueryHandler(CatEcommerceDbContext db, TimeProvider timeProvider)
    : IQueryHandlerAsync<VerifyControlledDrugRegisterQuery, RegisterVerificationViewModel>
{
    private const int ChunkSize = 500;

    public async Task<QueryResponse<RegisterVerificationViewModel>> HandleAsync(VerifyControlledDrugRegisterQuery query)
    {
        var now = timeProvider.GetUtcNow();
        long expectedPosition = 1;
        var previousHash = ControlledDrugRules.ZeroHash;
        var checkedCount = 0;
        long? firstBroken = null;

        var lastPosition = 0L;
        while (firstBroken is null)
        {
            var seals = await db.ControlledDrugRegisterSeals.AsNoTracking()
                .Where(s => s.BranchId == query.BranchId && s.ChainPosition > lastPosition)
                .OrderBy(s => s.ChainPosition).Take(ChunkSize)
                .Select(s => new { s.Id, s.ChainPosition, s.PreviousHash, s.EntryHash })
                .ToListAsync();
            if (seals.Count == 0) break;

            var ids = seals.Select(s => s.Id).ToList();
            var entries = (await db.ControlledDrugRegisterEntries.Where(e => ids.Contains(e.Id)).AsRows().ToListAsync()).ToDictionary(e => e.Id);

            foreach (var seal in seals)
            {
                checkedCount++;
                lastPosition = seal.ChainPosition;

                if (seal.ChainPosition != expectedPosition || seal.PreviousHash != previousHash || !entries.TryGetValue(seal.Id, out var entry))
                {
                    firstBroken = seal.ChainPosition;
                    break;
                }

                var recomputed = ControlledDrugEntryCanonicalForm.Hash(previousHash, entry.ToCanonical());
                if (!string.Equals(recomputed, seal.EntryHash, StringComparison.Ordinal))
                {
                    firstBroken = seal.ChainPosition;
                    break;
                }

                previousHash = recomputed;
                expectedPosition++;
            }
        }

        var staleCutoff = now.AddMinutes(-ControlledDrugRules.UnsealedAlertMinutes);
        var stale = await db.ControlledDrugRegisterEntries.AsNoTracking()
            .Where(e => e.BranchId == query.BranchId && e.RecordedOn < staleCutoff && !db.ControlledDrugRegisterSeals.Any(s => s.Id == e.Id))
            .OrderBy(e => e.EntryNumber).Take(50)
            .Select(e => new UnsealedEntryViewModel(e.EntryNumber, e.RecordedOn))
            .ToListAsync();

        return new QueryResponse<RegisterVerificationViewModel> { Result = new RegisterVerificationViewModel(firstBroken is null, checkedCount, firstBroken, stale, now) };
    }
}

/// <summary>The inspection export: one row per entry with its running balance (per medicine and batch), its seal position and hash and its corrections' entry numbers.
/// At most <c>MaxRows</c> rows are read; the controller refuses a bigger result with 413.</summary>
public sealed class ExportControlledDrugRegisterQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<ExportControlledDrugRegisterQuery, IReadOnlyList<RegisterExportRow>>
{
    public async Task<QueryResponse<IReadOnlyList<RegisterExportRow>>> HandleAsync(ExportControlledDrugRegisterQuery query)
    {
        var ct = CancellationToken.None;
        var filtered = ControlledDrugReads.Filtered(db, query.BranchId, query.MedicineId, null, null, query.From, query.To);
        var rows = await filtered.OrderBy(e => e.EntryNumber).Take(query.MaxRows).AsRows().ToListAsync(ct);
        if (rows.Count == 0) return new QueryResponse<IReadOnlyList<RegisterExportRow>> { Result = [] };

        // The balance before the first row of the window, per medicine and batch -- so the running balance is the register's, not the window's.
        var firstNumber = rows[0].EntryNumber;
        var opening = (await db.ControlledDrugRegisterEntries.AsNoTracking().Where(e => e.BranchId == query.BranchId && e.EntryNumber < firstNumber)
                .GroupBy(e => new { e.MedicineId, e.BatchId }).Select(g => new { g.Key.MedicineId, g.Key.BatchId, Sum = g.Sum(e => e.QuantityDelta) }).ToListAsync(ct))
            .ToDictionary(o => (o.MedicineId, o.BatchId), o => o.Sum);

        var seals = (await (
                from e in filtered
                join s in db.ControlledDrugRegisterSeals.AsNoTracking() on e.Id equals s.Id
                select new { e.Id, s.ChainPosition, s.EntryHash }).ToListAsync(ct))
            .ToDictionary(s => s.Id);

        var corrections = await db.ControlledDrugRegisterEntries.AsNoTracking().Where(e => e.BranchId == query.BranchId && e.CorrectsEntryId != null)
            .Select(e => new { Target = e.CorrectsEntryId!.Value, e.EntryNumber }).ToListAsync(ct);
        var correctedBy = corrections.GroupBy(c => c.Target).ToDictionary(g => g.Key, g => string.Join(" ", g.Select(c => c.EntryNumber).OrderBy(n => n)));
        var numberById = (await db.ControlledDrugRegisterEntries.AsNoTracking().Where(e => e.BranchId == query.BranchId && e.CorrectsEntryId != null)
            .Select(e => e.CorrectsEntryId!.Value).Distinct().ToListAsync(ct)).ToList();
        var targetNumbers = numberById.Count == 0
            ? new Dictionary<Guid, long>()
            : (await db.ControlledDrugRegisterEntries.AsNoTracking().Where(e => numberById.Contains(e.Id)).Select(e => new { e.Id, e.EntryNumber }).ToListAsync(ct)).ToDictionary(e => e.Id, e => e.EntryNumber);

        var staff = await PrescriptionReads.StaffNamesAsync(db, rows.SelectMany(r => new Guid?[] { r.ActorId, r.WitnessId }), ct);
        var cats = await ControlledDrugReads.CatNamesAsync(db, rows.Select(r => r.CatId), ct);
        var labels = await ControlledDrugReads.SourceLabelsAsync(db, rows.Select(r => (r.SourceType, r.SourceId)), ct);

        var running = new Dictionary<(Guid, Guid?), decimal>(opening);
        var result = new List<RegisterExportRow>(rows.Count);
        foreach (var r in rows)
        {
            var key = (r.MedicineId, r.BatchId);
            running[key] = running.GetValueOrDefault(key) + r.QuantityDelta;
            seals.TryGetValue(r.Id, out var seal);

            result.Add(new RegisterExportRow(
                r.EntryNumber, r.RecordedOn, r.OccurredOn, r.Kind.DisplayName, r.MedicineName, r.MedicineStrengthText, r.LotNumber, r.BatchExpiresOn, r.QuantityDelta,
                running[key], r.MedicineDispensingUnit.DisplayName, r.DoseText, r.WastageQuantity, r.CatId is { } cat && cats.TryGetValue(cat, out var catName) ? catName : null,
                PrescriptionReads.NameOf(staff, r.ActorId), r.WitnessId is null ? null : PrescriptionReads.NameOf(staff, r.WitnessId), r.SourceType.DisplayName,
                labels[(r.SourceType, r.SourceId)], r.CorrectionReason, r.Notes, r.CorrectsEntryId is { } target && targetNumbers.TryGetValue(target, out var number) ? number : null,
                correctedBy.GetValueOrDefault(r.Id) ?? "", seal?.ChainPosition, seal?.EntryHash));
        }

        return new QueryResponse<IReadOnlyList<RegisterExportRow>> { Result = result };
    }
}

/// <summary>The branches the worker should seal: those with any register entry at all (a pass over a branch with nothing unsealed writes nothing).</summary>
public sealed class GetBranchesWithUnsealedRegisterEntriesQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetBranchesWithUnsealedRegisterEntriesQuery, IReadOnlyList<Guid>>
{
    public async Task<QueryResponse<IReadOnlyList<Guid>>> HandleAsync(GetBranchesWithUnsealedRegisterEntriesQuery query)
    {
        IReadOnlyList<Guid> branches = await db.ControlledDrugRegisterEntries.AsNoTracking()
            .Where(e => !db.ControlledDrugRegisterSeals.Any(s => s.Id == e.Id))
            .Select(e => e.BranchId).Distinct().ToListAsync();

        return new QueryResponse<IReadOnlyList<Guid>> { Result = branches };
    }
}
