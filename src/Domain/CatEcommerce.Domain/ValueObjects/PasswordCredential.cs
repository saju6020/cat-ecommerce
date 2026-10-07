using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Domain.ValueObjects;

/// <summary>
/// HO-ACC-001 §8.2 / ADR-025. There is no factory that takes a plaintext -- the aggregate never
/// sees one. A handler calls <c>IPasswordHasher</c> and passes the already-built credential in.
/// </summary>
public sealed record PasswordCredential
{
    public const int MaxHashLength = 256;
    public const int MaxAlgorithmLength = 50;

    public string Hash { get; }
    public string Algorithm { get; }
    public DateTimeOffset SetOn { get; }

    private PasswordCredential(string hash, string algorithm, DateTimeOffset setOn)
    {
        Hash = hash;
        Algorithm = algorithm;
        SetOn = setOn;
    }

    public static PasswordCredential Create(string hash, string algorithm, DateTimeOffset setOn)
    {
        if (string.IsNullOrWhiteSpace(hash) || hash.Length > MaxHashLength) throw Invalid();
        if (string.IsNullOrWhiteSpace(algorithm) || algorithm.Length > MaxAlgorithmLength) throw Invalid();

        return new PasswordCredential(hash, algorithm, setOn);
    }

    private static DomainRuleViolationException Invalid() => new(
        BusinessViolationCodes.InvalidPasswordCredential,
        "The password credential is invalid.",
        nameof(PasswordCredential));
}
