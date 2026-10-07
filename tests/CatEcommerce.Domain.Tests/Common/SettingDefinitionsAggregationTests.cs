using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.Common;

/// <summary>
/// HO-ADM-001 §10.2, ADR-050, ADR-341. Gate fix 2026-09-26: <c>SettingDefinitions.BuildRegistry</c>
/// (renamed from the untestable, hardcoded-type <c>LoadAll</c>) must fail loudly on a public field
/// or a property of the settings-contribution shape -- previously both were invisible to its
/// reflection and would have silently dropped every setting they defined, with no error, exactly
/// the failure mode <see cref="ActionRoles.BuildTable"/> already guards against for its own rows.
/// </summary>
public sealed class SettingDefinitionsAggregationTests
{
    private static SettingDefinition SampleDefinition(string key) =>
        new(key, "Test", SettingType.Integer, "0", "10", null, "5", false, key, key);

    private sealed class GoodFixture
    {
        private static readonly IReadOnlyList<SettingDefinition> FixtureEntries = [SampleDefinition("fixture.good")];
    }

    private sealed class DuplicateKeyFixture
    {
        private static readonly IReadOnlyList<SettingDefinition> EntriesA = [SampleDefinition("fixture.duplicate")];
        private static readonly IReadOnlyList<SettingDefinition> EntriesB = [SampleDefinition("fixture.duplicate")];
    }

    private sealed class PublicFieldFixture
    {
        public static readonly IReadOnlyList<SettingDefinition> BadEntries = [SampleDefinition("fixture.bad")];
    }

    private sealed class PropertyFixture
    {
        private static IReadOnlyList<SettingDefinition> BadEntries => [SampleDefinition("fixture.bad")];
    }

    [Fact]
    public void BuildRegistry_PrivateStaticReadonlyField_IsDiscovered()
    {
        var entries = SettingDefinitions.BuildRegistry(typeof(GoodFixture));

        Assert.Contains(entries, e => e.Key == "fixture.good");
    }

    [Fact]
    public void BuildRegistry_DuplicateKeyAcrossTwoFields_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => SettingDefinitions.BuildRegistry(typeof(DuplicateKeyFixture)));

        Assert.Contains("fixture.duplicate", exception.Message);
    }

    [Fact]
    public void BuildRegistry_PublicField_ThrowsNamingTheField()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => SettingDefinitions.BuildRegistry(typeof(PublicFieldFixture)));

        Assert.Contains(nameof(PublicFieldFixture), exception.Message);
        Assert.Contains("BadEntries", exception.Message);
    }

    [Fact]
    public void BuildRegistry_Property_ThrowsNamingTheProperty()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => SettingDefinitions.BuildRegistry(typeof(PropertyFixture)));

        Assert.Contains(nameof(PropertyFixture), exception.Message);
        Assert.Contains("BadEntries", exception.Message);
    }

    [Fact]
    public void All_RealAdministrationRegistry_HasNoDuplicateKeys()
    {
        // Exercising the real, production registry through the same mechanism (§20.6-style
        // "cannot drift" proof) -- would throw here if any two areas ever defined the same key.
        var keys = SettingDefinitions.All.Select(d => d.Key).ToList();

        Assert.Equal(keys.Distinct(StringComparer.Ordinal).Count(), keys.Count);
        Assert.Contains(keys, k => k == SettingKeys.CommerceCurrencyCode);
    }
}
