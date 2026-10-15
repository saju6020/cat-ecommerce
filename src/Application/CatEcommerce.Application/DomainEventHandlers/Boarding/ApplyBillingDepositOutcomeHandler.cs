using CatEcommerce.Domain.Aggregates.Boarding;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Billing;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Boarding;

/// <summary>
/// HO-BRD-001 section 11.6 (ADR-223): Billing rejected a payment. If it was a boarding deposit awaiting approval, the stay returns from <c>PendingApproval</c> to
/// <c>Awaiting</c> with its due time unchanged, so it may lapse on the next worker pass. Mutates only the stay; sends no message (HO-NTF-001 ADR-057). Idempotent.
/// </summary>
public sealed class ApplyBillingDepositOutcomeHandler(
    IBoardingDepositLedger depositLedger, IAggregateRootRepository<BoardingStayAggregate> stayRepository)
    : IEventHandlerAsync<PaymentRejectedEvent>
{
    public async Task HandleAsync(PaymentRejectedEvent @event)
    {
        var stayId = await depositLedger.GetStayIdOfPaymentAsync(@event.PaymentId);
        if (stayId is null) return;

        var stay = await stayRepository.GetByIdAsync(stayId.Value);
        if (stay is null) return;

        var ledger = await depositLedger.GetAsync(stay.Id);
        if (ledger.PendingApproval > 0m) return; // another payment still awaits approval

        stay.ClearDepositPendingApproval();
        await stayRepository.UpdateAsync(stay, EventPublishOption.InMemoryPublishOnly);
    }
}
