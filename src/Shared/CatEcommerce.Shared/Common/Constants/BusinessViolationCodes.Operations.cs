namespace CatEcommerce.Shared.Common.Constants;

/// <summary>Cross-cutting / Operations (HO-XCT-001 §10.4) violation codes.</summary>
public static partial class BusinessViolationCodes
{
    public const string RecordIsRetained = nameof(RecordIsRetained);
    public const string CustomerAccountNotFound = nameof(CustomerAccountNotFound);
    public const string InvalidHoldDate = nameof(InvalidHoldDate);
    public const string ErasureRequestNotFound = nameof(ErasureRequestNotFound);
    public const string ErasureScopeNotFound = nameof(ErasureScopeNotFound);
    public const string CustomerRecordMerged = nameof(CustomerRecordMerged);
    public const string ReapplyListTooLarge = nameof(ReapplyListTooLarge);
    public const string RetentionRunAlreadyInProgress = nameof(RetentionRunAlreadyInProgress);
    public const string RestoreTestAlreadyRecorded = nameof(RestoreTestAlreadyRecorded);
    public const string InvalidRestoreTestTimes = nameof(InvalidRestoreTestTimes);
    public const string RestoreTestChecksRequired = nameof(RestoreTestChecksRequired);
    public const string SupersededRestoreTestNotFound = nameof(SupersededRestoreTestNotFound);
    public const string PurgeSourceNotRegistered = nameof(PurgeSourceNotRegistered);
    public const string InvalidWebVitalsPayload = nameof(InvalidWebVitalsPayload);
    public const string ActiveBranchRequired = nameof(ActiveBranchRequired);
    public const string InvalidRetentionRun = nameof(InvalidRetentionRun);
}
