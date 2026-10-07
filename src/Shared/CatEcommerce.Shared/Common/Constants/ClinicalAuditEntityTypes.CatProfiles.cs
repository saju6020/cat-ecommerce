namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// Cat Profiles' (HO-CAT-001) audit <c>EntityType</c> strings. Written on that area's behalf
/// (gate fix 2026-09-26, the same "ahead of that area's own change" precedent as
/// <c>AdministrationPersonalDataEraser</c> being built ahead of HO-XCT-001): these three strings
/// are not new -- they are exactly what <c>AuditChangeCaptureInterceptor</c> and HO-CAT-001's own
/// <c>BusinessEvent</c> handlers already write, transcribed here so the redaction registry has
/// somewhere to find them.
///
/// <c>CatAggregate</c>, <c>CatWeightRecordAggregate</c> -- the interceptor's own <c>DataChange</c>
/// rows, named after the CLR aggregate root type (`clinic.cat`, `clinic.cat_weight_record`, both
/// `RecordClass.Clinical` in `RecordClassification.CatProfiles`). Vaccinations and parasite
/// treatments are owned (`OwnsMany`) by `CatAggregate`, so they audit under that same root type and
/// need no separate entry.
///
/// <c>Cat</c> -- the label four <c>BusinessEvent</c> handlers use for a second audit row a plain
/// `DataChange` snapshot cannot carry: `AuditCatHighRiskFlaggedHandler`/
/// `AuditCatHighRiskFlagClearedHandler` (`HandlingNote`, the flag/clear `Reason` -- squarely
/// clinical, a vet's judgement about the cat's condition or behaviour) and
/// `AuditCatOwnershipTransferredHandler`/`AuditCatOwnerReassignedOnMergeHandler` (an owner id
/// change and an administrative `Reason` -- not clinical on its face). <strong>Decision, recorded
/// here per the gate's own request:</strong> all four are treated as clinical and redacted
/// uniformly, rather than splitting `Cat` by `Action`. The simpler, more cautious rule was chosen
/// over exact precision: an Accountant has no legitimate business need to read any per-cat
/// operational reason text, clinical or not, and a transfer's free-text reason can incidentally
/// contain clinical detail (e.g. "owner can no longer care for a chronically ill cat") that a
/// blanket redaction catches without needing to know that in advance.
/// </summary>
public static partial class ClinicalAuditEntityTypes
{
    private static readonly IReadOnlyList<string> CatProfilesEntries = ["CatAggregate", "CatWeightRecordAggregate", "Cat"];
}
