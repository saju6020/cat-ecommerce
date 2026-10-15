using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-POR-001 ADR-261 (IDOR) beyond "two customers": the dashboard follows the cat's CURRENT owner and the account's CURRENT identity.
/// TEST gate additions (2026-10-10): a cat transferred to another customer leaves the former owner's dashboard at once (cats and visits),
/// and a merged-away (duplicate) customer's records never appear on a third customer's dashboard. The ownership change and the merge are
/// done through the owning modules' staff endpoints, never by editing rows.
/// </summary>
public sealed class PortalOwnershipChangeEndpointTests(PortalApiFactory factory) : PortalIntegrationTestBase(factory)
{
    private static List<Guid> Ids(JsonElement board, string section, string idName, string list = "items") =>
        board.GetProperty(section).GetProperty("data").GetProperty(list).EnumerateArray().Select(i => i.GetProperty(idName).GetGuid()).ToList();

    [Fact]
    public async Task ATransferredCat_AndItsVisits_LeaveTheFormerOwnersDashboard_AndTheNewOwnerSeesTheCat()
    {
        var clinic = await SeedBookableBranchAsync();
        var former = await RegisterCustomerAsync("Former Owner");
        var newOwner = await RegisterCustomerAsync("New Owner");
        var cat = await RegisterCatAsync(former, "Travelled");
        var visit = await FinaliseVisitAsync(clinic, former.CustomerId, cat);

        var before = await GetDashboardAsync(former.Client);
        Assert.Equal([cat], Ids(before, "cats", "catId"));      // the precondition: the former owner does see the cat and its visit
        Assert.Equal([visit], Ids(before, "visits", "visitId"));

        var (admin, _) = await SignInNewStaffAsync(StaffRoles.Administrator);
        var transfer = await admin.PostAsJsonAsync($"/api/cats/{cat}/ownership-transfer", new { newOwnerId = newOwner.CustomerId, reason = "Sold to a friend" }, Json);
        await ExpectAsync(HttpStatusCode.OK, transfer);

        var after = await GetDashboardAsync(former.Client);
        Assert.Empty(Ids(after, "cats", "catId"));
        Assert.False(after.GetProperty("cats").GetProperty("data").GetProperty("hasCats").GetBoolean());
        Assert.Empty(Ids(after, "visits", "visitId"));
        // Sections that hold the cat's records. (The former owner's OWN appointment for the cat still names it -- Appointments' read, not the
        // Portal's; recorded as POR backlog 14.)
        foreach (var section in new[] { "cats", "visits", "prescriptions" })
        {
            Assert.DoesNotContain(cat.ToString(), after.GetProperty(section).ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(visit.ToString(), after.GetProperty(section).ToString(), StringComparison.OrdinalIgnoreCase);
        }

        var newBoard = await GetDashboardAsync(newOwner.Client);
        Assert.Equal([cat], Ids(newBoard, "cats", "catId"));
    }

    [Fact]
    public async Task AMergedAwayCustomersRecords_NeverAppearOnAThirdCustomersDashboard()
    {
        var clinic = await SeedBookableBranchAsync();
        var taxClassId = await PrepareBillingAsync(clinic);
        var survivor = await RegisterCustomerAsync("Survivor Owner");
        var duplicate = await RegisterCustomerAsync("Duplicate Owner");
        var bystander = await RegisterCustomerAsync("Bystander Owner");

        var duplicateCat = await RegisterCatAsync(duplicate, "DupCat");
        var duplicateInvoice = await IssueInvoiceAsync(clinic, taxClassId, duplicate.CustomerId, 450m);
        await RegisterCatAsync(bystander, "ByCat");

        var shared = NewMobile();   // the merge rule: the two records must share a contact number
        foreach (var customer in new[] { survivor, duplicate })
            await ExpectAsync(HttpStatusCode.NoContent, await customer.Client.PutAsJsonAsync("/api/customers/me",
                new { fullName = customer.FullName, additionalPhoneNumbers = new[] { shared } }, Json));

        var (admin, _) = await SignInNewStaffAsync(StaffRoles.Administrator);
        var merge = await admin.PostAsJsonAsync($"/api/staff/customers/{duplicate.CustomerId}/merge", new { survivingCustomerId = survivor.CustomerId, reason = "Same person" }, Json);
        await ExpectAsync(HttpStatusCode.OK, merge);

        // The merged-away account's own session is dead: it cannot read a dashboard any more (SECURITY gate: MergeInto rotates the security stamp).
        var staleSession = await duplicate.Client.GetAsync("/api/portal/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, staleSession.StatusCode);

        var bystanderBoard = await GetDashboardAsync(bystander.Client);
        Assert.DoesNotContain(duplicateCat.ToString(), bystanderBoard.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(duplicateInvoice.ToString(), bystanderBoard.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Duplicate Owner", bystanderBoard.ToString());
        Assert.DoesNotContain("Survivor Owner", bystanderBoard.ToString());
        Assert.Equal("Bystander Owner", bystanderBoard.GetProperty("customer").GetProperty("data").GetProperty("fullName").GetString());

        // The surviving account still gets its own dashboard (its own name; a forged id is still ignored).
        var survivorBoard = await GetDashboardAsync(survivor.Client);
        Assert.Equal("Survivor Owner", survivorBoard.GetProperty("customer").GetProperty("data").GetProperty("fullName").GetString());
        Assert.DoesNotContain("Bystander Owner", survivorBoard.ToString());
        // Billing folds the merged-away customer into the survivor (its ADR-192): the duplicate's issued invoice is the survivor's to see.
        Assert.Contains(duplicateInvoice, Ids(survivorBoard, "balances", "invoiceId", "invoices"));
    }
}
