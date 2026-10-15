using CatEcommerce.Domain.Models;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-ORD-001 §8.14, §8.16 (ADR-205) -- writes a reconciliation issue in a <b>new DI scope</b> (its own <c>DbContext</c>), because the failed unit of work's
/// context is rolled back or poisoned. It <b>never throws out</b>: if even this fails it is logged at <c>Critical</c> (ids only) and the original
/// failure still reaches the caller. Returns the issue's id when it was written, else <c>null</c>. Implemented in Infrastructure.
/// </summary>
public interface IReconciliationIssueRecorder
{
    Task<Guid?> RecordAsync(ReconciliationIssueDto issue, CancellationToken ct = default);
}
