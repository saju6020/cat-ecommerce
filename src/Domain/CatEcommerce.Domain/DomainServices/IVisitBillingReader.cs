using CatEcommerce.Domain.Models;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-BIL-001 Phase J (owner decision 2026-10-14, backlog EMR 43) -- the one door through which Billing reads a visit. Owned by EMR and implemented in
/// <c>Infrastructure/Consultation</c>; the values are typed (<c>decimal</c> / <c>Money</c> / kinds), not the display strings of the Read-layer
/// <c>GetVisitChargeLinesQuery</c>, which Billing never calls. Billing reads no <c>clinic.*</c> table itself. Read-only; an unknown visit is null.
/// </summary>
public interface IVisitBillingReader
{
    Task<VisitBillingFacts?> GetAsync(Guid visitId, CancellationToken ct = default);
}
