using CatEcommerce.Application.CommandHandlers.Billing;
using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Application.Commands.Boarding;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Boarding;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Boarding.BoardingHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Boarding;

/// <summary>
/// HO-BRD-001 section 8.15 (mismatch 2 of section 1.1 Amended) -- pushes the stay's charges to Billing through <see cref="IBillableChargeRecorder"/>, IN THE HANDLER, before the stay's own save, so
/// the charges commit atomically with the stay (the producer rules of HO-BIL-001 section 8.4). Pure planning is <see cref="BoardingChargePlanner"/>; this only executes it.
/// </summary>
public static class BoardingCharges
{
    public static async Task<BoardingChargePlan> SyncAsync(BoardingStayAggregate stay, IBoardingChargeLedger ledger, IBillableChargeRecorder recorder, CancellationToken ct = default)
    {
        var rows = await ledger.GetRowsAsync(stay.Id, ct);
        var plan = BoardingChargePlanner.Plan(rows, stay.RateUnits, stay.ChargeRevision);

        foreach (var lineRef in plan.VoidRefs)
            await recorder.VoidAsync(new ChargeSourceKey(ChargeSourceKind.BoardingStay, stay.Id, lineRef), "Boarding charges re-derived", ct);

        var taxClassId = stay.RateCardSnapshot?.TaxClassId ?? throw new DomainRuleViolationException(BusinessViolationCodes.InvalidStayTransition, "This stay has no rate card to bill.", "RateCard");
        var catId = stay.Cats.First().CatId;
        foreach (var planned in plan.Record)
            await recorder.RecordAsync(BoardingChargePlanner.ToRequest(planned, stay.Id, stay.CustomerId, stay.BranchId, stay.ArrivalOn, stay.CurrencyCode, taxClassId, catId), ct);

        return plan;
    }
}

/// <summary>
/// The shared preparation of an extension (also used by relocation with a new departure): the window, the unit free for the extra range, the cats free of other stays, the re-quote with the
/// stay's OWN snapshots, and the gate as of the new departure. Returns the failure to answer, or the DTO for <see cref="BoardingStayAggregate.Extend"/>.
/// </summary>
public static class StayExtension
{
    public sealed record Prepared(ExtendDto? Dto, CommandResponse? Failure, BoardingInputSet? Inputs);

    public static async Task<Prepared> PrepareAsync(
        BoardingStayAggregate stay, DateOnly newDepartureOn, Guid? relocatedToUnitId, DateTimeOffset relocatedFrom, IBoardingInputs boardingInputs, IBoardingReferenceReader referenceReader,
        IAggregateRootRepository<CatAggregate> catRepository, DateTimeOffset now, Platform.Infrastructure.Common.Security.UserContext userContext, Guid correlationId)
    {
        if (newDepartureOn <= stay.DepartureOn)
            return new Prepared(null, Violation(BusinessViolationCodes.InvalidStayDates, "NewDepartureOn", "The new departure must be after the current departure."), null);

        var inputs = await boardingInputs.LoadAsync(stay.BranchId, stay.ArrivalOn, newDepartureOn, stay.Id, now);
        if (inputs is null) return new Prepared(null, Violation(BusinessViolationCodes.BranchNotFound, "BranchId", "Branch not found."), null);

        var s = inputs.Settings;
        var window = BoardingOccupancyWindow.Compute(stay.ArrivalOn, newDepartureOn, s.TimeZoneId, s.CheckInWindow, s.CheckOutWindow, s.TurnaroundHours);
        var current = stay.CurrentAllocation;
        if (current is null) return new Prepared(null, Violation(BusinessViolationCodes.InvalidStayTransition, nameof(stay.Status), "This stay holds no unit."), inputs);

        var unitId = relocatedToUnitId ?? current.UnitId;
        var extra = new OccupancyRange(relocatedToUnitId is null ? current.OccupiedTo : relocatedFrom, window.To);
        var busy = inputs.BusyUnits.TryGetValue(unitId, out var ranges) && ranges.Any(r => r.Overlaps(extra));
        if (busy)
        {
            var options = RelocationOptions.Find(inputs, current.UnitType, stay.Cats.Count, new OccupancyRange(MaxOf(now, stay.OccupiedFrom), window.To), current.UnitId);
            var hint = options.Count == 0 ? "No other unit is free for the rest of the stay." : "Units free for the rest of the stay: " + string.Join(", ", options.Select(o => o.Code)) + ".";
            return new Prepared(null, Violation(BusinessViolationCodes.UnitNotFreeForExtension, "NewDepartureOn", $"The unit is booked after {stay.DepartureOn:yyyy-MM-dd}. {hint}"), inputs);
        }

        var overlaps = await boardingInputs.FindCatOverlapsAsync(stay.Cats.Select(c => c.CatId).ToList(), extra, stay.Id);
        if (overlaps.Count > 0)
            return new Prepared(null, Violation(BusinessViolationCodes.CatAlreadyBoarding, "CatIds", BoardingMessages.CatAlreadyBoarding), inputs);

        var catIds = stay.Cats.Select(c => c.CatId).ToList();
        var (cats, failure) = await StayCatGate.LoadCatsAsync(catRepository, catIds, stay.CustomerId);
        if (failure is not null) return new Prepared(null, failure, inputs);

        var gates = StayCatGate.Evaluate(cats!, await referenceReader.GetActiveCoreVaccinesAsync(), newDepartureOn, s.VaccinationDueSoonDays);
        var quote = BoardingQuoteCalculator.Quote(StayDateRules.Nights(stay.ArrivalOn, newDepartureOn), stay.Cats.Count, stay.RateCardSnapshot!, stay.PricingSnapshot!);
        var today = BoardingOccupancyWindow.LocalDateOf(now, s.TimeZoneId);

        return new Prepared(new ExtendDto(newDepartureOn, window, quote, gates, s.MaxStayNights, today, now, userContext, correlationId), null, inputs);
    }

    private static DateTimeOffset MaxOf(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;
}

/// <summary>C21 -- US-BRD-15 AC1/AC2. A stale version is a 409. Free: the allocation, the quote and the charges follow. Not free: <c>UnitNotFreeForExtension</c> naming the units that are.</summary>
public sealed class ExtendBoardingStayCommandHandler(
    IAggregateRootRepository<BoardingStayAggregate> stayRepository,
    IAggregateRootRepository<CatAggregate> catRepository,
    IBoardingInputs boardingInputs,
    IBoardingReferenceReader referenceReader,
    IBoardingChargeLedger chargeLedger,
    IBillableChargeRecorder chargeRecorder,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<ExtendBoardingStayCommand>
{
    public async Task<CommandResponse> HandleAsync(ExtendBoardingStayCommand command)
    {
        var stay = await StayAccess.LoadAsync(stayRepository, command.StayId, null, command.BranchId);
        if (stay is null) return StayAccess.NotFound();
        if (stay.Version != command.ExpectedVersion)
            return Violation(BusinessViolationCodes.StaleStayVersion, nameof(command.ExpectedVersion), "This stay has changed since you loaded it. Reload and try again.");
        if (stay.Status != BoardingStayStatus.Confirmed && stay.Status != BoardingStayStatus.CheckedIn)
            return Violation(BusinessViolationCodes.InvalidStayTransition, nameof(stay.Status), BoardingMessages.InvalidTransition(stay.Status.DisplayName, "extended"));

        var now = timeProvider.GetUtcNow();
        var prepared = await StayExtension.PrepareAsync(stay, command.NewDepartureOn, null, now, boardingInputs, referenceReader, catRepository, now, command.UserContext!, command.CorrelationId);
        if (prepared.Failure is not null) return prepared.Failure;

        try
        {
            stay.Extend(prepared.Dto!);
            if (stay.Status == BoardingStayStatus.CheckedIn)
            {
                var plan = await BoardingCharges.SyncAsync(stay, chargeLedger, chargeRecorder);
                stay.ApplyChargePlan(plan.Revision, plan.CreditDue, command.CorrelationId, command.UserContext!);
            }
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await stayRepository.UpdateAsync(stay, EventPublishOption.InMemoryPublishOnly);
        return Success(stay.Id);
    }
}

/// <summary>C21 -- US-BRD-15 AC2. Moves the stay to another unit from an instant, for a recorded reason, and extends it in the same command when a new departure is given (one aggregate).</summary>
public sealed class RelocateBoardingStayCommandHandler(
    IAggregateRootRepository<BoardingStayAggregate> stayRepository,
    IAggregateRootRepository<CatAggregate> catRepository,
    IBoardingInputs boardingInputs,
    IBoardingReferenceReader referenceReader,
    IBoardingChargeLedger chargeLedger,
    IBillableChargeRecorder chargeRecorder,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RelocateBoardingStayCommand>
{
    public async Task<CommandResponse> HandleAsync(RelocateBoardingStayCommand command)
    {
        var stay = await StayAccess.LoadAsync(stayRepository, command.StayId, null, command.BranchId);
        if (stay is null) return StayAccess.NotFound();
        if (stay.Version != command.ExpectedVersion)
            return Violation(BusinessViolationCodes.StaleStayVersion, nameof(command.ExpectedVersion), "This stay has changed since you loaded it. Reload and try again.");
        if (stay.Status != BoardingStayStatus.Confirmed && stay.Status != BoardingStayStatus.CheckedIn)
            return Violation(BusinessViolationCodes.InvalidStayTransition, nameof(stay.Status), BoardingMessages.InvalidTransition(stay.Status.DisplayName, "relocated"));

        var now = timeProvider.GetUtcNow();
        var newDeparture = command.NewDepartureOn ?? stay.DepartureOn;
        var inputs = await boardingInputs.LoadAsync(stay.BranchId, stay.ArrivalOn, newDeparture, stay.Id, now);
        if (inputs is null) return Violation(BusinessViolationCodes.BranchNotFound, "BranchId", "Branch not found.");

        var s = inputs.Settings;
        var target = inputs.Units.FirstOrDefault(u => u.UnitId == command.TargetUnitId && u.IsActive);
        var current = stay.CurrentAllocation;
        if (target is null || current is null) return Violation(BusinessViolationCodes.BoardingUnitNotFound, nameof(command.TargetUnitId), "Unit not found.");

        var allowedType = target.Type == current.UnitType || (stay.RequiresIsolation && target.Type == BoardingUnitType.Isolation);
        if (!allowedType)
            return Violation(BusinessViolationCodes.UnitNoLongerAvailable, nameof(command.TargetUnitId), $"That unit is not a {current.UnitType.DisplayName} unit, which is what the stay was booked and priced as.");

        var window = BoardingOccupancyWindow.Compute(stay.ArrivalOn, newDeparture, s.TimeZoneId, s.CheckInWindow, s.CheckOutWindow, s.TurnaroundHours);
        var requested = BoardingOccupancyWindow.InstantOf(command.EffectiveFromLocalDate, TimeOnly.MinValue, s.TimeZoneId);
        var fromInstant = requested > now ? requested : now;
        var remaining = new OccupancyRange(fromInstant, window.To);
        if (inputs.BusyUnits.TryGetValue(target.UnitId, out var busy) && busy.Any(r => r.Overlaps(remaining)))
            return Violation(BusinessViolationCodes.UnitNoLongerAvailable, nameof(command.TargetUnitId), BoardingMessages.UnitNoLongerAvailable);

        try
        {
            stay.Relocate(new RelocateDto(new AllocatedUnit(target.UnitId, target.Type, target.CapacityCats), fromInstant, command.Reason, now, command.UserContext!, command.CorrelationId));

            if (newDeparture > stay.DepartureOn)
            {
                var prepared = await StayExtension.PrepareAsync(stay, newDeparture, target.UnitId, fromInstant, boardingInputs, referenceReader, catRepository, now, command.UserContext!, command.CorrelationId);
                if (prepared.Failure is not null) return prepared.Failure;

                stay.Extend(prepared.Dto!);
                if (stay.Status == BoardingStayStatus.CheckedIn)
                {
                    var plan = await BoardingCharges.SyncAsync(stay, chargeLedger, chargeRecorder);
                    stay.ApplyChargePlan(plan.Revision, plan.CreditDue, command.CorrelationId, command.UserContext!);
                }
            }
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await stayRepository.UpdateAsync(stay, EventPublishOption.InMemoryPublishOnly);
        return Success(stay.Id);
    }
}

/// <summary>
/// C22 -- US-BRD-16 / US-BRD-17. Checks the cats out against the checklist. Before the booked departure the stay is re-quoted for the nights charged (basis recalculated) and the unit is freed at the
/// check-out instant plus the turnaround; the stay's charges are re-derived (an invoiced charge that no longer applies becomes a credit owed). The message <c>boarding.checked-out</c> is enqueued
/// before the save; the cat's weight reaches HO-CAT-001's history through an event handler.
/// </summary>
public sealed class CheckOutBoardingStayCommandHandler(
    IAggregateRootRepository<BoardingStayAggregate> stayRepository,
    IBoardingInputs boardingInputs,
    IBoardingChargeLedger chargeLedger,
    IBillableChargeRecorder chargeRecorder,
    INotificationOutbox notificationOutbox,
    INotificationLinkBuilder linkBuilder,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<CheckOutBoardingStayCommand>
{
    public async Task<CommandResponse> HandleAsync(CheckOutBoardingStayCommand command)
    {
        var stay = await StayAccess.LoadAsync(stayRepository, command.StayId, null, command.BranchId);
        if (stay is null) return StayAccess.NotFound();

        var settings = await boardingInputs.GetSettingsAsync(stay.BranchId);
        if (settings is null) return Violation(BusinessViolationCodes.BranchNotFound, "BranchId", "Branch not found.");

        var now = timeProvider.GetUtcNow();
        var today = BoardingOccupancyWindow.LocalDateOf(now, settings.TimeZoneId);

        var cats = command.Cats.Select(c => new CheckOutCatDto(
            c.CatId, c.WeightKg, c.Belongings.Select(b => new BelongingReturnInput(b.BelongingId, b.Returned, b.Note)).ToList(),
            c.Signature is null ? null : new SignatureCapture(c.Signature.SignerName, BoardingUnitMapper.ParseEnum<SignatureMethod>(c.Signature.Method), c.Signature.Strokes, c.Signature.ConfirmedStatement, now))).ToList();

        var outcome = EarlyCheckout.Compute(stay, today, now, settings.TurnaroundHours);

        try
        {
            stay.CheckOut(new CheckOutDto(cats, today, now, outcome.Window, outcome.Quote, outcome.ChargedDepartureOn, command.UserContext!, command.CorrelationId));
            var plan = await BoardingCharges.SyncAsync(stay, chargeLedger, chargeRecorder);
            stay.ApplyChargePlan(plan.Revision, plan.CreditDue, command.CorrelationId, command.UserContext!);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        BoardingNotifications.EnqueueCheckedOut(notificationOutbox, linkBuilder, stay, command.CorrelationId, command.UserContext!);
        await stayRepository.UpdateAsync(stay, EventPublishOption.InMemoryPublishOnly);
        return Success(stay.Id);
    }
}

/// <summary>C23 -- staff have dealt with a flagged stay (a cat died or changed owner). The note says how; nothing is sent.</summary>
public sealed class ClearStayAttentionCommandHandler(IAggregateRootRepository<BoardingStayAggregate> stayRepository, TimeProvider timeProvider)
    : ICommandHandlerAsync<ClearStayAttentionCommand>
{
    public async Task<CommandResponse> HandleAsync(ClearStayAttentionCommand command)
    {
        var stay = await StayAccess.LoadAsync(stayRepository, command.StayId, null, command.BranchId);
        if (stay is null) return StayAccess.NotFound();

        try
        {
            stay.ClearAttention(command.Note, timeProvider.GetUtcNow(), command.UserContext!.UserId);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await stayRepository.UpdateAsync(stay, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>
/// US-BRD-20 -- one cycle (or the final) invoice of a stay, issued through HO-BIL-001's published <see cref="IssueInvoiceFromChargesCommand"/> with a billing-group selector. A cycle is keyed
/// (<c>boarding-{stay}-c{k}</c>) so a replay returns the same invoice; the FINAL invoice (cycle 0, the stay is checked out) covers every remaining uninvoiced charge. Nothing remaining to
/// invoice is a success with nothing issued. A refusal from Billing (a missing billing profile) is returned, not thrown, and the worker retries on its next pass.
/// </summary>
public sealed class InvoiceBoardingCycleCommandHandler(
    IAggregateRootRepository<BoardingStayAggregate> stayRepository, IBoardingChargeLedger chargeLedger, IInMemoryDispatcher dispatcher)
    : ICommandHandlerAsync<InvoiceBoardingCycleCommand>
{
    public async Task<CommandResponse> HandleAsync(InvoiceBoardingCycleCommand command)
    {
        var stay = await stayRepository.GetByIdAsync(command.StayId);
        if (stay is null || (stay.Status != BoardingStayStatus.CheckedIn && stay.Status != BoardingStayStatus.CheckedOut)) return Success();

        var isFinal = command.Cycle == 0;
        string key;
        if (isFinal)
        {
            // Deterministic per set of remaining charges: a replay of the same final invoice returns the same invoice, a late clinic charge (a new id) is a new key. Nothing left -> nothing to do.
            var remaining = (await chargeLedger.GetRowsAsync(stay.Id)).Where(r => !r.IsVoided && !r.IsBilled).Select(r => r.ChargeId).OrderBy(id => id).ToList();
            if (remaining.Count == 0) return Success();

            key = $"boarding-{stay.Id:N}-final-{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join(',', remaining))))[..16].ToLowerInvariant()}";
        }
        else
        {
            key = $"boarding-{stay.Id:N}-c{command.Cycle}";
        }

        var issued = await dispatcher.SendAsync(new IssueInvoiceFromChargesCommand
        {
            InvoiceId = Guid.NewGuid(), CustomerId = stay.CustomerId, BranchId = stay.BranchId,
            Selector = new InvoiceChargeSelector(BillingGroup: new BillingGroupChargeSelector(BillingGroupKind.BoardingStay.DisplayName, stay.Id, isFinal ? null : command.ServiceDateTo)),
            IdempotencyKey = key, UserContext = command.UserContext, CorrelationId = command.CorrelationId, ValidateCommand = true
        });

        return issued.IsSuccess ? Success(issued.Result) : new CommandResponse { IsSuccess = false, ValidationResult = issued.ValidationResult };
    }
}
