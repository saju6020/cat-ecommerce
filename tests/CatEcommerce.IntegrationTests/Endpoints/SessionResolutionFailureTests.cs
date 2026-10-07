using System.Net;
using System.Net.Http.Headers;
using System.Text;
using CatEcommerce.Read.Queries.Account;
using CatEcommerce.Read.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-ACC-001 ARCHITECTURE gate, second pass, HIGH: a database fault during session resolution
/// must never log the plaintext password (or any other part of the request body) of the request
/// it happened to occur on. <c>SessionAuthenticationMiddleware</c> dispatches
/// <c>GetCurrentCustomerSessionQuery</c> for any request carrying a <c>pc_session</c> cookie
/// against a session-bearing endpoint, and <c>Platform.Infrastructure.Core</c>'s
/// <c>QueryProcessor</c> propagates a query handler's exception rather than catching it. This test
/// forces exactly that failure and proves the resulting 500 carries no trace of the password, in
/// the response body or in the logs.
///
/// ADR-342 (owner decision A, 2026-09-25): sign-in itself no longer resolves any session at all --
/// it carries <c>[CsrfExempt]</c>, and the middleware now skips session resolution entirely for
/// that marker, so a stale or forged <c>pc_session</c> cookie on a sign-in request can no longer
/// trigger this code path (that is by design: sign-in must never depend on, or be blocked by,
/// whatever a pre-existing session cookie happens to resolve to). The scenario this test forces is
/// therefore reproduced instead on the email step-up (<c>PUT /api/customers/me</c>), which still
/// resolves the caller's session on every request and carries its own password
/// (<c>currentPassword</c>) in the body.
/// </summary>
public sealed class SessionResolutionFailureTests(AccountsApiFactory factory) : AccountsIntegrationTestBase(factory)
{
    private sealed class ThrowingCustomerSessionQueryHandler : IQueryHandlerAsync<GetCurrentCustomerSessionQuery, CustomerSessionViewModel?>
    {
        public Task<QueryResponse<CustomerSessionViewModel?>> HandleAsync(GetCurrentCustomerSessionQuery query) =>
            throw new InvalidOperationException("Simulated database fault during session resolution (test).");
    }

    [Fact]
    public async Task DatabaseFaultDuringSessionResolution_OnAStepUpRequestWithAStaleCookie_FailsClosedWithoutLoggingThePassword()
    {
        var (capturingFactory, logs) = Factory.WithCapturedLogs();
        var throwingFactory = capturingFactory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddScoped<IQueryHandlerAsync<GetCurrentCustomerSessionQuery, CustomerSessionViewModel?>, ThrowingCustomerSessionQueryHandler>()));

        using var client = throwingFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/")
        });

        const string password = "TotallySecretPlaintextPassword1!";
        // Content-Type set to EXACTLY "application/json" (no charset parameter) -- the platform's
        // GlobalExceptionHandlerMiddleware (now removed entirely, but this keeps the test a
        // faithful regression guard) matched only that exact string; JsonContent.Create's default
        // "application/json; charset=utf-8" would not have exercised the original vulnerability.
        var content = new StringContent(
            $$"""{"fullName":"Someone","emailAddress":"someone@example.test","additionalPhoneNumbers":[],"currentPassword":"{{password}}"}""",
            Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var request = new HttpRequestMessage(HttpMethod.Put, "/api/customers/me") { Content = content };
        // Any non-empty pc_session cookie makes SessionAuthenticationMiddleware dispatch
        // GetCurrentCustomerSessionQuery -- forged/stale is exactly the scenario. No real session
        // ever needs to exist; [RequireCustomerSession] never gets the chance to run, since the
        // simulated fault happens inside the middleware itself, ahead of MVC entirely.
        request.Headers.Add("Cookie", "pc_session=stale-or-forged-value");

        var response = await client.SendAsync(request);

        // Fails closed: a 500, not a 200 and not a crash that reaches the caller unhandled.
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(password, body, StringComparison.Ordinal);
        // ErrorVerbosity is unset in the committed appsettings.json -- SafeExceptionHandlerMiddleware
        // matches the platform handler's shape for that configuration: an empty body.
        Assert.Empty(body);

        Assert.DoesNotContain(logs, line => line.Contains(password, StringComparison.Ordinal));
    }

    private sealed class ThrowingResourceFilter : IResourceFilter
    {
        public void OnResourceExecuting(ResourceExecutingContext context) =>
            throw new InvalidOperationException("Simulated fault before model binding (test).");

        public void OnResourceExecuted(ResourceExecutedContext context) { }
    }

    // SECURITY gate LOW: an exception thrown BEFORE model binding (a resource filter, an
    // authorization filter, controller activation) on a password-bearing endpoint must not log the
    // plaintext password either. This was live against the first version of the HIGH fix: the
    // platform's GlobalExceptionHandlerMiddleware still ran nested inside UseHttpPipeline(),
    // wrapping UseCors/UseEndpoints, so it still caught (and could still log the body of) anything
    // MVC itself threw ahead of System.Text.Json consuming the request stream. Dropping
    // UseHttpPipeline() entirely removes that inner instance; SafeExceptionHandlerMiddleware is now
    // the only handler anywhere in the pipeline, for an exception from ANY position.
    [Fact]
    public async Task ThrowBeforeModelBinding_OnSignIn_DoesNotLogThePassword()
    {
        var (capturingFactory, logs) = Factory.WithCapturedLogs();
        var throwingFactory = capturingFactory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.Configure<MvcOptions>(o => o.Filters.Add(new ThrowingResourceFilter()))));

        using var client = throwingFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/")
        });

        const string password = "AnotherTotallySecretPassword2!";
        var content = new StringContent(
            $$"""{"identifier":"01700000000","password":"{{password}}"}""", Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var response = await client.PostAsync("/api/auth/sign-in", content);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(password, body, StringComparison.Ordinal);
        Assert.Empty(body);

        Assert.DoesNotContain(logs, line => line.Contains(password, StringComparison.Ordinal));
    }
}
