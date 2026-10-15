using System.Reflection;
using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Platform.Infrastructure.Core;
using Xunit;

namespace CatEcommerce.IntegrationTests.Unit;

/// <summary>HO-POR-001 §15 U-A4 (lives here because only this project references the Api assembly). Every action of the portal controller
/// requires a customer session; no route says "pay" (ADR-264); the controller injects the dispatcher and the user-context provider only.</summary>
public sealed class PortalControllerConventionTests
{
    private static readonly MethodInfo[] Actions = typeof(PortalController)
        .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .Where(m => !m.IsSpecialName)
        .ToArray();

    [Fact]
    public void U_A4_EveryPortalAction_CarriesRequireCustomerSession_AndNothingElseAsAccessMarker()
    {
        Assert.NotEmpty(Actions);

        foreach (var action in Actions)
        {
            Assert.NotEmpty(action.GetCustomAttributes<RequireCustomerSessionAttribute>());
            Assert.Empty(action.GetCustomAttributes<RequireActionAttribute>());
            Assert.Empty(action.GetCustomAttributes<PublicEndpointAttribute>());
        }
    }

    [Fact]
    public void U_A4_NoPortalRouteContainsPay()
    {
        var templates = new[] { typeof(PortalController).GetCustomAttribute<RouteAttribute>()!.Template }
            .Concat(Actions.SelectMany(a => a.GetCustomAttributes<HttpMethodAttribute>()).Select(h => h.Template ?? string.Empty))
            .Concat(Actions.Select(a => a.Name))
            .ToList();

        Assert.All(templates, t => Assert.DoesNotContain("pay", t, StringComparison.OrdinalIgnoreCase));
        Assert.Equal("api/portal", templates[0]);
    }

    [Fact]
    public void ThePortalController_InjectsTheDispatcherAndTheUserContextProviderOnly()
    {
        var parameters = Assert.Single(typeof(PortalController).GetConstructors()).GetParameters().Select(p => p.ParameterType.Name).Order().ToArray();

        Assert.Equal(["IInMemoryDispatcher", "IUserContextProvider"], parameters);
    }
}
