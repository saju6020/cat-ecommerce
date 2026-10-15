using CatEcommerce.Application.Commands.Prescription;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Prescription;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Prescription.PharmacyHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Prescription;

/// <summary>
/// HO-PHM-001 §11.4 C12 (US-PHM-06, ADR-148) -- the owner asks for a repeat. The prescription is reached THROUGH its cat's current owner: anything that is not the caller's is
/// <c>PrescriptionNotFound</c> (a <c>404</c>; no hint that it exists). The rule is the aggregate's <c>RequestRepeat</c> -- the same function the portal list uses for
/// <c>canRequest</c>, so no button is offered that this would refuse. Outside the vet's window the refusal carries the date and the booking link (US-PHM-06 AC2).
/// </summary>
public sealed class RequestRepeatCommandHandler(
    IAggregateRootRepository<PrescriptionAggregate> repository,
    IAggregateRootRepository<CatAggregate> catRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    IPharmacyClock clock,
    INotificationLinkBuilder links,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RequestRepeatCommand>
{
    public async Task<CommandResponse> HandleAsync(RequestRepeatCommand command)
    {
        var user = command.UserContext!;
        var prescription = await repository.GetByIdAsync(command.PrescriptionId);
        var cat = prescription is null ? null : await catRepository.GetByIdAsync(prescription.CatId);
        if (prescription is null || cat is null || cat.OwnerId != user.UserId)
            return Violation(BusinessViolationCodes.PrescriptionNotFound, nameof(command.PrescriptionId), "Prescription not found.");

        if (cat.LifeStatus != CatLifeStatus.Alive)
            return Violation(BusinessViolationCodes.CatNotTreatable, nameof(command.PrescriptionId), $"{cat.Name} is no longer with us.");

        var branch = await branchRepository.GetByIdAsync(command.PreferredBranchId);
        if (branch is null || !branch.IsActive)
            return Violation(BusinessViolationCodes.BranchNotFound, nameof(command.PreferredBranchId), "Choose one of our branches.");

        var today = await clock.LocalTodayAsync(branch.Id, CancellationToken.None);
        try
        {
            prescription.RequestRepeat(new RepeatRequestDto(
                command.RepeatRequestId, user.UserId, branch.Id, command.Note, today, timeProvider.GetUtcNow(), command.ToPharmacyActor()));
        }
        catch (DomainRuleViolationException ex)
        {
            var refusal = Violation(ex);
            if (ex.ErrorCode == BusinessViolationCodes.PrescriptionValidityEnded) refusal.Result = new RepeatRefusal(links.CatCareLink(cat.Id, "FollowUp"));
            return refusal;
        }

        await repository.UpdateAsync(prescription, EventPublishOption.InMemoryPublishOnly);
        return Success(new RequestedRepeat(command.RepeatRequestId));
    }
}

/// <summary>HO-PHM-001 §11.4 C13 -- the owner withdraws their OWN open request. Not theirs, not open, not found: success, nothing written (idempotent, and it reveals nothing).</summary>
public sealed class WithdrawRepeatRequestCommandHandler(
    IAggregateRootRepository<PrescriptionAggregate> repository, IAggregateRootRepository<CatAggregate> catRepository, TimeProvider timeProvider)
    : ICommandHandlerAsync<WithdrawRepeatRequestCommand>
{
    public async Task<CommandResponse> HandleAsync(WithdrawRepeatRequestCommand command)
    {
        var user = command.UserContext!;
        var prescription = await repository.GetByIdAsync(command.PrescriptionId);
        var cat = prescription is null ? null : await catRepository.GetByIdAsync(prescription.CatId);
        if (prescription is null || cat is null || cat.OwnerId != user.UserId) return Success();

        if (!prescription.RepeatRequests.Any(r => r.Id == command.RepeatRequestId && r.RequestedBy == user.UserId && r.IsOpen)) return Success();

        prescription.WithdrawRepeatRequest(command.RepeatRequestId, user.UserId, timeProvider.GetUtcNow());
        await repository.UpdateAsync(prescription, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>HO-PHM-001 §11.4 C14 (BR-6) -- a vet declines an open repeat request. A clinical judgement, so Vet only (defence in depth behind the filter); the owner -- the cat's
/// CURRENT owner -- is told, with the vet's plain-text reason inserted as a literal text node.</summary>
public sealed class DeclineRepeatRequestCommandHandler(
    IAggregateRootRepository<PrescriptionAggregate> repository,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository,
    IAggregateRootRepository<CatAggregate> catRepository,
    INotificationOutbox outbox,
    INotificationLinkBuilder links,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<DeclineRepeatRequestCommand>
{
    public async Task<CommandResponse> HandleAsync(DeclineRepeatRequestCommand command)
    {
        var user = command.UserContext!;
        var caller = await staffRepository.GetByIdAsync(user.UserId);
        if (caller is null || !caller.RoleIds.Contains(StaffRoles.Vet))
            return Violation(BusinessViolationCodes.PrescribingNotPermitted, nameof(command.UserContext), "Only a veterinarian may decline a repeat request.");

        var prescription = await repository.GetByIdAsync(command.PrescriptionId);
        if (prescription is null)
            return Violation(BusinessViolationCodes.PrescriptionNotFound, nameof(command.PrescriptionId), "Prescription not found.");

        try { prescription.DeclineRepeatRequest(command.RepeatRequestId, user.UserId, command.Reason, timeProvider.GetUtcNow(), command.ToPharmacyActor()); }
        catch (DomainRuleViolationException ex) { return Violation(ex); }

        if (await catRepository.GetByIdAsync(prescription.CatId) is { } cat)
        {
            PharmacyNotifications.EnqueueRepeatDeclined(
                outbox, links, cat.OwnerId, cat.Id, cat.Name, prescription.MedicineSnapshot.Name, command.Reason.Trim(), command.CorrelationId, user);
        }

        await repository.UpdateAsync(prescription, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>
/// HO-PHM-001 §8.8 / §11.4 C15 (US-PHM-06 AC2, BR-6, ADR-148) -- renew a prescription: the SAME <see cref="PrescribingSteps"/> as issue (so the full safety assessment, the recent-weight
/// rule and the acknowledgement rules run again on today's facts), then the new prescription is issued, the old one is linked to it and -- if still running -- cancelled "Renewed as
/// RX-...". ONE save (a recorded rule-1 exception): two prescriptions, one commit.
/// </summary>
public sealed class RenewPrescriptionCommandHandler(
    PrescribingPorts ports,
    IAggregateRootRepository<PrescriptionAggregate> repository,
    IAggregateRootRepository<CatAggregate> catRepository,
    INotificationOutbox outbox,
    INotificationLinkBuilder links)
    : ICommandHandlerAsync<RenewPrescriptionCommand>
{
    public async Task<CommandResponse> HandleAsync(RenewPrescriptionCommand command)
    {
        var original = await repository.GetByIdAsync(command.FromPrescriptionId);
        if (original is null)
            return Violation(BusinessViolationCodes.PrescriptionNotFound, nameof(command.FromPrescriptionId), "Prescription not found.");

        if (original.RenewedByPrescriptionId is not null)
            return Violation(BusinessViolationCodes.PrescriptionAlreadyRenewed, nameof(command.FromPrescriptionId), "This prescription has already been renewed.");

        var outcome = await PrescribingSteps.RunAsync(ports, command, original.CatId, original.MedicineId, command.VisitId, original.VisitId, original.Id);
        if (outcome.Failure is not null) return outcome.Failure;

        var renewal = new PrescriptionAggregate(command.NewPrescriptionId);
        try
        {
            renewal.Issue(outcome.Issue!);
            var openRequest = original.OpenRepeatRequest;
            original.MarkRenewed(renewal.Id);

            if (PrescriptionActionRules.IsAllowed(original.Status, PrescriptionActions.Cancel))
            {
                var reason = $"Renewed as {renewal.PrescriptionNumber}";
                original.Cancel(command.UserContext!.UserId, reason, outcome.Issue!.Now, command.ToPharmacyActor());

                if (openRequest is not null && await catRepository.GetByIdAsync(original.CatId) is { } cat)
                    PharmacyNotifications.EnqueueRepeatDeclined(outbox, links, cat.OwnerId, cat.Id, cat.Name, original.MedicineSnapshot.Name, reason, command.CorrelationId, command.UserContext);
            }
        }
        catch (DomainRuleViolationException ex) { return Violation(ex); }

        await repository.SaveAsync(renewal, EventPublishOption.InMemoryPublishOnly);
        return Success(new IssuedPrescription(renewal.Id, renewal.PrescriptionNumber));
    }
}

/// <summary>HO-PHM-001 §11.4 C16 (ADR-142) -- the worker records an expiry. "Today" is the PRESCRIPTION's branch-local date; not expirable (already expired, cancelled, completed, still valid) is
/// success with nothing written.</summary>
public sealed class ExpirePrescriptionCommandHandler(IAggregateRootRepository<PrescriptionAggregate> repository, IPharmacyClock clock)
    : ICommandHandlerAsync<ExpirePrescriptionCommand>
{
    public async Task<CommandResponse> HandleAsync(ExpirePrescriptionCommand command)
    {
        var prescription = await repository.GetByIdAsync(command.PrescriptionId);
        if (prescription is null) return Success();

        var today = await clock.LocalTodayAsync(prescription.BranchId, CancellationToken.None);
        if (!prescription.CanExpire(today)) return Success();

        prescription.Expire(today, command.ToPharmacyActor());
        await repository.UpdateAsync(prescription, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}
