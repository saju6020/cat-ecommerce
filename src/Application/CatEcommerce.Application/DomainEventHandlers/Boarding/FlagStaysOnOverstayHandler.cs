using CatEcommerce.Domain.Aggregates.Boarding;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Boarding;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Boarding;

/// <summary>
/// Owner 2026-10-15: "Charge extra nights, never block". A cat checked out AFTER its booked departure is charged the extra nights, but its allocation is not extended, so the check-out can never be
/// refused by the exclusion constraint. A booking of the same unit that overlaps the overstay is FLAGGED for staff (<see cref="StayAttentionReason.UnitOverstayed"/>, on the dashboard attention list)
/// so they can move it. Idempotent (an already flagged stay is left alone); never enqueues a message.
/// </summary>
public sealed class FlagStaysOnOverstayHandler(IBoardingStayFinder stayFinder, IAggregateRootRepository<BoardingStayAggregate> stayRepository)
    : IEventHandlerAsync<BoardingStayCheckedOutEvent>
{
    public async Task HandleAsync(BoardingStayCheckedOutEvent @event)
    {
        if (!@event.Late || @event.OverstayUnitId is not { } unitId || @event.OverstayFrom is not { } from || @event.OverstayTo is not { } to) return;

        foreach (var id in await stayFinder.FindOnUnitOverlappingAsync(unitId, from, to, @event.StayId))
        {
            var stay = await stayRepository.GetByIdAsync(id);
            if (stay is null || stay.NeedsAttention) continue;

            stay.FlagAttention(StayAttentionReason.UnitOverstayed);
            await stayRepository.UpdateAsync(stay, EventPublishOption.InMemoryPublishOnly);
        }
    }
}
