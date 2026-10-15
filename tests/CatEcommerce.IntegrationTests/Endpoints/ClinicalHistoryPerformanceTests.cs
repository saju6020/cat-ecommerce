using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Xunit;
using Xunit.Abstractions;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-EMR-001 §16 I-E4 / NFR-PRF-4 / ADR-128 -- the server's share of the history budget, <b>measured, not estimated</b>, against
/// the 10-year fixture of §16.1 rule 4: 60 visits x (3 problems, 2 vitals, 1 photo), 250 weights, 40 vaccinations, 40 parasite
/// treatments, 80 diagnostic orders x 12 values, all on one cat. (The 3 treatments per visit of the handover's fixture
/// belong to Phase F and are not built.) The fixture is built through the aggregates and repositories, not HTTP; the
/// measured calls go over real HTTP, so the access-log write is inside every measurement.
///
/// p95 of 50 summary calls must be at most 600 ms and of 50 section pages (20 items) at most 300 ms. This is a timing test:
/// under heavy load on a shared machine it can flake -- rerun it alone (<c>--filter ClinicalHistoryPerformanceTests</c>).
/// </summary>
public sealed class ClinicalHistoryPerformanceTests(ConsultationApiFactory factory, ITestOutputHelper output) : ConsultationIntegrationTestBase(factory)
{
    private const int Visits = 60;
    private const int Weights = 250;
    private const int Vaccinations = 40;
    private const int ParasiteTreatments = 40;
    private const int Orders = 80;
    private const int ValuesPerOrder = 12;

    private static readonly string[] Sections = ["timeline", "visits", "diagnoses", "lab-results", "photos", "vaccinations", "parasite-treatments", "weights"];

    private async Task<Guid> BuildTenYearCatAsync(ClinicalWorld world)
    {
        var now = world.Clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var user = new UserContext(world.VetStaffId, Guid.NewGuid());
        var catId = Guid.NewGuid();

        await using var scope = world.Api.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var cat = new CatAggregate(catId);
        cat.Register(new CatRegistrationDto(world.CustomerId, "Methuselah", "Cat", CatSex.Male, today.AddYears(-11), DateOfBirthPrecision.Exact, "Domestic Shorthair", "Grey", today, user, Guid.NewGuid()));
        for (var i = 0; i < Vaccinations; i++)
            cat.RecordVaccination(new CatVaccinationDto(
                Guid.NewGuid(), Guid.NewGuid(), "FVRCP", "FVRCP booster", true, 12, "LOT-" + i, today.AddYears(1), today.AddDays(-(i * 90) - 5),
                world.VetStaffId, null, today, user, Guid.NewGuid()));
        for (var i = 0; i < ParasiteTreatments; i++)
            cat.RecordParasiteTreatment(new CatParasiteTreatmentDto(
                Guid.NewGuid(), ParasiteTreatmentType.Deworming, "Drontal", null, today.AddDays(-(i * 90) - 7), world.VetStaffId, 90, null, today, user, Guid.NewGuid()));
        await sp.GetRequiredService<IAggregateRootRepository<CatAggregate>>().SaveAsync(cat, EventPublishOption.InMemoryPublishOnly);

        var weightRepository = sp.GetRequiredService<IAggregateRootRepository<CatWeightRecordAggregate>>();
        for (var i = 0; i < Weights; i++)
        {
            var weight = new CatWeightRecordAggregate(Guid.NewGuid());
            weight.Record(new CatWeightRecordDto(catId, 3.5m + (i % 20) / 10m, today.AddDays(-(i * 14)), world.VetStaffId, null, today, user, Guid.NewGuid()));
            await weightRepository.SaveAsync(weight, EventPublishOption.InMemoryPublishOnly);
        }

        var codeRepository = sp.GetRequiredService<IAggregateRootRepository<DiagnosisCodeAggregate>>();
        var codes = new List<DiagnosisCodeAggregate>();
        for (var i = 0; i < 6; i++)
        {
            var code = new DiagnosisCodeAggregate(Guid.NewGuid());
            code.Create(new DiagnosisCodeCreateDto("P-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(), "Problem " + i, null, null, null, user, Guid.NewGuid()));
            await codeRepository.SaveAsync(code, EventPublishOption.InMemoryPublishOnly);
            codes.Add(code);
        }

        var visitRepository = sp.GetRequiredService<IAggregateRootRepository<VisitAggregate>>();
        var visits = new List<VisitAggregate>();
        for (var i = 0; i < Visits; i++)
        {
            var openedOn = now.AddDays(-(i * 60) - 1);
            var visit = new VisitAggregate(Guid.NewGuid());
            visit.Open(new VisitOpeningDto(world.BranchId, Guid.NewGuid(), catId, AppointmentStatus.CheckedIn, CatLifeStatus.Alive, world.ServiceId,
                "Consultation", ServiceCategory.Consultation, openedOn, user, Guid.NewGuid()));
            for (var v = 0; v < 2; v++)
                visit.RecordVitals(new VitalsDto(Guid.NewGuid(), openedOn.AddMinutes(v), null, 38.4m + v / 10m, 150 + v, 30, 5, 1, [], null, world.NurseStaffId, openedOn, user, Guid.NewGuid()));
            visit.SaveNote(new SoapNoteDto("S", "O", "Assessment " + i, "Plan " + i, "Home care", codes.Skip(i % 4).Take(3)
                .Select(c => new ProblemDto(Guid.NewGuid(), c.Id, c.Code, c.Name, true, ProblemCertainty.Confirmed, null)).ToList(),
                null, null, world.VetStaffId, openedOn, user, Guid.NewGuid()));
            visit.AttachPhoto(new ClinicalPhotoDto(Guid.NewGuid(), Guid.NewGuid(), DateOnly.FromDateTime(openedOn.UtcDateTime), "photo", world.VetStaffId,
                DateOnly.FromDateTime(openedOn.UtcDateTime), openedOn, user, Guid.NewGuid()));
            await visitRepository.SaveAsync(visit, EventPublishOption.InMemoryPublishOnly);
            visits.Add(visit);
        }

        // Finalised in a second commit: the immutability triggers refuse a child row inserted under an already-finalised visit.
        foreach (var visit in visits)
        {
            visit.Finalise(new VisitFinalisationDto([Shared.Common.Constants.StaffRoles.Vet], world.VetStaffId, visit.OpenedOn.AddMinutes(30), user, Guid.NewGuid(), Guid.NewGuid()));
            await visitRepository.UpdateAsync(visit, EventPublishOption.InMemoryPublishOnly);
        }

        var orderRepository = sp.GetRequiredService<IAggregateRootRepository<DiagnosticOrderAggregate>>();
        for (var i = 0; i < Orders; i++)
        {
            var visit = visits[i % Visits];
            var order = new DiagnosticOrderAggregate(Guid.NewGuid());
            order.Order(new DiagnosticOrderDto(visit.Id, catId, world.BranchId, VisitStatus.Open, DiagnosticKind.BloodWork, null, [], world.VetStaffId, visit.OpenedOn.AddMinutes(10), user, Guid.NewGuid()));
            order.RecordResults(new DiagnosticResultSetDto(
                Guid.NewGuid(), DateOnly.FromDateTime(visit.OpenedOn.UtcDateTime), null, null,
                Enumerable.Range(0, ValuesPerOrder).Select(v => new ResultValueDto(
                    Guid.NewGuid(), null, "Analyte " + v, ResultValueKind.Numeric, 5m + v, null, "u", 4m, 12m, null)).ToList(),
                world.VetStaffId, DateOnly.FromDateTime(visit.OpenedOn.UtcDateTime), visit.OpenedOn.AddDays(2), user, Guid.NewGuid()));
            await orderRepository.SaveAsync(order, EventPublishOption.InMemoryPublishOnly);
        }

        return catId;
    }

    private static double Percentile95(List<double> milliseconds)
    {
        milliseconds.Sort();
        return milliseconds[(int)Math.Ceiling(milliseconds.Count * 0.95) - 1];
    }

    [Fact]
    public async Task I_E4_TenYearHistory_ServerBudget_600msSummary_300msSectionPage()
    {
        var world = await SeedClinicalWorldAsync();
        var build = Stopwatch.StartNew();
        var catId = await BuildTenYearCatAsync(world);
        output.WriteLine($"Fixture built in {build.ElapsedMilliseconds} ms");

        // Sanity: the fixture is what the budget is stated against.
        var summary = await ReadAsync(await world.Vet.GetAsync($"/api/staff/cats/{catId}/clinical-history"));
        var counts = summary.GetProperty("sections").EnumerateArray().ToDictionary(s => s.GetProperty("key").GetString()!, s => s.GetProperty("count").GetInt32());
        Assert.Equal(Visits, counts["visits"]);
        Assert.Equal(Visits, counts["photos"]);
        Assert.Equal(Weights, counts["weights"]);
        Assert.Equal(Vaccinations, counts["vaccinations"]);
        Assert.Equal(ParasiteTreatments, counts["parasite-treatments"]);
        Assert.Equal(Orders, counts["lab-results"]);
        Assert.Equal(Weights, summary.GetProperty("weightSeries").GetArrayLength());

        // Warm-up (JIT, plan cache), then measure.
        for (var i = 0; i < 3; i++)
        {
            await world.Vet.GetAsync($"/api/staff/cats/{catId}/clinical-history");
            await world.Vet.GetAsync($"/api/staff/cats/{catId}/clinical-history/timeline?pageSize=20");
        }

        var summaryTimes = new List<double>();
        for (var i = 0; i < 50; i++)
        {
            var watch = Stopwatch.StartNew();
            var response = await world.Vet.GetAsync($"/api/staff/cats/{catId}/clinical-history");
            watch.Stop();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await response.Content.ReadAsStringAsync();
            summaryTimes.Add(watch.Elapsed.TotalMilliseconds);
        }

        var pageTimes = new List<double>();
        for (var i = 0; i < 50; i++)
        {
            var section = Sections[i % Sections.Length];
            var page = 1 + (i / Sections.Length) % 3;
            var watch = Stopwatch.StartNew();
            var response = await world.Vet.GetAsync($"/api/staff/cats/{catId}/clinical-history/{section}?pageNumber={page}&pageSize=20");
            watch.Stop();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await response.Content.ReadAsStringAsync();
            pageTimes.Add(watch.Elapsed.TotalMilliseconds);
        }

        var summaryP95 = Percentile95(summaryTimes);
        var pageP95 = Percentile95(pageTimes);
        output.WriteLine($"summary p95 = {summaryP95:F0} ms (budget 600); section page p95 = {pageP95:F0} ms (budget 300)");

        Assert.True(summaryP95 <= 600, $"summary p95 {summaryP95:F0} ms exceeds 600 ms");
        Assert.True(pageP95 <= 300, $"section page p95 {pageP95:F0} ms exceeds 300 ms");
    }
}
