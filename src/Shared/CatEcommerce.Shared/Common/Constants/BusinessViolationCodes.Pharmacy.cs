namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-PHM-001 §10.4 -- Pharmacy and Prescriptions' own violation codes. See ADR-341. A code another area already declares
/// (<c>SkuNotFound</c>, <c>ProductNotFound</c>, <c>VisitNotFound</c>, <c>BatchExpired</c>, <c>StaleVersion</c>, ...) is reused, never redeclared.
/// The ledger's own codes (<c>InsufficientStock</c>, <c>AllocationOverrideReasonRequired</c>) are HO-IMP-001's, passed through.</summary>
public static partial class BusinessViolationCodes
{
    // Formulary
    public const string FormularyCodeAlreadyExists = nameof(FormularyCodeAlreadyExists);
    public const string FormularyMedicineNotFound = nameof(FormularyMedicineNotFound);
    public const string InvalidStrength = nameof(InvalidStrength);
    public const string DoseRangeRequired = nameof(DoseRangeRequired);
    public const string InvalidDoseRange = nameof(InvalidDoseRange);
    public const string InvalidInteraction = nameof(InvalidInteraction);
    public const string ControlledScheduleRequired = nameof(ControlledScheduleRequired);
    public const string ControlledFlagChangeReasonRequired = nameof(ControlledFlagChangeReasonRequired);
    // SkuNotFound -- already declared by another area; reused.
    public const string SkuAlreadyLinkedToMedicine = nameof(SkuAlreadyLinkedToMedicine);
    public const string MedicineInactive = nameof(MedicineInactive);

    // Prescribing
    // VisitNotFound -- already declared by another area; reused.
    public const string VisitCatMismatch = nameof(VisitCatMismatch);
    public const string VisitNotOpen = nameof(VisitNotOpen);
    public const string CatNotTreatable = nameof(CatNotTreatable);
    public const string RouteNotAllowedForMedicine = nameof(RouteNotAllowedForMedicine);
    public const string InvalidDuration = nameof(InvalidDuration);
    // InvalidQuantity -- already declared by another area; reused.
    public const string InvalidValidity = nameof(InvalidValidity);
    public const string RepeatsRequireValidity = nameof(RepeatsRequireValidity);
    public const string RecentWeightRequired = nameof(RecentWeightRequired);
    public const string PrescriptionWarningsNotAcknowledged = nameof(PrescriptionWarningsNotAcknowledged);
    public const string PrescriptionWarningsChanged = nameof(PrescriptionWarningsChanged);
    public const string AllergyAcknowledgementReasonRequired = nameof(AllergyAcknowledgementReasonRequired);
    public const string PrescriptionNotFound = nameof(PrescriptionNotFound);
    public const string InvalidPrescriptionTransition = nameof(InvalidPrescriptionTransition);
    // CancellationReasonRequired -- already declared by another area; reused.
    public const string PrescriptionAlreadyRenewed = nameof(PrescriptionAlreadyRenewed);
    public const string InvalidDose = nameof(InvalidDose);
    public const string InvalidInstructions = nameof(InvalidInstructions);
    public const string PrescribingNotPermitted = nameof(PrescribingNotPermitted);

    // Dispensing
    public const string MedicineNotStocked = nameof(MedicineNotStocked);
    public const string SkuNotLinkedToMedicine = nameof(SkuNotLinkedToMedicine);
    // BatchExpired -- already declared by another area; reused.
    public const string BatchNotDispensable = nameof(BatchNotDispensable);
    public const string OnlyExpiredStockAvailable = nameof(OnlyExpiredStockAvailable);
    public const string InsufficientDispensaryStock = nameof(InsufficientDispensaryStock);
    public const string DispenseQuantityMismatch = nameof(DispenseQuantityMismatch);
    public const string FefoOverrideReasonRequired = nameof(FefoOverrideReasonRequired);
    public const string TooManyDispensingLines = nameof(TooManyDispensingLines);
    public const string PrescriptionValidityEnded = nameof(PrescriptionValidityEnded);
    public const string NoSuppliesRemaining = nameof(NoSuppliesRemaining);
    public const string DispensingNotFound = nameof(DispensingNotFound);
    public const string DispensingNotReversible = nameof(DispensingNotReversible);
    public const string ReversalWindowPassed = nameof(ReversalWindowPassed);
    public const string ReversalReasonRequired = nameof(ReversalReasonRequired);

    // Witness and register
    public const string WitnessRequired = nameof(WitnessRequired);
    public const string WitnessNotVerified = nameof(WitnessNotVerified);
    public const string WitnessMustDifferFromDispenser = nameof(WitnessMustDifferFromDispenser);
    public const string InvalidRegisterQuantity = nameof(InvalidRegisterQuantity);
    public const string RegisterEntryNotFound = nameof(RegisterEntryNotFound);
    public const string CorrectionReasonRequired = nameof(CorrectionReasonRequired);
    public const string CorrectionTargetMismatch = nameof(CorrectionTargetMismatch);
    public const string InvalidRegisterEntry = nameof(InvalidRegisterEntry);

    // Repeats
    public const string NotRepeatable = nameof(NotRepeatable);
    public const string FirstSupplyNotDispensed = nameof(FirstSupplyNotDispensed);
    public const string NoRepeatsRemaining = nameof(NoRepeatsRemaining);
    public const string RepeatRequestAlreadyOpen = nameof(RepeatRequestAlreadyOpen);
    public const string RepeatRequestNotFound = nameof(RepeatRequestNotFound);
    public const string DeclineReasonRequired = nameof(DeclineReasonRequired);

    // Diet
    // ProductNotFound -- already declared by another area; reused.
    public const string ProductNotPrescriptionDiet = nameof(ProductNotPrescriptionDiet);
    public const string DietAuthorisationAlreadyActive = nameof(DietAuthorisationAlreadyActive);
    public const string DietAuthorisationNotFound = nameof(DietAuthorisationNotFound);
    public const string InvalidDietValidity = nameof(InvalidDietValidity);
    public const string DietAuthorisationNotExtendable = nameof(DietAuthorisationNotExtendable);
    public const string RevocationReasonRequired = nameof(RevocationReasonRequired);
}
