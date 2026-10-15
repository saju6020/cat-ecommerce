using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-RPT-001 section 16 X -- I-X1 / U-X4 (what the logs of a report and of a scheduled run hold) and I-X2 (what a report response body carries). The log capture is a logger provider added to the test
/// host (<see cref="ReportingApiFactory.LoggedLines"/>). In the schedule collection: its scheduled run is driven by the same worker pass, so it must not overlap the other schedule classes.
/// </summary>
[Collection(ScheduleTestBase.CollectionName)]
public sealed class ReportingCrossCuttingEndpointTests(ReportingApiFactory factory) : ScheduleTestBase(factory)
{
    private static readonly Regex Sensitive = new("password|hash|token", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // ── I-X1 + U-X4 ─────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_X1_U_X4_TheLogsOfAReport_ItsExport_AndAScheduledRun_HoldNoRowDataNoContactDetailAndNoFileContent()
    {
        await RetireOtherSchedulesAsync();
        var (context, admin) = await ContextWithRoleAsync(StaffRoles.Administrator);
        var recipient = await SeedRecipientAsync(context, StaffRoles.Reception);
        var customerName = "Zubaida Logcheck " + NewCode();
        var customerMobile = NewMobile();
        await ExpectAsync(HttpStatusCode.Created, await admin.PostAsJsonAsync("/api/staff/customers", new { fullName = customerName, mobileNumber = customerMobile }, Json));

        var before = ReportingFactory.LoggedLines.Count;

        // An on-screen report, both exports, and a scheduled run with its delivery.
        await RunReportAsync(admin, ReportingFixtures.RowsKey, context, "rows=3", Today, Today);
        await ExpectAsync(HttpStatusCode.OK, await admin.GetAsync($"/api/staff/reports/{ReportingFixtures.RowsKey}/export.csv" + Query(context, Today, Today, "rows=3")));
        await ExpectAsync(HttpStatusCode.OK, await admin.GetAsync($"/api/staff/reports/{ReportingFixtures.RowsKey}/export.pdf" + Query(context, Today, Today, "rows=3")));

        var created = await CreateScheduleAsync(admin, Body(context, ReportingFixtures.RowsKey, "Today", "10:00", [recipient.Id]));
        try
        {
            await AdvanceToAsync(created.NextRunOn!.Value);
            await RunReportingCycleAsync();
            await RunDeliveryPassesUntilAsync(() => Task.FromResult(EmailsTo(recipient.Email).Count > 0));
            var csv = Encoding.UTF8.GetString(EmailsTo(recipient.Email).Single().AttachmentList.Single().Content);
            Assert.Contains("Row 3", csv); // the file really holds the rows we are about to look for in the logs

            var lines = ReportingFactory.LoggedLines.Skip(before).Where(l => !l.StartsWith("Microsoft.EntityFrameworkCore.Database.Command|", StringComparison.Ordinal)).ToList();
            Assert.NotEmpty(lines);

            // No row data, no file content, no contact detail, no customer name.
            foreach (var marker in new[] { "Row 1", "Row 2", "Row 3", "HYPERLINK", "বিড়াল", recipient.Email, recipient.Mobile, customerName, customerMobile })
                Assert.DoesNotContain(lines, l => l.Contains(marker, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            await CancelAsync(admin, created.ScheduleId);
        }
    }

    // ── I-X2 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_X2_NoReportBodyHasAPasswordHashOrTokenField_AndMoneyAmountsAreStrings()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var charge = await RecordChargeAsync(context, customerId, 800m, 1m, itemKind: ItemKind.Service, sourceKind: ChargeSourceKind.Appointment, description: "Consultation");
        await IssueFromChargesAsync(context.Admin, customerId, [charge]);

        var bodies = new List<(string What, JsonElement Body)>
        {
            ("catalogue", await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await context.Admin.GetAsync("/api/staff/reports")))),
            ("fixture-rows", await RunReportAsync(context.Admin, ReportingFixtures.RowsKey, context, "rows=3", Today, Today)),
            (ReportCatalogue.RevenueByBusinessLineKey, await RunReportAsync(context.Admin, ReportCatalogue.RevenueByBusinessLineKey, context)),
            (ReportCatalogue.ReceivablesAgeingKey, await RunReportAsync(context.Admin, ReportCatalogue.ReceivablesAgeingKey, context)),
            (ReportCatalogue.RefundsCreditNotesKey, await RunReportAsync(context.Admin, ReportCatalogue.RefundsCreditNotesKey, context)),
            ("dashboard", await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await context.Admin.GetAsync("/api/staff/dashboard")))),
        };

        var moneyObjects = 0;
        foreach (var (what, body) in bodies)
        {
            Walk(what, body, ref moneyObjects);
        }

        Assert.True(moneyObjects > 0, "At least one money value was inspected.");
    }

    private static void Walk(string path, JsonElement element, ref int moneyObjects)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (element.TryGetProperty("amount", out var amount) && element.TryGetProperty("currencyCode", out _))
                {
                    Assert.True(amount.ValueKind == JsonValueKind.String, $"{path}.amount is {amount.ValueKind}, money must be a string.");
                    moneyObjects++;
                }

                foreach (var property in element.EnumerateObject())
                {
                    Assert.DoesNotMatch(Sensitive, property.Name);
                    Walk(path + "." + property.Name, property.Value, ref moneyObjects);
                }

                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray()) Walk($"{path}[{index++}]", item, ref moneyObjects);
                break;
        }
    }
}
