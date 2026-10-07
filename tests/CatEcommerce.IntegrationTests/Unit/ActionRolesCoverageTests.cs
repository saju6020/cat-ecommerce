using System.Reflection;
using CatEcommerce.Api.Authorization;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Xunit;

namespace CatEcommerce.IntegrationTests.Unit;

/// <summary>
/// TEST gate seventh pass: U-A2/U-A3, previously BLOCKED.
///
/// U-A2: every <see cref="RequireActionAttribute"/> action string actually used across the Api
/// assembly's controllers must have a row in <see cref="ActionRoles"/> -- an action present on a
/// controller but absent from the table fails closed (denies every role) rather than failing to
/// compile, so a typo'd action string was previously caught by no automated test.
///
/// U-A3: every role named in any <see cref="ActionRoles"/> row must be a real
/// <see cref="StaffRoles"/>.All member, and <c>"Customer"</c> (not a staff role -- see
/// <c>StaffRoles.cs</c>'s own doc comment) must never appear in one.
///
/// Discovery reuses <c>EndpointAccessConventionTests</c>'s own <see cref="ControllerFeatureProvider"/>
/// approach (not a hand-rolled "subclasses ControllerBase" filter), so a plain <c>*Controller</c>-named
/// class with no common base type is still found.
/// </summary>
public sealed class ActionRolesCoverageTests
{
    [Fact]
    public void EveryRequireActionAttribute_NamesAnActionPresentInActionRoles()
    {
        var actionStrings = DiscoverRequireActionStrings();
        Assert.NotEmpty(actionStrings);

        var missing = actionStrings.Where(a => !ActionRoles.TryGetRoles(a, out _)).ToList();

        Assert.True(missing.Count == 0,
            "The following [RequireAction] action strings have no row in ActionRoles and would fail closed for every role: " +
            string.Join(", ", missing));
    }

    [Fact]
    public void EveryActionRolesRow_NamesOnlyRealStaffRoles_AndNeverCustomer()
    {
        var table = ActionRoles.BuildTable(typeof(ActionRoles));
        Assert.NotEmpty(table);

        var unknownRoles = table
            .SelectMany(row => row.Value.Select(role => (Action: row.Key, Role: role)))
            .Where(entry => !StaffRoles.All.Contains(entry.Role, StringComparer.Ordinal))
            .ToList();

        Assert.True(unknownRoles.Count == 0,
            "The following ActionRoles rows name a role that is not in StaffRoles.All: " +
            string.Join(", ", unknownRoles.Select(e => $"{e.Action}->{e.Role}")));

        var customerRows = table.Where(row => row.Value.Contains("Customer", StringComparer.Ordinal)).Select(row => row.Key).ToList();
        Assert.True(customerRows.Count == 0, "The following ActionRoles rows wrongly list \"Customer\" as a staff role: " + string.Join(", ", customerRows));
    }

    private static List<string> DiscoverRequireActionStrings()
    {
        var controllerAssembly = typeof(RequireActionAttribute).Assembly;

        var partManager = new ApplicationPartManager();
        partManager.ApplicationParts.Add(new AssemblyPart(controllerAssembly));
        partManager.FeatureProviders.Add(new ControllerFeatureProvider());
        var feature = new ControllerFeature();
        partManager.PopulateFeature(feature);

        var controllerTypes = feature.Controllers.Select(ti => ti.AsType()).ToList();
        Assert.NotEmpty(controllerTypes);

        var actionStrings = new List<string>();
        foreach (var controllerType in controllerTypes)
        {
            foreach (var method in controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (method.IsSpecialName) continue;
                if (method.DeclaringType == typeof(object)) continue;

                foreach (var attribute in method.GetCustomAttributes<RequireActionAttribute>())
                    actionStrings.Add((string)attribute.Arguments![0]!);

                foreach (var attribute in method.DeclaringType!.GetCustomAttributes<RequireActionAttribute>())
                    actionStrings.Add((string)attribute.Arguments![0]!);
            }
        }

        return actionStrings.Distinct(StringComparer.Ordinal).ToList();
    }
}
