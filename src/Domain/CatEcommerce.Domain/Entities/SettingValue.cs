namespace CatEcommerce.Domain.Entities;

/// <summary>HO-ADM-001 §8.7. Owned by <c>BusinessSettingsAggregate</c>. The value is stored as an
/// invariant-culture string; typed parsing/bounds checking happens against
/// <c>SettingDefinitions</c> in the aggregate and in <c>IBusinessSettings</c>.</summary>
public sealed class SettingValue
{
    public string Key { get; private set; } = default!;
    public string Value { get; private set; } = default!;

    private SettingValue() { }

    public SettingValue(string key, string value)
    {
        Key = key;
        Value = value;
    }

    internal void ChangeValue(string value) => Value = value;
}
