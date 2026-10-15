namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-SRG-001 §8.12 -- the defaults used when a surgical service has no protocol of its own. They are the SRS examples; the clinic replaces them
/// with its own (OQ-5). All text is plain text.</summary>
public static class SurgeryProtocolDefaults
{
    public const int FastingHours = 8;

    public const string FastingInstructions = "No food from midnight before surgery; water may be given until {admitBy}.";

    public const string PostOpInstructions =
        "Keep your cat indoors, warm and quiet for the next 10 to 14 days. Offer a small meal and water this evening. " +
        "Do not let your cat lick or scratch the wound; use the cone if it is supplied. Check the wound twice a day. " +
        "Call the clinic at once if the wound is red, swollen or open, if your cat is not eating after 24 hours, or if she is very quiet or in pain.";

    public sealed record FollowUpDefault(string Kind, int DayOffset, int WindowFromDay, int WindowToDay);

    public static readonly IReadOnlyList<FollowUpDefault> FollowUps =
    [
        new("WoundCheck", 3, 2, 4),
        new("SutureRemoval", 12, 10, 14),
    ];

    public static string FastingInstructionsFor(string admitByLocal) => FastingInstructions.Replace("{admitBy}", admitByLocal, StringComparison.Ordinal);
}
