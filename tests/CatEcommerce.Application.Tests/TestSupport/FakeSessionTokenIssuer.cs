using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;

namespace CatEcommerce.Application.Tests.TestSupport;

public sealed class FakeSessionTokenIssuer : ISessionTokenIssuer
{
    private int _counter;

    public SessionTokenMaterial Issue()
    {
        _counter++;
        return new SessionTokenMaterial($"token-{_counter}", HashToken($"token-{_counter}"));
    }

    public string HashToken(string token) => $"hash-of-{token}";
}
