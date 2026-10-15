namespace CatEcommerce.Shared.Common.Constants;

/// <summary>Administration and Configuration (HO-ADM-001 §10.4) violation codes.</summary>
public static partial class BusinessViolationCodes
{
    public const string BranchCodeAlreadyExists = nameof(BranchCodeAlreadyExists);
    public const string InvalidTimeZone = nameof(InvalidTimeZone);
    public const string InvalidOpeningInterval = nameof(InvalidOpeningInterval);
    public const string LastActiveBranchCannotBeDeactivated = nameof(LastActiveBranchCannotBeDeactivated);
    public const string BranchNotFound = nameof(BranchNotFound);
    public const string PastClosureCannotBeCancelled = nameof(PastClosureCannotBeCancelled);
    public const string InvalidClosure = nameof(InvalidClosure);
    public const string RoomNameAlreadyExists = nameof(RoomNameAlreadyExists);
    public const string RoomNotFound = nameof(RoomNotFound);
    public const string BranchNotAssigned = nameof(BranchNotAssigned);
    public const string BranchAssignmentRequired = nameof(BranchAssignmentRequired);
    public const string StaffNotRosterable = nameof(StaffNotRosterable);
    public const string InvalidShiftSlot = nameof(InvalidShiftSlot);
    public const string InvalidPatternEffectiveDate = nameof(InvalidPatternEffectiveDate);
    public const string InvalidLeavePeriod = nameof(InvalidLeavePeriod);
    public const string LeaveOverlapsExistingLeave = nameof(LeaveOverlapsExistingLeave);
    public const string LeaveNotPending = nameof(LeaveNotPending);
    public const string LeaveCannotBeCancelled = nameof(LeaveCannotBeCancelled);
    public const string ServiceCodeAlreadyExists = nameof(ServiceCodeAlreadyExists);
    public const string InvalidServiceDuration = nameof(InvalidServiceDuration);
    public const string ServiceRolesRequired = nameof(ServiceRolesRequired);
    public const string ServiceNotFound = nameof(ServiceNotFound);
    public const string PriceCannotBeBackdated = nameof(PriceCannotBeBackdated);
    public const string DuplicatePriceEffectiveDate = nameof(DuplicatePriceEffectiveDate);
    public const string TaxClassNotFound = nameof(TaxClassNotFound);
    public const string DeliveryTaxClassRequired = nameof(DeliveryTaxClassRequired); // FU1 ORD-54: the DELIVERY tax class cannot be deactivated
    public const string UnknownSetting = nameof(UnknownSetting);
    public const string SettingNotBranchOverridable = nameof(SettingNotBranchOverridable);
    public const string InvalidSettingValue = nameof(InvalidSettingValue);
    public const string ContentSlugAlreadyExists = nameof(ContentSlugAlreadyExists);
    public const string InvalidContentMarkup = nameof(InvalidContentMarkup);
    public const string InvalidContentLink = nameof(InvalidContentLink);
    public const string PolicyNotFound = nameof(PolicyNotFound);
    public const string PolicyVersionNotCurrent = nameof(PolicyVersionNotCurrent);
    public const string PolicyKeyAlreadyExists = nameof(PolicyKeyAlreadyExists);
    public const string PolicyVersionAlreadyExists = nameof(PolicyVersionAlreadyExists);
    public const string PolicyAlreadyAccepted = nameof(PolicyAlreadyAccepted);
    public const string CatNotOwnedByCaller = nameof(CatNotOwnedByCaller);
    public const string RoleRequired = nameof(RoleRequired);
    public const string UnknownStaffRoleReferenced = nameof(UnknownStaffRoleReferenced);
    public const string RoomNotInBranch = nameof(RoomNotInBranch);
    public const string ContentNotFound = nameof(ContentNotFound);
    public const string InvalidClosureWindow = nameof(InvalidClosureWindow);
}
