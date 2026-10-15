namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-EMR-001 -- user-facing violation messages shared by more than one handler or
/// aggregate method. Each says what to do next (NFR-USA-4).</summary>
public static class ClinicalMessages
{
    public const string VisitFinalised = "This visit has been finalised and can no longer be changed. Add an addendum instead.";
    public const string VisitNotFound = "Visit not found.";
    public const string AppointmentNotReadyForVisit = "The appointment must be checked in before a visit can be opened.";
    public const string OnlyVeterinarianMayFinalise = "Only a veterinarian can finalise a visit.";
    public const string ProblemRequired = "Add at least one diagnosis before finalising. Use \"Routine visit — no abnormality detected\" for a well visit.";
    public const string AssessmentAndPlanRequired = "Write the assessment and the plan before finalising.";
    public const string AddendumRequiresFinalisedVisit = "This visit is still open — edit the note instead.";
    public const string VitalsRequired = "Enter at least one measurement.";
    public const string VitalsOutsidePlausibleRange = "Some values are outside the usual range. Check them, then confirm to save anyway.";
    public const string VisitBelongsToAnotherBranch = "This visit belongs to another branch.";
    public const string DiagnosticOrderHasResults = "This order already has results or files and cannot be cancelled.";
    public const string ConsentFormNotPublished = "This consent form has not been published yet.";
    public const string PrescriptionsOnLabel = "Medicines to take home are on the prescription label.";
    public const string VisitNotFinalised = "Finalise the consultation before completing the appointment.";
    public const string TreatmentLineNotFound = "Treatment line not found.";
    public const string TreatmentAlreadyReversed = "This treatment has already been reversed.";
    public const string ControlledDrugNotSupportedHere = "This is a controlled drug. Record it in the controlled-drug register, not as a treatment.";
    public const string DispensaryNotConfigured = "This branch has no clinic dispensary. Ask an administrator to set one up before recording stock items.";
    public const string BatchOverrideReasonRequired = "That is not the batch that expires first. Give a reason of at least five characters.";
    public const string BatchNotIssuable = "That batch is not held in this branch's dispensary or can no longer be used. Choose another batch.";
    public const string SkuNotPriced = "This item has no price yet, so it cannot be billed. Record it as not billable or ask for a price to be set.";
    public const string ReversalReasonRequired = "Say why this treatment is being reversed.";
    public const string TreatmentDescriptionRequired = "Describe the treatment.";

    public static string InsufficientStockInDispensary(decimal available, string unit, string item) =>
        $"Only {available.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)} {unit} of {item} are in the dispensary. Record anyway to note the shortfall for reordering, or change the quantity.";

    public const string StaleVisit = "This visit was changed by someone else. Reload and try again.";
}
