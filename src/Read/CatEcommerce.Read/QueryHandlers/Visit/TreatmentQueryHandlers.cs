using System.Linq.Expressions;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Visit;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Visit;

/// <summary>One treatment line as read: the columns only, projected from the owned row (the visit is never rehydrated).</summary>
internal sealed record TreatmentRow(
    Guid Id, TreatmentKind Kind, string Description, Guid? ClinicServiceId, Guid? SkuId, string? SkuName, string? StockUnit,
    Guid? BatchId, string? LotNumber, DateOnly? BatchExpiresOn, decimal Quantity, decimal DeductedQuantity, decimal ShortfallQuantity,
    string? Dose, string? Route, bool IsBillable, decimal? UnitPriceAmount, string? UnitPriceCurrency, Guid? TaxClassId, string? BatchOverrideReason,
    TreatmentLineStatus Status, DateTimeOffset RecordedOn, Guid RecordedBy, DateTimeOffset? ReversedOn, Guid? ReversedBy, string? ReversalReason)
{
    public static readonly Expression<Func<TreatmentLine, TreatmentRow>> From = t => new TreatmentRow(
        t.Id, t.Kind, t.Description, t.ClinicServiceId, t.SkuId, t.SkuName, t.StockUnit, t.BatchId, t.LotNumber, t.BatchExpiresOn,
        t.Quantity, t.DeductedQuantity, t.ShortfallQuantity, t.Dose, t.Route, t.IsBillable, t.UnitPriceAmount, t.UnitPriceCurrency, t.TaxClassId,
        t.BatchOverrideReason, t.Status, t.RecordedOn, t.RecordedBy, t.ReversedOn, t.ReversedBy, t.ReversalReason);

    public bool IsReversed => Status == TreatmentLineStatus.Reversed;

    /// <summary>The line's unit: the SKU stock unit snapshot, "each" for a procedure.</summary>
    public string Unit => StockUnit ?? "each";
}

internal static class TreatmentRows
{
    /// <summary>The visit's treatment lines, oldest first (a total order: id breaks ties).</summary>
    public static Task<List<TreatmentRow>> OfVisitAsync(CatEcommerceDbContext db, Guid visitId, CancellationToken ct = default) =>
        db.Set<VisitAggregate>().AsNoTracking().Where(v => v.Id == visitId)
            .SelectMany(v => v.Treatments).OrderBy(t => t.RecordedOn).ThenBy(t => t.Position).ThenBy(t => t.Id)
            .Select(TreatmentRow.From).ToListAsync(ct);

    public static Measurement Quantity(decimal value, string unit) => new(InventoryViewFormat.Quantity(value), unit);

    public static VisitTreatmentViewModel ToViewModel(TreatmentRow t, IReadOnlyDictionary<Guid, string> names) => new(
        t.Id, t.Kind.DisplayName, t.Description, t.ClinicServiceId, t.SkuId, t.SkuName,
        Quantity(t.Quantity, t.Unit), Quantity(t.DeductedQuantity, t.Unit), Quantity(t.ShortfallQuantity, t.Unit),
        t.BatchId is { } batchId ? new VisitTreatmentBatchViewModel(batchId, t.LotNumber ?? string.Empty, t.BatchExpiresOn) : null,
        t.BatchOverrideReason, t.Dose, t.Route, t.IsBillable,
        t.UnitPriceAmount is { } amount && t.UnitPriceCurrency is { } currency ? InventoryViewFormat.Money(amount, currency) : null,
        t.Status.DisplayName, t.RecordedOn, StaffNames.Of(names, t.RecordedBy),
        t.ReversedOn, t.ReversedBy is null ? null : StaffNames.Of(names, t.ReversedBy), t.ReversalReason);

    /// <summary>One line of the discharge summary: what was done, the dose and route, and for a stock item how much (section 8.8). No price,
    /// no batch, no staff name -- the owner reads this.</summary>
    public static string SummaryLine(TreatmentRow t)
    {
        var text = t.Description;
        if (!string.IsNullOrWhiteSpace(t.Dose)) text += $", {t.Dose}";
        if (!string.IsNullOrWhiteSpace(t.Route)) text += $", {t.Route}";
        if (t.Kind.MovesStock) text += $" ({InventoryViewFormat.Quantity(t.Quantity)} {t.Unit})";
        return text;
    }
}

/// <summary>HO-BIL-001's contract (ADR-127): the billable, non-reversed lines of a finalised visit with their price and tax-class snapshots.</summary>
public sealed class GetVisitChargeLinesQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetVisitChargeLinesQuery, VisitChargeLinesViewModel?>
{
    public async Task<QueryResponse<VisitChargeLinesViewModel?>> HandleAsync(GetVisitChargeLinesQuery query)
    {
        var visit = await db.Set<VisitAggregate>().AsNoTracking()
            .Where(v => v.Id == query.VisitId && v.Status == VisitStatus.Finalised && !v.IsMarkedToDelete)
            .Select(v => new { v.Id, v.AppointmentId, v.CatId, v.BranchId, v.FinalisedOn })
            .FirstOrDefaultAsync();
        if (visit is null || visit.FinalisedOn is not { } finalisedOn) return new QueryResponse<VisitChargeLinesViewModel?> { Result = null };

        var customerId = await db.Set<AppointmentAggregate>().AsNoTracking().Where(a => a.Id == visit.AppointmentId).Select(a => a.CustomerId).FirstOrDefaultAsync();

        // A visit always has a branch; a missing one is a data fault, never a reason to guess UTC (the Asia/Dhaka date would silently be a day off).
        var timeZoneId = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == visit.BranchId).Select(b => b.TimeZoneId).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("The visit's branch no longer exists, so its local service date cannot be computed.");
        var rows = await TreatmentRows.OfVisitAsync(db, visit.Id);

        IReadOnlyList<VisitChargeLineViewModel> lines = rows
            .Where(t => !t.IsReversed && t.IsBillable && t.UnitPriceAmount is not null && t.UnitPriceCurrency is not null && t.TaxClassId is not null)
            .Select(t => new VisitChargeLineViewModel(
                t.Id, t.Kind.DisplayName, t.Description, InventoryViewFormat.Quantity(t.Quantity), t.Unit,
                InventoryViewFormat.Money(t.UnitPriceAmount!.Value, t.UnitPriceCurrency!), t.TaxClassId!.Value, t.ClinicServiceId, t.SkuId))
            .ToList();

        return new QueryResponse<VisitChargeLinesViewModel?>
        {
            Result = new VisitChargeLinesViewModel(visit.Id, visit.AppointmentId, visit.CatId, customerId, visit.BranchId, finalisedOn, BranchCalendar.LocalDateOf(finalisedOn, timeZoneId), lines)
        };
    }
}

/// <summary>Section 12.4 -- the dispensary's items, paged. With no search text: the SKUs with stock on hand at the branch's clinic dispensary (Inventory's
/// <c>GetDispensarySkuIdsAsync</c>). With text: the active SKUs matching it (the first <see cref="MaxMatches"/>), in stock or not -- an item that has run out
/// must still be findable to be recorded as a shortfall (AC2). The available quantity is Inventory's: usable batches only, nothing reserved.</summary>
public sealed class GetDispensaryItemsQueryHandler(ISkuDirectory skus, IStockLedger ledger, IControlledDrugDirectory controlledDrugs)
    : IQueryHandlerAsync<GetDispensaryItemsQuery, PagedResult<DispensaryItemViewModel>>
{
    public const int MaxMatches = 200;
    public const int DefaultPageSize = 30;
    public const int MaxPageSize = 100;

    public async Task<QueryResponse<PagedResult<DispensaryItemViewModel>>> HandleAsync(GetDispensaryItemsQuery query)
    {
        var pageSize = query.PageSize is > 0 ? Math.Min(query.PageSize.Value, MaxPageSize) : DefaultPageSize;
        var pageNumber = Math.Max(1, query.PageNumber ?? 1);

        IReadOnlyList<SkuStockProfile> profiles;
        if (string.IsNullOrWhiteSpace(query.Search))
        {
            var stocked = await ledger.GetDispensarySkuIdsAsync(query.BranchId, CancellationToken.None);
            profiles = stocked.IsSuccess && stocked.Value!.Count > 0 ? await skus.GetManyAsync(stocked.Value!, CancellationToken.None) : [];
        }
        else
        {
            profiles = await skus.SearchAsync(query.Search.Trim(), MaxMatches, CancellationToken.None);
        }

        var ordered = profiles.Where(p => p.IsActive).OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.SkuId).ToList();
        var page = ordered.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
        if (page.Count == 0) return new QueryResponse<PagedResult<DispensaryItemViewModel>> { Result = new PagedResult<DispensaryItemViewModel>([], pageNumber, pageSize, ordered.Count) };

        var held = await ledger.GetDispensaryBatchesAsync(query.BranchId, page.Select(p => p.SkuId).ToList(), CancellationToken.None);
        var batches = held.IsSuccess ? held.Value! : [];

        var usable = new HashSet<Guid>();
        if (batches.Count > 0)
        {
            var usability = await ledger.GetBatchUsabilityAsync(query.BranchId, batches.Select(b => b.BatchId).Distinct().ToList(), CancellationToken.None);
            if (usability.IsSuccess) usable = usability.Value!.Where(u => u.RefusalCode is null).Select(u => u.BatchId).ToHashSet();
        }

        var items = new List<DispensaryItemViewModel>(page.Count);
        foreach (var profile in page)
        {
            var available = batches.Where(b => b.SkuId == profile.SkuId && usable.Contains(b.BatchId)).Sum(b => Math.Max(0m, b.Free));
            var controlled = await controlledDrugs.FindBySkuAsync(profile.SkuId, CancellationToken.None) is not null;
            items.Add(new DispensaryItemViewModel(profile.SkuId, profile.DisplayName, profile.StockUnit, InventoryViewFormat.Quantity(available), controlled));
        }

        return new QueryResponse<PagedResult<DispensaryItemViewModel>> { Result = new PagedResult<DispensaryItemViewModel>(items, pageNumber, pageSize, ordered.Count) };
    }
}

/// <summary>Section 12.4 -- the batches a vet may choose, FEFO order, the default marked. The same picker the record handler uses, so the screen
/// and the command cannot disagree about which batch is the default or usable.</summary>
public sealed class GetIssuableBatchesQueryHandler(DispensaryBatchPicker picker)
    : IQueryHandlerAsync<GetIssuableBatchesQuery, IReadOnlyList<IssuableBatchViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<IssuableBatchViewModel>>> HandleAsync(GetIssuableBatchesQuery query)
    {
        var stock = await picker.ReadAsync(query.BranchId, query.SkuId, CancellationToken.None);

        IReadOnlyList<IssuableBatchViewModel> batches = !stock.IsSuccess
            ? []
            : stock.Value!.Batches.Where(b => b.IsIssuable)
                .Select(b => new IssuableBatchViewModel(b.BatchId, b.LotNumber, b.ExpiresOn, InventoryViewFormat.Quantity(b.Available), b.IsFefoDefault))
                .ToList();

        return new QueryResponse<IReadOnlyList<IssuableBatchViewModel>> { Result = batches };
    }
}
