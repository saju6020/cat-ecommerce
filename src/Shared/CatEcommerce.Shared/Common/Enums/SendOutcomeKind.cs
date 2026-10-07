namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-NTF-001 §8.9/§10.1.</summary>
public sealed class SendOutcomeKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly SendOutcomeKind Accepted = new(1, nameof(Accepted));
    public static readonly SendOutcomeKind TransientFailure = new(2, nameof(TransientFailure));
    public static readonly SendOutcomeKind PermanentFailure = new(3, nameof(PermanentFailure));
}
