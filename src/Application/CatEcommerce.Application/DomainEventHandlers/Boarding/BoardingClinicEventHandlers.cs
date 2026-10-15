using CatEcommerce.Domain.Aggregates.Boarding;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Boarding;
using CatEcommerce.Domain.Events.Surgery;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Boarding;

// HO-BRD-001 section 11.6 (Phase G). One UpdateAsync per row, idempotent, and NEVER enqueues a message (HO-NTF-001 ADR-057).

/// <summary>HO-EMR-001 opened the visit of a referral: stored on the referral (section 12.8 E1). Idempotent; an unknown referral is ignored.</summary>
public sealed class LinkReferralVisitHandler(IBoardingStayFinder stayFinder, IAggregateRootRepository<BoardingStayAggregate> stayRepository)
    : IEventHandlerAsync<ReferralVisitOpenedEvent>
{
    public async Task HandleAsync(ReferralVisitOpenedEvent @event)
    {
        var stayId = await stayFinder.FindByReferralAsync(@event.ReferralId);
        var stay = stayId is null ? null : await stayRepository.GetByIdAsync(stayId.Value);
        if (stay is null || !stay.LinkReferralVisit(@event.ReferralId, @event.VisitId)) return;

        await stayRepository.UpdateAsync(stay, EventPublishOption.InMemoryPublishOnly);
    }
}

/// <summary>A clinic charge on a boarded cat (raised by HO-EMR-001 / HO-BIL-001 / HO-SRG-001) counts against the cat's vet spending limit. Idempotent by charge id; a refusal is logged.</summary>
public sealed class RecordVetCommitmentHandler(
    IAggregateRootRepository<BoardingStayAggregate> stayRepository, TimeProvider timeProvider, ILogger<RecordVetCommitmentHandler> logger)
    : IEventHandlerAsync<BoardingVetChargeRecordedEvent>
{
    public async Task HandleAsync(BoardingVetChargeRecordedEvent @event)
    {
        var stay = await stayRepository.GetByIdAsync(@event.StayId);
        if (stay is null) return;

        try
        {
            if (!stay.RecordVetCommitment(new RecordVetCommitmentDto(@event.ChargeId, @event.CatId, @event.Amount, timeProvider.GetUtcNow()))) return;
        }
        catch (DomainRuleViolationException ex)
        {
            logger.LogWarning("Boarding vet commitment for charge {ChargeId} on stay {StayId} was refused: {Code}.", @event.ChargeId, @event.StayId, ex.ErrorCode);
            return;
        }

        await stayRepository.UpdateAsync(stay, EventPublishOption.InMemoryPublishOnly);
    }
}

/// <summary>US-SRG-09: HO-SRG-001 scheduled (or moved) a surgery for a cat that is boarding. It is recorded on the stay; the unit is not released.</summary>
public sealed class RecordSurgeryOnBoardedCatHandler(IBoardingStayFinder stayFinder, IAggregateRootRepository<BoardingStayAggregate> stayRepository)
    : IEventHandlerAsync<SurgeryScheduledEvent>, IEventHandlerAsync<SurgeryRescheduledEvent>
{
    public Task HandleAsync(SurgeryScheduledEvent @event) => RecordAsync(@event.SurgeryId, @event.CatId, @event.StartsOn, @event.CorrelationId, @event.UserContext);

    public Task HandleAsync(SurgeryRescheduledEvent @event) => RecordAsync(@event.SurgeryId, @event.CatId, @event.StartsOn, @event.CorrelationId, @event.UserContext);

    private async Task RecordAsync(Guid surgeryId, Guid catId, DateTimeOffset startsOn, Guid correlationId, Platform.Infrastructure.Common.Security.UserContext userContext)
    {
        var stayId = await stayFinder.FindInHouseByCatAsync(catId);
        var stay = stayId is null ? null : await stayRepository.GetByIdAsync(stayId.Value);
        if (stay is null || !stay.RecordSurgery(surgeryId, catId, startsOn, correlationId, userContext)) return;

        await stayRepository.UpdateAsync(stay, EventPublishOption.InMemoryPublishOnly);
    }
}

/// <summary>US-SRG-09: the surgery ended -- the cat is back in its unit. Idempotent; a surgery of a cat that was not boarding is ignored.</summary>
public sealed class DischargeSurgeryOnBoardedCatHandler(IBoardingStayFinder stayFinder, IAggregateRootRepository<BoardingStayAggregate> stayRepository)
    : IEventHandlerAsync<SurgeryDischargedEvent>
{
    public async Task HandleAsync(SurgeryDischargedEvent @event)
    {
        var stayId = await stayFinder.FindInHouseByCatAsync(@event.CatId);
        var stay = stayId is null ? null : await stayRepository.GetByIdAsync(stayId.Value);
        if (stay is null || !stay.DischargeSurgery(@event.SurgeryId, @event.DischargedOn, @event.CorrelationId, @event.UserContext)) return;

        await stayRepository.UpdateAsync(stay, EventPublishOption.InMemoryPublishOnly);
    }
}

/// <summary>
/// A cat was away (at the clinic, or in theatre) and is back: care tasks that fell due while it was away are SKIPPED with a reason -- not marked missed (section 8.21). Idempotent: only pending
/// tasks are touched.
/// </summary>
public sealed class SkipCareTasksWhileAwayHandler(
    IBoardingTaskFinder taskFinder, IAggregateRootRepository<CareTaskAggregate> taskRepository, TimeProvider timeProvider)
    : IEventHandlerAsync<CatReturnedFromClinicEvent>, IEventHandlerAsync<StaySurgeryDischargedEvent>
{
    public Task HandleAsync(CatReturnedFromClinicEvent @event) => SkipAsync(@event.StayId, @event.CatId, @event.AwayFrom, @event.AwayTo, "Away at the clinic", @event.UserContext.UserId);

    public Task HandleAsync(StaySurgeryDischargedEvent @event) => SkipAsync(@event.StayId, @event.CatId, @event.AwayFrom, @event.AwayTo, "Away in surgery", @event.UserContext.UserId);

    private async Task SkipAsync(Guid stayId, Guid catId, DateTimeOffset from, DateTimeOffset to, string reason, Guid by)
    {
        var now = timeProvider.GetUtcNow();
        foreach (var id in await taskFinder.FindPendingInRangeAsync(stayId, catId, from, to))
        {
            var task = await taskRepository.GetByIdAsync(id);
            if (task is null) continue;

            task.Skip(by, now, reason);
            await taskRepository.UpdateAsync(task, EventPublishOption.InMemoryPublishOnly);
        }
    }
}
