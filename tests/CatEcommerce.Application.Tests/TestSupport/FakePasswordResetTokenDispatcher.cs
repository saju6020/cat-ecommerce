using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;

namespace CatEcommerce.Application.Tests.TestSupport;

public sealed class FakePasswordResetTokenDispatcher : IPasswordResetTokenDispatcher
{
    public List<PasswordResetDispatchRequest> Dispatched { get; } = [];

    public Task DispatchAsync(PasswordResetDispatchRequest request, CancellationToken ct = default)
    {
        Dispatched.Add(request);
        return Task.CompletedTask;
    }
}
