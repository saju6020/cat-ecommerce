using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Application.CommandHandlers.Prescription;

/// <summary>
/// HO-PHM-001 §8.11 (ADR-150) -- builds and enqueues the five pharmacy messages. Called from a <b>command handler, before its save</b>, through
/// <see cref="INotificationOutbox"/> (HO-NTF-001 ADR-056/057) -- never from a domain event handler. Variables are the cat's name, the medicine's or food's
/// name, dates and links; no dose, no diagnosis, no batch number ever travels. A decline reason is the vet's plain text, inserted by the renderer as a
/// literal text node (HO-NTF-001 ADR-062).
/// </summary>
public static class PharmacyNotifications
{
    public static void EnqueueRepeatDeclined(
        INotificationOutbox outbox, INotificationLinkBuilder links, Guid customerId, Guid catId, string catName, string medicineName, string reason,
        Guid correlationId, UserContext userContext, Guid? branchId = null)
    {
        var variables = new Dictionary<string, string>
        {
            ["catName"] = catName,
            ["medicineName"] = medicineName,
            ["declineReason"] = reason,
            ["bookLink"] = links.CatCareLink(catId, "FollowUp"),
        };

        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.PharmacyRepeatDeclined, NotificationRecipientKind.Customer, customerId, variables,
            BranchId: branchId, CorrelationId: correlationId, UserContext: userContext));
    }

    public static void EnqueueRepeatReady(
        INotificationOutbox outbox, INotificationLinkBuilder links, Guid customerId, Guid prescriptionId, string catName, string medicineName, string branchName,
        Guid branchId, Guid correlationId, UserContext userContext)
    {
        var variables = new Dictionary<string, string>
        {
            ["catName"] = catName,
            ["medicineName"] = medicineName,
            ["branchName"] = branchName,
            ["prescriptionLink"] = links.PrescriptionLink(prescriptionId),
        };

        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.PharmacyRepeatReady, NotificationRecipientKind.Customer, customerId, variables,
            BranchId: branchId, CorrelationId: correlationId, UserContext: userContext));
    }

    public static void EnqueueDietAuthorised(
        INotificationOutbox outbox, INotificationLinkBuilder links, Guid customerId, string catName, string productName, DateOnly validTo,
        Guid correlationId, UserContext userContext)
    {
        var variables = new Dictionary<string, string>
        {
            ["catName"] = catName,
            ["productName"] = productName,
            ["validTo"] = validTo.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            ["authorisationsLink"] = links.DietAuthorisationsLink(),
        };

        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.PharmacyDietAuthorised, NotificationRecipientKind.Customer, customerId, variables,
            CorrelationId: correlationId, UserContext: userContext));
    }

    public static void EnqueueDietAuthorisationExpired(
        INotificationOutbox outbox, INotificationLinkBuilder links, Guid customerId, Guid catId, string catName, string productName, DateOnly expiredOn,
        Guid authorisationId, Guid correlationId, UserContext userContext)
    {
        var variables = new Dictionary<string, string>
        {
            ["catName"] = catName,
            ["productName"] = productName,
            ["expiredOn"] = expiredOn.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            ["bookLink"] = links.CatCareLink(catId, "FollowUp"),
        };

        // Deduplicated per authorisation: the sweep may meet it twice, but the owner is told once.
        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.PharmacyDietAuthorisationExpired, NotificationRecipientKind.Customer, customerId, variables,
            DeduplicationKey: $"pharmacy:diet-expired:{authorisationId:N}", CorrelationId: correlationId, UserContext: userContext));
    }
}
