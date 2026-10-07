using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.SharedDto.Cat;

namespace CatEcommerce.Domain.Tests.Aggregates;

/// <summary>
/// Domain unit test sheet, cases U1-U26 (HO-CAT-001 section 15). No database, no DI --
/// direct calls against the aggregate.
/// </summary>
public sealed class CatAggregateTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static CatRegistrationDto ValidRegistration(
        DateOnly? dob = null,
        bool useDefaultDateOfBirth = true,
        DateOfBirthPrecision? precision = null,
        string name = "Mittens") =>
        new(
            OwnerId: Guid.NewGuid(),
            Name: name,
            Species: "Cat",
            Sex: CatSex.Female,
            DateOfBirth: dob ?? (useDefaultDateOfBirth ? Today.AddYears(-2) : null),
            DateOfBirthPrecision: precision ?? DateOfBirthPrecision.Exact,
            Breed: "Domestic Shorthair",
            ColourAndMarkings: "Tabby",
            Today: Today,
            UserContext: TestUserContext.Create(),
            CorrelationId: Guid.NewGuid());

    private static CatAggregate RegisteredCat(DateOnly? dob = null)
    {
        var cat = new CatAggregate(Guid.NewGuid());
        cat.Register(ValidRegistration(dob));
        return cat;
    }

    // U1
    [Fact]
    public void Register_WithCompleteValidDto_SetsPropertiesRaisesEventAndIncrementsVersion()
    {
        var cat = new CatAggregate(Guid.NewGuid());
        var versionBefore = cat.Version;

        cat.Register(ValidRegistration());

        Assert.Equal("Mittens", cat.Name);
        Assert.Single(cat.Events);
        Assert.True(cat.Version > versionBefore);
    }

    // U2
    [Fact]
    public void Register_WithFutureDateOfBirth_Throws()
    {
        var cat = new CatAggregate(Guid.NewGuid());

        Assert.Throws<DomainRuleViolationException>(
            () => cat.Register(ValidRegistration(dob: Today.AddDays(1))));
    }

    // U3
    [Fact]
    public void Register_ExactPrecisionWithNoDateOfBirth_Throws()
    {
        var cat = new CatAggregate(Guid.NewGuid());

        var ex = Assert.Throws<DomainRuleViolationException>(
            () => cat.Register(ValidRegistration(useDefaultDateOfBirth: false, precision: DateOfBirthPrecision.Exact)));

        Assert.Equal(BusinessViolationCodes.DateOfBirthRequired, ex.ErrorCode);
    }

    // U4
    [Fact]
    public void Register_EstimatedPrecisionWithNoDateOfBirth_Succeeds()
    {
        var cat = new CatAggregate(Guid.NewGuid());

        cat.Register(ValidRegistration(useDefaultDateOfBirth: false, precision: DateOfBirthPrecision.Estimated));

        Assert.Null(cat.DateOfBirth);
    }

    // U5
    [Fact]
    public void RecordNeuterStatus_NeuteredWithNoDate_Throws()
    {
        var cat = RegisteredCat();

        Assert.Throws<DomainRuleViolationException>(() => cat.RecordNeuterStatus(
            new CatNeuterStatusDto(NeuterStatus.Neutered, null, Today, TestUserContext.Create(), Guid.NewGuid())));
    }

    // U6
    [Fact]
    public void RecordNeuterStatus_DateBeforeDateOfBirth_Throws()
    {
        var dob = Today.AddYears(-2);
        var cat = RegisteredCat(dob);

        Assert.Throws<DomainRuleViolationException>(() => cat.RecordNeuterStatus(
            new CatNeuterStatusDto(NeuterStatus.Neutered, dob.AddDays(-1), Today, TestUserContext.Create(), Guid.NewGuid())));
    }

    // U7
    [Fact]
    public void RecordNeuterStatus_DateInFuture_Throws()
    {
        var cat = RegisteredCat();

        Assert.Throws<DomainRuleViolationException>(() => cat.RecordNeuterStatus(
            new CatNeuterStatusDto(NeuterStatus.Neutered, Today.AddDays(1), Today, TestUserContext.Create(), Guid.NewGuid())));
    }

    [Fact]
    public void RecordNeuterStatus_Valid_Succeeds()
    {
        var cat = RegisteredCat();

        cat.RecordNeuterStatus(
            new CatNeuterStatusDto(NeuterStatus.Neutered, Today, Today, TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(NeuterStatus.Neutered, cat.NeuterStatus);
        Assert.Equal(Today, cat.NeuteredOn);
    }

    private static CatPhotoDto PhotoDto(bool isPrimary = false) =>
        new(Guid.NewGuid(), Guid.NewGuid(), isPrimary, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid());

    // U8/U9 (content-type/size checks moved to StoredFileAggregate.Register -- gate-fix 2026-09-27,
    // ARCHITECTURE HIGH: CatPhotoDto no longer carries those fields at all, see
    // StoredFileAggregateTests for the equivalent coverage on FIL's own aggregate).

    // U10
    [Fact]
    public void AddPhoto_EleventhPhoto_ThrowsPhotoLimitExceeded()
    {
        var cat = RegisteredCat();
        for (var i = 0; i < CatPhotoRules.MaxPhotosPerCat; i++)
            cat.AddPhoto(PhotoDto());

        var ex = Assert.Throws<DomainRuleViolationException>(() => cat.AddPhoto(PhotoDto()));

        Assert.Equal(BusinessViolationCodes.PhotoLimitExceeded, ex.ErrorCode);
    }

    // U11
    [Fact]
    public void AddPhoto_First_BecomesPrimary()
    {
        var cat = RegisteredCat();

        cat.AddPhoto(PhotoDto(isPrimary: false));

        Assert.True(cat.Photos.Single().IsPrimary);
    }

    private static CatVaccinationDto VaccinationDto(
        Guid? vaccineId = null, DateOnly? administeredOn = null, DateOnly? batchExpiresOn = null,
        int validityMonths = 12) =>
        new(
            VaccinationId: Guid.NewGuid(),
            VaccineId: vaccineId ?? Guid.NewGuid(),
            VaccineCode: "RABIES",
            VaccineName: "Rabies",
            IsCore: true,
            ValidityMonths: validityMonths,
            BatchReference: "BATCH-1",
            BatchExpiresOn: batchExpiresOn ?? Today.AddMonths(6),
            AdministeredOn: administeredOn ?? Today,
            AdministeredByVetId: Guid.NewGuid(),
            Notes: null,
            Today: Today,
            UserContext: TestUserContext.Create(),
            CorrelationId: Guid.NewGuid());

    // U12
    [Fact]
    public void RecordVaccination_BatchExpiredBeforeAdministration_ThrowsExpiredVaccineBatch()
    {
        var cat = RegisteredCat();

        var ex = Assert.Throws<DomainRuleViolationException>(() => cat.RecordVaccination(
            VaccinationDto(administeredOn: Today, batchExpiresOn: Today.AddDays(-1))));

        Assert.Equal(BusinessViolationCodes.ExpiredVaccineBatch, ex.ErrorCode);
    }

    // U13
    [Fact]
    public void RecordVaccination_BatchExpiresOnSameDayAsAdministration_Succeeds()
    {
        var cat = RegisteredCat();

        cat.RecordVaccination(VaccinationDto(administeredOn: Today, batchExpiresOn: Today));

        Assert.Single(cat.Vaccinations);
    }

    // U14
    [Fact]
    public void RecordVaccination_Valid_ComputesNextDueOnFromValidityMonths()
    {
        var cat = RegisteredCat();

        cat.RecordVaccination(VaccinationDto(administeredOn: Today, validityMonths: 12));

        Assert.Equal(Today.AddMonths(12), cat.Vaccinations.Single().NextDueOn);
    }

    // U15
    [Fact]
    public void RecordVaccination_SameVaccineSameCatSameDay_ThrowsDuplicateVaccinationOnDate()
    {
        var cat = RegisteredCat();
        var vaccineId = Guid.NewGuid();
        cat.RecordVaccination(VaccinationDto(vaccineId: vaccineId, administeredOn: Today));

        var ex = Assert.Throws<DomainRuleViolationException>(
            () => cat.RecordVaccination(VaccinationDto(vaccineId: vaccineId, administeredOn: Today)));

        Assert.Equal(BusinessViolationCodes.DuplicateVaccinationOnDate, ex.ErrorCode);
    }

    // U16
    [Fact]
    public void RecordVaccination_SameVaccineDifferentDay_Succeeds()
    {
        var cat = RegisteredCat();
        var vaccineId = Guid.NewGuid();
        cat.RecordVaccination(VaccinationDto(vaccineId: vaccineId, administeredOn: Today.AddDays(-30)));

        cat.RecordVaccination(VaccinationDto(vaccineId: vaccineId, administeredOn: Today));

        Assert.Equal(2, cat.Vaccinations.Count);
    }

    // U17
    [Fact]
    public void RecordParasiteTreatment_IntervalDays90_ComputesNextDueOn()
    {
        var cat = RegisteredCat();

        cat.RecordParasiteTreatment(new CatParasiteTreatmentDto(
            Guid.NewGuid(), ParasiteTreatmentType.Deworming, "Product", "BATCH-1",
            Today, Guid.NewGuid(), 90, null, Today, TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(Today.AddDays(90), cat.ParasiteTreatments.Single().NextDueOn);
    }

    // U18
    [Fact]
    public void FlagHighRisk_SetsFlagAndRaisesEventWithReason()
    {
        var cat = RegisteredCat();

        cat.FlagHighRisk(new CatHighRiskFlagDto("Bites when restrained", "Aggressive during exams",
            TestUserContext.Create(), Guid.NewGuid()));

        Assert.True(cat.IsHighRisk);
        Assert.Equal("Bites when restrained", cat.HandlingNote);
        var raised = Assert.IsType<CatEcommerce.Domain.Events.Cat.CatFlaggedHighRiskEvent>(cat.Events.Last());
        Assert.Equal("Aggressive during exams", raised.Reason);
    }

    // U19
    [Fact]
    public void ClearHighRiskFlag_WhenNotFlagged_ThrowsCatNotFlaggedHighRisk()
    {
        var cat = RegisteredCat();

        var ex = Assert.Throws<DomainRuleViolationException>(() => cat.ClearHighRiskFlag(
            new CatHighRiskClearDto("No longer aggressive", TestUserContext.Create(), Guid.NewGuid())));

        Assert.Equal(BusinessViolationCodes.CatNotFlaggedHighRisk, ex.ErrorCode);
    }

    // U20
    [Fact]
    public void MarkDeceased_ThenUpdateProfile_ThrowsCatIsDeceased()
    {
        var cat = RegisteredCat();
        cat.MarkDeceased(new CatDeceasedDto(Today, null, Today, TestUserContext.Create(), Guid.NewGuid()));

        var ex = Assert.Throws<DomainRuleViolationException>(() => cat.UpdateProfile(
            new CatProfileUpdateDto("Mittens", CatSex.Female, Today.AddYears(-2), DateOfBirthPrecision.Exact,
                null, null, Today, TestUserContext.Create(), Guid.NewGuid())));

        Assert.Equal(BusinessViolationCodes.CatIsDeceased, ex.ErrorCode);
    }

    // U21
    [Fact]
    public void MarkDeceased_Twice_ThrowsCatAlreadyDeceased()
    {
        var cat = RegisteredCat();
        cat.MarkDeceased(new CatDeceasedDto(Today, null, Today, TestUserContext.Create(), Guid.NewGuid()));

        var ex = Assert.Throws<DomainRuleViolationException>(() => cat.MarkDeceased(
            new CatDeceasedDto(Today, null, Today, TestUserContext.Create(), Guid.NewGuid())));

        Assert.Equal(BusinessViolationCodes.CatAlreadyDeceased, ex.ErrorCode);
    }

    // U22
    [Fact]
    public void MarkDeceased_FutureDate_Throws()
    {
        var cat = RegisteredCat();

        Assert.Throws<DomainRuleViolationException>(() => cat.MarkDeceased(
            new CatDeceasedDto(Today.AddDays(1), null, Today, TestUserContext.Create(), Guid.NewGuid())));
    }

    // U23
    [Fact]
    public void TransferOwnership_ToCurrentOwner_ThrowsOwnerUnchanged()
    {
        var registration = ValidRegistration();
        var cat = new CatAggregate(Guid.NewGuid());
        cat.Register(registration);

        var ex = Assert.Throws<DomainRuleViolationException>(() => cat.TransferOwnership(
            new CatOwnershipTransferDto(registration.OwnerId, "Moving house", TestUserContext.Create(), Guid.NewGuid())));

        Assert.Equal(BusinessViolationCodes.OwnerUnchanged, ex.ErrorCode);
    }

    // U24
    [Fact]
    public void TransferOwnership_Valid_ChangesOwnerRaisesEventAndKeepsCollectionsIntact()
    {
        var cat = RegisteredCat();
        cat.RecordParasiteTreatment(new CatParasiteTreatmentDto(
            Guid.NewGuid(), ParasiteTreatmentType.Deworming, "Product", null,
            Today, Guid.NewGuid(), 90, null, Today, TestUserContext.Create(), Guid.NewGuid()));
        var previousOwnerId = cat.OwnerId;
        var newOwnerId = Guid.NewGuid();

        cat.TransferOwnership(new CatOwnershipTransferDto(newOwnerId, "Adopted by relative",
            TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(newOwnerId, cat.OwnerId);
        Assert.Single(cat.ParasiteTreatments);
        var raised = Assert.IsType<CatEcommerce.Domain.Events.Cat.CatOwnershipTransferredEvent>(cat.Events.Last());
        Assert.Equal(previousOwnerId, raised.PreviousOwnerId);
        Assert.Equal(newOwnerId, raised.NewOwnerId);
    }

    // NTF-001 backlog row 1: each owner change moves the generation, so an A-B-A round trip is distinguishable.
    [Fact]
    public void TransferOwnership_AroundTheHouses_EachChangeIncrementsOwnershipGeneration()
    {
        var cat = RegisteredCat();
        var original = cat.OwnerId;
        Assert.Equal(0, cat.OwnershipGeneration);

        cat.TransferOwnership(new CatOwnershipTransferDto(Guid.NewGuid(), "Sold", TestUserContext.Create(), Guid.NewGuid()));
        Assert.Equal(1, cat.OwnershipGeneration);

        cat.TransferOwnership(new CatOwnershipTransferDto(original, "Returned", TestUserContext.Create(), Guid.NewGuid()));
        Assert.Equal(original, cat.OwnerId);
        Assert.Equal(2, cat.OwnershipGeneration);
    }

    // U25
    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void Weight_Create_OutOfRange_Throws(decimal kilograms)
    {
        Assert.Throws<DomainRuleViolationException>(
            () => CatEcommerce.Domain.ValueObjects.Weight.Create(kilograms));
    }

    // U26 -- CODE_REVIEW gate, LOW (2026-09-26): broadened from a single FlagHighRisk call to
    // every one of the eleven mutation methods §8.2 lists, in an order that satisfies each
    // method's own preconditions (MarkDeceased last, since EnsureAlive forecloses everything
    // after it). Each step is asserted independently, so a method that stops raising its event
    // fails on its own line, not just the running total.
    [Fact]
    public void EveryMutationMethod_RaisesExactlyOneEventAndIncrementsVersion()
    {
        var cat = new CatAggregate(Guid.NewGuid());
        AssertRaisesOneEventAndIncrementsVersion(cat, () => cat.Register(ValidRegistration())); // 1

        AssertRaisesOneEventAndIncrementsVersion(cat, () => cat.UpdateProfile(
            new CatProfileUpdateDto("Whiskers", CatSex.Female, Today.AddYears(-2), DateOfBirthPrecision.Exact,
                null, null, Today, TestUserContext.Create(), Guid.NewGuid()))); // 2

        AssertRaisesOneEventAndIncrementsVersion(cat, () => cat.RecordNeuterStatus(
            new CatNeuterStatusDto(NeuterStatus.Neutered, Today, Today, TestUserContext.Create(), Guid.NewGuid()))); // 3

        AssertRaisesOneEventAndIncrementsVersion(cat, () => cat.UpdateMedicalProfile(
            new CatMedicalProfileDto([], [], [], "Grain-free", "Calm",
                TestUserContext.Create(), Guid.NewGuid()))); // 4

        AssertRaisesOneEventAndIncrementsVersion(cat, () => cat.AddPhoto(PhotoDto())); // 5

        AssertRaisesOneEventAndIncrementsVersion(cat, () => cat.FlagHighRisk(
            new CatHighRiskFlagDto("Note", "Reason", TestUserContext.Create(), Guid.NewGuid()))); // 6

        AssertRaisesOneEventAndIncrementsVersion(cat, () => cat.ClearHighRiskFlag(
            new CatHighRiskClearDto("Resolved", TestUserContext.Create(), Guid.NewGuid()))); // 7

        AssertRaisesOneEventAndIncrementsVersion(cat, () => cat.RecordVaccination(VaccinationDto())); // 8

        AssertRaisesOneEventAndIncrementsVersion(cat, () => cat.RecordParasiteTreatment(
            new CatParasiteTreatmentDto(Guid.NewGuid(), ParasiteTreatmentType.Deworming, "Product", null,
                Today, Guid.NewGuid(), 90, null, Today, TestUserContext.Create(), Guid.NewGuid()))); // 9

        AssertRaisesOneEventAndIncrementsVersion(cat, () => cat.TransferOwnership(
            new CatOwnershipTransferDto(Guid.NewGuid(), "Adopted by relative",
                TestUserContext.Create(), Guid.NewGuid()))); // 10

        AssertRaisesOneEventAndIncrementsVersion(cat, () => cat.MarkDeceased(
            new CatDeceasedDto(Today, null, Today, TestUserContext.Create(), Guid.NewGuid()))); // 11

        Assert.Equal(11, cat.Events.Count);
    }

    private static void AssertRaisesOneEventAndIncrementsVersion(CatAggregate cat, Action mutate)
    {
        var eventCountBefore = cat.Events.Count;
        var versionBefore = cat.Version;

        mutate();

        Assert.Equal(eventCountBefore + 1, cat.Events.Count);
        Assert.True(cat.Version > versionBefore);
    }
}
