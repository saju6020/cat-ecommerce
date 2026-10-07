using System.Security.Cryptography;
using System.Text;
using CatEcommerce.Application.CommandHandlers.Notification;
using CatEcommerce.Application.Commands.Notification;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Time.Testing;

namespace CatEcommerce.Application.Tests.CommandHandlers.Notification;

/// <summary>HO-NTF-001 §15/§16 C9 -- I-E1/I-E2.</summary>
public sealed class UnsubscribeFromMarketingCommandHandlerTests
{
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static NotificationAggregate MarketingNotification(Guid recipientId, string tokenHash, DateTimeOffset now)
    {
        var notification = new NotificationAggregate(Guid.NewGuid());
        notification.CreateExternal(new NotificationCreateDto(
            Guid.NewGuid(), "shop.back-in-stock", 0, "en", NotificationCategory.Marketing, NotificationUrgency.Normal, false,
            NotificationRecipientKind.Customer, recipientId, null, null, null, null, null, NotificationChannel.Email, "a@example.com",
            "Subject", null, "Body", null, null, null, now, null, false, tokenHash, "https://x/unsubscribe", null, null, null));
        return notification;
    }

    [Fact]
    public async Task ValidToken_CurrentlySubscribed_RevokesAndReturnsWasSubscribedTrue()
    {
        var notifications = new FakeAggregateRootRepository<NotificationAggregate>();
        var preferences = new FakeAggregateRootRepository<CustomerNotificationPreferencesAggregate>();

        var customerId = Guid.NewGuid();
        const string token = "plain-token-value";
        var notification = MarketingNotification(customerId, Hash(token), DateTimeOffset.UtcNow);
        notifications.Seed(notification);

        var pref = new CustomerNotificationPreferencesAggregate(customerId);
        pref.Open(new CustomerNotificationPreferencesOpenDto(TestUserContext.Create(), Guid.NewGuid()));
        pref.ChangePreferences(new ChangeNotificationPreferencesDto(
            [new NotificationPreferenceChangeDto(NotificationChannel.Email, NotificationCategory.Marketing, true)],
            "test", DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid()));
        preferences.Seed(pref);

        var handler = new UnsubscribeFromMarketingCommandHandler(notifications, preferences, new FakeNotificationRecipientDirectory(), new FakeTimeProvider());
        var response = await handler.HandleAsync(new UnsubscribeFromMarketingCommand { Token = token, UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid() });

        Assert.True(response.IsSuccess);
        var outcome = Assert.IsType<UnsubscribeOutcome>(response.Result);
        Assert.True(outcome.WasSubscribed);
        Assert.Equal("Email", outcome.Channel);
        Assert.False(pref.Permits(NotificationChannel.Email, NotificationCategory.Marketing));
    }

    [Fact]
    public async Task UnknownToken_ReturnsInvalidUnsubscribeLink()
    {
        var handler = new UnsubscribeFromMarketingCommandHandler(
            new FakeAggregateRootRepository<NotificationAggregate>(), new FakeAggregateRootRepository<CustomerNotificationPreferencesAggregate>(),
            new FakeNotificationRecipientDirectory(), new FakeTimeProvider());

        var response = await handler.HandleAsync(new UnsubscribeFromMarketingCommand { Token = "unknown", UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid() });

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.InvalidUnsubscribeLink);
    }

    [Fact]
    public async Task AlreadyRevoked_ReturnsWasSubscribedFalse_WritesNothing()
    {
        var notifications = new FakeAggregateRootRepository<NotificationAggregate>();
        var preferences = new FakeAggregateRootRepository<CustomerNotificationPreferencesAggregate>();

        var customerId = Guid.NewGuid();
        const string token = "another-token";
        notifications.Seed(MarketingNotification(customerId, Hash(token), DateTimeOffset.UtcNow));
        // No preferences aggregate at all -- default is not granted (never subscribed).

        var handler = new UnsubscribeFromMarketingCommandHandler(notifications, preferences, new FakeNotificationRecipientDirectory(), new FakeTimeProvider());
        var response = await handler.HandleAsync(new UnsubscribeFromMarketingCommand { Token = token, UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid() });

        Assert.True(response.IsSuccess);
        var outcome = Assert.IsType<UnsubscribeOutcome>(response.Result);
        Assert.False(outcome.WasSubscribed);
        Assert.Empty(preferences.Saved);
        Assert.Empty(preferences.Updated);
    }

    /// <summary>HO-NTF-001 CODE_REVIEW gate fix (2026-09-27, MEDIUM). The notification's own
    /// RecipientId is the pre-merge duplicate (§11.6 keeps it as history); the unsubscribe must
    /// revoke consent on the SURVIVOR's own preferences root, not the duplicate's.</summary>
    [Fact]
    public async Task ValidToken_RecipientWasMergedIntoSurvivor_RevokesSurvivorsConsent_NotTheDuplicates()
    {
        var notifications = new FakeAggregateRootRepository<NotificationAggregate>();
        var preferences = new FakeAggregateRootRepository<CustomerNotificationPreferencesAggregate>();
        var directory = new FakeNotificationRecipientDirectory();

        var duplicateId = Guid.NewGuid();
        var survivorId = Guid.NewGuid();
        const string token = "merged-token";
        notifications.Seed(MarketingNotification(duplicateId, Hash(token), DateTimeOffset.UtcNow));

        directory.Seed(NotificationRecipientKind.Customer, duplicateId,
            new RecipientProfile(NotificationRecipientKind.Customer, survivorId, true, "Survivor", null, "survivor@example.com", NotificationChannel.Email, _ => true));

        var survivorPreferences = new CustomerNotificationPreferencesAggregate(survivorId);
        survivorPreferences.Open(new CustomerNotificationPreferencesOpenDto(TestUserContext.Create(), Guid.NewGuid()));
        survivorPreferences.ChangePreferences(new ChangeNotificationPreferencesDto(
            [new NotificationPreferenceChangeDto(NotificationChannel.Email, NotificationCategory.Marketing, true)],
            "test", DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid()));
        preferences.Seed(survivorPreferences);

        var handler = new UnsubscribeFromMarketingCommandHandler(notifications, preferences, directory, new FakeTimeProvider());
        var response = await handler.HandleAsync(new UnsubscribeFromMarketingCommand { Token = token, UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid() });

        Assert.True(response.IsSuccess);
        var outcome = Assert.IsType<UnsubscribeOutcome>(response.Result);
        Assert.True(outcome.WasSubscribed);
        Assert.False(survivorPreferences.Permits(NotificationChannel.Email, NotificationCategory.Marketing));
        Assert.DoesNotContain(duplicateId, preferences.Updated.Concat(preferences.Saved).Select(p => p.Id));
    }
}
