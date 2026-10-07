using System.Net;
using System.Net.Http.Json;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// CODE_REVIEW gate, MEDIUM (2026-09-26, third pass): a customer id that exists but is
/// <c>Merged</c> or <c>Anonymised</c> passed the existence-only check RegisterCatCommandHandler/
/// TransferCatOwnershipCommandHandler used to have -- `EfAggregateRootRepository.GetByIdAsync`
/// only excludes a hard-deleted row, and neither status marks a row deleted. Both handlers now
/// also require <c>owner.Status == CustomerAccountStatus.Active</c>, reusing
/// <c>BusinessViolationCodes.AccountNotActive</c> (the same code <c>MergeCustomerCommandHandler</c>
/// already returns for an identical "record exists, not active" check on its own surviving
/// customer) rather than <c>CustomerNotFound</c> (the record does exist) or a new code.
///
/// Kept in its own file (not <c>CatsPaginationEndpointTests.cs</c>/other existing Cat test files)
/// to avoid colliding with the TEST gate's own concurrent unknown-id/staff-id test additions in
/// this same worktree.
/// </summary>
public sealed class CatOwnerCustomerStatusEndpointTests(CatApiFactory factory) : CatIntegrationTestBase(factory)
{
    [Fact]
    public async Task RegisterCat_WithMergedOwnerId_Returns400AccountNotActive()
    {
        var mergedOwnerId = await SeedMergedCustomerAsync();

        using var client = CreateClient();
        await SignInAsFreshAdministratorAsync(client);

        var response = await client.PostAsJsonAsync("/api/cats", new
        {
            ownerId = mergedOwnerId,
            name = "Planted On Merged Owner",
            sex = "Female",
            dateOfBirthPrecision = "Estimated"
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertAccountNotActiveAsync(response, "OwnerId");
    }

    [Fact]
    public async Task RegisterCat_WithAnonymisedOwnerId_Returns400AccountNotActive()
    {
        var anonymisedOwnerId = await SeedAnonymisedCustomerAsync();

        using var client = CreateClient();
        await SignInAsFreshAdministratorAsync(client);

        var response = await client.PostAsJsonAsync("/api/cats", new
        {
            ownerId = anonymisedOwnerId,
            name = "Planted On Anonymised Owner",
            sex = "Female",
            dateOfBirthPrecision = "Estimated"
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertAccountNotActiveAsync(response, "OwnerId");
    }

    [Fact]
    public async Task TransferCatOwnership_ToMergedCustomer_Returns400AccountNotActive()
    {
        using var client = CreateClient();
        var catId = await RegisterCatAsync(client);
        var mergedNewOwnerId = await SeedMergedCustomerAsync();

        var response = await client.PostAsJsonAsync($"/api/cats/{catId}/ownership-transfer", new
        {
            newOwnerId = mergedNewOwnerId,
            reason = "attempted transfer to a merged-away customer"
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertAccountNotActiveAsync(response, "NewOwnerId");
    }

    [Fact]
    public async Task TransferCatOwnership_ToAnonymisedCustomer_Returns400AccountNotActive()
    {
        using var client = CreateClient();
        var catId = await RegisterCatAsync(client);
        var anonymisedNewOwnerId = await SeedAnonymisedCustomerAsync();

        var response = await client.PostAsJsonAsync($"/api/cats/{catId}/ownership-transfer", new
        {
            newOwnerId = anonymisedNewOwnerId,
            reason = "attempted transfer to an anonymised customer"
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertAccountNotActiveAsync(response, "NewOwnerId");
    }

    private static async Task AssertAccountNotActiveAsync(HttpResponseMessage response, string expectedPropertyName)
    {
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Json);
        var errors = body.GetProperty("errors").EnumerateArray().ToList();
        Assert.Contains(errors, e => e.GetProperty("errorCode").GetString() == "AccountNotActive"
            && e.GetProperty("propertyName").GetString() == expectedPropertyName);
    }

    /// <summary>Seeds a customer, then merges it into an arbitrary (never separately seeded)
    /// surviving id -- MergeInto only needs a shared mobile/email to accept the pair as duplicates,
    /// which is trivially true here since the survivor's mobile is the duplicate's own. The
    /// resulting row is Status == Merged, present (not IsMarkedToDelete), exactly the case the
    /// handler-level existence check alone used to miss.</summary>
    private async Task<Guid> SeedMergedCustomerAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CustomerAccountAggregate>>();

        var duplicate = new CustomerAccountAggregate(Guid.NewGuid());
        var mobile = MobileNumber.Create($"019{Random.Shared.Next(10000000, 99999999)}");
        duplicate.RegisterWalkIn(new WalkInCustomerRegistrationDto(
            "Merged-Away Test Customer", mobile,
            new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.SaveAsync(duplicate, EventPublishOption.InMemoryPublishOnly);

        duplicate.MergeInto(new CustomerMergeDto(
            SurvivingCustomerId: Guid.NewGuid(),
            Reason: "Test setup: seeding a Merged customer for the AccountNotActive check.",
            SurvivorMobile: mobile,
            SurvivorAdditionalPhoneNumbers: [],
            SurvivorEmail: null,
            MergedOn: DateTimeOffset.UtcNow,
            UserContext: new UserContext(Guid.NewGuid(), Guid.NewGuid()),
            CorrelationId: Guid.NewGuid()));
        await repository.UpdateAsync(duplicate, EventPublishOption.InMemoryPublishOnly);

        return duplicate.Id;
    }

    /// <summary>Seeds a customer, then anonymises it -- Status == Anonymised, present (not
    /// IsMarkedToDelete), the other case the handler-level existence check alone used to miss.</summary>
    private async Task<Guid> SeedAnonymisedCustomerAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CustomerAccountAggregate>>();

        var customer = new CustomerAccountAggregate(Guid.NewGuid());
        var mobile = MobileNumber.Create($"019{Random.Shared.Next(10000000, 99999999)}");
        customer.RegisterWalkIn(new WalkInCustomerRegistrationDto(
            "Anonymised Test Customer", mobile,
            new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.SaveAsync(customer, EventPublishOption.InMemoryPublishOnly);

        customer.Anonymise(new CustomerAnonymisationDto(
            DateTimeOffset.UtcNow, new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.UpdateAsync(customer, EventPublishOption.InMemoryPublishOnly);

        return customer.Id;
    }
}
