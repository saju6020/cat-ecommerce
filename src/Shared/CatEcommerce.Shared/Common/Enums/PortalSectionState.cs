namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-POR-001 §10.1. Not persisted; serialised by <see cref="Enumeration.DisplayName"/>. An
/// <c>Unavailable</c> section carries no data at all -- never an empty list or a zero (ADR-260).</summary>
public sealed class PortalSectionState(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly PortalSectionState Ready = new(1, nameof(Ready));
    public static readonly PortalSectionState Unavailable = new(2, nameof(Unavailable));
}
