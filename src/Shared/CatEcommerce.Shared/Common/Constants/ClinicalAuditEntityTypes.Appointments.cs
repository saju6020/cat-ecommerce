namespace CatEcommerce.Shared.Common.Constants;

/// <summary>Clinic Appointments' audit <c>EntityType</c> strings (HO-XCT-001 §5.2 C-ALL-5). The
/// interceptor's own <c>DataChange</c> rows are named after the CLR aggregate root type
/// (<c>AppointmentAggregate</c>, <c>clinic.appointment</c>, <c>RecordClass.Clinical</c> in
/// <c>RecordClassification.Appointments</c>). <c>WaitlistEntryAggregate</c> is not listed here -- it
/// is <c>RecordClass.Operational</c>, not clinical.</summary>
public static partial class ClinicalAuditEntityTypes
{
    private static readonly IReadOnlyList<string> AppointmentsEntries = ["AppointmentAggregate"];
}
