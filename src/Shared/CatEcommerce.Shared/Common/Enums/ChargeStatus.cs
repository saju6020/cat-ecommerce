namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-BIL-001 §8.3 / §10.1. A charge is billed when a live invoice line references it -- that is derived, never stored here.</summary>
public sealed class ChargeStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ChargeStatus Active = new(1, nameof(Active));
    public static readonly ChargeStatus Voided = new(2, nameof(Voided));
}
