using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>The Boarding host with the booking rate limit pinned LOW (the shared Boarding factory raises it to 100000 so ordinary tests never trip it).</summary>
public sealed class BoardingRateLimitApiFactory : BoardingApiFactory
{
    protected override void ConfigureAdditionalSettings(IDictionary<string, string?> settings)
    {
        base.ConfigureAdditionalSettings(settings);
        settings["Boarding:Booking:PermitLimit"] = "3";
        settings["Boarding:Booking:WindowMinutes"] = "10";
    }
}

/// <summary>
/// TEST gate (HO-BRD-001 backlog row 23: "the three attributes are not exercised by a test"). The policy `boarding-booking` is partitioned per customer, 3 permits here. A request
/// that is refused for its CONTENT still spends a permit, so the calls use throw-away bodies and ids: what is under test is the attribute and the partition, not the handler.
/// </summary>
[Collection("Boarding")]
public sealed class BoardingRateLimitTests(BoardingRateLimitApiFactory factory) : BoardingIntegrationTestBase(factory), IClassFixture<BoardingRateLimitApiFactory>
{
    private async Task<HttpStatusCode> ReserveCallAsync(HttpClient customer) => (await customer.PostAsync($"/api/customers/me/boarding/stays/{Guid.NewGuid()}/reserve", null)).StatusCode;

    private async Task<HttpStatusCode> WaitlistCallAsync(HttpClient customer) =>
        (await customer.PostAsJsonAsync("/api/customers/me/boarding/waitlist",
            new { branchId = Guid.NewGuid(), arrivalOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5), departureOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(8), catCount = 1 }, Json)).StatusCode;

    private async Task<HttpStatusCode> DaycareCallAsync(HttpClient customer) =>
        (await customer.PostAsJsonAsync("/api/customers/me/daycare",
            new { branchId = Guid.NewGuid(), catId = Guid.NewGuid(), localDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2), basis = "Day" }, Json)).StatusCode;

    private static async Task AssertFourthIsRefusedAsync(Func<HttpClient, Task<HttpStatusCode>> call, HttpClient customer)
    {
        for (var i = 1; i <= 3; i++)
            Assert.NotEqual(HttpStatusCode.TooManyRequests, await call(customer)); // permits 1..3 are spent (their own answer is 404/400, not 429)

        Assert.Equal(HttpStatusCode.TooManyRequests, await call(customer));
    }

    [Fact]
    public async Task T_L1_Reserve_IsLimitedPerCustomer_AndAnotherCustomerIsNotAffected()
    {
        var (first, _, _) = await RegisterAndSignInCustomerAsync();
        var (second, _, _) = await RegisterAndSignInCustomerAsync();

        await AssertFourthIsRefusedAsync(ReserveCallAsync, first);

        Assert.NotEqual(HttpStatusCode.TooManyRequests, await ReserveCallAsync(second)); // the partition is the customer, not the shared test-server address
    }

    [Fact]
    public async Task T_L2_JoinWaitlist_IsLimitedPerCustomer()
    {
        var (customer, _, _) = await RegisterAndSignInCustomerAsync();

        await AssertFourthIsRefusedAsync(WaitlistCallAsync, customer);
    }

    [Fact]
    public async Task T_L3_BookDaycare_IsLimitedPerCustomer()
    {
        var (customer, _, _) = await RegisterAndSignInCustomerAsync();

        await AssertFourthIsRefusedAsync(DaycareCallAsync, customer);
    }

    [Fact]
    public async Task T_L4_TheThreeRoutesShareOneBucket_PerCustomer()
    {
        var (customer, _, _) = await RegisterAndSignInCustomerAsync();

        Assert.NotEqual(HttpStatusCode.TooManyRequests, await ReserveCallAsync(customer));
        Assert.NotEqual(HttpStatusCode.TooManyRequests, await WaitlistCallAsync(customer));
        Assert.NotEqual(HttpStatusCode.TooManyRequests, await DaycareCallAsync(customer));

        Assert.Equal(HttpStatusCode.TooManyRequests, await ReserveCallAsync(customer));
    }
}
