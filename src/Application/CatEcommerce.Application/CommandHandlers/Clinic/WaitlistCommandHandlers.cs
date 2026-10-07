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

/// <summary>C14 -- §8.11 join rules.</summary>
public sealed class JoinWaitlistCommandHandler(
    IAggregateRootRepository<WaitlistEntryAggregate> waitlistRepository,
    IAggregateRootRepository<CatAggregate> catRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    IAppointmentSchedulingInputs schedulingInputs,
    IBusinessSettings businessSettings,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<JoinWaitlistCommand>
{
    public async Task<CommandResponse> HandleAsync(JoinWaitlistCommand command)
    {
        var cat = await catRepository.GetByIdAsync(command.CatId);
        if (cat is null || cat.OwnerId != command.UserContext.UserId)
            return Violation(BusinessViolationCodes.CatNotFound, nameof(command.CatId), "Cat not found.", "CatAggregate");
        if (cat.LifeStatus != CatLifeStatus.Alive)
            return Violation(BusinessViolationCodes.CatNotBookable, nameof(command.CatId), ClinicMessages.CatNotBookable);

        var branch = await branchRepository.GetByIdAsync(command.BranchId);
        if (branch is null || !branch.IsActive)
            return Violation(BusinessViolationCodes.ServiceNotBookable, nameof(command.BranchId), "Branch not found or inactive.");

        var now = timeProvider.GetUtcNow();
        var horizonDays = businessSettings.GetInt(SettingKeys.BookingHorizonDays, command.BranchId);
        var today = BranchCalendar.LocalDateOf(now, branch.TimeZoneId);
        if (command.LocalDate < today || command.LocalDate > today.AddDays(horizonDays))
            return Violation(BusinessViolationCodes.SlotBeyondHorizon, nameof(command.LocalDate), "This date is beyond the online booking horizon.");

        if (await waitlistRepository.ExistAsync(w =>
            w.CatId == command.CatId && w.ClinicServiceId == command.ServiceId && w.LocalDate == command.LocalDate &&
            (w.Status == WaitlistStatus.Waiting || w.Status == WaitlistStatus.Offered)))
        {
            return Violation(BusinessViolationCodes.AlreadyWaitlisted, nameof(command.CatId), "Already on the waitlist for this cat, service and date.");
        }

        var inputSet = await schedulingInputs.LoadAsync(new AppointmentSchedulingInputRequest(
            command.BranchId, command.ServiceId, command.LocalDate, true, null, command.CatId, command.PreferredPerformerId));
        var service = inputSet.Service;
        if (service is null || !service.IsActive || service.Price is null || !service.IsBookableOnline)
            return Violation(BusinessViolationCodes.ServiceNotBookable, nameof(command.ServiceId), ClinicMessages.ServiceNotBookable);

        var gridMinutes = businessSettings.GetInt(SettingKeys.BookingSlotIntervalMinutes, command.BranchId);
        var minimumNoticeMinutes = businessSettings.GetInt(SettingKeys.BookingMinimumNoticeMinutes, command.BranchId);

        var slots = AppointmentSlotFinder.FindSlots(new SlotRequest(
            new SlotServiceInfo(service.DurationMinutes, service.QualifiedRoleIds, service.RequiredRoomKind),
            command.LocalDate, branch.TimeZoneId, inputSet.Performers, inputSet.Rooms, inputSet.Busy,
            gridMinutes, now.AddMinutes(minimumNoticeMinutes), command.PreferredPerformerId));

        if (slots.Count > 0)
            return Violation(BusinessViolationCodes.SlotsStillAvailable, nameof(command.LocalDate), "Slots are still available -- book one instead.");

        var entry = new WaitlistEntryAggregate(command.WaitlistEntryId);
        entry.Join(new WaitlistJoinDto(command.BranchId, command.UserContext.UserId, command.CatId, command.ServiceId, command.LocalDate, command.PreferredPerformerId, now, command.UserContext, command.CorrelationId));

        await waitlistRepository.SaveAsync(entry, EventPublishOption.InMemoryPublishOnly);
        return Success(entry.Id);
    }
}

public sealed class WithdrawFromWaitlistCommandHandler(IAggregateRootRepository<WaitlistEntryAggregate> waitlistRepository)
    : ICommandHandlerAsync<WithdrawFromWaitlistCommand>
{
    public async Task<CommandResponse> HandleAsync(WithdrawFromWaitlistCommand command)
    {
        var entry = await waitlistRepository.GetByIdAsync(command.WaitlistEntryId);
        if (entry is null || entry.CustomerId != command.UserContext.UserId) return Success();

        entry.Withdraw(new WaitlistWithdrawDto(command.UserContext, command.CorrelationId));
        await waitlistRepository.UpdateAsync(entry, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>C15-offer -- §8.11/ADR-075. Dispatched by <c>ClinicWorker</c>, never by a controller.
/// Places the hold and marks the entry <c>Offered</c> in one save: the entry, loaded through its
/// own repository, is already tracked on this scoped <c>DbContext</c>, so mutating it and then
/// saving the new hold appointment commits both together.</summary>
public sealed class OfferWaitlistSlotCommandHandler(
    IAggregateRootRepository<WaitlistEntryAggregate> waitlistRepository,
    IAggregateRootRepository<AppointmentAggregate> appointmentRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    IAggregateRootRepository<CatAggregate> catRepository,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository,
    IAppointmentSchedulingInputs schedulingInputs,
    IBusinessSettings businessSettings,
    IQuietHoursCalendar quietHoursCalendar,
    IStaleHoldReleaser staleHoldReleaser,
    INotificationOutbox notificationOutbox,
    INotificationLinkBuilder linkBuilder,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<OfferWaitlistSlotCommand>
{
    public async Task<CommandResponse> HandleAsync(OfferWaitlistSlotCommand command)
    {
        var entry = await waitlistRepository.GetByIdAsync(command.WaitlistEntryId);
        if (entry is null || entry.Status != WaitlistStatus.Waiting) return Success();

        var branch = await branchRepository.GetByIdAsync(entry.BranchId);
        if (branch is null) return Success();

        var now = timeProvider.GetUtcNow();

        // §8.11: an offer is never made inside the branch's quiet hours -- nobody is offered a
        // slot at 03:00. The entry stays Waiting; the next pass tries again.
        if (await quietHoursCalendar.NextAllowedInstantAsync(now, entry.BranchId) is not null) return Success();

        var inputSet = await schedulingInputs.LoadAsync(new AppointmentSchedulingInputRequest(
            entry.BranchId, entry.ClinicServiceId, entry.LocalDate, true, null, entry.CatId, entry.PreferredPerformerId));
        var service = inputSet.Service;
        if (service is null || service.Price is null) return Success();

        var gridMinutes = businessSettings.GetInt(SettingKeys.BookingSlotIntervalMinutes, entry.BranchId);
        var minimumNoticeMinutes = businessSettings.GetInt(SettingKeys.BookingMinimumNoticeMinutes, entry.BranchId);

        var chosen = AppointmentSlotFinder.FindSlots(new SlotRequest(
            new SlotServiceInfo(service.DurationMinutes, service.QualifiedRoleIds, service.RequiredRoomKind),
            entry.LocalDate, branch.TimeZoneId, inputSet.Performers, inputSet.Rooms, inputSet.Busy,
            gridMinutes, now.AddMinutes(minimumNoticeMinutes), entry.PreferredPerformerId)).FirstOrDefault();

        if (chosen is null) return Success(); // no slot after all -- stays Waiting

        var claimMinutes = businessSettings.GetInt(SettingKeys.BookingWaitlistClaimMinutes, entry.BranchId);
        var holdExpiresOn = now.AddMinutes(claimMinutes);
        var serviceSnapshot = new ServiceSnapshot(service.Name, service.Category, service.DurationMinutes, service.Price);

        // ADR-070/§11.4 step 3: re-offering a freed slot must not 23P01 on the very hold that just
        // lapsed (or any other stale hold overlapping it) -- this is what makes I-G3's re-offer to
        // the next entry possible.
        await SlotClaim.ExpireOverlappingStaleHoldsAsync(
            staleHoldReleaser, appointmentRepository, chosen, entry.CatId, now, command.AppointmentId);

        var appointment = new AppointmentAggregate(command.AppointmentId);
        try
        {
            appointment.PlaceHold(new SlotHoldDto(
                entry.BranchId, entry.CustomerId, entry.CatId, entry.ClinicServiceId, serviceSnapshot,
                chosen.PerformerId, chosen.RoomId, chosen.StartsOn, chosen.EndsOn, BookingChannel.Waitlist, entry.Id,
                holdExpiresOn, command.UserContext, command.CorrelationId));
        }
        catch (DomainRuleViolationException)
        {
            return Success(); // stays Waiting
        }

        entry.Offer(new WaitlistOfferDto(appointment.Id, holdExpiresOn, command.UserContext, command.CorrelationId));

        var ctx = await ClinicNotifications.LoadContextAsync(branchRepository, catRepository, staffRepository, appointment);
        ClinicNotifications.EnqueueWaitlistOffer(notificationOutbox, linkBuilder, entry, appointment, ctx, command.CorrelationId, command.UserContext);

        // §11.4 rule 4/ADR-068: a 23P01 here means the slot went elsewhere -- propagates uncaught;
        // ClinicWorker logs it and the entry stays Waiting for the next pass, exactly like any
        // other race on this constraint. The handler does not retry.
        await appointmentRepository.SaveAsync(appointment, EventPublishOption.InMemoryPublishOnly);

        // The save above persisted the entry too (same DbContext, ADR-075), but the repository only
        // publishes the events of the root it was handed. The entry is tracked and unchanged now, so
        // this update writes nothing and exists to dispatch WaitlistSlotOfferedEvent (backlog #4).
        await waitlistRepository.UpdateAsync(entry, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

public sealed class LapseWaitlistOfferCommandHandler(IAggregateRootRepository<WaitlistEntryAggregate> waitlistRepository)
    : ICommandHandlerAsync<LapseWaitlistOfferCommand>
{
    public async Task<CommandResponse> HandleAsync(LapseWaitlistOfferCommand command)
    {
        var entry = await waitlistRepository.GetByIdAsync(command.WaitlistEntryId);
        if (entry is null || entry.Status != WaitlistStatus.Offered) return Success();

        entry.Lapse(new WaitlistLapseDto(command.UserContext, command.CorrelationId));
        await waitlistRepository.UpdateAsync(entry, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

public sealed class CloseStaleWaitlistEntriesCommandHandler(IAggregateRootRepository<WaitlistEntryAggregate> waitlistRepository)
    : ICommandHandlerAsync<CloseStaleWaitlistEntriesCommand>
{
    public async Task<CommandResponse> HandleAsync(CloseStaleWaitlistEntriesCommand command)
    {
        var entry = await waitlistRepository.GetByIdAsync(command.WaitlistEntryId);
        if (entry is null || entry.Status != WaitlistStatus.Waiting) return Success();

        entry.Close(new WaitlistCloseDto(command.UserContext, command.CorrelationId));
        await waitlistRepository.UpdateAsync(entry, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}
