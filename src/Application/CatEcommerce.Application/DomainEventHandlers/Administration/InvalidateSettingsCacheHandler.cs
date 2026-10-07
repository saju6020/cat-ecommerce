using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Administration;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Administration;

/// <summary>HO-ADM-001 §11.6 -- evicts the cached scope so a settings change is visible on the
/// very next request (§8.9, A11).</summary>
public sealed class InvalidateSettingsCacheHandler(ISettingsCacheInvalidator cacheInvalidator) : IEventHandlerAsync<BusinessSettingsChangedEvent>
{
    public Task HandleAsync(BusinessSettingsChangedEvent @event)
    {
        cacheInvalidator.Invalidate(@event.ScopeId);
        return Task.CompletedTask;
    }
}
