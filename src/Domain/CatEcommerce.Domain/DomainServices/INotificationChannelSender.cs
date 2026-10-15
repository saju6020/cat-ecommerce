using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>HO-NTF-001 §8.9 / ADR-061 -- the provider-neutral sending port. Shape transcribed
/// verbatim from §8.9's own code block.</summary>
public interface INotificationChannelSender
{
    NotificationChannel Channel { get; }
    string ProviderName { get; }
    Task<SendOutcome> SendAsync(OutgoingMessage message, CancellationToken ct = default);
}

/// <summary><see cref="NotificationId"/> is passed to the provider as the idempotency reference
/// (§8.9 rule 4, at-least-once delivery).</summary>
public sealed record OutgoingMessage(
    Guid NotificationId,
    string Destination,
    string? Subject,
    string TextBody,
    string? HtmlBody,
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyList<OutgoingAttachment>? Attachments = null)
{
    /// <summary>HO-RPT-001 section 11.7: the files the message carries, as MIME parts (HO-NTF-001 section 8.9 rule 7: never logged). Empty by default.</summary>
    public IReadOnlyList<OutgoingAttachment> AttachmentList => Attachments ?? [];
}

/// <summary>A file attached to an outgoing email: loaded from its owner immediately before sending.</summary>
public sealed record OutgoingAttachment(string FileName, string ContentType, byte[] Content);

public sealed record SendOutcome(SendOutcomeKind Kind, string? ProviderMessageId, string? ProviderResponse);
