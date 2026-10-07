using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Notification;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Notification;

/// <summary>
/// HO-NTF-001 §12.4 -- <c>cannotResendReason</c> uses the SAME <c>NotificationResendEvaluation</c>
/// C7 uses (§12.6), so the screen and the command cannot disagree.
///
/// Gate fix 2026-09-27 (SECURITY HIGH / ARCHITECTURE HIGH 4):
/// 1. Projects with <c>Select</c> instead of <c>Include</c>-ing and materialising the aggregate
///    root, matching every other read handler and the read-side-pattern rule.
/// 2. For any secret-bearing template (<c>ContainsSecret</c>), <c>body</c>/<c>subject</c>/<c>title</c>
///    are ALWAYS <c>"[redacted]"</c>, regardless of <c>RedactedOn</c> -- previously the plaintext
///    (a live password-reset link) was served to any <c>NotificationLog.View</c> holder for the
///    entire window before the notification reached a terminal status (Queued/Sending/RetryPending
///    -- proven end to end, SECURITY probe P1: Reception reads a live reset link and takes over an
///    Administrator's account). Persisted redaction (via C5/C6) still happens on its own schedule
///    for defence in depth (the database row itself, backups, replicas); this is the display-time
///    backstop that closes the gap between "secret" and "persisted-redacted".
/// </summary>
public sealed class GetNotificationDetailQueryHandler(
    CatEcommerceDbContext db, INotificationRecipientDirectory recipientDirectory, INotificationResendCounter resendCounter)
    : IQueryHandlerAsync<GetNotificationDetailQuery, NotificationDetailViewModel?>
{
    public async Task<QueryResponse<NotificationDetailViewModel?>> HandleAsync(GetNotificationDetailQuery query)
    {
        var row = await db.Set<NotificationAggregate>().AsNoTracking()
            .Where(n => n.Id == query.NotificationId)
            .Select(n => new
            {
                n.Id, n.CreatedOn, n.TemplateKey, n.Category, n.Channel, n.Destination,
                n.RecipientKind, n.RecipientId, n.Status, n.AttemptCount, n.SentOn, n.NotBefore,
                n.ResendOfId, n.FallbackFromId, n.Subject, n.Title, n.Body, n.ContainsSecret,
                n.RedactedOn, n.RedactedReason, n.RequestId,
                Attempts = n.Attempts.OrderBy(a => a.AttemptedOn)
                    .Select(a => new { a.AttemptedOn, a.Outcome, a.ProviderName, a.ProviderMessageId, a.ProviderResponse })
                    .ToList()
            })
            .FirstOrDefaultAsync();
        if (row is null) return new QueryResponse<NotificationDetailViewModel?> { Result = null };

        var staffName = await db.StaffAccounts.AsNoTracking().Where(s => s.Id == row.RecipientId).Select(s => s.FullName).FirstOrDefaultAsync();
        var customerName = staffName is null
            ? await db.CustomerAccounts.AsNoTracking().Where(c => c.Id == row.RecipientId).Select(c => c.FullName).FirstOrDefaultAsync()
            : null;
        var recipientName = staffName ?? customerName ?? row.RecipientId.ToString();

        var chainRootId = row.ResendOfId ?? row.Id;
        var fallbacks = await db.Set<NotificationAggregate>().AsNoTracking().Where(n => n.FallbackFromId == row.Id).Select(n => n.Id).ToListAsync();
        var resends = await db.Set<NotificationAggregate>().AsNoTracking().Where(n => n.ResendOfId == chainRootId).Select(n => n.Id).ToListAsync();

        var recipient = await recipientDirectory.ResolveAsync(row.RecipientKind, row.RecipientId);
        var isMarketing = row.Category == NotificationCategory.Marketing;
        var priorResends = await resendCounter.CountResendsAsync(chainRootId);

        var resendableChannels = new List<string>();
        string? cannotResendReason = null;
        var canResend = false;

        foreach (var candidate in NotificationChannelRules.DeliverableChannels.Where(c => c != NotificationChannel.InApp))
        {
            var marketingGranted = recipient is not null && recipient.MarketingGranted(candidate);
            var destination = recipient?.DestinationFor(candidate);
            var evaluation = NotificationResendEvaluation.Evaluate(
                row.ContainsSecret, row.Channel, row.Status, recipient?.IsActive ?? false,
                isMarketing, marketingGranted, priorResends, NotificationChannelRules.IsDeliverable(candidate), destination is not null);

            if (evaluation.CanResend)
            {
                resendableChannels.Add(candidate.DisplayName);
                canResend = true;
            }
            else if (cannotResendReason is null)
            {
                cannotResendReason = evaluation.Reason ?? evaluation.ErrorCode;
            }
        }

        // SECURITY HIGH fix: a secret-bearing template is ALWAYS shown redacted, whatever RedactedOn
        // says -- see the type's own remarks.
        var showRedacted = row.ContainsSecret;
        var body = showRedacted ? "[redacted]" : row.Body;
        var subject = showRedacted ? (row.Subject is null ? null : "[redacted]") : row.Subject;
        var title = showRedacted ? (row.Title is null ? null : "[redacted]") : row.Title;
        var redactedReason = row.RedactedReason?.DisplayName ?? (showRedacted ? NotificationRedactionReason.Secret.DisplayName : null);

        var result = new NotificationDetailViewModel(
            row.Id, row.CreatedOn, row.TemplateKey, ResolveTemplateName(row.TemplateKey),
            row.Category.DisplayName, row.Channel.DisplayName, row.Destination,
            row.RecipientKind.DisplayName, row.RecipientId, recipientName, row.Status.DisplayName,
            row.AttemptCount, row.SentOn, row.NotBefore, row.ResendOfId is not null, row.FallbackFromId is not null,
            subject, title, body, redactedReason,
            row.Attempts.Select(a => new NotificationAttemptViewModel(a.AttemptedOn, a.Outcome.DisplayName, a.ProviderName, a.ProviderMessageId, a.ProviderResponse)).ToList(),
            new NotificationChainViewModel(row.RequestId, row.FallbackFromId, fallbacks, row.ResendOfId, resends),
            canResend, cannotResendReason, resendableChannels);

        return new QueryResponse<NotificationDetailViewModel?> { Result = result };
    }

    private static string ResolveTemplateName(string key) => NotificationTemplateCatalogue.Find(key)?.DisplayName ?? key;
}
