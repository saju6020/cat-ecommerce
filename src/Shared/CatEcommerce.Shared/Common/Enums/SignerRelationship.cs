namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-EMR-001 §10.1.</summary>
public sealed class SignerRelationship(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly SignerRelationship Owner = new(1, nameof(Owner));
    public static readonly SignerRelationship AuthorisedAgent = new(2, nameof(AuthorisedAgent));
}
