namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-ACC-001 §10.2 / §20.6. Every user-facing message that appears on two independent code
/// paths, as one constant, so the two paths cannot drift by a character (which would let a
/// caller tell which path they hit):
/// - the handler's duplicate-identifier pre-check AND <c>UniqueConstraintExceptionFilter</c>'s
///   <c>23505</c> mapping (ADR-026);
/// - the sign-in handler's generic failure AND the Api's R1 response body (ADR-028);
/// - the reset-request handler's neutral outcome AND the Api's R3 response body (ADR-034).
/// </summary>
public static class AccountMessages
{
    public const string MobileInUse =
        "This mobile number is already registered. You can log in, or reset your password.";

    public const string EmailInUse =
        "This email address is already registered. You can log in, or reset your password.";

    public const string SignInFailed =
        "The identifier or password is incorrect.";

    public const string PasswordResetRequested =
        "If an account matches, we've sent a link to reset the password.";

    public const string CaptchaInvalidMessage =
        "The characters didn't match, or the code expired. Type the new code shown.";
}
