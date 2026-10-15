using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-EMR-001 §16 Phase H -- I-H1 .. I-H6: consent by on-screen signature or signed upload, linked to the cat, the procedure
/// booking, the kind, the date and the exact form version; withdrawal; the status contract HO-SRG-001 reads; and the database's
/// refusal to delete or edit a consent.</summary>
public sealed class ConsentEndpointTests(ConsultationApiFactory factory) : ConsultationIntegrationTestBase(factory)
{
    private static decimal[][][] Strokes() =>
    [
        [[0.10m, 0.20m], [0.20m, 0.30m], [0.30m, 0.25m], [0.40m, 0.40m], [0.50m, 0.35m], [0.60m, 0.50m]],
        [[0.15m, 0.60m], [0.35m, 0.62m], [0.55m, 0.58m], [0.75m, 0.65m], [0.85m, 0.60m]],
    ];

    private static byte[] Pdf() => "%PDF-1.4\n%%EOF"u8.ToArray();

    /// <summary>The consent forms are HO-ADM-001 policy documents the owner authors (OQ-15). A shared database keeps a published form
    /// from run to run, so this publishes only what is missing and returns the current version number.</summary>
    private static async Task<int> EnsureFormPublishedAsync(HttpClient admin, string kind)
    {
        var key = ConsentForms.KeyFor(Enumeration.FromDisplayName<ConsentKind>(kind));
        if ((await admin.GetAsync($"/api/staff/consent-forms/{kind}")).StatusCode != HttpStatusCode.OK)
        {
            var create = await admin.PostAsJsonAsync("/api/admin/policies", new { key, title = $"Consent - {kind}", requiresAcceptance = false }, Json);
            Assert.True(create.StatusCode is HttpStatusCode.Created or HttpStatusCode.BadRequest, await create.Content.ReadAsStringAsync()); // 400: another run created it first
            var publish = await admin.PostAsJsonAsync($"/api/admin/policies/{key}/versions", new { body = $"I consent to the {kind.ToLowerInvariant()} described.", changeSummary = "Initial wording" }, Json);
            Assert.True(publish.StatusCode == HttpStatusCode.Created, await publish.Content.ReadAsStringAsync());
        }

        var form = await admin.GetAsync($"/api/staff/consent-forms/{kind}");
        Assert.Equal(HttpStatusCode.OK, form.StatusCode);
        return (await ReadAsync(form)).GetProperty("versionNumber").GetInt32();
    }

    private static object OnScreen(ClinicalWorld world, int version, string kind = "Surgery", decimal[][][]? strokes = null) => new
    {
        catId = world.CatId, appointmentId = world.AppointmentId, kind, procedureDescription = "Dental extraction under anaesthesia",
        method = "OnScreenSignature", signature = strokes ?? Strokes(), signedByName = "Rahim Uddin", signerRelationship = "Owner",
        signedByCustomerId = world.CustomerId, policyVersionNumber = version
    };

    /// <summary>I-H1 -- the consent form is the current published policy wording, with its version number.</summary>
    [Fact]
    public async Task I_H1_TheConsentFormIsTheCurrentPublishedWording_WithItsVersion()
    {
        var world = await SeedClinicalWorldAsync();

        var version = await EnsureFormPublishedAsync(world.Admin, "Surgery");

        var form = await ReadAsync(await world.Reception.GetAsync("/api/staff/consent-forms/Surgery"));
        Assert.Equal("consent-surgery", form.GetProperty("policyKey").GetString());
        Assert.Equal(version, form.GetProperty("versionNumber").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(form.GetProperty("body").GetString()));
        Assert.Equal(HttpStatusCode.NotFound, (await world.Reception.GetAsync("/api/staff/consent-forms/Nonsense")).StatusCode);

        // The kinds are separate documents: until the owner publishes euthanasia wording, it is not found (consent is refused).
        var other = await world.Reception.GetAsync("/api/staff/consent-forms/Euthanasia");
        Assert.True(other.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound);
    }

    /// <summary>I-H2 -- reception records an on-screen consent: linked to cat, procedure booking, kind, date and form version (US-CLN-21 AC1).</summary>
    [Fact]
    public async Task I_H2_ReceptionRecordsAnOnScreenConsent_LinkedToTheProcedureAndTheFormVersion()
    {
        var world = await SeedClinicalWorldAsync();
        var version = await EnsureFormPublishedAsync(world.Admin, "Surgery");

        var response = await world.Reception.PostAsJsonAsync("/api/staff/consents", OnScreen(world, version), Json);

        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var consentId = (await ReadAsync(response)).GetProperty("consentId").GetGuid();

        var consent = await ReadAsync(await world.Nurse.GetAsync($"/api/staff/consents/{consentId}"));
        Assert.Equal(world.CatId, consent.GetProperty("catId").GetGuid());
        Assert.Equal(world.AppointmentId, consent.GetProperty("appointmentId").GetGuid());
        Assert.Equal("Surgery", consent.GetProperty("kind").GetString());
        Assert.Equal("OnScreenSignature", consent.GetProperty("method").GetString());
        Assert.Equal(version, consent.GetProperty("policyVersionNumber").GetInt32());
        Assert.Equal("consent-surgery", consent.GetProperty("policyKey").GetString());
        Assert.Equal(JsonValueKind.String, consent.GetProperty("signedOn").ValueKind);
        Assert.Equal("Recorded", consent.GetProperty("status").GetString());
        Assert.False(consent.TryGetProperty("signature", out _), "the stroke coordinates are never returned as JSON");

        // The signature is drawn from numbers: only <svg> and <polyline> elements, numeric points.
        var svg = await world.Nurse.GetAsync(consent.GetProperty("signatureUrl").GetString()!);
        Assert.Equal(HttpStatusCode.OK, svg.StatusCode);
        Assert.Equal("image/svg+xml", svg.Content.Headers.ContentType!.MediaType);
        Assert.Contains("default-src 'none'", svg.Headers.GetValues("Content-Security-Policy").Single());
        var markup = await svg.Content.ReadAsStringAsync();
        Assert.Equal(["polyline", "polyline", "svg"], Regex.Matches(markup, "<([a-zA-Z]+)").Select(m => m.Groups[1].Value).Order().ToArray());
        Assert.All(Regex.Matches(markup, "points=\"([^\"]*)\"").Select(m => m.Groups[1].Value),
            points => Assert.Matches(@"^[0-9.,\- ]+$", points));
        Assert.DoesNotContain("script", markup, StringComparison.OrdinalIgnoreCase);

        var list = await ReadAsync(await world.Nurse.GetAsync($"/api/staff/cats/{world.CatId}/consents"));
        Assert.Equal(consentId, Assert.Single(list.EnumerateArray()).GetProperty("id").GetGuid());
    }

    /// <summary>I-H3 -- a signed paper form is uploaded as a ConsentForm file and referenced by the consent.</summary>
    [Fact]
    public async Task I_H3_ASignedFormIsUploadedAndRecorded()
    {
        var world = await SeedClinicalWorldAsync();
        var version = await EnsureFormPublishedAsync(world.Admin, "Surgery");

        using var form = new MultipartFormDataContent("emr-" + Guid.NewGuid().ToString("N"));
        var part = new ByteArrayContent(Pdf());
        part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        form.Add(part, "file", "signed-consent.pdf");
        var upload = await world.Reception.PostAsync("/api/staff/files?purpose=ConsentForm", form);
        Assert.True(upload.StatusCode == HttpStatusCode.Created, await upload.Content.ReadAsStringAsync());
        var fileId = (await ReadAsync(upload)).GetProperty("fileId").GetGuid();

        var response = await world.Reception.PostAsJsonAsync("/api/staff/consents", new
        {
            catId = world.CatId, appointmentId = world.AppointmentId, kind = "Surgery", procedureDescription = "Neutering",
            method = "SignedUpload", fileId, signedByName = "Rahim Uddin", signerRelationship = "AuthorisedAgent", policyVersionNumber = version
        }, Json);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var consentId = (await ReadAsync(response)).GetProperty("consentId").GetGuid();

        var consent = await ReadAsync(await world.Nurse.GetAsync($"/api/staff/consents/{consentId}"));
        Assert.Equal("SignedUpload", consent.GetProperty("method").GetString());
        Assert.Equal("AuthorisedAgent", consent.GetProperty("signerRelationship").GetString());
        Assert.Equal(HttpStatusCode.OK, (await world.Nurse.GetAsync(consent.GetProperty("file").GetProperty("url").GetString()!)).StatusCode);

        // The same file cannot back a second consent, and a stored file a consent references cannot be deleted.
        var again = await world.Reception.PostAsJsonAsync("/api/staff/consents", new
        {
            catId = world.CatId, appointmentId = world.AppointmentId, kind = "Surgery", procedureDescription = "Again",
            method = "SignedUpload", fileId, signedByName = "Rahim Uddin", signerRelationship = "Owner", policyVersionNumber = version
        }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Equal("CR001", await SqlStateOfAsync("delete from files.stored_file where \"Id\" = @f", ("f", fileId)));
    }

    /// <summary>I-H4 -- consent is refused unless it names the form version currently in force.</summary>
    [Fact]
    public async Task I_H4_AnOutOfDateFormVersion_IsRefused()
    {
        var world = await SeedClinicalWorldAsync();
        var shown = await EnsureFormPublishedAsync(world.Admin, "Anaesthesia");

        var publish = await world.Admin.PostAsJsonAsync("/api/admin/policies/consent-anaesthesia/versions", new { body = "Updated wording.", changeSummary = "Clarified risks" }, Json);
        Assert.Equal(HttpStatusCode.Created, publish.StatusCode);

        var stale = await world.Reception.PostAsJsonAsync("/api/staff/consents", OnScreen(world, shown, "Anaesthesia"), Json);
        Assert.Equal(HttpStatusCode.BadRequest, stale.StatusCode);
        Assert.Contains(BusinessViolationCodes.ConsentFormVersionNotCurrent, await stale.Content.ReadAsStringAsync());

        var current = await EnsureFormPublishedAsync(world.Admin, "Anaesthesia");
        Assert.True(current > shown);
        Assert.Equal(HttpStatusCode.Created, (await world.Reception.PostAsJsonAsync("/api/staff/consents", OnScreen(world, current, "Anaesthesia"), Json)).StatusCode);
    }

    /// <summary>I-H5 -- the status contract: false before, true after recording, false again after withdrawal (US-CLN-21 AC2).</summary>
    [Fact]
    public async Task I_H5_TheConsentStatus_FollowsRecordingAndWithdrawal()
    {
        var world = await SeedClinicalWorldAsync();
        var version = await EnsureFormPublishedAsync(world.Admin, "Surgery");
        var status = $"/api/staff/consents/status?catId={world.CatId}&appointmentId={world.AppointmentId}&kind=Surgery";

        Assert.False((await ReadAsync(await world.Nurse.GetAsync(status))).GetProperty("hasValidConsent").GetBoolean());

        var recorded = await world.Reception.PostAsJsonAsync("/api/staff/consents", OnScreen(world, version), Json);
        var consentId = (await ReadAsync(recorded)).GetProperty("consentId").GetGuid();
        var valid = await ReadAsync(await world.Nurse.GetAsync(status));
        Assert.True(valid.GetProperty("hasValidConsent").GetBoolean());
        Assert.Equal(consentId, valid.GetProperty("consentId").GetGuid());

        // Another kind, another appointment, another cat: not valid.
        Assert.False((await ReadAsync(await world.Nurse.GetAsync($"/api/staff/consents/status?catId={world.CatId}&appointmentId={world.AppointmentId}&kind=Euthanasia"))).GetProperty("hasValidConsent").GetBoolean());
        Assert.False((await ReadAsync(await world.Nurse.GetAsync($"/api/staff/consents/status?catId={world.CatId}&appointmentId={Guid.NewGuid()}&kind=Surgery"))).GetProperty("hasValidConsent").GetBoolean());

        var withdrawn = await world.Reception.PostAsJsonAsync($"/api/staff/consents/{consentId}/withdraw", new { reason = "Owner changed their mind" }, Json);
        Assert.Equal(HttpStatusCode.NoContent, withdrawn.StatusCode);
        Assert.False((await ReadAsync(await world.Nurse.GetAsync(status))).GetProperty("hasValidConsent").GetBoolean());

        var detail = await ReadAsync(await world.Nurse.GetAsync($"/api/staff/consents/{consentId}"));
        Assert.Equal("Withdrawn", detail.GetProperty("status").GetString());
        Assert.Equal("Owner changed their mind", detail.GetProperty("withdrawalReason").GetString());

        var twice = await world.Reception.PostAsJsonAsync($"/api/staff/consents/{consentId}/withdraw", new { reason = "again" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, twice.StatusCode);
        Assert.Contains(BusinessViolationCodes.ConsentAlreadyWithdrawn, await twice.Content.ReadAsStringAsync());
    }

    /// <summary>I-H6b -- (EMR backlog row 5) the soft-delete flag is part of the frozen evidence: it cannot be set on a consent, and withdrawal still works.</summary>
    [Fact]
    public async Task I_H6b_AConsent_CannotBeMarkedToDelete_ButCanStillBeWithdrawn()
    {
        var world = await SeedClinicalWorldAsync();
        var version = await EnsureFormPublishedAsync(world.Admin, "Surgery");
        var consentId = (await ReadAsync(await world.Reception.PostAsJsonAsync("/api/staff/consents", OnScreen(world, version), Json))).GetProperty("consentId").GetGuid();

        Assert.Equal("CR001", await SqlStateOfAsync("update clinic.consent_record set \"IsMarkedToDelete\" = true where \"Id\" = @c", ("c", consentId)));
        Assert.Equal(0, await ScalarAsync<int>("select count(*) from clinic.consent_record where \"Id\" = @c and \"IsMarkedToDelete\"", ("c", consentId)));
        Assert.Equal(HttpStatusCode.NoContent, (await world.Reception.PostAsJsonAsync($"/api/staff/consents/{consentId}/withdraw", new { reason = "Withdrawn" }, Json)).StatusCode);
    }

    /// <summary>I-H6 -- the database refuses to delete a consent, to edit its evidence, or to change it once withdrawn (NFR-LEG-4).</summary>
    [Fact]
    public async Task I_H6_ARecordedConsent_CannotBeDeletedOrEdited_ThroughTheDatabase()
    {
        var world = await SeedClinicalWorldAsync();
        var version = await EnsureFormPublishedAsync(world.Admin, "Surgery");
        var consentId = (await ReadAsync(await world.Reception.PostAsJsonAsync("/api/staff/consents", OnScreen(world, version), Json))).GetProperty("consentId").GetGuid();

        Assert.Equal("CR001", await SqlStateOfAsync("delete from clinic.consent_record where \"Id\" = @c", ("c", consentId)));
        Assert.Equal("CR001", await SqlStateOfAsync("update clinic.consent_record set signed_by_name = 'Someone else' where \"Id\" = @c", ("c", consentId)));
        Assert.Equal("CR001", await SqlStateOfAsync("update clinic.consent_record set signature = '[[[0.1,0.1],[0.2,0.2]]]'::jsonb where \"Id\" = @c", ("c", consentId)));
        Assert.Equal("CR001", await SqlStateOfAsync("truncate clinic.consent_record"));

        Assert.Equal(HttpStatusCode.NoContent, (await world.Reception.PostAsJsonAsync($"/api/staff/consents/{consentId}/withdraw", new { reason = "Withdrawn" }, Json)).StatusCode);
        Assert.Equal("CR001", await SqlStateOfAsync("update clinic.consent_record set withdrawal_reason = 'rewritten' where \"Id\" = @c", ("c", consentId)));
        Assert.Equal("Withdrawn", await ScalarAsync<string>("select withdrawal_reason from clinic.consent_record where \"Id\" = @c", ("c", consentId)));
    }

    /// <summary>The refusals of §8.9: evidence that does not match the method, a malformed signature, another cat's appointment, and
    /// who may record.</summary>
    [Fact]
    public async Task Refusals_EvidenceMismatch_InvalidSignature_ForeignAppointment_AndRoles()
    {
        var world = await SeedClinicalWorldAsync();
        var version = await EnsureFormPublishedAsync(world.Admin, "Surgery");

        var withFileAndSignature = await world.Reception.PostAsJsonAsync("/api/staff/consents", new
        {
            catId = world.CatId, appointmentId = world.AppointmentId, kind = "Surgery", procedureDescription = "x", method = "OnScreenSignature",
            signature = Strokes(), fileId = Guid.NewGuid(), signedByName = "A", signerRelationship = "Owner", policyVersionNumber = version
        }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, withFileAndSignature.StatusCode);
        Assert.Contains(BusinessViolationCodes.ConsentEvidenceMismatch, await withFileAndSignature.Content.ReadAsStringAsync());

        decimal[][][] onePoint = [[[0.1m, 0.1m]]];
        var tooFew = await world.Reception.PostAsJsonAsync("/api/staff/consents", OnScreen(world, version, strokes: onePoint), Json);
        Assert.Equal(HttpStatusCode.BadRequest, tooFew.StatusCode);
        Assert.Contains(BusinessViolationCodes.InvalidSignature, await tooFew.Content.ReadAsStringAsync());

        decimal[][][] outOfRange = [[[0.1m, 0.1m], [1.2m, 0.5m], [0.3m, 0.3m], [0.4m, 0.4m], [0.5m, 0.5m], [0.6m, 0.6m], [0.7m, 0.7m], [0.8m, 0.8m], [0.9m, 0.9m], [0.95m, 0.95m]]];
        var badCoordinate = await world.Reception.PostAsJsonAsync("/api/staff/consents", OnScreen(world, version, strokes: outOfRange), Json);
        Assert.Equal(HttpStatusCode.BadRequest, badCoordinate.StatusCode);
        Assert.Contains(BusinessViolationCodes.InvalidSignature, await badCoordinate.Content.ReadAsStringAsync());

        var otherCat = await SeedCatAsync(world.CustomerId, "Other cat");
        var wrongCat = await world.Reception.PostAsJsonAsync("/api/staff/consents", new
        {
            catId = otherCat, appointmentId = world.AppointmentId, kind = "Surgery", procedureDescription = "x", method = "OnScreenSignature",
            signature = Strokes(), signedByName = "A", signerRelationship = "Owner", policyVersionNumber = version
        }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, wrongCat.StatusCode);
        Assert.Contains(BusinessViolationCodes.ConsentAppointmentInvalid, await wrongCat.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound, (await world.Reception.GetAsync($"/api/staff/consents/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await world.Reception.PostAsJsonAsync($"/api/staff/consents/{Guid.NewGuid()}/withdraw", new { reason = "x" }, Json)).StatusCode);

        // A customer holds none of the consent actions.
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Customer.PostAsJsonAsync("/api/staff/consents", OnScreen(world, version), Json)).StatusCode);
    }

    /// <summary>Reads of a recorded consent are access-logged, and another branch's consent is a 404.</summary>
    [Fact]
    public async Task ConsentReads_AreAccessLogged_AndAnotherBranchesConsentIsNotFound()
    {
        var world = await SeedClinicalWorldAsync();
        var version = await EnsureFormPublishedAsync(world.Admin, "Surgery");
        var consentId = (await ReadAsync(await world.Reception.PostAsJsonAsync("/api/staff/consents", OnScreen(world, version), Json))).GetProperty("consentId").GetGuid();

        var before = await CountAuditRowsAsync(AuditCategory.ClinicalRecordAccess, nameof(ClinicalRecordKind.Consent), consentId);
        Assert.Equal(HttpStatusCode.OK, (await world.Nurse.GetAsync($"/api/staff/consents/{consentId}")).StatusCode);
        Assert.Equal(before + 1, await CountAuditRowsAsync(AuditCategory.ClinicalRecordAccess, nameof(ClinicalRecordKind.Consent), consentId));

        var other = await SeedClinicalWorldAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await other.Reception.GetAsync($"/api/staff/consents/{consentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Reception.GetAsync($"/api/staff/consents/{consentId}/signature.svg")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Reception.PostAsJsonAsync($"/api/staff/consents/{consentId}/withdraw", new { reason = "x" }, Json)).StatusCode);

        // Recorded and withdrawn are BusinessEvent audit rows.
        Assert.Equal(1, await CountAuditRowsAsync(AuditCategory.BusinessEvent, "ConsentRecordAggregate", consentId));
    }
}
