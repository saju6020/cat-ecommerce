using System.Net;
using System.Net.Http.Json;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-NTF-001 §8.5, U-B14. TEST gate, second pass, 2026-09-27 -- closes a first-pass BLOCKED row:
/// no isolated test exercised <c>AccountRecipientDirectory</c>'s own merge-chain-walk and
/// anonymised-recipient resolution directly; only end-to-end coverage existed
/// (I-B14/I-D10/I-D11), which exercises the effect through a full notification-delivery scenario,
/// never <c>INotificationRecipientDirectory.ResolveAsync</c> itself. These call the directory
/// directly against a real <see cref="CatEcommerce.Infrastructure.Persistence.CatEcommerceDbContext"/>,
/// seeded through the same staff/customer HTTP endpoints the rest of this suite already trusts.
/// </summary>
[Collection("Notifications")]
public sealed class AccountRecipientDirectoryTests(NotificationsApiFactory factory) : NotificationsIntegrationTestBase(factory)
{
    private const string CustomerPassword = "correcthorsebattery"; // matches RegisterAndSignInCustomerAsync's own hardcoded password

    /// <summary>A genuine TWO-HOP merge chain (A -&gt; B -&gt; survivor) -- proves the directory's own
    /// reverse walk (§8.5, bounded at 5 hops) actually follows a chain longer than one link, not
    /// just a single merge.</summary>
    [Fact]
    public async Task ResolveAsync_TwoHopMergeChain_ResolvesToTheFinalSurvivor()
    {
        var (clientA, customerA, _) = await RegisterAndSignInCustomerAsync();
        var (clientB, customerB, _) = await RegisterAndSignInCustomerAsync();
        var (survivorClient, survivorId, _) = await RegisterAndSignInCustomerAsync();

        // Merge requires a shared identifier between the two accounts on EACH merge call. B needs
        // BOTH numbers at once -- a second PUT would REPLACE, not append to, additionalPhoneNumbers.
        var sharedNumberAB = NewMobile();
        var sharedNumberBSurvivor = NewMobile();
        await AddSharedAdditionalNumberAsync(clientA, sharedNumberAB);
        await AddSharedAdditionalNumbersAsync(clientB, sharedNumberAB, sharedNumberBSurvivor);
        await AddSharedAdditionalNumberAsync(survivorClient, sharedNumberBSurvivor);

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);

        var mergeAIntoB = await adminClient.PostAsJsonAsync($"/api/staff/customers/{customerA}/merge",
            new { survivingCustomerId = customerB, reason = "Same person, confirmed by phone" }, Json);
        Assert.Equal(HttpStatusCode.OK, mergeAIntoB.StatusCode);

        var mergeBIntoSurvivor = await adminClient.PostAsJsonAsync($"/api/staff/customers/{customerB}/merge",
            new { survivingCustomerId = survivorId, reason = "Same person, confirmed by phone" }, Json);
        Assert.Equal(HttpStatusCode.OK, mergeBIntoSurvivor.StatusCode);

        await using var scope = Factory.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<INotificationRecipientDirectory>();

        var resolvedFromA = await directory.ResolveAsync(NotificationRecipientKind.Customer, customerA);
        var resolvedFromB = await directory.ResolveAsync(NotificationRecipientKind.Customer, customerB);

        Assert.NotNull(resolvedFromA);
        Assert.Equal(survivorId, resolvedFromA!.Id);
        Assert.True(resolvedFromA.IsActive);
        Assert.NotNull(resolvedFromB);
        Assert.Equal(survivorId, resolvedFromB!.Id);
        Assert.True(resolvedFromB!.IsActive);
    }

    /// <summary>An anonymised customer resolves with <c>IsActive: false</c> -- the routing policy's
    /// own "recipient inactive" suppression (§8.6) depends on this being false, not merely absent.</summary>
    [Fact]
    public async Task ResolveAsync_AnonymisedCustomer_ResolvesInactive()
    {
        var (client, customerId, _) = await RegisterAndSignInCustomerAsync();

        var deletion = await client.PostAsJsonAsync("/api/customers/me/deletion", new { currentPassword = CustomerPassword }, Json);
        Assert.Equal(HttpStatusCode.NoContent, deletion.StatusCode);

        await using var scope = Factory.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<INotificationRecipientDirectory>();
        var resolved = await directory.ResolveAsync(NotificationRecipientKind.Customer, customerId);

        Assert.NotNull(resolved);
        Assert.False(resolved!.IsActive);
    }

    /// <summary>Neither a merge nor an anonymisation touches <c>FindMergedDuplicatesAsync</c>'s own
    /// reverse walk in isolation elsewhere -- proves it returns the duplicate for a direct
    /// (one-hop) merge, seeded the same way <c>ResolveAsync_TwoHopMergeChain...</c> above is.</summary>
    [Fact]
    public async Task FindMergedDuplicatesAsync_OneHopMerge_ReturnsTheDuplicate()
    {
        var (duplicateClient, duplicateId, _) = await RegisterAndSignInCustomerAsync();
        var (survivorClient, survivorId, _) = await RegisterAndSignInCustomerAsync();

        var sharedNumber = NewMobile();
        await AddSharedAdditionalNumberAsync(duplicateClient, sharedNumber);
        await AddSharedAdditionalNumberAsync(survivorClient, sharedNumber);

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var merge = await adminClient.PostAsJsonAsync($"/api/staff/customers/{duplicateId}/merge",
            new { survivingCustomerId = survivorId, reason = "Same person, confirmed by phone" }, Json);
        Assert.Equal(HttpStatusCode.OK, merge.StatusCode);

        await using var scope = Factory.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<INotificationRecipientDirectory>();
        var duplicates = await directory.FindMergedDuplicatesAsync(survivorId);

        Assert.Contains(duplicateId, duplicates);
    }

    /// <summary>Matches <c>NotificationLogAndInboxEndpointTests</c>'s own private helper of the same
    /// shape -- "Test Customer" matches <c>RegisterAndSignInCustomerAsync</c>'s hardcoded registration
    /// fullName.</summary>
    private async Task AddSharedAdditionalNumberAsync(HttpClient client, string sharedNumber) =>
        await AddSharedAdditionalNumbersAsync(client, sharedNumber);

    private async Task AddSharedAdditionalNumbersAsync(HttpClient client, params string[] numbers)
    {
        var update = await client.PutAsJsonAsync("/api/customers/me", new
        {
            fullName = "Test Customer", additionalPhoneNumbers = numbers, currentPassword = CustomerPassword
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
    }
}
