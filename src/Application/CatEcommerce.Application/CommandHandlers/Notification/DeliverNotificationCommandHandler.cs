using CatEcommerce.Application.Commands.Notification;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Notification;

/// <summary>
/// HO-NTF-001 §11.4 C5 / ADR-059/ADR-060/ADR-061 -- idempotent: a second run on a terminal or
/// not-yet-due notification returns success and writes nothing. Never calls the provider while a
/// database transaction is open (§20.6): the claim is committed BEFORE <c>SendAsync</c>, and the
/// final <c>RecordAttempt</c> commit happens AFTER it returns.
/// </summary>
public sealed class DeliverNotificationCommandHandler(
    ILogger<DeliverNotificationCommandHandler> logger,
    IAggregateRootRepository<NotificationAggregate> notificationRepository,
    IAggregateRootRepository<NotificationRequestAggregate> requestRepository,
    IAggregateRootRepository<NotificationTemplateAggregate> templateRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    INotificationTemplateCatalogue catalogue,
    INotificationRecipientDirectory recipientDirectory,
    ISubjectGuardResolver subjectGuardResolver,
    IQuietHoursCalendar quietHoursCalendar,
    INotificationLinkBuilder linkBuilder,
    INotificationChannelSenderResolver senderResolver,
    INotificationDeliverySettings deliverySettings,
    INotificationClaimStore claimStore,
    INotificationEnlister enlister,
    INotificationAttachmentResolver attachmentResolver,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<DeliverNotificationCommand>
{
    public async Task<CommandResponse> HandleAsync(DeliverNotificationCommand command)
    {
        var notification = await notificationRepository.GetByIdAsync(command.NotificationId);
        if (notification is null) return Success();

        var now = timeProvider.GetUtcNow();
        if (notification.IsTerminal || !notification.CanClaim(now)) return Success();

        if (notification.NotAfter is not null && notification.NotAfter <= now)
        {
            notification.Expire(now);
            if (notification.ContainsSecret) notification.Redact(NotificationRedactionReason.Secret, now);
            await notificationRepository.UpdateAsync(notification, EventPublishOption.InMemoryPublishOnly);
            return Success();
        }

        var guardValid = await subjectGuardResolver.IsStillValidAsync(
            notification.SubjectKind, notification.RecipientId, notification.SubjectId, notification.SubjectRef, notification.SubjectDueOn, notification.SubjectStamp);
        if (!guardValid)
        {
            notification.Cancel(NotificationCloseReason.SubjectNoLongerApplies, now);
            await notificationRepository.UpdateAsync(notification, EventPublishOption.InMemoryPublishOnly);
            return Success();
        }

        var recipient = await recipientDirectory.ResolveAsync(notification.RecipientKind, notification.RecipientId);
        if (recipient is null || !recipient.IsActive)
        {
            notification.Cancel(NotificationCloseReason.RecipientInactive, now);
            await notificationRepository.UpdateAsync(notification, EventPublishOption.InMemoryPublishOnly);
            return Success();
        }

        if (notification.Category == NotificationCategory.Marketing && !recipient.MarketingGranted(notification.Channel))
        {
            notification.Cancel(NotificationCloseReason.RecipientOptedOut, now);
            await notificationRepository.UpdateAsync(notification, EventPublishOption.InMemoryPublishOnly);
            return Success();
        }

        if (notification.MustPark(now))
        {
            // Checked BEFORE quiet hours: Defer() rejects a Sending row, so a stuck row would throw there.
            // Claimed MaxAttempts times without a recorded outcome (a save kept failing after the
            // claim): stop re-sending it every lease. NTF-001 backlog row 5.
            logger.LogError("Notification {NotificationId} parked after {ClaimCount} claims without a recorded outcome.", notification.Id, notification.ClaimCount);
            notification.Park(now);
            await FinishUndeliverableOrSentAsync(notification, recipient, now);
            return Success();
        }

        if (notification.Urgency == NotificationUrgency.Normal)
        {
            var deferUntil = await quietHoursCalendar.NextAllowedInstantAsync(now, notification.BranchId);
            if (deferUntil is not null)
            {
                // §8.14: a hold that would pass NotAfter expires the notification now, instead of
                // holding it to the end of the window and expiring it on a later pass (NTF-001 #16).
                if (notification.NotAfter is not null && notification.NotAfter <= deferUntil.Value)
                {
                    notification.Expire(now);
                    if (notification.ContainsSecret) notification.Redact(NotificationRedactionReason.Secret, now);
                    await notificationRepository.UpdateAsync(notification, EventPublishOption.InMemoryPublishOnly);
                    return Success();
                }

                notification.Defer(deferUntil.Value);
                await notificationRepository.UpdateAsync(notification, EventPublishOption.InMemoryPublishOnly);
                return Success();
            }
        }

        // HO-RPT-001 section 11.7 item 3 (HO-NTF-001 amendment): an attachment is loaded from its owner immediately before sending, for the Email channel only. A file that no longer
        // exists (a purged report) cancels the notification -- it is never sent without the file it promised.
        IReadOnlyList<OutgoingAttachment> attachments = [];
        if (notification.Channel == NotificationChannel.Email && notification.AttachmentKind is { } attachmentKind && notification.AttachmentId is { } attachmentId)
        {
            var attachment = await attachmentResolver.LoadAsync(attachmentKind, attachmentId);
            if (attachment is null)
            {
                notification.Cancel(NotificationCloseReason.SubjectNoLongerApplies, now);
                await notificationRepository.UpdateAsync(notification, EventPublishOption.InMemoryPublishOnly);
                return Success();
            }

            attachments = [new OutgoingAttachment(attachment.FileName, attachment.ContentType, attachment.Content)];
        }

        notification.Claim(now, TimeSpan.FromSeconds(deliverySettings.LeaseSeconds));
        var claimed = await claimStore.TrySaveClaimAsync(notification);
        if (!claimed) return Success(); // another worker has it

        var sender = senderResolver.Resolve(notification.Channel);
        SendOutcome outcome;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(deliverySettings.SendTimeoutSeconds));
            var headers = BuildHeaders(notification);
            var message = new OutgoingMessage(notification.Id, notification.Destination ?? "", notification.Subject, notification.Body, notification.HtmlBody, headers, attachments);
            outcome = await sender.SendAsync(message, cts.Token);
        }
        catch (Exception ex)
        {
            // §8.9 rule 5 / §20.4: never the exception's message (it may echo the destination) --
            // only the type name.
            outcome = new SendOutcome(SendOutcomeKind.TransientFailure, null, ex.GetType().Name);
        }

        var sentAt = timeProvider.GetUtcNow();
        notification.RecordAttempt(outcome.Kind, sentAt, sender.ProviderName, outcome.ProviderMessageId, outcome.ProviderResponse);

        await FinishUndeliverableOrSentAsync(notification, recipient, sentAt);

        return Success();
    }

    private async Task FinishUndeliverableOrSentAsync(NotificationAggregate notification, RecipientProfile recipient, DateTimeOffset sentAt)
    {
        NotificationAggregate? fallback = null;
        if (notification.Status == NotificationStatus.Sent && notification.ContainsSecret)
        {
            notification.Redact(NotificationRedactionReason.Secret, sentAt);
        }
        else if (notification.Status == NotificationStatus.Undeliverable)
        {
            fallback = await TryBuildFallbackAsync(notification, recipient, sentAt);
            if (fallback is not null)
            {
                enlister.Enlist(fallback);
            }
            else if (notification.ContainsSecret)
            {
                notification.Redact(NotificationRedactionReason.Secret, sentAt);
            }
        }

        await notificationRepository.UpdateAsync(notification, EventPublishOption.InMemoryPublishOnly); // one commit (ADR-056)

    }

    private static IReadOnlyDictionary<string, string> BuildHeaders(NotificationAggregate notification)
    {
        if (notification.Channel != NotificationChannel.Email || string.IsNullOrEmpty(notification.UnsubscribeLink))
            return new Dictionary<string, string>();

        return new Dictionary<string, string>
        {
            ["List-Unsubscribe"] = $"<{notification.UnsubscribeLink}>",
            ["List-Unsubscribe-Post"] = "List-Unsubscribe=One-Click"
        };
    }

    /// <summary>§8.6 -- "a channel -> render from the request's variables (if redacted -> no
    /// fallback), enlist the fallback (FallbackFromId)".</summary>
    private async Task<NotificationAggregate?> TryBuildFallbackAsync(NotificationAggregate original, RecipientProfile recipient, DateTimeOffset now)
    {
        var request = await requestRepository.GetByIdAsync(original.RequestId);
        if (request is null || request.VariablesRedactedOn is not null) return null;

        var definition = catalogue.Find(original.TemplateKey);
        if (definition is null) return null;

        var tried = await TriedChannelsForChainAsync(original);
        var fallbackChannel = NotificationRoutingPolicy.NextFallback(definition, recipient, tried);
        if (fallbackChannel is null) return null;

        var customisedTemplate = await templateRepository.GetByFilterAsync(t => t.Key == original.TemplateKey);
        var (content, version) = EffectiveNotificationTemplate.ChannelFor(definition, customisedTemplate, fallbackChannel);
        if (content is null) return null;

        var branch = await branchRepository.GetByIdAsync(request.BranchId ?? AdministrationSeed.MainBranchId);
        var values = new Dictionary<string, string>(request.Variables, StringComparer.Ordinal)
        {
            [StandardTemplateVariables.RecipientName] = recipient.FullName,
            [StandardTemplateVariables.ClinicName] = branch?.Name ?? "PurrCare",
            [StandardTemplateVariables.ClinicPhone] = branch?.PhoneNumber ?? ""
        };

        var isMarketing = definition.Category == NotificationCategory.Marketing;
        string? unsubscribeTokenHash = null;
        string? unsubscribeLink = null;
        if (isMarketing)
        {
            var (plainToken, hash) = ExpandNotificationRequestCommandHandler.IssueUnsubscribeToken();
            unsubscribeLink = linkBuilder.UnsubscribeLink(plainToken);
            values[StandardTemplateVariables.UnsubscribeLink] = unsubscribeLink;
            unsubscribeTokenHash = hash;
        }

        RenderedMessage rendered;
        try
        {
            rendered = RenderedMessageColumnLimits.Enforce(NotificationRenderer.Render(content, definition.Variables, values, fallbackChannel));
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException or ArgumentOutOfRangeException)
        {
            // Gate fix 2026-09-27 (SECURITY/CODE_REVIEW MEDIUM 7): a poison template/value can throw
            // ArgumentOutOfRangeException or FormatException here too, not only InvalidOperationException
            // -- narrowly catching only the latter left the caller's claim committed but its final
            // save (which would have recorded Undeliverable) never reached, so the row stayed
            // Sending and was re-claimed and re-sent every lease, unbounded.
            //
            // Gate fix 2026-09-27 (SECURITY/CODE_REVIEW second pass, MEDIUM/HIGH): narrowed from
            // `Exception ex when (ex is not OperationCanceledException)` to this closed, render-only
            // set -- matching ExpandNotificationRequestCommandHandler's own second-pass fix -- so a
            // database exception is never accidentally caught here either, even though nothing in
            // THIS try block currently makes a database call (this is defence in depth against a
            // future change adding one). RenderedMessageColumnLimits.Enforce also now throws
            // NotificationRenderOverflowException (an InvalidOperationException) for an overlong
            // Body/LinkPath, so a fallback that would have overflowed its column ends this
            // notification Undeliverable/redacted here, exactly like any other unrenderable
            // fallback, instead of overflowing the save below and leaving the row stuck Sending.
            logger.LogError(ex, "Fallback rendering failed for notification {NotificationId}.", original.Id);
            return null;
        }

        var destination = recipient.DestinationFor(fallbackChannel);
        if (destination is null) return null;

        var fallback = new NotificationAggregate(Guid.NewGuid());
        fallback.CreateExternal(new NotificationCreateDto(
            original.RequestId, original.TemplateKey, version, original.Locale, original.Category, original.Urgency, original.ContainsSecret,
            original.RecipientKind, original.RecipientId, original.SubjectKind, original.SubjectId, original.SubjectRef, original.SubjectDueOn,
            original.BranchId, fallbackChannel, destination, rendered.Subject, rendered.Title, rendered.TextBody, rendered.HtmlBody, rendered.LinkPath,
            original.NotAfter, now, null, false, unsubscribeTokenHash, unsubscribeLink, FallbackFromId: original.Id, ResendOfId: null, ResentBy: null,
            SubjectStamp: original.SubjectStamp, AttachmentKind: original.AttachmentKind, AttachmentId: original.AttachmentId));

        return fallback;
    }

    /// <summary>
    /// Gate fix 2026-09-27 (SECURITY HIGH 1 / CODE_REVIEW BLOCKER 1, ADR-058 R8). Before this fix,
    /// <c>tried</c> was seeded with only the CURRENTLY failing notification's own channel, not every
    /// channel already attempted for this request -- so a recipient whose SMS falls back to Email,
    /// and whose Email then also fails, saw <c>NextFallback</c> hand back SMS again (its own
    /// <c>tried</c> set only ever contained {Email}), looping forever, one new row per poll. Walks
    /// the <c>FallbackFromId</c> chain back to the root, collecting every channel already tried,
    /// bounded by the number of deliverable channels as a defensive guard against a corrupt cycle.
    /// </summary>
    private async Task<HashSet<NotificationChannel>> TriedChannelsForChainAsync(NotificationAggregate current)
    {
        var tried = new HashSet<NotificationChannel> { current.Channel };
        var hops = 0;

        while (current.FallbackFromId is { } predecessorId && hops < NotificationChannelRules.DeliverableChannels.Count)
        {
            var predecessor = await notificationRepository.GetByIdAsync(predecessorId);
            if (predecessor is null) break;

            tried.Add(predecessor.Channel);
            current = predecessor;
            hops++;
        }

        return tried;
    }

    private static CommandResponse Success() => new() { IsSuccess = true };
}
