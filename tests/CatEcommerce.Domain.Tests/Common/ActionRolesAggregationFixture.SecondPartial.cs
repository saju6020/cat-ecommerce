namespace CatEcommerce.Domain.Tests.Common;

/// <summary>
/// The "second area's own file" half of <see cref="ActionRolesAggregationFixture"/> -- contributes
/// its own rows field with no reference to <c>ActionRolesAggregationFixtures.cs</c>'s field,
/// exactly as a later area's real <c>ActionRoles.[Area].cs</c> would.
/// </summary>
internal static partial class ActionRolesAggregationFixture
{
    private static readonly (string Action, string[] Roles)[] SecondAreaRows =
    [
        ("FixtureSecondArea.Create", ["RoleB", "RoleC"]),
    ];
}

/// <summary>The duplicate-inducing second file for <see cref="ActionRolesDuplicateAggregationFixture"/>
/// -- maps the SAME action key ("FixtureDuplicate.View") as the first file.</summary>
internal static partial class ActionRolesDuplicateAggregationFixture
{
    private static readonly (string Action, string[] Roles)[] SecondAreaRows =
    [
        ("FixtureDuplicate.View", ["RoleZ"]),
    ];
}
