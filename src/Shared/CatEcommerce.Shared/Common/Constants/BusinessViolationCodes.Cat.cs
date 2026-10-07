namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// Cat Profiles (HO-CAT-001) violation codes. The set specified in HO-CAT-001 §10 plus the
/// additional per-property codes needed to cover every invariant in §8.2's mutation table
/// one-for-one. See ADR-341 for the per-area partial pattern.
/// </summary>
public static partial class BusinessViolationCodes
{
    public const string CatNotFound = nameof(CatNotFound);
    public const string VaccineNotFound = nameof(VaccineNotFound);
    public const string VaccineCodeAlreadyExists = nameof(VaccineCodeAlreadyExists);
    public const string ExpiredVaccineBatch = nameof(ExpiredVaccineBatch);
    public const string DuplicateVaccinationOnDate = nameof(DuplicateVaccinationOnDate);
    public const string CatIsDeceased = nameof(CatIsDeceased);
    public const string CatAlreadyDeceased = nameof(CatAlreadyDeceased);
    public const string CatNotFlaggedHighRisk = nameof(CatNotFlaggedHighRisk);
    public const string OwnerUnchanged = nameof(OwnerUnchanged);
    public const string PhotoLimitExceeded = nameof(PhotoLimitExceeded);
    public const string InactiveVaccine = nameof(InactiveVaccine);

    // Additional per-property codes needed by §8.2's mutation invariants table.
    public const string InvalidWeight = nameof(InvalidWeight);
    public const string NameRequired = nameof(NameRequired);
    public const string DateOfBirthInFuture = nameof(DateOfBirthInFuture);
    public const string DateOfBirthRequired = nameof(DateOfBirthRequired);
    public const string NeuterDateRequired = nameof(NeuterDateRequired);
    public const string NeuterDateInFuture = nameof(NeuterDateInFuture);
    public const string NeuterDateBeforeDateOfBirth = nameof(NeuterDateBeforeDateOfBirth);
    public const string HandlingNoteRequired = nameof(HandlingNoteRequired);
    public const string ReasonRequired = nameof(ReasonRequired);
    public const string VaccinationDateInFuture = nameof(VaccinationDateInFuture);
    public const string VaccinationDateBeforeDateOfBirth = nameof(VaccinationDateBeforeDateOfBirth);
    public const string ParasiteTreatmentDateInFuture = nameof(ParasiteTreatmentDateInFuture);
    public const string DeceasedDateInFuture = nameof(DeceasedDateInFuture);
    public const string DeceasedDateBeforeDateOfBirth = nameof(DeceasedDateBeforeDateOfBirth);
    public const string ValidityMonthsOutOfRange = nameof(ValidityMonthsOutOfRange);
    public const string WeightRecordDateInFuture = nameof(WeightRecordDateInFuture);
    public const string CodeRequired = nameof(CodeRequired);

    // SECURITY gate, MEDIUM (2026-09-26): AdministeredByVetId/AdministeredByStaffId name a
    // colleague, so they must resolve to a real, active staff account -- StaffAccountNotFound is
    // reused from BusinessViolationCodes.Accounts.cs (the same partial class). This one is
    // Cat-specific: the vet field must additionally hold the Vet role.
    public const string StaffAccountNotVeterinarian = nameof(StaffAccountNotVeterinarian);

    // ARCHITECTURE/CODE_REVIEW gates, LOW (2026-09-26): an unknown Enumeration string or an
    // out-of-range pageSize now returns the standard ValidationResult 400 shape (ADR-014)
    // instead of a bespoke `{ error }` object -- CatsController.TryParseEnum/ApplyPaging.
    public const string InvalidEnumValue = nameof(InvalidEnumValue);
    public const string InvalidPageSize = nameof(InvalidPageSize);

    // ARCHITECTURE/CODE_REVIEW/SECURITY gates, second pass (2026-09-26): pageNumber had no upper
    // bound, so a very large value overflowed Query<T>.Skip's (PageNumber-1)*PageSize to a
    // negative OFFSET Postgres rejects with a 500 -- PagingHelper.ApplyPaging now caps it at
    // PagingRules.MaxPageNumber, the same shape as InvalidPageSize.
    public const string InvalidPageNumber = nameof(InvalidPageNumber);

    /// <summary>HO-FIL-001 §1 Amended (integrator-authorised, closes HO-CAT-002-SECURITY): the
    /// referenced <c>FileId</c> does not exist, is not a <c>CatProfilePhoto</c>, was not uploaded
    /// by the calling staff member, or was uploaded too long ago to still be attached
    /// (<c>FileRules.AttachWindow</c>). One code for all four reasons, deliberately -- distinguishing
    /// them in the response would let a caller probe which file ids exist.</summary>
    public const string PhotoFileNotEligible = nameof(PhotoFileNotEligible);
}
