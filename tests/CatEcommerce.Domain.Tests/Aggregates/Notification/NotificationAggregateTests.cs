using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.Domain.Tests.Aggregates.Notification;

/// <summary>HO-NTF-001 §15 U-B11..U-B13.</summary>
public sealed class NotificationAggregateTests
{
    private static NotificationAggregate ExternalSms(DateTimeOffset createdOn) =>
        Build(createdOn, NotificationChannel.Sms, "+8801700000000");

    private static NotificationAggregate Build(DateTimeOffset createdOn, NotificationChannel channel, string? destination)
    {
        var aggregate = new NotificationAggregate(Guid.NewGuid());
        aggregate.CreateExternal(new NotificationCreateDto(
            Guid.NewGuid(), "care.vaccination-due", 0, "en", NotificationCategory.Transactional, NotificationUrgency.Normal, false,
            NotificationRecipientKind.Customer, Guid.NewGuid(), null, null, null, null, null, channel, destination,
            null, null, "Body", null, null, null, createdOn, null, false, null, null, null, null, null));
        return aggregate;
    }

    [Fact]
    public void RecordAttempt_TransientFiveTimes_ThenTransient_BackoffThenUndeliverable()
    {
        var now = DateTimeOffset.UtcNow;
        var notification = ExternalSms(now);
        notification.Claim(now, TimeSpan.FromMinutes(2));

        var expectedGaps = new[] { 1, 5, 15, 60, 240 };
        var current = now;
        for (var i = 0; i < 5; i++)
        {
            notification.RecordAttempt(SendOutcomeKind.TransientFailure, current, "Test", null, "err");
            Assert.Equal(NotificationStatus.RetryPending, notification.Status);
            Assert.Equal(current + TimeSpan.FromMinutes(expectedGaps[i]), notification.NextAttemptOn);

            current = notification.NextAttemptOn!.Value;
            notification.Claim(current, TimeSpan.FromMinutes(2));
        }

        // 6th attempt -- attempts exhausted.
        notification.RecordAttempt(SendOutcomeKind.TransientFailure, current, "Test", null, "err");
        Assert.Equal(NotificationStatus.Undeliverable, notification.Status);
        Assert.Equal(6, notification.AttemptCount);
    }

    [Fact]
    public void RecordAttempt_PermanentFailure_UndeliverableAtOnce()
    {
        var now = DateTimeOffset.UtcNow;
        var notification = ExternalSms(now);
        notification.Claim(now, TimeSpan.FromMinutes(2));

        notification.RecordAttempt(SendOutcomeKind.PermanentFailure, now, "Test", null, "no");

        Assert.Equal(NotificationStatus.Undeliverable, notification.Status);
        Assert.Equal(1, notification.AttemptCount);
    }

    [Fact]
    public void Claim_NotDue_Throws()
    {
        var now = DateTimeOffset.UtcNow;
        var notification = ExternalSms(now);
        notification.Claim(now, TimeSpan.FromMinutes(2)); // now Sending

        var ex = Assert.Throws<DomainRuleViolationException>(() => notification.Claim(now, TimeSpan.FromMinutes(2)));
        Assert.Equal(BusinessViolationCodes.NotificationNotDue, ex.ErrorCode);
    }

    [Fact]
    public void Claim_LeaseExpired_CanBeReclaimed()
    {
        var now = DateTimeOffset.UtcNow;
        var notification = ExternalSms(now);
        notification.Claim(now, TimeSpan.FromMinutes(2));

        var afterLease = now.AddMinutes(3);
        Assert.True(notification.CanClaim(afterLease));
        notification.Claim(afterLease, TimeSpan.FromMinutes(2));
        Assert.Equal(NotificationStatus.Sending, notification.Status);
    }

    // NTF-001 backlog row 5: claims whose outcome was never recorded are capped, then the row is parked.
    [Fact]
    public void Claim_RepeatedlyWithoutARecordedAttempt_IsParkedAtTheCap_NotReclaimedForever()
    {
        var now = DateTimeOffset.UtcNow;
        var notification = ExternalSms(now);
        var lease = TimeSpan.FromMinutes(2);
        var current = now;

        for (var i = 1; i <= NotificationRetryRules.MaxAttempts; i++)
        {
            Assert.False(notification.MustPark(current));
            notification.Claim(current, lease);
            Assert.Equal(i, notification.ClaimCount);
            current = current.AddMinutes(3); // lease expires; the save after the send never happens
        }

        Assert.True(notification.CanClaim(current));
        Assert.True(notification.MustPark(current));

        notification.Park(current);

        Assert.Equal(NotificationStatus.Undeliverable, notification.Status);
        Assert.False(notification.CanClaim(current.AddMinutes(10)));
        Assert.Equal(SendOutcomeKind.PermanentFailure, notification.Attempts.Last().Outcome);
    }

    [Fact]
    public void Park_BeforeTheCap_Throws()
    {
        var now = DateTimeOffset.UtcNow;
        var notification = ExternalSms(now);
        notification.Claim(now, TimeSpan.FromMinutes(2));

        Assert.Throws<DomainRuleViolationException>(() => notification.Park(now.AddMinutes(3)));
    }

    [Fact]
    public void Redact_ClearsBodyAndTitleAndSubject_KeepsDestination_UnlessAnonymisation()
    {
        var now = DateTimeOffset.UtcNow;
        var notification = new NotificationAggregate(Guid.NewGuid());
        notification.CreateExternal(new NotificationCreateDto(
            Guid.NewGuid(), "account.password-reset", 0, "en", NotificationCategory.Transactional, NotificationUrgency.Urgent, true,
            NotificationRecipientKind.Customer, Guid.NewGuid(), null, null, null, null, null, NotificationChannel.Sms, "+8801700000000",
            "Subject", "Title", "Body with link", null, null, null, now, null, false, null, null, null, null, null));

        notification.Redact(NotificationRedactionReason.Secret, now);

        Assert.Equal("[redacted]", notification.Body);
        Assert.Equal("[redacted]", notification.Subject);
        Assert.Equal("[redacted]", notification.Title);
        Assert.Equal("+8801700000000", notification.Destination);
        Assert.NotNull(notification.RedactedOn);
        Assert.Equal(NotificationRedactionReason.Secret, notification.RedactedReason);

        // Idempotent.
        notification.Redact(NotificationRedactionReason.Secret, now.AddMinutes(1));
        Assert.Equal(now, notification.RedactedOn);
    }

    [Fact]
    public void Redact_Anonymisation_AlsoNullsDestinationAndUnsubscribeLink()
    {
        var now = DateTimeOffset.UtcNow;
        var notification = ExternalSms(now);
        notification.Redact(NotificationRedactionReason.Anonymisation, now);

        Assert.Null(notification.Destination);
        Assert.NotNull(notification.AnonymisedOn);
        Assert.Equal(NotificationRedactionReason.Anonymisation, notification.RedactedReason);
    }

    /// <summary>HO-NTF-001 SECURITY/ARCHITECTURE/CODE_REVIEW gate fix (2026-09-27, HIGH). Before the
    /// fix, <c>Redact</c> returned immediately whenever <c>RedactedOn</c> was already set -- so a
    /// delivered password reset (Secret-redacted within minutes of being sent) kept its real
    /// destination forever once the customer was later anonymised. A16/ADR-041.</summary>
    [Fact]
    public void Redact_Anonymisation_AfterAlreadySecretRedacted_StillNullsDestination()
    {
        var now = DateTimeOffset.UtcNow;
        var notification = new NotificationAggregate(Guid.NewGuid());
        notification.CreateExternal(new NotificationCreateDto(
            Guid.NewGuid(), "account.password-reset", 0, "en", NotificationCategory.Transactional, NotificationUrgency.Urgent, true,
            NotificationRecipientKind.Customer, Guid.NewGuid(), null, null, null, null, null, NotificationChannel.Sms, "+8801700000000",
            null, null, "Body with link", null, null, null, now, null, false, null, null, null, null, null));

        notification.Redact(NotificationRedactionReason.Secret, now); // as C5/C6 do, minutes after send
        Assert.Equal("+8801700000000", notification.Destination);

        notification.Redact(NotificationRedactionReason.Anonymisation, now.AddDays(30)); // later, on anonymisation

        Assert.Null(notification.Destination);
        Assert.NotNull(notification.AnonymisedOn);
        Assert.Equal(NotificationRedactionReason.Anonymisation, notification.RedactedReason); // overrides Secret
        Assert.Equal("[redacted]", notification.Body); // still redacted, unaffected

        // Idempotent on its own marker.
        var anonymisedOn = notification.AnonymisedOn;
        notification.Redact(NotificationRedactionReason.Anonymisation, now.AddDays(60));
        Assert.Equal(anonymisedOn, notification.AnonymisedOn);
    }
}
