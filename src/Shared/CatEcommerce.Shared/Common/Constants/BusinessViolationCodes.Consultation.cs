namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-EMR-001 §10.4 -- Consultation and Medical Records' own violation codes. See ADR-341.
/// Codes that already exist in another area's partial (FileNotFound, FileTooLarge, CatIsDeceased, ...)
/// are consumed, not redefined. The stock and treatment codes (Phase F, US-CLN-15) are at the end; the stock refusals themselves (BatchExpired, InsufficientStock,
/// InvalidQuantity, FractionalQuantityNotAllowed, SkuNotFound, SkuInactive, ServiceNotPriced) are Inventory's, Shop's and Billing's and are reused.</summary>
public static partial class BusinessViolationCodes
{
    public const string VisitNotFound = nameof(VisitNotFound);
    public const string VisitAlreadyExists = nameof(VisitAlreadyExists);
    public const string AppointmentNotReadyForVisit = nameof(AppointmentNotReadyForVisit);
    public const string VisitBelongsToAnotherBranch = nameof(VisitBelongsToAnotherBranch);
    public const string VisitFinalised = nameof(VisitFinalised);
    public const string VitalsRequired = nameof(VitalsRequired);
    public const string VitalSignOutOfRange = nameof(VitalSignOutOfRange);
    public const string VitalsOutsidePlausibleRange = nameof(VitalsOutsidePlausibleRange);
    public const string DiagnosisCodeInactive = nameof(DiagnosisCodeInactive);
    public const string DiagnosisCodeNotFound = nameof(DiagnosisCodeNotFound);
    public const string DuplicateProblem = nameof(DuplicateProblem);
    public const string ProblemRequired = nameof(ProblemRequired);
    public const string AssessmentAndPlanRequired = nameof(AssessmentAndPlanRequired);
    public const string OnlyVeterinarianMayFinalise = nameof(OnlyVeterinarianMayFinalise);
    public const string AddendumRequiresFinalisedVisit = nameof(AddendumRequiresFinalisedVisit);
    public const string DiagnosisCodeAlreadyExists = nameof(DiagnosisCodeAlreadyExists);
    public const string SystemDiagnosisCodeReadOnly = nameof(SystemDiagnosisCodeReadOnly);
    public const string LabAnalyteAlreadyExists = nameof(LabAnalyteAlreadyExists);
    public const string LabAnalyteNotFound = nameof(LabAnalyteNotFound);
    public const string InvalidReferenceRange = nameof(InvalidReferenceRange);
    public const string NoteTemplateAlreadyExistsForService = nameof(NoteTemplateAlreadyExistsForService);
    public const string NoteTemplateNotFound = nameof(NoteTemplateNotFound);
    public const string DiagnosticOrderNotFound = nameof(DiagnosticOrderNotFound);
    public const string DiagnosticOrderHasResults = nameof(DiagnosticOrderHasResults);
    public const string DiagnosticOrderCancelled = nameof(DiagnosticOrderCancelled);
    public const string InvalidDiagnosticResult = nameof(InvalidDiagnosticResult);
    public const string ResultSetNotFound = nameof(ResultSetNotFound);
    public const string FollowUpDateInPast = nameof(FollowUpDateInPast);
    public const string FollowUpAppointmentInvalid = nameof(FollowUpAppointmentInvalid);
    public const string ConsentEvidenceMismatch = nameof(ConsentEvidenceMismatch);
    public const string ConsentAppointmentInvalid = nameof(ConsentAppointmentInvalid);
    public const string ConsentFormVersionNotCurrent = nameof(ConsentFormVersionNotCurrent);
    public const string ConsentNotFound = nameof(ConsentNotFound);
    public const string ConsentAlreadyWithdrawn = nameof(ConsentAlreadyWithdrawn);
    public const string InvalidSignature = nameof(InvalidSignature);
    public const string FilePurposeMismatch = nameof(FilePurposeMismatch);
    public const string FileAlreadyAttached = nameof(FileAlreadyAttached);
    public const string VisitNotFinalised = nameof(VisitNotFinalised);
    public const string VisitInProgress = nameof(VisitInProgress);

    // Phase F -- treatments and stock (US-CLN-15, ADR-126).
    public const string TreatmentLineNotFound = nameof(TreatmentLineNotFound);
    public const string TreatmentAlreadyReversed = nameof(TreatmentAlreadyReversed);
    public const string ControlledDrugNotSupportedHere = nameof(ControlledDrugNotSupportedHere);
    public const string DispensaryNotConfigured = nameof(DispensaryNotConfigured);
    public const string BatchNotIssuable = nameof(BatchNotIssuable);
    public const string BatchOverrideReasonRequired = nameof(BatchOverrideReasonRequired);
    public const string SkuNotPriced = nameof(SkuNotPriced);
    public const string TreatmentLineAlreadyRecorded = nameof(TreatmentLineAlreadyRecorded);
    public const string TreatmentDescriptionRequired = nameof(TreatmentDescriptionRequired);

    /// <summary>C8 -- the caller's <c>ExpectedVersion</c> no longer matches the visit's current version
    /// (checked in the handler before <c>SaveNote</c> mutates it). Maps to <c>409</c>, the same shape a
    /// <c>DbUpdateConcurrencyException</c> on <c>xmin</c> produces.</summary>
    public const string StaleVisitVersion = nameof(StaleVisitVersion);

    /// <summary>Defensive aggregate-level bounds (the validators reject the same shapes first).</summary>
    public const string ClinicalTextTooLong = nameof(ClinicalTextTooLong);
    public const string ClinicalLimitExceeded = nameof(ClinicalLimitExceeded);
    public const string ClinicalDateInFuture = nameof(ClinicalDateInFuture);
    public const string DiagnosticDescriptionRequired = nameof(DiagnosticDescriptionRequired);
    public const string InvalidDiagnosisCode = nameof(InvalidDiagnosisCode);
}
