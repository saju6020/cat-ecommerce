namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-APT-001 §8.11/§10.1. <c>Lapsed</c> is terminal (OQ-8) -- the customer may join again,
/// but a lapsed entry never rejoins the queue.</summary>
public sealed class WaitlistStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly WaitlistStatus Waiting = new(1, nameof(Waiting));
    public static readonly WaitlistStatus Offered = new(2, nameof(Offered));
    public static readonly WaitlistStatus Claimed = new(3, nameof(Claimed));
    public static readonly WaitlistStatus Lapsed = new(4, nameof(Lapsed));
    public static readonly WaitlistStatus Withdrawn = new(5, nameof(Withdrawn));
    public static readonly WaitlistStatus Closed = new(6, nameof(Closed));
}
