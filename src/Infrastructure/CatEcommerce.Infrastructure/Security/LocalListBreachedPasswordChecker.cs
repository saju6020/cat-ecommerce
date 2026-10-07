using System.Reflection;
using CatEcommerce.Domain.DomainServices;

namespace CatEcommerce.Infrastructure.Security;

/// <summary>
/// HO-ACC-001 §12.2 / ADR-027 / OQ-6. Loads the embedded resource once into a case-insensitive
/// <see cref="HashSet{T}"/> -- no network call. See <c>breached-passwords.txt</c> for provenance.
/// </summary>
public sealed class LocalListBreachedPasswordChecker : IBreachedPasswordChecker
{
    private static readonly Lazy<HashSet<string>> BreachedPasswords = new(Load);

    public Task<bool> IsBreachedAsync(string plaintext, CancellationToken ct = default) =>
        Task.FromResult(BreachedPasswords.Value.Contains(plaintext));

    private static HashSet<string> Load()
    {
        var assembly = typeof(LocalListBreachedPasswordChecker).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .Single(n => n.EndsWith("breached-passwords.txt", StringComparison.Ordinal));

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
            set.Add(trimmed);
        }

        return set;
    }
}
