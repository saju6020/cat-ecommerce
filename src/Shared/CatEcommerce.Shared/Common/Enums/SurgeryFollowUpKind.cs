namespace CatEcommerce.Shared.Common.Enums;

/// <summary>
/// HO-SRG-001 §10.1 -- <c>FollowUpKind</c> in the handover. Named <c>SurgeryFollowUpKind</c> because HO-EMR-001 already owns a different
/// <c>FollowUpKind</c> (Appointment / Reminder) in this namespace. Persisted by <c>Value</c> (ADR-022).
/// </summary>
public sealed class SurgeryFollowUpKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly SurgeryFollowUpKind WoundCheck = new(1, nameof(WoundCheck));
    public static readonly SurgeryFollowUpKind SutureRemoval = new(2, nameof(SutureRemoval));
    public static readonly SurgeryFollowUpKind Recheck = new(3, nameof(Recheck));
}
