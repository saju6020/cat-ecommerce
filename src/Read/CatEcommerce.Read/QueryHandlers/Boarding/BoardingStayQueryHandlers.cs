using System.Globalization;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Boarding;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Boarding;

/// <summary>Maps the domain quote to the wire shape: rate units and discount lines SEPARATE (US-BRD-03 AC2).</summary>
public static class QuoteMapping
{
    public static QuoteViewModel ToViewModel(BoardingQuote quote, string currency) => new(
        quote.Basis.DisplayName,
        quote.Units.Select(u => new QuoteUnitViewModel(u.Sequence, u.Kind.DisplayName, u.FromNight, u.ToNight, u.Description, BoardingMoney.Of(u.Amount, currency))).ToList(),
        quote.Units.Where(u => u.Discount > 0m).Select(u => new QuoteDiscountLineViewModel(u.Sequence, "Long-stay discount - " + u.Description, BoardingMoney.Of(u.Discount, currency))).ToList(),
        BoardingMoney.Of(quote.Gross, currency), BoardingMoney.Of(quote.Discount, currency), BoardingMoney.Of(quote.Total, currency));

    /// <summary>The stay's stored quote from PROJECTED rate-unit rows (a read never rehydrates the stay).</summary>
    internal static QuoteViewModel FromRows(StayRateBasis? basis, IReadOnlyList<RateUnitRow> units, decimal quoteTotal, decimal discountTotal, string currency) => new(
        (basis ?? StayRateBasis.Nightly).DisplayName,
        units.OrderBy(u => u.Sequence).Select(u => new QuoteUnitViewModel(u.Sequence, u.Kind.DisplayName, u.FromNight, u.ToNight, u.Description, BoardingMoney.Of(u.Amount, currency))).ToList(),
        units.Where(u => u.Discount > 0m).OrderBy(u => u.Sequence).Select(u => new QuoteDiscountLineViewModel(u.Sequence, "Long-stay discount - " + u.Description, BoardingMoney.Of(u.Discount, currency))).ToList(),
        BoardingMoney.Of(quoteTotal + discountTotal, currency), BoardingMoney.Of(discountTotal, currency), BoardingMoney.Of(quoteTotal, currency));
}

// ── the columns a stay read needs, projected inside the Select (ADR-012: a query handler never materialises the aggregate) ─────────────────────────────

internal sealed record RateUnitRow(int Sequence, RateUnitKind Kind, int FromNight, int ToNight, decimal Amount, decimal Discount, string Description);

internal sealed record FeedingRow(TimeOnly Time, string Portion, FoodSource FoodSource, string FoodDescription, Guid? HouseFoodSkuId);

internal sealed record MedicationRow(string Name, string Dose, string Route, TimeOnly[] Times, string? Instructions, DateOnly? FromOn, DateOnly? ToOn);

internal sealed record BelongingRow(Guid Id, BelongingKind Kind, string Description, Guid[] FileIds, bool? Returned, string? ReturnNote);

internal sealed record StayCatRow(
    Guid CatId, string CatName, VaccinationGateStatus GateStatus, IReadOnlyList<GateReason> GateReasons, DateOnly? GateEvaluatedOn, Guid? GateOverriddenBy, DateTimeOffset? GateOverriddenOn,
    string? GateOverrideReason, bool HasCareProfile, LitterType? Litter, string? LitterNote, bool VetVisitAuthorised, decimal VetSpendingLimit, string? EmergencyContactName,
    string? EmergencyContactPhone, string? EmergencyContactRelationship, string? CareNotes, List<FeedingRow> Feedings, List<MedicationRow> Medications, bool SuspectedInfectious, string? InfectiousNote,
    DateTimeOffset? CheckedInOn, decimal? CheckInWeightKg, string? CheckInHealthObservation, bool NoBelongings, List<BelongingRow> Belongings, string? CheckInSignerName, DateTimeOffset? CheckInSignedOn,
    DateTimeOffset? CheckedOutOn, decimal? CheckOutWeightKg, string? CheckOutSignerName, DateTimeOffset? CheckOutSignedOn);

internal sealed record AllocationRow(Guid UnitId, BoardingUnitType UnitType, DateTimeOffset OccupiedFrom, DateTimeOffset OccupiedTo, bool IsActive, string? Reason);

internal sealed record StatusChangeRow(BoardingStayStatus From, BoardingStayStatus To, DateTimeOffset ChangedOn, Guid ChangedBy, int Sequence);

internal sealed record ReferralRow(Guid Id, Guid CatId, string Reason, TriagePriority Urgency, DateTimeOffset ReferredOn, Guid? VisitId, DateTimeOffset? ReturnedOn);

internal sealed record ApprovalRow(
    Guid Id, Guid CatId, decimal Amount, string Description, VetApprovalStatus Status, DateTimeOffset RequestedOn, DateTimeOffset? DecidedOn, AcknowledgementChannel? DecidedVia, string? DecisionNote);

internal sealed record CommitmentRow(Guid CatId, decimal Amount);

internal sealed record SurgeryRow(Guid CatId, DateTimeOffset StartsOn, DateTimeOffset? DischargedOn);

internal sealed record StayRow(
    Guid Id, int Version, Guid BranchId, Guid CustomerId, BoardingChannel Channel, BoardingStayStatus Status, DateOnly ArrivalOn, DateOnly DepartureOn, DateTimeOffset OccupiedFrom,
    DateTimeOffset OccupiedTo, BoardingUnitType RequestedUnitType, string CurrencyCode, bool RequiresIsolation, bool NeedsAttention, StayAttentionReason? AttentionReason, StayRateBasis? RateBasis,
    decimal QuoteTotal, decimal DiscountTotal, decimal DepositRequired, DepositState DepositState, DateTimeOffset? DepositDueBy, DateTimeOffset? CancelledOn, string? CancellationNote,
    bool DepositForfeited, decimal CreditDue, string? AttentionClearedNote, List<StayCatRow> Cats, List<AllocationRow> Allocations, List<RateUnitRow> RateUnits, List<StatusChangeRow> History,
    List<ReferralRow> Referrals, List<ApprovalRow> Approvals, List<CommitmentRow> Commitments, List<SurgeryRow> Surgeries)
{
    public int Nights => StayDateRules.Nights(ArrivalOn, DepartureOn);

    public AllocationRow? Current => StayAllocationRule.Current(Allocations, a => a.IsActive, a => a.OccupiedFrom);

    public string? AwayReason(Guid catId, DateTimeOffset at) =>
        StayAwayRule.Reason(catId, at, Referrals.Select(r => (r.CatId, r.ReturnedOn is null)), Surgeries.Select(s => (s.CatId, s.StartsOn, s.DischargedOn)));

    public VetSpendingHeadroomResult Headroom(StayCatRow cat) => VetSpendingHeadroom.Compute(
        cat.VetVisitAuthorised, cat.VetSpendingLimit, Approvals.Where(a => a.CatId == cat.CatId && a.Status == VetApprovalStatus.Approved).Select(a => a.Amount),
        Commitments.Where(c => c.CatId == cat.CatId).Select(c => c.Amount), CurrencyCode);
}

internal static class StayProjections
{
    /// <summary>One <c>Select</c> for the stay and its bounded owned collections: no aggregate is materialised.</summary>
    public static IQueryable<StayRow> ProjectStay(this IQueryable<CatEcommerce.Domain.Aggregates.Boarding.BoardingStayAggregate> stays) => stays.Select(s => new StayRow(
        s.Id, s.Version, s.BranchId, s.CustomerId, s.Channel, s.Status, s.ArrivalOn, s.DepartureOn, s.OccupiedFrom, s.OccupiedTo, s.RequestedUnitType, s.CurrencyCode, s.RequiresIsolation,
        s.NeedsAttention, s.AttentionReason, s.RateBasis, s.QuoteTotal, s.DiscountTotal, s.DepositRequired, s.DepositState, s.DepositDueBy, s.CancelledOn, s.CancellationNote, s.DepositForfeited,
        s.CreditDue, s.AttentionClearedNote,
        s.Cats.Select(c => new StayCatRow(
            c.CatId, c.CatName, c.GateStatus, c.GateReasons, c.GateEvaluatedOn, c.GateOverriddenBy, c.GateOverriddenOn, c.GateOverrideReason, c.HasCareProfile, c.Litter, c.LitterNote,
            c.VetVisitAuthorised, c.VetSpendingLimit, c.EmergencyContactName, c.EmergencyContactPhone, c.EmergencyContactRelationship, c.CareNotes,
            c.Feedings.Select(f => new FeedingRow(f.Time, f.Portion, f.FoodSource, f.FoodDescription, f.HouseFoodSkuId)).ToList(),
            c.Medications.Select(m => new MedicationRow(m.Name, m.Dose, m.Route, m.Times, m.Instructions, m.FromOn, m.ToOn)).ToList(),
            c.SuspectedInfectious, c.InfectiousNote, c.CheckedInOn, c.CheckInWeightKg, c.CheckInHealthObservation, c.NoBelongings,
            c.Belongings.Select(b => new BelongingRow(b.Id, b.Kind, b.Description, b.FileIds, b.Returned, b.ReturnNote)).ToList(),
            c.CheckInSignerName, c.CheckInSignedOn, c.CheckedOutOn, c.CheckOutWeightKg, c.CheckOutSignerName, c.CheckOutSignedOn)).ToList(),
        s.Allocations.Select(a => new AllocationRow(a.UnitId, a.UnitType, a.OccupiedFrom, a.OccupiedTo, a.IsActive, a.Reason)).ToList(),
        s.RateUnits.Select(u => new RateUnitRow(u.Sequence, u.Kind, u.FromNight, u.ToNight, u.Amount, u.Discount, u.Description)).ToList(),
        s.StatusHistory.Select(h => new StatusChangeRow(h.From, h.To, h.ChangedOn, h.ChangedBy, h.Sequence)).ToList(),
        s.Referrals.Select(r => new ReferralRow(r.Id, r.CatId, r.Reason, r.Urgency, r.ReferredOn, r.VisitId, r.ReturnedOn)).ToList(),
        s.Approvals.Select(a => new ApprovalRow(a.Id, a.CatId, a.Amount, a.Description, a.Status, a.RequestedOn, a.DecidedOn, a.DecidedVia, a.DecisionNote)).ToList(),
        s.Commitments.Select(c => new CommitmentRow(c.CatId, c.Amount)).ToList(),
        s.Surgeries.Select(x => new SurgeryRow(x.CatId, x.StartsOn, x.DischargedOn)).ToList()));
}

/// <summary>US-BRD-02 / 03: the computed availability for a date range and cat count -- booking, maintenance and turnaround excluded, nearest alternatives when full.</summary>
public sealed class GetBoardingAvailabilityQueryHandler(IBoardingInputs boardingInputs, TimeProvider timeProvider)
    : IQueryHandlerAsync<GetBoardingAvailabilityQuery, BoardingAvailabilityViewModel>
{
    public async Task<QueryResponse<BoardingAvailabilityViewModel>> HandleAsync(GetBoardingAvailabilityQuery query)
    {
        var now = timeProvider.GetUtcNow();
        var inputs = await boardingInputs.LoadAsync(query.BranchId, query.ArrivalOn, query.DepartureOn, query.ExcludeStayId, now);
        if (inputs is null)
            return new QueryResponse<BoardingAvailabilityViewModel>
            {
                Result = new BoardingAvailabilityViewModel("UTC", 0, null, null, new AvailabilityViolationViewModel("BranchNotFound", "Branch not found."), [], null, [], false)
            };

        var s = inputs.Settings;
        var mode = query.IsStaff ? AvailabilityMode.Staff : AvailabilityMode.Online;
        var result = BoardingAvailabilityFinder.Find(new AvailabilityRequest(
            query.ArrivalOn, query.DepartureOn, Math.Max(1, query.CatCount), now, s.TimeZoneId, s.CheckInWindow, s.CheckOutWindow, s.TurnaroundHours,
            s.MaxStayNights, s.BookingHorizonDays, s.AlternativeSearchDays, inputs.Units, inputs.BusyUnits, query.IsStaff && query.RequiresIsolation, mode,
            inputs.RateCards, s.Pricing));

        var currency = s.CurrencyCode;
        var view = new BoardingAvailabilityViewModel(
            s.TimeZoneId, result.Nights,
            result.Violation is null ? result.Window.From : null, result.Violation is null ? result.Window.To : null,
            result.Violation is null ? null : new AvailabilityViolationViewModel(result.Violation.Code, result.Violation.Message),
            result.UnitTypes.Select(t => new AvailabilityUnitTypeViewModel(t.UnitType.DisplayName, t.CapacityCats, t.FreeUnitCount, QuoteMapping.ToViewModel(t.Quote, currency))).ToList(),
            query.IsStaff ? result.Units.Select(u => new AvailabilityUnitViewModel(u.UnitId, u.Code, u.Type.DisplayName, u.CapacityCats, u.Block, u.IsFree)).ToList() : null,
            result.Alternatives.Select(a => new AvailabilityAlternativeViewModel(a.ArrivalOn, a.DepartureOn, a.CheapestType.DisplayName, BoardingMoney.Of(a.Total, currency))).ToList(),
            result.WaitlistAvailable);

        return new QueryResponse<BoardingAvailabilityViewModel> { Result = view };
    }
}

public sealed class GetStayDetailQueryHandler(CatEcommerceDbContext db, IBoardingInputs boardingInputs, IBoardingDepositLedger depositLedger, IBoardingChargeLedger chargeLedger, TimeProvider timeProvider)
    : IQueryHandlerAsync<GetStayDetailQuery, StayViewModel?>
{
    public async Task<QueryResponse<StayViewModel?>> HandleAsync(GetStayDetailQuery query)
    {
        if (query.CustomerId is null && query.BranchId is null) return new QueryResponse<StayViewModel?> { Result = null };

        var stays = db.BoardingStays.AsNoTracking().Where(s => s.Id == query.StayId);
        if (query.CustomerId is { } customerId) stays = stays.Where(s => s.CustomerId == customerId);
        if (query.BranchId is { } branchId) stays = stays.Where(s => s.BranchId == branchId);

        var stay = await stays.ProjectStay().FirstOrDefaultAsync();
        if (stay is null) return new QueryResponse<StayViewModel?> { Result = null };

        var isStaff = query.BranchId is not null;
        var zone = (await boardingInputs.GetSettingsAsync(stay.BranchId))?.TimeZoneId ?? "UTC";

        var catIds = stay.Cats.Select(c => c.CatId).ToList();
        var cats = await db.Set<CatAggregate>().AsNoTracking().Where(c => catIds.Contains(c.Id)).Select(c => new { c.Id, c.IsHighRisk, c.HandlingNote }).ToListAsync();

        string? customerName = isStaff ? await db.Set<CustomerAccountAggregate>().AsNoTracking().Where(c => c.Id == stay.CustomerId).Select(c => c.FullName).FirstOrDefaultAsync() : null;

        StayUnitViewModel? unit = null;
        if (stay.Current is { } allocation)
        {
            var u = await db.BoardingUnits.AsNoTracking().Where(x => x.Id == allocation.UnitId).Select(x => new { x.Code, x.Name, x.Block }).FirstOrDefaultAsync();
            if (u is not null) unit = new StayUnitViewModel(allocation.UnitId, u.Code, u.Name, allocation.UnitType.DisplayName, u.Block);
        }

        var ledger = await depositLedger.GetAsync(stay.Id);
        IReadOnlyList<IsolationEntryViewModel>? isolation = null;
        if (isStaff)
        {
            var entries = await db.BoardingIsolationEntries.AsNoTracking().Where(e => e.StayId == stay.Id).OrderBy(e => e.EnteredOn)
                .Select(e => new { e.Id, e.UnitId, e.StaffAccountId, e.EnteredOn, e.LeftOn, e.Purpose }).ToListAsync();
            var staffIds = entries.Select(e => e.StaffAccountId).Distinct().ToList();
            var names = await db.Set<StaffAccountAggregate>().AsNoTracking().Where(s => staffIds.Contains(s.Id)).Select(s => new { s.Id, s.FullName }).ToDictionaryAsync(s => s.Id, s => s.FullName);
            isolation = entries.Select(e => new IsolationEntryViewModel(e.Id, e.UnitId, e.StaffAccountId, names.GetValueOrDefault(e.StaffAccountId), e.EnteredOn, e.LeftOn, e.Purpose)).ToList();
        }

        var incidents = (await db.BoardingIncidents.AsNoTracking().Where(i => i.StayId == stay.Id && (isStaff || i.Severity != IncidentSeverity.Low)).OrderBy(i => i.OccurredOn)
                .Select(i => new { i.Id, i.CatId, i.Kind, i.Severity, i.OccurredOn, i.OwnerAcknowledgedOn, i.Status }).ToListAsync())
            .Select(i => new StayIncidentViewModel(i.Id, i.CatId, i.Kind.DisplayName, i.Severity.DisplayName, i.OccurredOn, i.OwnerAcknowledgedOn is not null, i.Status.DisplayName)).ToList();

        var charges = await ChargesAsync(stay, isStaff);

        // What is still owed for invoiced charges that no longer apply: the same planner the check-out runs, over the current ledger -- so it clears once a credit note is issued.
        var creditDue = 0m;
        if (isStaff && stay.CreditDue > 0m)
        {
            var rows = await chargeLedger.GetRowsAsync(stay.Id);
            creditDue = BoardingChargePlanner.Plan(rows, stay.RateUnits.Select(ToUnit), 0).CreditDue;
        }

        return new QueryResponse<StayViewModel?> { Result = StayMapping.ToViewModel(stay, zone, unit, cats.ToDictionary(c => c.Id, c => (c.IsHighRisk, c.HandlingNote)), customerName, isStaff, ledger, isolation, charges, timeProvider.GetUtcNow(), incidents, creditDue) };
    }

    private async Task<IReadOnlyList<StayChargeViewModel>> ChargesAsync(StayRow stay, bool isStaff)
    {
        var rows = await db.Charges.AsNoTracking().Where(c => c.SourceKind == ChargeSourceKind.BoardingStay && c.SourceId == stay.Id && (isStaff || c.Status == ChargeStatus.Active))
            .OrderBy(c => c.ServiceDate).ThenBy(c => c.SourceLineRef)
            .Select(c => new { c.Id, c.SourceLineRef, c.Description, c.Quantity, c.UnitPriceAmount, c.LineDiscountAmount, c.ServiceDate, c.Status })
            .ToListAsync();
        if (rows.Count == 0) return [];

        var ids = rows.Select(r => r.Id).ToList();
        var billed = (await db.Invoices.AsNoTracking().Where(i => i.Status != InvoiceStatus.Discarded).SelectMany(i => i.Lines)
            .Where(l => l.ChargeId != null && ids.Contains(l.ChargeId.Value)).Select(l => l.ChargeId!.Value).ToListAsync()).ToHashSet();

        return rows.Select(r => new StayChargeViewModel(
            r.Id, r.SourceLineRef, r.Description, BoardingMoney.Of(r.Quantity * r.UnitPriceAmount - r.LineDiscountAmount, stay.CurrencyCode), r.ServiceDate, billed.Contains(r.Id), r.Status == ChargeStatus.Voided)).ToList();
    }

    internal static CatEcommerce.Domain.Entities.QuotedRateUnit ToUnit(RateUnitRow u) =>
        new(Guid.Empty, u.Sequence, u.Kind, u.FromNight, u.ToNight, u.Amount, u.Discount, u.Description);
}

internal static class StayMapping
{
    public static StayViewModel ToViewModel(
        StayRow stay, string zone, StayUnitViewModel? unit, IReadOnlyDictionary<Guid, (bool IsHighRisk, string? HandlingNote)> catFacts, string? customerName, bool isStaff, DepositLedgerEntry ledger,
        IReadOnlyList<IsolationEntryViewModel>? isolationEntries, IReadOnlyList<StayChargeViewModel>? charges, DateTimeOffset at, IReadOnlyList<StayIncidentViewModel>? incidents, decimal creditDue)
    {
        var currency = stay.CurrencyCode;
        var cats = stay.Cats.Select(c =>
        {
            catFacts.TryGetValue(c.CatId, out var facts);
            return new StayCatViewModel(
                c.CatId, c.CatName, facts.IsHighRisk, facts.HandlingNote,
                new StayCatGateViewModel(
                    c.GateStatus.DisplayName, c.GateReasons.Select(r => new GateReasonViewModel(r.VaccineCode, r.VaccineName, r.Reason, r.DueOn)).ToList(),
                    c.GateEvaluatedOn, c.GateOverriddenBy is not null, isStaff ? c.GateOverrideReason : null, c.GateOverriddenOn),
                !c.HasCareProfile ? null : new CareProfileViewModel(
                    c.Feedings.OrderBy(f => f.Time).Select(f => new FeedingViewModel(f.Time.ToString("HH:mm", CultureInfo.InvariantCulture), f.Portion, f.FoodSource.DisplayName, f.FoodDescription, f.HouseFoodSkuId)).ToList(),
                    c.Medications.Select(m => new MedicationViewModel(m.Name, m.Dose, m.Route, m.Times.Select(t => t.ToString("HH:mm", CultureInfo.InvariantCulture)).ToList(), m.Instructions, m.FromOn, m.ToOn)).ToList(),
                    c.Litter!.DisplayName, c.LitterNote, c.VetVisitAuthorised, BoardingMoney.Of(c.VetSpendingLimit, currency),
                    new EmergencyContactViewModel(c.EmergencyContactName!, c.EmergencyContactPhone!, c.EmergencyContactRelationship), c.CareNotes),
                c.SuspectedInfectious, isStaff ? c.InfectiousNote : null,
                c.CheckedInOn is null ? null : new StayCatCheckInViewModel(
                    c.CheckedInOn.Value, c.CheckInWeightKg ?? 0m, isStaff ? c.CheckInHealthObservation : null, c.NoBelongings,
                    c.Belongings.Select(b => new BelongingViewModel(b.Id, b.Kind.DisplayName, b.Description, b.FileIds, b.Returned, b.ReturnNote)).ToList(),
                    c.CheckInSignerName, c.CheckInSignedOn, c.CheckedOutOn, c.CheckOutWeightKg, c.CheckOutSignerName),
                stay.Status == BoardingStayStatus.CheckedIn ? stay.AwayReason(c.CatId, at) : null);
        }).ToList();
        var nameOf = stay.Cats.ToDictionary(c => c.CatId, c => c.CatName);

        return new StayViewModel(
            stay.Id, stay.Version, stay.BranchId, zone, stay.CustomerId, customerName, stay.Status.DisplayName, StayTransitionRules.AllowedActions(stay.Status),
            stay.Channel.DisplayName, stay.ArrivalOn, stay.DepartureOn, stay.OccupiedFrom, stay.OccupiedTo, stay.Nights, stay.RequestedUnitType.DisplayName,
            unit, stay.RequiresIsolation, stay.NeedsAttention, stay.AttentionReason?.DisplayName,
            stay.RateUnits.Count == 0 ? null : QuoteMapping.FromRows(stay.RateBasis, stay.RateUnits, stay.QuoteTotal, stay.DiscountTotal, currency),
            new StayDepositViewModel(
                BoardingMoney.Of(stay.DepositRequired, currency), stay.DepositState.DisplayName, stay.DepositDueBy, BoardingMoney.Of(ledger.Accepted, currency),
                BoardingMoney.Of(ledger.PendingApproval, currency), BoardingMoney.Of(Math.Max(0m, stay.DepositRequired - ledger.Accepted), currency)),
            stay.RateUnits.Count == 0 ? null : BoardingMoney.Of(Math.Max(0m, stay.QuoteTotal - ledger.Accepted), currency),
            cats,
            stay.History.OrderBy(h => h.Sequence).Select(h => new StayStatusChangeViewModel(h.From.DisplayName, h.To.DisplayName, h.ChangedOn, h.ChangedBy)).ToList(),
            stay.CancelledOn, isStaff ? stay.CancellationNote : null, stay.DepositForfeited,
            isStaff ? stay.Allocations.OrderBy(a => a.OccupiedFrom).Select(a => new StayAllocationViewModel(a.UnitId, a.UnitType.DisplayName, a.OccupiedFrom, a.OccupiedTo, a.IsActive, a.Reason)).ToList() : null,
            isolationEntries, stay.Current?.UnitType == BoardingUnitType.Isolation,
            stay.Referrals.OrderBy(r => r.ReferredOn).Select(r => new StayReferralViewModel(r.Id, r.CatId, nameOf.GetValueOrDefault(r.CatId, ""), r.Reason, r.Urgency.DisplayName, r.ReferredOn, r.VisitId, r.ReturnedOn)).ToList(),
            stay.Approvals.OrderBy(a => a.RequestedOn).Select(a => new StayApprovalViewModel(
                a.Id, a.CatId, nameOf.GetValueOrDefault(a.CatId, ""), BoardingMoney.Of(a.Amount, currency), a.Description, a.Status.DisplayName, a.RequestedOn, a.DecidedOn, a.DecidedVia?.DisplayName, isStaff ? a.DecisionNote : null)).ToList(),
            charges, isStaff && creditDue > 0m ? BoardingMoney.Of(creditDue, currency) : null,
            isStaff ? stay.Cats.Select(c => { var h = stay.Headroom(c); return new CatHeadroomViewModel(c.CatId, c.CatName, h.VetVisitAuthorised, BoardingMoney.Of(h.Limit.Amount, currency), BoardingMoney.Of(h.Committed.Amount, currency), BoardingMoney.Of(h.Headroom.Amount, currency)); }).ToList() : null,
            isStaff ? stay.AttentionClearedNote : null, incidents ?? []);
    }
}

internal sealed record StaySummaryRow(
    Guid Id, BoardingStayStatus Status, DateOnly ArrivalOn, DateOnly DepartureOn, List<string> CatNames, BoardingUnitType RequestedUnitType, DepositState DepositState, bool HasQuote,
    decimal QuoteTotal, string CurrencyCode, Guid CustomerId);

internal static class StaySummaries
{
    public static IQueryable<StaySummaryRow> Project(this IQueryable<CatEcommerce.Domain.Aggregates.Boarding.BoardingStayAggregate> stays) => stays.Select(s => new StaySummaryRow(
        s.Id, s.Status, s.ArrivalOn, s.DepartureOn, s.Cats.Select(c => c.CatName).ToList(), s.RequestedUnitType, s.DepositState, s.RateUnits.Any(), s.QuoteTotal, s.CurrencyCode, s.CustomerId));

    public static IReadOnlyList<StaySummaryViewModel> Map(
        IReadOnlyList<StaySummaryRow> stays, IReadOnlyDictionary<Guid, string>? customerNames, IReadOnlyDictionary<Guid, DepositLedgerEntry> ledgers) =>
        stays.Select(s => new StaySummaryViewModel(
            s.Id, s.Status.DisplayName, s.ArrivalOn, s.DepartureOn, StayDateRules.Nights(s.ArrivalOn, s.DepartureOn), s.CatNames, s.RequestedUnitType.DisplayName,
            null, s.DepositState.DisplayName, s.HasQuote ? BoardingMoney.Of(Math.Max(0m, s.QuoteTotal - (ledgers.TryGetValue(s.Id, out var ledger) ? ledger.Accepted : 0m)), s.CurrencyCode) : null,
            s.CustomerId, customerNames is not null && customerNames.TryGetValue(s.CustomerId, out var name) ? name : null)).ToList();
}

public sealed class GetCustomerStaysQueryHandler(CatEcommerceDbContext db, TimeProvider timeProvider, IBoardingDepositLedger depositLedger)
    : IQueryHandlerAsync<GetCustomerStaysQuery, PagedResult<StaySummaryViewModel>>
{
    public async Task<QueryResponse<PagedResult<StaySummaryViewModel>>> HandleAsync(GetCustomerStaysQuery query)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var stays = db.BoardingStays.AsNoTracking().Where(s => s.CustomerId == query.CustomerId);

        // Enquiries that never became a reservation are not "stays" to the owner; a cancelled one is history.
        stays = query.Scope switch
        {
            "past" => stays.Where(s => s.DepartureOn < today || s.Status == BoardingStayStatus.CheckedOut || s.Status == BoardingStayStatus.Cancelled || s.Status == BoardingStayStatus.NoShow),
            "current" => stays.Where(s => s.Status == BoardingStayStatus.CheckedIn),
            _ => stays.Where(s => s.Status == BoardingStayStatus.Enquiry || s.Status == BoardingStayStatus.Reserved || s.Status == BoardingStayStatus.Confirmed),
        };

        var total = await stays.CountAsync();
        var rows = await stays.OrderByDescending(s => s.ArrivalOn).Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize).Project().ToListAsync();

        return new QueryResponse<PagedResult<StaySummaryViewModel>>
        {
            Result = new PagedResult<StaySummaryViewModel>(StaySummaries.Map(rows, null, await depositLedger.GetManyAsync(rows.Select(r => r.Id).ToList())), query.PageNumber, query.PageSize, total)
        };
    }
}

public sealed class GetStaysQueryHandler(CatEcommerceDbContext db, IBoardingDepositLedger depositLedger) : IQueryHandlerAsync<GetStaysQuery, PagedResult<StaySummaryViewModel>>
{
    public async Task<QueryResponse<PagedResult<StaySummaryViewModel>>> HandleAsync(GetStaysQuery query)
    {
        var stays = db.BoardingStays.AsNoTracking().Where(s => s.BranchId == query.BranchId);
        if (!string.IsNullOrWhiteSpace(query.Status) && EnumerationParsing.TryParse<BoardingStayStatus>(query.Status, out var status) && status is not null)
            stays = stays.Where(s => s.Status == status);
        if (query.From is { } from) stays = stays.Where(s => s.DepartureOn >= from);
        if (query.To is { } to) stays = stays.Where(s => s.ArrivalOn <= to);
        if (query.CustomerId is { } customerId) stays = stays.Where(s => s.CustomerId == customerId);
        if (query.CatId is { } catId) stays = stays.Where(s => s.Cats.Any(c => c.CatId == catId));

        var total = await stays.CountAsync();
        var rows = await stays.OrderByDescending(s => s.ArrivalOn).Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize).Project().ToListAsync();

        var customerIds = rows.Select(r => r.CustomerId).Distinct().ToList();
        var names = await db.Set<CustomerAccountAggregate>().AsNoTracking().Where(c => customerIds.Contains(c.Id)).Select(c => new { c.Id, c.FullName }).ToDictionaryAsync(c => c.Id, c => c.FullName);

        return new QueryResponse<PagedResult<StaySummaryViewModel>>
        {
            Result = new PagedResult<StaySummaryViewModel>(StaySummaries.Map(rows, names, await depositLedger.GetManyAsync(rows.Select(r => r.Id).ToList())), query.PageNumber, query.PageSize, total)
        };
    }
}

/// <summary>The staff occupancy grid (units x days, at most 31 days): stay blocks, maintenance and turnaround, by branch-local date.</summary>
public sealed class GetBoardingOccupancyQueryHandler(CatEcommerceDbContext db, IBoardingInputs boardingInputs)
    : IQueryHandlerAsync<GetBoardingOccupancyQuery, BoardingOccupancyViewModel>
{
    public async Task<QueryResponse<BoardingOccupancyViewModel>> HandleAsync(GetBoardingOccupancyQuery query)
    {
        var settings = await boardingInputs.GetSettingsAsync(query.BranchId);
        var zone = settings?.TimeZoneId ?? "UTC";
        var from = BoardingOccupancyWindow.ForDays(query.From, query.To, zone);

        var units = await db.BoardingUnits.AsNoTracking().Where(u => u.BranchId == query.BranchId)
            .OrderBy(u => u.Block).ThenBy(u => u.SortOrder).ThenBy(u => u.Code)
            .Select(u => new { u.Id, u.Code, u.Name, u.Type, u.Block, u.IsActive }).ToListAsync();

        var allocations = await db.BoardingStays.AsNoTracking()
            .Where(s => s.BranchId == query.BranchId)
            .SelectMany(s => s.Allocations, (s, a) => new { StayId = s.Id, s.Status, CatNames = s.Cats.Select(c => c.CatName).ToList(), a.UnitId, a.IsActive, a.OccupiedFrom, a.OccupiedTo })
            .Where(a => a.IsActive && a.OccupiedTo > from.From && a.OccupiedFrom < from.To)
            .ToListAsync();
        var blocks = await db.BoardingMaintenanceBlocks.AsNoTracking()
            .Where(b => b.BranchId == query.BranchId && !b.IsCancelled && b.OccupiedTo > from.From && b.OccupiedFrom < from.To)
            .Select(b => new { b.UnitId, b.OccupiedFrom, b.OccupiedTo, b.Reason }).ToListAsync();

        var rows = new List<OccupancyRowViewModel>();
        foreach (var unit in units)
        {
            var cells = new List<OccupancyCellViewModel>();
            for (var date = query.From; date <= query.To; date = date.AddDays(1))
            {
                var day = BoardingOccupancyWindow.ForDays(date, date, zone);
                var maintenance = blocks.FirstOrDefault(b => b.UnitId == unit.Id && b.OccupiedFrom < day.To && day.From < b.OccupiedTo);
                var stay = allocations.FirstOrDefault(a => a.UnitId == unit.Id && a.OccupiedFrom < day.To && day.From < a.OccupiedTo);
                cells.Add(maintenance is not null ? new OccupancyCellViewModel(date, "Maintenance", null, maintenance.Reason)
                    : stay is not null ? new OccupancyCellViewModel(date, "Occupied", stay.StayId, stay.Status.DisplayName, stay.CatNames, stay.OccupiedTo.AddHours(-(settings?.TurnaroundHours ?? 0)) <= day.From)
                    : new OccupancyCellViewModel(date, "Free", null, null));
            }

            rows.Add(new OccupancyRowViewModel(unit.Id, unit.Code, unit.Name, unit.Type.DisplayName, unit.Block, unit.IsActive, cells));
        }

        return new QueryResponse<BoardingOccupancyViewModel> { Result = new BoardingOccupancyViewModel(query.From, query.To, rows) };
    }
}
