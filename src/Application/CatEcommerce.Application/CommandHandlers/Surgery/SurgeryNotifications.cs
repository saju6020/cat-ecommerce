using System.Globalization;
using CatEcommerce.Application.CommandHandlers.Clinic;
using CatEcommerce.Domain.Aggregates.Surgery;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Surgery;

/// <summary>
/// HO-SRG-001 §8.10 (ADR-169) -- builds and enqueues the six surgery messages. Called from a command handler, BEFORE its aggregate save, through
/// <see cref="INotificationOutbox"/> (HO-NTF-001 ADR-056/057) -- never from a domain event handler. Formatting follows HO-APT-001 §8.9: times in the branch zone as
/// <c>ddd d MMM yyyy, HH:mm</c>. SMS bodies carry the link, never the instructions.
/// </summary>
public static class SurgeryNotifications
{
    public sealed record MessageContext(string CatName, string BranchName, string BranchAddress, string TimeZoneId);

    public static string FormatLocal(DateTimeOffset instant, string timeZoneId) => ClinicNotifications.FormatLocal(instant, timeZoneId);

    private static Dictionary<string, string> BookingVariables(SurgeryAggregate surgery, MessageContext ctx, INotificationLinkBuilder links) => new()
    {
        ["catName"] = ctx.CatName,
        ["procedureName"] = surgery.Procedure.ServiceName,
        ["startsOnLocal"] = FormatLocal(surgery.StartsOn, ctx.TimeZoneId),
        ["admitByLocal"] = FormatLocal(surgery.AdmitBy, ctx.TimeZoneId),
        ["branchName"] = ctx.BranchName,
        ["branchAddress"] = ctx.BranchAddress,
        ["fastingInstructions"] = surgery.FastingInstructions,
        ["detailsLink"] = links.SurgeryLink(surgery.Id),
    };

    public static void EnqueueBooked(INotificationOutbox outbox, INotificationLinkBuilder links, SurgeryAggregate surgery, MessageContext ctx, Guid correlationId, UserContext user) =>
        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.SurgeryBooked, NotificationRecipientKind.Customer, surgery.CustomerId,
            BookingVariables(surgery, ctx, links), BranchId: surgery.BranchId, CorrelationId: correlationId, UserContext: user));

    /// <summary>Not enqueued when the reminder is off (0 hours) or its send time has already passed -- a booking made the evening before gets the "booked" message only.</summary>
    public static void EnqueuePreOpReminder(
        INotificationOutbox outbox, INotificationLinkBuilder links, IBusinessSettings settings, SurgeryAggregate surgery, MessageContext ctx,
        DateTimeOffset now, Guid correlationId, UserContext user)
    {
        var hours = settings.GetInt(SettingKeys.SurgeryPreOpReminderHours, surgery.BranchId);
        if (hours <= 0) return;

        var sendAfter = surgery.StartsOn.AddHours(-hours);
        if (sendAfter <= now) return;

        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.SurgeryPreOpReminder, NotificationRecipientKind.Customer, surgery.CustomerId,
            BookingVariables(surgery, ctx, links), SubjectKind: NotificationSubjectKind.SurgeryPreOp, SubjectId: surgery.Id,
            BranchId: surgery.BranchId, NotAfter: surgery.StartsOn, CorrelationId: correlationId, UserContext: user,
            SendAfter: sendAfter, SubjectStamp: surgery.ScheduleStamp));
    }

    public static void EnqueueRescheduled(
        INotificationOutbox outbox, INotificationLinkBuilder links, SurgeryAggregate surgery, MessageContext ctx, DateTimeOffset previousStartsOn, Guid correlationId, UserContext user)
    {
        var variables = BookingVariables(surgery, ctx, links);
        variables["previousStartsOnLocal"] = FormatLocal(previousStartsOn, ctx.TimeZoneId);

        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.SurgeryRescheduled, NotificationRecipientKind.Customer, surgery.CustomerId,
            variables, BranchId: surgery.BranchId, CorrelationId: correlationId, UserContext: user));
    }

    public static void EnqueueCancelled(INotificationOutbox outbox, SurgeryAggregate surgery, MessageContext ctx, Guid correlationId, UserContext user) =>
        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.SurgeryCancelled, NotificationRecipientKind.Customer, surgery.CustomerId,
            new Dictionary<string, string>
            {
                ["catName"] = ctx.CatName,
                ["procedureName"] = surgery.Procedure.ServiceName,
                ["startsOnLocal"] = FormatLocal(surgery.StartsOn, ctx.TimeZoneId),
            },
            BranchId: surgery.BranchId, CorrelationId: correlationId, UserContext: user));

    public static void EnqueuePostOp(
        INotificationOutbox outbox, INotificationLinkBuilder links, SurgeryAggregate surgery, MessageContext ctx, Guid correlationId, UserContext user)
    {
        var summary = surgery.FollowUps.Count == 0
            ? "No follow-up is needed."
            : string.Join("; ", surgery.FollowUps.OrderBy(f => f.DueOn).Select(f => $"{Describe(f.Kind)} {f.DueOn.ToString("d MMM", CultureInfo.InvariantCulture)}")) + ".";

        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.SurgeryPostOpInstructions, NotificationRecipientKind.Customer, surgery.CustomerId,
            new Dictionary<string, string>
            {
                ["catName"] = ctx.CatName,
                ["procedureName"] = surgery.Procedure.ServiceName,
                ["followUpSummary"] = summary,
                ["detailsLink"] = links.SurgeryLink(surgery.Id),
            },
            BranchId: surgery.BranchId, CorrelationId: correlationId, UserContext: user));
    }

    /// <summary>One reminder per follow-up, <c>SendAfter</c> = due date minus <c>surgery.followUpReminderLeadDays</c> at 10:00 branch-local, <c>NotAfter</c> = end of the due date.
    /// Valid while the follow-up is still Pending (the <c>SurgeryFollowUp</c> guard). Not enqueued when the lead days are 0 and the time has passed.</summary>
    public static void EnqueueFollowUpReminder(
        INotificationOutbox outbox, INotificationLinkBuilder links, IBusinessSettings settings, SurgeryAggregate surgery, SurgeryFollowUp followUp, MessageContext ctx,
        DateTimeOffset now, Guid correlationId, UserContext user)
    {
        var lead = settings.GetInt(SettingKeys.SurgeryFollowUpReminderLeadDays, surgery.BranchId);
        var sendAfter = AppointmentSlotFinder.InstantOf(followUp.DueOn.AddDays(-lead), new TimeOnly(SurgeryRules.FollowUpReminderLocalHour, 0), ctx.TimeZoneId);
        if (sendAfter < now) sendAfter = now; // a follow-up due sooner than the lead time is reminded at once rather than never

        var notAfter = AppointmentSlotFinder.InstantOf(followUp.DueOn.AddDays(1), TimeOnly.MinValue, ctx.TimeZoneId);
        if (notAfter <= sendAfter) return;

        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.SurgeryFollowUpDue, NotificationRecipientKind.Customer, surgery.CustomerId,
            new Dictionary<string, string>
            {
                ["catName"] = ctx.CatName,
                ["followUpName"] = Describe(followUp.Kind),
                ["dueOn"] = followUp.DueOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["bookingLink"] = links.FollowUpBookingLink(surgery.CatId),
            },
            SubjectKind: NotificationSubjectKind.SurgeryFollowUp, SubjectId: surgery.Id, SubjectRef: followUp.Id,
            BranchId: surgery.BranchId, NotAfter: notAfter, CorrelationId: correlationId, UserContext: user, SendAfter: sendAfter));
    }

    public static string Describe(SurgeryFollowUpKind kind) =>
        kind == SurgeryFollowUpKind.WoundCheck ? "Wound check" : kind == SurgeryFollowUpKind.SutureRemoval ? "Suture removal" : "Recheck";
}
