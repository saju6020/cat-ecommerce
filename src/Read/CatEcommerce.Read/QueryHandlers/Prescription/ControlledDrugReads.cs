using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Prescription;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Pharmacy;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Read.QueryHandlers.Prescription;

/// <summary>Shared reads of the controlled-drug register: names (cats, customers followed to their merge survivor, staff) and the source labels. Everything is a
/// projection -- no aggregate is rehydrated (ADR-012) -- and the register itself is never written here.</summary>
internal static class ControlledDrugReads
{
    public static async Task<IReadOnlyDictionary<Guid, string>> CatNamesAsync(CatEcommerceDbContext db, IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var distinct = ids.Where(id => id is not null).Select(id => id!.Value).Distinct().ToList();
        if (distinct.Count == 0) return new Dictionary<Guid, string>();

        var rows = await db.Cats.AsNoTracking().Where(c => distinct.Contains(c.Id)).Select(c => new { c.Id, c.Name }).ToListAsync(ct);
        return rows.ToDictionary(r => r.Id, r => r.Name);
    }

    /// <summary>A customer's display name; a MERGED customer is followed to the survivor (ADR-152) -- the register entry itself keeps the id it was written with.</summary>
    public static async Task<IReadOnlyDictionary<Guid, string>> CustomerNamesAsync(CatEcommerceDbContext db, IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var distinct = ids.Where(id => id is not null).Select(id => id!.Value).Distinct().ToList();
        if (distinct.Count == 0) return new Dictionary<Guid, string>();

        var rows = await db.Set<CustomerAccountAggregate>().AsNoTracking().Where(c => distinct.Contains(c.Id))
            .Select(c => new { c.Id, c.FullName, c.MergedIntoCustomerId }).ToListAsync(ct);

        var survivorIds = rows.Where(r => r.MergedIntoCustomerId != null).Select(r => r.MergedIntoCustomerId!.Value).Distinct().ToList();
        var survivors = survivorIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await db.Set<CustomerAccountAggregate>().AsNoTracking().Where(c => survivorIds.Contains(c.Id)).Select(c => new { c.Id, c.FullName }).ToListAsync(ct))
                .ToDictionary(r => r.Id, r => r.FullName);

        return rows.ToDictionary(r => r.Id, r => r.MergedIntoCustomerId is { } survivor && survivors.TryGetValue(survivor, out var name) ? name : r.FullName);
    }

    /// <summary>"Dispensing RX-2026-000123 #1" for a dispensing, the type and a short id otherwise.</summary>
    public static async Task<IReadOnlyDictionary<(ControlledDrugSourceType Type, Guid Id), string>> SourceLabelsAsync(
        CatEcommerceDbContext db, IEnumerable<(ControlledDrugSourceType Type, Guid Id)> sources, CancellationToken ct)
    {
        var distinct = sources.Distinct().ToList();
        var dispensingIds = distinct.Where(s => s.Type == ControlledDrugSourceType.Dispensing || s.Type == ControlledDrugSourceType.DispensingReversal).Select(s => s.Id).Distinct().ToList();
        var dispensings = dispensingIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await db.Dispensings.AsNoTracking().Where(d => dispensingIds.Contains(d.Id)).Select(d => new { d.Id, d.PrescriptionNumber, d.SupplyNumber }).ToListAsync(ct))
                .ToDictionary(d => d.Id, d => $"{d.PrescriptionNumber} #{d.SupplyNumber}");

        return distinct.ToDictionary(
            s => s,
            s => (s.Type == ControlledDrugSourceType.Dispensing || s.Type == ControlledDrugSourceType.DispensingReversal) && dispensings.TryGetValue(s.Id, out var label)
                ? $"{s.Type.DisplayName} {label}"
                : $"{s.Type.DisplayName} {s.Id.ToString("N")[..8]}");
    }

    public static IQueryable<ControlledDrugRegisterEntryAggregate> Filtered(
        CatEcommerceDbContext db, Guid branchId, Guid? medicineId, Guid? batchId, string? kind, DateOnly? from, DateOnly? to)
    {
        var entries = db.ControlledDrugRegisterEntries.AsNoTracking().Where(e => e.BranchId == branchId);
        if (medicineId is { } medicine) entries = entries.Where(e => e.MedicineId == medicine);
        if (batchId is { } batch) entries = entries.Where(e => e.BatchId == batch);
        if (!string.IsNullOrWhiteSpace(kind) && EnumerationParsing.TryParse<ControlledDrugEntryKind>(kind, out var parsed) && parsed is not null)
            entries = entries.Where(e => e.Kind == parsed);
        if (from is { } start)
        {
            var startInstant = new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            entries = entries.Where(e => e.RecordedOn >= startInstant);
        }

        if (to is { } end)
        {
            var endInstant = new DateTimeOffset(end.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            entries = entries.Where(e => e.RecordedOn < endInstant);
        }

        return entries;
    }
}
