using System.Reflection;
using CatEcommerce.Api.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Xunit;

namespace CatEcommerce.IntegrationTests.Unit;

/// <summary>
/// HO-ADM-001 §12.3 / ADR-043, U-A1. Reflects over every controller action in the Api assembly and
/// fails the build if it carries anything other than exactly one of
/// <see cref="RequireActionAttribute"/>, <c>RequireCustomerSessionAttribute</c> (method or class
/// level) or <see cref="PublicEndpointAttribute"/>. US-ADM-02 AC1: "every endpoint's access
/// matches Appendix D" -- a convention test catches an endpoint someone simply forgot to
/// attribute, which fail-closed-by-default action lookups alone do not.
///
/// Discovery uses ASP.NET Core's own <see cref="ControllerFeatureProvider"/> (gate fix
/// 2026-09-26), not a hand-rolled "subclasses <see cref="ControllerBase"/>" filter -- MVC also
/// treats a plain class named <c>*Controller</c> (or annotated <c>[Controller]</c>), with no
/// common base type, as a controller. The previous filter would have silently skipped such a class
/// and left every one of its actions unguarded and undetected.
///
/// Gate fix 2026-09-26 (second pass, SEC LOW): <c>BindingFlags.DeclaredOnly</c> is dropped --
/// planted proof was an abstract base controller with an unmarked action and an empty derived
/// controller; <c>DeclaredOnly</c> made the inherited action invisible to this test (it is declared
/// on the base type, not the derived one MVC actually routes), so it passed while the action itself
/// answered anonymously. Every method still declared on <c>object</c>, <c>ControllerBase</c> or
/// <c>Controller</c> is excluded by <see cref="IsAction"/>, exactly as before.
///
/// Gate fix 2026-09-26 (third pass): the scanning logic is extracted into
/// <see cref="FindAccessViolations"/>, taking the controller type set as a parameter instead of
/// hardcoding the Api assembly, specifically so a test-only fixture (an abstract base controller
/// with an unmarked action, defined in this assembly, never shipped in the Api project) can prove
/// the scanner actually catches an inherited unmarked action -- the exact shape the SECURITY gate's
/// own probe used, without planting anything in the real Api project to do it.
/// </summary>
public sealed class EndpointAccessConventionTests
{
    [Fact]
    public void EveryControllerAction_HasExactlyOneAccessMarker()
    {
        var controllerAssembly = typeof(RequireActionAttribute).Assembly;

        var partManager = new ApplicationPartManager();
        partManager.ApplicationParts.Add(new AssemblyPart(controllerAssembly));
        partManager.FeatureProviders.Add(new ControllerFeatureProvider());
        var feature = new ControllerFeature();
        partManager.PopulateFeature(feature);

        var controllerTypes = feature.Controllers.Select(ti => ti.AsType()).ToList();
        Assert.NotEmpty(controllerTypes);

        var (undeclared, overDeclared) = FindAccessViolations(controllerTypes);

        Assert.True(undeclared.Count == 0, "Undeclared access on: " + string.Join(", ", undeclared));
        Assert.True(overDeclared.Count == 0, "More than one access marker on: " + string.Join(", ", overDeclared));
    }

    /// <summary>Test-only fixture proving the scanner catches an action inherited from an abstract
    /// base controller -- declared on the base type, not the concrete one MVC actually routes,
    /// exactly the shape <c>BindingFlags.DeclaredOnly</c> used to make invisible (see the class doc
    /// comment's second-pass note). <see cref="ProbeDerivedController"/> is never registered with
    /// MVC and never added to the real Api project; <see cref="FindAccessViolations"/> is called
    /// directly against it, bypassing <see cref="ControllerFeatureProvider"/> entirely, so this
    /// proves the scanning logic itself, not routing.</summary>
    [Fact]
    public void FindAccessViolations_InheritedUnmarkedAction_IsReportedAsUndeclared()
    {
        var (undeclared, overDeclared) = FindAccessViolations([typeof(ProbeDerivedController)]);

        Assert.Contains(undeclared, label => label == $"{nameof(ProbeBaseController)}.{nameof(ProbeBaseController.Leak)}");
        Assert.Empty(overDeclared);
    }

    /// <summary>A correctly-marked inherited action is not reported -- the fixture test above would
    /// be meaningless if every inherited action were flagged regardless of its markers.</summary>
    [Fact]
    public void FindAccessViolations_InheritedMarkedAction_IsNotReported()
    {
        var (undeclared, overDeclared) = FindAccessViolations([typeof(ProbeDerivedController)]);

        Assert.DoesNotContain(undeclared, label => label.Contains(nameof(ProbeBaseController.Marked)));
        Assert.Empty(overDeclared);
    }

    internal static (List<string> Undeclared, List<string> OverDeclared) FindAccessViolations(IEnumerable<Type> controllerTypes)
    {
        var actions = controllerTypes
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Where(IsAction)
            .ToList();

        var undeclared = new List<string>();
        var overDeclared = new List<string>();

        foreach (var action in actions)
        {
            var markerCount =
                CountMarker<RequireActionAttribute>(action) +
                CountMarker<RequireCustomerSessionAttribute>(action) +
                CountMarker<PublicEndpointAttribute>(action);

            var label = $"{action.DeclaringType!.Name}.{action.Name}";
            if (markerCount == 0) undeclared.Add(label);
            else if (markerCount > 1) overDeclared.Add(label);
        }

        return (undeclared, overDeclared);
    }

    private static bool IsAction(MethodInfo method)
    {
        if (method.IsSpecialName) return false;
        if (method.GetCustomAttribute<NonActionAttribute>() is not null) return false;
        if (method.DeclaringType == typeof(object) || method.DeclaringType == typeof(ControllerBase) || method.DeclaringType == typeof(Controller)) return false;
        // Internal helper methods dispatched to from another action (e.g. AuthController's shared
        // password-reset-request body) are not routable MVC actions.
        return method.IsPublic;
    }

    private static int CountMarker<TAttribute>(MethodInfo method) where TAttribute : Attribute =>
        method.GetCustomAttributes<TAttribute>().Any() || method.DeclaringType!.GetCustomAttributes<TAttribute>().Any() ? 1 : 0;

    /// <summary>Test-only: never registered with MVC, never shipped in the Api project.</summary>
    public abstract class ProbeBaseController : ControllerBase
    {
        [HttpGet("api/probe/inherited-unmarked")]
        public IActionResult Leak() => Ok();

        [HttpGet("api/probe/inherited-marked")]
        [PublicEndpoint]
        public IActionResult Marked() => Ok();
    }

    /// <summary>Test-only: an empty subclass, so both actions above are inherited, not declared,
    /// on this type -- the shape <c>BindingFlags.DeclaredOnly</c> used to miss.</summary>
    public sealed class ProbeDerivedController : ProbeBaseController;
}
