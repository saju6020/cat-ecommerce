using CatEcommerce.Domain.Models;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// One functional area's own erasure of a customer's personal data (HO-XCT-001 §8.8, ADR-302,
/// ADR-316). Every context that stores a customer's personal data beyond the id registers one,
/// in its own module, in the same change that adds the data (§5.2 C-ALL-2); a context that stores
/// none is exempted instead in <c>ErasureExemptions</c> (not built by this pass).
///
/// <c>CustomerAccountAnonymisedEvent</c> opens one <c>ErasureRequestAggregate</c> scope per
/// registered eraser; the retention worker's erasure pass (owned by HO-XCT-001, not built here)
/// calls <see cref="EraseAsync"/> for each due scope and records the <see cref="ErasureOutcome"/>.
///
/// The contract every implementation meets (HO-XCT-001 §8.8, enforced by its own U-B7 test, not
/// built here):
/// 1. Idempotent -- a second call after <see cref="ErasureOutcome.Completed"/> is
///    <see cref="ErasureOutcome.Completed"/> and writes nothing.
/// 2. Erases everything not held on every call, then returns
///    <see cref="ErasureOutcome.Held"/> with the earliest remaining date while anything is held.
/// 3. Mutates only this context's own aggregates, through their own public methods and
///    repository -- never SQL, never another context, never a <c>Forbidden</c> row.
/// 4. Never throws for a customer it has no data for -- returns
///    <see cref="ErasureOutcome.Completed"/>.
/// 5. Returns <see cref="ErasureOutcome.Failed"/> for a recoverable problem. An unhandled
///    exception is caught by the caller and recorded as
///    <c>Failed("UnhandledEraserException")</c>.
/// 6. Logs ids and codes only -- never the personal data itself.
/// </summary>
public interface IPersonalDataEraser
{
    /// <summary>Matches one <c>ErasureScope.ContextKey</c>, e.g. "account", "notification",
    /// "administration", "boarding". Stable once chosen -- it is stored in the ledger.</summary>
    string ContextKey { get; }

    Task<ErasureOutcome> EraseAsync(
        Guid customerAccountId,
        DateTimeOffset asOf,
        UserContext actor,
        CancellationToken ct);
}
