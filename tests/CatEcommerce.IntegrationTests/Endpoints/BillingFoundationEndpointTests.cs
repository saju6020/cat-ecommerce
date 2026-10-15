using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using CatEcommerce.Application.CommandHandlers.Billing;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Events;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-BIL-001 §16 group A (I-A2, I-A4) -- the host starts with scope validation and every Billing
/// registration resolves; the billing profile round-trips and its change is audited.</summary>
public sealed class BillingFoundationEndpointTests(BillingApiFactory factory) : BillingIntegrationTestBase(factory)
{
    /// <summary>I-A2 -- "count them": every Billing command handler, validator, query handler and event handler
    /// that exists in the code is registered and resolves in a scope (ValidateScopes/ValidateOnBuild are on).</summary>
    [Fact]
    public async Task I_A2_EveryBillingHandlerValidatorQueryHandlerAndEventHandler_IsRegisteredAndResolves()
    {
        using var client = CreateClient(); // forces the host to build with scope validation on

        var application = typeof(SetBillingProfileCommandHandler).Assembly;
        var read = typeof(CatEcommerce.Read.QueryHandlers.Billing.GetPaymentMethodsQueryHandler).Assembly;

        var problems = new List<string>();
        await using var scope = Factory.Services.CreateAsyncScope();

        foreach (var type in application.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false, Namespace: "CatEcommerce.Application.CommandHandlers.Billing" }))
        {
            foreach (var contract in type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommandHandlerAsync<>)))
            {
                if (scope.ServiceProvider.GetService(contract) is null) problems.Add("unregistered handler " + contract.Name + " for " + type.Name);

                var validator = typeof(IValidator<>).MakeGenericType(contract.GetGenericArguments()[0]);
                if (scope.ServiceProvider.GetService(validator) is null) problems.Add("no IValidator<" + contract.GetGenericArguments()[0].Name + ">");
            }
        }

        foreach (var type in read.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false, Namespace: "CatEcommerce.Read.QueryHandlers.Billing" }))
        {
            foreach (var contract in type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQueryHandlerAsync<,>)))
                if (scope.ServiceProvider.GetService(contract) is null) problems.Add("unregistered query handler " + contract.Name + " for " + type.Name);
        }

        foreach (var type in application.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false, Namespace: "CatEcommerce.Application.DomainEventHandlers.Billing" }))
        {
            foreach (var contract in type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandlerAsync<>)))
            {
                var all = scope.ServiceProvider.GetServices(contract).Select(s => s!.GetType());
                if (!all.Contains(type)) problems.Add("unregistered event handler " + type.Name + " for " + contract.GetGenericArguments()[0].Name);
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public async Task I_A4_TheBillingProfile_RoundTrips_AndItsChangeIsAudited()
    {
        var context = await CreateBillingContextAsync(withProfile: false);

        Assert.Equal(HttpStatusCode.NotFound, (await context.Admin.GetAsync($"/api/admin/billing-profiles/{context.BranchId}")).StatusCode);

        await SetProfileAsync(context, legalName: "Round Trip Ltd", taxNumber: "BIN-7");

        var profile = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await context.Admin.GetAsync($"/api/admin/billing-profiles/{context.BranchId}")));
        Assert.Equal("Round Trip Ltd", profile.GetProperty("legalName").GetString());
        Assert.Equal("BIN-7", profile.GetProperty("taxRegistrationNumber").GetString());
        Assert.Equal(2, profile.GetProperty("addressLines").GetArrayLength());
        var account = Assert.Single(profile.GetProperty("bankAccounts").EnumerateArray());
        Assert.Equal("1234567890", account.GetProperty("accountNumber").GetString());

        // A second set replaces the bank accounts wholesale.
        await ExpectAsync(HttpStatusCode.NoContent, await context.Admin.PutAsJsonAsync($"/api/admin/billing-profiles/{context.BranchId}", new
        {
            legalName = "Round Trip Ltd", taxRegistrationNumber = "BIN-7", addressLines = new[] { "Only line" }, bankAccounts = Array.Empty<object>(),
        }, Json));
        var second = await ReadAsync(await context.Admin.GetAsync($"/api/admin/billing-profiles/{context.BranchId}"));
        Assert.Empty(second.GetProperty("bankAccounts").EnumerateArray());

        var rows = await WithDbAsync(db => db.Set<AuditEntryAggregate>().AsNoTracking()
            .Where(a => a.EntityId == context.BranchId && a.EntityType == "BillingProfileAggregate").ToListAsync());
        Assert.Contains(rows, r => r.Category == AuditCategory.DataChange);
        Assert.True(rows.Count >= 2);
    }

    [Fact]
    public async Task APaymentMethodsEndpoint_ReturnsTheFlags_SoTheUiRendersFieldsFromData()
    {
        var context = await CreateBillingContextAsync();

        var methods = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await context.Admin.GetAsync("/api/billing/payment-methods")));

        Assert.Equal(4, methods.GetArrayLength());
        var card = methods.EnumerateArray().Single(m => m.GetProperty("name").GetString() == "CardInStore");
        Assert.True(card.GetProperty("requiresReference").GetBoolean());
        Assert.Equal("Terminal slip number", card.GetProperty("referenceLabel").GetString());
        Assert.False(card.GetProperty("isOnline").GetBoolean());
        var cod = methods.EnumerateArray().Single(m => m.GetProperty("name").GetString() == "CashOnDelivery");
        Assert.True(cod.GetProperty("requiresCollector").GetBoolean());
        Assert.Equal(2, cod.GetProperty("allowedRefundMethods").GetArrayLength());
    }

    [Fact]
    public async Task OnlyAnAdministrator_MaySetTheProfile_AndAnyBillingStaffMayRead()
    {
        var context = await CreateBillingContextAsync();
        var (reception, _) = await StaffClientAsync(context, CatEcommerce.Shared.Common.Constants.StaffRoles.Reception);

        var put = await reception.PutAsJsonAsync($"/api/admin/billing-profiles/{context.BranchId}", new
        {
            legalName = "X", taxRegistrationNumber = "Y", addressLines = new[] { "Z" }, bankAccounts = Array.Empty<object>(),
        }, Json);
        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await reception.GetAsync($"/api/admin/billing-profiles/{context.BranchId}")).StatusCode);
    }

    [Fact]
    public async Task ASetOnAnUnknownBranch_Is404()
    {
        var context = await CreateBillingContextAsync();

        var response = await context.Admin.PutAsJsonAsync($"/api/admin/billing-profiles/{Guid.NewGuid()}", new
        {
            legalName = "X", taxRegistrationNumber = "Y", addressLines = new[] { "Z" }, bankAccounts = Array.Empty<object>(),
        }, Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AnInvalidProfile_Is400WithTheValidationShape()
    {
        var context = await CreateBillingContextAsync();

        var response = await context.Admin.PutAsJsonAsync($"/api/admin/billing-profiles/{context.BranchId}", new
        {
            legalName = "", taxRegistrationNumber = "Y", addressLines = new[] { "Z" }, bankAccounts = Array.Empty<object>(),
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
