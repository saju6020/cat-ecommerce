namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-SHP-001 section 8.11/10.1.</summary>
public sealed class ReviewRejectionReason(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ReviewRejectionReason OffTopic = new(1, nameof(OffTopic));
    public static readonly ReviewRejectionReason Offensive = new(2, nameof(Offensive));
    public static readonly ReviewRejectionReason PersonalInformation = new(3, nameof(PersonalInformation));
    public static readonly ReviewRejectionReason Spam = new(4, nameof(Spam));
    public static readonly ReviewRejectionReason Other = new(5, nameof(Other));
}
