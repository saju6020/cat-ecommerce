using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// TEST gate (2026-09-26). HO-CAT-001 §16 I19, literally: 25 cats under this test's own
/// <c>ownerId</c>, <c>pageSize=10</c> -> 10 returned, paging correct, scoped to the owner (the
/// table holds other tests' data, §16.1 rule 2). <c>CatsPaginationEndpointTests</c> already covers
/// the clamp/400/total-count behaviours generically with a handful of cats; this is the specific,
/// larger-population case §16 names.
/// </summary>
public sealed class CatOwnerScopedPaginationEndpointTests(CatApiFactory factory) : CatIntegrationTestBase(factory)
{
    // I19
    [Fact]
    public async Task GetCats_TwentyFiveCatsUnderOneOwner_PageSizeTen_ReturnsTenAndTotalScopedToOwner()
    {
        using var client = CreateClient();
        var ownerId = await SeedCustomerAsync();

        for (var i = 0; i < 25; i++)
            await RegisterCatAsync(client, ownerId, name: $"I19 Cat {i}");

        var response = await client.GetAsync($"/api/cats?ownerId={ownerId}&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(10, body.GetProperty("items").GetArrayLength());
        Assert.Equal(10, body.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, body.GetProperty("pageNumber").GetInt32());
        Assert.Equal(25, body.GetProperty("totalCount").GetInt32());

        // Every item on this page belongs to this test's own owner -- scoped, not a slice of the
        // whole shared table (§16.1 rule 2).
        foreach (var item in body.GetProperty("items").EnumerateArray())
            Assert.Equal(ownerId, item.GetProperty("ownerId").GetGuid());

        var secondPage = await client.GetAsync($"/api/cats?ownerId={ownerId}&pageSize=10&pageNumber=3");
        var secondBody = await secondPage.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(5, secondBody.GetProperty("items").GetArrayLength()); // 25 total, page 3 of 10 -> the last 5
    }
}
