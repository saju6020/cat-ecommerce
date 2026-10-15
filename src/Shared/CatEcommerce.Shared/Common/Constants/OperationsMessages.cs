namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-XCT-001 §3.3 / §10.3 -- plain-language messages (US-XCT-04), shared by every path that can return the same rule.</summary>
public static class OperationsMessages
{
    public const string RecordIsRetained = "This record is kept for legal reasons and cannot be deleted.";
    public const string CustomerAccountNotFound = "This customer account was not found.";
    public const string CustomerRecordMerged = "This record was merged into another customer record. Erase the surviving record instead.";
    public const string InvalidWebVitalsPayload = "The performance sample is not valid.";
    public const string ActiveBranchRequired = "Choose the branch you are working at first.";
    public const string ReapplyListTooLarge = "Send at most 500 customer ids at a time.";
    public const string RetentionRunAlreadyInProgress = "A retention run is already waiting or running. Wait for it to finish.";
    public const string RestoreTestAlreadyRecorded = "This restore test has already been recorded.";
    public const string InvalidRestoreTestTimes = "Check the times: the recovered point cannot be after the simulated failure, and the restore must finish after it started.";
    public const string RestoreTestChecksRequired = "Add at least one check result from the drill output.";
    public const string InvalidHoldDate = "A hold must end after today.";
    public const string DatabaseRoleUnsafe = "The application is connected as a role that can disable retention triggers. Use catecommerce_app (see deploy/database/roles.sql).";
}
