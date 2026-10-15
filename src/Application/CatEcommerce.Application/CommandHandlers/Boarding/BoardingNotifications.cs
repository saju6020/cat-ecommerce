using System.Globalization;
using CatEcommerce.Domain.Aggregates.Boarding;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Application.CommandHandlers.Boarding;

/// <summary>
/// HO-BRD-001 section 8.19 (ADR-226, renumbered ADR-359) -- builds and enqueues the boarding messages. Called from a COMMAND HANDLER, before its aggregate
/// save, through <see cref="INotificationOutbox"/> (HO-NTF-001 ADR-056/057) -- never from a domain event handler. Variables are ids, names, dates, amounts and
/// links: no description, note or health text is ever sent (the link opens it behind sign-in).
/// </summary>
public static class BoardingNotifications
{
    public static string FormatMoney(decimal amount, string currencyCode) =>
        $"{currencyCode} {amount.ToString("N2", CultureInfo.InvariantCulture)}";

    public static string FormatLocal(DateTimeOffset instant, string timeZoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return TimeZoneInfo.ConvertTime(instant, zone).ToString("ddd d MMM yyyy, HH:mm", CultureInfo.InvariantCulture);
    }

    public static string CatNames(BoardingStayAggregate stay)
    {
        var names = stay.Cats.Select(c => c.CatName).ToList();
        return names.Count <= 1 ? string.Join("", names) : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1];
    }

    private static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static void EnqueueReserved(
        INotificationOutbox outbox, INotificationLinkBuilder links, BoardingStayAggregate stay, string timeZoneId, Guid correlationId, UserContext userContext)
    {
        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.BoardingReserved, NotificationRecipientKind.Customer, stay.CustomerId,
            new Dictionary<string, string>
            {
                ["catNames"] = CatNames(stay),
                ["arrivalOn"] = Date(stay.ArrivalOn),
                ["departureOn"] = Date(stay.DepartureOn),
                ["depositAmount"] = FormatMoney(stay.DepositRequired, stay.CurrencyCode),
                ["payByLocal"] = stay.DepositDueBy is { } due ? FormatLocal(due, timeZoneId) : "",
                ["paymentInstructionsLink"] = links.BoardingPaymentInstructionsLink(),
                ["stayLink"] = links.BoardingStayLink(stay.Id),
            },
            BranchId: stay.BranchId, CorrelationId: correlationId, UserContext: userContext));
    }

    public static void EnqueueConfirmed(
        INotificationOutbox outbox, INotificationLinkBuilder links, BoardingStayAggregate stay, decimal receivedDeposit, Guid correlationId, UserContext userContext)
    {
        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.BoardingConfirmed, NotificationRecipientKind.Customer, stay.CustomerId,
            new Dictionary<string, string>
            {
                ["catNames"] = CatNames(stay),
                ["arrivalOn"] = Date(stay.ArrivalOn),
                ["departureOn"] = Date(stay.DepartureOn),
                ["balanceDue"] = FormatMoney(Math.Max(0m, stay.QuoteTotal - receivedDeposit), stay.CurrencyCode),
                ["stayLink"] = links.BoardingStayLink(stay.Id),
            },
            BranchId: stay.BranchId, CorrelationId: correlationId, UserContext: userContext));
    }

    public static void EnqueueLapsed(INotificationOutbox outbox, INotificationLinkBuilder links, BoardingStayAggregate stay, Guid correlationId, UserContext userContext)
    {
        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.BoardingReservationLapsed, NotificationRecipientKind.Customer, stay.CustomerId,
            new Dictionary<string, string>
            {
                ["catNames"] = CatNames(stay),
                ["arrivalOn"] = Date(stay.ArrivalOn),
                ["bookLink"] = links.BoardingBookLink(stay.ArrivalOn, stay.DepartureOn),
            },
            BranchId: stay.BranchId, CorrelationId: correlationId, UserContext: userContext));
    }

    /// <summary>The wording of a decision already made (as HO-APT-001's cancelledByClinic): kept under policy, refunded, or nothing was paid.</summary>
    public static void EnqueueCancelled(
        INotificationOutbox outbox, INotificationLinkBuilder links, BoardingStayAggregate stay, bool depositWasReceived, Guid correlationId, UserContext userContext)
    {
        var outcome = !depositWasReceived ? "was not charged" : stay.DepositForfeited ? "is kept under our cancellation policy" : "will be refunded";
        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.BoardingCancelled, NotificationRecipientKind.Customer, stay.CustomerId,
            new Dictionary<string, string>
            {
                ["catNames"] = CatNames(stay),
                ["arrivalOn"] = Date(stay.ArrivalOn),
                ["depositOutcome"] = outcome,
                ["stayLink"] = links.BoardingStayLink(stay.Id),
            },
            BranchId: stay.BranchId, CorrelationId: correlationId, UserContext: userContext));
    }

    public static void EnqueueCheckedIn(INotificationOutbox outbox, INotificationLinkBuilder links, BoardingStayAggregate stay, Guid correlationId, UserContext userContext)
    {
        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.BoardingCheckedIn, NotificationRecipientKind.Customer, stay.CustomerId,
            new Dictionary<string, string>
            {
                ["catNames"] = CatNames(stay),
                ["departureOn"] = Date(stay.DepartureOn),
                ["diaryLink"] = links.BoardingDiaryLink(stay.Id),
            },
            BranchId: stay.BranchId, CorrelationId: correlationId, UserContext: userContext));
    }

    /// <summary>Medium -> Normal template, high -> Urgent template (two definitions, F33). The kind and the local time go in the message; the description never does.</summary>
    public static void EnqueueIncident(
        INotificationOutbox outbox, INotificationLinkBuilder links, BoardingStayAggregate stay, string catName, BoardingIncidentAggregate incident, string timeZoneId,
        Guid correlationId, UserContext userContext)
    {
        var template = incident.Severity == IncidentSeverity.High ? NotificationTemplateCatalogue.BoardingIncidentHigh : NotificationTemplateCatalogue.BoardingIncidentMedium;
        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), template, NotificationRecipientKind.Customer, stay.CustomerId,
            new Dictionary<string, string>
            {
                ["catName"] = catName,
                ["incidentKind"] = incident.Kind.DisplayName,
                ["occurredOnLocal"] = FormatLocal(incident.OccurredOn, timeZoneId),
                ["incidentLink"] = links.BoardingIncidentLink(stay.Id, incident.Id),
            },
            BranchId: stay.BranchId, CorrelationId: correlationId, UserContext: userContext));
    }

    public static void EnqueueVetApprovalRequested(
        INotificationOutbox outbox, INotificationLinkBuilder links, BoardingStayAggregate stay, string catName, Guid approvalId, decimal amount, Guid correlationId, UserContext userContext)
    {
        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.BoardingVetApprovalRequested, NotificationRecipientKind.Customer, stay.CustomerId,
            new Dictionary<string, string>
            {
                ["catName"] = catName,
                ["amount"] = FormatMoney(amount, stay.CurrencyCode),
                ["approvalLink"] = links.BoardingApprovalLink(stay.Id, approvalId),
            },
            BranchId: stay.BranchId, CorrelationId: correlationId, UserContext: userContext));
    }

    public static void EnqueueCheckedOut(INotificationOutbox outbox, INotificationLinkBuilder links, BoardingStayAggregate stay, Guid correlationId, UserContext userContext)
    {
        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.BoardingCheckedOut, NotificationRecipientKind.Customer, stay.CustomerId,
            new Dictionary<string, string>
            {
                ["catNames"] = CatNames(stay),
                ["summaryLink"] = links.BoardingSummaryLink(stay.Id),
            },
            BranchId: stay.BranchId, CorrelationId: correlationId, UserContext: userContext));
    }

    public static void EnqueueWaitlistAvailable(INotificationOutbox outbox, INotificationLinkBuilder links, BoardingWaitlistEntryAggregate entry, Guid correlationId, UserContext userContext)
    {
        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.BoardingWaitlistAvailable, NotificationRecipientKind.Customer, entry.CustomerId,
            new Dictionary<string, string>
            {
                ["arrivalOn"] = Date(entry.ArrivalOn),
                ["departureOn"] = Date(entry.DepartureOn),
                ["bookLink"] = links.BoardingBookLink(entry.ArrivalOn, entry.DepartureOn),
            },
            BranchId: entry.BranchId, CorrelationId: correlationId, UserContext: userContext));
    }
}
