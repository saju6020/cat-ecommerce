using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Boarding;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Boarding;

/// <summary>
/// US-BRD-17 AC1 -- the stay summary, ALWAYS a read. Per cat: the daily observations, the weight at check-in and check-out and the change, the incidents shared with the owner (a low incident is internal),
/// the medications given (completed Medication tasks with time, the caretaker's name for staff only), the vet visits (referrals and visit ids), the belongings with their returned status and
/// the check-out signature (who and when, never the strokes). Plus the charge lines, the deposit received and what Billing holds for the stay (clinic charges join it by Billing's composition).
/// </summary>
public sealed class GetStaySummaryQueryHandler(
    CatEcommerceDbContext db, IBoardingDepositLedger depositLedger, IBoardingChargeLedger chargeLedger,
    IQueryHandlerAsync<CatEcommerce.Read.Queries.Billing.GetBillingGroupSummaryQuery, BillingGroupSummaryViewModel> billingSummary)
    : IQueryHandlerAsync<GetStaySummaryQuery, StayReportViewModel?>
{
    public async Task<QueryResponse<StayReportViewModel?>> HandleAsync(GetStaySummaryQuery query)
    {
        if (query.CustomerId is null && query.BranchId is null) return new QueryResponse<StayReportViewModel?> { Result = null };

        var stays = db.BoardingStays.AsNoTracking().Where(s => s.Id == query.StayId);
        if (query.CustomerId is { } customerId) stays = stays.Where(s => s.CustomerId == customerId);
        if (query.BranchId is { } branchId) stays = stays.Where(s => s.BranchId == branchId);

        var stay = await stays.ProjectStay().FirstOrDefaultAsync();
        if (stay is null) return new QueryResponse<StayReportViewModel?> { Result = null };

        var isStaff = query.BranchId is not null;
        var currency = stay.CurrencyCode;

        var observations = await db.BoardingCareObservations.AsNoTracking().Where(o => o.StayId == stay.Id).OrderBy(o => o.RecordedOn)
            .Select(o => new { o.CatId, o.LocalDate, o.PortionEaten, o.WaterIntake, o.LitterOutput, o.Mood, o.Notes }).ToListAsync();
        var incidents = await db.BoardingIncidents.AsNoTracking().Where(i => i.StayId == stay.Id && (isStaff || i.Severity != IncidentSeverity.Low)).OrderBy(i => i.OccurredOn)
            .Select(i => new { i.Id, i.CatId, i.Kind, i.Severity, i.OccurredOn, i.Status, i.Resolution }).ToListAsync();
        var medicationTasks = await db.BoardingCareTasks.AsNoTracking()
            .Where(t => t.StayId == stay.Id && t.Kind == CareTaskKind.Medication && t.Status == CareTaskStatus.Done && t.CompletedOn != null).OrderBy(t => t.CompletedOn)
            .Select(t => new { t.CatId, t.Title, t.CompletedOn, t.CompletedBy }).ToListAsync();

        var byIds = medicationTasks.Where(t => t.CompletedBy != null).Select(t => t.CompletedBy!.Value).Distinct().ToList();
        var names = isStaff && byIds.Count > 0
            ? await db.Set<StaffAccountAggregate>().AsNoTracking().Where(s => byIds.Contains(s.Id)).Select(s => new { s.Id, s.FullName }).ToDictionaryAsync(s => s.Id, s => s.FullName)
            : [];

        var cats = stay.Cats.Select(c =>
        {
            decimal? weightIn = c.CheckInWeightKg, weightOut = c.CheckOutWeightKg;
            return new SummaryCatViewModel(
                c.CatId, c.CatName, weightIn, weightOut, weightIn is not null && weightOut is not null ? weightOut - weightIn : null,
                observations.Where(o => o.CatId == c.CatId).Select(o => new SummaryObservationViewModel(o.LocalDate, o.PortionEaten.DisplayName, o.WaterIntake.DisplayName, o.LitterOutput.DisplayName, o.Mood.DisplayName, o.Notes)).ToList(),
                incidents.Where(i => i.CatId == c.CatId).Select(i => new SummaryIncidentViewModel(i.Id, i.Kind.DisplayName, i.Severity.DisplayName, i.OccurredOn, i.Status.DisplayName, i.Resolution)).ToList(),
                medicationTasks.Where(t => t.CatId == c.CatId).Select(t => new SummaryMedicationViewModel(t.Title, t.CompletedOn!.Value, t.CompletedBy is { } by ? names.GetValueOrDefault(by) : null)).ToList(),
                stay.Referrals.Where(r => r.CatId == c.CatId).OrderBy(r => r.ReferredOn).Select(r => new SummaryVetVisitViewModel(r.Id, r.Reason, r.Urgency.DisplayName, r.ReferredOn, r.ReturnedOn, r.VisitId)).ToList(),
                c.Belongings.Select(b => new SummaryBelongingViewModel(b.Kind.DisplayName, b.Description, b.Returned, b.ReturnNote)).ToList(),
                c.CheckOutSignerName, c.CheckOutSignedOn);
        }).ToList();

        var charges = await db.Charges.AsNoTracking()
            .Where(c => c.SourceKind == ChargeSourceKind.BoardingStay && c.SourceId == stay.Id && c.Status == ChargeStatus.Active).OrderBy(c => c.ServiceDate).ThenBy(c => c.SourceLineRef)
            .Select(c => new { c.Id, c.SourceLineRef, c.Description, c.Quantity, c.UnitPriceAmount, c.LineDiscountAmount }).ToListAsync();
        var chargeIds = charges.Select(c => c.Id).ToList();
        var billed = chargeIds.Count == 0 ? [] : (await db.Invoices.AsNoTracking().Where(i => i.Status != InvoiceStatus.Discarded).SelectMany(i => i.Lines)
            .Where(l => l.ChargeId != null && chargeIds.Contains(l.ChargeId.Value)).Select(l => l.ChargeId!.Value).ToListAsync()).ToHashSet();

        var ledger = await depositLedger.GetAsync(stay.Id);
        var creditDue = 0m;
        if (isStaff && stay.CreditDue > 0m)
            creditDue = BoardingChargePlanner.Plan(await chargeLedger.GetRowsAsync(stay.Id), stay.RateUnits.Select(GetStayDetailQueryHandler.ToUnit), 0).CreditDue;

        var group = (await billingSummary.HandleAsync(new CatEcommerce.Read.Queries.Billing.GetBillingGroupSummaryQuery
        { Kind = "BoardingStay", GroupId = stay.Id, UserContext = query.UserContext, CorrelationId = query.CorrelationId })).Result;

        return new QueryResponse<StayReportViewModel?>
        {
            Result = new StayReportViewModel(
                stay.Id, stay.Status.DisplayName, stay.ArrivalOn, stay.DepartureOn, stay.Nights, stay.Cats.Select(c => c.CheckedOutOn).Max(), cats,
                charges.Select(c => new SummaryChargeViewModel(c.SourceLineRef, c.Description, BoardingMoney.Of(c.Quantity * c.UnitPriceAmount - c.LineDiscountAmount, currency), billed.Contains(c.Id))).ToList(),
                BoardingMoney.Of(ledger.Accepted, currency),
                new SummaryBillingViewModel(group.BilledTotal, group.UnbilledTotal, group.InvoicedOutstanding, group.InvoiceIds),
                isStaff && creditDue > 0m ? BoardingMoney.Of(creditDue, currency) : null)
        };
    }
}

public sealed class GetVetHeadroomQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetVetHeadroomQuery, IReadOnlyList<CatHeadroomViewModel>?>
{
    public async Task<QueryResponse<IReadOnlyList<CatHeadroomViewModel>?>> HandleAsync(GetVetHeadroomQuery query)
    {
        var stay = await db.BoardingStays.AsNoTracking().Where(s => s.Id == query.StayId && s.BranchId == query.BranchId).ProjectStay().FirstOrDefaultAsync();
        if (stay is null) return new QueryResponse<IReadOnlyList<CatHeadroomViewModel>?> { Result = null };

        IReadOnlyList<CatHeadroomViewModel> result = stay.Cats.Select(c =>
        {
            var h = stay.Headroom(c);
            return new CatHeadroomViewModel(c.CatId, c.CatName, h.VetVisitAuthorised, BoardingMoney.Of(h.Limit.Amount, stay.CurrencyCode), BoardingMoney.Of(h.Committed.Amount, stay.CurrencyCode), BoardingMoney.Of(h.Headroom.Amount, stay.CurrencyCode));
        }).ToList();
        return new QueryResponse<IReadOnlyList<CatHeadroomViewModel>?> { Result = result };
    }
}

public sealed class GetRelocationOptionsQueryHandler(CatEcommerceDbContext db, IBoardingInputs boardingInputs, TimeProvider timeProvider)
    : IQueryHandlerAsync<GetRelocationOptionsQuery, IReadOnlyList<RelocationOptionViewModel>?>
{
    public async Task<QueryResponse<IReadOnlyList<RelocationOptionViewModel>?>> HandleAsync(GetRelocationOptionsQuery query)
    {
        var stay = await db.BoardingStays.AsNoTracking().Where(s => s.Id == query.StayId && s.BranchId == query.BranchId)
            .Select(s => new { s.BranchId, s.ArrivalOn, s.DepartureOn, s.OccupiedFrom, CatCount = s.Cats.Count, Allocations = s.Allocations.Select(a => new AllocationRow(a.UnitId, a.UnitType, a.OccupiedFrom, a.OccupiedTo, a.IsActive, a.Reason)).ToList() })
            .FirstOrDefaultAsync();
        var current = stay is null ? null : StayAllocationRule.Current(stay.Allocations, a => a.IsActive, a => a.OccupiedFrom);
        if (stay is null || current is null) return new QueryResponse<IReadOnlyList<RelocationOptionViewModel>?> { Result = null };

        var now = timeProvider.GetUtcNow();
        var departure = query.NewDepartureOn is { } d && d > stay.DepartureOn ? d : stay.DepartureOn;
        var inputs = await boardingInputs.LoadAsync(stay.BranchId, stay.ArrivalOn, departure, query.StayId, now);
        if (inputs is null) return new QueryResponse<IReadOnlyList<RelocationOptionViewModel>?> { Result = null };

        var s = inputs.Settings;
        var window = BoardingOccupancyWindow.Compute(stay.ArrivalOn, departure, s.TimeZoneId, s.CheckInWindow, s.CheckOutWindow, s.TurnaroundHours);
        var from = now > stay.OccupiedFrom ? now : stay.OccupiedFrom;
        var options = RelocationOptions.Find(inputs, current.UnitType, stay.CatCount, new OccupancyRange(from, window.To), current.UnitId);
        var optionIds = options.Select(o => o.UnitId).ToList();
        var names = await db.BoardingUnits.AsNoTracking().Where(u => optionIds.Contains(u.Id)).Select(u => new { u.Id, u.Name }).ToDictionaryAsync(u => u.Id, u => u.Name);

        IReadOnlyList<RelocationOptionViewModel> result = options.Select(o => new RelocationOptionViewModel(o.UnitId, o.Code, names.GetValueOrDefault(o.UnitId, o.Code), o.Type.DisplayName, o.Block)).ToList();
        return new QueryResponse<IReadOnlyList<RelocationOptionViewModel>?> { Result = result };
    }
}

public sealed class GetStaysDueForInvoicingQueryHandler(IBoardingChargeLedger ledger) : IQueryHandlerAsync<GetStaysDueForInvoicingQuery, IReadOnlyList<StayInvoiceDueViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<StayInvoiceDueViewModel>>> HandleAsync(GetStaysDueForInvoicingQuery query)
    {
        IReadOnlyList<StayInvoiceDueViewModel> due = (await ledger.GetStaysDueForInvoicingAsync(query.Now)).Select(d => new StayInvoiceDueViewModel(d.StayId, d.BranchId, d.CustomerId, d.Cycle, d.ServiceDateTo)).ToList();
        return new QueryResponse<IReadOnlyList<StayInvoiceDueViewModel>> { Result = due };
    }
}

internal sealed record PreviewRow(
    Guid Id, Guid BranchId, BoardingStayStatus Status, DateOnly ArrivalOn, DateOnly DepartureOn, int CatCount, DateTimeOffset OccupiedFrom, DateTimeOffset OccupiedTo, RateCardSnapshot? Card,
    PricingSnapshot? Pricing, string CurrencyCode, decimal QuoteTotal, int ChargeRevision, List<RateUnitRow> RateUnits);

/// <summary>Owner-authorised 2026-10-14. Uses <see cref="EarlyCheckout.Compute"/> and <see cref="BoardingChargePlanner"/> -- the very calculations the check-out handler runs -- and writes nothing.</summary>
public sealed class GetEarlyCheckoutPreviewQueryHandler(CatEcommerceDbContext db, IBoardingInputs boardingInputs, IBoardingChargeLedger ledger, TimeProvider timeProvider)
    : IQueryHandlerAsync<GetEarlyCheckoutPreviewQuery, EarlyCheckoutPreviewViewModel?>
{
    public async Task<QueryResponse<EarlyCheckoutPreviewViewModel?>> HandleAsync(GetEarlyCheckoutPreviewQuery query)
    {
        var stay = await db.BoardingStays.AsNoTracking().Where(s => s.Id == query.StayId && s.BranchId == query.BranchId)
            .Select(s => new PreviewRow(
                s.Id, s.BranchId, s.Status, s.ArrivalOn, s.DepartureOn, s.Cats.Count, s.OccupiedFrom, s.OccupiedTo, s.RateCardSnapshot, s.PricingSnapshot, s.CurrencyCode, s.QuoteTotal, s.ChargeRevision,
                s.RateUnits.Select(u => new RateUnitRow(u.Sequence, u.Kind, u.FromNight, u.ToNight, u.Amount, u.Discount, u.Description)).ToList()))
            .FirstOrDefaultAsync();
        var settings = stay is null ? null : await boardingInputs.GetSettingsAsync(stay.BranchId);
        if (stay is null || settings is null || stay.Status != BoardingStayStatus.CheckedIn) return new QueryResponse<EarlyCheckoutPreviewViewModel?> { Result = null };

        var now = timeProvider.GetUtcNow();
        var today = BoardingOccupancyWindow.LocalDateOf(now, settings.TimeZoneId);
        var outcome = EarlyCheckout.Compute(
            new EarlyCheckoutInput(stay.Status, stay.ArrivalOn, stay.DepartureOn, stay.CatCount, stay.OccupiedFrom, stay.OccupiedTo, stay.Card, stay.Pricing), today, now, settings.TurnaroundHours);
        var currency = stay.CurrencyCode;

        var rows = await ledger.GetRowsAsync(stay.Id);
        var units = outcome.Quote?.Units ?? stay.RateUnits.Select(GetStayDetailQueryHandler.ToUnit).ToList();
        var plan = BoardingChargePlanner.Plan(rows, units, stay.ChargeRevision);
        var descriptions = await db.Charges.AsNoTracking().Where(c => c.SourceKind == ChargeSourceKind.BoardingStay && c.SourceId == stay.Id).Select(c => new { c.SourceLineRef, c.Description }).ToDictionaryAsync(c => c.SourceLineRef, c => c.Description);

        var lines = new List<EarlyCheckoutLineViewModel>();
        foreach (var row in rows.Where(r => !r.IsVoided))
            lines.Add(new EarlyCheckoutLineViewModel(row.LineRef, descriptions.GetValueOrDefault(row.LineRef, row.LineRef), BoardingMoney.Of(row.Net, currency), plan.VoidRefs.Contains(row.LineRef) ? "Void" : "Keep"));
        foreach (var planned in plan.Record)
            lines.Add(new EarlyCheckoutLineViewModel(planned.LineRef, planned.Unit.Description, BoardingMoney.Of(planned.Unit.Net, currency), "Record"));

        var newTotal = outcome.Quote?.Total ?? stay.QuoteTotal;
        return new QueryResponse<EarlyCheckoutPreviewViewModel?>
        {
            Result = new EarlyCheckoutPreviewViewModel(
                outcome.IsEarly, outcome.BookedNights, outcome.ChargedNights, outcome.Quote is null ? null : QuoteMapping.ToViewModel(outcome.Quote, currency), lines,
                BoardingMoney.Of(plan.CreditDue, currency), EarlyCheckoutCredit.Outcome(plan.CreditDue, stay.Pricing?.EarlyCheckoutRefundAsCredit),
                today, stay.DepartureOn, outcome.ChargedDepartureOn, outcome.Window.To, BoardingMoney.Of(stay.QuoteTotal, currency), BoardingMoney.Of(newTotal, currency), outcome.IsLate)
        };
    }
}
