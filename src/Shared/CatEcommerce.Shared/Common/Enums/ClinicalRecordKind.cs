namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-EMR-001 §10.1.</summary>
public sealed class ClinicalRecordKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ClinicalRecordKind Visit = new(1, nameof(Visit));
    public static readonly ClinicalRecordKind ClinicalHistory = new(2, nameof(ClinicalHistory));
    public static readonly ClinicalRecordKind DiagnosticOrder = new(3, nameof(DiagnosticOrder));
    public static readonly ClinicalRecordKind DischargeSummary = new(4, nameof(DischargeSummary));
    public static readonly ClinicalRecordKind Consent = new(5, nameof(Consent));
    public static readonly ClinicalRecordKind VisitList = new(6, nameof(VisitList));
}
