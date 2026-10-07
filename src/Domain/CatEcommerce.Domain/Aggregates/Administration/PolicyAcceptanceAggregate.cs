using CatEcommerce.Domain.Events.Administration;
using CatEcommerce.Domain.Models;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Administration;

/// <summary>HO-ADM-001 §8.8. Append-only. The unique (customer, key, version) index makes
/// accepting twice a handler-level no-op (§11.4) -- this aggregate has no "already accepted" guard
/// of its own to duplicate that.</summary>
public sealed class PolicyAcceptanceAggregate : AggregateRoot
{
    public Guid CustomerAccountId { get; private set; }
    public string PolicyKey { get; private set; } = default!;
    public int VersionNumber { get; private set; }
    public DateTimeOffset AcceptedOn { get; private set; }
    public string? IpAddress { get; private set; }

    private PolicyAcceptanceAggregate() { }

    public PolicyAcceptanceAggregate(Guid id) : base(id) { }

    public void Create(PolicyAcceptanceCreateDto dto)
    {
        CustomerAccountId = dto.CustomerAccountId;
        PolicyKey = dto.PolicyKey;
        VersionNumber = dto.VersionNumber;
        AcceptedOn = dto.AcceptedOn;
        IpAddress = dto.IpAddress;

        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new PolicyVersionAcceptedEvent
        {
            CustomerAccountId = CustomerAccountId, PolicyKey = PolicyKey, VersionNumber = VersionNumber,
            CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        UpdateVersion();
    }

    /// <summary>HO-XCT-001 §5.3 Amendment / §8.8 -- idempotent; used by
    /// <c>AdministrationPersonalDataEraser</c> once the acceptance evidence hold expires.</summary>
    public void RedactIpAddress(PolicyAcceptanceIpRedactDto dto)
    {
        if (IpAddress is null) return;

        IpAddress = null;

        AddDomainEvent(new PolicyAcceptanceIpRedactedEvent { PolicyAcceptanceId = Id, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }
}
