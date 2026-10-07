using CatEcommerce.Domain.Events.Administration;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Administration;

/// <summary>HO-ADM-001 §8.8. Append-only -- version numbers are assigned by the handler as
/// current + 1 and are never reused (C-ALL-4).</summary>
public sealed class PolicyVersionAggregate : AggregateRoot
{
    public string PolicyKey { get; private set; } = default!;
    public int VersionNumber { get; private set; }
    public string Body { get; private set; } = default!;
    public DateTimeOffset PublishedOn { get; private set; }
    public string ChangeSummary { get; private set; } = default!;
    public bool RequiresAcceptance { get; private set; }

    private PolicyVersionAggregate() { }

    public PolicyVersionAggregate(Guid id) : base(id) { }

    public void Create(PolicyVersionCreateDto dto)
    {
        var error = LimitedMarkup.Validate(dto.Body);
        if (error is not null) throw new DomainRuleViolationException(error, "The policy body contains disallowed markup.", nameof(Body));

        if (dto.ChangeSummary.Length > 500)
            throw new DomainRuleViolationException(BusinessViolationCodes.NameRequired, "The change summary is too long.", nameof(ChangeSummary));

        PolicyKey = dto.PolicyKey;
        VersionNumber = dto.VersionNumber;
        Body = dto.Body;
        PublishedOn = dto.PublishedOn;
        ChangeSummary = dto.ChangeSummary;
        RequiresAcceptance = dto.RequiresAcceptance;

        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new PolicyVersionPublishedEvent
        {
            PolicyKey = PolicyKey, VersionNumber = VersionNumber, RequiresAcceptance = RequiresAcceptance,
            CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        UpdateVersion();
    }
}
