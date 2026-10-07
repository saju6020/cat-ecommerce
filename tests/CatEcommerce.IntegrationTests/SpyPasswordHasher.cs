using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.ValueObjects;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// SECURITY gate MEDIUM (timing enumeration): wraps the real <see cref="IPasswordHasher"/> and
/// counts calls to <see cref="VerifyAgainstDummy"/>, so a test can assert a refusal branch burns
/// the same one Argon2 verify a real wrong-password attempt would -- without relying on wall-clock
/// timing, which the gate's own probe warned against.
/// </summary>
public sealed class SpyPasswordHasher(IPasswordHasher inner) : IPasswordHasher
{
    private static int _verifyAgainstDummyCalls;

    public static int VerifyAgainstDummyCalls => _verifyAgainstDummyCalls;

    public static void Reset() => Interlocked.Exchange(ref _verifyAgainstDummyCalls, 0);

    public PasswordCredential Hash(string plaintext) => inner.Hash(plaintext);

    public bool Verify(string plaintext, PasswordCredential credential) => inner.Verify(plaintext, credential);

    public void VerifyAgainstDummy(string plaintext)
    {
        Interlocked.Increment(ref _verifyAgainstDummyCalls);
        inner.VerifyAgainstDummy(plaintext);
    }
}
