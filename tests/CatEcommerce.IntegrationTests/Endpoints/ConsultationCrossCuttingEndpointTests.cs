using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-EMR-001 §16 I-X1 (no clinical content in logs) and I-X2 (every clinical read is matched by an access audit row).</summary>
public sealed class ConsultationCrossCuttingEndpointTests(ConsultationApiFactory factory) : ConsultationIntegrationTestBase(factory)
{
    private sealed class CapturingLoggerProvider(ConcurrentBag<string> logs) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, logs);
        public void Dispose() { }

        private sealed class CapturingLogger(string categoryName, ConcurrentBag<string> logs) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                logs.Add($"[{logLevel}] {categoryName}: {formatter(state, exception)}" + (exception is null ? "" : $" | {exception}"));
        }
    }

    private static byte[] Pdf() => "%PDF-1.4\n%%EOF"u8.ToArray();

    private static async Task<int> PublishedFormVersionAsync(HttpClient admin)
    {
        if ((await admin.GetAsync("/api/staff/consent-forms/Surgery")).StatusCode != HttpStatusCode.OK)
        {
            await admin.PostAsJsonAsync("/api/admin/policies", new { key = "consent-surgery", title = "Consent - Surgery", requiresAcceptance = false }, Json);
            await admin.PostAsJsonAsync("/api/admin/policies/consent-surgery/versions", new { body = "I consent.", changeSummary = "Initial" }, Json);
        }

        return (await ReadAsync(await admin.GetAsync("/api/staff/consent-forms/Surgery"))).GetProperty("versionNumber").GetInt32();
    }

    /// <summary>I-X1 -- a whole clinical flow leaves no SOAP text, problem name, addendum text, file name, signature data or storage
    /// path in any log line, at any level (NFR-SEC-8, §8.11, ADR-130).</summary>
    [Fact]
    public async Task I_X1_NoClinicalContentReachesTheLogs()
    {
        var logs = new ConcurrentBag<string>();
        var world = await SeedClinicalWorldAsync(configureServices: services =>
            services.AddLogging(logging => logging.AddProvider(new CapturingLoggerProvider(logs))));
        var formVersion = await PublishedFormVersionAsync(world.Admin);

        const string soap = "ZZSOAP-subjective-secret";
        const string assessment = "ZZASSESS-secret";
        const string problemName = "ZZPROBLEM-name-secret";
        const string addendum = "ZZADDENDUM-text-secret";
        const string fileName = "zzfilename-secret.pdf";
        const string signedBy = "ZZSIGNER-name-secret";

        var visitId = await OpenVisitAsync(world.Nurse, world.AppointmentId);
        var code = await world.Vet.PostAsJsonAsync("/api/staff/diagnosis-codes", new { code = NewDiagnosisCode(), name = problemName }, Json);
        var codeId = (await ReadAsync(code)).GetProperty("diagnosisCodeId").GetGuid();

        await world.Nurse.PostAsJsonAsync($"/api/staff/visits/{visitId}/vitals", new { weightKg = 4.2m, note = "ZZVITALS-note-secret" }, Json);
        var version = (await ReadAsync(await world.Vet.GetAsync($"/api/staff/visits/{visitId}"))).GetProperty("version").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await world.Vet.PutAsJsonAsync($"/api/staff/visits/{visitId}/note", new
        {
            expectedVersion = version, subjective = soap, assessment, plan = "ZZPLAN-secret",
            problems = new[] { new { diagnosisCodeId = codeId, certainty = "Confirmed" } }
        }, Json)).StatusCode);
        await world.Vet.PutAsJsonAsync($"/api/staff/visits/{visitId}/draft", new { subjective = "ZZDRAFT-secret" }, Json);

        using var form = new MultipartFormDataContent("emr-" + Guid.NewGuid().ToString("N"));
        var part = new ByteArrayContent(Pdf());
        part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        form.Add(part, "file", fileName);
        var upload = await world.Vet.PostAsync("/api/staff/files?purpose=LabResult", form);
        var fileId = (await ReadAsync(upload)).GetProperty("fileId").GetGuid();
        var orderId = (await ReadAsync(await world.Vet.PostAsJsonAsync($"/api/staff/visits/{visitId}/diagnostics", new { kind = "BloodWork" }, Json))).GetProperty("diagnosticOrderId").GetGuid();
        await world.Vet.PostAsJsonAsync($"/api/staff/diagnostics/{orderId}/attachments", new { fileId, resultDate = DateOnly.FromDateTime(world.Clock.GetUtcNow().UtcDateTime), note = "ZZLABNOTE-secret" }, Json);

        var current = (await ReadAsync(await world.Vet.GetAsync($"/api/staff/visits/{visitId}"))).GetProperty("version").GetInt32();
        Assert.Equal(HttpStatusCode.NoContent, (await world.Vet.PostAsJsonAsync($"/api/staff/visits/{visitId}/finalise", new { expectedVersion = current }, Json)).StatusCode);
        await world.Vet.PostAsJsonAsync($"/api/staff/visits/{visitId}/addenda", new { text = addendum }, Json);

        await world.Reception.PostAsJsonAsync("/api/staff/consents", new
        {
            catId = world.CatId, appointmentId = world.AppointmentId, kind = "Surgery", procedureDescription = "ZZPROCEDURE-secret", method = "OnScreenSignature",
            signature = new[] { new[] { new[] { 0.1234m, 0.2345m }, new[] { 0.3456m, 0.4567m }, new[] { 0.5678m, 0.6789m }, new[] { 0.7891m, 0.8912m }, new[] { 0.9123m, 0.1357m }, new[] { 0.2468m, 0.3579m }, new[] { 0.4680m, 0.5791m }, new[] { 0.6802m, 0.7913m }, new[] { 0.8024m, 0.9135m }, new[] { 0.1246m, 0.3579m } } },
            signedByName = signedBy, signerRelationship = "Owner", policyVersionNumber = formVersion
        }, Json);

        // Reads: the visit, the history, the summary (JSON and PDF), the portal copy, the signed URL.
        await world.Vet.GetAsync($"/api/staff/visits/{visitId}");
        await world.Vet.GetAsync($"/api/staff/cats/{world.CatId}/clinical-history");
        await world.Vet.GetAsync($"/api/staff/cats/{world.CatId}/clinical-history/timeline");
        await world.Vet.GetAsync($"/api/staff/visits/{visitId}/discharge-summary");
        await world.Vet.GetAsync($"/api/staff/visits/{visitId}/discharge-summary.pdf");
        await world.Customer.GetAsync($"/api/customers/me/visits/{visitId}");
        var order = await ReadAsync(await world.Vet.GetAsync($"/api/staff/diagnostics/{orderId}"));
        await world.Vet.GetAsync(order.GetProperty("attachments")[0].GetProperty("file").GetProperty("url").GetString()!);

        Assert.NotEmpty(logs); // the capture works
        var content = logs.ToList();
        string[] secrets = [soap, assessment, problemName, addendum, fileName, signedBy, "ZZPLAN-secret", "ZZDRAFT-secret", "ZZVITALS-note-secret", "ZZLABNOTE-secret", "ZZPROCEDURE-secret",
            "0.1234", "0.2345", "0.5678", "0.9123", Factory.StoreRoot];

        foreach (var secret in secrets)
            Assert.DoesNotContain(content, line => line.Contains(secret, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>I-X2 -- every clinical GET in the suite's flows is matched by exactly one access row for the caller (NFR-SEC-8).</summary>
    [Fact]
    public async Task I_X2_EveryClinicalRead_IsMatchedByAnAccessRow()
    {
        var world = await SeedClinicalWorldAsync();
        var formVersion = await PublishedFormVersionAsync(world.Admin);
        var visitId = await OpenVisitAsync(world.Nurse, world.AppointmentId);
        var codeId = await CreateDiagnosisCodeAsync(world.Vet);
        var version = (await ReadAsync(await world.Vet.GetAsync($"/api/staff/visits/{visitId}"))).GetProperty("version").GetInt32();
        await world.Vet.PutAsJsonAsync($"/api/staff/visits/{visitId}/note", new
        { expectedVersion = version, assessment = "A", plan = "P", problems = new[] { new { diagnosisCodeId = codeId, certainty = "Confirmed" } } }, Json);
        var orderId = (await ReadAsync(await world.Vet.PostAsJsonAsync($"/api/staff/visits/{visitId}/diagnostics", new { kind = "BloodWork" }, Json))).GetProperty("diagnosticOrderId").GetGuid();
        var current = (await ReadAsync(await world.Vet.GetAsync($"/api/staff/visits/{visitId}"))).GetProperty("version").GetInt32();
        await world.Vet.PostAsJsonAsync($"/api/staff/visits/{visitId}/finalise", new { expectedVersion = current }, Json);
        var consentId = (await ReadAsync(await world.Reception.PostAsJsonAsync("/api/staff/consents", new
        {
            catId = world.CatId, appointmentId = world.AppointmentId, kind = "Surgery", procedureDescription = "Procedure", method = "OnScreenSignature",
            signature = new[] { new[] { new[] { 0.1m, 0.2m }, new[] { 0.2m, 0.3m }, new[] { 0.3m, 0.4m }, new[] { 0.4m, 0.5m }, new[] { 0.5m, 0.6m }, new[] { 0.6m, 0.7m }, new[] { 0.7m, 0.8m }, new[] { 0.8m, 0.9m }, new[] { 0.9m, 0.1m }, new[] { 0.2m, 0.4m } } },
            signedByName = "Signer", signerRelationship = "Owner", policyVersionNumber = formVersion
        }, Json))).GetProperty("consentId").GetGuid();

        var staffReads = new[]
        {
            $"/api/staff/visits/{visitId}", "/api/staff/visits", $"/api/staff/diagnostics/{orderId}", "/api/staff/diagnostics",
            $"/api/staff/cats/{world.CatId}/clinical-history", $"/api/staff/cats/{world.CatId}/clinical-history/visits",
            $"/api/staff/visits/{visitId}/discharge-summary", $"/api/staff/visits/{visitId}/discharge-summary.pdf",
            $"/api/staff/consents/{consentId}", $"/api/staff/cats/{world.CatId}/consents", $"/api/staff/consents/{consentId}/signature.svg",
        };

        var before = await CountAccessRowsAsync(world.VetStaffId);
        foreach (var route in staffReads)
            Assert.Equal(HttpStatusCode.OK, (await world.Vet.GetAsync(route)).StatusCode);

        Assert.Equal(before + staffReads.Length, await CountAccessRowsAsync(world.VetStaffId));

        var customerReads = new[]
        {
            $"/api/customers/me/cats/{world.CatId}/visits", $"/api/customers/me/visits/{visitId}",
            $"/api/customers/me/visits/{visitId}/discharge-summary.pdf", $"/api/customers/me/cats/{world.CatId}/lab-results",
        };

        var customerBefore = await CountAccessRowsAsync(world.CustomerId);
        foreach (var route in customerReads)
            Assert.Equal(HttpStatusCode.OK, (await world.Customer.GetAsync(route)).StatusCode);

        Assert.Equal(customerBefore + customerReads.Length, await CountAccessRowsAsync(world.CustomerId));
    }
}
