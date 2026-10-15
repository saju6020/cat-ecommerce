namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-BIL-001 §8.8 / §10.1.</summary>
public sealed class PaymentStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly PaymentStatus PendingApproval = new(1, nameof(PendingApproval));
    public static readonly PaymentStatus Accepted = new(2, nameof(Accepted));
    public static readonly PaymentStatus Rejected = new(3, nameof(Rejected));
    public static readonly PaymentStatus Reversed = new(4, nameof(Reversed));
}
