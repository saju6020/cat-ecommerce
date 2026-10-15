using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using CatEcommerce.Api.Authorization;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-BIL-001 section 16 I-J3 -- <b>every</b> Billing staff endpoint against <b>every</b> staff role: the roles ActionRoles maps to the endpoint action
/// are let through to the handler (anything but 401/403), every other role is refused with 403, and nobody signed in gets 401. The endpoint
/// list is read from the running host (its routing table), so a new Billing endpoint cannot be forgotten by the matrix (section 12.3).</summary>
public sealed partial class BillingAccessMatrixTests(BillingApiFactory factory) : BillingIntegrationTestBase(factory)
{
    private static readonly string[] ActionPrefixes =
        ["Billing.", "Payments.", "CreditNotes.", "Refunds.", "CashUp.", "Reconciliation.", "AccountingExport.", "BillingProfile."];

    private static readonly string[] Roles = [.. StaffRoles.All];

    private sealed record BillingEndpoint(string Method, string Pattern, string Action);

    [GeneratedRegex(@"\{(\w+)(:[^}]*)?\}")]
    private static partial Regex RouteParameter();

    private IReadOnlyList<BillingEndpoint> Endpoints()
    {
        var dataSources = Factory.Services.GetServices<EndpointDataSource>();
        var endpoints = new List<BillingEndpoint>();

        foreach (var endpoint in dataSources.SelectMany(d => d.Endpoints).OfType<RouteEndpoint>())
        {
            var attribute = endpoint.Metadata.OfType<RequireActionAttribute>().FirstOrDefault();
            if (attribute?.Arguments is not { Length: > 0 } args || args[0] is not string action) continue;
            if (!ActionPrefixes.Any(p => action.StartsWith(p, StringComparison.Ordinal))) continue;

            var methods = endpoint.Metadata.OfType<HttpMethodMetadata>().FirstOrDefault()?.HttpMethods ?? ["GET"];
            foreach (var method in methods)
                endpoints.Add(new BillingEndpoint(method, endpoint.RoutePattern.RawText ?? string.Empty, action));
        }

        return endpoints.OrderBy(e => e.Pattern, StringComparer.Ordinal).ThenBy(e => e.Method, StringComparer.Ordinal).ToList();
    }

    private string Url(string pattern) => "/" + RouteParameter().Replace(pattern, m =>
        m.Groups[1].Value.Contains("date", StringComparison.OrdinalIgnoreCase) ? Today.ToString("yyyy-MM-dd") : Guid.NewGuid().ToString()).TrimStart('/');

    private static HttpRequestMessage Request(BillingEndpoint endpoint, string url)
    {
        var request = new HttpRequestMessage(new HttpMethod(endpoint.Method), url);
        if (endpoint.Method is "POST" or "PUT" or "PATCH") request.Content = JsonContent.Create(new { });
        return request;
    }

    [Fact]
    public async Task I_J3_EveryBillingEndpoint_IsOpenToItsRoles_AndClosedToTheOthers()
    {
        var endpoints = Endpoints();
        Assert.True(endpoints.Count >= 50, $"Only {endpoints.Count} Billing endpoints were found; the matrix would prove nothing.");

        var context = await CreateBillingContextAsync();
        var clients = new Dictionary<string, HttpClient> { [StaffRoles.Administrator] = context.Admin };
        foreach (var role in Roles.Where(r => r != StaffRoles.Administrator))
            clients[role] = (await StaffClientAsync(context, role)).Client;

        var wrong = new List<string>();
        foreach (var endpoint in endpoints)
        {
            Assert.True(ActionRoles.TryGetRoles(endpoint.Action, out var allowed), $"{endpoint.Action} is not in ActionRoles.");
            Assert.DoesNotContain("Customer", allowed);

            foreach (var role in Roles)
            {
                var url = Url(endpoint.Pattern);
                using var response = await clients[role].SendAsync(Request(endpoint, url));
                var status = response.StatusCode;
                var shouldPass = allowed.Contains(role);

                if (shouldPass && status is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
                    wrong.Add($"{role} should reach {endpoint.Method} {endpoint.Pattern} ({endpoint.Action}) but got {(int)status}");
                else if (!shouldPass && status != HttpStatusCode.Forbidden)
                    wrong.Add($"{role} should be refused {endpoint.Method} {endpoint.Pattern} ({endpoint.Action}) but got {(int)status}");
            }
        }

        Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong.Take(30)));
    }

    [Fact]
    public async Task EveryBillingEndpoint_RefusesSomeoneWhoIsNotSignedIn_With401()
    {
        using var anonymous = CreateClient();
        var wrong = new List<string>();

        foreach (var endpoint in Endpoints())
        {
            using var response = await anonymous.SendAsync(Request(endpoint, Url(endpoint.Pattern)));
            if (response.StatusCode != HttpStatusCode.Unauthorized)
                wrong.Add($"{endpoint.Method} {endpoint.Pattern} answered {(int)response.StatusCode} to an anonymous caller");
        }

        Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong.Take(30)));
    }

    [Fact]
    public async Task ACustomer_CannotReachAnyStaffBillingEndpoint()
    {
        var (customer, _) = await RegisterAndSignInCustomerAsync();
        var wrong = new List<string>();

        foreach (var endpoint in Endpoints())
        {
            using var response = await customer.SendAsync(Request(endpoint, Url(endpoint.Pattern)));
            if (response.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden))
                wrong.Add($"{endpoint.Method} {endpoint.Pattern} answered {(int)response.StatusCode} to a customer");
        }

        Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong.Take(30)));
    }
}
