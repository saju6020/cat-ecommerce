namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-BIL-001 §8.9 / §10.1.</summary>
public sealed class CreditNoteKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly CreditNoteKind Correction = new(1, nameof(Correction));
    public static readonly CreditNoteKind Return = new(2, nameof(Return));
    public static readonly CreditNoteKind Cancellation = new(3, nameof(Cancellation));
    public static readonly CreditNoteKind Goodwill = new(4, nameof(Goodwill));
}
