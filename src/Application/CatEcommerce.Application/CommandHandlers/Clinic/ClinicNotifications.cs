using System.Globalization;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Clinic;

/// <summary>
/// HO-APT-001 §8.9 (ADR-074) -- builds and enqueues the five appointment/waitlist messages.
/// Called from a command handler, before its aggregate save, through <see cref="INotificationOutbox"/>
/// (HO-NTF-001 ADR-056/057) -- never from a domain event handler.
/// </summary>
public static class ClinicNotifications
{
    public static string FormatLocal(DateTimeOffset instant, string timeZoneId)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var local = TimeZoneInfo.ConvertTime(instant, timeZone);
        return local.ToString("ddd d MMM yyyy, HH:mm", CultureInfo.InvariantCulture);
    }

    public sealed record MessageContext(string CatName, string BranchName, string BranchAddress, string PerformerName, string TimeZoneId);

    public static void EnqueueConfirmed(INotificationOutbox outbox, INotificationLinkBuilder linkBuilder, AppointmentAggregate appointment, MessageContext ctx, Guid correlationId, UserContext userContext)
    {
        var variables = BaseVariables(appointment, ctx);
        variables["manageLink"] = linkBuilder.AppointmentLink(appointment.Id);

        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.AppointmentConfirmed, NotificationRecipientKind.Customer, appointment.CustomerId,
            variables, BranchId: appointment.BranchId, CorrelationId: correlationId, UserContext: userContext));
    }

    /// <summary>§8.9 -- <c>SendAfter</c>/<c>SubjectStamp</c> are the HO-NTF-001 amendment. A
    /// reminder whose lead time has already passed at booking time is not enqueued.</summary>
    public static void EnqueueReminders(
        INotificationOutbox outbox, INotificationLinkBuilder linkBuilder, IBusinessSettings settings,
        AppointmentAggregate appointment, MessageContext ctx, DateTimeOffset now, Guid correlationId, UserContext userContext)
    {
        var firstHours = settings.GetInt(SettingKeys.BookingFirstReminderHours, appointment.BranchId);
        var secondHours = settings.GetInt(SettingKeys.BookingSecondReminderHours, appointment.BranchId);

        EnqueueOneReminder(outbox, linkBuilder, appointment, ctx, now, firstHours, correlationId, userContext);
        EnqueueOneReminder(outbox, linkBuilder, appointment, ctx, now, secondHours, correlationId, userContext);
    }

    private static void EnqueueOneReminder(
        INotificationOutbox outbox, INotificationLinkBuilder linkBuilder, AppointmentAggregate appointment,
        MessageContext ctx, DateTimeOffset now, int hoursBefore, Guid correlationId, UserContext userContext)
    {
        if (hoursBefore <= 0) return;

        var sendAfter = appointment.StartsOn!.Value.AddHours(-hoursBefore);
        if (sendAfter <= now) return;

        var variables = BaseVariables(appointment, ctx);
        variables["manageLink"] = linkBuilder.AppointmentLink(appointment.Id);
        variables["hoursBefore"] = hoursBefore.ToString(CultureInfo.InvariantCulture);

        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.AppointmentReminder, NotificationRecipientKind.Customer, appointment.CustomerId,
            variables, SubjectKind: NotificationSubjectKind.AppointmentReminder, SubjectId: appointment.Id,
            BranchId: appointment.BranchId, NotAfter: appointment.StartsOn, CorrelationId: correlationId, UserContext: userContext,
            SendAfter: sendAfter, SubjectStamp: appointment.ScheduleStamp));
    }

    public static void EnqueueRescheduled(
        INotificationOutbox outbox, INotificationLinkBuilder linkBuilder, AppointmentAggregate appointment, MessageContext ctx,
        DateTimeOffset previousStartsOn, Guid correlationId, UserContext userContext)
    {
        var variables = BaseVariables(appointment, ctx);
        variables["manageLink"] = linkBuilder.AppointmentLink(appointment.Id);
        variables["previousStartsOnLocal"] = FormatLocal(previousStartsOn, ctx.TimeZoneId);

        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.AppointmentRescheduled, NotificationRecipientKind.Customer, appointment.CustomerId,
            variables, BranchId: appointment.BranchId, CorrelationId: correlationId, UserContext: userContext));
    }

    public static void EnqueueCancelled(
        INotificationOutbox outbox, INotificationLinkBuilder linkBuilder, AppointmentAggregate appointment, MessageContext ctx,
        bool cancelledByClinic, Guid correlationId, UserContext userContext)
    {
        var variables = new Dictionary<string, string>
        {
            ["catName"] = ctx.CatName,
            ["serviceName"] = appointment.ServiceSnapshot.Name,
            ["startsOnLocal"] = FormatLocal(appointment.StartsOn ?? appointment.CancelledOn ?? DateTimeOffset.UtcNow, ctx.TimeZoneId),
            ["cancelledByClinic"] = cancelledByClinic ? "yes" : "no",
            ["bookLink"] = linkBuilder.BookingLink(),
        };

        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.AppointmentCancelled, NotificationRecipientKind.Customer, appointment.CustomerId,
            variables, BranchId: appointment.BranchId, CorrelationId: correlationId, UserContext: userContext));
    }

    public static void EnqueueWaitlistOffer(
        INotificationOutbox outbox, INotificationLinkBuilder linkBuilder, WaitlistEntryAggregate entry, AppointmentAggregate heldAppointment,
        MessageContext ctx, Guid correlationId, UserContext userContext)
    {
        var variables = new Dictionary<string, string>
        {
            ["catName"] = ctx.CatName,
            ["serviceName"] = heldAppointment.ServiceSnapshot.Name,
            ["startsOnLocal"] = FormatLocal(heldAppointment.StartsOn!.Value, ctx.TimeZoneId),
            ["claimLink"] = linkBuilder.WaitlistClaimLink(heldAppointment.Id),
            ["claimByLocal"] = FormatLocal(heldAppointment.HoldExpiresOn!.Value, ctx.TimeZoneId),
        };

        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.WaitlistSlotOffered, NotificationRecipientKind.Customer, entry.CustomerId,
            variables, BranchId: entry.BranchId, CorrelationId: correlationId, UserContext: userContext));
    }

    private static Dictionary<string, string> BaseVariables(AppointmentAggregate appointment, MessageContext ctx) => new()
    {
        ["catName"] = ctx.CatName,
        ["serviceName"] = appointment.ServiceSnapshot.Name,
        ["startsOnLocal"] = FormatLocal(appointment.StartsOn!.Value, ctx.TimeZoneId),
        ["performerName"] = ctx.PerformerName,
        ["branchName"] = ctx.BranchName,
        ["branchAddress"] = ctx.BranchAddress,
    };

    /// <summary>Loads the strings <see cref="MessageContext"/> needs -- one place, used by every
    /// handler in §11.4 that enqueues a message.</summary>
    public static async Task<MessageContext> LoadContextAsync(
        IAggregateRootRepository<BranchAggregate> branchRepository,
        IAggregateRootRepository<CatAggregate> catRepository,
        IAggregateRootRepository<StaffAccountAggregate> staffRepository,
        AppointmentAggregate appointment)
    {
        var branch = await branchRepository.GetByIdAsync(appointment.BranchId);
        var cat = await catRepository.GetByIdAsync(appointment.CatId);

        var performerName = "our team";
        if (appointment.PerformerId is { } performerId)
        {
            var performer = await staffRepository.GetByIdAsync(performerId);
            if (performer is not null) performerName = performer.FullName;
        }

        return new MessageContext(cat?.Name ?? "your cat", branch?.Name ?? "the clinic", branch?.AddressLine ?? "", performerName, branch?.TimeZoneId ?? "UTC");
    }
}
