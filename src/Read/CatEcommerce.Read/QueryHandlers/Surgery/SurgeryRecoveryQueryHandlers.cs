using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Surgery;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Surgery;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Surgery;

/// <summary>
/// HO-SRG-001 section 12.6 (US-SRG-10) -- the recovery board. ONE projection joins the cat, the owner and the latest status change; ONE more resolves the names of the staff who made them.
/// That is two SQL commands whatever the number of entries (I-F2): the board is polled every 30 s, so no per-row lookups.
/// </summary>
public sealed class GetRecoveryBoardQueryHandler(CatEcommerceDbContext db, TimeProvider timeProvider) : IQueryHandlerAsync<GetRecoveryBoardQuery, RecoveryBoardViewModel>
{
    public async Task<QueryResponse<RecoveryBoardViewModel>> HandleAsync(GetRecoveryBoardQuery query)
    {
        var rows = await (
            from s in db.Surgeries.AsNoTracking()
            where !s.IsMarkedToDelete && s.BranchId == query.BranchId && s.Status == SurgeryStatus.Recovery
            join cat in db.Set<CatAggregate>().AsNoTracking() on s.CatId equals cat.Id into cats
            from cat in cats.DefaultIfEmpty()
            join customer in db.Set<CustomerAccountAggregate>().AsNoTracking() on s.CustomerId equals customer.Id into customers
            from customer in customers.DefaultIfEmpty()
            orderby s.ProcedureEndedOn
            select new
            {
                s.Id, s.CatId, CatName = cat != null ? cat.Name : "", CatIsHighRisk = cat != null && cat.IsHighRisk,
                CustomerName = customer != null ? customer.FullName : "", CustomerPhone = customer != null && customer.MobileNumber != null ? customer.MobileNumber.Value : null,
                Procedure = s.Procedure.ServiceName, s.ProcedureEndedOn, s.RecoveryStatus,
                ChangedOn = s.RecoveryHistory.OrderByDescending(h => h.Sequence).Select(h => (DateTimeOffset?)h.ChangedOn).FirstOrDefault(),
                ChangedBy = s.RecoveryHistory.OrderByDescending(h => h.Sequence).Select(h => (Guid?)h.ChangedBy).FirstOrDefault(),
            }).ToListAsync();

        var staffIds = rows.Where(r => r.ChangedBy is not null).Select(r => r.ChangedBy!.Value).Distinct().ToList();
        var names = staffIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Set<StaffAccountAggregate>().AsNoTracking().Where(a => staffIds.Contains(a.Id)).Select(a => new { a.Id, a.FullName }).ToDictionaryAsync(a => a.Id, a => a.FullName);

        var entries = rows.Select(r => new RecoveryBoardEntryViewModel(
            r.Id, r.CatId, r.CatName, r.CatIsHighRisk, r.CustomerName, r.CustomerPhone, r.Procedure, r.ProcedureEndedOn, r.RecoveryStatus?.DisplayName ?? RecoveryStatus.Recovering.DisplayName,
            r.ChangedOn, r.ChangedBy, r.ChangedBy is { } by && names.TryGetValue(by, out var name) ? name : null)).ToList();

        return new QueryResponse<RecoveryBoardViewModel> { Result = new RecoveryBoardViewModel(timeProvider.GetUtcNow(), entries) };
    }
}

/// <summary>HO-SRG-001 section 12.6 (US-SRG-07) -- the discharge form, filled from the protocol (or its shipped defaults) and the procedure date. The windows tell the UI how far each date may move.</summary>
public sealed class GetDischargeDraftQueryHandler(CatEcommerceDbContext db, TimeProvider timeProvider) : IQueryHandlerAsync<GetDischargeDraftQuery, DischargeDraftViewModel?>
{
    public async Task<QueryResponse<DischargeDraftViewModel?>> HandleAsync(GetDischargeDraftQuery query)
    {
        var s = await db.Surgeries.AsNoTracking()
            .Where(x => x.Id == query.SurgeryId && x.BranchId == query.BranchId && !x.IsMarkedToDelete)
            .Select(x => new { x.Id, x.Status, x.RecoveryStatus, x.CatId, x.ClinicServiceId, x.ProcedureEndedLocalDate, x.PostOpInstructions })
            .FirstOrDefaultAsync();
        if (s is null) return new QueryResponse<DischargeDraftViewModel?> { Result = null };

        var timeZoneId = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == query.BranchId).Select(b => b.TimeZoneId).FirstOrDefaultAsync() ?? "UTC";
        var deceased = await db.Set<CatAggregate>().AsNoTracking().Where(c => c.Id == s.CatId).AnyAsync(c => c.LifeStatus == CatLifeStatus.Deceased);

        var protocol = await db.SurgeryProtocols.AsNoTracking().Where(p => p.ProtocolServiceId == s.ClinicServiceId && !p.IsMarkedToDelete)
            .Select(p => new { p.PostOpInstructions, FollowUps = p.FollowUps.Select(f => new { f.Kind, f.DayOffset, f.WindowFromDay, f.WindowToDay }).ToList() })
            .FirstOrDefaultAsync();
        IReadOnlyList<FollowUpRule> rules = protocol is not null
            ? protocol.FollowUps.Select(f => new FollowUpRule(f.Kind, f.DayOffset, f.WindowFromDay, f.WindowToDay)).ToList()
            : SurgeryProtocolDefaults.FollowUps.Select(f => new FollowUpRule(Enumeration.FromDisplayName<SurgeryFollowUpKind>(f.Kind), f.DayOffset, f.WindowFromDay, f.WindowToDay)).ToList();
        var instructions = s.PostOpInstructions ?? protocol?.PostOpInstructions ?? SurgeryProtocolDefaults.PostOpInstructions;

        var procedureDate = s.ProcedureEndedLocalDate ?? BranchCalendar.LocalDateOf(timeProvider.GetUtcNow(), timeZoneId);
        var followUps = deceased ? [] : FollowUpPlanner.Plan(rules, procedureDate).Select(p => new DischargeDraftFollowUpViewModel(p.Kind.DisplayName, p.DueOn, p.WindowFrom, p.WindowTo)).ToList();

        return new QueryResponse<DischargeDraftViewModel?>
        {
            Result = new DischargeDraftViewModel(s.Id, s.Status.DisplayName, s.RecoveryStatus?.DisplayName, deceased, deceased ? "" : instructions, followUps)
        };
    }
}

/// <summary>HO-SRG-001 section 12.4 -- the portal list. Status is shown as Scheduled, InCare (PreOp, InTheatre, Recovery; OQ-12), Discharged or Cancelled. Only the caller's own surgeries.</summary>
public sealed class GetCustomerSurgeriesQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetCustomerSurgeriesQuery, PagedResult<CustomerSurgerySummaryViewModel>>
{
    public async Task<QueryResponse<PagedResult<CustomerSurgerySummaryViewModel>>> HandleAsync(GetCustomerSurgeriesQuery query)
    {
        var past = string.Equals(query.Scope, "past", StringComparison.OrdinalIgnoreCase);
        var mine = db.Surgeries.AsNoTracking().Where(s => !s.IsMarkedToDelete && s.CustomerId == query.CustomerId);
        mine = past
            ? mine.Where(s => s.Status == SurgeryStatus.Discharged || s.Status == SurgeryStatus.Cancelled)
            : mine.Where(s => s.Status != SurgeryStatus.Discharged && s.Status != SurgeryStatus.Cancelled);

        var total = await mine.CountAsync();
        var ordered = past ? mine.OrderByDescending(x => x.StartsOn).ThenBy(x => x.Id) : mine.OrderBy(x => x.StartsOn).ThenBy(x => x.Id);
        var rows = await (
            from s in ordered.Skip(query.Skip).Take(query.Take)
            join cat in db.Set<CatAggregate>().AsNoTracking() on s.CatId equals cat.Id into cats
            from cat in cats.DefaultIfEmpty()
            join branch in db.Set<BranchAggregate>().AsNoTracking() on s.BranchId equals branch.Id into branches
            from branch in branches.DefaultIfEmpty()
            select new { s.Id, CatName = cat != null ? cat.Name : "", Procedure = s.Procedure.ServiceName, s.StartsOn, s.AdmitBy, s.Status, Branch = branch != null ? branch.Name : "", Zone = branch != null ? branch.TimeZoneId : "UTC" })
            .ToListAsync();

        IReadOnlyList<CustomerSurgerySummaryViewModel> items = rows
            .Select(r => new CustomerSurgerySummaryViewModel(r.Id, r.CatName, r.Procedure, r.StartsOn, r.AdmitBy, CustomerStatus(r.Status), r.Branch, r.Zone)).ToList();
        return new QueryResponse<PagedResult<CustomerSurgerySummaryViewModel>> { Result = new PagedResult<CustomerSurgerySummaryViewModel>(items, query.PageNumber, query.PageSize, total) };
    }

    public static string CustomerStatus(SurgeryStatus status) =>
        status == SurgeryStatus.Scheduled ? "Scheduled"
        : status == SurgeryStatus.Discharged ? "Discharged"
        : status == SurgeryStatus.Cancelled ? "Cancelled"
        : "InCare";
}

public sealed class GetCustomerSurgeryDetailQueryHandler(CatEcommerceDbContext db, INotificationLinkBuilder links)
    : IQueryHandlerAsync<GetCustomerSurgeryDetailQuery, CustomerSurgeryDetailViewModel?>
{
    public async Task<QueryResponse<CustomerSurgeryDetailViewModel?>> HandleAsync(GetCustomerSurgeryDetailQuery query)
    {
        // The customer id is part of the predicate: another customer's surgery is exactly as "not found" as a missing one.
        var s = await db.Surgeries.AsNoTracking()
            .Where(x => x.Id == query.SurgeryId && x.CustomerId == query.CustomerId && !x.IsMarkedToDelete)
            .Select(x => new
            {
                x.Id, x.CatId, x.BranchId, x.StartsOn, x.AdmitBy, x.Status, x.FastingInstructions, x.PostOpInstructions, Procedure = x.Procedure.ServiceName,
                FollowUps = x.FollowUps.OrderBy(f => f.DueOn).Select(f => new { f.Kind, f.DueOn, f.Status }).ToList(),
            })
            .FirstOrDefaultAsync();
        if (s is null) return new QueryResponse<CustomerSurgeryDetailViewModel?> { Result = null };

        var catName = await db.Set<CatAggregate>().AsNoTracking().Where(c => c.Id == s.CatId).Select(c => c.Name).FirstOrDefaultAsync() ?? "";
        var branch = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == s.BranchId).Select(b => new { b.Name, b.TimeZoneId }).FirstOrDefaultAsync();

        // The instructions are the owner's only once the cat has been discharged (the staff may still be editing them before).
        var postOp = s.Status == SurgeryStatus.Discharged ? s.PostOpInstructions : null;
        var detail = new CustomerSurgeryDetailViewModel(
            s.Id, s.CatId, catName, s.Procedure, s.StartsOn, s.AdmitBy, GetCustomerSurgeriesQueryHandler.CustomerStatus(s.Status), branch?.Name ?? "", branch?.TimeZoneId ?? "UTC",
            s.FastingInstructions, postOp, s.FollowUps.Select(f => new CustomerSurgeryFollowUpViewModel(f.Kind.DisplayName, f.DueOn, f.Status.DisplayName)).ToList(), links.FollowUpBookingLink(s.CatId));
        return new QueryResponse<CustomerSurgeryDetailViewModel?> { Result = detail };
    }
}

/// <summary>HO-EMR-001 history contract (US-CLN-13) -- every non-cancelled surgery of the cat, newest first.</summary>
public sealed class GetCatSurgeriesQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetCatSurgeriesQuery, PagedResult<CatSurgeryHistoryViewModel>>
{
    public async Task<QueryResponse<PagedResult<CatSurgeryHistoryViewModel>>> HandleAsync(GetCatSurgeriesQuery query)
    {
        var total = await db.Surgeries.AsNoTracking().CountAsync(s => !s.IsMarkedToDelete && s.CatId == query.CatId && s.Status != SurgeryStatus.Cancelled);
        var rows = await (
            from s in db.Surgeries.AsNoTracking().Where(s => !s.IsMarkedToDelete && s.CatId == query.CatId && s.Status != SurgeryStatus.Cancelled)
                .OrderByDescending(s => s.StartsOn).ThenBy(s => s.Id).Skip(query.Skip).Take(query.Take)
            join r in db.SurgicalReports.AsNoTracking() on s.Id equals r.Id into reports
            from r in reports.DefaultIfEmpty()
            orderby s.StartsOn descending, s.Id
            select new
            {
                s.Id, s.VisitId, Procedure = s.Procedure.ServiceName, s.Procedure.IsSterilisation, s.StartsOn, s.Status, s.Outcome, s.DischargedOn,
                ReportExists = r != null, ReportFinalisedOn = r != null ? r.FinalisedOn : null
            }).ToListAsync();

        IReadOnlyList<CatSurgeryHistoryViewModel> items = rows.Select(x => new CatSurgeryHistoryViewModel(
            x.Id, x.VisitId, x.Procedure, x.IsSterilisation, x.StartsOn, x.Status.DisplayName, x.Outcome?.DisplayName, x.DischargedOn,
            !x.ReportExists ? "None" : x.ReportFinalisedOn is null ? "Draft" : "Finalised")).ToList();
        return new QueryResponse<PagedResult<CatSurgeryHistoryViewModel>> { Result = new PagedResult<CatSurgeryHistoryViewModel>(items, query.PageNumber, query.PageSize, total) };
    }
}

/// <summary>HO-SRG-001 section 12.6 -- the billable contents of a surgery for HO-BIL-001: the price snapshot and the drug entries as they stand AFTER corrections (the leaf of each
/// correction chain), each with its SKU and the quantity drawn. A drug entry without a SKU (a shortfall, or a corrected-away drug) is not billable.</summary>
public sealed class GetSurgeryChargeableItemsQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetSurgeryChargeableItemsQuery, SurgeryChargeableItemsViewModel?>
{
    public async Task<QueryResponse<SurgeryChargeableItemsViewModel?>> HandleAsync(GetSurgeryChargeableItemsQuery query)
    {
        var s = await db.Surgeries.AsNoTracking().Where(x => x.Id == query.SurgeryId && !x.IsMarkedToDelete)
            .Select(x => new
            {
                x.Id, x.CatId, x.CustomerId, x.BranchId, x.ClinicServiceId, ProcedureName = x.Procedure.ServiceName,
                Amount = x.PriceSnapshot.Amount.Amount, Currency = x.PriceSnapshot.Amount.CurrencyCode, Basis = x.PriceSnapshot.Basis, x.PriceSnapshot.CampaignId
            }).FirstOrDefaultAsync();
        if (s is null) return new QueryResponse<SurgeryChargeableItemsViewModel?> { Result = null };

        var rows = await AnaesthesiaLogRows.ProjectAsync(db.AnaesthesiaLogEntries.AsNoTracking()
            .Where(e => e.SurgeryId == s.Id && !e.IsMarkedToDelete && (e.Kind == AnaesthesiaEntryKind.Drug || (e.Kind == AnaesthesiaEntryKind.Correction && e.CorrectedKind == AnaesthesiaEntryKind.Drug))));

        var corrected = rows.Where(r => r.CorrectsEntryId is not null).GroupBy(r => r.CorrectsEntryId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.LoggedOn).ThenByDescending(r => r.Id).First());

        var drugs = new List<SurgeryChargeableDrugViewModel>();
        foreach (var original in rows.Where(r => r.Kind == AnaesthesiaEntryKind.Drug).OrderBy(r => r.OccurredOn).ThenBy(r => r.LoggedOn))
        {
            var leaf = original;
            while (corrected.TryGetValue(leaf.Id, out var next)) leaf = next;
            if (leaf.SkuId is not { } sku || leaf.QuantityDrawn is not { } quantity || quantity <= 0m) continue;

            drugs.Add(new SurgeryChargeableDrugViewModel(original.Id, leaf.MedicineId, leaf.MedicineName, sku, quantity, leaf.StockUnit));
        }

        return new QueryResponse<SurgeryChargeableItemsViewModel?>
        {
            Result = new SurgeryChargeableItemsViewModel(
                s.Id, s.CatId, s.CustomerId, s.BranchId, s.ClinicServiceId, s.ProcedureName, SurgeryReadFormat.Money(s.Amount, s.Currency), s.Basis.DisplayName, s.CampaignId, drugs)
        };
    }
}
