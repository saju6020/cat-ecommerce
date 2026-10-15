using CatEcommerce.Application.Commands.Boarding;
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

public sealed record ConfirmResult(string Status, string DepositState, decimal Outstanding, string CurrencyCode);

public sealed record CancelResult(bool DepositForfeited);

/// <summary>
/// C9 -- ADR-223 (renumbered ADR-356). Reads Billing's accepted deposit through <see cref="IBoardingDepositLedger"/>: accepted &gt;= required -> gate -> Confirm and
/// enqueue <c>boarding.confirmed</c> before the save; a payment awaiting Billing's second approver -> the lapse is suspended, no message; nothing accepted -> staff get
/// <c>DepositNotYetReceived</c> with the amount outstanding, the worker skips silently. Boarding never records the payment.
/// </summary>
public sealed class ConfirmBoardingStayCommandHandler(
    IAggregateRootRepository<BoardingStayAggregate> stayRepository,
    IAggregateRootRepository<CatAggregate> catRepository,
    IBoardingDepositLedger depositLedger,
    IBoardingInputs boardingInputs,
    IBoardingReferenceReader referenceReader,
    INotificationOutbox notificationOutbox,
    INotificationLinkBuilder linkBuilder,
    TimeProvider timeProvider,
    IFreshScopeExecutor scopes,
    IUniqueViolationDetector uniqueViolation)
    : ICommandHandlerAsync<ConfirmBoardingStayCommand>
{
    // Backlog row 54: a staff Confirm racing the worker Confirm of the same stay hits a stale row version; the loser re-reads in a fresh scope, finds the stay Confirmed and reports it (idempotent success).
    public Task<CommandResponse> HandleAsync(ConfirmBoardingStayCommand command) =>
        ClaimRetry.RunAsync(this, command, (h, c) => h.AttemptAsync(c), scopes, uniqueViolation.IsConcurrencyConflict, exhausted: null);

    public async Task<CommandResponse> AttemptAsync(ConfirmBoardingStayCommand command)
    {
        var stay = command.IsWorker
            ? await stayRepository.GetByIdAsync(command.StayId)
            : await StayAccess.LoadAsync(stayRepository, command.StayId, command.CustomerId, command.BranchId);
        if (stay is null) return StayAccess.NotFound();

        // Idempotent: a stay already past Reserved is simply reported (a double click, or the worker racing staff).
        if (stay.Status != BoardingStayStatus.Reserved)
        {
            if (command.IsWorker || stay.Status == BoardingStayStatus.Confirmed)
                return Success(new ConfirmResult(stay.Status.DisplayName, stay.DepositState.DisplayName, 0m, stay.CurrencyCode));
        }

        var ledger = await depositLedger.GetAsync(stay.Id);
        var now = timeProvider.GetUtcNow();

        if (ledger.Accepted < stay.DepositRequired)
        {
            if (ledger.PendingApproval > 0m && stay.Status == BoardingStayStatus.Reserved)
            {
                stay.MarkDepositPendingApproval();
                await stayRepository.UpdateAsync(stay, EventPublishOption.InMemoryPublishOnly);
                return Success(new ConfirmResult(stay.Status.DisplayName, stay.DepositState.DisplayName, stay.DepositRequired - ledger.Accepted, stay.CurrencyCode));
            }

            if (command.IsWorker)
                return Success(new ConfirmResult(stay.Status.DisplayName, stay.DepositState.DisplayName, stay.DepositRequired - ledger.Accepted, stay.CurrencyCode));
        }

        var settings = await boardingInputs.GetSettingsAsync(stay.BranchId);
        if (settings is null) return Violation(BusinessViolationCodes.BranchNotFound, "BranchId", "Branch not found.");

        var catIds = stay.Cats.Select(c => c.CatId).ToList();
        var (cats, failure) = await StayCatGate.LoadCatsAsync(catRepository, catIds, stay.CustomerId);
        if (failure is not null) return command.IsWorker ? Success(new ConfirmResult(stay.Status.DisplayName, stay.DepositState.DisplayName, 0m, stay.CurrencyCode)) : failure;

        var gates = StayCatGate.Evaluate(cats!, await referenceReader.GetActiveCoreVaccinesAsync(), stay.DepartureOn, settings.VaccinationDueSoonDays);
        try
        {
            stay.Confirm(new ConfirmStayDto(ledger.Accepted, gates, BoardingOccupancyWindow.LocalDateOf(now, settings.TimeZoneId), now, command.UserContext!, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex) when (command.IsWorker && ex.ErrorCode == BusinessViolationCodes.VaccinationGateBlocked)
        {
            // A paid reservation whose cat has fallen out of the gate can neither confirm nor (while paid) lapse: flag it for staff and let the lapse proceed once the hold has passed.
            // Already flagged (for this or another reason): nothing to write, so the worker does not re-save the stay every pass.
            if (!stay.NeedsAttention)
            {
                stay.FlagAttention(StayAttentionReason.VaccinationGateBlocked);
                await stayRepository.UpdateAsync(stay, EventPublishOption.InMemoryPublishOnly);
            }

            return Success(new ConfirmResult(stay.Status.DisplayName, stay.DepositState.DisplayName, 0m, stay.CurrencyCode));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        BoardingNotifications.EnqueueConfirmed(notificationOutbox, linkBuilder, stay, ledger.Accepted, command.CorrelationId, command.UserContext!);

        await stayRepository.UpdateAsync(stay, EventPublishOption.InMemoryPublishOnly);
        return Success(new ConfirmResult(stay.Status.DisplayName, stay.DepositState.DisplayName, 0m, stay.CurrencyCode));
    }
}

/// <summary>C10 -- BR-16, worker only. The ledger is re-read INSIDE the handler (never trust the pass's snapshot); a payment awaiting approval suspends the lapse; a
/// reservation that is not lapsable is a success that writes nothing.</summary>
public sealed class LapseBoardingReservationCommandHandler(
    IAggregateRootRepository<BoardingStayAggregate> stayRepository,
    IAggregateRootRepository<CatAggregate> catRepository,
    IBoardingInputs boardingInputs,
    IBoardingReferenceReader referenceReader,
    IBoardingDepositLedger depositLedger,
    INotificationOutbox notificationOutbox,
    INotificationLinkBuilder linkBuilder,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<LapseBoardingReservationCommand>
{
    public async Task<CommandResponse> HandleAsync(LapseBoardingReservationCommand command)
    {
        var stay = await stayRepository.GetByIdAsync(command.StayId);
        if (stay is null || stay.Status != BoardingStayStatus.Reserved) return Success();

        var ledger = await depositLedger.GetAsync(stay.Id);
        if (ledger.PendingApproval > 0m && stay.DepositState == DepositState.Awaiting)
        {
            stay.MarkDepositPendingApproval();
            await stayRepository.UpdateAsync(stay, EventPublishOption.InMemoryPublishOnly);
            return Success();
        }

        // A paid reservation is the confirm pass's -- unless it cannot confirm because a cat fails the vaccination gate NOW (a fresh evaluation, whatever else the stay is flagged for),
        // when it lapses once the hold has passed.
        if (ledger.Accepted >= stay.DepositRequired && stay.DepositRequired > 0m && !await GateBlockedAsync(stay)) return Success();

        try
        {
            stay.Lapse(new LapseStayDto(timeProvider.GetUtcNow(), command.UserContext!, command.CorrelationId));
        }
        catch (DomainRuleViolationException)
        {
            return Success();
        }

        BoardingNotifications.EnqueueLapsed(notificationOutbox, linkBuilder, stay, command.CorrelationId, command.UserContext!);
        await stayRepository.UpdateAsync(stay, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }

    private async Task<bool> GateBlockedAsync(BoardingStayAggregate stay)
    {
        var settings = await boardingInputs.GetSettingsAsync(stay.BranchId);
        if (settings is null) return false;

        var (cats, failure) = await StayCatGate.LoadCatsAsync(catRepository, stay.Cats.Select(c => c.CatId).ToList(), stay.CustomerId);
        if (failure is not null) return true; // a cat that is gone or no longer the customer's cannot be confirmed either

        var gates = StayCatGate.Evaluate(cats!, await referenceReader.GetActiveCoreVaccinesAsync(), stay.DepartureOn, settings.VaccinationDueSoonDays);
        return gates.Values.Any(g => !g.Passed);
    }
}

public sealed class ExpireBoardingEnquiryCommandHandler(
    IAggregateRootRepository<BoardingStayAggregate> stayRepository, IBoardingInputs boardingInputs, TimeProvider timeProvider)
    : ICommandHandlerAsync<ExpireBoardingEnquiryCommand>
{
    public async Task<CommandResponse> HandleAsync(ExpireBoardingEnquiryCommand command)
    {
        var stay = await stayRepository.GetByIdAsync(command.StayId);
        if (stay is null || stay.Status != BoardingStayStatus.Enquiry) return Success();

        var now = timeProvider.GetUtcNow();
        var zone = (await boardingInputs.GetSettingsAsync(stay.BranchId))?.TimeZoneId ?? "UTC";
        try
        {
            stay.ExpireEnquiry(new LapseStayDto(now, command.UserContext!, command.CorrelationId), BoardingOccupancyWindow.LocalDateOf(now, zone));
        }
        catch (DomainRuleViolationException)
        {
            return Success();
        }

        await stayRepository.UpdateAsync(stay, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>C11 -- BR-12. Customer: own stay; staff: the active branch, note required. Enqueues <c>boarding.cancelled</c> stating what happens to the deposit.</summary>
public sealed class CancelBoardingStayCommandHandler(
    IAggregateRootRepository<BoardingStayAggregate> stayRepository,
    IBoardingInputs boardingInputs,
    IBoardingDepositLedger depositLedger,
    INotificationOutbox notificationOutbox,
    INotificationLinkBuilder linkBuilder,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<CancelBoardingStayCommand>
{
    public async Task<CommandResponse> HandleAsync(CancelBoardingStayCommand command)
    {
        var stay = await StayAccess.LoadAsync(stayRepository, command.StayId, command.CustomerId, command.BranchId);
        if (stay is null) return StayAccess.NotFound();

        var settings = await boardingInputs.GetSettingsAsync(stay.BranchId);
        if (settings is null) return Violation(BusinessViolationCodes.BranchNotFound, "BranchId", "Branch not found.");

        var received = (await depositLedger.GetAsync(stay.Id)).Accepted > 0m;
        try
        {
            stay.Cancel(new CancelStayDto(command.CustomerId is not null, command.Note, timeProvider.GetUtcNow(), settings.CancellationWindowHours, command.UserContext!, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        BoardingNotifications.EnqueueCancelled(notificationOutbox, linkBuilder, stay, received, command.CorrelationId, command.UserContext!);
        await stayRepository.UpdateAsync(stay, EventPublishOption.InMemoryPublishOnly);
        return Success(new CancelResult(stay.DepositForfeited));
    }
}

public sealed class RecordBoardingNoShowCommandHandler(
    IAggregateRootRepository<BoardingStayAggregate> stayRepository, IBoardingInputs boardingInputs, TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordBoardingNoShowCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordBoardingNoShowCommand command)
    {
        var stay = await StayAccess.LoadAsync(stayRepository, command.StayId, command.CustomerId, command.BranchId);
        if (stay is null) return StayAccess.NotFound();

        var settings = await boardingInputs.GetSettingsAsync(stay.BranchId);
        if (settings is null) return Violation(BusinessViolationCodes.BranchNotFound, "BranchId", "Branch not found.");

        try
        {
            stay.RecordNoShow(new NoShowDto(timeProvider.GetUtcNow(), settings.NoShowGraceHours, command.UserContext!, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await stayRepository.UpdateAsync(stay, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}
