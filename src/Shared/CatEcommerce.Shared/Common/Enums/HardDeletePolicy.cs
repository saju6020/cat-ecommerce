namespace CatEcommerce.Shared.Common.Enums;

/// <summary>
/// How a table classified under a given <see cref="RecordClass"/> may lose a row (HO-XCT-001
/// §8.2, ADR-298/ADR-299). Enforcement (the database trigger, <c>HardDeleteGuardInterceptor</c>
/// and the source scan) is built by HO-XCT-001 itself; this type is the classification only.
/// </summary>
public sealed class HardDeletePolicy(int value, string displayName) : Enumeration(value, displayName)
{
    /// <summary>No delete by any path. The default for Clinical, Financial, Regulatory, Audit,
    /// Notification and most Operational tables.</summary>
    public static readonly HardDeletePolicy Forbidden = new(1, nameof(Forbidden));

    /// <summary>A row may disappear only as an owned entity removed through its owning
    /// aggregate, in a save where the owner is itself tracked and not deleted.</summary>
    public static readonly HardDeletePolicy AggregateOnly = new(2, nameof(AggregateOnly));

    /// <summary>Deleted only by the retention worker's purger or a registered
    /// <c>IRetentionPurgeSource</c>, under the retention schedule.</summary>
    public static readonly HardDeletePolicy Purgeable = new(3, nameof(Purgeable));
}
