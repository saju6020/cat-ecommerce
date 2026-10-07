namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ADM-001 §10.1.</summary>
public sealed class LeaveKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly LeaveKind Annual = new(1, nameof(Annual));
    public static readonly LeaveKind Sick = new(2, nameof(Sick));
    public static readonly LeaveKind Training = new(3, nameof(Training));
    public static readonly LeaveKind Other = new(4, nameof(Other));
}
