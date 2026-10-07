using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Domain.Tests.Common;

/// <summary>
/// HO-ACC-001 §12.8, ADR-036, ADR-341. ARCHITECTURE gate HIGH finding: the action-to-role table
/// must be assembled from every area's own <c>ActionRoles.[Area].cs</c> partial by reflection, the
/// way <c>RecordClassification.cs</c> does it for tables -- no later area may need to edit an
/// Accounts file. These tests exercise the reflection merge itself (against throwaway fixture
/// types, so a real second area is not required to prove the mechanism), and the last two exercise
/// the real, production <see cref="ActionRoles"/> table built from Accounts' own rows.
/// </summary>
public sealed class ActionRolesAggregationTests
{
    [Fact]
    public void BuildTable_MergesRowsFromEveryPartialFile_WithNoReferenceBetweenThem()
    {
        var table = ActionRoles.BuildTable(typeof(ActionRolesAggregationFixture));

        Assert.True(table.TryGetValue("FixtureFirstArea.View", out var firstRoles));
        Assert.Equal(["RoleA"], firstRoles);

        Assert.True(table.TryGetValue("FixtureSecondArea.Create", out var secondRoles));
        Assert.Equal(["RoleB", "RoleC"], secondRoles);
    }

    [Fact]
    public void BuildTable_TwoPartialsMapTheSameAction_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ActionRoles.BuildTable(typeof(ActionRolesDuplicateAggregationFixture)));

        Assert.Contains("FixtureDuplicate.View", exception.Message);
    }

    [Fact]
    public void TryGetRoles_RealAccountsAction_IsFoundThroughTheSameMechanism()
    {
        var found = ActionRoles.TryGetRoles(Actions.Customers.Merge, out var roles);

        Assert.True(found);
        Assert.Contains(StaffRoles.Administrator, roles);
    }

    [Fact]
    public void TryGetRoles_UnmappedAction_FailsClosed()
    {
        var found = ActionRoles.TryGetRoles("NoSuchAction.AnywhereInTheSolution", out var roles);

        Assert.False(found);
        Assert.Empty(roles);
    }

    // ARCHITECTURE gate, second pass, LOW: a "*Rows"-named member of the wrong shape must fail
    // loudly, not be silently skipped (which would leave that area's [RequireAction] endpoints
    // failing closed with a 403 nobody could explain).
    [Fact]
    public void BuildTable_RowsFieldOfTheWrongType_ThrowsNamingTheField()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ActionRoles.BuildTable(typeof(ActionRolesWrongShapeFixture)));

        Assert.Contains(nameof(ActionRolesWrongShapeFixture), exception.Message);
        Assert.Contains("BadRows", exception.Message);
    }

    [Fact]
    public void BuildTable_PublicRowsField_ThrowsNamingTheField()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ActionRoles.BuildTable(typeof(ActionRolesPublicFieldFixture)));

        Assert.Contains(nameof(ActionRolesPublicFieldFixture), exception.Message);
        Assert.Contains("BadRows", exception.Message);
    }

    [Fact]
    public void BuildTable_RowsProperty_ThrowsNamingTheProperty()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ActionRoles.BuildTable(typeof(ActionRolesPropertyFixture)));

        Assert.Contains(nameof(ActionRolesPropertyFixture), exception.Message);
        Assert.Contains("BadRows", exception.Message);
    }

    // ARCHITECTURE gate, third pass, LOW: a correctly-typed field whose name does not end with
    // "Rows" was invisible to every one of the checks above (all name-first), so it was silently
    // dropped instead of throwing -- the exact failure mode this whole method exists to prevent.
    [Fact]
    public void BuildTable_CorrectlyTypedFieldMissingTheRowsSuffix_ThrowsNamingTheField()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ActionRoles.BuildTable(typeof(ActionRolesMisnamedFieldFixture)));

        Assert.Contains(nameof(ActionRolesMisnamedFieldFixture), exception.Message);
        Assert.Contains("BillingTable", exception.Message);
    }
}
