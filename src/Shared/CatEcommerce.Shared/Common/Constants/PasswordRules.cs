namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-ACC-001 §10.2. <see cref="MaximumLength"/> is a resource bound on the hasher for anonymous
/// callers, not a strength rule.
/// </summary>
public static class PasswordRules
{
    public const int MinimumLength = 10;
    public const int MaximumLength = 256;
}
