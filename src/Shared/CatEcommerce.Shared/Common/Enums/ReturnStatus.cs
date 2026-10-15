namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ORD-001 §10.1. A return request's lifecycle.</summary>
public sealed class ReturnStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ReturnStatus Requested = new(1, nameof(Requested));
    public static readonly ReturnStatus Approved = new(2, nameof(Approved));
    public static readonly ReturnStatus Rejected = new(3, nameof(Rejected));
    public static readonly ReturnStatus Received = new(4, nameof(Received));
    public static readonly ReturnStatus Closed = new(5, nameof(Closed));
}
