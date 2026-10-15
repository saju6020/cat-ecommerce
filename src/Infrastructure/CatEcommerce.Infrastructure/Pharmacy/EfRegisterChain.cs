using CatEcommerce.Domain.Aggregates.Prescription;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CatEcommerce.Infrastructure.Pharmacy;

/// <summary>The column values of a register entry as projected for hashing -- one query, no aggregate (ADR-012).</summary>
public sealed class RegisterEntryRow
{
    public Guid Id { get; set; }
    public long EntryNumber { get; set; }
    public Guid BranchId { get; set; }
    public Guid MedicineId { get; set; }
    public string MedicineName { get; set; } = default!;
    public string MedicineStrengthText { get; set; } = default!;
    public CatEcommerce.Shared.Common.Enums.MedicineForm MedicineForm { get; set; } = default!;
    public string? MedicineControlledSchedule { get; set; }
    public CatEcommerce.Shared.Common.Enums.DispensingUnit MedicineDispensingUnit { get; set; } = default!;
    public Guid? SkuId { get; set; }
    public Guid? BatchId { get; set; }
    public string? LotNumber { get; set; }
    public DateOnly? BatchExpiresOn { get; set; }
    public CatEcommerce.Shared.Common.Enums.ControlledDrugEntryKind Kind { get; set; } = default!;
    public decimal QuantityDelta { get; set; }
    public string? DoseText { get; set; }
    public decimal? WastageQuantity { get; set; }
    public Guid? CatId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid ActorId { get; set; }
    public Guid? WitnessId { get; set; }
    public CatEcommerce.Shared.Common.Enums.ControlledDrugSourceType SourceType { get; set; } = default!;
    public Guid SourceId { get; set; }
    public Guid? CorrectsEntryId { get; set; }
    public string? CorrectionReason { get; set; }
    public Guid? ReturnedEntryId { get; set; }
    public DateTimeOffset OccurredOn { get; set; }
    public DateTimeOffset RecordedOn { get; set; }
    public string? Notes { get; set; }

    public ControlledDrugCanonicalFields ToCanonical() => new(
        Id, EntryNumber, BranchId, MedicineId, MedicineName, MedicineStrengthText, MedicineForm.DisplayName, MedicineControlledSchedule, MedicineDispensingUnit.DisplayName,
        SkuId, BatchId, LotNumber, BatchExpiresOn, Kind.DisplayName, QuantityDelta, DoseText, WastageQuantity, CatId, CustomerId, ActorId, WitnessId, SourceType.DisplayName,
        SourceId, CorrectsEntryId, CorrectionReason, ReturnedEntryId, OccurredOn, RecordedOn, Notes);
}

public static class RegisterProjections
{
    public static IQueryable<RegisterEntryRow> AsRows(this IQueryable<ControlledDrugRegisterEntryAggregate> entries) =>
        entries.AsNoTracking().Select(e => new RegisterEntryRow
        {
            Id = e.Id, EntryNumber = e.EntryNumber, BranchId = e.BranchId, MedicineId = e.MedicineId, MedicineName = e.MedicineName,
            MedicineStrengthText = e.MedicineStrengthText, MedicineForm = e.MedicineForm, MedicineControlledSchedule = e.MedicineControlledSchedule,
            MedicineDispensingUnit = e.MedicineDispensingUnit, SkuId = e.SkuId, BatchId = e.BatchId, LotNumber = e.LotNumber, BatchExpiresOn = e.BatchExpiresOn,
            Kind = e.Kind, QuantityDelta = e.QuantityDelta, DoseText = e.DoseText, WastageQuantity = e.WastageQuantity, CatId = e.CatId, CustomerId = e.CustomerId,
            ActorId = e.ActorId, WitnessId = e.WitnessId, SourceType = e.SourceType, SourceId = e.SourceId, CorrectsEntryId = e.CorrectsEntryId,
            CorrectionReason = e.CorrectionReason, ReturnedEntryId = e.ReturnedEntryId, OccurredOn = e.OccurredOn, RecordedOn = e.RecordedOn, Notes = e.Notes
        });
}

/// <summary>HO-PHM-001 §12.2 (ADR-145) -- reads the unsealed entries and appends seals. A unique-violation race is the other worker winning, never an error.</summary>
public sealed class EfRegisterChain(CatEcommerceDbContext db) : IRegisterChain
{
    public async Task<RegisterSealTip?> GetTipAsync(Guid branchId, CancellationToken ct)
    {
        var tip = await db.ControlledDrugRegisterSeals.AsNoTracking()
            .Where(s => s.BranchId == branchId)
            .OrderByDescending(s => s.ChainPosition)
            .Select(s => new { s.ChainPosition, s.EntryHash })
            .FirstOrDefaultAsync(ct);

        return tip is null ? null : new RegisterSealTip(tip.ChainPosition, tip.EntryHash);
    }

    public async Task<IReadOnlyList<ControlledDrugCanonicalFields>> GetUnsealedAsync(Guid branchId, DateTimeOffset recordedBefore, int take, CancellationToken ct)
    {
        var rows = await db.ControlledDrugRegisterEntries
            .Where(e => e.BranchId == branchId && e.RecordedOn < recordedBefore && !db.ControlledDrugRegisterSeals.Any(s => s.Id == e.Id))
            .OrderBy(e => e.EntryNumber)
            .Take(take)
            .AsRows()
            .ToListAsync(ct);

        return rows.Select(r => r.ToCanonical()).ToList();
    }

    public async Task<bool> TryAppendAsync(IReadOnlyList<ControlledDrugRegisterSealAggregate> seals, CancellationToken ct)
    {
        if (seals.Count == 0) return true;

        db.ControlledDrugRegisterSeals.AddRange(seals);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Another worker sealed these positions first. Drop ours; nothing was written (one transaction).
            foreach (var entry in db.ChangeTracker.Entries<ControlledDrugRegisterSealAggregate>().ToList()) entry.State = EntityState.Detached;
            return false;
        }
    }
}
