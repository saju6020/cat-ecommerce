using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.Operations;

/// <summary>HO-XCT-001 section 5.3 / ADR-305 -- <c>Redact(Retention)</c>: content and destination go, metadata stays, never overrides an anonymisation, idempotent.</summary>
public sealed class NotificationRetentionRedactionTests
{
    private static readonly DateTimeOffset Created = new(2024, 6, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 2, 0, 0, TimeSpan.Zero);

    private static NotificationAggregate CancelledSms(Guid recipientId)
    {
        var notification = new NotificationAggregate(Guid.NewGuid());
        notification.CreateExternal(new NotificationCreateDto(
            Guid.NewGuid(), "care.vaccination-due", 0, "en", NotificationCategory.Transactional, NotificationUrgency.Normal, false,
            NotificationRecipientKind.Customer, recipientId, null, null, null, null, null, NotificationChannel.Sms, "+8801700000000",
            "Subject", "Title", "Your cat Mitu is due", "<p>html</p>", "/inbox", null, Created, null, false, "tokenhash", "https://x/unsub", null, null, null, null, null));
        notification.Cancel(NotificationCloseReason.RecipientOptedOut, Created);
        return notification;
    }

    [Fact]
    public void Retention_RedactsContentAndDestination_KeepsMetadata()
    {
        var recipient = Guid.NewGuid();
        var notification = CancelledSms(recipient);

        notification.Redact(NotificationRedactionReason.Retention, Now);

        Assert.Equal("[redacted]", notification.Body);
        Assert.Equal("[redacted]", notification.Subject);
        Assert.Equal("[redacted]", notification.HtmlBody);
        Assert.Null(notification.Destination);
        Assert.Null(notification.UnsubscribeLink);
        Assert.Equal(NotificationRedactionReason.Retention, notification.RedactedReason);
        Assert.Equal(Now, notification.RedactedOn);
        Assert.Null(notification.AnonymisedOn);                         // it is not an anonymisation
        Assert.Equal(recipient, notification.RecipientId);              // metadata kept
        Assert.Equal("care.vaccination-due", notification.TemplateKey);
        Assert.Equal(NotificationStatus.Cancelled, notification.Status);
    }

    [Fact]
    public void Retention_IsIdempotent_AndKeepsTheFirstRedactedOn()
    {
        var notification = CancelledSms(Guid.NewGuid());
        notification.Redact(NotificationRedactionReason.Retention, Now);
        var version = notification.Version;

        notification.Redact(NotificationRedactionReason.Retention, Now.AddDays(1));

        Assert.Equal(Now, notification.RedactedOn);
        Assert.Equal(version, notification.Version);
    }

    [Fact]
    public void Retention_NeverOverridesAnAnonymisation()
    {
        var notification = CancelledSms(Guid.NewGuid());
        notification.Redact(NotificationRedactionReason.Anonymisation, Now);
        var version = notification.Version;

        notification.Redact(NotificationRedactionReason.Retention, Now.AddDays(1));

        Assert.Equal(NotificationRedactionReason.Anonymisation, notification.RedactedReason);
        Assert.Equal(version, notification.Version);
    }
}
