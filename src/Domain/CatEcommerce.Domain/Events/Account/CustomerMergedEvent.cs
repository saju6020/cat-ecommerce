using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Account;

/// <summary>
/// HO-ACC-001 §8.9 / ADR-039. The binding integration contract every future context subscribes
/// to, idempotently, to move its own data off the duplicate.
/// </summary>
public sealed class CustomerMergedEvent : Event
{
    public required Guid DuplicateCustomerId { get; init; }
    public required Guid SurvivingCustomerId { get; init; }
    public required string Reason { get; init; }
    public required bool IsReapplication { get; init; }
}
