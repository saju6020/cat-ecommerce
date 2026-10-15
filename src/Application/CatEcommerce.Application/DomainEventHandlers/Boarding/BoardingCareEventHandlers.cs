using CatEcommerce.Domain.Aggregates.Boarding;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Account;
using CatEcommerce.Domain.Events.Administration;
using CatEcommerce.Domain.Events.Boarding;
using CatEcommerce.Domain.Events.Cat;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Boarding;

// HO-BRD-001 section 11.6 (Phase E). Every handler here loops with one UpdateAsync per row, is idempotent, and NEVER enqueues a message (HO-NTF-001 ADR-057): a handler runs
// after the commit, so a failure here cannot be allowed to be the only thing between a customer and a message.

/// <summary>
/// A cat checked in: its weight joins HO-CAT-001's weight history as a <c>CatWeightRecordAggregate</c> (the cat itself is untouched, ADR-023). A failure here is LOGGED and swallowed --
/// it must not undo a check-in that has already been committed (.ai/domain-events-and-outbox.md section 3).
/// </summary>
public sealed class RecordCatWeightOnBoardingHandler(
    IAggregateRootRepository<CatWeightRecordAggregate> weightRepository, ILogger<RecordCatWeightOnBoardingHandler> logger)
    : IEventHandlerAsync<StayCatCheckedInEvent>, IEventHandlerAsync<StayCatCheckedOutEvent>
{
    public Task HandleAsync(StayCatCheckedInEvent @event) => RecordAsync(@event.CatId, @event.WeightKg, @event.RecordedOn, @event.UserContext, @event.CorrelationId, "check-in");

    public Task HandleAsync(StayCatCheckedOutEvent @event) => RecordAsync(@event.CatId, @event.WeightKg, @event.RecordedOn, @event.UserContext, @event.CorrelationId, "check-out");

    private async Task RecordAsync(Guid catId, decimal kilograms, DateOnly recordedOn, Platform.Infrastructure.Common.Security.UserContext userContext, Guid correlationId, string moment)
    {
        try
        {
            var record = new CatWeightRecordAggregate(Guid.NewGuid());
            record.Record(new CatWeightRecordDto(catId, kilograms, recordedOn, userContext.UserId, "Boarding " + moment, recordedOn, userContext, correlationId));
            await weightRepository.SaveAsync(record, EventPublishOption.InMemoryPublishOnly);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not record the boarding {Moment} weight of cat {CatId}; the stay is unaffected.", moment, catId);
        }
    }
}

/// <summary>US-BRD-09: a care profile changed while the cat is in house. Its pending tasks for today and later are voided (their keys freed); the worker's next pass regenerates the new schedule.</summary>
public sealed class VoidPendingTasksOnCareProfileChangedHandler(
    IBoardingTaskFinder taskFinder, IAggregateRootRepository<CareTaskAggregate> taskRepository, IBoardingInputs boardingInputs, IAggregateRootRepository<BoardingStayAggregate> stayRepository, TimeProvider timeProvider)
    : IEventHandlerAsync<StayCareProfileSetEvent>
{
    public async Task HandleAsync(StayCareProfileSetEvent @event)
    {
        if (@event.Status != BoardingStayStatus.CheckedIn) return;

        var stay = await stayRepository.GetByIdAsync(@event.StayId);
        var zone = stay is null ? "UTC" : (await boardingInputs.GetSettingsAsync(stay.BranchId))?.TimeZoneId ?? "UTC";
        var today = BoardingOccupancyWindow.LocalDateOf(timeProvider.GetUtcNow(), zone);

        foreach (var id in await taskFinder.FindPendingAsync(@event.StayId, @event.CatId, today))
        {
            var task = await taskRepository.GetByIdAsync(id);
            if (task is null) continue;

            task.Void();
            await taskRepository.UpdateAsync(task, EventPublishOption.InMemoryPublishOnly);
        }
    }
}

/// <summary>US-BRD-09 / US-BRD-16: the stay ended (cancelled, or checked out early) -- remaining pending tasks are voided.</summary>
public sealed class VoidTasksOnStayEndedHandler(IBoardingTaskFinder taskFinder, IAggregateRootRepository<CareTaskAggregate> taskRepository)
    : IEventHandlerAsync<BoardingStayCancelledEvent>, IEventHandlerAsync<BoardingStayCheckedOutEvent>
{
    public Task HandleAsync(BoardingStayCancelledEvent @event) => VoidAsync(@event.StayId);

    public Task HandleAsync(BoardingStayCheckedOutEvent @event) => VoidAsync(@event.StayId);

    private async Task VoidAsync(Guid stayId)
    {
        foreach (var id in await taskFinder.FindPendingAsync(stayId, null, DateOnly.MinValue))
        {
            var task = await taskRepository.GetByIdAsync(id);
            if (task is null) continue;

            task.Void();
            await taskRepository.UpdateAsync(task, EventPublishOption.InMemoryPublishOnly);
        }
    }
}

/// <summary>HO-ADM-001 ADR-048 / US-ADM-03 AC1 (boarding half): approved caretaker leave -- that caretaker's pending tasks in the range are re-run through the assigner without them. Idempotent.</summary>
public sealed class ReassignCareTasksOnLeaveApprovedHandler(
    IBoardingTaskFinder taskFinder, IAggregateRootRepository<CareTaskAggregate> taskRepository, IAggregateRootRepository<BoardingStayAggregate> stayRepository,
    IBoardingInputs boardingInputs, IBoardingCareInputs careInputs, TimeProvider timeProvider)
    : IEventHandlerAsync<StaffLeaveApprovedEvent>
{
    public async Task HandleAsync(StaffLeaveApprovedEvent @event)
    {
        var excluded = new HashSet<Guid> { @event.StaffAccountId };

        foreach (var id in await taskFinder.FindPendingAssignedToAsync(@event.StaffAccountId, @event.StartsOn, @event.EndsOn))
        {
            var task = await taskRepository.GetByIdAsync(id);
            if (task is null) continue;

            var inputs = await boardingInputs.LoadAsync(task.BranchId, task.LocalDate, task.LocalDate, null, timeProvider.GetUtcNow());
            var shifts = await careInputs.GetCaretakerShiftsAsync(task.BranchId, task.LocalDate, task.LocalDate);
            var units = inputs?.Units.Select(u => new AssignableUnit(u.UnitId, u.Block, u.Code)).ToList() ?? [];

            task.Assign(CareTaskAssigner.Assign(task.DueOn, task.UnitId, units, shifts, excluded));
            await taskRepository.UpdateAsync(task, EventPublishOption.InMemoryPublishOnly);
        }
    }
}

/// <summary>HO-CAT-001 event: a cat with a live stay died. The stay is FLAGGED for staff; never cancelled, never a message (section 20.7).</summary>
public sealed class FlagStaysOnCatDeceasedHandler(IBoardingStayFinder stayFinder, IAggregateRootRepository<BoardingStayAggregate> stayRepository)
    : IEventHandlerAsync<CatMarkedDeceasedEvent>
{
    public async Task HandleAsync(CatMarkedDeceasedEvent @event)
    {
        foreach (var id in await stayFinder.FindLiveByCatAsync(@event.CatId))
        {
            var stay = await stayRepository.GetByIdAsync(id);
            if (stay is null) continue;

            stay.FlagAttention(StayAttentionReason.CatDeceased);
            await stayRepository.UpdateAsync(stay, EventPublishOption.InMemoryPublishOnly);
        }
    }
}

public sealed class FlagStaysOnCatTransferredHandler(IBoardingStayFinder stayFinder, IAggregateRootRepository<BoardingStayAggregate> stayRepository)
    : IEventHandlerAsync<CatOwnershipTransferredEvent>
{
    public async Task HandleAsync(CatOwnershipTransferredEvent @event)
    {
        foreach (var id in await stayFinder.FindLiveByCatAsync(@event.CatId))
        {
            var stay = await stayRepository.GetByIdAsync(id);
            if (stay is null) continue;

            stay.FlagAttention(StayAttentionReason.CatOwnerChanged);
            await stayRepository.UpdateAsync(stay, EventPublishOption.InMemoryPublishOnly);
        }
    }
}

/// <summary>HO-ACC-001 ADR-039: a merged duplicate's stays move to the survivor. Idempotent.</summary>
public sealed class ReassignStaysOnCustomerMergedHandler(
    IBoardingStayFinder stayFinder, IAggregateRootRepository<BoardingStayAggregate> stayRepository, IBoardingWaitlistDaycareFinder waitlistFinder,
    IAggregateRootRepository<BoardingWaitlistEntryAggregate> waitlistRepository, IAggregateRootRepository<DaycareVisitAggregate> daycareRepository)
    : IEventHandlerAsync<CustomerMergedEvent>
{
    public async Task HandleAsync(CustomerMergedEvent @event)
    {
        // Waitlist entries and daycare visits follow the survivor too (section 11.6).
        foreach (var id in await waitlistFinder.FindWaitlistByCustomerAsync(@event.DuplicateCustomerId))
        {
            var entry = await waitlistRepository.GetByIdAsync(id);
            if (entry is null) continue;

            entry.ReassignOwner(@event.SurvivingCustomerId);
            await waitlistRepository.UpdateAsync(entry, EventPublishOption.InMemoryPublishOnly);
        }

        foreach (var id in await waitlistFinder.FindDaycareByCustomerAsync(@event.DuplicateCustomerId))
        {
            var visit = await daycareRepository.GetByIdAsync(id);
            if (visit is null) continue;

            visit.ReassignOwner(@event.SurvivingCustomerId);
            await daycareRepository.UpdateAsync(visit, EventPublishOption.InMemoryPublishOnly);
        }

        foreach (var id in await stayFinder.FindByCustomerAsync(@event.DuplicateCustomerId))
        {
            var stay = await stayRepository.GetByIdAsync(id);
            if (stay is null) continue;

            stay.ReassignOwner(@event.SurvivingCustomerId);
            await stayRepository.UpdateAsync(stay, EventPublishOption.InMemoryPublishOnly);
        }
    }
}
