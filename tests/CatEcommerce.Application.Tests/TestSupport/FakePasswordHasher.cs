using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.ValueObjects;

namespace CatEcommerce.Application.Tests.TestSupport;

/// <summary>Hand-written test double -- no mocking library in the allowed scope (§13).</summary>
public sealed class FakePasswordHasher : IPasswordHasher
{
    public int VerifyAgainstDummyCallCount { get; private set; }
    public string? LastPlaintextHashed { get; private set; }

    public PasswordCredential Hash(string plaintext)
    {
        LastPlaintextHashed = plaintext;
        return PasswordCredential.Create($"hashed:{plaintext}", "fake", DateTimeOffset.UtcNow);
    }

    public bool Verify(string plaintext, PasswordCredential credential) => credential.Hash == $"hashed:{plaintext}";

    public void VerifyAgainstDummy(string plaintext) => VerifyAgainstDummyCallCount++;
}
