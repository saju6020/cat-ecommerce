namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-APT-001 §8.2/§10.1 -- ordered by <see cref="Enumeration.Value"/>, highest first in
/// the queue (§8.7, US-CLN-05 AC1).</summary>
public sealed class TriagePriority(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly TriagePriority Routine = new(1, nameof(Routine));
    public static readonly TriagePriority Urgent = new(2, nameof(Urgent));
    public static readonly TriagePriority Emergency = new(3, nameof(Emergency));
}
