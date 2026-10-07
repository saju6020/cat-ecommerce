using System.Text.RegularExpressions;

namespace CatEcommerce.Infrastructure.Auditing;

/// <summary>
/// HO-ACC-001 §11.9 step 3. Any property whose name matches the secret pattern is written as
/// <c>"[redacted]"</c> -- fail-closed, so a new secret-bearing property is redacted by default
/// until explicitly allow-listed here.
/// </summary>
public static partial class AuditRedactionRules
{
    [GeneratedRegex("password|hash|token|stamp|secret|answer", RegexOptions.IgnoreCase)]
    private static partial Regex SecretLikePattern();

    /// <summary>Non-secret exceptions to the pattern above -- property names, not values.</summary>
    private static readonly HashSet<string> AllowList = new(StringComparer.OrdinalIgnoreCase)
    {
        // (none yet -- every property matched by SecretLikePattern in this slice IS a secret)
    };

    public static bool IsSecret(string propertyName) =>
        SecretLikePattern().IsMatch(propertyName) && !AllowList.Contains(propertyName);
}
