using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Infrastructure.Notifications.Senders;

/// <summary>HO-NTF-001 §8.9 -- the default outside Development. Always a permanent failure; logs
/// nothing about the message (there is nothing to log).</summary>
public sealed class UnconfiguredChannelSender(NotificationChannel channel) : INotificationChannelSender
{
    public NotificationChannel Channel { get; } = channel;
    public string ProviderName => "Unconfigured";

    public Task<SendOutcome> SendAsync(OutgoingMessage message, CancellationToken ct = default)
    {
        var response = Channel == NotificationChannel.Sms ? NotificationMessages.NoSmsProviderConfigured : NotificationMessages.NoEmailProviderConfigured;
        return Task.FromResult(new SendOutcome(SendOutcomeKind.PermanentFailure, null, response));
    }
}
