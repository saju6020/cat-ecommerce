using System.Net;
using System.Net.Http.Json;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-XCT-001 U-B7 -- the contract every <see cref="IPersonalDataEraser"/> meets (section 8.8), run against EVERY eraser registered in the host:
/// idempotent; <c>Completed</c> (never a throw) for a customer it has no data for; never <c>Failed</c> for an anonymised customer; and it logs
/// no personal value. A new context that registers an eraser is covered automatically.
/// </summary>
[Collection("XctRetention")]
public sealed class XctErasureContractTests(XctApiFactory factory) : XctIntegrationTestBase(factory)
{
    private static UserContext Worker => new(SystemActors.RetentionWorker, Guid.Empty);

    [Fact]
    public async Task U_B7_EveryRegisteredEraser_IsIdempotent_CompletedForAnUnknownCustomer_NeverFailsForAnErasedOne_AndLogsNoPersonalValue()
    {
        var (logged, logs) = Factory.WithCapturedLogs();
        var (admin, _) = await SignedInStaffAsync(StaffRoles.Administrator, logged);
        var (customer, customerId, mobile, password) = await RegisterAndSignInCustomerAsync(logged);
        await SeedCatAsync(customerId, "Contract Cat", logged);

        var key = "xct-" + Guid.NewGuid().ToString("N")[..12];
        await admin.PostAsJsonAsync("/api/admin/policies", new { key, title = "Terms", requiresAcceptance = true }, Json);
        await admin.PostAsJsonAsync($"/api/admin/policies/{key}/versions", new { body = "v1", changeSummary = "Initial" }, Json);
        await customer.PostAsJsonAsync("/api/customers/me/policy-acceptances", new { policyKey = key, versionNumber = 1 }, Json);
        Assert.Equal(HttpStatusCode.NoContent, (await customer.PostAsJsonAsync("/api/customers/me/deletion", new { currentPassword = password }, Json)).StatusCode);

        await using var scope = logged.Services.CreateAsyncScope();
        var erasers = scope.ServiceProvider.GetServices<IPersonalDataEraser>().ToList();
        Assert.NotEmpty(erasers);
        var asOf = DateTimeOffset.UtcNow;

        foreach (var eraser in erasers)
        {
            // 4. Never throws for a customer it has no data for: Completed.
            Assert.IsType<ErasureOutcome.Completed>(await eraser.EraseAsync(Guid.NewGuid(), asOf, Worker, CancellationToken.None));

            // 2 + 5. Erases what it may, never answers Failed for an anonymised customer ...
            var first = await eraser.EraseAsync(customerId, asOf, Worker, CancellationToken.None);
            Assert.False(first is ErasureOutcome.Failed, $"{eraser.ContextKey} failed for an anonymised customer: {(first as ErasureOutcome.Failed)?.ErrorCode}");

            // 1. ... and a second call after the first is the same answer, writing nothing new.
            var second = await eraser.EraseAsync(customerId, asOf, Worker, CancellationToken.None);
            Assert.Equal(first.GetType(), second.GetType());
            if (first is ErasureOutcome.Held heldFirst)
                Assert.Equal(heldFirst.Until, Assert.IsType<ErasureOutcome.Held>(second).Until);
        }

        // 6. Logs ids and codes only.
        Assert.DoesNotContain(logs, line => line.Contains(mobile, StringComparison.Ordinal));
        Assert.DoesNotContain(logs, line => line.Contains("Test Customer", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EveryRegisteredEraser_HasAStableNonEmptyKeyThatFitsAScope()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var keys = scope.ServiceProvider.GetServices<IPersonalDataEraser>().Select(e => e.ContextKey).ToList();

        Assert.All(keys, key => Assert.InRange(key.Length, 1, CatEcommerce.Domain.Entities.ErasureScope.MaxContextKeyLength));
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
    }
}
