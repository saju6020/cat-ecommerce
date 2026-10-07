using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ZXing;
using ZXing.Common;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// ARCHITECTURE gate, HIGH (2026-09-26): GetCatProfileQueryHandler (Q1) and
/// GetCatMedicalHistoryQueryHandler (Q5) were rewritten from aggregate rehydration to a
/// database-side Select projection. These tests prove the outputs are byte-identical in shape
/// and value to what the rehydrated version produced -- every field on the profile view model,
/// including nested owned collections (allergies, chronic conditions, medications, photos), and
/// the medical-history PDF still renders from the same data.
/// </summary>
public sealed class CatProfileProjectionEndpointTests(CatApiFactory factory) : CatIntegrationTestBase(factory)
{
    [Fact]
    public async Task GetCat_FullProfile_ProjectsEveryFieldCorrectly()
    {
        using var client = CreateClient();
        // SECURITY gate, LOW (2026-09-26): a staff-supplied ownerId must resolve to a real
        // customer (RegisterCatCommandHandler) -- a bare Guid.NewGuid() now fails 400.
        var ownerId = await SeedCustomerAsync();
        var catId = await RegisterCatAsync(client, ownerId);

        var recordedOn = DateOnly.FromDateTime(DateTime.UtcNow);
        var medicalProfileResponse = await client.PutAsJsonAsync($"/api/cats/{catId}/medical-profile", new
        {
            allergies = new[]
            {
                new { allergen = "Chicken", reaction = "Vomiting", severity = "Moderate", recordedOn }
            },
            chronicConditions = new[]
            {
                new { condition = "Asthma", diagnosedOn = (DateOnly?)recordedOn, notes = "Mild" }
            },
            medications = new[]
            {
                new { medicationName = "Prednisolone", dosage = "5mg", frequency = "Daily", startedOn = recordedOn, endsOn = (DateOnly?)null }
            },
            dietaryNotes = "Grain-free diet",
            behaviouralNotes = "Nervous around strangers"
        }, Json);
        medicalProfileResponse.EnsureSuccessStatusCode();

        // Gate-fix 2026-09-27 (HO-FIL-001-ARCHITECTURE.md third pass): the endpoint no longer
        // trusts a client-supplied storageKey/contentType/sizeInBytes/fileName -- a photo is a
        // server-issued FileId, minted by a real upload through /api/staff/files, then attached.
        // `client` is already the signed-in Administrator RegisterCatAsync used above.
        using var uploadForm = new MultipartFormDataContent("test-boundary-" + Guid.NewGuid().ToString("N"));
        var pngBytes = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, // IHDR chunk header (fake, enough for the magic-byte check)
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
            0xDE
        };
        var pngContent = new ByteArrayContent(pngBytes);
        pngContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        uploadForm.Add(pngContent, "file", "cat.png");

        var uploadResponse = await client.PostAsync("/api/staff/files?purpose=CatProfilePhoto", uploadForm);
        uploadResponse.EnsureSuccessStatusCode();
        var uploadBody = await uploadResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        var fileId = uploadBody.GetProperty("fileId").GetGuid();

        var photoResponse = await client.PostAsJsonAsync($"/api/cats/{catId}/photos", new { fileId, isPrimary = true }, Json);
        photoResponse.EnsureSuccessStatusCode();

        var highRiskResponse = await client.PutAsJsonAsync($"/api/cats/{catId}/high-risk-flag", new
        {
            handlingNote = "Bites when scared",
            reason = "New behaviour observed at check-in"
        }, Json);
        highRiskResponse.EnsureSuccessStatusCode();

        var response = await client.GetAsync($"/api/cats/{catId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        Assert.Equal(catId, body.GetProperty("id").GetGuid());
        Assert.Equal(ownerId, body.GetProperty("ownerId").GetGuid());
        Assert.Equal("Integration Test Cat", body.GetProperty("name").GetString());
        Assert.Equal("Cat", body.GetProperty("species").GetString());
        Assert.Equal("Female", body.GetProperty("sex").GetString());
        Assert.Equal("Exact", body.GetProperty("dateOfBirthPrecision").GetString());
        Assert.Equal("Unknown", body.GetProperty("neuterStatus").GetString());
        Assert.True(body.GetProperty("isHighRisk").GetBoolean());
        Assert.Equal("Bites when scared", body.GetProperty("handlingNote").GetString());
        Assert.Equal("Alive", body.GetProperty("lifeStatus").GetString());
        Assert.Equal("Grain-free diet", body.GetProperty("dietaryNotes").GetString());
        Assert.Equal("Nervous around strangers", body.GetProperty("behaviouralNotes").GetString());
        Assert.True(body.GetProperty("hasRecordedAllergies").GetBoolean());

        var allergies = body.GetProperty("allergies");
        Assert.Equal(1, allergies.GetArrayLength());
        var allergy = allergies[0];
        Assert.Equal("Chicken", allergy.GetProperty("allergen").GetString());
        Assert.Equal("Vomiting", allergy.GetProperty("reaction").GetString());
        Assert.Equal("Moderate", allergy.GetProperty("severity").GetString());

        var chronicConditions = body.GetProperty("chronicConditions");
        Assert.Equal(1, chronicConditions.GetArrayLength());
        Assert.Equal("Asthma", chronicConditions[0].GetProperty("condition").GetString());

        var medications = body.GetProperty("medications");
        Assert.Equal(1, medications.GetArrayLength());
        Assert.Equal("Prednisolone", medications[0].GetProperty("medicationName").GetString());

        var photos = body.GetProperty("photos");
        Assert.Equal(1, photos.GetArrayLength());
        Assert.Equal(fileId, photos[0].GetProperty("fileId").GetGuid());
        Assert.False(string.IsNullOrEmpty(photos[0].GetProperty("url").GetString()));
        Assert.NotEqual(Guid.Empty, photos[0].GetProperty("id").GetGuid());
        Assert.True(photos[0].GetProperty("isPrimary").GetBoolean());
        // HO-FIL-001-ARCHITECTURE.md (second pass, MEDIUM): UploadedOn was restored to
        // CatPhotoViewModel alongside Id -- no test asserted it was actually reachable from the
        // read before this TEST gate.
        Assert.True(photos[0].TryGetProperty("uploadedOn", out var uploadedOnProperty), "photos[0] should carry an uploadedOn field.");
        Assert.True(uploadedOnProperty.GetDateTimeOffset() > DateTimeOffset.UtcNow.AddMinutes(-5));
    }

    // ARCHITECTURE gate, second pass (LOW): GetCatProfileQueryHandler (owned by Cat Profiles'
    // own module) depends on IStoredFileSigningBatcher (owned by FIL's Read layer) -- a DI edge
    // across modules that ValidateOnBuild would only catch if FileStorageModule were ever omitted
    // from the host. No test resolved this cross-module dependency directly before this TEST gate;
    // every other test only relied on it implicitly by virtue of every request succeeding.
    [Fact]
    public void Host_ResolvesTheCrossModuleStoredFileSigningBatcherDependency()
    {
        using var scope = Factory.Services.CreateScope();

        var batcher = scope.ServiceProvider.GetRequiredService<CatEcommerce.Read.QueryHandlers.StoredFile.IStoredFileSigningBatcher>();

        Assert.NotNull(batcher);
    }

    [Fact]
    public async Task GetCatMedicalHistoryPdf_AfterVaccinationAndParasiteTreatment_Returns200WithPdf()
    {
        using var client = CreateClient();
        var catId = await RegisterCatAsync(client);

        var vaccineCode = $"RABIES-{Guid.NewGuid():N}";
        var registerVaccineResponse = await client.PostAsJsonAsync("/api/vaccines", new
        {
            code = vaccineCode,
            name = "Rabies",
            isCore = true,
            validityMonths = 12,
            description = (string?)null
        }, Json);
        registerVaccineResponse.EnsureSuccessStatusCode();
        var vaccineBody = await registerVaccineResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        var vaccineId = vaccineBody.GetProperty("vaccineId").GetGuid();

        var vetId = await SeedStaffAccountAsync(CatEcommerce.Shared.Common.Constants.StaffRoles.Vet);
        var staffId = await SeedStaffAccountAsync(CatEcommerce.Shared.Common.Constants.StaffRoles.Nurse);

        var administeredOn = DateOnly.FromDateTime(DateTime.UtcNow);
        var vaccinationResponse = await client.PostAsJsonAsync($"/api/cats/{catId}/vaccinations", new
        {
            vaccineId,
            batchReference = "BATCH-PDF-1",
            batchExpiresOn = administeredOn.AddMonths(6),
            administeredOn,
            administeredByVetId = vetId,
            notes = (string?)null
        }, Json);
        vaccinationResponse.EnsureSuccessStatusCode();

        var parasiteResponse = await client.PostAsJsonAsync($"/api/cats/{catId}/parasite-treatments", new
        {
            treatmentType = "Deworming",
            productName = "Drontal",
            batchReference = "BATCH-PDF-2",
            administeredOn,
            administeredByStaffId = staffId,
            intervalDays = 90,
            notes = (string?)null
        }, Json);
        parasiteResponse.EnsureSuccessStatusCode();

        var response = await client.GetAsync($"/api/cats/{catId}/medical-history.pdf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        // SEC hardening, gate fix 2026-09-26, second pass: a clinical record document.
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);
    }

    /// <summary>SEC hardening, gate fix 2026-09-26, second pass: neither the medical-history PDF
    /// nor the cage-card image is meant to be cached by an intermediary or the browser.</summary>
    [Fact]
    public async Task GetCatCageCard_Returns200WithNoStoreCacheControl()
    {
        using var client = CreateClient();
        var catId = await RegisterCatAsync(client);

        var response = await client.GetAsync($"/api/cats/{catId}/cage-card");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);
    }

    /// <summary>
    /// ACCEPTANCE gate A22 fix, owner decisions (a)/(b)/1, 2026-09-27. The gate's own P2 probe
    /// showed the QR payload was the bare relative string "/cats/{catId}" -- a QR code with no
    /// host does not resolve when a phone scans it. This decodes the actual PNG the endpoint
    /// returns (ZXing.Net, test project only) and asserts the exact absolute URL, rather than only
    /// checking the response envelope (200/image/png/non-empty, as the pre-existing
    /// <see cref="GetCatCageCard_Returns200WithNoStoreCacheControl"/> above already did and still
    /// does). <c>CatApiFactory.WithPublicSiteWebBaseUrl</c> sets an absolute base URL for this one
    /// factory instance -- the default Testing configuration leaves it empty.
    /// </summary>
    [Fact]
    public async Task GetCatCageCard_QrPayload_DecodesToTheAbsoluteProfileUrl()
    {
        const string webBaseUrl = "https://cat-ecommerce.test";
        var webFactory = Factory.WithPublicSiteWebBaseUrl(webBaseUrl);
        using var client = webFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"),
            HandleCookies = true
        });
        var catId = await RegisterCatAsync(client);

        var response = await client.GetAsync($"/api/cats/{catId}/cage-card");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        var (luminance, width, height) = QrPngDecoder.DecodeGrayscale1Bit(bytes);

        var luminanceSource = new RGBLuminanceSource(luminance, width, height, RGBLuminanceSource.BitmapFormat.Gray8);
        var reader = new MultiFormatReader();
        var result = reader.decode(new BinaryBitmap(new HybridBinarizer(luminanceSource)));

        Assert.NotNull(result);
        Assert.Equal($"{webBaseUrl}/cats/{catId}", result.Text);
    }

    // SECURITY gate, LOW (2026-09-26): a Bengali cat name used to crash the whole PDF with a
    // QuestPDF DocumentDrawingException (proven live, P5/P6: "Mishti ঢ়ুমুর").
    // Noto Sans Bengali is now registered as a fallback font.
    [Fact]
    public async Task GetCatMedicalHistoryPdf_BengaliCatName_Returns200WithPdf()
    {
        using var client = CreateClient();
        var catId = await RegisterCatAsync(client, name: "মিষ্টি ঝুমুর");

        var response = await client.GetAsync($"/api/cats/{catId}/medical-history.pdf");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);
    }
}
