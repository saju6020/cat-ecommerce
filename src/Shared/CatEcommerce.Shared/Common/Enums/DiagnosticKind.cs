namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-EMR-001 §10.1.</summary>
public sealed class DiagnosticKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly DiagnosticKind BloodWork = new(1, nameof(BloodWork));
    public static readonly DiagnosticKind Urinalysis = new(2, nameof(Urinalysis));
    public static readonly DiagnosticKind FelvFivTest = new(3, nameof(FelvFivTest));
    public static readonly DiagnosticKind Imaging = new(4, nameof(Imaging));
    public static readonly DiagnosticKind Other = new(5, nameof(Other));
}
