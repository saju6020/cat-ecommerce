using System.Text.Json;
using CatEcommerce.Application.Commands.Notification;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Notification;

/// <summary>HO-NTF-001 §11.4 C7 / US-NTF-06 AC2. Uses the same <c>NotificationResendEvaluation</c>
/// the read side's <c>cannotResendReason</c> uses, so the two cannot disagree.</summary>
public sealed class ResendNotificationCommandHandler(
    ILogger<ResendNotificationCommandHandler> logger,
    IAggregateRootRepository<NotificationAggregate> notificationRepository,
    IAggregateRootRepository<NotificationRequestAggregate> requestRepository,
    IAggregateRootRepository<NotificationTemplateAggregate> templateRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    INotificationTemplateCatalogue catalogue,
    INotificationRecipientDirectory recipientDirectory,
    IQuietHoursCalendar quietHoursCalendar,
    INotificationResendCounter resendCounter,
    INotificationResendStore resendStore,
    INotificationEnlister enlister,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<ResendNotificationCommand>
{
    public async Task<CommandResponse> HandleAsync(ResendNotificationCommand command)
    {
        var original = await notificationRepository.GetByIdAsync(command.NotificationId);
        if (original is null)
            return Violation(BusinessViolationCodes.NotificationNotFound, nameof(command.NotificationId), "Notification not found.");

        var targetChannel = string.IsNullOrEmpty(command.Channel)
            ? original.Channel
            : Enumeration.FromDisplayName<NotificationChannel>(command.Channel);

        var recipient = await recipientDirectory.ResolveAsync(original.RecipientKind, original.RecipientId);
        var isMarketing = original.Category == NotificationCategory.Marketing;
        var marketingGranted = recipient is not null && recipient.MarketingGranted(targetChannel);
        var targetDestination = recipient?.DestinationFor(targetChannel);

        var chainRootId = original.ResendOfId ?? original.Id;
        var priorResends = await resendCounter.CountResendsAsync(chainRootId);

        var evaluation = NotificationResendEvaluation.Evaluate(
            original.ContainsSecret, original.Channel, original.Status, recipient?.IsActive ?? false,
            isMarketing, marketingGranted, priorResends,
            NotificationChannelRules.IsDeliverable(targetChannel), targetDestination is not null);

        if (!evaluation.CanResend)
            return Violation(evaluation.ErrorCode!, nameof(command.NotificationId), evaluation.Reason ?? "This notification cannot be resent.");

        var now = timeProvider.GetUtcNow();

        // Gate fix 2026-09-27 (ARCHITECTURE/CODE_REVIEW MEDIUM, §11.4 C7: "re-rendered only when
        // the channel differs"). Same channel: the already-rendered fields are known good and are
        // reused as-is. A DIFFERENT channel is re-rendered from the original request's own
        // variables -- reusing the original channel's rendered fields verbatim previously meant an
        // SMS resent as Email had no Subject/HtmlBody, and an Email resent as SMS carried the
        // email's markup-derived text. `original.ContainsSecret` notifications can never reach here
        // (NotificationResendEvaluation refuses them above), so the request's own variables are
        // never the "[redacted]" placeholder text C6 would have written.
        var rendered = targetChannel == original.Channel
            ? new RenderedMessage(original.Subject, original.Title, original.Body, original.HtmlBody, original.LinkPath)
            : await RenderForChannelAsync(original, targetChannel, recipient, now)
              ?? new RenderedMessage(original.Subject, original.Title, original.Body, original.HtmlBody, original.LinkPath);

        DateTimeOffset? notBefore = null;
        var deferred = false;
        if (original.Urgency == NotificationUrgency.Normal)
        {
            var deferUntil = await quietHoursCalendar.NextAllowedInstantAsync(now, original.BranchId);
            if (deferUntil is not null) { notBefore = deferUntil; deferred = true; }
        }

        // Gate fix 2026-09-27 (SECURITY LOW): the resend limit used to be enforced only by
        // counting-then-inserting here, which N concurrent resends could all pass before any of
        // them committed. ResendOrdinal (this chain's 1-based position) backs a unique filtered
        // index, so only ONE resend can ever win a given ordinal -- serialised at the database,
        // not in this handler.
        var ordinal = priorResends + 1;

        var newNotification = new NotificationAggregate(command.NewNotificationId);
        newNotification.CreateExternal(new NotificationCreateDto(
            original.RequestId, original.TemplateKey, original.TemplateVersion, original.Locale, original.Category, original.Urgency, original.ContainsSecret,
            original.RecipientKind, original.RecipientId, original.SubjectKind, original.SubjectId, original.SubjectRef, original.SubjectDueOn,
            original.BranchId, targetChannel, targetDestination, rendered.Subject, rendered.Title, rendered.TextBody, rendered.HtmlBody, rendered.LinkPath,
            original.NotAfter, now, notBefore, deferred, null, null, FallbackFromId: null, ResendOfId: chainRootId, ResentBy: command.UserContext.UserId,
            ResendOrdinal: ordinal));

        var auditEntry = new AuditEntryAggregate(Guid.NewGuid());
        auditEntry.Record(new AuditEntryDto(
            EntityType: nameof(NotificationAggregate), EntityId: original.Id, Action: "NotificationResent", Reason: "NotificationResent",
            ActorUserId: command.UserContext.UserId, OccurredOn: now, BeforeValue: null,
            AfterValue: JsonSerializer.Serialize(new { NewNotificationId = newNotification.Id, Channel = targetChannel.DisplayName }),
            UserContext: command.UserContext, CorrelationId: command.CorrelationId, Category: AuditCategory.BusinessEvent, IpAddress: null));

        enlister.Enlist(auditEntry);
        var saved = await resendStore.TrySaveAsync(newNotification); // one commit
        if (!saved)
            return Violation(BusinessViolationCodes.ResendLimitReached, nameof(command.NotificationId), "This notification has reached its resend limit.");

        return new CommandResponse
        {
            IsSuccess = true,
            Result = new ResendNotificationResult(newNotification.Id, newNotification.Status.DisplayName, newNotification.NotBefore)
        };
    }

    /// <summary>Re-renders from the original request's own variables for the NEW channel, the same
    /// way <c>ExpandNotificationRequestCommandHandler</c>/the fallback path in
    /// <c>DeliverNotificationCommandHandler</c> do. Returns <c>null</c> (falls back to reusing the
    /// original channel's rendered fields) if the definition, the request or that channel's content
    /// cannot be found, or if rendering itself fails -- a degraded resend is still a resend; the
    /// staff member asked for the message to go out.</summary>
    private async Task<RenderedMessage?> RenderForChannelAsync(NotificationAggregate original, NotificationChannel channel, RecipientProfile? recipient, DateTimeOffset now)
    {
        if (recipient is null) return null;

        var definition = catalogue.Find(original.TemplateKey);
        if (definition is null) return null;

        var request = await requestRepository.GetByIdAsync(original.RequestId);
        if (request is null) return null;

        var customisedTemplate = await templateRepository.GetByFilterAsync(t => t.Key == original.TemplateKey);
        var (content, _) = EffectiveNotificationTemplate.ChannelFor(definition, customisedTemplate, channel);
        if (content is null) return null;

        var branch = await branchRepository.GetByIdAsync(request.BranchId ?? AdministrationSeed.MainBranchId);
        var values = new Dictionary<string, string>(request.Variables, StringComparer.Ordinal)
        {
            [StandardTemplateVariables.RecipientName] = recipient.FullName,
            [StandardTemplateVariables.ClinicName] = branch?.Name ?? "PurrCare",
            [StandardTemplateVariables.ClinicPhone] = branch?.PhoneNumber ?? ""
        };

        try
        {
            return RenderedMessageColumnLimits.Enforce(NotificationRenderer.Render(content, definition.Variables, values, channel));
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException or ArgumentOutOfRangeException)
        {
            // Gate fix 2026-09-27 (ARCHITECTURE second pass, LOW): this used to fall back silently.
            // A silent fallback to the ORIGINAL channel's rendered fields is exactly the C7 defect
            // this re-render path exists to fix (an SMS resent as Email with no Subject/HtmlBody),
            // so a render failure here must be visible to operators, not hidden. Never logs the
            // exception's own text beyond its type/message (the renderer's own messages carry only a
            // variable NAME, never a value) and never the rendered content itself.
            //
            // Gate fix 2026-09-27 (SECURITY second pass, MEDIUM): a cross-channel resend whose
            // re-rendered content overflowed its column used to reach TrySaveAsync and return 500
            // (EfNotificationResendStore only translates the unique-violation exception). Enforce
            // now throws NotificationRenderOverflowException (an InvalidOperationException, already
            // in this closed set) for an overlong Body/LinkPath BEFORE the save, so it falls back to
            // reusing the original channel's already-column-safe rendered fields here instead --
            // "a degraded resend is still a resend" -- and the resend still succeeds with 201, never
            // 500.
            logger.LogWarning(ex, "Resend re-render failed for notification {NotificationId} on channel {Channel}; falling back to the original channel's rendered fields.",
                original.Id, channel.DisplayName);
            return null;
        }
    }

    private static CommandResponse Violation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: nameof(NotificationAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}
