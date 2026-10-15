namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// Consultation and Medical Records' audit <c>EntityType</c> strings (HO-XCT-001 §5.2 C-ALL-5). The
/// interceptor's <c>DataChange</c> rows are named after the CLR aggregate root type; the
/// <c>BusinessEvent</c> handlers use the same names. The six clinical-record <em>access</em> rows
/// (<c>ClinicalRecordAccess</c> category, ADR-129) carry the <c>ClinicalRecordKind</c> display name as
/// their entity type. <c>ConsultationDraftAggregate</c> is excluded from change capture
/// (<c>AuditCaptureExclusions</c>) and so writes none. The three catalogue roots are
/// <c>RecordClass.Operational</c>, not clinical.
/// </summary>
public static partial class ClinicalAuditEntityTypes
{
    private static readonly IReadOnlyList<string> ConsultationEntries =
    [
        "VisitAggregate",
        "VisitAddendumAggregate",
        "DiagnosticOrderAggregate",
        "ConsentRecordAggregate",
        "Visit",
        "ClinicalHistory",
        "DiagnosticOrder",
        "DischargeSummary",
        "Consent",
        "VisitList",
    ];
}
