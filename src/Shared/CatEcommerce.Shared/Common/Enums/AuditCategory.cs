namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ACC-001 §10.1 / §8.11.</summary>
public sealed class AuditCategory(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly AuditCategory DataChange = new(1, nameof(DataChange));
    public static readonly AuditCategory AuthenticationEvent = new(2, nameof(AuthenticationEvent));
    public static readonly AuditCategory BusinessEvent = new(3, nameof(BusinessEvent));

    /// <summary>HO-EMR-001 §8.11 / ADR-129 -- one row per read of a clinical record (NFR-SEC-8).</summary>
    public static readonly AuditCategory ClinicalRecordAccess = new(4, nameof(ClinicalRecordAccess));
}
