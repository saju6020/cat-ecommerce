using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// TEST gate (HO-APT-001, extra check (d)) -- <c>ClinicDetailProjection.BuildAsync</c> projects
/// <c>AppointmentStatusChange.From.DisplayName</c>/<c>To.DisplayName</c> inside a nested collection
/// projection (<c>a.StatusHistory.OrderBy(...).Select(...)</c>) run against real PostgreSQL, not the
/// in-memory provider. <c>DisplayName</c> is a C# property on <c>Enumeration</c>, not a mapped
/// column, so this depends on EF Core successfully client-evaluating it inside the subquery rather
/// than throwing at translation time. Drives an appointment through four real transitions (Confirm,
/// CheckIn, Start, Complete) and reads the staff detail endpoint once at the end, asserting
/// <c>statusHistory[]</c> is present, in chronological order, and names the correct states at each
/// step -- not merely that the endpoint returns 200.
/// </summary>
public sealed class AppointmentDetailStatusHistoryTests(AppointmentsApiFactory factory, ITestOutputHelper output) : AppointmentsIntegrationTestBase(factory)
{
    private static readonly DateTimeOffset BaselineNow = new(2026, 10, 12, 4, 0, 0, TimeSpan.Zero); // 2026-10-12 10:00 Asia/Dhaka

    [Fact]
    public async Task GetDetail_AfterFourTransitions_StatusHistoryIsOrderedAndNamesEachState()
    {
        var (webFactory, timeProvider) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;
        var (adminClient, context, admin) = await SeedBranchWithAdminAssignedAsync(apiFactory, BaselineNow);

        var (customerClient, customerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var catId = await SeedCatAsync(customerId);
        var startsOn = BaselineNow.AddHours(2);

        // Held -> Booked (Confirm), inside BookConfirmedAppointmentAsync.
        var appointmentId = await BookConfirmedAppointmentAsync(customerClient, context, catId, startsOn);

        timeProvider.SetUtcNow(startsOn);
        await ReauthenticateAsync(adminClient, admin.Mobile, admin.Password);

        // Booked -> CheckedIn.
        var checkIn = await adminClient.PostAsJsonAsync($"/api/staff/appointments/{appointmentId}/check-in", new { }, Json);
        Assert.True(checkIn.StatusCode == HttpStatusCode.NoContent, await checkIn.Content.ReadAsStringAsync());

        // Test-only fix (found during a later gate's verification, unrelated to that gate's own
        // brief): the fake clock must advance a little between each of these three transitions.
        // ClinicDetailProjection's own StatusHistory query orders only by ChangedOn -- with no
        // second sort key, three rows sharing the exact same instant (as these did when the clock
        // never moved between them) sort in whatever order Postgres happens to return ties in,
        // which is not guaranteed to be insertion order. That made this test genuinely flaky
        // (reproduced: failed in ~half of repeated runs), unrelated to any notification-backlog
        // issue. Advancing by a minute per step gives each transition its own distinct ChangedOn,
        // so the existing chronological-order assertion is deterministic without touching
        // ClinicDetailProjection.cs (a production file) at all.
        timeProvider.SetUtcNow(startsOn.AddMinutes(1));

        // CheckedIn -> InConsultation.
        var start = await adminClient.PostAsJsonAsync($"/api/staff/appointments/{appointmentId}/start", new { }, Json);
        Assert.True(start.StatusCode == HttpStatusCode.NoContent, await start.Content.ReadAsStringAsync());

        timeProvider.SetUtcNow(startsOn.AddMinutes(2));

        // InConsultation -> Completed.
        var complete = await adminClient.PostAsync($"/api/staff/appointments/{appointmentId}/complete", null);
        Assert.True(complete.StatusCode == HttpStatusCode.NoContent, await complete.Content.ReadAsStringAsync());

        var detail = await adminClient.GetFromJsonAsync<JsonElement>($"/api/staff/appointments/{appointmentId}", Json);
        Assert.Equal("Completed", detail.GetProperty("status").GetString());

        var history = detail.GetProperty("statusHistory").EnumerateArray().ToList();
        output.WriteLine("RAW statusHistory: " + detail.GetProperty("statusHistory").GetRawText());

        // Held -> Booked is written by Confirm before this test's own clock advance, so it is not
        // necessarily the same instant as the rest; only chronological order across all four matters.
        Assert.True(history.Count >= 4, $"Expected at least 4 status-history rows, found {history.Count}.");

        var changedOns = history.Select(h => h.GetProperty("changedOn").GetDateTimeOffset()).ToList();
        Assert.Equal(changedOns.OrderBy(x => x), changedOns);

        (string From, string To) Transition(int index) =>
            (history[index].GetProperty("from").GetString()!, history[index].GetProperty("to").GetString()!);

        // The four transitions this test drove must appear, each naming both states, in order.
        var lastFour = Enumerable.Range(history.Count - 4, 4).Select(Transition).ToList();
        Assert.Equal(("Held", "Booked"), lastFour[0]);
        Assert.Equal(("Booked", "CheckedIn"), lastFour[1]);
        Assert.Equal(("CheckedIn", "InConsultation"), lastFour[2]);
        Assert.Equal(("InConsultation", "Completed"), lastFour[3]);

        foreach (var row in history)
            Assert.NotEqual(Guid.Empty, row.GetProperty("changedBy").GetGuid());
    }

    /// <summary>APT backlog row 47 -- the clock never moves between transitions, so every changedOn
    /// ties; the Sequence tie-breaker must still return them in insertion order, every time.</summary>
    [Fact]
    public async Task GetDetail_TransitionsAtTheSameInstant_StayInInsertionOrder()
    {
        var (webFactory, timeProvider) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;
        var (adminClient, context, admin) = await SeedBranchWithAdminAssignedAsync(apiFactory, BaselineNow);
        var (customerClient, customerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var catId = await SeedCatAsync(customerId);
        var startsOn = BaselineNow.AddHours(2);
        var appointmentId = await BookConfirmedAppointmentAsync(customerClient, context, catId, startsOn);

        timeProvider.SetUtcNow(startsOn);
        await ReauthenticateAsync(adminClient, admin.Mobile, admin.Password);

        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PostAsJsonAsync($"/api/staff/appointments/{appointmentId}/check-in", new { }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PostAsJsonAsync($"/api/staff/appointments/{appointmentId}/start", new { }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PostAsync($"/api/staff/appointments/{appointmentId}/complete", null)).StatusCode);

        for (var read = 0; read < 5; read++)
        {
            var detail = await adminClient.GetFromJsonAsync<JsonElement>($"/api/staff/appointments/{appointmentId}", Json);
            var tail = detail.GetProperty("statusHistory").EnumerateArray().TakeLast(3)
                .Select(h => (h.GetProperty("from").GetString(), h.GetProperty("to").GetString())).ToList();

            Assert.Equal([("Booked", "CheckedIn"), ("CheckedIn", "InConsultation"), ("InConsultation", "Completed")], tail);
        }
    }
}
