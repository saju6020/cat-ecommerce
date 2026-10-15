namespace CatEcommerce.Shared.Common.Constants;

/// <summary>Surgery's audit <c>EntityType</c> strings (HO-XCT-001 §5.2 C-ALL-5, HO-SRG-001). The interceptor's <c>DataChange</c> rows are named after the CLR
/// aggregate root type; the <c>BusinessEvent</c> handlers (<c>SurgeryConsentOverridden</c>, <c>ControlledDrugAdministered</c>, <c>NeuterStatusNotApplied</c>)
/// use the same names. Protocols and campaigns are practice configuration, not a record of care, so they are not listed.</summary>
public static partial class ClinicalAuditEntityTypes
{
    private static readonly IReadOnlyList<string> SurgeryEntries =
    [
        "SurgeryAggregate",
        "AnaesthesiaLogEntryAggregate",
        "SurgicalReportAggregate",
        "Surgery",
    ];
}
