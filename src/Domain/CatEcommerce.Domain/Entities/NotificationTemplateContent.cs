using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Entities;

/// <summary>
/// HO-NTF-001 §8.1/§8.2. One channel's wording for a customised
/// <c>NotificationTemplateAggregate</c>. Exactly one per channel of the definition
/// (<c>TemplateChannelMismatch</c> otherwise).
/// </summary>
public sealed class NotificationTemplateContent
{
    public Guid Id { get; private set; }
    public NotificationChannel Channel { get; private set; } = default!;
    public string? Subject { get; private set; }
    public string? Title { get; private set; }
    public string Body { get; private set; } = default!;
    public string? LinkPath { get; private set; }

    private NotificationTemplateContent() { }

    public NotificationTemplateContent(Guid id, NotificationChannel channel, string? subject, string? title, string body, string? linkPath)
    {
        Id = id;
        Channel = channel;
        Subject = subject;
        Title = title;
        Body = body;
        LinkPath = linkPath;
    }
}
