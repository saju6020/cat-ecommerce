using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// Cat Profiles (HO-CAT-001) table classifications. Every entry and its <c>Basis</c> are
/// transcribed verbatim from HO-XCT-001 §10.3's own table, not guessed -- §10.3 already
/// classifies every table Cat Profiles owns, so there is no gap here. Table names transcribed
/// from the merged EF configurations (<c>CatConfiguration</c>, <c>CatWeightRecordConfiguration</c>,
/// <c>VaccineConfiguration</c>).
///
/// <c>clinic.appointment</c> and <c>clinic.waitlist_entry</c> are also listed in §10.3 but are
/// Appointments' (HO-APT-001) tables, not Cat Profiles', so they are left for that area's own
/// partial file. <c>audit.audit_entry</c> is mapped in the current EF model too (Cat Profiles
/// writes <c>AuditEntryAggregate</c> rows directly), but it is an Accounts-context table (§10.3:
/// "Audit | Forbidden (HO-ACC-001's triggers, F33) | D-5") -- it is deliberately **not**
/// classified here, so that HO-ACC-001's own <c>RecordClassification.Accounts.cs</c> does not
/// collide with this file (ADR-341: only the owning area classifies a table). ARCHITECTURE gate,
/// LOW (2026-09-26): Accounts has since landed and classifies it at
/// <c>RecordClassification.Accounts.cs:42</c>, so there is no longer a gap in
/// <see cref="RecordClassification.All"/> -- this comment previously said otherwise.
/// </summary>
public static partial class RecordClassification
{
    private static readonly IReadOnlyList<TableClassification> CatProfilesEntries =
    [
        new TableClassification("clinic", "cat", RecordClass.Clinical, HardDeletePolicy.Forbidden, "D-8"),
        new TableClassification("clinic", "cat_vaccination", RecordClass.Clinical, HardDeletePolicy.Forbidden, "D-8"),
        new TableClassification("clinic", "cat_parasite_treatment", RecordClass.Clinical, HardDeletePolicy.Forbidden, "D-8"),
        new TableClassification("clinic", "cat_weight_record", RecordClass.Clinical, HardDeletePolicy.Forbidden, "D-8"),

        new TableClassification("clinic", "cat_allergy", RecordClass.CurrentState, HardDeletePolicy.AggregateOnly, "F32; history in audit"),
        new TableClassification("clinic", "cat_chronic_condition", RecordClass.CurrentState, HardDeletePolicy.AggregateOnly, "F32; history in audit"),
        new TableClassification("clinic", "cat_medication", RecordClass.CurrentState, HardDeletePolicy.AggregateOnly, "F32; history in audit"),

        new TableClassification("clinic", "cat_photo", RecordClass.CustomerPersonal, HardDeletePolicy.AggregateOnly, "No removal method today"),

        new TableClassification("clinic", "vaccine", RecordClass.Operational, HardDeletePolicy.Forbidden, "Referenced by clinical records"),
    ];
}
