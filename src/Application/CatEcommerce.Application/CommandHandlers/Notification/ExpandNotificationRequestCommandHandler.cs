using System.Security.Cryptography;
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

/// <summary>HO-NTF-001 §11.4 C4 / ADR-056/ADR-058/ADR-059/ADR-064 -- idempotent: a second run on a
/// request already past <c>Pending</c> returns success and writes nothing.</summary>
public sealed class ExpandNotificationRequestCommandHandler(
    ILogger<ExpandNotificationRequestCommandHandler> logger,
    IAggregateRootRepository<NotificationRequestAggregate> requestRepository,
    IAggregateRootRepository<NotificationTemplateAggregate> templateRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    INotificationTemplateCatalogue catalogue,
    INotificationRecipientDirectory recipientDirectory,
    ISubjectGuardResolver subjectGuardResolver,
    IQuietHoursCalendar quietHoursCalendar,
    INotificationLinkBuilder linkBuilder,
    INotificationEnlister enlister,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<ExpandNotificationRequestCommand>
{
    public async Task<CommandResponse> HandleAsync(ExpandNotificationRequestCommand command)
    {
        var request = await requestRepository.GetByIdAsync(command.RequestId);
        if (request is null || request.Status != NotificationRequestStatus.Pending) return Success();

        var now = timeProvider.GetUtcNow();

        if (request.NotAfter is not null && request.NotAfter <= now)
        {
            request.Expire(now);
            await requestRepository.UpdateAsync(request, EventPublishOption.InMemoryPublishOnly);
            return Success();
        }

        var definition = catalogue.Find(request.TemplateKey);
        if (definition is null)
        {
            logger.LogError("Notification request {RequestId} references unknown template {TemplateKey}.", request.Id, request.TemplateKey);
            return await SuppressAsync(request, NotificationSuppressionReason.UnknownTemplate, now);
        }

        var recipient = await recipientDirectory.ResolveAsync(request.RecipientKind, request.RecipientId);
        if (recipient is null || !recipient.IsActive)
            return await SuppressAsync(request, NotificationSuppressionReason.RecipientInactive, now);

        var guardValid = await subjectGuardResolver.IsStillValidAsync(request.SubjectKind, request.RecipientId, request.SubjectId, request.SubjectRef, request.SubjectDueOn, request.SubjectStamp);
        if (!guardValid)
            return await SuppressAsync(request, NotificationSuppressionReason.SubjectNoLongerApplies, now);

        var plan = NotificationRoutingPolicy.Plan(definition, recipient, request.ChannelHint);
        if (plan.SuppressionReason is not null)
            return await SuppressAsync(request, plan.SuppressionReason, now);

        var isMarketing = definition.Category == NotificationCategory.Marketing;
        var missingVariable = definition.Variables.FirstOrDefault(v => !request.Variables.ContainsKey(v.Name));
        if (missingVariable is not null)
        {
            logger.LogError("Notification request {RequestId} for template {TemplateKey} is missing variable {Variable}.",
                request.Id, request.TemplateKey, missingVariable.Name);
            return await SuppressAsync(request, NotificationSuppressionReason.MissingVariable, now);
        }

        var branchId = request.BranchId ?? AdministrationSeed.MainBranchId;
        var branch = await branchRepository.GetByIdAsync(branchId);

        var standardValues = new Dictionary<string, string>(request.Variables, StringComparer.Ordinal)
        {
            [StandardTemplateVariables.RecipientName] = recipient.FullName,
            [StandardTemplateVariables.ClinicName] = branch?.Name ?? "PurrCare",
            [StandardTemplateVariables.ClinicPhone] = branch?.PhoneNumber ?? ""
        };

        var customisedTemplate = await templateRepository.GetByFilterAsync(t => t.Key == request.TemplateKey);

        var built = new List<NotificationAggregate>();
        try
        {
            if (plan.PrimaryChannel is { } primary)
            {
                var notification = await BuildExternalAsync(definition, customisedTemplate, request, recipient, primary, standardValues, isMarketing, now);
                if (notification is not null) built.Add(notification);
            }

            if (plan.InboxCopy)
            {
                var inbox = BuildInbox(definition, customisedTemplate, request, recipient, standardValues, now);
                if (inbox is not null) built.Add(inbox);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException or ArgumentOutOfRangeException)
        {
            // Gate fix 2026-09-27 (SECURITY/CODE_REVIEW MEDIUM 7): narrowly catching only
            // InvalidOperationException left a poison template/value (a malformed Date, a
            // sentinel-character injection that slipped past validation, ...) throwing
            // ArgumentOutOfRangeException/FormatException uncaught here -- the request was never
            // suppressed and stayed Pending forever, retried every poll, and (being the OLDEST
            // pending row) blocked every other pending request behind it in the same
            // BatchSize-capped queue, including password resets (head-of-line blocking).
            // Suppressing immediately, on ANY render failure, means a poison row leaves Pending on
            // its very first failed attempt and can never fill a future batch again.
            //
            // Gate fix 2026-09-27 (CODE_REVIEW second pass, HIGH): that MEDIUM-7 fix had gone too far
            // the other way -- widening the catch to `Exception ex when (ex is not
            // OperationCanceledException)` also swallowed genuine infra failures from the
            // `await quietHoursCalendar.NextAllowedInstantAsync(...)` DB read a few lines below in
            // BuildExternalAsync, permanently Suppressing a request (silent, irrecoverable data loss,
            // including of password resets) on a momentary database hiccup instead of leaving it
            // Pending to retry on the next poll. This is now a CLOSED set of the exception types
            // NotificationRenderer.Render (and its Url/Date value resolution) actually throws for a
            // poison template/value -- InvalidOperationException (missing/invalid resolved value),
            // FormatException (a malformed Date) and ArgumentOutOfRangeException (a corrupted
            // sentinel round-trip). No database exception type is ever caught here: it propagates
            // out of this handler, is logged by NotificationWorkCycle.DispatchAsync's per-item
            // catch-all exactly like any other transient infra failure, and the request stays
            // Pending for the next pass to retry.
            //
            // Gate fix 2026-09-27 (SECURITY/CODE_REVIEW second pass, MEDIUM): a rendered field
            // longer than its `notify.notification` column used to reach SaveChangesAsync -- OUTSIDE
            // this try -- and raise Postgres 22001, which is a persistence failure, not a render one
            // (see RecordNotificationExpansionFailureCommandHandler for that half of the fix).
            // RenderedMessageColumnLimits.Enforce now checks column length BEFORE that save is ever
            // attempted, from inside BuildExternalAsync/BuildInbox (i.e. inside THIS try), and throws
            // NotificationRenderOverflowException -- which derives from InvalidOperationException,
            // so it is already caught by this same closed set -- when Body or LinkPath cannot be
            // safely shortened. Subject/Title are truncated in place instead, never thrown for.
            logger.LogError(ex, "Rendering failed for notification request {RequestId}.", request.Id);
            return await SuppressAsync(request, NotificationSuppressionReason.RenderFailed, now);
        }

        if (built.Count == 0)
            return await SuppressAsync(request, NotificationSuppressionReason.NoPermittedChannel, now);

        foreach (var notification in built) enlister.Enlist(notification);

        request.MarkExpanded(now);
        await requestRepository.UpdateAsync(request, EventPublishOption.InMemoryPublishOnly); // one commit (ADR-056)

        return Success();
    }

    private async Task<NotificationAggregate?> BuildExternalAsync(
        NotificationTemplateDefinition definition, NotificationTemplateAggregate? customised, NotificationRequestAggregate request,
        RecipientProfile recipient, NotificationChannel channel, IReadOnlyDictionary<string, string> baseValues, bool isMarketing, DateTimeOffset now)
    {
        var (content, version) = EffectiveNotificationTemplate.ChannelFor(definition, customised, channel);
        if (content is null) return null;

        var values = new Dictionary<string, string>(baseValues, StringComparer.Ordinal);
        string? unsubscribeTokenHash = null;
        string? unsubscribeLink = null;
        if (isMarketing)
        {
            var (plainToken, hash) = IssueUnsubscribeToken();
            unsubscribeLink = linkBuilder.UnsubscribeLink(plainToken);
            values[StandardTemplateVariables.UnsubscribeLink] = unsubscribeLink;
            unsubscribeTokenHash = hash;
        }

        var rendered = RenderedMessageColumnLimits.Enforce(NotificationRenderer.Render(content, definition.Variables, values, channel));
        var destination = recipient.DestinationFor(channel);

        DateTimeOffset? notBefore = null;
        var deferred = false;
        if (definition.Urgency == NotificationUrgency.Normal)
        {
            var deferUntil = await quietHoursCalendar.NextAllowedInstantAsync(now, request.BranchId);
            if (deferUntil is not null) { notBefore = deferUntil; deferred = true; }
        }

        var notification = new NotificationAggregate(Guid.NewGuid());
        notification.CreateExternal(new NotificationCreateDto(
            request.Id, request.TemplateKey, version, "en", definition.Category, definition.Urgency, definition.ContainsSecret,
            request.RecipientKind, request.RecipientId, request.SubjectKind, request.SubjectId, request.SubjectRef, request.SubjectDueOn,
            request.BranchId, channel, destination, rendered.Subject, rendered.Title, rendered.TextBody, rendered.HtmlBody, rendered.LinkPath,
            request.NotAfter, now, notBefore, deferred, unsubscribeTokenHash, unsubscribeLink, FallbackFromId: null, ResendOfId: null, ResentBy: null,
            SubjectStamp: request.SubjectStamp, AttachmentKind: request.AttachmentKind, AttachmentId: request.AttachmentId));

        return notification;
    }

    private NotificationAggregate? BuildInbox(
        NotificationTemplateDefinition definition, NotificationTemplateAggregate? customised, NotificationRequestAggregate request,
        RecipientProfile recipient, IReadOnlyDictionary<string, string> baseValues, DateTimeOffset now)
    {
        var (content, version) = EffectiveNotificationTemplate.ChannelFor(definition, customised, NotificationChannel.InApp);
        if (content is null) return null;

        var rendered = RenderedMessageColumnLimits.Enforce(NotificationRenderer.Render(content, definition.Variables, baseValues, NotificationChannel.InApp));

        var notification = new NotificationAggregate(Guid.NewGuid());
        notification.CreateInbox(new NotificationCreateDto(
            request.Id, request.TemplateKey, version, "en", definition.Category, definition.Urgency, definition.ContainsSecret,
            request.RecipientKind, request.RecipientId, request.SubjectKind, request.SubjectId, request.SubjectRef, request.SubjectDueOn,
            request.BranchId, NotificationChannel.InApp, null, rendered.Subject, rendered.Title, rendered.TextBody, rendered.HtmlBody, rendered.LinkPath,
            request.NotAfter, now, null, false, null, null, FallbackFromId: null, ResendOfId: null, ResentBy: null,
            SubjectStamp: request.SubjectStamp, AttachmentKind: request.AttachmentKind, AttachmentId: request.AttachmentId));

        return notification;
    }

    /// <summary>256-bit random token; only the SHA-256 hash is stored (§8.11, ADR-064).</summary>
    /// <summary>The hash is computed over the token's own base64url STRING encoding (its UTF-8
    /// bytes), never the raw random bytes -- C9 (<c>UnsubscribeFromMarketingCommandHandler</c>)
    /// only ever has the string form (read back off the incoming URL), so hashing the raw bytes
    /// here would make the two hashes never match anything the verifier could compute.</summary>
    internal static (string PlainToken, string Hash) IssueUnsubscribeToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
        return (token, hash);
    }

    private async Task<CommandResponse> SuppressAsync(NotificationRequestAggregate request, NotificationSuppressionReason? reason, DateTimeOffset now)
    {
        request.Suppress(reason ?? NotificationSuppressionReason.NoPermittedChannel, now);
        await requestRepository.UpdateAsync(request, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }

    private static CommandResponse Success() => new() { IsSuccess = true };
}
