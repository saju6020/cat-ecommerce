using CatEcommerce.Application.Commands.Clinic;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Clinic;

using static ClinicCommandHandlerSupport;

/// <summary>HO-APT-001 §8.6/§11.4 C1. Cat existence/ownership is checked before availability so a
/// customer never learns another customer's cat id exists (HO-ADM-001 §12.3's rule -- 404 via
/// <see cref="BusinessViolationCodes.CatNotFound"/>).</summary>
public sealed class PlaceSlotHoldCommandHandler(
    IAggregateRootRepository<AppointmentAggregate> appointmentRepository,
    IAggregateRootRepository<CatAggregate> catRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    IAppointmentSchedulingInputs schedulingInputs,
    IBusinessSettings businessSettings,
    IStaleHoldReleaser staleHoldReleaser,
    ICustomerHoldSerializer customerHoldSerializer,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<PlaceSlotHoldCommand>
{
    public async Task<CommandResponse> HandleAsync(PlaceSlotHoldCommand command)
    {
        var cat = await catRepository.GetByIdAsync(command.CatId);
        if (cat is null || cat.OwnerId != command.UserContext.UserId)
            return Violation(BusinessViolationCodes.CatNotFound, nameof(command.CatId), "Cat not found.", "CatAggregate");
        if (cat.LifeStatus != CatLifeStatus.Alive)
            return Violation(BusinessViolationCodes.CatNotBookable, nameof(command.CatId), ClinicMessages.CatNotBookable, "CatAggregate");

        var now = timeProvider.GetUtcNow();
        var outcome = await SlotClaim.ValidateAsync(
            new SlotClaim.Request(command.BranchId, command.ServiceId, command.StartsOn, command.PerformerId, command.CatId, null, IsStaff: false, now),
            branchRepository, schedulingInputs, businessSettings);

        if (!outcome.IsSuccess)
        {
            var forCustomer = SlotClaim.ForCustomer(outcome);
            return Violation(forCustomer.ErrorCode!, nameof(command.StartsOn), forCustomer.Message!);
        }

        var slot = outcome.Slot!;
        var service = outcome.Inputs!.Service!;

        // SECURITY gate fix (2026-09-28, HIGH): "one hold per customer" (§8.6 step 3,
        // booking.maxHoldsPerCustomer = 1, ADR-070) is not safe under concurrency with only an
        // unlocked read of the caller's own hold followed by an insert -- concurrent requests from
        // one account all see "no hold yet". Everything from here (expiring overlapping stale
        // holds, finding and releasing the caller's own previous hold, and placing the new one) now
        // runs inside ICustomerHoldSerializer's transaction, serialised per customer by a Postgres
        // transaction-scoped advisory lock (see that interface's own doc comment for why, and why
        // it does not deadlock with ADR-070's stale-hold release). The stale-hold expiry's own
        // per-hold saves still happen as separate SaveChangesAsync calls -- and so still send their
        // UPDATE statements to Postgres, in order, ahead of the new hold's INSERT below -- but now
        // participate in this same ambient transaction rather than auto-committing individually;
        // Postgres still sees each earlier statement's effect when it later checks the exclusion
        // constraint for the INSERT, since both are the same transaction (read-your-own-writes),
        // so the ordering fix HIGH A relied on is unaffected.
        return await customerHoldSerializer.RunSerializedAsync(command.UserContext.UserId, async () =>
        {
            await SlotClaim.ExpireOverlappingStaleHoldsAsync(
                staleHoldReleaser, appointmentRepository, slot, command.CatId, now, command.AppointmentId);

            var ownHoldId = await staleHoldReleaser.FindActiveHoldIdForCustomerAsync(command.UserContext.UserId, command.AppointmentId);
            if (ownHoldId is { } ownId)
            {
                var own = await appointmentRepository.GetByIdAsync(ownId);
                if (own is not null && own.Status == AppointmentStatus.Held) own.ReleaseHold(command.UserContext, now);
            }

            var holdMinutes = businessSettings.GetInt(SettingKeys.BookingSlotHoldMinutes, command.BranchId);
            var serviceSnapshot = new ServiceSnapshot(service.Name, service.Category, service.DurationMinutes, service.Price!);

            var appointment = new AppointmentAggregate(command.AppointmentId);
            try
            {
                appointment.PlaceHold(new SlotHoldDto(
                    command.BranchId, command.UserContext.UserId, command.CatId, command.ServiceId, serviceSnapshot,
                    slot.PerformerId, slot.RoomId, slot.StartsOn, slot.EndsOn, BookingChannel.Online, null,
                    now.AddMinutes(holdMinutes), command.UserContext, command.CorrelationId));
            }
            catch (DomainRuleViolationException ex)
            {
                return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
            }

            // Deliberately NOT caught here (§11.4 rule 4/ADR-068): a 23P01 from any of the three
            // exclusion constraints propagates through this transaction's rollback, out of
            // RunSerializedAsync, to UniqueConstraintExceptionFilter, which maps it to
            // 409 SlotNoLongerAvailable for a customer route. The handler never retries.
            await appointmentRepository.SaveAsync(appointment, EventPublishOption.InMemoryPublishOnly);

            return Success(new AppointmentHoldResult(appointment.Id, appointment.StartsOn!.Value, appointment.EndsOn!.Value, appointment.PerformerId, appointment.RoomId, appointment.HoldExpiresOn!.Value));
        });
    }
}

/// <summary>C2 -- own <c>Held</c> only; anything else is success, nothing written (§11.4).</summary>
public sealed class ReleaseSlotHoldCommandHandler(IAggregateRootRepository<AppointmentAggregate> appointmentRepository, TimeProvider timeProvider)
    : ICommandHandlerAsync<ReleaseSlotHoldCommand>
{
    public async Task<CommandResponse> HandleAsync(ReleaseSlotHoldCommand command)
    {
        var appointment = await appointmentRepository.GetByIdAsync(command.AppointmentId);
        if (appointment is null || appointment.CustomerId != command.UserContext.UserId || appointment.Status != AppointmentStatus.Held)
            return Success();

        appointment.ReleaseHold(command.UserContext, timeProvider.GetUtcNow());
        await appointmentRepository.UpdateAsync(appointment, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>C3 -- §8.6. Enqueues the confirmation and the two reminders; claims the waitlist entry
/// in the same save when the hold came from an offer (ADR-075's exception).</summary>
public sealed class ConfirmAppointmentCommandHandler(
    IAggregateRootRepository<AppointmentAggregate> appointmentRepository,
    IAggregateRootRepository<WaitlistEntryAggregate> waitlistRepository,
    IAggregateRootRepository<CatAggregate> catRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository,
    INotificationOutbox notificationOutbox,
    INotificationLinkBuilder linkBuilder,
    IBusinessSettings businessSettings,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<ConfirmAppointmentCommand>
{
    public async Task<CommandResponse> HandleAsync(ConfirmAppointmentCommand command)
    {
        var appointment = await appointmentRepository.GetByIdAsync(command.AppointmentId);
        if (appointment is null || !await IsCurrentOwnerAsync(catRepository, appointment, command.UserContext.UserId))
            return Violation(BusinessViolationCodes.HoldNotFound, nameof(command.AppointmentId), "Hold not found.");

        var now = timeProvider.GetUtcNow();

        // §8.6: the handler checks expiry first and persists ExpireHold as its own outcome,
        // rather than Confirm turning itself into a failed save.
        if (appointment.Status == AppointmentStatus.Held && now >= appointment.HoldExpiresOn)
        {
            appointment.ExpireHold(now);
            await appointmentRepository.UpdateAsync(appointment, EventPublishOption.InMemoryPublishOnly);
            return Violation(BusinessViolationCodes.SlotHoldExpired, nameof(command.AppointmentId), ClinicMessages.SlotHoldExpired);
        }

        var cat = await catRepository.GetByIdAsync(appointment.CatId);
        if (cat is null || cat.LifeStatus != CatLifeStatus.Alive)
            return Violation(BusinessViolationCodes.CatNotBookable, nameof(command.AppointmentId), ClinicMessages.CatNotBookable);

        try
        {
            appointment.Confirm(new AppointmentConfirmDto(now, command.ReasonForVisit, command.UserContext, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        WaitlistEntryAggregate? claimedEntry = null;
        if (appointment.WaitlistEntryId is { } waitlistEntryId)
        {
            claimedEntry = await waitlistRepository.GetByIdAsync(waitlistEntryId);
            claimedEntry?.Claim(new WaitlistClaimDto(command.UserContext, command.CorrelationId));
        }

        var ctx = await ClinicNotifications.LoadContextAsync(branchRepository, catRepository, staffRepository, appointment);
        ClinicNotifications.EnqueueConfirmed(notificationOutbox, linkBuilder, appointment, ctx, command.CorrelationId, command.UserContext);
        ClinicNotifications.EnqueueReminders(notificationOutbox, linkBuilder, businessSettings, appointment, ctx, now, command.CorrelationId, command.UserContext);

        await appointmentRepository.UpdateAsync(appointment, EventPublishOption.InMemoryPublishOnly);

        // The claimed entry was saved by the update above, but only the appointment's events were
        // published; publish the entry's own (backlog #4). Nothing is written by this call.
        if (claimedEntry is not null)
            await waitlistRepository.UpdateAsync(claimedEntry, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}
