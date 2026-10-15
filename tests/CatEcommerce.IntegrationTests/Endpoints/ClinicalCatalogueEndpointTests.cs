using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-EMR-001 §16 Phase A -- I-A1, I-A3 and the catalogue endpoints of §12.4.</summary>
public sealed class ClinicalCatalogueEndpointTests(ConsultationApiFactory factory) : ConsultationIntegrationTestBase(factory)
{
    /// <summary>I-A1 -- migrations on an empty database produce the catalogue tables, their uniques and the seeded system code.</summary>
    [Fact]
    public async Task I_A1_Migrations_CreateTheCatalogueTables_UniqueIndexes_AndSeedTheSystemCode()
    {
        foreach (var table in new[] { "diagnosis_code", "lab_analyte", "note_template" })
            Assert.Equal(1, await ScalarAsync<int>("select count(*) from information_schema.tables where table_schema = 'clinic' and table_name = @t", ("t", table)));

        foreach (var index in new[] { "ix_diagnosis_code_code", "ix_lab_analyte_code", "ix_note_template_service_active" })
        {
            var definition = await ScalarAsync<string>("select indexdef from pg_indexes where schemaname = 'clinic' and indexname = @i", ("i", index));
            Assert.NotNull(definition);
            Assert.Contains("UNIQUE", definition);
        }

        Assert.Contains("WHERE", await ScalarAsync<string>("select indexdef from pg_indexes where indexname = 'ix_note_template_service_active'") ?? "");

        var seeded = await ScalarAsync<string>("select name from clinic.diagnosis_code where code = @c and \"Id\" = @id",
            ("c", DiagnosisCodeSeed.RoutineNoAbnormality.Code), ("id", DiagnosisCodeSeed.RoutineNoAbnormality.Id));
        Assert.Equal(DiagnosisCodeSeed.RoutineNoAbnormality.Name, seeded);
    }

    /// <summary>I-A3 -- a vet creates a diagnosis code; a nurse is refused and the refusal is audited.</summary>
    [Fact]
    public async Task I_A3_VetCreatesADiagnosisCode_NurseIsRefused_AndAuditedAsDenied()
    {
        var world = await SeedClinicalWorldAsync();
        var code = NewDiagnosisCode();

        var created = await world.Vet.PostAsJsonAsync("/api/staff/diagnosis-codes", new { code, name = "Feline lower urinary tract disease", category = "Urinary" }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var nurseAttempt = await world.Nurse.PostAsJsonAsync("/api/staff/diagnosis-codes", new { code = NewDiagnosisCode(), name = "x" }, Json);
        Assert.Equal(HttpStatusCode.Forbidden, nurseAttempt.StatusCode);
        Assert.Equal(1, await CountDenialsAsync(world.NurseStaffId, Actions.ClinicalCatalogue.Manage));

        // A nurse may read the catalogue; reception may not.
        var nurseList = await world.Nurse.GetAsync($"/api/staff/diagnosis-codes?search={code}&activeOnly=true");
        Assert.Equal(HttpStatusCode.OK, nurseList.StatusCode);
        Assert.Equal(1, (await ReadAsync(nurseList)).GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Reception.GetAsync("/api/staff/diagnosis-codes")).StatusCode);
    }

    [Fact]
    public async Task DiagnosisCode_DuplicateCode_And_LowerCase_AreRefused_SystemCodeCannotBeDeactivated()
    {
        var world = await SeedClinicalWorldAsync();
        var code = NewDiagnosisCode();
        await CreateDiagnosisCodeAsync(world.Vet, code);

        var duplicate = await world.Vet.PostAsJsonAsync("/api/staff/diagnosis-codes", new { code, name = "Again" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Contains(BusinessViolationCodes.DiagnosisCodeAlreadyExists, await duplicate.Content.ReadAsStringAsync());

        var lower = await world.Vet.PostAsJsonAsync("/api/staff/diagnosis-codes", new { code = "lower-case", name = "x" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, lower.StatusCode);

        var deactivate = await world.Vet.PutAsJsonAsync($"/api/staff/diagnosis-codes/{DiagnosisCodeSeed.RoutineNoAbnormality.Id}/active", new { isActive = false }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, deactivate.StatusCode);
        Assert.Contains(BusinessViolationCodes.SystemDiagnosisCodeReadOnly, await deactivate.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task LabAnalyte_Create_Read_Update_AndInvalidRangeIsRefused()
    {
        var world = await SeedClinicalWorldAsync();
        var code = "A" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();

        var created = await world.Vet.PostAsJsonAsync("/api/staff/lab-analytes", new
        { code, name = "Creatinine", valueKind = "Numeric", diagnosticKind = "BloodWork", unit = "mg/dL", referenceLow = 0.8, referenceHigh = 1.8 }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await ReadAsync(created)).GetProperty("labAnalyteId").GetGuid();

        var detail = await ReadAsync(await world.Nurse.GetAsync($"/api/staff/lab-analytes/{id}"));
        Assert.Equal("0.8", detail.GetProperty("referenceLow").GetString());
        Assert.Equal("Numeric", detail.GetProperty("valueKind").GetString());

        var inverted = await world.Vet.PutAsJsonAsync($"/api/staff/lab-analytes/{id}", new
        { name = "Creatinine", valueKind = "Numeric", diagnosticKind = "BloodWork", unit = "mg/dL", referenceLow = 2.0, referenceHigh = 1.0 }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, inverted.StatusCode);
        Assert.Contains(BusinessViolationCodes.InvalidReferenceRange, await inverted.Content.ReadAsStringAsync());

        var noUnit = await world.Vet.PostAsJsonAsync("/api/staff/lab-analytes", new
        { code = "B" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(), name = "No unit", valueKind = "Numeric", diagnosticKind = "BloodWork" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, noUnit.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await world.Vet.GetAsync($"/api/staff/lab-analytes/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task NoteTemplate_OneActivePerService_VersionBumpsOnEdit()
    {
        var world = await SeedClinicalWorldAsync();

        var first = await world.Vet.PostAsJsonAsync("/api/staff/note-templates", new { serviceId = world.ServiceId, name = "Wellness", subjective = "History: " }, Json);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var templateId = (await ReadAsync(first)).GetProperty("noteTemplateId").GetGuid();

        var second = await world.Vet.PostAsJsonAsync("/api/staff/note-templates", new { serviceId = world.ServiceId, name = "Other" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Contains(BusinessViolationCodes.NoteTemplateAlreadyExistsForService, await second.Content.ReadAsStringAsync());

        var edit = await world.Vet.PutAsJsonAsync($"/api/staff/note-templates/{templateId}", new { name = "Wellness v2", subjective = "History: ", plan = "Plan: " }, Json);
        Assert.Equal(HttpStatusCode.NoContent, edit.StatusCode);
        Assert.Equal(2, (await ReadAsync(await world.Vet.GetAsync($"/api/staff/note-templates/{templateId}"))).GetProperty("templateVersion").GetInt32());

        // Deactivate frees the slot for a new active template.
        Assert.Equal(HttpStatusCode.NoContent, (await world.Vet.PutAsJsonAsync($"/api/staff/note-templates/{templateId}/active", new { isActive = false }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await world.Vet.PostAsJsonAsync("/api/staff/note-templates", new { serviceId = world.ServiceId, name = "Replacement" }, Json)).StatusCode);
    }
}
