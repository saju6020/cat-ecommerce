namespace CatEcommerce.Shared.Common.Enums;

/// <summary>
/// The classification every table in the EF model carries in <c>RecordClassification</c>
/// (HO-XCT-001 §8.2/§10.1, ADR-298). Determines the table's <see cref="HardDeletePolicy"/> and
/// retention rule. See <c>RecordClassification</c> for the ordered classification rules a future
/// table follows when it is not one of the tables already listed.
/// </summary>
public sealed class RecordClass(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly RecordClass Clinical = new(1, nameof(Clinical));
    public static readonly RecordClass Financial = new(2, nameof(Financial));
    public static readonly RecordClass Regulatory = new(3, nameof(Regulatory));
    public static readonly RecordClass Audit = new(4, nameof(Audit));
    public static readonly RecordClass CustomerPersonal = new(5, nameof(CustomerPersonal));
    public static readonly RecordClass CurrentState = new(6, nameof(CurrentState));
    public static readonly RecordClass Operational = new(7, nameof(Operational));
    public static readonly RecordClass Notification = new(8, nameof(Notification));
    public static readonly RecordClass Security = new(9, nameof(Security));
    public static readonly RecordClass Media = new(10, nameof(Media));
    public static readonly RecordClass Telemetry = new(11, nameof(Telemetry));
}
