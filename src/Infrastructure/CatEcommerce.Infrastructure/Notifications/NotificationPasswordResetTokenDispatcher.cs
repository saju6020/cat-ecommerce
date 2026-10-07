using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Infrastructure.Notifications;

/// <summary>
/// HO-NTF-001 §11.4 -- replaces HO-ACC-001's <c>DevelopmentPasswordResetTokenDispatcher</c> and
/// <c>UndeliveredPasswordResetTokenDispatcher</c> (both deleted). Enqueues
/// <c>account.password-reset</c> through <see cref="INotificationOutbox"/> and returns without
/// saving -- the caller (<c>RequestPasswordResetCommandHandler</c>) must call this BEFORE the
/// token's own repository save, so both commit in one transaction (F23).
/// </summary>
public sealed class NotificationPasswordResetTokenDispatcher(INotificationOutbox outbox, INotificationLinkBuilder linkBuilder, TimeProvider timeProvider)
    : IPasswordResetTokenDispatcher
{
    public Task DispatchAsync(PasswordResetDispatchRequest request, CancellationToken ct = default)
    {
        var channelHint = request.Channel == PasswordResetChannel.Mobile ? NotificationChannel.Sms : NotificationChannel.Email;

        outbox.Enqueue(new NotificationRequestDto(
            RequestId: Guid.NewGuid(),
            TemplateKey: NotificationTemplateCatalogue.PasswordReset,
            RecipientKind: request.AccountKind == AccountKind.Customer ? NotificationRecipientKind.Customer : NotificationRecipientKind.Staff,
            RecipientId: request.AccountId,
            Variables: new Dictionary<string, string>
            {
                ["resetLink"] = linkBuilder.PasswordResetLink(request.PlainToken),
                // Gate fix 2026-09-27 (CODE_REVIEW LOW): TimeProvider, not DateTimeOffset.UtcNow --
                // matches every other instant in this slice, and can be driven by FakeTimeProvider.
                ["expiresInMinutes"] = Math.Max(1, (int)Math.Round((request.ExpiresOn - timeProvider.GetUtcNow()).TotalMinutes)).ToString()
            },
            ChannelHint: channelHint,
            NotAfter: request.ExpiresOn));

        return Task.CompletedTask;
    }
}
