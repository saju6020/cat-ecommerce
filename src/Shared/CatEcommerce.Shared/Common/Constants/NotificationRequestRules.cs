namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-NTF-001 §8.4/§10.3.</summary>
public static class NotificationRequestRules
{
    public const int MaxVariables = 30;
    public const int MaxVariableLength = 2000;
    public const int MaxDeduplicationKeyLength = 200;

    /// <summary>Gate fix 2026-09-27 (SECURITY/CODE_REVIEW second pass, MEDIUM). A request whose
    /// expansion keeps failing to SAVE (never rendering -- a render failure is suppressed
    /// immediately, on its first attempt) is moved to a terminal state
    /// (<c>NotificationSuppressionReason.PersistenceFailed</c>) after this many failed attempts, so
    /// it can never retry forever and block every other request behind it in the
    /// <c>Worker:BatchSize</c>-capped queue.</summary>
    public const int MaxExpansionAttempts = 5;
}
