namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// Accounts and Identity (HO-ACC-001 §10.3) violation codes. See ADR-341 for the per-area
/// partial pattern. Do not reorder or rename another area's codes.
/// </summary>
public static partial class BusinessViolationCodes
{
    // Identifiers & registration
    // NameRequired is already declared in BusinessViolationCodes.Cat.cs -- reused verbatim.
    public const string IdentifierRequired = nameof(IdentifierRequired);
    public const string InvalidMobileNumber = nameof(InvalidMobileNumber);
    public const string InvalidEmailAddress = nameof(InvalidEmailAddress);
    public const string MobileNumberAlreadyRegistered = nameof(MobileNumberAlreadyRegistered);
    public const string EmailAddressAlreadyRegistered = nameof(EmailAddressAlreadyRegistered);
    public const string PrimaryIdentifierReadOnly = nameof(PrimaryIdentifierReadOnly);
    public const string DuplicatePhoneNumber = nameof(DuplicatePhoneNumber);
    public const string AdditionalPhoneNumberLimitExceeded = nameof(AdditionalPhoneNumberLimitExceeded);
    // SECURITY gate LOW (second pass, HO-ACC-001 §1 Amended (18)): a self-service additional
    // number that equals another ACTIVE customer's PRIMARY mobile -- see
    // UpdateCustomerContactDetailsCommandHandler.
    public const string AdditionalPhoneNumberMatchesAnotherCustomer = nameof(AdditionalPhoneNumberMatchesAnotherCustomer);

    // Password
    public const string PasswordTooShort = nameof(PasswordTooShort);
    public const string BreachedPassword = nameof(BreachedPassword);
    public const string InvalidPasswordCredential = nameof(InvalidPasswordCredential);

    // CAPTCHA & limits
    public const string CaptchaRequired = nameof(CaptchaRequired);
    public const string CaptchaInvalid = nameof(CaptchaInvalid);
    public const string TooManyRequests = nameof(TooManyRequests);

    // Sign-in & sessions
    public const string InvalidCredentials = nameof(InvalidCredentials);
    public const string AccountLocked = nameof(AccountLocked);
    public const string AccountNotActive = nameof(AccountNotActive);
    public const string SessionInvalid = nameof(SessionInvalid);
    public const string SessionIdleTimeout = nameof(SessionIdleTimeout);
    public const string CsrfTokenInvalid = nameof(CsrfTokenInvalid);
    public const string ReauthenticationFailed = nameof(ReauthenticationFailed);

    // Reset
    public const string InvalidPasswordResetToken = nameof(InvalidPasswordResetToken);
    public const string PasswordResetTokenExpired = nameof(PasswordResetTokenExpired);
    public const string PasswordResetTokenSuperseded = nameof(PasswordResetTokenSuperseded);

    // Addresses & preferences
    public const string DeliveryAddressLimitExceeded = nameof(DeliveryAddressLimitExceeded);
    public const string InvalidDeliveryAddress = nameof(InvalidDeliveryAddress);
    public const string DeliveryAddressNotFound = nameof(DeliveryAddressNotFound);
    public const string DefaultAddressCannotBeRemoved = nameof(DefaultAddressCannotBeRemoved);
    public const string DuplicateNotificationPreferenceChange = nameof(DuplicateNotificationPreferenceChange);
    public const string NotificationPreferenceNotEditable = nameof(NotificationPreferenceNotEditable);

    // Staff & authorization
    public const string StaffRolesRequired = nameof(StaffRolesRequired);
    public const string UnknownStaffRole = nameof(UnknownStaffRole);
    public const string LastAdministratorCannotBeRemoved = nameof(LastAdministratorCannotBeRemoved);
    public const string StaffAccountNotFound = nameof(StaffAccountNotFound);
    public const string ActionNotPermitted = nameof(ActionNotPermitted);

    // Customers (staff-facing)
    public const string CustomerNotFound = nameof(CustomerNotFound);
    public const string CannotMergeCustomerIntoItself = nameof(CannotMergeCustomerIntoItself);
    public const string CustomerAlreadyMerged = nameof(CustomerAlreadyMerged);
    public const string CustomersNotDuplicates = nameof(CustomersNotDuplicates);
    public const string CatOwnerMismatch = nameof(CatOwnerMismatch);
    // ReasonRequired is already declared in BusinessViolationCodes.Cat.cs -- reused verbatim,
    // not redeclared (a partial static class cannot repeat a const member's name).

    // Deletion
    public const string AccountAlreadyAnonymised = nameof(AccountAlreadyAnonymised);

    // Audit
    public const string AuditEntryAlreadyRecorded = nameof(AuditEntryAlreadyRecorded);
    public const string InvalidAuditEntry = nameof(InvalidAuditEntry);
}
