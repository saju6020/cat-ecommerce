using System.Reflection;
using CatEcommerce.Domain.Events.Operations;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Tests.Operations;

/// <summary>HO-XCT-001 U-X1 (events carry no personal data) and U-X2 (the nine settings).</summary>
public sealed class OperationsConventionTests
{
    /// <summary>The only string properties an Operations event may have: codes and context keys, never free text (section 11.5).</summary>
    private static readonly HashSet<string> AllowedStringProperties = ["ContextKey", "ErrorCode", "Outcome"];

    // U-X1
    [Fact]
    public void U_X1_EveryOperationsEvent_HasNoStringPropertyOtherThanACodeOrAContextKey()
    {
        var eventTypes = typeof(ErasureRequestOpenedEvent).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(ErasureRequestOpenedEvent).Namespace && typeof(Event).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();
        Assert.NotEmpty(eventTypes);

        var violations = eventTypes
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(p => (Type: t, Property: p)))
            .Where(x => x.Property.PropertyType == typeof(string) && !AllowedStringProperties.Contains(x.Property.Name))
            .Select(x => $"{x.Type.Name}.{x.Property.Name}")
            .ToList();

        Assert.True(violations.Count == 0, "Free-text string properties on Operations events: " + string.Join(", ", violations));
    }

    // U-X2
    [Fact]
    public void U_X2_TheNineOperationsSettings_AreRegistered_WithDefaultsInsideTheirBounds_AndUniqueKeys()
    {
        string[] keys =
        [
            SettingKeys.RetentionClinicalYears, SettingKeys.RetentionFinancialYears, SettingKeys.RetentionBoardingMediaMonths,
            SettingKeys.RetentionNotificationContentMonths, SettingKeys.RetentionSecurityArtefactDays, SettingKeys.RetentionWebVitalsDays,
            SettingKeys.RetentionSweepHour, SettingKeys.OperationsRestoreTestIntervalDays, SettingKeys.OfflineSnapshotRefreshSeconds,
        ];

        Assert.Equal(9, keys.Distinct().Count());
        Assert.Equal(SettingDefinitions.All.Count, SettingDefinitions.All.Select(d => d.Key).Distinct(StringComparer.Ordinal).Count());

        foreach (var key in keys)
        {
            var definition = SettingDefinitions.Find(key);
            Assert.NotNull(definition);
            Assert.Equal(SettingType.Integer, definition!.Type);

            var value = int.Parse(definition.Default);
            Assert.InRange(value, int.Parse(definition.Min!), int.Parse(definition.Max!));
        }

        // Section 10.2: only the snapshot refresh may be overridden per branch.
        Assert.True(SettingDefinitions.Find(SettingKeys.OfflineSnapshotRefreshSeconds)!.BranchOverridable);
        Assert.All(keys.Where(k => k != SettingKeys.OfflineSnapshotRefreshSeconds), k => Assert.False(SettingDefinitions.Find(k)!.BranchOverridable));
    }

    [Fact]
    public void TheSettingDefaults_AreTheSrsAssumedPeriods()
    {
        Assert.Equal("7", SettingDefinitions.Find(SettingKeys.RetentionClinicalYears)!.Default);
        Assert.Equal("6", SettingDefinitions.Find(SettingKeys.RetentionFinancialYears)!.Default);
        Assert.Equal("12", SettingDefinitions.Find(SettingKeys.RetentionBoardingMediaMonths)!.Default);
        Assert.Equal("24", SettingDefinitions.Find(SettingKeys.RetentionNotificationContentMonths)!.Default);
        Assert.Equal("92", SettingDefinitions.Find(SettingKeys.OperationsRestoreTestIntervalDays)!.Default);
    }

    [Fact]
    public void TheErasureBackoff_IsFifteenSixtyTwoFortyThenADay()
    {
        Assert.Equal(15, RetentionRules.BackoffAfter(1));
        Assert.Equal(60, RetentionRules.BackoffAfter(2));
        Assert.Equal(240, RetentionRules.BackoffAfter(3));
        Assert.Equal(1440, RetentionRules.BackoffAfter(4));
        Assert.Equal(1440, RetentionRules.BackoffAfter(40));
    }
}
