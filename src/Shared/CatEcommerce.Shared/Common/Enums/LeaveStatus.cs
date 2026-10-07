namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ADM-001 §10.1 / §8.4.</summary>
public sealed class LeaveStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly LeaveStatus Requested = new(1, nameof(Requested));
    public static readonly LeaveStatus Approved = new(2, nameof(Approved));
    public static readonly LeaveStatus Rejected = new(3, nameof(Rejected));
    public static readonly LeaveStatus Cancelled = new(4, nameof(Cancelled));
}
