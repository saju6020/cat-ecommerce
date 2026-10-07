namespace CatEcommerce.Shared.Common.Enums;

/// <summary>
/// HO-APT-001 §8.3/§10.1 (ADR-067). Appendix C's states plus two pre-booking states, <see
/// cref="Held"/> and <see cref="HoldExpired"/>/<see cref="HoldReleased"/>, which never appear as an
/// appointment to a customer or on any staff list -- a hold is a row in the same table so the same
/// exclusion constraints (ADR-068) protect it, but it is not a booking.
/// </summary>
public sealed class AppointmentStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly AppointmentStatus Held = new(1, nameof(Held));
    public static readonly AppointmentStatus HoldExpired = new(2, nameof(HoldExpired));
    public static readonly AppointmentStatus HoldReleased = new(3, nameof(HoldReleased));
    public static readonly AppointmentStatus Booked = new(4, nameof(Booked));
    public static readonly AppointmentStatus CheckedIn = new(5, nameof(CheckedIn));
    public static readonly AppointmentStatus InConsultation = new(6, nameof(InConsultation));
    public static readonly AppointmentStatus Completed = new(7, nameof(Completed));
    public static readonly AppointmentStatus Cancelled = new(8, nameof(Cancelled));
    public static readonly AppointmentStatus NoShow = new(9, nameof(NoShow));
}
