using CatEcommerce.Domain.Events.Administration;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Administration;

/// <summary>HO-ADM-001 §8.8 / ADR-053. Singleton -- id is <see cref="SystemStatusIds.Singleton"/>.</summary>
public sealed class SystemStatusAggregate : AggregateRoot
{
    public bool MaintenanceMode { get; private set; }
    public string? MaintenanceMessage { get; private set; }
    public DateTimeOffset ChangedOn { get; private set; }

    private SystemStatusAggregate() { }

    public SystemStatusAggregate(Guid id) : base(id) { }

    public void SetMaintenanceMode(SetMaintenanceModeDto dto)
    {
        if (dto.Message is not null && dto.Message.Length > 500)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidSettingValue, "The maintenance message is too long.", nameof(MaintenanceMessage));

        MaintenanceMode = dto.MaintenanceMode;
        MaintenanceMessage = dto.Message;
        ChangedOn = dto.ChangedOn;

        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new MaintenanceModeChangedEvent { MaintenanceMode = MaintenanceMode, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }
}
