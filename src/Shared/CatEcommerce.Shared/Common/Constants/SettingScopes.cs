namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-ADM-001 §10.3. The fixed id for the global scope of <c>BusinessSettingsAggregate</c>.
/// A branch scope uses the branch's own id.</summary>
public static class SettingScopes
{
    public static readonly Guid GlobalId = Guid.Parse("00000000-0000-0000-0000-0000000000ad");
}
