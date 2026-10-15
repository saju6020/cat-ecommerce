namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-SRG-001 -- user-facing messages shared by more than one handler or aggregate method, so the sequential and the concurrent path for one
/// rule cannot drift.</summary>
public static class SurgeryMessages
{
    public const string SurgeryNotFound = "Surgery not found.";
    public const string ConsentNotRecorded = "Consent has not been recorded for this surgery. Record consent, or a veterinarian may override in an emergency.";
    public const string StaffBusy = "The surgeon or assistant has just been booked elsewhere. Check availability again.";
    public const string TheatreBusy = "The theatre has just been booked. Check availability again.";
    public const string RecordIsImmutable = "This record can no longer be changed.";
    public const string AnaesthesiaEntryAlreadyCorrected = "That entry has just been corrected by someone else. Reload the log and correct the latest correction instead.";
    public const string DuplicateAnaesthesiaEntry = "This entry id has already been used for another record. Send the request again with a new entry id.";
    public const string AnaesthesiaEntryRaced = "This entry is already being saved by an identical request. Send the request again with the SAME entry id to get the saved result; do not use a new entry id.";
    public const string AnaesthesiaEntryIdMismatch = "This entry id was already used for this surgery with different values. Send the original values again to replay it, or use a new entry id for a different drug entry.";
    public const string AppointmentManagedBySurgery = "This appointment belongs to a surgery and can only be changed from the surgery.";
    public const string NotReadyForDischarge = "The cat is not ready for discharge. Set the recovery status to ready for discharge first.";
    public const string ServiceNotSurgical = "This service is not a theatre procedure.";
    public const string LogClosed = "The anaesthesia log is only open while the cat is in pre-op, in theatre or in recovery.";

    public static string InvalidTransition(string from, string to) => $"A surgery that is {Describe(from)} cannot be {to}.";

    private static string Describe(string status) => status switch
    {
        "PreOp" => "in pre-op",
        "InTheatre" => "in theatre",
        "Recovery" => "in recovery",
        _ => status.ToLowerInvariant(),
    };
}
