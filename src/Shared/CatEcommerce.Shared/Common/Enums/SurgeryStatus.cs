namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-SRG-001 §10.1. Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class SurgeryStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly SurgeryStatus Scheduled = new(1, nameof(Scheduled));
    public static readonly SurgeryStatus PreOp = new(2, nameof(PreOp));
    public static readonly SurgeryStatus InTheatre = new(3, nameof(InTheatre));
    public static readonly SurgeryStatus Recovery = new(4, nameof(Recovery));
    public static readonly SurgeryStatus Discharged = new(5, nameof(Discharged));
    public static readonly SurgeryStatus Cancelled = new(6, nameof(Cancelled));
}
