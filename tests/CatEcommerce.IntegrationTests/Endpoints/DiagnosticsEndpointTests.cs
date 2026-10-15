using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Infrastructure.Files;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Domain.DomainServices;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-EMR-001 §16 Phase D -- I-D1 .. I-D11: diagnostic orders, structured results with flags, lab files stored and served
/// safely (the pipeline itself is HO-FIL-001's; here it is exercised through the clinical flow), photographs.</summary>
public sealed class DiagnosticsEndpointTests(ConsultationApiFactory factory) : ConsultationIntegrationTestBase(factory)
{
    private static byte[] Pdf() => "%PDF-1.4\n%%EOF"u8.ToArray();

    private static byte[] Png() =>
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, 0xDE
    ];

    private static byte[] Dicom()
    {
        var bytes = new byte[140];
        "DICM"u8.CopyTo(bytes.AsSpan(128));
        return bytes;
    }

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, string purpose, byte[] content, string fileName, string contentType)
    {
        using var form = new MultipartFormDataContent("emr-boundary-" + Guid.NewGuid().ToString("N"));
        var part = new ByteArrayContent(content);
        part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(part, "file", fileName);
        return await client.PostAsync($"/api/staff/files?purpose={purpose}", form);
    }

    private static async Task<(Guid FileId, string Url)> UploadOkAsync(HttpClient client, string purpose, byte[] content, string fileName, string contentType)
    {
        var response = await UploadAsync(client, purpose, content, fileName, contentType);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var body = await ReadAsync(response);
        return (body.GetProperty("fileId").GetGuid(), body.GetProperty("url").GetString()!);
    }

    private static async Task<Guid> OrderAsync(HttpClient vet, Guid visitId, string kind = "BloodWork", Guid[]? analyteIds = null, string? description = null)
    {
        var response = await vet.PostAsJsonAsync($"/api/staff/visits/{visitId}/diagnostics", new { kind, description, analyteIds = analyteIds ?? [] }, Json);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await ReadAsync(response)).GetProperty("diagnosticOrderId").GetGuid();
    }

    private static async Task<Guid> CreateAnalyteAsync(HttpClient vet, string name, decimal? low = 5m, decimal? high = 10m, string unit = "g/dL")
    {
        var response = await vet.PostAsJsonAsync("/api/staff/lab-analytes", new
        {
            code = "A" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(), name, valueKind = "Numeric", diagnosticKind = "BloodWork", unit, referenceLow = low, referenceHigh = high
        }, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadAsync(response)).GetProperty("labAnalyteId").GetGuid();
    }

    private async Task<(ClinicalWorld World, Guid VisitId)> OpenWorldAsync(Action<IServiceCollection>? configureServices = null)
    {
        var world = await SeedClinicalWorldAsync(configureServices: configureServices);
        var visitId = await OpenVisitAsync(world.Nurse, world.AppointmentId);
        return (world, visitId);
    }

    private static async Task FinaliseWithSystemCodeAsync(ClinicalWorld world, Guid visitId)
    {
        var version = (await ReadAsync(await world.Vet.GetAsync($"/api/staff/visits/{visitId}"))).GetProperty("version").GetInt32();
        var save = await world.Vet.PutAsJsonAsync($"/api/staff/visits/{visitId}/note", new
        {
            expectedVersion = version, assessment = "Well", plan = "None",
            problems = new[] { new { diagnosisCodeId = DiagnosisCodeSeed.RoutineNoAbnormality.Id, certainty = "Confirmed" } }
        }, Json);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var finalised = await world.Vet.PostAsJsonAsync($"/api/staff/visits/{visitId}/finalise",
            new { expectedVersion = (await ReadAsync(save)).GetProperty("version").GetInt32() }, Json);
        Assert.Equal(HttpStatusCode.NoContent, finalised.StatusCode);
    }

    /// <summary>I-D1 -- a blood-work order is recorded as pending (US-CLN-14 AC1).</summary>
    [Fact]
    public async Task I_D1_OrderingBloodWork_IsRecordedAsPending()
    {
        var (world, visitId) = await OpenWorldAsync();
        var analyteId = await CreateAnalyteAsync(world.Vet, "Haemoglobin");

        var orderId = await OrderAsync(world.Vet, visitId, analyteIds: [analyteId]);

        var order = await ReadAsync(await world.Vet.GetAsync($"/api/staff/diagnostics/{orderId}"));
        Assert.Equal("Pending", order.GetProperty("status").GetString());
        Assert.Equal("BloodWork", order.GetProperty("kind").GetString());
        Assert.Equal("Haemoglobin", Assert.Single(order.GetProperty("requestedAnalytes").EnumerateArray()).GetProperty("name").GetString());

        var pending = await ReadAsync(await world.Nurse.GetAsync("/api/staff/diagnostics?status=Pending"));
        Assert.Contains(pending.EnumerateArray(), o => o.GetProperty("id").GetGuid() == orderId);

        var visit = await ReadAsync(await world.Vet.GetAsync($"/api/staff/visits/{visitId}"));
        Assert.Equal("Pending", Assert.Single(visit.GetProperty("diagnostics").EnumerateArray()).GetProperty("status").GetString());

        // Nurses may read orders but never write them (Appendix D).
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Nurse.PostAsJsonAsync($"/api/staff/visits/{visitId}/diagnostics", new { kind = "BloodWork" }, Json)).StatusCode);
    }

    /// <summary>I-D2 -- values are flagged against their reference range, returned and persisted with the range (US-CLN-14 AC2).</summary>
    [Fact]
    public async Task I_D2_ResultValues_AreFlaggedAgainstTheirRange_AndPersistedWithIt()
    {
        var (world, visitId) = await OpenWorldAsync();
        var analyteId = await CreateAnalyteAsync(world.Vet, "Haemoglobin", low: 5m, high: 10m, unit: "g/dL");
        var orderId = await OrderAsync(world.Vet, visitId, analyteIds: [analyteId]);

        var response = await world.Vet.PostAsJsonAsync($"/api/staff/diagnostics/{orderId}/results", new
        {
            reportedOn = DateOnly.FromDateTime(world.Clock.GetUtcNow().UtcDateTime),
            values = new object[]
            {
                new { analyteId, numericValue = 4.9m },
                new { analyteId, numericValue = 7.0m },
                new { analyteId, numericValue = 10.1m },
                new { name = "FeLV", valueKind = "Qualitative", textValue = "Positive", normalText = "Negative" },
            }
        }, Json);

        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var body = await ReadAsync(response);
        Assert.Equal(["Low", "Normal", "High", "Abnormal"], body.GetProperty("values").EnumerateArray().Select(v => v.GetProperty("flag").GetString()));

        var order = await ReadAsync(await world.Vet.GetAsync($"/api/staff/diagnostics/{orderId}"));
        Assert.Equal("Resulted", order.GetProperty("status").GetString());
        var values = Assert.Single(order.GetProperty("resultSets").EnumerateArray()).GetProperty("values").EnumerateArray().ToList();
        Assert.Equal("g/dL", values[0].GetProperty("unit").GetString());
        Assert.Equal("5", values[0].GetProperty("referenceLow").GetString());
        Assert.Equal("10", values[0].GetProperty("referenceHigh").GetString());
        Assert.Equal("Negative", values[3].GetProperty("normalText").GetString());

        Assert.Equal(1, await ScalarAsync<int>(
            "select count(*) from clinic.diagnostic_result_value v join clinic.diagnostic_result_set s on s.\"Id\" = v.result_set_id where s.diagnostic_order_id = @o and v.flag = @f",
            ("o", orderId), ("f", 2))); // ResultFlag.Low
    }

    /// <summary>I-D3 -- a lab PDF is uploaded, attached with date and note, and served from a short-lived signed URL with hardened headers (US-CLN-14 AC3).</summary>
    [Fact]
    public async Task I_D3_ALabPdfIsAttached_AndServedWithHardenedHeaders()
    {
        var (world, visitId) = await OpenWorldAsync();
        var orderId = await OrderAsync(world.Vet, visitId);
        var (fileId, _) = await UploadOkAsync(world.Vet, "LabResult", Pdf(), "cbc.pdf", "application/pdf");

        var attach = await world.Vet.PostAsJsonAsync($"/api/staff/diagnostics/{orderId}/attachments",
            new { fileId, resultDate = DateOnly.FromDateTime(world.Clock.GetUtcNow().UtcDateTime), note = "CBC from the reference lab" }, Json);
        Assert.Equal(HttpStatusCode.Created, attach.StatusCode);

        var order = await ReadAsync(await world.Vet.GetAsync($"/api/staff/diagnostics/{orderId}"));
        var attachment = Assert.Single(order.GetProperty("attachments").EnumerateArray());
        Assert.Equal("CBC from the reference lab", attachment.GetProperty("note").GetString());
        var url = attachment.GetProperty("file").GetProperty("url").GetString()!;

        var served = await world.Vet.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Equal("application/pdf", served.Content.Headers.ContentType!.MediaType);
        Assert.Equal("nosniff", served.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("sandbox", served.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Contains("no-store", served.Headers.CacheControl!.ToString());
    }

    /// <summary>I-D4 -- DICOM is stored as a file and always served as an attachment.</summary>
    [Fact]
    public async Task I_D4_DicomIsServedAsAnAttachment()
    {
        var (world, visitId) = await OpenWorldAsync();
        var orderId = await OrderAsync(world.Vet, visitId, kind: "Imaging", description: "Thorax, two views");
        var (fileId, _) = await UploadOkAsync(world.Vet, "LabResult", Dicom(), "scan.dcm", "application/dicom");
        Assert.Equal(HttpStatusCode.Created, (await world.Vet.PostAsJsonAsync($"/api/staff/diagnostics/{orderId}/attachments",
            new { fileId, resultDate = DateOnly.FromDateTime(world.Clock.GetUtcNow().UtcDateTime) }, Json)).StatusCode);

        var order = await ReadAsync(await world.Vet.GetAsync($"/api/staff/diagnostics/{orderId}"));
        var url = Assert.Single(order.GetProperty("attachments").EnumerateArray()).GetProperty("file").GetProperty("url").GetString()!;

        var served = await world.Vet.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Contains("attachment", served.Content.Headers.ContentDisposition!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>I-D5 -- content is checked, not the declared type; infected content is refused and nothing is stored.</summary>
    [Fact]
    public async Task I_D5_UploadRefusals_AreByContent_AndLeaveNothingBehind()
    {
        var (world, _) = await OpenWorldAsync();
        var before = Directory.Exists(Factory.TempRoot) ? Directory.GetFiles(Factory.TempRoot).Length : 0;

        var pngAsPdf = await UploadAsync(world.Vet, "LabResult", Png(), "report.pdf", "application/pdf");
        Assert.Equal(HttpStatusCode.BadRequest, pngAsPdf.StatusCode);
        Assert.Contains(BusinessViolationCodes.FileContentMismatch, await pngAsPdf.Content.ReadAsStringAsync());

        var eicar = System.Text.Encoding.ASCII.GetBytes(@"%PDF-1.4 X5O!P%@AP[4\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*");
        var infected = await UploadAsync(world.Vet, "LabResult", eicar, "report.pdf", "application/pdf");
        Assert.Equal(HttpStatusCode.BadRequest, infected.StatusCode);
        Assert.Contains(BusinessViolationCodes.FileRejected, await infected.Content.ReadAsStringAsync());

        var oversize = new byte[(50 * 1024 * 1024) + 1];
        "%PDF-1.4"u8.CopyTo(oversize);
        var tooBig = await UploadAsync(world.Vet, "LabResult", oversize, "huge.pdf", "application/pdf");
        Assert.True(tooBig.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge, tooBig.StatusCode.ToString());

        var after = Directory.Exists(Factory.TempRoot) ? Directory.GetFiles(Factory.TempRoot).Length : 0;
        Assert.Equal(before, after); // every temp file was deleted
    }

    /// <summary>I-D6 -- a signed URL expires after five minutes, and a tampered signature is refused; both 403 with no detail.</summary>
    [Fact]
    public async Task I_D6_ASignedUrl_ExpiresAfterFiveMinutes_AndATamperedOneIsRefused()
    {
        var (world, _) = await OpenWorldAsync();
        var (_, url) = await UploadOkAsync(world.Vet, "LabResult", Pdf(), "cbc.pdf", "application/pdf");

        Assert.Equal(HttpStatusCode.OK, (await world.Vet.GetAsync(url)).StatusCode);

        var tampered = url[..^2] + (url[^1] == 'a' ? "bb" : "aa");
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Vet.GetAsync(tampered)).StatusCode);

        world.Clock.Advance(TimeSpan.FromMinutes(6));
        var expired = await world.Vet.GetAsync(url);
        Assert.Equal(HttpStatusCode.Forbidden, expired.StatusCode);
        Assert.Equal("{}", await expired.Content.ReadAsStringAsync());
    }

    /// <summary>I-D7 -- with no scanner available an upload is refused, never stored unscanned (OQ-6 posture, ADR-319).</summary>
    [Fact]
    public async Task I_D7_WhenNoScannerIsAvailable_TheUploadIsRefused()
    {
        var (world, _) = await OpenWorldAsync(services =>
        {
            services.AddScoped<UnconfiguredMalwareScanner>();
            services.AddScoped<IMalwareScanner>(sp => sp.GetRequiredService<UnconfiguredMalwareScanner>());
        });

        var response = await UploadAsync(world.Vet, "LabResult", Pdf(), "cbc.pdf", "application/pdf");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(BusinessViolationCodes.FileScanUnavailable, await response.Content.ReadAsStringAsync());
    }

    /// <summary>I-D8 -- results arrive after the visit is finalised; a correction is a second set that names the first, and both stay visible.</summary>
    [Fact]
    public async Task I_D8_ResultsAfterFinalisation_AndASupersedingSet_BothStayVisible()
    {
        var (world, visitId) = await OpenWorldAsync();
        var analyteId = await CreateAnalyteAsync(world.Vet, "Haemoglobin");
        var orderId = await OrderAsync(world.Vet, visitId, analyteIds: [analyteId]);
        await FinaliseWithSystemCodeAsync(world, visitId);
        var today = BranchCalendar.LocalDateOf(world.Clock.GetUtcNow(), "Asia/Dhaka");

        var first = await world.Vet.PostAsJsonAsync($"/api/staff/diagnostics/{orderId}/results", new { reportedOn = today, values = new[] { new { analyteId, numericValue = 7.0m } } }, Json);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstId = (await ReadAsync(first)).GetProperty("resultSetId").GetGuid();

        world.Clock.Advance(TimeSpan.FromMinutes(2));
        var second = await world.Vet.PostAsJsonAsync($"/api/staff/diagnostics/{orderId}/results",
            new { reportedOn = today, supersedesResultSetId = firstId, values = new[] { new { analyteId, numericValue = 7.5m } } }, Json);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        var order = await ReadAsync(await world.Vet.GetAsync($"/api/staff/diagnostics/{orderId}"));
        var sets = order.GetProperty("resultSets").EnumerateArray().ToList();
        Assert.Equal(2, sets.Count);
        Assert.Equal(firstId, sets[1].GetProperty("supersedesResultSetId").GetGuid());
        Assert.Equal(JsonValueKind.Null, sets[0].GetProperty("supersedesResultSetId").ValueKind);
    }

    /// <summary>I-D9 -- the database refuses to change a recorded result value (append-only trigger).</summary>
    [Fact]
    public async Task I_D9_ARecordedResultValue_CannotBeUpdatedOrDeleted_ThroughTheDatabase()
    {
        var (world, visitId) = await OpenWorldAsync();
        var orderId = await OrderAsync(world.Vet, visitId);
        var recorded = await world.Vet.PostAsJsonAsync($"/api/staff/diagnostics/{orderId}/results", new
        {
            reportedOn = DateOnly.FromDateTime(world.Clock.GetUtcNow().UtcDateTime),
            values = new[] { new { name = "Creatinine", valueKind = "Numeric", numericValue = 1.2m, unit = "mg/dL", referenceLow = 0.8m, referenceHigh = 1.8m } }
        }, Json);
        Assert.Equal(HttpStatusCode.Created, recorded.StatusCode);

        var valueId = await ScalarAsync<Guid>(
            "select v.\"Id\" from clinic.diagnostic_result_value v join clinic.diagnostic_result_set s on s.\"Id\" = v.result_set_id where s.diagnostic_order_id = @o limit 1", ("o", orderId));
        var setId = await ScalarAsync<Guid>("select \"Id\" from clinic.diagnostic_result_set where diagnostic_order_id = @o limit 1", ("o", orderId));

        Assert.Equal("CR001", await SqlStateOfAsync("update clinic.diagnostic_result_value set numeric_value = 0.5 where \"Id\" = @v", ("v", valueId)));
        Assert.Equal("CR001", await SqlStateOfAsync("delete from clinic.diagnostic_result_value where \"Id\" = @v", ("v", valueId)));
        Assert.Equal("CR001", await SqlStateOfAsync("update clinic.diagnostic_result_set set note = 'x' where \"Id\" = @s", ("s", setId)));
        Assert.Equal("CR001", await SqlStateOfAsync("delete from clinic.diagnostic_order where \"Id\" = @o", ("o", orderId)));
        Assert.Equal("CR001", await SqlStateOfAsync("truncate clinic.diagnostic_result_value"));
        Assert.Equal(1.2m, await ScalarAsync<decimal>("select numeric_value from clinic.diagnostic_result_value where \"Id\" = @v", ("v", valueId)));
    }

    /// <summary>I-D10 -- a dated, noted photograph is attached to the open visit and shown with a signed URL (US-CLN-19).
    /// Its place on the cat's clinical timeline is asserted in the history tests (Phase E).</summary>
    [Fact]
    public async Task I_D10_APhotoWithDateAndNote_IsAttachedToTheVisit()
    {
        var (world, visitId) = await OpenWorldAsync();
        var (fileId, _) = await UploadOkAsync(world.Vet, "ClinicalPhoto", Png(), "ear.png", "image/png");
        // The server judges "future" against the branch-local date (Asia/Dhaka, UTC+6), not UTC: from 18:00 UTC the two differ.
        var takenOn = BranchCalendar.LocalDateOf(world.Clock.GetUtcNow(), "Asia/Dhaka");

        var attach = await world.Vet.PostAsJsonAsync($"/api/staff/visits/{visitId}/photos", new { fileId, takenOn, note = "Left ear, day 1" }, Json);
        Assert.Equal(HttpStatusCode.Created, attach.StatusCode);

        var visit = await ReadAsync(await world.Vet.GetAsync($"/api/staff/visits/{visitId}"));
        var photo = Assert.Single(visit.GetProperty("photos").EnumerateArray());
        Assert.Equal("Left ear, day 1", photo.GetProperty("note").GetString());
        Assert.Equal(takenOn.ToString("yyyy-MM-dd"), photo.GetProperty("takenOn").GetString());
        Assert.Equal(photo.GetProperty("url").GetString(), photo.GetProperty("thumbnailUrl").GetString());
        Assert.Equal(HttpStatusCode.OK, (await world.Vet.GetAsync(photo.GetProperty("url").GetString()!)).StatusCode);

        var tomorrow = await UploadOkAsync(world.Vet, "ClinicalPhoto", Png(), "ear2.png", "image/png");
        var future = await world.Vet.PostAsJsonAsync($"/api/staff/visits/{visitId}/photos", new { fileId = tomorrow.FileId, takenOn = takenOn.AddDays(1) }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, future.StatusCode);
    }

    /// <summary>I-D11 -- one file is attached once, across photos and lab attachments.</summary>
    [Fact]
    public async Task I_D11_TheSameFileCannotBeAttachedTwice()
    {
        var (world, visitId) = await OpenWorldAsync();
        var orderId = await OrderAsync(world.Vet, visitId);
        var (fileId, _) = await UploadOkAsync(world.Vet, "LabResult", Pdf(), "cbc.pdf", "application/pdf");
        var today = BranchCalendar.LocalDateOf(world.Clock.GetUtcNow(), "Asia/Dhaka");

        Assert.Equal(HttpStatusCode.Created, (await world.Vet.PostAsJsonAsync($"/api/staff/diagnostics/{orderId}/attachments", new { fileId, resultDate = today }, Json)).StatusCode);

        var again = await world.Vet.PostAsJsonAsync($"/api/staff/diagnostics/{orderId}/attachments", new { fileId, resultDate = today }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Contains(BusinessViolationCodes.FileAlreadyAttached, await again.Content.ReadAsStringAsync());

        var asPhoto = await world.Vet.PostAsJsonAsync($"/api/staff/visits/{visitId}/photos", new { fileId, takenOn = today }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, asPhoto.StatusCode);

        // A file attached to a clinical record cannot be deleted out from under it, even by a direct delete.
        Assert.Equal("CR001", await SqlStateOfAsync("delete from files.stored_file where \"Id\" = @f", ("f", fileId)));
    }

    /// <summary>Cancel: a pending order with nothing back may be cancelled; one with results may not; another branch's order is 404.</summary>
    [Fact]
    public async Task Cancel_AndTheBranchRule()
    {
        var (world, visitId) = await OpenWorldAsync();
        var pending = await OrderAsync(world.Vet, visitId);
        var withResults = await OrderAsync(world.Vet, visitId);
        await world.Vet.PostAsJsonAsync($"/api/staff/diagnostics/{withResults}/results", new
        {
            reportedOn = DateOnly.FromDateTime(world.Clock.GetUtcNow().UtcDateTime),
            values = new[] { new { name = "x", valueKind = "Numeric", numericValue = 1m, unit = "u" } }
        }, Json);

        Assert.Equal(HttpStatusCode.NoContent, (await world.Vet.PostAsJsonAsync($"/api/staff/diagnostics/{pending}/cancel", new { reason = "ordered by mistake" }, Json)).StatusCode);
        var refused = await world.Vet.PostAsJsonAsync($"/api/staff/diagnostics/{withResults}/cancel", new { reason = "no" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains(BusinessViolationCodes.DiagnosticOrderHasResults, await refused.Content.ReadAsStringAsync());

        var other = await SeedClinicalWorldAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await other.Vet.GetAsync($"/api/staff/diagnostics/{pending}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Vet.PostAsJsonAsync($"/api/staff/diagnostics/{pending}/cancel", new { reason = "x" }, Json)).StatusCode);
    }
}
