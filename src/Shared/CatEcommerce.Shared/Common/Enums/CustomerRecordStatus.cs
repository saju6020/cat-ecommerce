namespace CatEcommerce.Shared.Common.Enums;

/// <summary>
/// HO-ACC-001 §10.1. Computed only (ADR-038, <c>CustomerRecordStatusRules.Derive</c>) -- never
/// persisted on <c>CustomerAccountAggregate</c>.
/// </summary>
public sealed class CustomerRecordStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly CustomerRecordStatus Complete = new(1, nameof(Complete));
    public static readonly CustomerRecordStatus Incomplete = new(2, nameof(Incomplete));
}
