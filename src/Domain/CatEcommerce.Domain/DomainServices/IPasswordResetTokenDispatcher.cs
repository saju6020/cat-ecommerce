using CatEcommerce.Domain.Models;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-ACC-001 §8.10 / §4.3 / §20.4. No SMS or email provider exists yet -- the Development
/// implementation logs the link; the non-Development implementation records the request and
/// delivers nothing.
/// </summary>
public interface IPasswordResetTokenDispatcher
{
    Task DispatchAsync(PasswordResetDispatchRequest request, CancellationToken ct = default);
}
