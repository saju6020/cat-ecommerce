using CatEcommerce.Domain.Models;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// Owned by HO-BIL-001 §8.4 (ADR-178) -- the push path for areas built after Billing (HO-ORD-001,
/// HO-BRD-001, HO-SUB-001). Exact signature from §8.4's own code block.
///
/// Producer rules (checked by the ARCHITECTURE gate on the consumer): call in the command handler
/// before the producer's own save, so the charge commits atomically with the business change; never
/// from a domain event handler; supply prices and discounts already computed (Billing validates,
/// never reprices).
///
/// Real implementation (<c>EfBillableChargeRecorder</c>, enlisting a <c>ChargeAggregate</c> without
/// saving, F23) is HO-BIL-001's. Until it lands, <c>Fakes/Billing/InMemoryBillableChargeRecorder</c>
/// keeps recorded charges in an in-memory list keyed by <see cref="ChargeSourceKey"/> so a
/// consumer's idempotency assumption ("the existing one if this source key was already recorded")
/// can be exercised in tests -- it cannot truly enlist into the shared DbContext because no
/// <c>ChargeAggregate</c>/table exists yet (this contracts pass adds no entities).
/// </summary>
public interface IBillableChargeRecorder
{
    Task<Guid> RecordAsync(ChargeRequestDto request, CancellationToken ct = default);

    Task VoidAsync(ChargeSourceKey key, string reason, CancellationToken ct = default);
}
