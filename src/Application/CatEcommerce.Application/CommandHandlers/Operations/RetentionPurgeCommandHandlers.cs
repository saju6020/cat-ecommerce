using System.Text.Json;
using CatEcommerce.Application.Commands.Operations;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Operations;

/// <summary>
/// HO-XCT-001 section 11.4 C7 / ADR-304 -- one batch of a purge pass: the <c>PassKind</c> picks the matching <see cref="IRetentionPurger"/> method (a
/// switch; an unknown kind is a programming error and throws). Writes ONE business-event audit row with the kind, the cutoff and the count when
/// something was deleted (an empty batch writes nothing -- it would be seven rows of noise a night). Returns the count.
/// </summary>
public sealed class PurgeExpiredRecordsCommandHandler(
    IRetentionPurger purger,
    IAggregateRootRepository<AuditEntryAggregate> audit,
    IClientIpAccessor clientIp,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<PurgeExpiredRecordsCommand>
{
    public async Task<CommandResponse> HandleAsync(PurgeExpiredRecordsCommand command)
    {
        var kind = Enumeration.FromDisplayName<RetentionPassKind>(command.PassKind);

        var purged = kind.DisplayName switch
        {
            nameof(RetentionPassKind.PurgeSessions) =>
                await purger.PurgeCustomerSessionsAsync(command.Cutoff, command.BatchSize, CancellationToken.None) +
                await purger.PurgeStaffSessionsAsync(command.Cutoff, command.BatchSize, CancellationToken.None),
            nameof(RetentionPassKind.PurgeResetTokens) => await purger.PurgePasswordResetTokensAsync(command.Cutoff, command.BatchSize, CancellationToken.None),
            nameof(RetentionPassKind.PurgeWebVitals) => await purger.PurgeWebVitalBatchesAsync(command.Cutoff, command.BatchSize, CancellationToken.None),
            nameof(RetentionPassKind.PurgeBackupRuns) => await purger.PurgeBackupRunsAsync(command.Cutoff, command.BatchSize, CancellationToken.None),
            _ => throw new InvalidOperationException($"'{command.PassKind}' is not a purge pass handled by the retention purger."),
        };

        if (purged > 0)
        {
            await OperationsAudit.WriteBusinessEventAsync(
                audit, clientIp, timeProvider.GetUtcNow(), command.UserContext, command.CorrelationId,
                entityType: "Retention", entityId: command.CorrelationId, action: "RetentionPurge", reason: kind.DisplayName,
                afterValue: JsonSerializer.Serialize(new { kind = kind.DisplayName, cutoff = command.Cutoff, count = purged }));
        }

        return OperationsResponses.Ok(purged);
    }
}

/// <summary>HO-XCT-001 section 11.4 C8 -- one batch of one registered purge source, by <c>SourceKey</c>. Same audit rule as C7.</summary>
public sealed class RunPurgeSourceCommandHandler(
    IEnumerable<IRetentionPurgeSource> sources,
    IAggregateRootRepository<AuditEntryAggregate> audit,
    IClientIpAccessor clientIp,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RunPurgeSourceCommand>
{
    public async Task<CommandResponse> HandleAsync(RunPurgeSourceCommand command)
    {
        var source = sources.FirstOrDefault(s => s.SourceKey == command.SourceKey);
        if (source is null)
            return OperationsResponses.Violation(BusinessViolationCodes.PurgeSourceNotRegistered, nameof(command.SourceKey), "That purge source is not registered.", "Operations");

        var result = await source.PurgeAsync(command.Cutoff, command.BatchSize, CancellationToken.None);

        if (result.Purged > 0 || result.Failed > 0)
        {
            await OperationsAudit.WriteBusinessEventAsync(
                audit, clientIp, timeProvider.GetUtcNow(), command.UserContext, command.CorrelationId,
                entityType: "Retention", entityId: command.CorrelationId, action: "RetentionPurge", reason: command.SourceKey,
                afterValue: JsonSerializer.Serialize(new { source = command.SourceKey, cutoff = command.Cutoff, result.Examined, result.Purged, result.Held, result.Failed }));
        }

        return OperationsResponses.Ok(result);
    }
}

/// <summary>
/// HO-XCT-001 section 11.4 C9 / ADR-305. Redacts one notification (<c>Redact(Retention)</c>: body, subject, title, HTML and link, and the destination) or
/// the variables of one notification request, once the content is past <c>retention.notificationContentMonths</c>. Already redacted is a success.
/// Metadata -- channel, template, status, timestamps, recipient id -- is kept. Excluded from audit capture by HO-NTF-001 ADR-066.
/// </summary>
public sealed class RedactExpiredNotificationContentCommandHandler(
    IAggregateRootRepository<NotificationAggregate> notifications,
    IAggregateRootRepository<NotificationRequestAggregate> requests,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RedactExpiredNotificationContentCommand>
{
    public async Task<CommandResponse> HandleAsync(RedactExpiredNotificationContentCommand command)
    {
        var now = timeProvider.GetUtcNow();

        if (command.NotificationId is { } notificationId)
        {
            var notification = await notifications.GetByIdAsync(notificationId);
            if (notification is not null && notification.RedactedReason != NotificationRedactionReason.Retention && notification.AnonymisedOn is null)
            {
                notification.Redact(NotificationRedactionReason.Retention, now);
                await notifications.UpdateAsync(notification, EventPublishOption.InMemoryPublishOnly);
            }
        }

        if (command.RequestId is { } requestId)
        {
            var request = await requests.GetByIdAsync(requestId);
            if (request is not null && request.VariablesRedactedOn is null)
            {
                request.RedactVariables(now);
                await requests.UpdateAsync(request, EventPublishOption.InMemoryPublishOnly);
            }
        }

        return OperationsResponses.Ok();
    }
}
