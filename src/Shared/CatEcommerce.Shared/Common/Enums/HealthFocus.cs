namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-SHP-001 section 8.3/10.1 -- food-only, up to 6 per product (section 8.13, the
/// recommendation input).</summary>
public sealed class HealthFocus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly HealthFocus Sterilised = new(1, nameof(Sterilised));
    public static readonly HealthFocus WeightManagement = new(2, nameof(WeightManagement));
    public static readonly HealthFocus Urinary = new(3, nameof(Urinary));
    public static readonly HealthFocus Renal = new(4, nameof(Renal));
    public static readonly HealthFocus Digestive = new(5, nameof(Digestive));
    public static readonly HealthFocus SkinAndCoat = new(6, nameof(SkinAndCoat));
    public static readonly HealthFocus Hairball = new(7, nameof(Hairball));
    public static readonly HealthFocus Dental = new(8, nameof(Dental));
    public static readonly HealthFocus Sensitive = new(9, nameof(Sensitive));
}
