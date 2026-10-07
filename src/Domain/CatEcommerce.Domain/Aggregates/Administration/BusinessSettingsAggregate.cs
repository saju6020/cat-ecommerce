using System.Globalization;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Administration;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Administration;

/// <summary>
/// HO-ADM-001 §8.7 / ADR-050. Id is <see cref="SettingScopes.GlobalId"/> or a branch id. Created
/// on first write. Change is all-or-nothing: every key must exist, parse and be within bounds, and
/// (for a branch scope) be branch-overridable, before any of them is applied.
/// </summary>
public sealed class BusinessSettingsAggregate : AggregateRoot
{
    private readonly List<SettingValue> _values = [];

    public bool IsGlobalScope => Id == SettingScopes.GlobalId;
    public IReadOnlyList<SettingValue> Values => _values;

    private BusinessSettingsAggregate() { }

    public BusinessSettingsAggregate(Guid id) : base(id) { }

    public void Open(Platform.Infrastructure.Common.Security.UserContext userContext) => SetDefaultValue(userContext);

    public void Change(BusinessSettingsChangeDto dto)
    {
        var validated = new List<(string Key, string Value)>();

        foreach (var change in dto.Changes)
        {
            var definition = SettingDefinitions.Find(change.Key)
                ?? throw new DomainRuleViolationException(BusinessViolationCodes.UnknownSetting, $"'{change.Key}' is not a known setting.", nameof(change.Key));

            if (!IsGlobalScope && !definition.BranchOverridable)
                throw new DomainRuleViolationException(BusinessViolationCodes.SettingNotBranchOverridable, $"'{change.Key}' cannot be overridden per branch.", nameof(change.Key));

            if (!TryValidateValue(definition, change.Value))
                throw new DomainRuleViolationException(BusinessViolationCodes.InvalidSettingValue, $"'{change.Value}' is not a valid value for '{change.Key}'.", nameof(change.Value));

            validated.Add((change.Key, change.Value));
        }

        var changedKeys = new List<string>();
        foreach (var (key, value) in validated)
        {
            var existing = _values.FirstOrDefault(v => v.Key == key);
            if (existing is null)
            {
                _values.Add(new SettingValue(key, value));
                changedKeys.Add(key);
            }
            else if (existing.Value != value)
            {
                existing.ChangeValue(value);
                changedKeys.Add(key);
            }
        }

        if (changedKeys.Count == 0) return; // no effective change -> no event (§8.7)

        AddDomainEvent(new BusinessSettingsChangedEvent { ScopeId = Id, Keys = changedKeys, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    public void ClearOverride(BusinessSettingsClearOverrideDto dto)
    {
        var existing = _values.FirstOrDefault(v => v.Key == dto.Key);
        if (existing is null) return;

        _values.Remove(existing);

        AddDomainEvent(new BusinessSettingsChangedEvent { ScopeId = Id, Keys = [dto.Key], CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    private static bool TryValidateValue(SettingDefinition definition, string value)
    {
        if (definition.Type == SettingType.Integer)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) return false;
            return i >= int.Parse(definition.Min!, CultureInfo.InvariantCulture) && i <= int.Parse(definition.Max!, CultureInfo.InvariantCulture);
        }

        if (definition.Type == SettingType.Decimal)
        {
            if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)) return false;
            return d >= decimal.Parse(definition.Min!, CultureInfo.InvariantCulture) && d <= decimal.Parse(definition.Max!, CultureInfo.InvariantCulture);
        }

        if (definition.Type == SettingType.Boolean)
            return bool.TryParse(value, out _);

        if (definition.Type == SettingType.TimeRange)
        {
            var parts = value.Split('-');
            return parts.Length == 2 && TimeOnly.TryParse(parts[0], CultureInfo.InvariantCulture, out _) && TimeOnly.TryParse(parts[1], CultureInfo.InvariantCulture, out _);
        }

        if (definition.Type == SettingType.CurrencyCode)
            return value.Length == 3 && value.All(char.IsAsciiLetterUpper);

        return false;
    }
}
