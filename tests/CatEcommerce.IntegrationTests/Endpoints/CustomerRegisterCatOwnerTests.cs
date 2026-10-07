using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-CAT-002-ADDCAT (backlog CAT row AC1): the customer portal's add-a-cat screen sends
/// <c>ownerId = Guid.Empty</c>, relying on <c>CatsController.RegisterCat</c> forcing a customer
/// caller's owner to themselves (F19). These pin that against the real API, and that a customer
/// cannot register a cat for another owner by naming them.
/// </summary>
public sealed class CustomerRegisterCatOwnerTests(AccountsApiFactory factory) : AccountsIntegrationTestBase(factory)
{
    [Fact]
    public async Task CustomerPost_WithEmptyOwnerId_Returns201_AndTheCatIsOwnedByTheCaller()
    {
        var (client, customerId) = await RegisterAndSignInAsync();
        using var _client = client;

        var catId = await RegisterCatAsync(client, Guid.Empty);

        var cat = await client.GetFromJsonAsync<JsonElement>($"/api/cats/{catId}", Json);
        Assert.Equal(customerId, cat.GetProperty("ownerId").GetGuid());
    }

    [Fact]
    public async Task CustomerPost_NamingAnotherOwner_StillRegistersTheCatToTheCaller()
    {
        var (otherClient, otherCustomerId) = await RegisterAndSignInAsync();
        otherClient.Dispose();
        var (client, customerId) = await RegisterAndSignInAsync();
        using var _client = client;

        var catId = await RegisterCatAsync(client, otherCustomerId);

        var cat = await client.GetFromJsonAsync<JsonElement>($"/api/cats/{catId}", Json);
        Assert.Equal(customerId, cat.GetProperty("ownerId").GetGuid());
        Assert.NotEqual(otherCustomerId, cat.GetProperty("ownerId").GetGuid());
    }

    private static async Task<Guid> RegisterCatAsync(HttpClient client, Guid ownerId)
    {
        var response = await client.PostAsJsonAsync("/api/cats", new
        {
            ownerId, name = "Portal Cat", species = "Cat", sex = "Unknown", dateOfBirthPrecision = "Estimated"
        }, Json);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("catId").GetGuid();
    }

    private async Task<(HttpClient Client, Guid CustomerId)> RegisterAndSignInAsync()
    {
        var client = CreateClient();
        var (customerId, mobile, password) = await RegisterCustomerAsync(client);
        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));
        return (client, customerId);
    }
}
