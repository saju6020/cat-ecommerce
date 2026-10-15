using CatEcommerce.Application.Commands.Surgery;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.Aggregates.Surgery;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Surgery.SurgeryHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Surgery;

/// <summary>
/// HO-SRG-001 §8.5 / §11.4 C2 (US-SRG-01, US-SRG-02, ADR-158) -- book a surgery against a theatre, a surgeon and an assistant. In order: the service is a priced, active theatre
/// procedure; the cat is alive, the customer's, of a sex the procedure fits and not already neutered (unless the vet confirms); EVERY clash is named (theatre, surgeon, assistant,
/// cat) in one 400; the price is the standard price at now or a campaign's; stale holds are expired; then the appointment and the surgery are written in ONE save and the booked
/// and pre-op messages are enlisted with them. A <c>23P01</c> at commit means another booking won the race -- the filter maps it to 409; the handler never retries (ADR-068).
/// </summary>
public sealed class BookSurgeryCommandHandler(
    IAggregateRootRepository<SurgeryAggregate> surgeryRepository,
    IAggregateRootRepository<AppointmentAggregate> appointmentRepository,
    IAggregateRootRepository<CatAggregate> catRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    IAggregateRootRepository<SurgeryProtocolAggregate> protocolRepository,
    ISurgeryAppointmentEnlister appointmentEnlister,
    ISurgerySchedulingInputs schedulingInputs,
    IStaleHoldReleaser staleHoldReleaser,
    ISurgeryCampaignPricing campaignPricing,
    INotificationOutbox notificationOutbox,
    INotificationLinkBuilder linkBuilder,
    IBusinessSettings businessSettings,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<BookSurgeryCommand>
{
    public async Task<CommandResponse> HandleAsync(BookSurgeryCommand command)
    {
        var user = command.UserContext!;
        var now = timeProvider.GetUtcNow();
        var startsOn = command.StartsOn.ToUniversalTime(); // Npgsql accepts only offset-0 instants

        var branch = await branchRepository.GetByIdAsync(command.BranchId);
        if (branch is null || !branch.IsActive)
            return Violation(BusinessViolationCodes.ServiceNotBookable, nameof(command.BranchId), "Branch not found or inactive.");

        // 1 + 4. The service, and the clashes (judged once the cat is known).
        var claim = await SurgerySlotClaim.ValidateAsync(
            new SurgerySlotClaim.Request(command.BranchId, command.ServiceId, startsOn, command.SurgeonId, command.AssistantId, command.TheatreId, command.CatId, null, now),
            branch.TimeZoneId, schedulingInputs, businessSettings);
        if (claim.Error is { } serviceError)
            return Violation(serviceError.Code, nameof(command.ServiceId), serviceError.Message);

        var service = claim.Inputs!.Service!;

        // 2 + 3. The cat.
        var cat = await catRepository.GetByIdAsync(command.CatId);
        if (cat is null || cat.OwnerId != command.CustomerId || cat.LifeStatus != CatLifeStatus.Alive)
            return Violation(BusinessViolationCodes.CatNotBookable, nameof(command.CatId), ClinicMessages.CatNotBookable);

        var isSpay = service.Category == ServiceCategory.Spay;
        var isNeuter = service.Category == ServiceCategory.Neuter;
        if ((isSpay && cat.Sex == CatSex.Male) || (isNeuter && cat.Sex == CatSex.Female))
            return Violation(BusinessViolationCodes.ProcedureSexMismatch, nameof(command.ServiceId),
                isSpay ? "A spay cannot be booked for a male cat." : "A neuter cannot be booked for a female cat.");

        if ((isSpay || isNeuter) && cat.NeuterStatus == NeuterStatus.Neutered && !command.ConfirmNeuterStatusDiscrepancy)
            return Violation(BusinessViolationCodes.CatAlreadyNeutered, nameof(command.ConfirmNeuterStatusDiscrepancy),
                $"{cat.Name} is already recorded as neutered. Confirm the discrepancy to book anyway.");

        // 4. Every clash, in the fixed order, as one violation each.
        if (claim.Clashes.Count > 0)
            return Violations(SurgerySlotClaim.AsViolations(claim.Clashes));

        // 5. The price: a campaign's (phase G) or the standard price at now.
        var price = await campaignPricing.ResolveAsync(
            new SurgeryPricingRequest(command.BranchId, command.ServiceId, claim.LocalDate, service.Price!, command.CampaignId, command.CampaignEligibilityNote));
        if (price.Refusal is { } refusal)
            return Violation(refusal.Code, nameof(command.CampaignId), refusal.Message);

        // 6. Stale holds (their own saves, so before anything is tracked), then the appointment and the surgery.
        var endsOn = startsOn.AddMinutes(claim.DurationMinutes);
        await SurgerySlotClaim.ExpireStaleHoldsAsync(
            staleHoldReleaser, appointmentRepository, command.SurgeonId, command.AssistantId, command.TheatreId, command.CatId, startsOn, endsOn, now, null);

        var admitBy = startsOn.AddMinutes(-businessSettings.GetInt(SettingKeys.SurgeryAdmissionLeadMinutes, command.BranchId));
        var fasting = await SurgeryProtocols.FastingInstructionsAsync(protocolRepository, command.ServiceId, admitBy, branch.TimeZoneId);

        var appointment = new AppointmentAggregate(command.AppointmentId);
        var surgery = new SurgeryAggregate(command.SurgeryId);
        try
        {
            appointment.BookForSurgery(new SurgeryAppointmentDto(
                surgery.Id, command.BranchId, command.CustomerId, command.CatId, command.ServiceId,
                new ServiceSnapshot(service.Name, service.Category, service.DurationMinutes, service.Price!),
                command.SurgeonId, command.AssistantId, command.TheatreId, startsOn, endsOn, user));

            surgery.Schedule(new SurgeryScheduleDto(
                command.BranchId, command.CatId, command.CustomerId, command.ServiceId, new ProcedureSnapshot(service.Name, service.Category, service.DurationMinutes), price.Snapshot!,
                appointment.Id, command.VisitId, command.TheatreId, command.SurgeonId, command.AssistantId, startsOn, endsOn, admitBy, fasting, now, user, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex);
        }

        // 7. The messages, enlisted with the change (HO-NTF-001 ADR-056/057), then ONE save for the appointment and the surgery (ADR-158).
        var ctx = new SurgeryNotifications.MessageContext(cat.Name, branch.Name, branch.AddressLine ?? "", branch.TimeZoneId);
        SurgeryNotifications.EnqueueBooked(notificationOutbox, linkBuilder, surgery, ctx, command.CorrelationId, user);
        SurgeryNotifications.EnqueuePreOpReminder(notificationOutbox, linkBuilder, businessSettings, surgery, ctx, now, command.CorrelationId, user);

        appointmentEnlister.Enlist(appointment);
        await surgeryRepository.SaveAsync(surgery, EventPublishOption.InMemoryPublishOnly);
        return Success(surgery.Id);
    }
}

/// <summary>
/// HO-SRG-001 §11.4 C3 (US-SRG-01) -- reschedule through the SAME checks as booking (<see cref="SurgerySlotClaim"/>), excluding the surgery's own appointment. The surgery and
/// its appointment change in one save; the old pre-op reminder fails the stamp guard because the stamp changed, and a fresh one is enqueued with the rescheduled notice.
/// <c>ExpectedVersion</c> is checked against the version the caller loaded, BEFORE the aggregate bumps it.
/// </summary>
public sealed class RescheduleSurgeryCommandHandler(
    IAggregateRootRepository<SurgeryAggregate> surgeryRepository,
    IAggregateRootRepository<AppointmentAggregate> appointmentRepository,
    IAggregateRootRepository<CatAggregate> catRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    IAggregateRootRepository<SurgeryProtocolAggregate> protocolRepository,
    ISurgerySchedulingInputs schedulingInputs,
    IStaleHoldReleaser staleHoldReleaser,
    INotificationOutbox notificationOutbox,
    INotificationLinkBuilder linkBuilder,
    IBusinessSettings businessSettings,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RescheduleSurgeryCommand>
{
    public async Task<CommandResponse> HandleAsync(RescheduleSurgeryCommand command)
    {
        var user = command.UserContext!;
        var now = timeProvider.GetUtcNow();
        var startsOn = command.StartsOn.ToUniversalTime();

        var surgery = await LoadSurgeryAsync(surgeryRepository, command.SurgeryId, command.BranchId);
        if (surgery is null)
            return Violation(BusinessViolationCodes.SurgeryNotFound, nameof(command.SurgeryId), SurgeryMessages.SurgeryNotFound);

        if (surgery.Version != command.ExpectedVersion)
            return Violation(BusinessViolationCodes.StaleVersion, nameof(command.ExpectedVersion), "This surgery has changed since you loaded it. Reload and try again.");

        if (!SurgeryTransitionRules.IsAllowed(surgery.Status, SurgeryTransitionRules.Reschedule))
            return TransitionViolation(surgery.Status, SurgeryTransitionRules.Reschedule);

        var branch = await branchRepository.GetByIdAsync(surgery.BranchId);
        var appointment = await appointmentRepository.GetByIdAsync(surgery.AppointmentId);
        if (branch is null || appointment is null)
            return Violation(BusinessViolationCodes.SurgeryNotFound, nameof(command.SurgeryId), SurgeryMessages.SurgeryNotFound);

        var claim = await SurgerySlotClaim.ValidateAsync(
            new SurgerySlotClaim.Request(surgery.BranchId, surgery.ClinicServiceId, startsOn, command.SurgeonId, command.AssistantId, command.TheatreId, surgery.CatId, appointment.Id, now,
                surgery.Procedure.DurationMinutes),
            branch.TimeZoneId, schedulingInputs, businessSettings);
        if (claim.Error is { } serviceError)
            return Violation(serviceError.Code, nameof(surgery.ClinicServiceId), serviceError.Message);
        if (claim.Clashes.Count > 0)
            return Violations(SurgerySlotClaim.AsViolations(claim.Clashes));

        var endsOn = startsOn.AddMinutes(surgery.Procedure.DurationMinutes);
        await SurgerySlotClaim.ExpireStaleHoldsAsync(
            staleHoldReleaser, appointmentRepository, command.SurgeonId, command.AssistantId, command.TheatreId, surgery.CatId, startsOn, endsOn, now, appointment.Id);

        var previousStartsOn = surgery.StartsOn;
        var admitBy = startsOn.AddMinutes(-businessSettings.GetInt(SettingKeys.SurgeryAdmissionLeadMinutes, surgery.BranchId));
        var fasting = await SurgeryProtocols.FastingInstructionsAsync(protocolRepository, surgery.ClinicServiceId, admitBy, branch.TimeZoneId);

        try
        {
            surgery.Reschedule(new SurgeryRescheduleDto(
                command.TheatreId, command.SurgeonId, command.AssistantId, startsOn, endsOn, admitBy, fasting, claim.LocalDate,
                businessSettings.GetInt(SettingKeys.SurgeryWeightValidityDays, surgery.BranchId), now, user, command.CorrelationId));
            appointment.MoveForSurgery(new SurgeryAppointmentMoveDto(surgery.Id, command.SurgeonId, command.AssistantId, command.TheatreId, startsOn, endsOn, user));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex);
        }

        if (surgery.StartsOn != previousStartsOn)
        {
            var cat = await catRepository.GetByIdAsync(surgery.CatId);
            var ctx = new SurgeryNotifications.MessageContext(cat?.Name ?? "your cat", branch.Name, branch.AddressLine ?? "", branch.TimeZoneId);
            SurgeryNotifications.EnqueueRescheduled(notificationOutbox, linkBuilder, surgery, ctx, previousStartsOn, command.CorrelationId, user);
            SurgeryNotifications.EnqueuePreOpReminder(notificationOutbox, linkBuilder, businessSettings, surgery, ctx, now, command.CorrelationId, user);
        }

        // The appointment is tracked and modified; this save commits it with the surgery (ADR-158).
        await surgeryRepository.UpdateAsync(surgery, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>HO-SRG-001 §11.4 C4 (US-SRG-01) -- cancel a Scheduled or PreOp surgery: the surgery and its appointment in one save, the slot freed, the owner told.</summary>
public sealed class CancelSurgeryCommandHandler(
    IAggregateRootRepository<SurgeryAggregate> surgeryRepository,
    IAggregateRootRepository<AppointmentAggregate> appointmentRepository,
    IAggregateRootRepository<CatAggregate> catRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    INotificationOutbox notificationOutbox,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<CancelSurgeryCommand>
{
    public async Task<CommandResponse> HandleAsync(CancelSurgeryCommand command)
    {
        var user = command.UserContext!;
        var now = timeProvider.GetUtcNow();

        var surgery = await LoadSurgeryAsync(surgeryRepository, command.SurgeryId, command.BranchId);
        if (surgery is null)
            return Violation(BusinessViolationCodes.SurgeryNotFound, nameof(command.SurgeryId), SurgeryMessages.SurgeryNotFound);

        var appointment = await appointmentRepository.GetByIdAsync(surgery.AppointmentId);
        if (appointment is null)
            return Violation(BusinessViolationCodes.SurgeryNotFound, nameof(command.SurgeryId), SurgeryMessages.SurgeryNotFound);

        try
        {
            surgery.Cancel(new SurgeryCancelDto(command.Reason, now, user, command.CorrelationId));
            appointment.CancelForSurgery(new SurgeryAppointmentCancelDto(surgery.Id, surgery.CancellationReason!, now, user));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex);
        }

        var branch = await branchRepository.GetByIdAsync(surgery.BranchId);
        var cat = await catRepository.GetByIdAsync(surgery.CatId);
        SurgeryNotifications.EnqueueCancelled(
            notificationOutbox, surgery, new SurgeryNotifications.MessageContext(cat?.Name ?? "your cat", branch?.Name ?? "the clinic", branch?.AddressLine ?? "", branch?.TimeZoneId ?? "UTC"),
            command.CorrelationId, user);

        await surgeryRepository.UpdateAsync(surgery, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}
