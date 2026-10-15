using System.Globalization;
using CatEcommerce.Application.Commands.Boarding;
using CatEcommerce.Application.Commands.Notification;
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

/// <summary>Shared by the waitlist handlers: what the availability finder says about a range right now.</summary>
public static class WaitlistAvailability
{
    /// <summary>The number of free units for the range among the types the entry accepts (all online-bookable types when none is named), or the violation to answer.</summary>
    public static async Task<(int Free, string? Code, string? Message)> FreeUnitsAsync(
        IBoardingInputs boardingInputs, Guid branchId, DateOnly arrivalOn, DateOnly departureOn, int catCount, BoardingUnitType? unitType, DateTimeOffset now)
    {
        var inputs = await boardingInputs.LoadAsync(branchId, arrivalOn, departureOn, null, now);
        if (inputs is null) return (0, BusinessViolationCodes.BranchNotFound, "Branch not found.");

        var s = inputs.Settings;
        var result = BoardingAvailabilityFinder.Find(new AvailabilityRequest(
            arrivalOn, departureOn, catCount, now, s.TimeZoneId, s.CheckInWindow, s.CheckOutWindow, s.TurnaroundHours, s.MaxStayNights, s.BookingHorizonDays, s.AlternativeSearchDays,
            inputs.Units, inputs.BusyUnits, false, AvailabilityMode.Online, inputs.RateCards, s.Pricing));

        if (result.Violation is not null) return (0, result.Violation.Code, result.Violation.Message);

        var free = result.UnitTypes.Where(t => unitType is null || t.UnitType == unitType).Sum(t => t.FreeUnitCount);
        return (free, null, null);
    }
}

/// <summary>C24 -- US-BRD-02 AC3. The customer joins the waitlist only when the finder shows nothing free (<c>UnitsStillAvailable</c> otherwise); one live entry per range.</summary>
public sealed class JoinBoardingWaitlistCommandHandler(
    IAggregateRootRepository<BoardingWaitlistEntryAggregate> entryRepository, IBoardingInputs boardingInputs, IBoardingWaitlistDaycareFinder finder, TimeProvider timeProvider)
    : ICommandHandlerAsync<JoinBoardingWaitlistCommand>
{
    public async Task<CommandResponse> HandleAsync(JoinBoardingWaitlistCommand command)
    {
        var now = timeProvider.GetUtcNow();
        var settings = await boardingInputs.GetSettingsAsync(command.BranchId);
        if (settings is null) return Violation(BusinessViolationCodes.BranchNotFound, nameof(command.BranchId), "Branch not found.", nameof(BoardingWaitlistEntryAggregate));

        if (await entryRepository.ExistAsync(e => e.CustomerId == command.CustomerId && e.ArrivalOn == command.ArrivalOn && e.DepartureOn == command.DepartureOn
                                                  && (e.Status == BoardingWaitlistStatus.Waiting || e.Status == BoardingWaitlistStatus.Notified)))
            return Violation(BusinessViolationCodes.AlreadyWaitlisted, nameof(command.ArrivalOn), BoardingMessages.AlreadyWaitlisted, nameof(BoardingWaitlistEntryAggregate));

        if (await finder.CountLiveWaitlistAsync(command.CustomerId) >= StayRules.MaxLiveWaitlistEntriesPerCustomer)
            return Violation(BusinessViolationCodes.WaitlistLimitReached, nameof(command.ArrivalOn), $"You can be on at most {StayRules.MaxLiveWaitlistEntriesPerCustomer} waitlists at once. Withdraw one first.", nameof(BoardingWaitlistEntryAggregate));

        var unitType = command.UnitType is null ? null : BoardingUnitMapper.ParseEnum<BoardingUnitType>(command.UnitType);
        var (free, code, message) = await WaitlistAvailability.FreeUnitsAsync(boardingInputs, command.BranchId, command.ArrivalOn, command.DepartureOn, command.CatCount, unitType, now);
        if (code is not null) return Violation(code, nameof(command.ArrivalOn), message!, nameof(BoardingWaitlistEntryAggregate));
        if (free > 0) return Violation(BusinessViolationCodes.UnitsStillAvailable, nameof(command.ArrivalOn), "Units are still available for these dates. Book now instead.", nameof(BoardingWaitlistEntryAggregate));

        var entry = new BoardingWaitlistEntryAggregate(command.EntryId);
        try
        {
            entry.Join(command.BranchId, command.CustomerId, command.ArrivalOn, command.DepartureOn, command.CatCount, unitType, BoardingOccupancyWindow.LocalDateOf(now, settings.TimeZoneId), now, command.UserContext!);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(BoardingWaitlistEntryAggregate));
        }

        await entryRepository.SaveAsync(entry, EventPublishOption.InMemoryPublishOnly);
        return Success(entry.Id);
    }
}

public sealed class WithdrawBoardingWaitlistCommandHandler(IAggregateRootRepository<BoardingWaitlistEntryAggregate> entryRepository, TimeProvider timeProvider)
    : ICommandHandlerAsync<WithdrawBoardingWaitlistCommand>
{
    public async Task<CommandResponse> HandleAsync(WithdrawBoardingWaitlistCommand command)
    {
        var entry = await entryRepository.GetByIdAsync(command.EntryId);
        if (entry is null || entry.CustomerId != command.CustomerId)
            return Violation(BusinessViolationCodes.WaitlistEntryNotFound, nameof(command.EntryId), "Waitlist entry not found.", nameof(BoardingWaitlistEntryAggregate));

        entry.Withdraw(timeProvider.GetUtcNow());
        await entryRepository.UpdateAsync(entry, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>
/// C24 (worker) -- the oldest waiting entry whose range now has a free unit is told, once, with a booking link. NO HOLD: the first to book wins (ADR-235). A unit is not announced twice: the free
/// units must exceed the owners already notified for an overlapping range plus the older entries still waiting for one. Quiet hours are the notification service's (a Normal message is deferred).
/// </summary>
public sealed class NotifyWaitlistEntryCommandHandler(
    IAggregateRootRepository<BoardingWaitlistEntryAggregate> entryRepository, IBoardingInputs boardingInputs, IBoardingWaitlistDaycareFinder finder,
    INotificationOutbox notificationOutbox, INotificationLinkBuilder linkBuilder, TimeProvider timeProvider)
    : ICommandHandlerAsync<NotifyWaitlistEntryCommand>
{
    public async Task<CommandResponse> HandleAsync(NotifyWaitlistEntryCommand command)
    {
        var entry = await entryRepository.GetByIdAsync(command.EntryId);
        if (entry is null || entry.Status != BoardingWaitlistStatus.Waiting) return Success();

        var now = timeProvider.GetUtcNow();
        var (free, code, _) = await WaitlistAvailability.FreeUnitsAsync(boardingInputs, entry.BranchId, entry.ArrivalOn, entry.DepartureOn, entry.CatCount, entry.UnitType, now);
        if (code is not null || free <= 0) return Success();

        var claimed = await finder.CountNotifiedOverlappingAsync(entry.BranchId, entry.ArrivalOn, entry.DepartureOn, entry.Id)
            + await finder.CountOlderWaitingOverlappingAsync(entry.BranchId, entry.ArrivalOn, entry.DepartureOn, entry.JoinedOn);
        if (free <= claimed) return Success();

        entry.MarkNotified(now);
        BoardingNotifications.EnqueueWaitlistAvailable(notificationOutbox, linkBuilder, entry, command.CorrelationId, command.UserContext!);
        await entryRepository.UpdateAsync(entry, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

public sealed class CloseStaleBoardingWaitlistCommandHandler(
    IAggregateRootRepository<BoardingWaitlistEntryAggregate> entryRepository, IBoardingInputs boardingInputs, TimeProvider timeProvider)
    : ICommandHandlerAsync<CloseStaleBoardingWaitlistCommand>
{
    public async Task<CommandResponse> HandleAsync(CloseStaleBoardingWaitlistCommand command)
    {
        var entry = await entryRepository.GetByIdAsync(command.EntryId);
        if (entry is null) return Success();

        var now = timeProvider.GetUtcNow();
        var zone = (await boardingInputs.GetSettingsAsync(entry.BranchId))?.TimeZoneId ?? "UTC";
        if (!entry.CloseIfStale(BoardingOccupancyWindow.LocalDateOf(now, zone), now)) return Success();

        await entryRepository.UpdateAsync(entry, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>C3 -- the daycare price list of a branch, effective-dated and append-only like the boarding rate card.</summary>
public sealed class SetDaycareRateCardCommandHandler(
    IAggregateRootRepository<DaycareRateCardAggregate> rateCardRepository, IBoardingReferenceReader referenceReader, IBusinessSettings businessSettings, TimeProvider timeProvider)
    : ICommandHandlerAsync<SetDaycareRateCardCommand>
{
    public async Task<CommandResponse> HandleAsync(SetDaycareRateCardCommand command)
    {
        var branch = await referenceReader.GetBranchAsync(command.BranchId);
        if (branch is not { IsActive: true })
            return Violation(BusinessViolationCodes.BranchNotFound, nameof(command.BranchId), "Branch not found or inactive.", nameof(DaycareRateCardAggregate));
        if (!await referenceReader.TaxClassIsActiveAsync(command.TaxClassId))
            return Violation(BusinessViolationCodes.TaxClassNotFound, nameof(command.TaxClassId), "Tax class not found or inactive.", nameof(DaycareRateCardAggregate));
        if (await rateCardRepository.ExistAsync(c => c.BranchId == command.BranchId && c.EffectiveFrom == command.EffectiveFrom))
            return Violation(BusinessViolationCodes.DuplicateRateCardEffectiveDate, nameof(command.EffectiveFrom), BoardingMessages.DuplicateRateCardEffectiveDate, nameof(DaycareRateCardAggregate));

        var card = new DaycareRateCardAggregate(command.RateCardId);
        try
        {
            card.Create(command.BranchId, command.HourlyRate, command.DayRate, command.TaxClassId, businessSettings.GetString(SettingKeys.CommerceCurrencyCode), command.EffectiveFrom, timeProvider.GetUtcNow(), command.UserContext!);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(DaycareRateCardAggregate));
        }

        await rateCardRepository.SaveAsync(card, EventPublishOption.InMemoryPublishOnly);
        return Success(card.Id);
    }
}

/// <summary>
/// C25 -- US-BRD-21. Books a cat in for a day. Daycare never allocates a unit: capacity is <c>boarding.daycareDailyCapacity</c> visits per branch per day (0 = not offered), the next free slot number
/// is taken, and a race for the last slot loses on <c>ix_daycare_slot</c> (<c>409 DaycareFull</c>). The vaccination gate as of the visit day applies (OQ-12).
/// </summary>
public sealed class BookDaycareVisitCommandHandler(
    IAggregateRootRepository<DaycareVisitAggregate> visitRepository, IAggregateRootRepository<DaycareRateCardAggregate> rateCardRepository,
    IAggregateRootRepository<CatAggregate> catRepository, IBoardingWaitlistDaycareFinder finder, IBoardingInputs boardingInputs, IBoardingReferenceReader referenceReader,
    IBusinessSettings businessSettings, TimeProvider timeProvider, IFreshScopeExecutor scopes, IUniqueViolationDetector uniqueViolation)
    : ICommandHandlerAsync<BookDaycareVisitCommand>
{
    // Backlog row 50: the slot is the lowest free number; a parallel booking that loses ix_daycare_slot re-picks in a fresh scope (bounded), so DaycareFull means the day is really full.
    public Task<CommandResponse> HandleAsync(BookDaycareVisitCommand command) =>
        ClaimRetry.RunAsync(this, command, (h, c) => h.AttemptAsync(c), scopes, uniqueViolation.IsOccupancyClaimLost,
            () => Violation(BusinessViolationCodes.DaycareFull, nameof(command.LocalDate), BoardingMessages.DaycareFull, nameof(DaycareVisitAggregate)));

    public async Task<CommandResponse> AttemptAsync(BookDaycareVisitCommand command)
    {
        var settings = await boardingInputs.GetSettingsAsync(command.BranchId);
        if (settings is null) return Violation(BusinessViolationCodes.BranchNotFound, nameof(command.BranchId), "Branch not found.", nameof(DaycareVisitAggregate));

        var capacity = businessSettings.GetInt(SettingKeys.BoardingDaycareDailyCapacity, command.BranchId);
        if (capacity <= 0)
            return Violation(BusinessViolationCodes.DaycareNotOffered, nameof(command.BranchId), "Daycare is not offered at this branch.", nameof(DaycareVisitAggregate));

        var now = timeProvider.GetUtcNow();
        var todayLocal = BoardingOccupancyWindow.LocalDateOf(now, settings.TimeZoneId);

        // SECURITY 2026-10-15: no booking beyond the branch booking horizon, and a cap on a customer's future visits (daycare takes no deposit).
        if (command.LocalDate > todayLocal.AddDays(settings.BookingHorizonDays))
            return Violation(BusinessViolationCodes.StayBeyondHorizon, nameof(command.LocalDate), $"Daycare can be booked up to {settings.BookingHorizonDays} days ahead.", nameof(DaycareVisitAggregate));
        if (await finder.CountFutureDaycareVisitsAsync(command.CustomerId, todayLocal) >= StayRules.MaxFutureDaycareVisitsPerCustomer)
            return Violation(BusinessViolationCodes.DaycareLimitReached, nameof(command.LocalDate), $"You can hold at most {StayRules.MaxFutureDaycareVisitsPerCustomer} upcoming daycare visits.", nameof(DaycareVisitAggregate));

        var cardId = await finder.FindDaycareRateCardInForceAsync(command.BranchId, now);
        var card = cardId is null ? null : await rateCardRepository.GetByIdAsync(cardId.Value);
        if (card is null)
            return Violation(BusinessViolationCodes.DaycareNotOffered, nameof(command.BranchId), "Daycare is not priced at this branch yet.", nameof(DaycareVisitAggregate));

        var (cats, failure) = await StayCatGate.LoadCatsAsync(catRepository, [command.CatId], command.CustomerId);
        if (failure is not null) return failure;

        var gates = StayCatGate.Evaluate(cats!, await referenceReader.GetActiveCoreVaccinesAsync(), command.LocalDate, settings.VaccinationDueSoonDays);
        if (gates.Values.Any(g => !g.Passed))
            return Violation(BusinessViolationCodes.VaccinationGateBlocked, nameof(command.CatId), BoardingMessages.VaccinationGateBlocked, nameof(DaycareVisitAggregate));

        if (await finder.CatHasDaycareVisitAsync(command.CatId, command.LocalDate))
            return Violation(BusinessViolationCodes.CatAlreadyBoarding, nameof(command.CatId), "This cat already has a daycare visit that day.", nameof(DaycareVisitAggregate));

        var taken = await finder.GetTakenDaycareSlotsAsync(command.BranchId, command.LocalDate);
        var slot = Enumerable.Range(1, capacity).FirstOrDefault(n => !taken.Contains(n));
        if (slot == 0) return Violation(BusinessViolationCodes.DaycareFull, nameof(command.LocalDate), BoardingMessages.DaycareFull, nameof(DaycareVisitAggregate));

        var visit = new DaycareVisitAggregate(command.VisitId);
        try
        {
            visit.Book(
                command.BranchId, command.CustomerId, command.CatId, cats![0].Name, command.LocalDate, slot, BoardingUnitMapper.ParseEnum<DaycareBasis>(command.Basis), command.ExpectedHours, card,
                BoardingOccupancyWindow.LocalDateOf(now, settings.TimeZoneId), now, command.UserContext!);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(DaycareVisitAggregate));
        }

        await visitRepository.SaveAsync(visit, EventPublishOption.InMemoryPublishOnly);
        return Success(visit.Id);
    }
}

public static class DaycareAccess
{
    public static async Task<DaycareVisitAggregate?> LoadAsync(IAggregateRootRepository<DaycareVisitAggregate> repository, Guid visitId, Guid? customerId, Guid? branchId)
    {
        var visit = await repository.GetByIdAsync(visitId);
        if (visit is null) return null;
        if (customerId is { } customer && visit.CustomerId != customer) return null;
        if (branchId is { } branch && visit.BranchId != branch) return null;
        return customerId is null && branchId is null ? null : visit;
    }

    public static CommandResponse NotFound() => Violation(BusinessViolationCodes.DaycareVisitNotFound, "VisitId", "Daycare visit not found.", nameof(DaycareVisitAggregate));
}

public sealed class CheckInDaycareVisitCommandHandler(IAggregateRootRepository<DaycareVisitAggregate> visitRepository, IBoardingInputs boardingInputs, TimeProvider timeProvider)
    : ICommandHandlerAsync<CheckInDaycareVisitCommand>
{
    public async Task<CommandResponse> HandleAsync(CheckInDaycareVisitCommand command)
    {
        var visit = await DaycareAccess.LoadAsync(visitRepository, command.VisitId, null, command.BranchId);
        if (visit is null) return DaycareAccess.NotFound();

        var now = timeProvider.GetUtcNow();
        var zone = (await boardingInputs.GetSettingsAsync(visit.BranchId))?.TimeZoneId ?? "UTC";
        try
        {
            visit.CheckIn(BoardingOccupancyWindow.LocalDateOf(now, zone), now);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(DaycareVisitAggregate));
        }

        await visitRepository.UpdateAsync(visit, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>C25 -- the visit is charged through Billing as source <c>DaycareVisit</c>, in the same save as the check-out (the producer rules of HO-BIL-001 section 8.4).</summary>
public sealed class CheckOutDaycareVisitCommandHandler(
    IAggregateRootRepository<DaycareVisitAggregate> visitRepository, IBillableChargeRecorder chargeRecorder, TimeProvider timeProvider)
    : ICommandHandlerAsync<CheckOutDaycareVisitCommand>
{
    public async Task<CommandResponse> HandleAsync(CheckOutDaycareVisitCommand command)
    {
        var visit = await DaycareAccess.LoadAsync(visitRepository, command.VisitId, null, command.BranchId);
        if (visit is null) return DaycareAccess.NotFound();

        try
        {
            visit.CheckOut(timeProvider.GetUtcNow(), command.UserContext!, command.CorrelationId);
            await chargeRecorder.RecordAsync(new ChargeRequestDto(
                visit.CustomerId, visit.BranchId, visit.CatId, BusinessLine.Boarding, ItemKind.Service, ChargeSourceKind.DaycareVisit, visit.Id, "main", null, null,
                $"Daycare - {visit.CatName} ({visit.LocalDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)})", 1m, new Money(visit.ChargeAmount!.Value, visit.CurrencyCode),
                Money.Zero(visit.CurrencyCode), visit.TaxClassId, visit.LocalDate));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(DaycareVisitAggregate));
        }

        await visitRepository.UpdateAsync(visit, EventPublishOption.InMemoryPublishOnly);
        return Success(visit.ChargeAmount!.Value);
    }
}

public sealed class CancelDaycareVisitCommandHandler(IAggregateRootRepository<DaycareVisitAggregate> visitRepository) : ICommandHandlerAsync<CancelDaycareVisitCommand>
{
    public async Task<CommandResponse> HandleAsync(CancelDaycareVisitCommand command)
    {
        var visit = await DaycareAccess.LoadAsync(visitRepository, command.VisitId, command.CustomerId, command.BranchId);
        if (visit is null) return DaycareAccess.NotFound();

        try
        {
            visit.Cancel();
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(DaycareVisitAggregate));
        }

        await visitRepository.UpdateAsync(visit, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

public sealed class RecordDaycareNoShowCommandHandler(IAggregateRootRepository<DaycareVisitAggregate> visitRepository, IBoardingInputs boardingInputs, TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordDaycareNoShowCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordDaycareNoShowCommand command)
    {
        var visit = await DaycareAccess.LoadAsync(visitRepository, command.VisitId, null, command.BranchId);
        if (visit is null) return DaycareAccess.NotFound();

        var zone = (await boardingInputs.GetSettingsAsync(visit.BranchId))?.TimeZoneId ?? "UTC";
        try
        {
            visit.RecordNoShow(BoardingOccupancyWindow.LocalDateOf(timeProvider.GetUtcNow(), zone));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(DaycareVisitAggregate));
        }

        await visitRepository.UpdateAsync(visit, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>
/// The stay's daily update (US-BRD-12), requested through HO-NTF-001's <c>RequestNotificationCommand</c>: deduplicated per stay and local day, and guarded by <c>BoardingStayInHouse</c> so a stay
/// checked out at 17:00 gets no 18:00 update. The link opens the diary behind sign-in; nothing from the diary is in the message.
/// </summary>
public sealed class SendBoardingDailyUpdateCommandHandler(
    IAggregateRootRepository<BoardingStayAggregate> stayRepository, INotificationLinkBuilder linkBuilder, IInMemoryDispatcher dispatcher)
    : ICommandHandlerAsync<SendBoardingDailyUpdateCommand>
{
    public async Task<CommandResponse> HandleAsync(SendBoardingDailyUpdateCommand command)
    {
        var stay = await stayRepository.GetByIdAsync(command.StayId);
        if (stay is null || stay.Status != BoardingStayStatus.CheckedIn) return Success();

        var date = command.LocalDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var response = await dispatcher.SendAsync(new RequestNotificationCommand
        {
            RequestId = Guid.NewGuid(), TemplateKey = NotificationTemplateCatalogue.BoardingDailyUpdate, RecipientKind = NotificationRecipientKind.Customer.DisplayName, RecipientId = stay.CustomerId,
            Variables = new Dictionary<string, string> { ["catNames"] = BoardingNotifications.CatNames(stay), ["date"] = date, ["diaryLink"] = linkBuilder.BoardingDiaryLink(stay.Id) },
            SubjectKind = NotificationSubjectKind.BoardingStayInHouse.DisplayName, SubjectId = stay.Id, BranchId = stay.BranchId,
            DeduplicationKey = $"boarding.daily-update:{stay.Id:N}:{date}", UserContext = command.UserContext, CorrelationId = command.CorrelationId, ValidateCommand = true
        });

        return response.IsSuccess ? Success() : new CommandResponse { IsSuccess = false, ValidationResult = response.ValidationResult };
    }
}
