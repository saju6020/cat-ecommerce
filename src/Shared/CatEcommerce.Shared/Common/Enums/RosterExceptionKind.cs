namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ADM-001 §10.1.</summary>
public sealed class RosterExceptionKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly RosterExceptionKind ExtraShift = new(1, nameof(ExtraShift));
    public static readonly RosterExceptionKind ShiftCancelled = new(2, nameof(ShiftCancelled));
}
