using System.Net;
using CatEcommerce.Infrastructure.Documents;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// TEST gate (2026-09-26). ARCHITECTURE/CODE_REVIEW gates, LOW (2026-09-26, second pass, closed):
/// <c>QuestPdfCatMedicalHistoryRenderer</c> now translates its own library's
/// <c>DocumentDrawingException</c> into the port-level <see cref="CatMedicalHistoryRenderException"/>,
/// and <c>CatsController</c> catches only that port-level type. No real PDF input reliably forces
/// that exception on demand (the Bengali-name regression test forces the underlying QuestPDF
/// exception specifically, which is a different, adapter-level thing to prove) -- this substitutes a
/// fake <see cref="ICatMedicalHistoryPdfRenderer"/> that always throws the port-level exception
/// directly, via <c>ConfigureTestServices</c>, the same technique
/// <c>AdministrationApiFactory.WithFakeTimeProvider</c>/<c>WithLoweredAuditExportCap</c> already use
/// in this codebase for a controlled test double. This proves the controller's own catch/log/500
/// path in isolation from QuestPDF entirely.
/// </summary>
public sealed class CatMedicalHistoryPdfRenderFailureTests(CatApiFactory factory) : CatIntegrationTestBase(factory)
{
    [Fact]
    public async Task GetCatMedicalHistoryPdf_PortLevelRenderExceptionFromRenderer_Returns500WithNoSensitiveDataLogged()
    {
        using var registerClient = CreateClient();
        var catId = await RegisterCatAsync(registerClient, name: "Render Failure Cat");

        var (loggingFactory, logs) = Factory.WithCapturedLogs();
        using var failingFactory = loggingFactory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddScoped<ICatMedicalHistoryPdfRenderer>(_ => new AlwaysFailingRenderer())));

        using var failingClient = failingFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"),
            HandleCookies = true
        });
        await SignInAsFreshAdministratorAsync(failingClient);

        var response = await failingClient.GetAsync($"/api/cats/{catId}/medical-history.pdf");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        // The controller returns a bare StatusCode(500); the host's own default ASP.NET Core
        // ProblemDetails behaviour fills in the generic RFC 9110 shape for it (observed live:
        // `{"type":"https://tools.ietf.org/html/rfc9110#section-15.6.1", "title":"An error
        // occurred while processing your request.", "status":500, ...}`) -- not something
        // CatsController opts into itself, and it carries no exception detail. The actual point
        // of this test is that NOTHING sensitive (the injected exception's message, its type
        // name, or a QuestPDF type) ever reaches the response body or the logs.
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("QuestPDF", body);
        Assert.DoesNotContain("DocumentDrawingException", body);
        Assert.DoesNotContain(nameof(AlwaysFailingRenderer), body);
        Assert.DoesNotContain("Injected test failure", body);

        // The controller's own catch logs the cat id only -- never the exception object, its
        // message, or any QuestPDF type name (SECURITY gate LOW: the underlying library's message
        // quotes offending free text verbatim; the port-level exception's own Message is generic,
        // but the controller must not log the exception object regardless of which type it is).
        var relevantLogs = logs.Where(l => l.Contains(catId.ToString())).ToList();
        Assert.NotEmpty(relevantLogs);
        Assert.All(relevantLogs, l =>
        {
            Assert.DoesNotContain("QuestPDF", l);
            Assert.DoesNotContain("DocumentDrawingException", l);
            Assert.DoesNotContain(nameof(AlwaysFailingRenderer), l);
        });
    }

    /// <summary>Always throws the port-level failure -- proves the controller depends only on the
    /// port's own contract (<see cref="ICatMedicalHistoryPdfRenderer"/>), never on QuestPDF.</summary>
    private sealed class AlwaysFailingRenderer : ICatMedicalHistoryPdfRenderer
    {
        public byte[] Render(CatMedicalHistoryDocument document) =>
            throw new CatMedicalHistoryRenderException(new InvalidOperationException("Injected test failure -- never a real QuestPDF exception."));
    }
}
