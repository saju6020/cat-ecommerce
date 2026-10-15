using CatEcommerce.Application.CommandHandlers.Validators;
using CatEcommerce.Application.Commands.Cat;
using CatEcommerce.Application.Commands.Vaccination;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Application.Tests.Validators;

/// <summary>U39 (HO-CAT-001 section 15) -- one invalid case per validator, asserting the
/// property name FluentValidation attaches the failure to.</summary>
public sealed class CommandValidatorsTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public void RegisterCatCommandValidator_EmptyName_FailsOnName()
    {
        var result = new RegisterCatCommandValidator(new Microsoft.Extensions.Time.Testing.FakeTimeProvider(DateTimeOffset.UtcNow)).Validate(new RegisterCatCommand
        {
            CatId = Guid.NewGuid(), OwnerId = Guid.NewGuid(), Name = "", Species = "Cat",
            Sex = CatSex.Female, DateOfBirth = Today, DateOfBirthPrecision = DateOfBirthPrecision.Exact,
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterCatCommand.Name));
    }

    [Fact]
    public void RecordCatNeuterStatusCommandValidator_NeuteredWithNoDate_FailsOnNeuteredOn()
    {
        var result = new RecordCatNeuterStatusCommandValidator(new Microsoft.Extensions.Time.Testing.FakeTimeProvider(DateTimeOffset.UtcNow)).Validate(new RecordCatNeuterStatusCommand
        {
            CatId = Guid.NewGuid(), NeuterStatus = NeuterStatus.Neutered, NeuteredOn = null,
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RecordCatNeuterStatusCommand.NeuteredOn));
    }

    [Fact]
    public void RecordCatWeightCommandValidator_OutOfRangeKilograms_FailsOnKilograms()
    {
        var result = new RecordCatWeightCommandValidator(new Microsoft.Extensions.Time.Testing.FakeTimeProvider(DateTimeOffset.UtcNow)).Validate(new RecordCatWeightCommand
        {
            WeightRecordId = Guid.NewGuid(), CatId = Guid.NewGuid(), Kilograms = 0m,
            RecordedOn = Today, RecordedByStaffId = Guid.NewGuid(),
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RecordCatWeightCommand.Kilograms));
    }

    // U40 (superseded by HO-FIL-001 §1 Amended, tidied gate-fix 2026-09-27 third pass -- content
    // type/size are no longer client-supplied on this command at all, so there is no
    // disallowed-content-type/oversize case left for this validator, or for CatAggregate.AddPhoto,
    // to exercise. Those checks now live entirely upstream, at upload time --
    // StoredFileAggregate.Register against FileRules -- covered by
    // StoredFileAggregateTests.Register_rejects_a_content_type_not_allowed_for_the_purpose and
    // .Register_rejects_a_size_over_the_purposes_cap).
    [Fact]
    public void AddCatPhotoCommandValidator_EmptyFileId_FailsOnFileId()
    {
        var result = new AddCatPhotoCommandValidator().Validate(new AddCatPhotoCommand
        {
            CatId = Guid.NewGuid(), PhotoId = Guid.NewGuid(), FileId = Guid.Empty, IsPrimary = true,
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AddCatPhotoCommand.FileId));
    }

    [Fact]
    public void FlagCatHighRiskCommandValidator_EmptyReason_FailsOnReason()
    {
        var result = new FlagCatHighRiskCommandValidator().Validate(new FlagCatHighRiskCommand
        {
            CatId = Guid.NewGuid(), HandlingNote = "Bites", Reason = "",
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(FlagCatHighRiskCommand.Reason));
    }

    [Fact]
    public void ClearCatHighRiskFlagCommandValidator_EmptyReason_FailsOnReason()
    {
        var result = new ClearCatHighRiskFlagCommandValidator().Validate(new ClearCatHighRiskFlagCommand
        {
            CatId = Guid.NewGuid(), Reason = "",
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ClearCatHighRiskFlagCommand.Reason));
    }

    // SECURITY/CODE_REVIEW gates, HIGH (2026-09-26): a Reason over 1000 characters used to pass
    // validation and only fail once the cat aggregate had already committed, when the audit
    // write hit AuditEntryConfiguration's HasMaxLength(1000). MaximumLength(1000) now catches it
    // here, before anything commits.
    [Fact]
    public void FlagCatHighRiskCommandValidator_ReasonAtMaximum_Succeeds()
    {
        var result = new FlagCatHighRiskCommandValidator().Validate(new FlagCatHighRiskCommand
        {
            CatId = Guid.NewGuid(), HandlingNote = "Bites", Reason = new string('a', 1000),
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void FlagCatHighRiskCommandValidator_ReasonOverMaximum_FailsOnReason()
    {
        var result = new FlagCatHighRiskCommandValidator().Validate(new FlagCatHighRiskCommand
        {
            CatId = Guid.NewGuid(), HandlingNote = "Bites", Reason = new string('a', 1001),
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(FlagCatHighRiskCommand.Reason));
    }

    [Fact]
    public void ClearCatHighRiskFlagCommandValidator_ReasonAtMaximum_Succeeds()
    {
        var result = new ClearCatHighRiskFlagCommandValidator().Validate(new ClearCatHighRiskFlagCommand
        {
            CatId = Guid.NewGuid(), Reason = new string('a', 1000),
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ClearCatHighRiskFlagCommandValidator_ReasonOverMaximum_FailsOnReason()
    {
        var result = new ClearCatHighRiskFlagCommandValidator().Validate(new ClearCatHighRiskFlagCommand
        {
            CatId = Guid.NewGuid(), Reason = new string('a', 1001),
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ClearCatHighRiskFlagCommand.Reason));
    }

    [Fact]
    public void RecordCatVaccinationCommandValidator_FutureDate_FailsOnAdministeredOn()
    {
        var result = new RecordCatVaccinationCommandValidator(new Microsoft.Extensions.Time.Testing.FakeTimeProvider(DateTimeOffset.UtcNow)).Validate(new RecordCatVaccinationCommand
        {
            CatId = Guid.NewGuid(), VaccinationId = Guid.NewGuid(), VaccineId = Guid.NewGuid(),
            BatchReference = "BATCH-1", BatchExpiresOn = Today.AddMonths(6),
            AdministeredOn = Today.AddDays(2), AdministeredByVetId = Guid.NewGuid(),
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RecordCatVaccinationCommand.AdministeredOn));
    }

    [Fact]
    public void RecordCatParasiteTreatmentCommandValidator_IntervalDaysOutOfRange_FailsOnIntervalDays()
    {
        var result = new RecordCatParasiteTreatmentCommandValidator(new Microsoft.Extensions.Time.Testing.FakeTimeProvider(DateTimeOffset.UtcNow)).Validate(new RecordCatParasiteTreatmentCommand
        {
            CatId = Guid.NewGuid(), TreatmentId = Guid.NewGuid(),
            TreatmentType = ParasiteTreatmentType.Deworming, ProductName = "Product",
            AdministeredOn = Today, AdministeredByStaffId = Guid.NewGuid(), IntervalDays = 0,
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors,
            e => e.PropertyName == nameof(RecordCatParasiteTreatmentCommand.IntervalDays));
    }

    [Fact]
    public void MarkCatDeceasedCommandValidator_FutureDate_FailsOnDeceasedOn()
    {
        var result = new MarkCatDeceasedCommandValidator(new Microsoft.Extensions.Time.Testing.FakeTimeProvider(DateTimeOffset.UtcNow)).Validate(new MarkCatDeceasedCommand
        {
            CatId = Guid.NewGuid(), DeceasedOn = Today.AddDays(2),
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(MarkCatDeceasedCommand.DeceasedOn));
    }

    [Fact]
    public void TransferCatOwnershipCommandValidator_EmptyReason_FailsOnReason()
    {
        var result = new TransferCatOwnershipCommandValidator().Validate(new TransferCatOwnershipCommand
        {
            CatId = Guid.NewGuid(), NewOwnerId = Guid.NewGuid(), Reason = "",
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(TransferCatOwnershipCommand.Reason));
    }

    [Fact]
    public void TransferCatOwnershipCommandValidator_ReasonAtMaximum_Succeeds()
    {
        var result = new TransferCatOwnershipCommandValidator().Validate(new TransferCatOwnershipCommand
        {
            CatId = Guid.NewGuid(), NewOwnerId = Guid.NewGuid(), Reason = new string('a', 1000),
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void TransferCatOwnershipCommandValidator_ReasonOverMaximum_FailsOnReason()
    {
        var result = new TransferCatOwnershipCommandValidator().Validate(new TransferCatOwnershipCommand
        {
            CatId = Guid.NewGuid(), NewOwnerId = Guid.NewGuid(), Reason = new string('a', 1001),
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(TransferCatOwnershipCommand.Reason));
    }

    [Fact]
    public void RegisterVaccineCommandValidator_ValidityMonthsOutOfRange_FailsOnValidityMonths()
    {
        var result = new RegisterVaccineCommandValidator().Validate(new RegisterVaccineCommand
        {
            VaccineId = Guid.NewGuid(), Code = "RABIES", Name = "Rabies", IsCore = true,
            ValidityMonths = 0,
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterVaccineCommand.ValidityMonths));
    }

    [Fact]
    public void UpdateCatMedicalProfileCommandValidator_EmptyAllergen_FailsOnAllergiesAllergen()
    {
        var result = new UpdateCatMedicalProfileCommandValidator().Validate(new UpdateCatMedicalProfileCommand
        {
            CatId = Guid.NewGuid(),
            Allergies = [new CatEcommerce.Shared.SharedDto.Cat.AllergyInput(
                "", "Hives", AllergySeverity.Mild, Today)],
            ChronicConditions = [],
            Medications = [],
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.Contains("Allergen"));
    }

    // SECURITY gate, LOW (2026-09-26, second pass): a count cap on each list, since the audit
    // column that used to bound this incidentally is now unbounded text.
    [Fact]
    public void UpdateCatMedicalProfileCommandValidator_AllergiesAtMaximum_Succeeds()
    {
        var allergies = Enumerable.Range(0, CatMedicalProfileRules.MaxListItems)
            .Select(_ => new CatEcommerce.Shared.SharedDto.Cat.AllergyInput("Chicken", "Vomiting", AllergySeverity.Mild, Today))
            .ToList();

        var result = new UpdateCatMedicalProfileCommandValidator().Validate(new UpdateCatMedicalProfileCommand
        {
            CatId = Guid.NewGuid(), Allergies = allergies, ChronicConditions = [], Medications = [],
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void UpdateCatMedicalProfileCommandValidator_AllergiesOverMaximum_FailsOnAllergies()
    {
        var allergies = Enumerable.Range(0, CatMedicalProfileRules.MaxListItems + 1)
            .Select(_ => new CatEcommerce.Shared.SharedDto.Cat.AllergyInput("Chicken", "Vomiting", AllergySeverity.Mild, Today))
            .ToList();

        var result = new UpdateCatMedicalProfileCommandValidator().Validate(new UpdateCatMedicalProfileCommand
        {
            CatId = Guid.NewGuid(), Allergies = allergies, ChronicConditions = [], Medications = [],
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateCatMedicalProfileCommand.Allergies));
    }

    [Fact]
    public void UpdateCatMedicalProfileCommandValidator_ChronicConditionsAtMaximum_Succeeds()
    {
        var conditions = Enumerable.Range(0, CatMedicalProfileRules.MaxListItems)
            .Select(_ => new CatEcommerce.Shared.SharedDto.Cat.ChronicConditionInput("Asthma", Today, null))
            .ToList();

        var result = new UpdateCatMedicalProfileCommandValidator().Validate(new UpdateCatMedicalProfileCommand
        {
            CatId = Guid.NewGuid(), Allergies = [], ChronicConditions = conditions, Medications = [],
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void UpdateCatMedicalProfileCommandValidator_ChronicConditionsOverMaximum_FailsOnChronicConditions()
    {
        var conditions = Enumerable.Range(0, CatMedicalProfileRules.MaxListItems + 1)
            .Select(_ => new CatEcommerce.Shared.SharedDto.Cat.ChronicConditionInput("Asthma", Today, null))
            .ToList();

        var result = new UpdateCatMedicalProfileCommandValidator().Validate(new UpdateCatMedicalProfileCommand
        {
            CatId = Guid.NewGuid(), Allergies = [], ChronicConditions = conditions, Medications = [],
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateCatMedicalProfileCommand.ChronicConditions));
    }

    [Fact]
    public void UpdateCatMedicalProfileCommandValidator_MedicationsAtMaximum_Succeeds()
    {
        var medications = Enumerable.Range(0, CatMedicalProfileRules.MaxListItems)
            .Select(_ => new CatEcommerce.Shared.SharedDto.Cat.MedicationInput("Prednisolone", "5mg", "Daily", Today, null))
            .ToList();

        var result = new UpdateCatMedicalProfileCommandValidator().Validate(new UpdateCatMedicalProfileCommand
        {
            CatId = Guid.NewGuid(), Allergies = [], ChronicConditions = [], Medications = medications,
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void UpdateCatMedicalProfileCommandValidator_MedicationsOverMaximum_FailsOnMedications()
    {
        var medications = Enumerable.Range(0, CatMedicalProfileRules.MaxListItems + 1)
            .Select(_ => new CatEcommerce.Shared.SharedDto.Cat.MedicationInput("Prednisolone", "5mg", "Daily", Today, null))
            .ToList();

        var result = new UpdateCatMedicalProfileCommandValidator().Validate(new UpdateCatMedicalProfileCommand
        {
            CatId = Guid.NewGuid(), Allergies = [], ChronicConditions = [], Medications = medications,
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateCatMedicalProfileCommand.Medications));
    }

    [Fact]
    public void UpdateCatProfileCommandValidator_ExactPrecisionWithNoDate_FailsOnDateOfBirth()
    {
        var result = new UpdateCatProfileCommandValidator(new Microsoft.Extensions.Time.Testing.FakeTimeProvider(DateTimeOffset.UtcNow)).Validate(new UpdateCatProfileCommand
        {
            CatId = Guid.NewGuid(), Name = "Mittens", Sex = CatSex.Female, DateOfBirth = null,
            DateOfBirthPrecision = DateOfBirthPrecision.Exact,
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateCatProfileCommand.DateOfBirth));
    }
}
