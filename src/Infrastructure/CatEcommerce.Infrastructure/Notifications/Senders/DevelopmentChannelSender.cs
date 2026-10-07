using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CatEcommerce.Infrastructure.Notifications.Senders;

/// <summary>HO-NTF-001 §8.9 -- Development only; the constructor throws otherwise. Logs at
/// <c>Warning</c> with a "NOT SENT -- development sender" banner so a developer can click the reset
/// link from the console. Never logs outside Development (ADR-063).</summary>
public sealed class DevelopmentChannelSender : INotificationChannelSender
{
    private readonly ILogger<DevelopmentChannelSender> _logger;

    public DevelopmentChannelSender(NotificationChannel channel, IHostEnvironment environment, ILogger<DevelopmentChannelSender> logger)
    {
        if (!environment.IsDevelopment())
            throw new InvalidOperationException($"{nameof(DevelopmentChannelSender)} must never be registered outside Development.");

        Channel = channel;
        _logger = logger;
    }

    public NotificationChannel Channel { get; }
    public string ProviderName => "Development";

    public Task<SendOutcome> SendAsync(OutgoingMessage message, CancellationToken ct = default)
    {
        _logger.LogWarning(
            "NOT SENT -- development sender. Channel: {Channel}. Destination: {Destination}. Subject: {Subject}. Body: {Body}",
            Channel.DisplayName, message.Destination, message.Subject, message.TextBody);

        return Task.FromResult(new SendOutcome(SendOutcomeKind.Accepted, ProviderMessageId: $"dev-{message.NotificationId:N}", ProviderResponse: null));
    }
}
