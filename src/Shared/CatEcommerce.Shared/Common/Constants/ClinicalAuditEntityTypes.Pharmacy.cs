namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// Pharmacy and Prescriptions' audit <c>EntityType</c> strings (HO-XCT-001 §5.2 C-ALL-5, HO-PHM-001). The interceptor's <c>DataChange</c> rows are
/// named after the CLR aggregate root type; the <c>BusinessEvent</c> handlers (<c>PrescriptionWarningsAcknowledged</c>, <c>LabelPrinted</c>,
/// <c>ControlledDrugRegisterExported</c>) use the same names. The formulary is practice configuration, not a record of care, so it is not
/// listed; the register, its seals and the witness-verification rows are.
/// </summary>
public static partial class ClinicalAuditEntityTypes
{
    private static readonly IReadOnlyList<string> PharmacyEntries =
    [
        "PrescriptionAggregate",
        "DispensingAggregate",
        "ControlledDrugRegisterEntryAggregate",
        "ControlledDrugRegisterSealAggregate",
        "DietAuthorisationAggregate",
        "Prescription",
        "Dispensing",
        "ControlledDrugRegister",
        "DietAuthorisation",
    ];
}
