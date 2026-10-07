using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace CatEcommerce.IntegrationTests.Unit;

/// <summary>
/// HO-ACC-001 §15, U-B14. Exercises Infrastructure directly -- placed here per §15's routing rule
/// ("rows that exercise Infrastructure or Api classes go in tests/CatEcommerce.IntegrationTests as
/// plain unit tests"), not because it needs a database.
/// </summary>
public sealed class Argon2idPasswordHasherTests
{
    private static Argon2idPasswordHasher CreateHasher() => new(
        Options.Create(new AccountOptions { Argon2MemoryKiB = 8192, Argon2Iterations = 1, Argon2Parallelism = 1 }),
        TimeProvider.System);

    [Fact]
    public void Hash_CalledTwice_ProducesDifferentEncodingsNeitherContainingThePlaintext()
    {
        var hasher = CreateHasher();
        const string plaintext = "correcthorsebattery";

        var first = hasher.Hash(plaintext);
        var second = hasher.Hash(plaintext);

        Assert.NotEqual(first.Hash, second.Hash); // different salt each time
        Assert.DoesNotContain(plaintext, first.Hash);
        Assert.DoesNotContain(plaintext, second.Hash);
        Assert.True(hasher.Verify(plaintext, first));
        Assert.True(hasher.Verify(plaintext, second));
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hasher = CreateHasher();
        var credential = hasher.Hash("correcthorsebattery");

        Assert.False(hasher.Verify("wrongpassword", credential));
    }

    [Fact]
    public void VerifyAgainstDummy_DoesNotThrow()
    {
        var hasher = CreateHasher();
        var exception = Record.Exception(() => hasher.VerifyAgainstDummy("anything"));
        Assert.Null(exception);
    }
}
