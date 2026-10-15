using CatEcommerce.Domain.Aggregates.Prescription;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Account;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Prescription;

/// <summary>
/// HO-PHM-001 §11.6 / ADR-152 -- HO-ACC-001's <c>CustomerMergedEvent</c> is binding on every context that holds a customer id (ADR-039). Prescriptions, dispensings and diet
/// authorisations move from the duplicate to the survivor, one <c>UpdateAsync</c> each; each aggregate's own <c>ReassignCustomer</c> is a no-op once moved, so a re-delivered event
/// changes nothing. <b>Controlled-drug register entries and seals are never touched</b> -- they are append-only and hash-chained; reads resolve a merged customer id to the survivor
/// instead. Never enqueues a message.
/// </summary>
public sealed class ReassignPharmacyRecordsOnCustomerMergedHandler(
    IPharmacyRecordFinder finder,
    IAggregateRootRepository<PrescriptionAggregate> prescriptions,
    IAggregateRootRepository<DispensingAggregate> dispensings,
    IAggregateRootRepository<DietAuthorisationAggregate> diets)
    : IEventHandlerAsync<CustomerMergedEvent>
{
    public async Task HandleAsync(CustomerMergedEvent @event)
    {
        var duplicate = @event.DuplicateCustomerId;
        var survivor = @event.SurvivingCustomerId;

        foreach (var id in await finder.FindPrescriptionIdsAsync(duplicate, CancellationToken.None))
        {
            var prescription = await prescriptions.GetByIdAsync(id);
            if (prescription is null) continue;
            prescription.ReassignCustomer(survivor);
            await prescriptions.UpdateAsync(prescription, EventPublishOption.InMemoryPublishOnly);
        }

        foreach (var id in await finder.FindDispensingIdsAsync(duplicate, CancellationToken.None))
        {
            var dispensing = await dispensings.GetByIdAsync(id);
            if (dispensing is null) continue;
            dispensing.ReassignCustomer(survivor);
            await dispensings.UpdateAsync(dispensing, EventPublishOption.InMemoryPublishOnly);
        }

        foreach (var id in await finder.FindDietAuthorisationIdsAsync(duplicate, CancellationToken.None))
        {
            var diet = await diets.GetByIdAsync(id);
            if (diet is null) continue;
            diet.ReassignCustomer(survivor);
            await diets.UpdateAsync(diet, EventPublishOption.InMemoryPublishOnly);
        }
    }
}
