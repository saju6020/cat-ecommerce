namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-EMR-001 §10.1.</summary>
public sealed class ConsentKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ConsentKind Surgery = new(1, nameof(Surgery));
    public static readonly ConsentKind Anaesthesia = new(2, nameof(Anaesthesia));
    public static readonly ConsentKind Euthanasia = new(3, nameof(Euthanasia));
}
