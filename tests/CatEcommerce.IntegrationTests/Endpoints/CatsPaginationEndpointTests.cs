using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// CODE_REVIEW gate, MEDIUM (2026-09-26): owner decision -- pageSize is capped at 100 (clamped,
/// not rejected); a value of 0 or less is a 400; every list endpoint returns a total count via
/// the shared PagedResult shape. Covers GET /api/cats, GET /api/cats/{id}/weights and
/// GET /api/vaccines (HO-CAT-001 §1 Amended).
///
/// HO-ADM-001 §12.3 / ADR-043: every cat and vaccine action now requires a signed-in caller
/// (an anonymous request short-circuits to 401 before the controller's own paging validation
/// ever runs), so every case here signs in a fresh Administrator first -- Administrator holds
/// every Cats/Vaccines action (§12.3's table), so it never becomes the thing under test.
///
/// SECURITY/ARCHITECTURE/CODE_REVIEW gates, second pass (2026-09-26): pageNumber also has an
/// upper bound now (PagingRules.MaxPageNumber, shared by the new Api.Controllers.PagingHelper),
/// closing the int-overflow-to-negative-OFFSET 500 the second-pass SECURITY gate proved live at
/// pageNumber=2147483647 on all three of these endpoints.
/// </summary>
public sealed class CatsPaginationEndpointTests(CatApiFactory factory) : CatIntegrationTestBase(factory)
{
    [Fact]
    public async Task GetCats_PageSizeZero_Returns400()
    {
        using var client = CreateClient();
        await SignInAsFreshAdministratorAsync(client);

        var response = await client.GetAsync("/api/cats?pageSize=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetCats_NegativePageSize_Returns400()
    {
        using var client = CreateClient();
        await SignInAsFreshAdministratorAsync(client);

        var response = await client.GetAsync("/api/cats?pageSize=-1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>SECURITY/ARCHITECTURE/CODE_REVIEW gates, second pass (2026-09-26): proven live at
    /// pageNumber=2147483647 -- Query&lt;T&gt;.Skip's (PageNumber-1)*PageSize overflowed int to a
    /// negative OFFSET Postgres rejected with an uncaught 500, since only pageSize was capped.
    /// PagingHelper now rejects a pageNumber above PagingRules.MaxPageNumber with 400.</summary>
    [Fact]
    public async Task GetCats_PageNumberAboveMaximum_Returns400()
    {
        using var client = CreateClient();
        await SignInAsFreshAdministratorAsync(client);

        var response = await client.GetAsync("/api/cats?pageNumber=2147483647");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetCats_PageSizeOverMaximum_IsClampedTo100()
    {
        using var client = CreateClient();
        // SECURITY gate, LOW (2026-09-26): a staff-supplied ownerId must resolve to a real
        // customer (RegisterCatCommandHandler) -- a bare Guid.NewGuid() now fails 400.
        var ownerId = await SeedCustomerAsync();
        await RegisterCatAsync(client, ownerId);

        var response = await client.GetAsync($"/api/cats?ownerId={ownerId}&pageSize=100000");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(100, body.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task GetCats_ReturnsTotalCountAndItems()
    {
        using var client = CreateClient();
        var ownerId = await SeedCustomerAsync();
        await RegisterCatAsync(client, ownerId);
        await RegisterCatAsync(client, ownerId);
        await RegisterCatAsync(client, ownerId);

        var response = await client.GetAsync($"/api/cats?ownerId={ownerId}&pageSize=2");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(3, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, body.GetProperty("items").GetArrayLength());
        Assert.Equal(2, body.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task GetCatWeightHistory_PageSizeZero_Returns400()
    {
        using var client = CreateClient();
        var catId = await RegisterCatAsync(client);

        var response = await client.GetAsync($"/api/cats/{catId}/weights?pageSize=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetCatWeightHistory_PageNumberAboveMaximum_Returns400()
    {
        using var client = CreateClient();
        var catId = await RegisterCatAsync(client);

        var response = await client.GetAsync($"/api/cats/{catId}/weights?pageNumber=2147483647");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetCatWeightHistory_PageSizeOverMaximum_IsClampedTo100()
    {
        using var client = CreateClient();
        var catId = await RegisterCatAsync(client);

        var response = await client.GetAsync($"/api/cats/{catId}/weights?pageSize=100000");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(100, body.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task GetCatWeightHistory_ReturnsTotalCountAndItems()
    {
        using var client = CreateClient();
        var catId = await RegisterCatAsync(client);

        for (var i = 0; i < 3; i++)
        {
            var weightResponse = await client.PostAsJsonAsync($"/api/cats/{catId}/weights", new
            {
                kilograms = 3.5m + i,
                recordedOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-i),
                notes = (string?)null
            }, Json);
            weightResponse.EnsureSuccessStatusCode();
        }

        var response = await client.GetAsync($"/api/cats/{catId}/weights?pageSize=2");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(3, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, body.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task GetVaccines_PageSizeZero_Returns400()
    {
        using var client = CreateClient();
        await SignInAsFreshAdministratorAsync(client);

        var response = await client.GetAsync("/api/vaccines?pageSize=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetVaccines_PageNumberAboveMaximum_Returns400()
    {
        using var client = CreateClient();
        await SignInAsFreshAdministratorAsync(client);

        var response = await client.GetAsync("/api/vaccines?pageNumber=2147483647");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetVaccines_PageSizeOverMaximum_IsClampedTo100()
    {
        using var client = CreateClient();
        await SignInAsFreshAdministratorAsync(client);
        var code = $"VAX-{Guid.NewGuid():N}";
        var registerResponse = await client.PostAsJsonAsync("/api/vaccines", new
        {
            code, name = "Test Vaccine", isCore = false, validityMonths = 12, description = (string?)null
        }, Json);
        registerResponse.EnsureSuccessStatusCode();

        var response = await client.GetAsync("/api/vaccines?pageSize=100000");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(100, body.GetProperty("pageSize").GetInt32());
        Assert.True(body.GetProperty("totalCount").GetInt32() >= 1);
    }
}
