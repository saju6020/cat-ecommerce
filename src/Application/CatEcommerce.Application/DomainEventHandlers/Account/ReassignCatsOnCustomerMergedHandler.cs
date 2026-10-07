using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Events.Account;
using CatEcommerce.Domain.Models;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Account;

/// <summary>
/// HO-ACC-001 §11.7 / ADR-039. The platform's <c>IAggregateRootRepository&lt;T&gt;.GetByFilterAsync</c>
/// returns a single match, not a list, so cats are moved one at a time: each reassignment changes
/// the matched cat's <c>OwnerId</c>, so the next call naturally finds the next remaining one. The
/// loop terminates when none match -- which is also what makes a re-send after a part-way failure
/// idempotent (it moves whatever is left, and does nothing once nothing is left).
///
/// <c>.ai/domain-events-and-outbox.md</c> §3: if this handler throws -- partway through, or before
/// moving a single cat -- the merge command's own aggregate save has **already committed** (the
/// duplicate customer is `Status = Merged`), and that failure still fails the overall request back
/// to the caller even though the merge itself cannot be rolled back. The merge is deliberately not
/// atomic with cat reassignment (ADR-039); the documented recovery is re-sending the identical
/// merge command, which resumes moving whatever cats are still owned by the duplicate.
/// </summary>
public sealed class ReassignCatsOnCustomerMergedHandler(
    ILogger<ReassignCatsOnCustomerMergedHandler> logger,
    IAggregateRootRepository<CatAggregate> catRepository)
    : IEventHandlerAsync<CustomerMergedEvent>
{
    public async Task HandleAsync(CustomerMergedEvent @event)
    {
        logger.LogInformation(
            "Reassigning cats from {DuplicateCustomerId} to {SurvivingCustomerId}. CorrelationId: {CorrelationId}",
            @event.DuplicateCustomerId, @event.SurvivingCustomerId, @event.CorrelationId);

        CatAggregate? cat;
        while ((cat = await catRepository.GetByFilterAsync(c => c.OwnerId == @event.DuplicateCustomerId)) is not null)
        {
            cat.ReassignOwnerOnCustomerMerge(new CatOwnerMergeReassignmentDto(
                @event.DuplicateCustomerId, @event.SurvivingCustomerId, @event.UserContext, @event.CorrelationId));

            await catRepository.UpdateAsync(cat, EventPublishOption.InMemoryPublishOnly);
        }
    }
}
