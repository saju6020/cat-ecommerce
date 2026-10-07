using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Domain.Tests.Common;

/// <summary>
/// HO-ADM-001, gate fix 2026-09-26 (SEC MEDIUM #3 / ARCH MEDIUM #3). Mirrors
/// <c>SettingDefinitionsAggregationTests</c>: <c>ClinicalAuditEntityTypes.BuildRegistry</c> must
/// fail loudly on a public field or a property, exactly like
/// <see cref="ActionRoles.BuildTable"/>/<see cref="SettingDefinitions.BuildRegistry"/>, and the
/// real, production registry must actually contain Cat Profiles' own entity types.
/// </summary>
public sealed class ClinicalAuditEntityTypesAggregationTests
{
    private sealed class GoodFixture
    {
        private static readonly IReadOnlyList<string> FixtureEntries = ["FixtureClinicalEntity"];
    }

    private sealed class PublicFieldFixture
    {
        public static readonly IReadOnlyList<string> BadEntries = ["Bad"];
    }

    private sealed class PropertyFixture
    {
        private static IReadOnlyList<string> BadEntries => ["Bad"];
    }

    [Fact]
    public void BuildRegistry_PrivateStaticReadonlyField_IsDiscovered()
    {
        var entries = ClinicalAuditEntityTypes.BuildRegistry(typeof(GoodFixture));

        Assert.Contains("FixtureClinicalEntity", entries);
    }

    [Fact]
    public void BuildRegistry_PublicField_ThrowsNamingTheField()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ClinicalAuditEntityTypes.BuildRegistry(typeof(PublicFieldFixture)));

        Assert.Contains(nameof(PublicFieldFixture), exception.Message);
        Assert.Contains("BadEntries", exception.Message);
    }

    [Fact]
    public void BuildRegistry_Property_ThrowsNamingTheProperty()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ClinicalAuditEntityTypes.BuildRegistry(typeof(PropertyFixture)));

        Assert.Contains(nameof(PropertyFixture), exception.Message);
        Assert.Contains("BadEntries", exception.Message);
    }

    [Fact]
    public void All_RealRegistry_ContainsCatProfilesEntityTypes()
    {
        Assert.Contains("CatAggregate", ClinicalAuditEntityTypes.All);
        Assert.Contains("CatWeightRecordAggregate", ClinicalAuditEntityTypes.All);
        Assert.Contains("Cat", ClinicalAuditEntityTypes.All);
    }

    [Fact]
    public void Contains_UnknownEntityType_IsFalse()
    {
        Assert.False(ClinicalAuditEntityTypes.Contains("SomethingNotClinical"));
    }
}
