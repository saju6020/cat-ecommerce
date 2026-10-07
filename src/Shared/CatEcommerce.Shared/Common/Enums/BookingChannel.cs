namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-APT-001 §8.2/§10.1. Amended by HO-SRG-001 with a further member (<c>Surgery</c>) --
/// see HO-APT-001 §1 Amended; that member is added by HO-SRG-001 itself, in its own change.</summary>
public sealed class BookingChannel(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly BookingChannel Online = new(1, nameof(Online));
    public static readonly BookingChannel Reception = new(2, nameof(Reception));
    public static readonly BookingChannel WalkIn = new(3, nameof(WalkIn));
    public static readonly BookingChannel Waitlist = new(4, nameof(Waitlist));
}
