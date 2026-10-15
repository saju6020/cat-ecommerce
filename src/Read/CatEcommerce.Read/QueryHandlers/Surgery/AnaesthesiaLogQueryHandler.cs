using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Surgery;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Surgery;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Surgery;

/// <summary>The columns of an anaesthesia log entry the reads need (ADR-012: projected, the aggregate is never rehydrated on the read side).</summary>
internal sealed record AnaesthesiaLogRow(
    Guid Id, AnaesthesiaEntryKind Kind, DateTimeOffset OccurredOn, DateTimeOffset LoggedOn, Guid LoggedBy, Guid? CorrectsEntryId, AnaesthesiaEntryKind? CorrectedKind, string? CorrectionReason,
    Guid? MedicineId, string? MedicineName, bool IsControlled, decimal? DoseAmount, DoseUnit? DoseUnit, AdministrationRoute? Route, Guid? AdministeredById, string? BatchNumber,
    decimal? QuantityDrawn, decimal? QuantityWasted, string? StockUnit, Guid? WitnessId, Guid? RegisterEntryId, bool StockShortfall, Guid? SkuId,
    int? HeartRateBpm, int? RespiratoryRatePerMin, int? SpO2Percent, int? EtCo2MmHg, decimal? TemperatureCelsius, int? SystolicMmHg, int? MeanArterialMmHg, bool ImplausibleConfirmed,
    AnaesthesiaMilestone? Milestone, string? Note);

internal static class AnaesthesiaLogRows
{
    public static Task<List<AnaesthesiaLogRow>> ProjectAsync(IQueryable<AnaesthesiaLogEntryAggregate> entries) =>
        entries.Select(r => new AnaesthesiaLogRow(
            r.Id, r.Kind, r.OccurredOn, r.LoggedOn, r.LoggedBy, r.CorrectsEntryId, r.CorrectedKind, r.CorrectionReason,
            r.MedicineId, r.MedicineName, r.IsControlled, r.DoseAmount, r.DoseUnit, r.Route, r.AdministeredById, r.BatchNumber,
            r.QuantityDrawn, r.QuantityWasted, r.StockUnit, r.WitnessId, r.RegisterEntryId, r.StockShortfall, r.SkuId,
            r.HeartRateBpm, r.RespiratoryRatePerMin, r.SpO2Percent, r.EtCo2MmHg, r.TemperatureCelsius, r.SystolicMmHg, r.MeanArterialMmHg, r.ImplausibleConfirmed,
            r.Milestone, r.Note)).ToListAsync();
}

/// <summary>
/// HO-SRG-001 §8.8 / §12.6 (US-SRG-04) -- the anaesthesia timeline: one read of the surgery entries (immutable rows, so nothing here can race a write), the names of the people in one more, and
/// the cat's latest weight. Corrections are resolved in memory: an entry EFFECTIVE values are those of the leaf of its correction chain. Dose in mg/kg is shown for information only (OQ-11).
/// </summary>
public sealed class GetAnaesthesiaLogQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetAnaesthesiaLogQuery, AnaesthesiaLogViewModel?>
{
    public async Task<QueryResponse<AnaesthesiaLogViewModel?>> HandleAsync(GetAnaesthesiaLogQuery query)
    {
        var surgery = await db.Surgeries.AsNoTracking()
            .Where(s => s.Id == query.SurgeryId && s.BranchId == query.BranchId && !s.IsMarkedToDelete)
            .Select(s => new { s.Id, s.CatId, s.BranchId, s.Status })
            .FirstOrDefaultAsync();
        if (surgery is null) return new QueryResponse<AnaesthesiaLogViewModel?> { Result = null };

        var timeZoneId = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == surgery.BranchId).Select(b => b.TimeZoneId).FirstOrDefaultAsync() ?? "UTC";
        var rows = await AnaesthesiaLogRows.ProjectAsync(db.AnaesthesiaLogEntries.AsNoTracking().Where(e => e.SurgeryId == surgery.Id && !e.IsMarkedToDelete));

        var staffIds = rows.SelectMany(r => new Guid?[] { r.LoggedBy, r.AdministeredById, r.WitnessId }).Where(id => id is not null).Select(id => id!.Value).Distinct().ToList();
        var names = await db.Set<StaffAccountAggregate>().AsNoTracking().Where(a => staffIds.Contains(a.Id)).Select(a => new { a.Id, a.FullName }).ToDictionaryAsync(a => a.Id, a => a.FullName);
        string? Name(Guid? id) => id is { } x && names.TryGetValue(x, out var n) ? n : null;

        var weight = await db.CatWeightRecords.AsNoTracking().Where(w => w.CatId == surgery.CatId && !w.IsMarkedToDelete)
            .OrderByDescending(w => w.RecordedOn).ThenByDescending(w => w.CreatedDate).Select(w => new { w.Weight.Kilograms, w.RecordedOn }).FirstOrDefaultAsync();

        var byCorrected = rows.Where(r => r.CorrectsEntryId is not null).GroupBy(r => r.CorrectsEntryId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.LoggedOn).ThenByDescending(r => r.Id).First());

        AnaesthesiaValuesViewModel ValuesOf(AnaesthesiaLogRow r) => new(
            r.MedicineId, r.MedicineName, r.IsControlled, r.DoseAmount, r.DoseUnit?.DisplayName, r.Route?.DisplayName, r.AdministeredById, Name(r.AdministeredById),
            r.BatchNumber, r.QuantityDrawn, r.QuantityWasted, r.StockUnit, r.WitnessId, Name(r.WitnessId), r.RegisterEntryId, r.StockShortfall,
            r.HeartRateBpm, r.RespiratoryRatePerMin, r.SpO2Percent, r.EtCo2MmHg, r.TemperatureCelsius, r.SystolicMmHg, r.MeanArterialMmHg, r.ImplausibleConfirmed,
            r.Milestone?.DisplayName, r.Note);

        AnaesthesiaLogRow Leaf(AnaesthesiaLogRow r)
        {
            var current = r;
            while (byCorrected.TryGetValue(current.Id, out var next)) current = next;
            return current;
        }

        decimal? MgPerKg(AnaesthesiaLogRow effective)
        {
            if (weight is null || weight.Kilograms <= 0m || effective.DoseAmount is not { } dose) return null;
            decimal? mg = effective.DoseUnit == DoseUnit.Mg ? dose : effective.DoseUnit == DoseUnit.Mcg ? dose / 1000m : effective.DoseUnit == DoseUnit.G ? dose * 1000m : null;
            return mg is null ? null : decimal.Round(mg.Value / weight.Kilograms, 3);
        }

        var entries = rows.OrderBy(r => r.OccurredOn).ThenBy(r => r.LoggedOn).ThenBy(r => r.Id).Select(r =>
        {
            var leaf = Leaf(r);
            byCorrected.TryGetValue(r.Id, out var directCorrector);
            return new AnaesthesiaEntryViewModel(
                r.Id, r.Kind.DisplayName, r.OccurredOn, r.LoggedOn, r.LoggedBy, Name(r.LoggedBy) ?? "", ValuesOf(r), ValuesOf(leaf),
                directCorrector is not null, directCorrector?.Id, r.CorrectsEntryId, r.CorrectedKind?.DisplayName, r.CorrectionReason, MgPerKg(leaf));
        }).ToList();

        var isOpen = surgery.Status == SurgeryStatus.PreOp || surgery.Status == SurgeryStatus.InTheatre || surgery.Status == SurgeryStatus.Recovery;
        return new QueryResponse<AnaesthesiaLogViewModel?>
        {
            Result = new AnaesthesiaLogViewModel(surgery.Id, surgery.CatId, timeZoneId, isOpen, weight is null ? null : new SurgeryLatestWeightViewModel(weight.Kilograms, weight.RecordedOn), entries)
        };
    }
}
