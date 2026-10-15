using System.Globalization;
using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Visit;

using static VisitCommandHandlerSupport;

/// <summary>
/// C18 -- the follow-up (§8.9, US-CLN-18, ADR-131).
/// <list type="bullet">
/// <item><b>Appointment:</b> the client booked through HO-APT-001 first; this links it. The handler decides whether the
/// appointment is this cat's, <c>Booked</c> and in the future, and passes the verdict to the aggregate. Nothing is
/// enqueued -- the booking already sent its confirmation and reminders.</item>
/// <item><b>Reminder:</b> enqueues <c>visit.follow-up-due</c> to the cat's <i>current</i> owner, <c>SendAfter</c> 09:00
/// branch-local on <c>DueOn - clinical.followUpReminderLeadDays</c> (now, if already past), <c>NotAfter</c> the end of
/// <c>DueOn</c>, subject <c>VisitFollowUp</c> carrying the follow-up's <b>stamp</b>. <c>VisitFollowUpGuard</c> stops it if
/// the follow-up changed or was cleared, or the owner already booked. No reason is sent -- a reason is clinical.</item>
/// </list>
/// Enqueued here, in the handler, before the save (HO-NTF-001 ADR-056/057) -- never from an event handler.
/// </summary>
public sealed class SetFollowUpCommandHandler(
    IAggregateRootRepository<VisitAggregate> visitRepository,
    IAggregateRootRepository<AppointmentAggregate> appointmentRepository,
    IAggregateRootRepository<CatAggregate> catRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    IBusinessSettings businessSettings,
    INotificationOutbox outbox,
    INotificationLinkBuilder linkBuilder,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<SetFollowUpCommand>
{
    public async Task<CommandResponse> HandleAsync(SetFollowUpCommand command)
    {
        var visit = await visitRepository.GetByIdAsync(command.VisitId);
        if (visit is null || visit.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.VisitNotFound, nameof(command.VisitId), ClinicalMessages.VisitNotFound);

        var branch = await branchRepository.GetByIdAsync(visit.BranchId);
        var timeZoneId = branch?.TimeZoneId ?? "UTC";
        var now = timeProvider.GetUtcNow();
        var today = BranchCalendar.LocalDateOf(now, timeZoneId);
        var kind = AppointmentMapper.ParseEnum<FollowUpKind>(command.Kind);

        var appointmentValid = false;
        if (kind == FollowUpKind.Appointment && command.AppointmentId is { } appointmentId)
        {
            var appointment = await appointmentRepository.GetByIdAsync(appointmentId);
            appointmentValid = appointment is not null
                && appointment.CatId == visit.CatId
                && appointment.Status == AppointmentStatus.Booked
                && appointment.StartsOn is { } startsOn && startsOn > now;
        }

        try
        {
            visit.SetFollowUp(new FollowUpDto(
                kind, command.DueOn, command.AppointmentId, appointmentValid, today, command.UserContext.UserId, now, command.UserContext, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        if (kind == FollowUpKind.Reminder)
        {
            var cat = await catRepository.GetByIdAsync(visit.CatId);
            if (cat is not null)
            {
                var leadDays = businessSettings.GetInt(SettingKeys.ClinicalFollowUpReminderLeadDays);
                outbox.Enqueue(new NotificationRequestDto(
                    Guid.NewGuid(), NotificationTemplateCatalogue.VisitFollowUpDue, NotificationRecipientKind.Customer, cat.OwnerId,
                    new Dictionary<string, string>
                    {
                        ["catName"] = cat.Name,
                        ["dueOn"] = command.DueOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        ["bookingLink"] = linkBuilder.CatCareLink(cat.Id, "FollowUp"),
                    },
                    SubjectKind: NotificationSubjectKind.VisitFollowUp, SubjectId: visit.Id, BranchId: visit.BranchId,
                    NotAfter: FollowUpSchedule.NotAfter(command.DueOn, timeZoneId),
                    CorrelationId: command.CorrelationId, UserContext: command.UserContext,
                    SendAfter: FollowUpSchedule.SendAfter(command.DueOn, leadDays, now, timeZoneId),
                    SubjectStamp: visit.FollowUp!.Stamp));
            }
        }

        await visitRepository.UpdateAsync(visit, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>C18 -- clears the follow-up; a reminder already queued for it fails its guard and is never sent.</summary>
public sealed class ClearFollowUpCommandHandler(
    IAggregateRootRepository<VisitAggregate> visitRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<ClearFollowUpCommand>
{
    public async Task<CommandResponse> HandleAsync(ClearFollowUpCommand command)
    {
        var visit = await visitRepository.GetByIdAsync(command.VisitId);
        if (visit is null || visit.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.VisitNotFound, nameof(command.VisitId), ClinicalMessages.VisitNotFound);

        try
        {
            visit.ClearFollowUp(new FollowUpClearDto(timeProvider.GetUtcNow(), command.UserContext, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await visitRepository.UpdateAsync(visit, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}
