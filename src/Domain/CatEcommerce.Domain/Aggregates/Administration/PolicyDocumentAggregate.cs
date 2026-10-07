using CatEcommerce.Domain.Events.Administration;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Administration;

/// <summary>HO-ADM-001 §8.8.</summary>
public sealed class PolicyDocumentAggregate : AggregateRoot
{
    public string Key { get; private set; } = default!;
    public string Title { get; private set; } = default!;
    public bool RequiresAcceptance { get; private set; }
    public int? CurrentVersionNumber { get; private set; }

    private PolicyDocumentAggregate() { }

    public PolicyDocumentAggregate(Guid id) : base(id) { }

    public void Create(PolicyDocumentCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Key) || dto.Key.Length > 80)
            throw new DomainRuleViolationException(BusinessViolationCodes.PolicyNotFound, "A policy key is required.", nameof(Key));
        if (string.IsNullOrWhiteSpace(dto.Title) || dto.Title.Length > 200)
            throw new DomainRuleViolationException(BusinessViolationCodes.NameRequired, "A title is required.", nameof(Title));

        Key = dto.Key;
        Title = dto.Title;
        RequiresAcceptance = dto.RequiresAcceptance;
        CurrentVersionNumber = null;

        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new PolicyDocumentCreatedEvent { PolicyDocumentId = Id, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    /// <summary>
    /// ADR-052's recorded rule-1 exception: the second of the two writes
    /// <c>PublishPolicyVersionCommand</c> performs. Raises no event of its own -- the pointer is a
    /// derivable cache of "latest version" (the public read uses <c>MAX(version_number)</c> so a
    /// lag here is never visible); <see cref="PolicyVersionAggregate"/>'s own creation already
    /// raises <c>PolicyVersionPublishedEvent</c>, the one event this whole operation needs.
    /// </summary>
    public void SetCurrentVersion(PolicyDocumentSetCurrentVersionDto dto)
    {
        CurrentVersionNumber = dto.VersionNumber;
        UpdateVersion();
    }
}
