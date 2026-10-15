using System.Net;
using System.Net.Http.Json;
using CatEcommerce.Read.Queries.Portal;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Core.Queries;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-POR-001 §16 groups A and B, Phase A sections (cats, appointments, profile). Each test mints its own customers and
/// asserts only their own dashboard (§16.1 rules 1 to 4).</summary>
public sealed class PortalDashboardEndpointTests(PortalApiFactory factory) : PortalIntegrationTestBase(factory)
{
    // I-A1 -- the host starts with ValidateScopes + ValidateOnBuild (Program.cs), so reaching this line already proves every reader
    // and every upstream handler it takes is registered; resolving them explicitly makes the proof visible and also covers the Q1 handler.
    [Fact]
    public async Task I_A1_TheComposingHandlerAndItsReadersResolve()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IQueryHandlerAsync<GetPortalDashboardQuery, PortalDashboardViewModel>>());
        Assert.NotNull(scope.ServiceProvider.GetService(typeof(CatEcommerce.Read.QueryHandlers.Portal.IPortalSectionReader<PortalCatsViewModel>)));
        Assert.NotNull(scope.ServiceProvider.GetService(typeof(CatEcommerce.Read.QueryHandlers.Portal.IPortalSectionReader<PortalAppointmentsViewModel>)));
        Assert.NotNull(scope.ServiceProvider.GetService(typeof(CatEcommerce.Read.QueryHandlers.Portal.IPortalSectionReader<PortalCustomerViewModel>)));

        var customer = await RegisterCustomerAsync();
        Assert.Equal(HttpStatusCode.OK, (await customer.Client.GetAsync("/api/portal/dashboard")).StatusCode);
    }

    // I-A3
    [Fact]
    public async Task I_A3_NoSession_AndStaffSessionOnly_Are401SessionInvalid()
    {
        var anonymous = ClientOn(Factory);
        var anonymousResponse = await anonymous.GetAsync("/api/portal/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Equal("session_invalid", (await ReadAsync(anonymousResponse)).GetProperty("error").GetString());

        var (staff, _) = await SignInNewStaffAsync(StaffRoles.Administrator);
        var staffResponse = await staff.GetAsync("/api/portal/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, staffResponse.StatusCode);
        Assert.Equal("session_invalid", (await ReadAsync(staffResponse)).GetProperty("error").GetString());
    }

    // I-B1 (Phase A part) -- US-POR-01 AC2
    [Fact]
    public async Task I_B1_NewCustomer_CatsReadyWithHasCatsFalse_AppointmentsReadyEmpty_GreetingPresent()
    {
        var customer = await RegisterCustomerAsync("Nusrat Jahan");

        var response = await customer.Client.GetAsync("/api/portal/dashboard");
        await ExpectAsync(HttpStatusCode.OK, response);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        var dashboard = await ReadAsync(response);

        Assert.Equal("Ready", dashboard.GetProperty("cats").GetProperty("state").GetString());
        var cats = dashboard.GetProperty("cats").GetProperty("data");
        Assert.False(cats.GetProperty("hasCats").GetBoolean());
        Assert.False(cats.GetProperty("hasMore").GetBoolean());
        Assert.Empty(cats.GetProperty("items").EnumerateArray());

        Assert.Equal("Ready", dashboard.GetProperty("appointments").GetProperty("state").GetString());
        Assert.Empty(dashboard.GetProperty("appointments").GetProperty("data").GetProperty("items").EnumerateArray());

        Assert.Equal("Nusrat Jahan", dashboard.GetProperty("customer").GetProperty("data").GetProperty("fullName").GetString());
    }

    // I-B2 (Phase A part) + I-B3 -- two customers, each sees only their own; a deceased cat is absent; the appointment is listed
    [Fact]
    public async Task I_B2_B3_CatsAndAppointments_AreOwnerScoped_AndDeceasedCatIsAbsent()
    {
        var clinic = await SeedBookableBranchAsync();
        var first = await RegisterCustomerAsync("First Owner");
        var second = await RegisterCustomerAsync("Second Owner");

        var mitu = await RegisterCatAsync(first, "Mitu");
        var gone = await RegisterCatAsync(first, "Departed");
        var other = await RegisterCatAsync(second, "Other");
        await MarkCatDeceasedAsync(gone);

        var startsOn = BookableSlot();
        var appointmentId = await BookConfirmedAppointmentAsync(first.Client, clinic, mitu, startsOn);

        var firstBoard = await GetDashboardAsync(first.Client);
        var firstCats = firstBoard.GetProperty("cats").GetProperty("data").GetProperty("items").EnumerateArray().ToList();
        Assert.Equal([mitu], firstCats.Select(c => c.GetProperty("catId").GetGuid()));
        Assert.True(firstBoard.GetProperty("cats").GetProperty("data").GetProperty("hasCats").GetBoolean());

        var appointments = firstBoard.GetProperty("appointments").GetProperty("data").GetProperty("items").EnumerateArray().ToList();
        var item = Assert.Single(appointments);
        Assert.Equal(appointmentId, item.GetProperty("appointmentId").GetGuid());
        Assert.Equal(mitu, item.GetProperty("catId").GetGuid());
        Assert.Equal("Mitu", item.GetProperty("catName").GetString());
        Assert.Equal("Asia/Dhaka", item.GetProperty("branchTimeZoneId").GetString());
        Assert.Equal("Booked", item.GetProperty("status").GetString());
        Assert.Equal(startsOn.ToUniversalTime(), item.GetProperty("startsOn").GetDateTimeOffset().ToUniversalTime());

        var secondBoard = await GetDashboardAsync(second.Client);
        Assert.Equal([other], secondBoard.GetProperty("cats").GetProperty("data").GetProperty("items").EnumerateArray().Select(c => c.GetProperty("catId").GetGuid()));
        Assert.Empty(secondBoard.GetProperty("appointments").GetProperty("data").GetProperty("items").EnumerateArray());
    }

    // I-B4
    [Fact]
    public async Task I_B4_VaccinationStatus_IsIdenticalToTheCatProfileEndpoint()
    {
        var customer = await RegisterCustomerAsync();
        var catId = await RegisterCatAsync(customer, "Vacc");

        var board = await GetDashboardAsync(customer.Client);
        var cat = Assert.Single(board.GetProperty("cats").GetProperty("data").GetProperty("items").EnumerateArray());
        Assert.Equal("Ready", cat.GetProperty("vaccinationState").GetString());
        var onDashboard = cat.GetProperty("vaccination");

        var direct = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await customer.Client.GetAsync($"/api/cats/{catId}/vaccination-status")));
        Assert.Equal(direct.GetProperty("overallStatus").GetString(), onDashboard.GetProperty("overallStatus").GetString());
        Assert.Equal(direct.GetProperty("bookingEligible").GetBoolean(), onDashboard.GetProperty("bookingEligible").GetBoolean());
        Assert.Equal(direct.GetProperty("earliestNextDueOn").ToString(), onDashboard.GetProperty("earliestNextDueOn").ToString());
    }

    // I-B6
    [Fact]
    public async Task I_B6_ThirteenAliveCats_TwelveReturned_HasMoreTrue()
    {
        var customer = await RegisterCustomerAsync();
        for (var i = 0; i < PortalRules.DashboardCatLimit + 1; i++)
            await RegisterCatAsync(customer, $"Cat{i:00}");

        var cats = (await GetDashboardAsync(customer.Client)).GetProperty("cats").GetProperty("data");
        Assert.Equal(PortalRules.DashboardCatLimit, cats.GetProperty("items").GetArrayLength());
        Assert.True(cats.GetProperty("hasMore").GetBoolean());
    }
}
