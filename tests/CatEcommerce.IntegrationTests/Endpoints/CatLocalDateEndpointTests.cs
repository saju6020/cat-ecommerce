using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// Bug fix 2026-10-10 (CAT date): the clinic is in Asia/Dhaka (UTC+6), but cat-record "cannot be in the
/// future" checks used the UTC date, so staff entering today's local date between 00:00 and 06:00 were
/// refused. The clock is pinned to 19:00Z, which is 01:00 the next day in Dhaka.
/// </summary>
public sealed class CatLocalDateEndpointTests(CatApiFactory factory) : CatIntegrationTestBase(factory)
{
    [Fact]
    public async Task RecordCatWeight_LocalTodayJustAfterMidnightInDhaka_Accepted_DayAfterRejected()
    {
        // Time-independent: the next 19:00Z (= 01:00 Asia/Dhaka) at least a day ahead of real time. It must
        // not be in the past: the session cookie's Expires is derived from the pinned clock, and the test
        // HttpClient's cookie container silently drops a cookie that is already expired by the real clock.
        var tomorrow = DateTimeOffset.UtcNow.AddDays(1);
        var instant = new DateTimeOffset(tomorrow.Year, tomorrow.Month, tomorrow.Day, 19, 0, 0, TimeSpan.Zero);
        var localToday = DateOnly.FromDateTime(instant.ToOffset(TimeSpan.FromHours(6)).DateTime);
        using var pinned = Factory.WithClockAt(instant);
        using var client = pinned.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"),
            HandleCookies = true
        });
        var catId = await RegisterCatAsync(client);

        var accepted = await client.PostAsJsonAsync($"/api/cats/{catId}/weights", new
        {
            kilograms = 4.0m, recordedOn = localToday, notes = (string?)null
        }, Json);
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);

        var rejected = await client.PostAsJsonAsync($"/api/cats/{catId}/weights", new
        {
            kilograms = 4.0m, recordedOn = localToday.AddDays(1), notes = (string?)null
        }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
    }
}
