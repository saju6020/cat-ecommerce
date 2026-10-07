namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// SECURITY gate, LOW (2026-09-26, second pass). HO-CAT-001 §8.2/§8.3 give each Allergies/
/// ChronicConditions/Medications list item its own field-length limits, but names no count cap on
/// the lists themselves. Before the integrator's <c>WidenAuditEntryValues</c> migration
/// (2026-09-26), <c>audit_entry.before_value</c>/<c>after_value</c>'s old
/// <c>character varying(2000)</c> limit incidentally bounded how large a medical-profile update
/// could grow the owned graph -- now that those columns are unbounded <c>text</c>, that incidental
/// cap is gone (proven live: 3,000 allergies of 500 characters each, about 1.5 MB, succeeded in
/// about 1.7 seconds, and the whole graph is then snapshotted into the immutable audit table on
/// every subsequent write to the same cat). <see cref="MaxListItems"/> is a sensible cap chosen
/// here, not one HO-CAT-001 specifies -- recorded in HO-CAT-001 §1 Amended, 2026-09-26.
/// </summary>
public static class CatMedicalProfileRules
{
    public const int MaxListItems = 50;
}
