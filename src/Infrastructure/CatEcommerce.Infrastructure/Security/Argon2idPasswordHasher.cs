using System.Security.Cryptography;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Configuration;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace CatEcommerce.Infrastructure.Security;

/// <summary>
/// HO-ACC-001 §12.2 / ADR-027. Argon2id, PHC-encoded
/// (<c>$argon2id$v=19$m=..,t=..,p=..$&lt;salt&gt;$&lt;hash&gt;</c>). A fixed dummy hash is
/// verified for unknown identifiers so response time does not reveal existence (ADR-028).
/// </summary>
public sealed class Argon2idPasswordHasher : IPasswordHasher
{
    private const string AlgorithmName = "argon2id";
    private const int SaltLength = 16;
    private const int HashLength = 32;

    private readonly int _memoryKiB;
    private readonly int _iterations;
    private readonly int _parallelism;
    private readonly TimeProvider _timeProvider;
    private readonly Lazy<PasswordCredential> _dummyCredential;

    public Argon2idPasswordHasher(IOptions<AccountOptions> options, TimeProvider timeProvider)
    {
        _memoryKiB = options.Value.Argon2MemoryKiB;
        _iterations = options.Value.Argon2Iterations;
        _parallelism = options.Value.Argon2Parallelism;
        _timeProvider = timeProvider;

        // Built once, lazily, from a fixed salt and fixed plaintext -- never randomised, so
        // VerifyAgainstDummy always does the same amount of work.
        _dummyCredential = new Lazy<PasswordCredential>(() =>
        {
            var fixedSalt = new byte[SaltLength]; // all-zero -- deterministic, never used for a real credential
            var hash = ComputeHash("purrcare-dummy-credential-not-a-real-password", fixedSalt);
            return PasswordCredential.Create(Encode(fixedSalt, hash), AlgorithmName, _timeProvider.GetUtcNow());
        });
    }

    public PasswordCredential Hash(string plaintext)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var hash = ComputeHash(plaintext, salt);
        return PasswordCredential.Create(Encode(salt, hash), AlgorithmName, _timeProvider.GetUtcNow());
    }

    public bool Verify(string plaintext, PasswordCredential credential)
    {
        var (salt, expectedHash, memoryKiB, iterations, parallelism) = Decode(credential.Hash);
        var actualHash = ComputeHash(plaintext, salt, memoryKiB, iterations, parallelism);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    public void VerifyAgainstDummy(string plaintext) => Verify(plaintext, _dummyCredential.Value);

    private byte[] ComputeHash(string plaintext, byte[] salt) =>
        ComputeHash(plaintext, salt, _memoryKiB, _iterations, _parallelism);

    private static byte[] ComputeHash(string plaintext, byte[] salt, int memoryKiB, int iterations, int parallelism)
    {
        using var argon2 = new Argon2id(System.Text.Encoding.UTF8.GetBytes(plaintext))
        {
            Salt = salt,
            DegreeOfParallelism = parallelism,
            MemorySize = memoryKiB,
            Iterations = iterations
        };

        return argon2.GetBytes(HashLength);
    }

    /// <summary>The encoded string carries the parameters used at hash time (not necessarily the
    /// current options), so a later parameter change does not break verifying older credentials.</summary>
    private string Encode(byte[] salt, byte[] hash) =>
        $"$argon2id$v=19$m={_memoryKiB},t={_iterations},p={_parallelism}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";

    private static (byte[] Salt, byte[] Hash, int MemoryKiB, int Iterations, int Parallelism) Decode(string encoded)
    {
        var parts = encoded.Split('$', StringSplitOptions.RemoveEmptyEntries);
        // parts: ["argon2id", "v=19", "m=..,t=..,p=..", "<salt>", "<hash>"]
        if (parts.Length != 5)
            throw new FormatException("Unrecognised password credential encoding.");

        var paramParts = parts[2].Split(',');
        var memoryKiB = int.Parse(paramParts[0].Split('=')[1]);
        var iterations = int.Parse(paramParts[1].Split('=')[1]);
        var parallelism = int.Parse(paramParts[2].Split('=')[1]);

        var salt = Convert.FromBase64String(parts[3]);
        var hash = Convert.FromBase64String(parts[4]);

        return (salt, hash, memoryKiB, iterations, parallelism);
    }
}
