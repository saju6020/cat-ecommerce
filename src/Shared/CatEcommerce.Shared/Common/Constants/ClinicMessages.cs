namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-APT-001 -- user-facing violation messages shared by more than one handler or
/// aggregate method, kept in one place so wording is consistent.</summary>
public static class ClinicMessages
{
    public const string SlotNoLongerAvailable = "That slot is no longer available. Please choose another.";
    public const string SlotHoldExpired = "Your hold on this slot has expired. Please choose again.";
    public const string ChangeCutoffPassed = "This appointment can no longer be changed online. Please contact the clinic.";
    public const string CatNotBookable = "This cat cannot be booked for an appointment.";
    public const string ServiceNotBookable = "This service cannot be booked.";
}
