using System.Globalization;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-PHM-001 §8 -- user-facing text shared by more than one handler, aggregate or read, kept in one place so wording is the same wherever it
/// appears. Owner-facing messages (the gate, the repeat refusal) carry no clinical detail (U-G4).
/// </summary>
public static class PharmacyMessages
{
    public const string WitnessNotVerified = "The witness could not be verified. Ask them to enter their own sign-in details.";
    public const string WitnessRequired = "A controlled drug needs a witness. The witness enters their own identifier and password.";
    public const string WitnessMustDiffer = "The witness must be a different person from the one dispensing or recording.";
    public const string DoseNotCheckable = "This dose could not be checked against a mg/kg range.";
    public const string NoAllergiesRecorded = "No allergies recorded.";
    public const string RegisterAppendOnly = "controlled-drug register entries are append-only";
    public const string DietNeedsAuthorisation = "This food needs a vet's authorisation for your cat. Book a consultation to get one.";

    public static string Date(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    public static string RecentWeightMissing(string catName) =>
        $"Record {catName}'s weight before prescribing -- none has been recorded.";

    public static string RecentWeightStale(string catName, decimal kilograms, DateOnly recordedOn, int ageDays) =>
        $"Record {catName}'s weight before prescribing -- the latest was {kilograms.ToString("0.00", CultureInfo.InvariantCulture)} kg on {Date(recordedOn)} ({ageDays} days ago).";

    public static string PrescriptionValidityEnded(DateOnly validUntil) =>
        $"This prescription could be repeated until {Date(validUntil)}. A vet needs to authorise a new prescription -- please book a consultation or call the clinic.";

    public static string DietAuthorisationEnded(string catName, DateOnly validTo) =>
        $"{catName}'s authorisation for this food ended on {Date(validTo)}.";

    public static string BatchExpired(string lotNumber, DateOnly expiresOn) =>
        $"Batch {lotNumber} expired on {Date(expiresOn)} and cannot be dispensed.";

    public static string OnlyExpiredStockAvailable(string medicineName, string lotNumber, DateOnly expiresOn) =>
        $"The only {medicineName} in the dispensary is expired (batch {lotNumber}, {Date(expiresOn)}). It cannot be dispensed -- ask Inventory to write it off and restock.";

    public static string InsufficientDispensaryStock(decimal available) =>
        $"Only {available.ToString("0.##", CultureInfo.InvariantCulture)} is available in the dispensary, and a supply is dispensed in full or not at all.";

    public static string DuplicateActivePrescription(string catName, string medicineName, string number) =>
        $"{catName} already has an active prescription for {medicineName} ({number}).";

    public static string InvalidTransition(string from, string to) => $"A prescription cannot go from {from} to {to}.";
}
