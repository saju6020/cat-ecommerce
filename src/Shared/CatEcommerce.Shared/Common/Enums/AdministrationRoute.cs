namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-PHM-001 §10.1 -- How a medicine is given. Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class AdministrationRoute(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly AdministrationRoute Oral = new(1, nameof(Oral));
    public static readonly AdministrationRoute Subcutaneous = new(2, nameof(Subcutaneous));
    public static readonly AdministrationRoute Intramuscular = new(3, nameof(Intramuscular));
    public static readonly AdministrationRoute Intravenous = new(4, nameof(Intravenous));
    public static readonly AdministrationRoute Topical = new(5, nameof(Topical));
    public static readonly AdministrationRoute Ophthalmic = new(6, nameof(Ophthalmic));
    public static readonly AdministrationRoute Otic = new(7, nameof(Otic));
    public static readonly AdministrationRoute Transdermal = new(8, nameof(Transdermal));
    public static readonly AdministrationRoute Inhaled = new(9, nameof(Inhaled));
    public static readonly AdministrationRoute Rectal = new(10, nameof(Rectal));
}
