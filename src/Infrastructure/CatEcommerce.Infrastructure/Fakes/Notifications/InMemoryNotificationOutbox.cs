using System.Collections.Concurrent;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;

namespace CatEcommerce.Infrastructure.Fakes.Notifications;

/// <summary>
/// FAKE until HO-NTF-001 lands -- replace this registration.
///
/// Records every enqueued request into an in-memory list (for test assertions) and does nothing
/// else -- there is no <c>NotificationRequestAggregate</c>/table yet for it to truly enlist into,
/// and there is no business rule to deny here, so a simple in-memory recorder is the right fake.
/// </summary>
public sealed class InMemoryNotificationOutbox : INotificationOutbox
{
    private readonly ConcurrentQueue<NotificationRequestDto> _requests = new();

    public IReadOnlyCollection<NotificationRequestDto> Requests => _requests.ToArray();

    public void Enqueue(NotificationRequestDto request) => _requests.Enqueue(request);
}
