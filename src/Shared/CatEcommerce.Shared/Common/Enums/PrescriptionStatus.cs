namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-PHM-001 §10.1 -- The lifecycle of a prescription (Appendix C, ADR-142). Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class PrescriptionStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly PrescriptionStatus Issued = new(1, nameof(Issued));
    public static readonly PrescriptionStatus Dispensed = new(2, nameof(Dispensed));
    public static readonly PrescriptionStatus Completed = new(3, nameof(Completed));
    public static readonly PrescriptionStatus Expired = new(4, nameof(Expired));
    public static readonly PrescriptionStatus Cancelled = new(5, nameof(Cancelled));
}
