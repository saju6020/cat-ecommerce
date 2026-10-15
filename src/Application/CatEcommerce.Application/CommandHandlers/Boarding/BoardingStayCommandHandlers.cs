using CatEcommerce.Application.Commands.Boarding;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Boarding;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Boarding.BoardingHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Boarding;

/// <summary>One cat's gate outcome as the enquiry/reserve response reports it; <see cref="BookingLink"/> is HO-APT-001's vaccination booking link.</summary>
public sealed record GateCatResult(Guid CatId, string CatName, GateEvaluation Evaluation, string? BookingLink);

public sealed record EnquiryResult(Guid StayId, IReadOnlyList<GateCatResult> Cats, BoardingQuote? Quote, string CurrencyCode);

public sealed record ReserveResult(Guid StayId, decimal DepositAmount, string CurrencyCode, DateTimeOffset? PayBy);

/// <summary>Resolves a stay by id AND caller: a customer sees only their own, staff only the active branch's. Anything else is "not found" (section 12.3).</summary>
public static class StayAccess
{
    public static async Task<BoardingStayAggregate?> LoadAsync(IAggregateRootRepository<BoardingStayAggregate> repository, Guid stayId, Guid? customerId, Guid? branchId)
    {
        var stay = await repository.GetByIdAsync(stayId);
        if (stay is null) return null;
        if (customerId is { } customer && stay.CustomerId != customer) return null;
        if (branchId is { } branch && stay.BranchId != branch) return null;
        return customerId is null && branchId is null ? null : stay; // fail closed: a caller with no scope sees nothing
    }

    public static CommandResponse NotFound() => Violation(BusinessViolationCodes.StayNotFound, "StayId", BoardingMessages.StayNotFound);
}

/// <summary>The cat facts every booking command needs: owned by the customer, alive, and its vaccination gate as of the departure date (BR-1, ADR-222).</summary>
public static class StayCatGate
{
    public static async Task<(IReadOnlyList<CatAggregate>? Cats, CommandResponse? Failure)> LoadCatsAsync(
        IAggregateRootRepository<CatAggregate> catRepository, IReadOnlyList<Guid> catIds, Guid customerId)
    {
        var cats = new List<CatAggregate>();
        foreach (var catId in catIds.Distinct())
        {
            var cat = await catRepository.GetByIdAsync(catId);
            if (cat is null || cat.OwnerId != customerId)
                return (null, Violation(BusinessViolationCodes.CatNotFound, nameof(catIds), "Cat not found.", nameof(CatAggregate)));
            if (cat.LifeStatus != CatLifeStatus.Alive)
                return (null, Violation(BusinessViolationCodes.CatNotBoardable, nameof(catIds), $"{cat.Name} cannot be booked for boarding.", nameof(CatAggregate)));
            cats.Add(cat);
        }

        return (cats, null);
    }

    public static Dictionary<Guid, GateEvaluation> Evaluate(
        IEnumerable<CatAggregate> cats, IReadOnlyList<VaccinationRuleVaccine> coreVaccines, DateOnly departureOn, int dueSoonDays) =>
        cats.ToDictionary(c => c.Id, c => BoardingVaccinationGate.Evaluate(c.EvaluateVaccinationStatus(coreVaccines, departureOn, dueSoonDays), departureOn));
}

/// <summary>C5 -- HO-BRD-001 section 11.4. A customer's cats must be their own and alive; the gate is evaluated per cat as of departure and stored. A blocked
/// enquiry is KEPT and resumable (US-BRD-04 AC2). The quote is the finder's price, not stored until reserve.</summary>
public sealed class CreateBoardingEnquiryCommandHandler(
    IAggregateRootRepository<BoardingStayAggregate> stayRepository,
    IAggregateRootRepository<CatAggregate> catRepository,
    IBoardingInputs boardingInputs,
    IBoardingReferenceReader referenceReader,
    INotificationLinkBuilder linkBuilder,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<CreateBoardingEnquiryCommand>
{
    public async Task<CommandResponse> HandleAsync(CreateBoardingEnquiryCommand command)
    {
        var settings = await boardingInputs.GetSettingsAsync(command.BranchId);
        if (settings is null) return Violation(BusinessViolationCodes.BranchNotFound, nameof(command.BranchId), "Branch not found.");

        var (cats, failure) = await StayCatGate.LoadCatsAsync(catRepository, command.CatIds, command.CustomerId);
        if (failure is not null) return failure;

        var now = timeProvider.GetUtcNow();
        var unitType = BoardingUnitMapper.ParseEnum<BoardingUnitType>(command.UnitType);
        var gates = StayCatGate.Evaluate(cats!, await referenceReader.GetActiveCoreVaccinesAsync(), command.DepartureOn, settings.VaccinationDueSoonDays);
        var window = BoardingOccupancyWindow.Compute(command.ArrivalOn, command.DepartureOn, settings.TimeZoneId, settings.CheckInWindow, settings.CheckOutWindow, settings.TurnaroundHours);

        var stay = new BoardingStayAggregate(command.StayId);
        try
        {
            stay.CreateEnquiry(new EnquiryDto(
                command.BranchId, command.CustomerId, command.IsStaff ? BoardingChannel.Reception : BoardingChannel.Online, command.ArrivalOn, command.DepartureOn, unitType,
                cats!.Select(c => new StayCatInput(c.Id, c.Name)).ToList(), gates, window,
                new StayDateContext(settings.TimeZoneId, settings.CheckInWindow, settings.MaxStayNights, settings.BookingHorizonDays, IsOnline: !command.IsStaff),
                now, settings.CurrencyCode, command.UserContext!, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await stayRepository.SaveAsync(stay, EventPublishOption.InMemoryPublishOnly);
        return Success(await BuildResultAsync(stay, cats!, gates, settings, command.BranchId, now, unitType));
    }

    private async Task<EnquiryResult> BuildResultAsync(
        BoardingStayAggregate stay, IReadOnlyList<CatAggregate> cats, Dictionary<Guid, GateEvaluation> gates, BoardingBranchSettings settings, Guid branchId, DateTimeOffset now, BoardingUnitType unitType) =>
        await EnquiryResults.BuildAsync(stay, cats, gates, settings, boardingInputs, linkBuilder, branchId, now, unitType);
}

/// <summary>Shared by the create and change handlers: the gate results with each blocked cat's vaccination booking link, and the (unstored) quote.</summary>
public static class EnquiryResults
{
    public static async Task<EnquiryResult> BuildAsync(
        BoardingStayAggregate stay, IReadOnlyList<CatAggregate> cats, Dictionary<Guid, GateEvaluation> gates, BoardingBranchSettings settings,
        IBoardingInputs boardingInputs, INotificationLinkBuilder linkBuilder, Guid branchId, DateTimeOffset now, BoardingUnitType unitType)
    {
        var gateResults = cats.Select(c => new GateCatResult(
            c.Id, c.Name, gates[c.Id], gates[c.Id].Passed ? null : linkBuilder.CatCareLink(c.Id, "Vaccination"))).ToList();

        var inputs = await boardingInputs.LoadAsync(branchId, stay.ArrivalOn, stay.DepartureOn, stay.Id, now);
        BoardingQuote? quote = inputs is not null && inputs.RateCards.TryGetValue(unitType.Value, out var card)
            ? BoardingQuoteCalculator.Quote(stay.Nights, cats.Count, card, settings.Pricing)
            : null;

        return new EnquiryResult(stay.Id, gateResults, quote, settings.CurrencyCode);
    }
}

public sealed class ChangeBoardingEnquiryCommandHandler(
    IAggregateRootRepository<BoardingStayAggregate> stayRepository,
    IAggregateRootRepository<CatAggregate> catRepository,
    IBoardingInputs boardingInputs,
    IBoardingReferenceReader referenceReader,
    INotificationLinkBuilder linkBuilder,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<ChangeBoardingEnquiryCommand>
{
    public async Task<CommandResponse> HandleAsync(ChangeBoardingEnquiryCommand command)
    {
        var stay = await StayAccess.LoadAsync(stayRepository, command.StayId, command.CustomerId, command.BranchId);
        if (stay is null) return StayAccess.NotFound();

        var settings = await boardingInputs.GetSettingsAsync(stay.BranchId);
        if (settings is null) return Violation(BusinessViolationCodes.BranchNotFound, "BranchId", "Branch not found.");

        var (cats, failure) = await StayCatGate.LoadCatsAsync(catRepository, command.CatIds, stay.CustomerId);
        if (failure is not null) return failure;

        var now = timeProvider.GetUtcNow();
        var unitType = BoardingUnitMapper.ParseEnum<BoardingUnitType>(command.UnitType);
        var gates = StayCatGate.Evaluate(cats!, await referenceReader.GetActiveCoreVaccinesAsync(), command.DepartureOn, settings.VaccinationDueSoonDays);
        var window = BoardingOccupancyWindow.Compute(command.ArrivalOn, command.DepartureOn, settings.TimeZoneId, settings.CheckInWindow, settings.CheckOutWindow, settings.TurnaroundHours);

        try
        {
            stay.ChangeEnquiry(new ChangeEnquiryDto(
                command.ArrivalOn, command.DepartureOn, unitType, cats!.Select(c => new StayCatInput(c.Id, c.Name)).ToList(), gates, window,
                new StayDateContext(settings.TimeZoneId, settings.CheckInWindow, settings.MaxStayNights, settings.BookingHorizonDays, IsOnline: command.CustomerId is not null),
                now, command.UserContext!, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await stayRepository.UpdateAsync(stay, EventPublishOption.InMemoryPublishOnly);
        return Success(await EnquiryResults.BuildAsync(stay, cats!, gates, settings, boardingInputs, linkBuilder, stay.BranchId, now, unitType));
    }
}

/// <summary>C6 -- US-BRD-05. The aggregate validates the profile; the handler only resolves the stay by id and caller.</summary>
public sealed class SetStayCareProfileCommandHandler(IAggregateRootRepository<BoardingStayAggregate> stayRepository)
    : ICommandHandlerAsync<SetStayCareProfileCommand>
{
    public async Task<CommandResponse> HandleAsync(SetStayCareProfileCommand command)
    {
        var stay = await StayAccess.LoadAsync(stayRepository, command.StayId, command.CustomerId, command.BranchId);
        if (stay is null) return StayAccess.NotFound();

        try
        {
            stay.SetCareProfile(new SetCareProfileDto(command.CatId, command.Profile.ToCareProfile(), command.UserContext!, command.CorrelationId, command.BranchId is not null, command.VetAuthorityChangeReason));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await stayRepository.UpdateAsync(stay, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>C7 -- US-BRD-04 AC3. Administrator only (the controller's action); the reason is mandatory; the audit row is written by the event handler.</summary>
public sealed class OverrideVaccinationGateCommandHandler(
    IAggregateRootRepository<BoardingStayAggregate> stayRepository, TimeProvider timeProvider)
    : ICommandHandlerAsync<OverrideVaccinationGateCommand>
{
    public async Task<CommandResponse> HandleAsync(OverrideVaccinationGateCommand command)
    {
        var stay = await StayAccess.LoadAsync(stayRepository, command.StayId, command.CustomerId, command.BranchId);
        if (stay is null) return StayAccess.NotFound();

        try
        {
            stay.OverrideGate(new OverrideGateDto(command.CatId, command.Reason, timeProvider.GetUtcNow(), command.UserContext!, command.CorrelationId));
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
/// C8 -- US-BRD-06. Re-evaluate the gate; <see cref="UnitClaim"/> for the type's deterministic unit; quote with the current card and settings; the deposit
/// policy; <c>Reserve</c>; enqueue <c>boarding.reserved</c> before the save. The unit and the cats are held by the exclusion constraints, so twenty parallel
/// reserves for the last unit leave exactly one winner (I-C12).
/// </summary>
public sealed class ReserveBoardingStayCommandHandler(
    IAggregateRootRepository<BoardingStayAggregate> stayRepository,
    IAggregateRootRepository<CatAggregate> catRepository,
    IBoardingInputs boardingInputs,
    IBoardingReferenceReader referenceReader,
    INotificationOutbox notificationOutbox,
    INotificationLinkBuilder linkBuilder,
    TimeProvider timeProvider,
    IFreshScopeExecutor scopes,
    IUniqueViolationDetector uniqueViolation)
    : ICommandHandlerAsync<ReserveBoardingStayCommand>
{
    // Backlog row 49: the unit is the deterministic first free one; a parallel reserve that loses the exclusion constraint re-picks in a fresh scope (bounded), so UnitNoLongerAvailable means none is left.
    public Task<CommandResponse> HandleAsync(ReserveBoardingStayCommand command) =>
        ClaimRetry.RunAsync(this, command, (h, c) => h.AttemptAsync(c), scopes, uniqueViolation.IsOccupancyClaimLost,
            () => Violation(BusinessViolationCodes.UnitNoLongerAvailable, "UnitId", BoardingMessages.UnitNoLongerAvailable));

    public async Task<CommandResponse> AttemptAsync(ReserveBoardingStayCommand command)
    {
        var stay = await StayAccess.LoadAsync(stayRepository, command.StayId, command.CustomerId, command.BranchId);
        if (stay is null) return StayAccess.NotFound();

        try
        {
            StayTransitionRules.EnsureAllowed(stay.Status, StayTransitionRules.Reserve);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        var now = timeProvider.GetUtcNow();
        var catIds = stay.Cats.Select(c => c.CatId).ToList();
        var (cats, failure) = await StayCatGate.LoadCatsAsync(catRepository, catIds, stay.CustomerId);
        if (failure is not null) return failure;

        var claim = await UnitClaim.ValidateAsync(boardingInputs, new UnitClaim.Request(
            stay.BranchId, stay.ArrivalOn, stay.DepartureOn, stay.RequestedUnitType, catIds, stay.RequiresIsolation,
            command.CustomerId is not null ? AvailabilityMode.Online : AvailabilityMode.Staff, stay.Id, now));
        if (!claim.IsSuccess) return Violation(claim.Code!, claim.Property!, claim.Message!);

        var settings = claim.Inputs!.Settings;
        var gates = StayCatGate.Evaluate(cats!, await referenceReader.GetActiveCoreVaccinesAsync(), stay.DepartureOn, settings.VaccinationDueSoonDays);
        var card = claim.Inputs.RateCards[stay.RequestedUnitType.Value];
        var quote = BoardingQuoteCalculator.Quote(stay.Nights, catIds.Count, card, settings.Pricing);
        var deposit = BoardingDepositPolicy.Compute(quote.Total, settings.DepositFixedAmount, settings.DepositPercent);
        var today = BoardingOccupancyWindow.LocalDateOf(now, settings.TimeZoneId);

        try
        {
            stay.Reserve(new ReserveDto(
                claim.Unit!, card, settings.Pricing, quote, deposit, gates, claim.Window, now, today, settings.ReservationHoldHours, settings.CurrencyCode,
                cats!.ToDictionary(c => c.Id, c => c.Name), command.UserContext!, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        BoardingNotifications.EnqueueReserved(notificationOutbox, linkBuilder, stay, settings.TimeZoneId, command.CorrelationId, command.UserContext!);

        await stayRepository.UpdateAsync(stay, EventPublishOption.InMemoryPublishOnly);
        return Success(new ReserveResult(stay.Id, stay.DepositRequired, stay.CurrencyCode, stay.DepositDueBy));
    }
}
