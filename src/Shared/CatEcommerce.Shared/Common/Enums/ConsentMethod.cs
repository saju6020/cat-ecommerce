namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-EMR-001 §10.1.</summary>
public sealed class ConsentMethod(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ConsentMethod OnScreenSignature = new(1, nameof(OnScreenSignature));
    public static readonly ConsentMethod SignedUpload = new(2, nameof(SignedUpload));
}
