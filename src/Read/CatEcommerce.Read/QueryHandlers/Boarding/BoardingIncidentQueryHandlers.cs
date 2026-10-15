using CatEcommerce.Domain.Aggregates.Boarding;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Boarding;
using CatEcommerce.Read.QueryHandlers.StoredFile;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Boarding;

/// <summary>The columns of an incident a read needs (projected -- a query handler never materialises the aggregate).</summary>
internal sealed record IncidentRow(
    Guid Id, Guid StayId, Guid CatId, IncidentKind Kind, IncidentSeverity Severity, string Description, string ImmediateAction, DateTimeOffset OccurredOn, DateTimeOffset ReportedOn,
    bool OwnerNotified, DateTimeOffset? OwnerAcknowledgedOn, AcknowledgementChannel? AcknowledgedVia, DateTimeOffset? EscalatedOn, DateTimeOffset? PhoneCallOn, string? PhoneCallOutcome,
    IncidentStatus Status, DateTimeOffset? ResolvedOn, string? Resolution);

internal static class IncidentMapping
{
    public static IQueryable<IncidentRow> Project(this IQueryable<BoardingIncidentAggregate> incidents) => incidents.Select(i => new IncidentRow(
        i.Id, i.StayId, i.CatId, i.Kind, i.Severity, i.Description, i.ImmediateAction, i.OccurredOn, i.ReportedOn, i.OwnerNotified, i.OwnerAcknowledgedOn, i.AcknowledgedVia, i.EscalatedOn,
        i.PhoneCallOn, i.PhoneCallOutcome, i.Status, i.ResolvedOn, i.Resolution));

    public static async Task<IReadOnlyList<IncidentViewModel>> MapAsync(CatEcommerceDbContext db, IStoredFileSigningBatcher signer, IReadOnlyList<IncidentRow> incidents)
    {
        var stayIds = incidents.Select(i => i.StayId).Distinct().ToList();
        var incidentIds = incidents.Select(i => i.Id).ToList();
        var catNames = (await db.BoardingStays.AsNoTracking().Where(s => stayIds.Contains(s.Id)).SelectMany(s => s.Cats).Select(c => new { c.CatId, c.CatName }).ToListAsync())
            .GroupBy(c => c.CatId).ToDictionary(g => g.Key, g => g.First().CatName);
        var media = await db.BoardingStayMedia.AsNoTracking()
            .Where(m => m.IncidentId != null && incidentIds.Contains(m.IncidentId!.Value) && m.Status == StayMediaStatus.Visible)
            .Select(m => new { m.Id, m.IncidentId, m.CatId, m.Caption, m.Status, m.FileId }).ToListAsync();
        var files = await signer.SignManyAsync(media.Select(m => m.FileId).ToList(), "inline");

        return incidents.Select(i => new IncidentViewModel(
            i.Id, i.StayId, i.CatId, catNames.GetValueOrDefault(i.CatId, ""), i.Kind.DisplayName, i.Severity.DisplayName, i.Description, i.ImmediateAction, i.OccurredOn, i.ReportedOn,
            i.OwnerNotified, i.OwnerAcknowledgedOn, i.AcknowledgedVia?.DisplayName, i.EscalatedOn, i.PhoneCallOn, i.PhoneCallOutcome, i.Status.DisplayName, i.ResolvedOn, i.Resolution,
            media.Where(m => m.IncidentId == i.Id).Select(m => new DiaryMediaViewModel(m.Id, m.CatId, m.Caption, m.Status.DisplayName, files.GetValueOrDefault(m.FileId))).ToList())).ToList();
    }
}

public sealed class GetIncidentsQueryHandler(CatEcommerceDbContext db, IStoredFileSigningBatcher signer) : IQueryHandlerAsync<GetIncidentsQuery, IReadOnlyList<IncidentViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<IncidentViewModel>>> HandleAsync(GetIncidentsQuery query)
    {
        var incidents = db.BoardingIncidents.AsNoTracking().Where(i => i.BranchId == query.BranchId);
        if (!string.IsNullOrWhiteSpace(query.Status) && EnumerationParsing.TryParse<IncidentStatus>(query.Status, out var status) && status is not null) incidents = incidents.Where(i => i.Status == status);
        if (query.StayId is { } stayId) incidents = incidents.Where(i => i.StayId == stayId);

        var rows = await incidents.OrderByDescending(i => i.ReportedOn).Take(200).Project().ToListAsync();
        return new QueryResponse<IReadOnlyList<IncidentViewModel>> { Result = await IncidentMapping.MapAsync(db, signer, rows) };
    }
}

public sealed class GetCustomerIncidentQueryHandler(CatEcommerceDbContext db, IStoredFileSigningBatcher signer) : IQueryHandlerAsync<GetCustomerIncidentQuery, IncidentViewModel?>
{
    public async Task<QueryResponse<IncidentViewModel?>> HandleAsync(GetCustomerIncidentQuery query)
    {
        var own = await db.BoardingStays.AsNoTracking().AnyAsync(s => s.Id == query.StayId && s.CustomerId == query.CustomerId);
        var incident = own ? await db.BoardingIncidents.AsNoTracking().Where(i => i.Id == query.IncidentId && i.StayId == query.StayId).Project().FirstOrDefaultAsync() : null;
        if (incident is null || incident.Severity == IncidentSeverity.Low) return new QueryResponse<IncidentViewModel?> { Result = null }; // low incidents are not shared

        return new QueryResponse<IncidentViewModel?> { Result = (await IncidentMapping.MapAsync(db, signer, [incident]))[0] };
    }
}

/// <summary>FR-POR-4 / US-BRD-12: the owner's diary of their own stay, per day. Hidden and purged media are not shown, and neither is media attached to a LOW (internal) incident; the diary stays readable after check-out until the retention purge.</summary>
public sealed class GetStayDiaryQueryHandler(CatEcommerceDbContext db, IStoredFileSigningBatcher signer)
    : IQueryHandlerAsync<GetStayDiaryQuery, StayDiaryViewModel?>
{
    public async Task<QueryResponse<StayDiaryViewModel?>> HandleAsync(GetStayDiaryQuery query)
    {
        var stay = await db.BoardingStays.AsNoTracking().Where(s => s.Id == query.StayId && s.CustomerId == query.CustomerId)
            .Select(s => new { s.Id, s.ArrivalOn, s.DepartureOn, Cats = s.Cats.Select(c => new { c.CatId, c.CatName }).ToList() }).FirstOrDefaultAsync();
        if (stay is null) return new QueryResponse<StayDiaryViewModel?> { Result = null };

        var from = query.From ?? stay.ArrivalOn;
        var to = query.To ?? stay.DepartureOn;
        if (to < from || to.DayNumber - from.DayNumber > 120) to = from.AddDays(120);

        var names = stay.Cats.ToDictionary(c => c.CatId, c => c.CatName);
        var observations = await db.BoardingCareObservations.AsNoTracking().Where(o => o.StayId == stay.Id && o.LocalDate >= from && o.LocalDate <= to).OrderBy(o => o.RecordedOn)
            .Select(o => new { o.CatId, o.LocalDate, o.RecordedOn, o.PortionEaten, o.WaterIntake, o.LitterOutput, o.Mood, o.Notes }).ToListAsync();
        var media = await db.BoardingStayMedia.AsNoTracking()
            .Where(m => m.StayId == stay.Id && m.Status == StayMediaStatus.Visible && m.CapturedOnLocalDate >= from && m.CapturedOnLocalDate <= to
                        && !db.BoardingIncidents.Any(i => i.Id == m.IncidentId && i.Severity == IncidentSeverity.Low))
            .Select(m => new { m.Id, m.CatId, m.Caption, m.Status, m.FileId, m.CapturedOnLocalDate }).ToListAsync();
        var doneByDay = (await db.BoardingCareTasks.AsNoTracking().Where(t => t.StayId == stay.Id && t.Status == CareTaskStatus.Done && t.LocalDate >= from && t.LocalDate <= to)
            .GroupBy(t => t.LocalDate).Select(g => new { Date = g.Key, Count = g.Count() }).ToListAsync()).ToDictionary(g => g.Date, g => g.Count);
        var files = await signer.SignManyAsync(media.Select(m => m.FileId).ToList(), "inline");

        var days = new List<DiaryDayViewModel>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var dayObservations = observations.Where(o => o.LocalDate == date)
                .Select(o => new DiaryObservationViewModel(o.CatId, names.GetValueOrDefault(o.CatId, ""), o.RecordedOn, o.PortionEaten.DisplayName, o.WaterIntake.DisplayName, o.LitterOutput.DisplayName, o.Mood.DisplayName, o.Notes)).ToList();
            var dayMedia = media.Where(m => m.CapturedOnLocalDate == date).Select(m => new DiaryMediaViewModel(m.Id, m.CatId, m.Caption, m.Status.DisplayName, files.GetValueOrDefault(m.FileId))).ToList();
            if (dayObservations.Count == 0 && dayMedia.Count == 0 && !doneByDay.ContainsKey(date)) continue;

            days.Add(new DiaryDayViewModel(date, dayObservations, dayMedia, doneByDay.GetValueOrDefault(date)));
        }

        return new QueryResponse<StayDiaryViewModel?> { Result = new StayDiaryViewModel(stay.Id, from, to, days) };
    }
}

public sealed class GetStayMediaQueryHandler(CatEcommerceDbContext db, IStoredFileSigningBatcher signer) : IQueryHandlerAsync<GetStayMediaQuery, IReadOnlyList<DiaryMediaViewModel>?>
{
    public async Task<QueryResponse<IReadOnlyList<DiaryMediaViewModel>?>> HandleAsync(GetStayMediaQuery query)
    {
        if (!await db.BoardingStays.AsNoTracking().AnyAsync(s => s.Id == query.StayId && s.BranchId == query.BranchId))
            return new QueryResponse<IReadOnlyList<DiaryMediaViewModel>?> { Result = null };

        var media = await db.BoardingStayMedia.AsNoTracking().Where(m => m.StayId == query.StayId).OrderBy(m => m.CapturedOnLocalDate).ThenBy(m => m.UploadedOn)
            .Select(m => new { m.Id, m.CatId, m.Caption, m.Status, m.FileId }).ToListAsync();
        var files = await signer.SignManyAsync(media.Select(m => m.FileId).ToList(), "inline");
        IReadOnlyList<DiaryMediaViewModel> result = media.Select(m => new DiaryMediaViewModel(m.Id, m.CatId, m.Caption, m.Status.DisplayName, files.GetValueOrDefault(m.FileId))).ToList();
        return new QueryResponse<IReadOnlyList<DiaryMediaViewModel>?> { Result = result };
    }
}

public sealed class GetIncidentsToEscalateQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetIncidentsToEscalateQuery, IReadOnlyList<Guid>>
{
    public async Task<QueryResponse<IReadOnlyList<Guid>>> HandleAsync(GetIncidentsToEscalateQuery query)
    {
        // The branch's own escalation minutes (5..240) decide in the handler; this only skips what cannot be due yet. Oldest first, so a backlog cannot starve the longest-waiting one.
        var cutoff = query.Now.AddMinutes(-5);
        IReadOnlyList<Guid> ids = await db.BoardingIncidents.AsNoTracking()
            .Where(i => i.Status == IncidentStatus.Open && i.Severity == IncidentSeverity.High && i.OwnerNotified && i.OwnerAcknowledgedOn == null && i.EscalatedOn == null && i.ReportedOn <= cutoff)
            .OrderBy(i => i.ReportedOn).Select(i => i.Id).Take(200).ToListAsync();

        return new QueryResponse<IReadOnlyList<Guid>> { Result = ids };
    }
}

internal sealed record CatNameRow(Guid CatId, string CatName);

internal sealed record DashboardStayRow(
    Guid Id, BoardingStayStatus Status, DateOnly ArrivalOn, DateOnly DepartureOn, bool NeedsAttention, DepositState DepositState, decimal DepositRequired, DateTimeOffset? DepositDueBy, bool DepositForfeited,
    string CurrencyCode, DateTimeOffset? CheckedOutOn, DateTimeOffset? CancelledOn, decimal CreditDue, List<CatNameRow> Cats, List<AllocationRow> Allocations,
    List<RateUnitRow> RateUnits)
{
    public List<string> CatNames => Cats.Select(c => c.CatName).ToList();

    public Guid? UnitId => StayAllocationRule.Current(Allocations, a => a.IsActive, a => a.OccupiedFrom)?.UnitId;
}

/// <summary>US-BRD-19 -- one handler, every section of the staff dashboard for the active branch and today (branch-local). Reads only; side-effect free; cheap enough to poll.</summary>
public sealed class GetBoardingDashboardQueryHandler(
    CatEcommerceDbContext db, IBoardingInputs boardingInputs, IBoardingDepositLedger depositLedger, IBoardingChargeLedger chargeLedger,
    IQueryHandlerAsync<CatEcommerce.Read.Queries.Billing.GetBillingGroupSummaryQuery, BillingGroupSummaryViewModel> billingSummary, TimeProvider timeProvider)
    : IQueryHandlerAsync<GetBoardingDashboardQuery, BoardingDashboardViewModel>
{
    public async Task<QueryResponse<BoardingDashboardViewModel>> HandleAsync(GetBoardingDashboardQuery query)
    {
        var settings = await boardingInputs.GetSettingsAsync(query.BranchId);
        var zone = settings?.TimeZoneId ?? "UTC";
        var grace = settings?.TaskOverdueGraceMinutes ?? 30;
        var currency = settings?.CurrencyCode ?? "BDT";
        var now = timeProvider.GetUtcNow();
        var today = BoardingOccupancyWindow.LocalDateOf(now, zone);

        var units = await db.BoardingUnits.AsNoTracking().Where(u => u.BranchId == query.BranchId).Select(u => new { u.Id, u.Code, u.IsActive, u.Type }).ToListAsync();
        var unitCodes = units.ToDictionary(u => u.Id, u => u.Code);

        var live = await Rows(db.BoardingStays.AsNoTracking().Where(s => s.BranchId == query.BranchId
            && (s.Status == BoardingStayStatus.CheckedIn || s.Status == BoardingStayStatus.Confirmed || s.Status == BoardingStayStatus.Reserved || s.NeedsAttention))).ToListAsync();
        var inHouse = live.Where(s => s.Status == BoardingStayStatus.CheckedIn).ToList();

        string? CodeOf(DashboardStayRow s) => s.UnitId is { } id ? unitCodes.GetValueOrDefault(id) : null;
        DashboardStayViewModel Row(DashboardStayRow s) => new(s.Id, s.CatNames, CodeOf(s), s.Status.DisplayName, s.DepositState.DisplayName);

        var occupiedUnits = inHouse.Select(s => s.UnitId).Where(id => id is not null).Select(id => id!.Value).Distinct().ToHashSet();
        var occupancy = new DashboardOccupancyViewModel(
            occupiedUnits.Count, units.Count(u => u.IsActive), inHouse.Sum(s => s.Cats.Count),
            units.Where(u => u.IsActive).GroupBy(u => u.Type.DisplayName).OrderBy(g => g.Key)
                .Select(g => new DashboardOccupancyTypeViewModel(g.Key, g.Count(u => occupiedUnits.Contains(u.Id)), g.Count())).ToList());

        var arrivals = live.Where(s => s.Status == BoardingStayStatus.Confirmed && s.ArrivalOn == today).Select(Row).ToList();
        var departures = inHouse.Where(s => s.DepartureOn == today).Select(Row).ToList();
        var attention = live.Where(s => s.NeedsAttention).Select(Row).ToList();

        var inHouseIds = inHouse.Select(s => s.Id).ToList();
        var catNames = inHouse.SelectMany(s => s.Cats.Select(c => (StayId: s.Id, c.CatId, c.CatName))).ToList();
        var observations = await db.BoardingCareObservations.AsNoTracking().Where(o => inHouseIds.Contains(o.StayId)).OrderByDescending(o => o.RecordedOn)
            .Select(o => new { o.StayId, o.CatId, o.PortionEaten }).ToListAsync();
        var notEating = catNames
            .Where(c => NotEatingRule.IsNotEating(observations.Where(o => o.CatId == c.CatId && o.StayId == c.StayId).Select(o => o.PortionEaten).ToList()))
            .Select(c => new DashboardCatViewModel(c.StayId, c.CatId, c.CatName, CodeOf(inHouse.First(s => s.Id == c.StayId)))).ToList();

        var nameOf = catNames.GroupBy(c => c.CatId).ToDictionary(g => g.Key, g => g.First().CatName);
        var pending = await db.BoardingCareTasks.AsNoTracking().Where(t => t.BranchId == query.BranchId && t.Status == CareTaskStatus.Pending && t.LocalDate <= today).OrderBy(t => t.DueOn).Take(500)
            .Select(t => new { t.Id, t.StayId, t.CatId, t.Title, t.DueOn, t.AssignedCaretakerId, t.LocalDate, t.Status }).ToListAsync();
        DashboardTaskViewModel Task(Guid id, Guid stayId, Guid catId, string title, DateTimeOffset dueOn, Guid? assigned) => new(id, stayId, nameOf.GetValueOrDefault(catId, ""), title, dueOn, assigned);
        var overdue = pending.Where(t => CareTaskAggregate.IsOverdue(t.Status, t.DueOn, now, grace)).Select(t => Task(t.Id, t.StayId, t.CatId, t.Title, t.DueOn, t.AssignedCaretakerId)).ToList();
        var unassigned = pending.Where(t => t.AssignedCaretakerId is null && t.LocalDate == today).Select(t => Task(t.Id, t.StayId, t.CatId, t.Title, t.DueOn, t.AssignedCaretakerId)).ToList();

        var open = await db.BoardingIncidents.AsNoTracking().Where(i => i.BranchId == query.BranchId && i.Status == IncidentStatus.Open).OrderByDescending(i => i.ReportedOn).Take(100)
            .Select(i => new { i.Id, i.StayId, i.CatId, i.Severity, i.ReportedOn, i.OwnerAcknowledgedOn, i.EscalatedOn, i.PhoneCallOn }).ToListAsync();
        var incidentCatIds = open.Select(i => i.CatId).Distinct().ToList();
        var incidentNames = (await db.BoardingStays.AsNoTracking().Where(s => s.BranchId == query.BranchId).SelectMany(s => s.Cats).Where(c => incidentCatIds.Contains(c.CatId)).Select(c => new { c.CatId, c.CatName }).ToListAsync())
            .GroupBy(c => c.CatId).ToDictionary(g => g.Key, g => g.First().CatName);
        var openIncidents = open.Select(i => new DashboardIncidentViewModel(i.Id, i.StayId, incidentNames.GetValueOrDefault(i.CatId, ""), i.Severity.DisplayName, i.ReportedOn, i.OwnerAcknowledgedOn is not null, i.EscalatedOn is not null)).ToList();
        var callOwner = open.Where(i => i.EscalatedOn != null && i.PhoneCallOn == null).Select(i => new DashboardIncidentViewModel(i.Id, i.StayId, incidentNames.GetValueOrDefault(i.CatId, ""), i.Severity.DisplayName, i.ReportedOn, i.OwnerAcknowledgedOn is not null, true)).ToList();

        var unpaid = new List<DashboardBalanceViewModel>();
        foreach (var s in live.Where(s => s.Status == BoardingStayStatus.Reserved && s.DepositState == DepositState.Awaiting))
            unpaid.Add(new DashboardBalanceViewModel(s.Id, s.CatNames, s.Status.DisplayName, BoardingMoney.Of(s.DepositRequired, currency), s.DepositDueBy, "Deposit awaited"));

        var recentlyOut = await Rows(db.BoardingStays.AsNoTracking()
            .Where(s => s.BranchId == query.BranchId && s.Status == BoardingStayStatus.CheckedOut && s.Cats.Any(c => c.CheckedOutOn != null && c.CheckedOutOn >= now.AddDays(-30)))).ToListAsync();
        foreach (var s in inHouse.Concat(recentlyOut))
        {
            var summary = (await billingSummary.HandleAsync(new CatEcommerce.Read.Queries.Billing.GetBillingGroupSummaryQuery { Kind = "BoardingStay", GroupId = s.Id, UserContext = query.UserContext, CorrelationId = query.CorrelationId })).Result;
            if (decimal.TryParse(summary.InvoicedOutstanding.Amount, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var outstanding) && outstanding > 0m)
                unpaid.Add(new DashboardBalanceViewModel(s.Id, s.CatNames, s.Status.DisplayName, summary.InvoicedOutstanding, null, "Invoice outstanding"));
        }

        // Deposits to give back: every stay that ENDED without a check-out and was not forfeited (cancelled, lapsed or expired -- a lapse is a cancellation with a reason) and still holds an
        // accepted, unreleased deposit. A late bank transfer recorded after the lapse shows up here too (SECURITY 2026-10-15).
        // No look-back window: the ledger query is the filter (a transfer recorded months after a lapse is still listed).
        var refundable = await depositLedger.GetRefundableAsync(query.BranchId);
        var refundIds = refundable.Keys.ToList();
        var ended = await Rows(db.BoardingStays.AsNoTracking().Where(s => refundIds.Contains(s.Id))).ToListAsync();
        var refundDue = ended
            .Select(s => new DashboardBalanceViewModel(s.Id, s.CatNames, s.Status.DisplayName, BoardingMoney.Of(refundable[s.Id], currency), null, "Deposit to refund")).ToList();

        // An early check-out leaves invoiced charges that no longer apply: owed only until Billing's credit note covers them (the planner nets credit-note lines).
        var credits = await Rows(db.BoardingStays.AsNoTracking().Where(s => s.BranchId == query.BranchId && s.CreditDue > 0m)).ToListAsync();
        foreach (var s in credits)
        {
            var owed = BoardingChargePlanner.Plan(await chargeLedger.GetRowsAsync(s.Id), s.RateUnits.Select(GetStayDetailQueryHandler.ToUnit), 0).CreditDue;
            if (owed > 0m) refundDue.Add(new DashboardBalanceViewModel(s.Id, s.CatNames, s.Status.DisplayName, BoardingMoney.Of(owed, currency), null, "Early check-out credit"));
        }

        return new QueryResponse<BoardingDashboardViewModel>
        {
            Result = new BoardingDashboardViewModel(today, now, occupancy, arrivals, departures, notEating, overdue, unassigned, openIncidents, callOwner, unpaid, refundDue, attention)
        };
    }

    private static IQueryable<DashboardStayRow> Rows(IQueryable<BoardingStayAggregate> stays) => stays.Select(s => new DashboardStayRow(
        s.Id, s.Status, s.ArrivalOn, s.DepartureOn, s.NeedsAttention, s.DepositState, s.DepositRequired, s.DepositDueBy, s.DepositForfeited, s.CurrencyCode,
        s.Cats.Select(c => c.CheckedOutOn).Max(), s.CancelledOn, s.CreditDue, s.Cats.Select(c => new CatNameRow(c.CatId, c.CatName)).ToList(),
        s.Allocations.Select(a => new AllocationRow(a.UnitId, a.UnitType, a.OccupiedFrom, a.OccupiedTo, a.IsActive, a.Reason)).ToList(),
        s.RateUnits.Select(u => new RateUnitRow(u.Sequence, u.Kind, u.FromNight, u.ToNight, u.Amount, u.Discount, u.Description)).ToList()));
}
