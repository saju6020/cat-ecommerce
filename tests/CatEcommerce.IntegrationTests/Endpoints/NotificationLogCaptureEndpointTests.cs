using System.Net;
using System.Net.Http.Json;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-NTF-001 §8.10 (reset token/link, ADR-063), §8.6 (rendered variable values), §16 I-X1.
/// TEST gate, second pass, 2026-09-27 -- closes a first-pass BLOCKED row. The first pass's own
/// reasoning ("<c>NotificationsApiFactory</c> has no <c>WithCapturedLogs()</c> ... unlike
/// <c>CatEcommerceApiFactory</c>/<c>FilesApiFactory</c>") was itself mistaken:
/// <c>NotificationsApiFactory : CatEcommerceApiFactory</c>, and <c>WithCapturedLogs()</c> is a
/// plain public instance method on the base class with no override anywhere in this tree -- it was
/// already inherited, simply unused here. <c>AdministrationApiFactory</c>'s own
/// <c>AdminLogCaptureEndpointTests</c> made the same discovery for its own area (TEST gate seventh
/// pass); this is that same harness's first use for Notifications. A representative set of flows
/// that carry the exact values §8.10/§8.6 forbid logging (a live password-reset link/token, a care
/// reminder's rendered variable values) is run against a log-capturing factory, with unique,
/// greppable markers; none of those markers may appear anywhere in the captured log lines, at any
/// level -- broader than "Information+", since a leak at ANY level (including Debug) still counts.
/// </summary>
[Collection("Notifications")]
public sealed class NotificationLogCaptureEndpointTests(NotificationsApiFactory factory) : NotificationsIntegrationTestBase(factory)
{
    [Fact]
    public async Task RepresentativeFlows_NeverLogTokenLinkOrRenderedVariableValues()
    {
        var (capturingFactory, logs) = Factory.WithCapturedLogs();

        // -- Password reset: registration itself is not the point (any factory would do); the LIVE
        // token and its link are only ever produced/consumed from here on, all through capturingFactory.
        var (_, _, mobile) = await RegisterAndSignInCustomerAsync();
        var mobileNormalised = "+880" + mobile[1..];
        using var resetClient = capturingFactory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost/"), HandleCookies = true });
        var reset = await resetClient.PostAsJsonAsync("/api/auth/password-reset/request", new { identifier = mobileNormalised }, Json);
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        await RunDeliveryPassAsync(capturingFactory); // expand -- renders the live reset link
        await RunDeliveryPassAsync(capturingFactory); // deliver -- sends it, then Secret-redacts

        // -- Care reminder: a distinctive, greppable cat name standing in for §8.6's rendered
        // variable values (catName, vaccineName, dueOn).
        var marker = Guid.NewGuid().ToString("N")[..12];
        var catNameMarker = $"CATNAME-{marker}";
        var (_, reminderCustomerId, _) = await RegisterAndSignInCustomerAsync();
        var catId = await SeedCatAsync(reminderCustomerId, catNameMarker);
        await SeedVaccinationDueInAsync(catId, dueInDays: 5);

        await RunReminderSweepAsync(capturingFactory);
        await RunDeliveryPassAsync(capturingFactory);
        await RunDeliveryPassAsync(capturingFactory);

        var allLogText = string.Join("\n", logs);
        Assert.DoesNotContain("reset-password#token=", allLogText);
        Assert.DoesNotContain(mobileNormalised, allLogText);
        Assert.DoesNotContain(catNameMarker, allLogText);
    }
}
