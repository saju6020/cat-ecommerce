namespace CatEcommerce.Shared.Common.Helpers;

/// <summary>
/// HO-ACC-001 §11.8. Storing the raw identifier a sign-in attempt was made against would let a
/// credential-stuffing run fill the audit table with its dictionary. Used only for the
/// unknown-identifier and idle/lockout audit rows -- never for a response body.
/// </summary>
public static class IdentifierMasking
{
    public static string Mask(string identifier)
    {
        if (identifier.Contains('@'))
        {
            var at = identifier.IndexOf('@');
            var first = identifier.Length > 0 ? identifier[0].ToString() : "";
            var domain = at >= 0 ? identifier[at..] : "";
            return $"{first}***{domain}";
        }

        var lastThree = identifier.Length >= 3 ? identifier[^3..] : identifier;
        return $"***{lastThree}";
    }
}
