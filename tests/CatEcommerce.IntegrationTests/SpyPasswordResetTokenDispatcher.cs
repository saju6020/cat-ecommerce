using System.Collections.Concurrent;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-ACC-001 §16.1 / I-D2 "dispatcher spy". Replaces IPasswordResetTokenDispatcher in the test
/// host so tests can retrieve the plaintext token a request generated, without needing a real SMS
/// or email transport (which does not exist yet, §4.3).
/// </summary>
public sealed class SpyPasswordResetTokenDispatcher : IPasswordResetTokenDispatcher
{
    private static readonly ConcurrentBag<PasswordResetDispatchRequest> AllDispatched = [];

    public Task DispatchAsync(PasswordResetDispatchRequest request, CancellationToken ct = default)
    {
        AllDispatched.Add(request);
        return Task.CompletedTask;
    }

    public static PasswordResetDispatchRequest? LastFor(Guid accountId) =>
        AllDispatched.LastOrDefault(r => r.AccountId == accountId);
}
