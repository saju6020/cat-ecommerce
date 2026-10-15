namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ORD-001 §10.1. The customer's reason for a return.</summary>
public sealed class ReturnReason(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ReturnReason WrongItem = new(1, nameof(WrongItem));
    public static readonly ReturnReason Damaged = new(2, nameof(Damaged));
    public static readonly ReturnReason QualityDefect = new(3, nameof(QualityDefect));
    public static readonly ReturnReason ChangedMind = new(4, nameof(ChangedMind));
    public static readonly ReturnReason Recalled = new(5, nameof(Recalled));
    public static readonly ReturnReason Other = new(6, nameof(Other));
}
