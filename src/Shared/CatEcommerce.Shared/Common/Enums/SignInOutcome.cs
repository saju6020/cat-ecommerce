namespace CatEcommerce.Shared.Common.Enums;

/// <summary>
/// HO-ACC-001 §10.1. For audit and tests only -- never returned in a response body (R1: every
/// sign-in failure collapses to one generic 401).
/// </summary>
public sealed class SignInOutcome(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly SignInOutcome Success = new(1, nameof(Success));
    public static readonly SignInOutcome InvalidCredentials = new(2, nameof(InvalidCredentials));
    public static readonly SignInOutcome AccountLocked = new(3, nameof(AccountLocked));
    public static readonly SignInOutcome AccountNotActive = new(4, nameof(AccountNotActive));
}
