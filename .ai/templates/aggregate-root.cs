using {{RootNamespace}}.Domain.Events.[ContextName];
using {{RootNamespace}}.Domain.Models;
using MongoDB.Bson.Serialization.Attributes;
using Platform.Infrastructure.Core.Domain;

namespace {{RootNamespace}}.Domain.Aggregates;

/// <summary>
/// Template for an Aggregate Root. Working reference:
/// src/Domain/Bits.Procurement.Domain/Aggregates/VendorAggregate.cs.
///
/// Standard responsibilities:
/// 1. Extend Platform.Infrastructure.Core.Domain.AggregateRoot.
/// 2. Annotate with [BsonIgnoreExtraElements].
/// 3. Entirely private setters — state changes only happen inside the aggregate's own mutation
///    methods (e.g. Create/Register/Update). A command handler never sets these directly.
/// 4. Every mutation method raises exactly one domain event via AddDomainEvent() and finishes with
///    UpdateVersion(). There is no AddBusinessRuleViolationEvent — business rule violations that
///    block a mutation (uniqueness, "not found") are checked before this method runs, by the
///    CommandService/handler, and surfaced via the ADR-005 rejection event
///    ([ContextName]CommandRejectedEvent), not raised from inside the aggregate. An invariant this
///    aggregate itself must defend (e.g. an illegal status transition) can still throw
///    InvalidOperationException — see VendorAggregate.Blacklist/Reinstate.
/// 5. Expose any child collection as IReadOnlyList&lt;T&gt; over a private backing field, never as
///    a `private set` List&lt;T&gt; — ADR-007: `private set` protects only the reference, not the
///    list contents, and `aggregate.Children.Add(...)` would still compile from outside. Give the
///    backing field its own [BsonElement] so it still round-trips through the same BSON element
///    name.
/// 6. If this aggregate's command handler mutates *accumulating* state (running totals, counters)
///    rather than state that is fully replaced on each mutation, keep a bounded ring of recently
///    applied command/event ids (see VendorAggregate.RecentRatingIds, ADR-007) and no-op on a
///    repeat, so a redelivered command cannot double-count. Skip this for mutations that are pure
///    assignment — those are already idempotent.
/// </summary>
[BsonIgnoreExtraElements]
public class [EntityName]Aggregate : AggregateRoot
{
    public string Name { get; private set; } = default!;

    // Optional: child entities as a read-only projection over a private backing field (ADR-007).
    // [BsonElement("Children")]
    // private List<ChildEntity> _children = new();
    // public IReadOnlyList<ChildEntity> Children => _children;

    public [EntityName]Aggregate()
    {
    }

    /// <summary>
    /// Creates the aggregate. The CommandHandler is expected to have already run any cross-entity
    /// uniqueness/existence checks via the CommandService before calling this — this method only
    /// enforces invariants that belong to the aggregate itself.
    /// </summary>
    public void Create([EntityName]CreationDto input)
    {
        Id = input.[EntityName]Id;
        Name = input.Name;

        SetDefaultValue(input.UserContext);

        AddDomainEvent(new [EntityName]CreatedEvent
        {
            [EntityName]Id = Id,
            Name = Name,
            CorrelationId = input.CorrelationId,
            UserContext = input.UserContext
        });

        UpdateVersion();
    }
}
