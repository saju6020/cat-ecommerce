using System.Collections.Concurrent;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Hosting;

namespace CatEcommerce.Infrastructure.Notifications.Senders;

/// <summary>HO-NTF-001 §8.9 -- Testing only; the constructor throws otherwise. Records every
/// message for test assertions and returns the outcome the test factory set for that destination
/// (default: <see cref="SendOutcomeKind.Accepted"/>).</summary>
public sealed class CapturingChannelSender : INotificationChannelSender
{
    private readonly ConcurrentBag<OutgoingMessage> _sent = [];

    public CapturingChannelSender(NotificationChannel channel, IHostEnvironment environment)
    {
        if (environment.EnvironmentName != "Testing")
            throw new InvalidOperationException($"{nameof(CapturingChannelSender)} must never be registered outside Testing.");

        Channel = channel;
    }

    public NotificationChannel Channel { get; }
    public string ProviderName => "Capturing";

    public IReadOnlyCollection<OutgoingMessage> Sent => _sent.ToArray();

    /// <summary>Destination (case-insensitive) -&gt; a queue of outcomes to return in order, one
    /// per <see cref="SendAsync"/> call; once exhausted, <see cref="SendOutcomeKind.Accepted"/>.</summary>
    public ConcurrentDictionary<string, ConcurrentQueue<SendOutcome>> ScriptedOutcomes { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Task<SendOutcome> SendAsync(OutgoingMessage message, CancellationToken ct = default)
    {
        _sent.Add(message);

        if (ScriptedOutcomes.TryGetValue(message.Destination, out var queue) && queue.TryDequeue(out var scripted))
            return Task.FromResult(scripted);

        return Task.FromResult(new SendOutcome(SendOutcomeKind.Accepted, $"capture-{message.NotificationId:N}", null));
    }
}
