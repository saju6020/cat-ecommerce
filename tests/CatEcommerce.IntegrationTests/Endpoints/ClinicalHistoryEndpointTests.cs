using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-EMR-001 §16 Phase E -- I-E1, I-E2, I-E3, I-E5 and the photograph's place on the timeline (I-D10, US-CLN-19).
/// The 10-year budget (I-E4) is <c>ClinicalHistoryPerformanceTests</c>.</summary>
public sealed class ClinicalHistoryEndpointTests(ConsultationApiFactory factory) : ConsultationIntegrationTestBase(factory)
{
    private static byte[] Png() =>
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, 0xDE
    ];

    /// <summary>A finalised visit with a coded problem, a lab result, a weight and a photograph, plus a vaccination and a
    /// parasite treatment on the cat -- every built section has something to show.</summary>
    private async Task<(ClinicalWorld World, Guid VisitId)> WorldWithHistoryAsync()
    {
        var world = await SeedClinicalWorldAsync();
        var today = DateOnly.FromDateTime(world.Clock.GetUtcNow().UtcDateTime);

        await using (var scope = world.Api.Services.CreateAsyncScope())
        {
            var cats = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CatAggregate>>();
            var cat = await cats.GetByIdAsync(world.CatId);
            var user = new UserContext(world.VetStaffId, Guid.NewGuid());
            cat.RecordVaccination(new CatVaccinationDto(
                Guid.NewGuid(), Guid.NewGuid(), "FVRCP", "FVRCP booster", true, 12, "LOT-1", today.AddYears(1), today.AddDays(-30),
                world.VetStaffId, null, today, user, Guid.NewGuid()));
            cat.RecordParasiteTreatment(new CatParasiteTreatmentDto(
                Guid.NewGuid(), ParasiteTreatmentType.Deworming, "Drontal", null, today.AddDays(-10), world.VetStaffId, 90, null, today, user, Guid.NewGuid()));
            await cats.UpdateAsync(cat, EventPublishOption.InMemoryPublishOnly);
        }

        var visitId = await OpenVisitAsync(world.Nurse, world.AppointmentId);
        Assert.Equal(HttpStatusCode.Created, (await world.Nurse.PostAsJsonAsync($"/api/staff/visits/{visitId}/vitals", new { weightKg = 4.2m, temperatureC = 38.5m }, Json)).StatusCode);

        var codeId = await CreateDiagnosisCodeAsync(world.Vet);
        var version = (await ReadAsync(await world.Vet.GetAsync($"/api/staff/visits/{visitId}"))).GetProperty("version").GetInt32();
        var save = await world.Vet.PutAsJsonAsync($"/api/staff/visits/{visitId}/note", new
        {
            expectedVersion = version, assessment = "Otitis", plan = "Drops", problems = new[] { new { diagnosisCodeId = codeId, certainty = "Confirmed" } }
        }, Json);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        var order = await world.Vet.PostAsJsonAsync($"/api/staff/visits/{visitId}/diagnostics", new { kind = "BloodWork" }, Json);
        var orderId = (await ReadAsync(order)).GetProperty("diagnosticOrderId").GetGuid();
        Assert.Equal(HttpStatusCode.Created, (await world.Vet.PostAsJsonAsync($"/api/staff/diagnostics/{orderId}/results", new
        {
            reportedOn = today,
            values = new[] { new { name = "Creatinine", valueKind = "Numeric", numericValue = 2.4m, unit = "mg/dL", referenceLow = 0.8m, referenceHigh = 1.8m } }
        }, Json)).StatusCode);

        using var form = new MultipartFormDataContent("emr-" + Guid.NewGuid().ToString("N"));
        var part = new ByteArrayContent(Png());
        part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        form.Add(part, "file", "ear.png");
        var upload = await world.Vet.PostAsync("/api/staff/files?purpose=ClinicalPhoto", form);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var fileId = (await ReadAsync(upload)).GetProperty("fileId").GetGuid();
        Assert.Equal(HttpStatusCode.Created, (await world.Vet.PostAsJsonAsync($"/api/staff/visits/{visitId}/photos", new { fileId, takenOn = today, note = "Left ear" }, Json)).StatusCode);

        var finalise = await world.Vet.PostAsJsonAsync($"/api/staff/visits/{visitId}/finalise",
            new { expectedVersion = (await ReadAsync(await world.Vet.GetAsync($"/api/staff/visits/{visitId}"))).GetProperty("version").GetInt32() }, Json);
        Assert.Equal(HttpStatusCode.NoContent, finalise.StatusCode);
        return (world, visitId);
    }

    /// <summary>I-E1 -- the summary declares every section; the three unbuilt ones report themselves unavailable with their owner (US-CLN-13 AC1).</summary>
    [Fact]
    public async Task I_E1_TheSummaryDeclaresEverySection_AndTheUnbuiltOnesNameTheirOwner()
    {
        var (world, visitId) = await WorldWithHistoryAsync();

        var response = await world.Vet.GetAsync($"/api/staff/cats/{world.CatId}/clinical-history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await ReadAsync(response);
        Assert.Equal("Mitu", summary.GetProperty("cat").GetProperty("name").GetString());

        var sections = summary.GetProperty("sections").EnumerateArray().ToDictionary(s => s.GetProperty("key").GetString()!);
        Assert.Equal(ClinicalHistorySections.All.Select(s => s.Key).Order(), sections.Keys.Order());
        foreach (var built in new[] { "timeline", "visits", "diagnoses", "lab-results", "photos", "vaccinations", "parasite-treatments", "weights" })
        {
            Assert.True(sections[built].GetProperty("available").GetBoolean(), built);
            Assert.True(sections[built].GetProperty("count").GetInt32() >= 1, built);
        }

        Assert.False(sections["prescriptions"].GetProperty("available").GetBoolean());
        Assert.Equal("HO-PHM-001", sections["prescriptions"].GetProperty("owner").GetString());
        Assert.Equal("HO-BRD-001", sections["boarding-stays"].GetProperty("owner").GetString());
        Assert.Equal("HO-ORD-001", sections["food-purchases"].GetProperty("owner").GetString());

        Assert.Equal(1, summary.GetProperty("activeProblems").GetArrayLength());
        Assert.Equal(visitId, Assert.Single(summary.GetProperty("recentVisits").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal("38.5", summary.GetProperty("lastVitals").GetProperty("temperature").GetProperty("value").GetString());
        Assert.Equal(4.2m, summary.GetProperty("weightSeries")[0].GetProperty("kilograms").GetDecimal());
        Assert.Equal(JsonValueKind.Object, summary.GetProperty("vaccinationStatus").ValueKind);
        Assert.Equal(0, summary.GetProperty("pendingDiagnostics").GetArrayLength()); // the only order has results
    }

    /// <summary>I-E2 -- a section is paged newest first; an unbuilt one answers <c>available: false</c>; an unknown one is 404.</summary>
    [Fact]
    public async Task I_E2_SectionsArePaged_NewestFirst_AndTheTimelineMergesEveryBuiltSource()
    {
        var (world, visitId) = await WorldWithHistoryAsync();

        var visits = await ReadAsync(await world.Vet.GetAsync($"/api/staff/cats/{world.CatId}/clinical-history/visits?pageNumber=1&pageSize=20"));
        Assert.True(visits.GetProperty("available").GetBoolean());
        var visit = Assert.Single(visits.GetProperty("items").EnumerateArray());
        Assert.Equal(visitId, visit.GetProperty("id").GetGuid());
        Assert.Equal("Finalised", visit.GetProperty("fields").GetProperty("status").GetString());
        Assert.Equal("0", visit.GetProperty("fields").GetProperty("addendumCount").GetString());

        var timeline = await ReadAsync(await world.Vet.GetAsync($"/api/staff/cats/{world.CatId}/clinical-history/timeline?pageNumber=1&pageSize=20"));
        var kinds = timeline.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("kind").GetString()).ToHashSet();
        Assert.Superset(new HashSet<string?> { "visit", "photo", "lab-result", "vaccination", "parasite-treatment", "weight" }, kinds);
        var dates = timeline.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("occurredOn").GetDateTimeOffset()).ToList();
        Assert.Equal(dates.OrderByDescending(d => d), dates);

        // Paging the timeline in twos covers every item exactly once.
        var total = timeline.GetProperty("totalCount").GetInt32();
        var seen = new List<Guid>();
        for (var page = 1; seen.Count < total; page++)
        {
            var slice = await ReadAsync(await world.Vet.GetAsync($"/api/staff/cats/{world.CatId}/clinical-history/timeline?pageNumber={page}&pageSize=2"));
            seen.AddRange(slice.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()));
            Assert.True(page <= total, "paging did not terminate");
        }
        Assert.Equal(total, seen.Distinct().Count());

        var unavailable = await ReadAsync(await world.Vet.GetAsync($"/api/staff/cats/{world.CatId}/clinical-history/prescriptions"));
        Assert.False(unavailable.GetProperty("available").GetBoolean());
        Assert.Equal("HO-PHM-001", unavailable.GetProperty("owner").GetString());
        Assert.Equal(0, unavailable.GetProperty("items").GetArrayLength());

        Assert.Equal(HttpStatusCode.NotFound, (await world.Vet.GetAsync($"/api/staff/cats/{world.CatId}/clinical-history/nonsense")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await world.Vet.GetAsync($"/api/staff/cats/{Guid.NewGuid()}/clinical-history")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Reception.GetAsync($"/api/staff/cats/{world.CatId}/clinical-history")).StatusCode);

        var diagnoses = await ReadAsync(await world.Vet.GetAsync($"/api/staff/cats/{world.CatId}/clinical-history/diagnoses"));
        Assert.Equal("Confirmed", Assert.Single(diagnoses.GetProperty("items").EnumerateArray()).GetProperty("fields").GetProperty("certainty").GetString());
        var labs = await ReadAsync(await world.Vet.GetAsync($"/api/staff/cats/{world.CatId}/clinical-history/lab-results"));
        var lab = Assert.Single(labs.GetProperty("items").EnumerateArray());
        Assert.Equal("High:1", lab.GetProperty("fields").GetProperty("flags").GetString());
        Assert.Equal("true", lab.GetProperty("fields").GetProperty("hasAbnormal").GetString());
    }

    /// <summary>I-D10 -- the photograph appears on the cat's clinical timeline at its date, with a short-lived signed URL (US-CLN-19).</summary>
    [Fact]
    public async Task I_D10_ThePhotographAppearsOnTheTimelineAtItsDate()
    {
        var (world, _) = await WorldWithHistoryAsync();
        var today = DateOnly.FromDateTime(world.Clock.GetUtcNow().UtcDateTime);

        var timeline = await ReadAsync(await world.Vet.GetAsync($"/api/staff/cats/{world.CatId}/clinical-history/timeline?pageSize=50"));
        var photo = timeline.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("kind").GetString() == "photo");

        Assert.Equal("Left ear", photo.GetProperty("detail").GetString());
        Assert.Equal(today.ToString("yyyy-MM-dd"), photo.GetProperty("fields").GetProperty("takenOn").GetString());
        Assert.Equal(HttpStatusCode.OK, (await world.Vet.GetAsync(photo.GetProperty("file").GetProperty("url").GetString()!)).StatusCode);
    }

    /// <summary>I-E3 -- each history call writes one access row naming the actor and the cat (NFR-SEC-8); a refused call writes none.</summary>
    [Fact]
    public async Task I_E3_EachHistoryCall_IsLoggedOnce_WithTheActorAndTheCat()
    {
        var world = await SeedClinicalWorldAsync();
        var before = await CountAuditRowsAsync(AuditCategory.ClinicalRecordAccess, nameof(ClinicalRecordKind.ClinicalHistory), world.CatId);

        Assert.Equal(HttpStatusCode.OK, (await world.Vet.GetAsync($"/api/staff/cats/{world.CatId}/clinical-history")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await world.Nurse.GetAsync($"/api/staff/cats/{world.CatId}/clinical-history/visits")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Reception.GetAsync($"/api/staff/cats/{world.CatId}/clinical-history")).StatusCode);

        Assert.Equal(before + 2, await CountAuditRowsAsync(AuditCategory.ClinicalRecordAccess, nameof(ClinicalRecordKind.ClinicalHistory), world.CatId));
        Assert.Equal(1, await CountAuditRowsAsync(AuditCategory.ClinicalRecordAccess, nameof(ClinicalRecordKind.ClinicalHistory), world.CatId, world.VetStaffId));
        Assert.Equal(1, await CountAuditRowsAsync(AuditCategory.ClinicalRecordAccess, nameof(ClinicalRecordKind.ClinicalHistory), world.CatId, world.NurseStaffId));
    }

    /// <summary>I-E5 -- when the access log cannot be written the read does not happen: <c>500</c>, and no clinical data in the body (ADR-129).</summary>
    [Fact]
    public async Task I_E5_WhenTheAccessLogFails_TheReadIsRefused_AndNoClinicalDataIsReturned()
    {
        var world = await SeedClinicalWorldAsync(configureServices: services =>
            services.AddScoped<ICommandHandlerAsync<RecordClinicalRecordAccessCommand>, FailingAccessLogHandler>());
        var visitId = await OpenVisitAsync(world.Nurse, world.AppointmentId);

        var history = await world.Vet.GetAsync($"/api/staff/cats/{world.CatId}/clinical-history");
        var visit = await world.Vet.GetAsync($"/api/staff/visits/{visitId}");

        Assert.Equal(HttpStatusCode.InternalServerError, history.StatusCode);
        Assert.Equal(HttpStatusCode.InternalServerError, visit.StatusCode);
        foreach (var body in new[] { await history.Content.ReadAsStringAsync(), await visit.Content.ReadAsStringAsync() })
        {
            Assert.DoesNotContain("Mitu", body);
            Assert.DoesNotContain("allowedActions", body);
            Assert.DoesNotContain(world.CatId.ToString(), body);
        }
    }

    private sealed class FailingAccessLogHandler : ICommandHandlerAsync<RecordClinicalRecordAccessCommand>
    {
        public Task<CommandResponse> HandleAsync(RecordClinicalRecordAccessCommand command) =>
            throw new InvalidOperationException("The access log is unavailable (test hook).");
    }
}
