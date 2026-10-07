namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-NTF-001 §8.2/§10.1.</summary>
public sealed class TemplateVariableKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly TemplateVariableKind Text = new(1, nameof(Text));
    public static readonly TemplateVariableKind Url = new(2, nameof(Url));
    public static readonly TemplateVariableKind Date = new(3, nameof(Date));
}
