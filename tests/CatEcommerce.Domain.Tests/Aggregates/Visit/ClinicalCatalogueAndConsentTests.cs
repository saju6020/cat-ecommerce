using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.Aggregates.Visit;

/// <summary>HO-EMR-001 §15 U-A1..U-A4 (catalogues) and U-H2, U-H3 (consent).</summary>
public sealed class ClinicalCatalogueAndConsentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);

    // ── diagnosis codes ────────────────────────────────────────────────────────────────────────

    private static DiagnosisCodeCreateDto NewCode(string code, string name = "Otitis externa") =>
        new(code, name, null, null, null, TestUserContext.Create(), Guid.NewGuid());

    [Theory]
    [InlineData("otitis")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-LEADING")]
    [InlineData("HAS SPACE")]
    public void DiagnosisCode_LowerCaseBlankOrMalformedCode_IsRefused(string code)
    {
        var aggregate = new DiagnosisCodeAggregate(Guid.NewGuid());
        Assert.Equal(BusinessViolationCodes.InvalidDiagnosisCode, Assert.Throws<DomainRuleViolationException>(() => aggregate.Create(NewCode(code))).ErrorCode);
    }

    [Fact]
    public void DiagnosisCode_ACodeLongerThanThirtyCharacters_IsRefused()
    {
        var aggregate = new DiagnosisCodeAggregate(Guid.NewGuid());
        Assert.Throws<DomainRuleViolationException>(() => aggregate.Create(NewCode(new string('A', 31))));
    }

    [Fact]
    public void DiagnosisCode_Valid_IsCreatedActive()
    {
        var aggregate = new DiagnosisCodeAggregate(Guid.NewGuid());
        aggregate.Create(NewCode("OTITIS-EXT.1"));

        Assert.True(aggregate.IsActive);
        Assert.False(aggregate.IsSystem);
    }

    [Fact]
    public void DiagnosisCode_TheSystemCode_CanBeNeitherRenamedNorDeactivated()
    {
        var system = new DiagnosisCodeAggregate(DiagnosisCodeSeed.RoutineNoAbnormality.Id);
        system.Create(NewCode(DiagnosisCodeSeed.RoutineNoAbnormality.Code, DiagnosisCodeSeed.RoutineNoAbnormality.Name));

        var deactivate = Assert.Throws<DomainRuleViolationException>(() =>
            system.SetActive(new CatalogueActiveDto(false, TestUserContext.Create(), Guid.NewGuid())));
        var rename = Assert.Throws<DomainRuleViolationException>(() =>
            system.Update(new DiagnosisCodeUpdateDto("Other", null, null, null, TestUserContext.Create(), Guid.NewGuid())));

        Assert.Equal(BusinessViolationCodes.SystemDiagnosisCodeReadOnly, deactivate.ErrorCode);
        Assert.Equal(BusinessViolationCodes.SystemDiagnosisCodeReadOnly, rename.ErrorCode);
        Assert.True(system.IsActive);
    }

    // ── lab analytes ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void LabAnalyte_LowAboveHigh_OrNumericWithoutUnit_IsRefused()
    {
        var aggregate = new LabAnalyteAggregate(Guid.NewGuid());
        LabAnalyteCreateDto Dto(string? unit, decimal? low, decimal? high) =>
            new("CREA", "Creatinine", ResultValueKind.Numeric, DiagnosticKind.BloodWork, unit, low, high, null, TestUserContext.Create(), Guid.NewGuid());

        Assert.Equal(BusinessViolationCodes.InvalidReferenceRange, Assert.Throws<DomainRuleViolationException>(() => aggregate.Create(Dto("mg/dL", 5m, 2m))).ErrorCode);
        Assert.Equal(BusinessViolationCodes.InvalidReferenceRange, Assert.Throws<DomainRuleViolationException>(() => aggregate.Create(Dto(null, 1m, 2m))).ErrorCode);

        aggregate.Create(Dto("mg/dL", 0.8m, 2.4m));
        Assert.Equal(0.8m, aggregate.ReferenceLow);
    }

    [Fact]
    public void LabAnalyte_Qualitative_KeepsTheNormalTextAndNoNumericRange()
    {
        var aggregate = new LabAnalyteAggregate(Guid.NewGuid());
        aggregate.Create(new LabAnalyteCreateDto("FELV", "FeLV antigen", ResultValueKind.Qualitative, DiagnosticKind.FelvFivTest, "ignored", 1m, 2m, "Negative",
            TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal("Negative", aggregate.NormalText);
        Assert.Null(aggregate.ReferenceLow);
        Assert.Null(aggregate.Unit);
    }

    // ── note templates (U-A4) ──────────────────────────────────────────────────────────────────

    [Fact]
    public void NoteTemplate_EveryUpdate_RaisesTheTemplateVersionByOne()
    {
        var template = new NoteTemplateAggregate(Guid.NewGuid());
        template.Create(new NoteTemplateCreateDto(Guid.NewGuid(), "Vaccination", "s", "o", "a", "p", "d", TestUserContext.Create(), Guid.NewGuid()));
        Assert.Equal(1, template.TemplateVersion);

        template.Update(new NoteTemplateUpdateDto("Vaccination v2", "s2", "o", "a", "p", "d", TestUserContext.Create(), Guid.NewGuid()));
        template.Update(new NoteTemplateUpdateDto("Vaccination v3", "s3", "o", "a", "p", "d", TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(3, template.TemplateVersion);
        Assert.Equal("s3", template.Subjective);
    }

    [Fact]
    public void NoteTemplate_ABlankName_OrAnOversizedText_IsRefused()
    {
        var template = new NoteTemplateAggregate(Guid.NewGuid());

        Assert.Throws<DomainRuleViolationException>(() =>
            template.Create(new NoteTemplateCreateDto(Guid.NewGuid(), " ", "s", "o", "a", "p", "d", TestUserContext.Create(), Guid.NewGuid())));
        Assert.Throws<DomainRuleViolationException>(() =>
            template.Create(new NoteTemplateCreateDto(Guid.NewGuid(), "n", new string('x', ClinicalNoteRules.MaxSoapFieldLength + 1), "o", "a", "p", "d", TestUserContext.Create(), Guid.NewGuid())));
    }

    // ── consent (U-H2, U-H3) ───────────────────────────────────────────────────────────────────

    private static SignatureStrokes Signature() => SignatureStrokes.Create([[[0.1m, 0.1m], [0.2m, 0.3m], [0.3m, 0.2m], [0.4m, 0.5m], [0.5m, 0.5m], [0.6m, 0.4m]], [[0.2m, 0.7m], [0.4m, 0.8m], [0.6m, 0.7m], [0.8m, 0.9m]]]);

    private static ConsentRecordingDto Recording(
        ConsentMethod? method = null, SignatureStrokes? signature = null, Guid? fileId = null, Guid? catId = null, Guid? appointmentCatId = null,
        AppointmentStatus? appointmentStatus = null, int version = 2, int? current = 2, CatLifeStatus? life = null, ConsentKind? kind = null)
    {
        var cat = catId ?? Guid.NewGuid();
        return new ConsentRecordingDto(
            cat, Guid.NewGuid(), appointmentCatId ?? cat, appointmentStatus ?? AppointmentStatus.Booked, Guid.NewGuid(), life ?? CatLifeStatus.Alive,
            kind ?? ConsentKind.Surgery, "Spay", method ?? ConsentMethod.OnScreenSignature, signature, fileId, "A. Owner", SignerRelationship.Owner, null,
            ConsentForms.KeyFor(kind ?? ConsentKind.Surgery), version, current, Guid.NewGuid(), null, Now, TestUserContext.Create(), Guid.NewGuid());
    }

    private static void ExpectCode(string code, Action action) =>
        Assert.Equal(code, Assert.Throws<DomainRuleViolationException>(action).ErrorCode);

    [Fact]
    public void Consent_OnScreenWithAFile_AndUploadWithStrokes_AreEvidenceMismatches()
    {
        var consent = new ConsentRecordAggregate(Guid.NewGuid());

        ExpectCode(BusinessViolationCodes.ConsentEvidenceMismatch, () => consent.Record(Recording(ConsentMethod.OnScreenSignature, Signature(), Guid.NewGuid())));
        ExpectCode(BusinessViolationCodes.ConsentEvidenceMismatch, () => consent.Record(Recording(ConsentMethod.SignedUpload, Signature(), null)));
        ExpectCode(BusinessViolationCodes.ConsentEvidenceMismatch, () => consent.Record(Recording(ConsentMethod.OnScreenSignature, null, null)));
        ExpectCode(BusinessViolationCodes.ConsentEvidenceMismatch, () => consent.Record(Recording(ConsentMethod.SignedUpload, null, null)));
    }

    [Fact]
    public void Consent_Valid_RecordsTheVersionSigned_AndRaisesTheEvent()
    {
        var consent = new ConsentRecordAggregate(Guid.NewGuid());

        consent.Record(Recording(signature: Signature()));

        Assert.Equal(ConsentStatus.Recorded, consent.Status);
        Assert.Equal(2, consent.PolicyVersionNumber);
        Assert.Equal(ConsentForms.Surgery, consent.PolicyKey);
        Assert.Equal(Now, consent.SignedOn);
        Assert.Single(consent.Events);
    }

    [Fact]
    public void Consent_AnotherCatsAppointment_ACancelledOne_ADeceasedCat_AndAStaleFormVersion_AreRefused()
    {
        var consent = new ConsentRecordAggregate(Guid.NewGuid());

        ExpectCode(BusinessViolationCodes.ConsentAppointmentInvalid, () => consent.Record(Recording(signature: Signature(), appointmentCatId: Guid.NewGuid())));
        ExpectCode(BusinessViolationCodes.ConsentAppointmentInvalid, () => consent.Record(Recording(signature: Signature(), appointmentStatus: AppointmentStatus.Cancelled)));
        ExpectCode(BusinessViolationCodes.CatIsDeceased, () => consent.Record(Recording(signature: Signature(), life: CatLifeStatus.Deceased)));
        ExpectCode(BusinessViolationCodes.ConsentFormVersionNotCurrent, () => consent.Record(Recording(signature: Signature(), version: 1, current: 2)));
        ExpectCode(BusinessViolationCodes.ConsentFormVersionNotCurrent, () => consent.Record(Recording(signature: Signature(), version: 1, current: null)));
    }

    [Fact]
    public void Consent_Withdraw_OnceOnly_AndNeedsAReason()
    {
        var consent = new ConsentRecordAggregate(Guid.NewGuid());
        consent.Record(Recording(signature: Signature()));
        ConsentWithdrawalDto Withdrawal(string reason) => new(reason, Guid.NewGuid(), Now.AddHours(1), TestUserContext.Create(), Guid.NewGuid());

        ExpectCode(BusinessViolationCodes.ClinicalTextTooLong, () => consent.Withdraw(Withdrawal(" ")));

        consent.Withdraw(Withdrawal("Owner changed their mind"));
        Assert.Equal(ConsentStatus.Withdrawn, consent.Status);

        ExpectCode(BusinessViolationCodes.ConsentAlreadyWithdrawn, () => consent.Withdraw(Withdrawal("again")));
    }

    [Fact]
    public void ConsentRules_IsValidFor_OnlyAMatchingRecordedConsent()
    {
        var recorded = new ConsentRecordAggregate(Guid.NewGuid());
        var dto = Recording(signature: Signature());
        recorded.Record(dto);

        Assert.True(ConsentRules.IsValidFor(recorded, dto.CatId, dto.AppointmentId, ConsentKind.Surgery));
        Assert.False(ConsentRules.IsValidFor(recorded, dto.CatId, Guid.NewGuid(), ConsentKind.Surgery));
        Assert.False(ConsentRules.IsValidFor(recorded, Guid.NewGuid(), dto.AppointmentId, ConsentKind.Surgery));
        Assert.False(ConsentRules.IsValidFor(recorded, dto.CatId, dto.AppointmentId, ConsentKind.Anaesthesia));

        recorded.Withdraw(new ConsentWithdrawalDto("changed mind", Guid.NewGuid(), Now, TestUserContext.Create(), Guid.NewGuid()));
        Assert.False(ConsentRules.IsValidFor(recorded, dto.CatId, dto.AppointmentId, ConsentKind.Surgery));
    }

    [Fact]
    public void ConsentForms_KeyFor_MapsEveryKind()
    {
        Assert.Equal("consent-surgery", ConsentForms.KeyFor(ConsentKind.Surgery));
        Assert.Equal("consent-anaesthesia", ConsentForms.KeyFor(ConsentKind.Anaesthesia));
        Assert.Equal("consent-euthanasia", ConsentForms.KeyFor(ConsentKind.Euthanasia));
    }
}
