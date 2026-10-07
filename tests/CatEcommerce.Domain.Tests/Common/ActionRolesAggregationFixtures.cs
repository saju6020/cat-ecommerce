namespace CatEcommerce.Domain.Tests.Common;

/// <summary>
/// A throwaway type shaped exactly like the real <c>ActionRoles</c> -- one static partial class,
/// one rows field per "area", split across two files -- used only to prove
/// <see cref="CatEcommerce.Shared.Common.Constants.ActionRoles.BuildTable"/>'s reflection-based
/// merge picks up every partial's rows field with no reference between the two files, and that it
/// throws when two areas map the same action (ADR-341). See
/// <c>ActionRolesAggregationFixture.SecondPartial.cs</c> for the second file.
/// </summary>
internal static partial class ActionRolesAggregationFixture
{
    private static readonly (string Action, string[] Roles)[] FirstAreaRows =
    [
        ("FixtureFirstArea.View", ["RoleA"]),
    ];
}

/// <summary>
/// A second throwaway type, structurally identical to <see cref="ActionRolesAggregationFixture"/>,
/// but with the SAME action key contributed by two different partial files -- proves
/// <c>ActionRoles.BuildTable</c> throws rather than silently letting the second one win.
/// </summary>
internal static partial class ActionRolesDuplicateAggregationFixture
{
    private static readonly (string Action, string[] Roles)[] FirstAreaRows =
    [
        ("FixtureDuplicate.View", ["RoleA"]),
    ];
}

/// <summary>
/// A "*Rows"-named field of the WRONG shape -- proves <c>ActionRoles.BuildTable</c> throws instead
/// of silently filtering it out the way a pure type check would.
/// </summary>
internal static class ActionRolesWrongShapeFixture
{
    private static readonly Dictionary<string, string[]> BadRows = new()
    {
        ["FixtureWrongShape.View"] = ["RoleA"],
    };
}

/// <summary>A correctly-shaped "*Rows"-named field that is <c>public</c> -- proves
/// <c>ActionRoles.BuildTable</c> throws rather than silently accepting or silently skipping it.</summary>
internal static class ActionRolesPublicFieldFixture
{
    public static readonly (string Action, string[] Roles)[] BadRows =
    [
        ("FixturePublicField.View", ["RoleA"]),
    ];
}

/// <summary>A "*Rows"-named PROPERTY instead of a field -- proves <c>ActionRoles.BuildTable</c>
/// throws rather than silently never discovering it (properties are never returned by
/// <c>Type.GetFields</c>, so a pure field-type check would just never see this at all).</summary>
internal static class ActionRolesPropertyFixture
{
    private static (string Action, string[] Roles)[] BadRows =>
    [
        ("FixtureProperty.View", ["RoleA"]),
    ];
}

/// <summary>
/// A correctly-typed, correctly-private field whose NAME does not end with "Rows" (e.g. a future
/// area naming its field <c>BillingTable</c>) -- ARCHITECTURE gate, third pass, LOW: proves
/// <c>ActionRoles.BuildTable</c> throws instead of silently never discovering it, which is exactly
/// what happened for every check that only looks for "*Rows"-named candidates.
/// </summary>
internal static class ActionRolesMisnamedFieldFixture
{
    private static readonly (string Action, string[] Roles)[] BillingTable =
    [
        ("FixtureMisnamed.View", ["RoleA"]),
    ];
}
