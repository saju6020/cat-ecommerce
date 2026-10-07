using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Xunit;

namespace CatEcommerce.Domain.Tests.Helpers;

/// <summary>HO-NTF-001 §15 U-D1. Pure -- no database.</summary>
public sealed class NotificationResendEvaluationTests
{
    private static ResendEvaluation Evaluate(
        bool secret = false, NotificationChannel? channel = null, NotificationStatus? status = null, bool active = true,
        bool marketing = false, bool marketingGranted = true, int priorResends = 0, bool deliverable = true, bool hasDestination = true) =>
        NotificationResendEvaluation.Evaluate(secret, channel ?? NotificationChannel.Sms, status ?? NotificationStatus.Undeliverable,
            active, marketing, marketingGranted, priorResends, deliverable, hasDestination);

    [Fact]
    public void Secret_Refused()
    {
        var result = Evaluate(secret: true);
        Assert.False(result.CanResend);
        Assert.Equal(NotificationMessages.ResendSecretReason, result.Reason);
    }

    [Fact]
    public void InApp_Refused()
    {
        var result = Evaluate(channel: NotificationChannel.InApp);
        Assert.False(result.CanResend);
        Assert.Equal(NotificationMessages.ResendInAppReason, result.Reason);
    }

    [Theory]
    [MemberData(nameof(StillDeliveringStatuses))]
    public void StillDelivering_Refused(NotificationStatus status)
    {
        var result = Evaluate(status: status);
        Assert.False(result.CanResend);
        Assert.Equal(NotificationMessages.ResendStillDeliveringReason, result.Reason);
    }

    public static IEnumerable<object[]> StillDeliveringStatuses()
    {
        yield return [NotificationStatus.Queued];
        yield return [NotificationStatus.Sending];
        yield return [NotificationStatus.RetryPending];
    }

    [Fact]
    public void RecipientInactive_Refused()
    {
        var result = Evaluate(active: false);
        Assert.False(result.CanResend);
        Assert.Equal(NotificationMessages.ResendRecipientInactiveReason, result.Reason);
    }

    [Fact]
    public void MarketingOptedOut_Refused()
    {
        var result = Evaluate(marketing: true, marketingGranted: false);
        Assert.False(result.CanResend);
        Assert.Equal(NotificationMessages.ResendRecipientOptedOutReason, result.Reason);
    }

    [Fact]
    public void ThreePriorResends_LimitReached()
    {
        var result = Evaluate(priorResends: 3);
        Assert.False(result.CanResend);
        Assert.Equal(BusinessViolationCodes.ResendLimitReached, result.ErrorCode);
    }

    [Fact]
    public void ChannelNotDeliverableOrNoDestination_Refused()
    {
        var notDeliverable = Evaluate(deliverable: false);
        Assert.Equal(BusinessViolationCodes.ResendChannelNotAvailable, notDeliverable.ErrorCode);

        var noDestination = Evaluate(hasDestination: false);
        Assert.Equal(BusinessViolationCodes.ResendChannelNotAvailable, noDestination.ErrorCode);
    }

    [Fact]
    public void AllClear_Allowed()
    {
        var result = Evaluate();
        Assert.True(result.CanResend);
        Assert.Null(result.ErrorCode);
    }
}
