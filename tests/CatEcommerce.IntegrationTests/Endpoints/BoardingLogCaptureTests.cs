using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using CatEcommerce.Api.Background;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// TEST gate, handover I-X1 (the build shipped no Boarding log capture): drive reserve, check-in, an observation, a high incident, the escalation pass, an early check-out and the
/// worker through a host whose every log line is captured, then assert no care note, incident description, observation note, emergency contact, customer name or mobile number is in any line.
/// </summary>
[Collection("Boarding")]
public sealed class BoardingLogCaptureTests(BoardingApiFactory factory) : BoardingIntegrationTestBase(factory)
{
    private WebApplicationFactory<Program>? _captured;
    private ConcurrentBag<string> _logs = [];

    private WebApplicationFactory<Program> Captured
    {
        get
        {
            if (_captured is null) (_captured, _logs) = Factory.WithCapturedLogs();
            return _captured;
        }
    }

    protected override HttpClient CreateClient() => Captured.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost/"), HandleCookies = true });

    [Fact]
    public async Task I_X1_NoCareNote_IncidentText_ContactOrCustomerDetail_IsLogged()
    {
        var stay = await CheckedInStayAsync(nights: 4);
        var observation = await stay.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{stay.StayId}/observations",
            new { catId = stay.CatIds[0], portionEaten = "Half", waterIntake = "Normal", litterOutput = "Normal", mood = "Relaxed", notes = "ZZOBS-PRIVATE-5512" }, Json);
        Assert.Equal(HttpStatusCode.Created, observation.StatusCode);
        var incident = await stay.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{stay.StayId}/incidents",
            new { catId = stay.CatIds[0], kind = "Injury", severity = "High", description = "ZZINC-PRIVATE-9921", immediateAction = "ZZACT-PRIVATE-4410", occurredOn = Now.AddMinutes(-5) }, Json);
        Assert.Equal(HttpStatusCode.Created, incident.StatusCode);

        Factory.Clock.Advance(TimeSpan.FromMinutes(31));
        await ReauthenticateAsync(stay.Branch.Admin, stay.Branch.AdminLogin);
        await using (var scope = Captured.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<BoardingWorkCycle>().RunAsync(); // escalation, daily update, task generation, lapse/confirm

        var day = stay.Arrival.AddDays(2);
        Factory.Clock.SetUtcNow(new DateTimeOffset(day.Year, day.Month, day.Day, 10, 0, 0, TimeSpan.FromHours(6)).ToUniversalTime());
        await ReauthenticateAsync(stay.Branch.Admin, stay.Branch.AdminLogin);
        var checkOut = await stay.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{stay.StayId}/check-out", new
        {
            cats = stay.CatIds.Select(id => new { catId = id, weightKg = 4.1m, belongings = Array.Empty<object>(), signature = TypedSignature("ZZSIGNER-PRIVATE-3307") }).ToArray()
        }, Json);
        Assert.True(checkOut.StatusCode == HttpStatusCode.OK, await checkOut.Content.ReadAsStringAsync());

        var lines = _logs.ToList();
        Assert.NotEmpty(lines); // the capture really saw this flow
        string[] secrets = ["ZZOBS-PRIVATE-5512", "ZZINC-PRIVATE-9921", "ZZACT-PRIVATE-4410", "ZZSIGNER-PRIVATE-3307", "Aunt May", "01711111111", stay.CustomerMobile, "Test Customer", "Scratch", "Metacam", "Own dry food"];
        foreach (var secret in secrets)
            Assert.DoesNotContain(lines, l => l.Contains(secret, StringComparison.OrdinalIgnoreCase));
    }
}
