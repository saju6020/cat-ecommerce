namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-APT-001 §8.6 step 3 (<c>booking.maxHoldsPerCustomer</c> = 1, ADR-070), SECURITY gate fix
/// (2026-09-28, HIGH): "one hold per customer" was enforced only by an unlocked
/// <c>AsNoTracking</c> read followed by an insert -- concurrent <c>PlaceHold</c> requests from one
/// account all saw "no hold yet" and all succeeded (reproduced: 8 concurrent requests, 8 × 201, 8
/// live <c>Held</c> rows for one customer).
///
/// Runs <paramref name="operation"/> inside one database transaction, having first taken a
/// Postgres <b>transaction-scoped advisory lock</b> (<c>pg_advisory_xact_lock</c>) keyed on
/// <paramref name="customerId"/>: every concurrent <c>PlaceHold</c> for the same customer, from any
/// process, on any host, serialises on this lock, one at a time, in commit order. The lock is held
/// for the lifetime of the transaction and released automatically when it commits or rolls back --
/// including on a crash or a dropped connection, so nothing can leak a permanently-held lock the
/// way an in-process lock (a <c>SemaphoreSlim</c>, a <c>lock</c> statement) would if the process
/// restarted mid-request. An in-memory lock was rejected for exactly that reason: it also would not
/// hold across the multiple API processes/instances a real deployment runs.
///
/// <b>Why this does not deadlock with ADR-070's stale-hold release</b> (which commits each hold's
/// expiry in its own save, its own 2026-09-28 amendment): this lock is keyed <em>only</em> on the
/// calling customer's own id, and a <c>PlaceHold</c> transaction never acquires any other
/// customer's advisory lock -- expiring a stale hold that happens to belong to a <em>different</em>
/// customer is an ordinary row update, not a second advisory lock acquisition, so two different
/// customers' transactions can never form a lock-ordering cycle through this mechanism. Postgres
/// can still, in principle, detect an unrelated row/index-level deadlock between two different
/// customers' concurrent inserts (the same class of deadlock ADR-068/the SECURITY gate's `40P01`
/// finding already covers) -- that is reported as `40P01` and mapped to `409 SlotNoLongerAvailable`
/// by <c>UniqueConstraintExceptionFilter</c> on the `[SlotClaimEndpoint]`-marked action, not raised
/// as an unhandled 500.
/// </summary>
public interface ICustomerHoldSerializer
{
    Task<T> RunSerializedAsync<T>(Guid customerId, Func<Task<T>> operation, CancellationToken ct = default);
}
