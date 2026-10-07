using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Helpers;

/// <summary>HO-NTF-001 §11.4 C7/§12.6 -- "cannotResendReason computed by the SAME function C7
/// uses, so the screen and the command cannot disagree." Pure, no I/O.</summary>
public readonly record struct ResendEvaluation(bool CanResend, string? ErrorCode, string? Reason);

public static class NotificationResendEvaluation
{
    public static ResendEvaluation Evaluate(
        bool containsSecret, NotificationChannel channel, NotificationStatus status, bool recipientActive,
        bool isMarketing, bool marketingGrantedOnTargetChannel, int priorResendCount,
        bool targetChannelDeliverable, bool targetHasDestination)
    {
        if (containsSecret) return new ResendEvaluation(false, BusinessViolationCodes.NotificationCannotBeResent, NotificationMessages.ResendSecretReason);
        if (channel == NotificationChannel.InApp) return new ResendEvaluation(false, BusinessViolationCodes.NotificationCannotBeResent, NotificationMessages.ResendInAppReason);
        if (status == NotificationStatus.Queued || status == NotificationStatus.Sending || status == NotificationStatus.RetryPending)
            return new ResendEvaluation(false, BusinessViolationCodes.NotificationCannotBeResent, NotificationMessages.ResendStillDeliveringReason);
        if (!recipientActive) return new ResendEvaluation(false, BusinessViolationCodes.NotificationCannotBeResent, NotificationMessages.ResendRecipientInactiveReason);
        if (isMarketing && !marketingGrantedOnTargetChannel)
            return new ResendEvaluation(false, BusinessViolationCodes.NotificationCannotBeResent, NotificationMessages.ResendRecipientOptedOutReason);
        if (priorResendCount >= NotificationResendRules.MaxResendsPerNotification)
            return new ResendEvaluation(false, BusinessViolationCodes.ResendLimitReached, null);
        if (!targetChannelDeliverable || !targetHasDestination)
            return new ResendEvaluation(false, BusinessViolationCodes.ResendChannelNotAvailable, null);

        return new ResendEvaluation(true, null, null);
    }
}
