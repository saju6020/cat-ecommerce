namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-SRG-001 §10.1. Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class PreOpChecklistItemKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly PreOpChecklistItemKind FastingInstructionsIssued = new(1, nameof(FastingInstructionsIssued));
    public static readonly PreOpChecklistItemKind PreAnaestheticBloodWork = new(2, nameof(PreAnaestheticBloodWork));
    public static readonly PreOpChecklistItemKind WeightRecorded = new(3, nameof(WeightRecorded));
    public static readonly PreOpChecklistItemKind ConsentSigned = new(4, nameof(ConsentSigned));
    public static readonly PreOpChecklistItemKind AsaGradeAssessed = new(5, nameof(AsaGradeAssessed));
}
