using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;

namespace CatEcommerce.Application.Tests.TestSupport;

public sealed class FakePasswordResetTokenGenerator : IPasswordResetTokenGenerator
{
    private int _counter;

    public PasswordResetTokenMaterial Generate(DateTimeOffset asOf)
    {
        _counter++;
        var token = $"reset-token-{_counter}";
        return new PasswordResetTokenMaterial(token, ComputeHash(token), asOf.AddMinutes(15));
    }

    public string ComputeHash(string plainToken) => $"hash-of-{plainToken}";
}
