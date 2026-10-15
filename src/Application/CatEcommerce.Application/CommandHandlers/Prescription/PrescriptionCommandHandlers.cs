using CatEcommerce.Application.Commands.Prescription;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Prescription;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Prescription.PharmacyHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Prescription;

/// <summary>
/// HO-PHM-001 §11.4 C5 (US-PHM-01, 02, 03; US-CLN-22; AC-18) -- issue a prescription. Everything that decides whether it MAY be issued is in
/// <see cref="PrescribingSteps"/> (shared with renew); the aggregate decides the rest of its own invariants and is saved in one write. Nothing here
/// enqueues a message and nothing stock-related happens: issuing moves no stock.
/// </summary>
public sealed class IssuePrescriptionCommandHandler(PrescribingPorts ports, IAggregateRootRepository<PrescriptionAggregate> repository)
    : ICommandHandlerAsync<IssuePrescriptionCommand>
{
    public async Task<CommandResponse> HandleAsync(IssuePrescriptionCommand command)
    {
        var outcome = await PrescribingSteps.RunAsync(ports, command, command.CatId, command.MedicineId, command.VisitId, remoteRenewalVisitId: null, renewedFrom: null);
        if (outcome.Failure is not null) return outcome.Failure;

        var prescription = new PrescriptionAggregate(command.PrescriptionId);
        try { prescription.Issue(outcome.Issue!); }
        catch (DomainRuleViolationException ex) { return Violation(ex); }

        await repository.SaveAsync(prescription, EventPublishOption.InMemoryPublishOnly);
        return Success(new IssuedPrescription(prescription.Id, prescription.PrescriptionNumber));
    }
}

/// <summary>
/// HO-PHM-001 §11.4 C6 -- a vet cancels a prescription. The reason is required; an open repeat request is declined by the aggregate with the same reason and
/// the owner is told (<c>pharmacy.repeat-declined</c> is enqueued here, before the save -- HO-NTF-001 rule 1).
/// </summary>
public sealed class CancelPrescriptionCommandHandler(
    IAggregateRootRepository<PrescriptionAggregate> repository,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository,
    IAggregateRootRepository<CatAggregate> catRepository,
    INotificationOutbox outbox,
    INotificationLinkBuilder links,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<CancelPrescriptionCommand>
{
    public async Task<CommandResponse> HandleAsync(CancelPrescriptionCommand command)
    {
        var caller = await staffRepository.GetByIdAsync(command.UserContext!.UserId);
        if (caller is null || !caller.RoleIds.Contains(StaffRoles.Vet))
            return Violation(BusinessViolationCodes.PrescribingNotPermitted, nameof(command.UserContext), "Only a veterinarian may cancel a prescription.");

        var prescription = await repository.GetByIdAsync(command.PrescriptionId);
        if (prescription is null)
            return Violation(BusinessViolationCodes.PrescriptionNotFound, nameof(command.PrescriptionId), "Prescription not found.");

        var openRequest = prescription.OpenRepeatRequest;
        try { prescription.Cancel(command.UserContext.UserId, command.Reason, timeProvider.GetUtcNow(), command.ToPharmacyActor()); }
        catch (DomainRuleViolationException ex) { return Violation(ex); }

        if (openRequest is not null && await catRepository.GetByIdAsync(prescription.CatId) is { } cat)
        {
            PharmacyNotifications.EnqueueRepeatDeclined(
                outbox, links, cat.OwnerId, cat.Id, cat.Name, prescription.MedicineSnapshot.Name, command.Reason.Trim(), command.CorrelationId, command.UserContext);
        }

        await repository.UpdateAsync(prescription, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}
