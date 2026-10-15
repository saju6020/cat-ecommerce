namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-EMR-001 §8.2/§10.3 -- lengths and bounds of the visit record. All clinical text is
/// plain text: it is never stored or rendered as markup.</summary>
public static class ClinicalNoteRules
{
    public const int MaxSoapFieldLength = 20_000;
    public const int MaxDischargeInstructionsLength = 10_000;
    public const int MaxProblems = 20;
    public const int MaxProblemNoteLength = 500;
    public const int MaxVitalsEntries = 20;
    public const int MaxVitalsNoteLength = 500;
    public const int MaxPhotos = 20;
    public const int MaxTreatmentLines = 100;

    /// <summary>The largest quantity a treatment line can hold (numeric(12,3)); stock items are bounded by StockQuantity.Max, which is the same figure.</summary>
    public const decimal MaxTreatmentQuantity = 999_999_999.999m;
    public const int MaxTreatmentDescriptionLength = 300;
    public const int MaxDoseLength = 100;
    public const int MaxRouteLength = 50;
    public const int MaxBatchOverrideReasonLength = 300;
    public const int MaxTreatmentReversalReasonLength = 300;
    public const int MinBatchOverrideReasonLength = 5;
    public const int MaxPhotoNoteLength = 500;
    public const int MaxAddendumLength = 4000;
    public const int MaxAddendumReasonLength = 500;
    public const int MaxServiceNameLength = 200;
    public const int MaxDiagnosisCodeLength = 30;
    public const int MaxDiagnosisNameLength = 200;
    public const int MaxCatalogueNameLength = 200;
    public const int MaxCategoryLength = 100;
    public const int MaxExternalCodeLength = 50;

    /// <summary>D-8's assumed default (7 years, SRS Q-1) used by the clinical retention review
    /// until HO-XCT-001 registers <c>retention.clinicalYears</c> in the settings registry.</summary>
    public const int DefaultClinicalRetentionYears = 7;
}
