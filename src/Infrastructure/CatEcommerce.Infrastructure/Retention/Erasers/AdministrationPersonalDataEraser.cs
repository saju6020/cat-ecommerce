using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Infrastructure.Retention.Erasers;

/// <summary>
/// HO-XCT-001 §5.2 C-ALL-2 / §8.8. Administration stores a customer's personal data beyond the id
/// on <c>PolicyAcceptanceAggregate.IpAddress</c> -- registered now, ahead of HO-XCT-001 itself
/// landing, per CONTRACTS.md's cross-cutting duties. Not out of §13's scope by accident: HO-XCT-001
/// §5.3 already names this exact eraser and its <c>RedactIpAddress</c> mechanism as owed to this
/// handover (Amended row, HO-ADM-001 §1).
///
/// <b>Retention period.</b> HO-XCT-001's own design reads <c>retention.financialYears</c> from
/// <see cref="IBusinessSettings"/> -- a setting HO-XCT-001 itself appends to the registry when it
/// lands (§5.3), not yet present. Until then this uses a fixed default
/// (<see cref="DefaultRetentionYears"/>, matching NFR-LEG-5's acceptance-evidence expectation) so
/// the eraser is real and correct today rather than blocked on a setting that does not exist yet;
/// rewiring to the real setting is HO-XCT-001's own change when it lands.
///
/// Mutates only through <see cref="PolicyAcceptanceAggregate"/>'s own public method and its
/// repository (contract rule 3) -- never raw SQL.
/// </summary>
public sealed class AdministrationPersonalDataEraser(
    CatEcommerceDbContext db,
    IAggregateRootRepository<PolicyAcceptanceAggregate> acceptanceRepository)
    : IPersonalDataEraser
{
    private const int DefaultRetentionYears = 6;

    public string ContextKey => "administration";

    public async Task<ErasureOutcome> EraseAsync(Guid customerAccountId, DateTimeOffset asOf, UserContext actor, CancellationToken ct)
    {
        var acceptanceIds = await db.Set<PolicyAcceptanceAggregate>()
            .AsNoTracking()
            .Where(a => a.CustomerAccountId == customerAccountId && a.IpAddress != null)
            .Select(a => a.Id)
            .ToListAsync(ct);

        if (acceptanceIds.Count == 0) return new ErasureOutcome.Completed();

        var today = DateOnly.FromDateTime(asOf.UtcDateTime);
        DateOnly? earliestHeldUntil = null;

        foreach (var id in acceptanceIds)
        {
            var acceptance = await acceptanceRepository.GetByIdAsync(id);
            if (acceptance is null || acceptance.IpAddress is null) continue;

            var heldUntil = DateOnly.FromDateTime(acceptance.AcceptedOn.UtcDateTime).AddYears(DefaultRetentionYears);
            if (heldUntil <= today)
            {
                acceptance.RedactIpAddress(new PolicyAcceptanceIpRedactDto(actor, Guid.NewGuid()));
                await acceptanceRepository.UpdateAsync(acceptance, EventPublishOption.InMemoryPublishOnly);
            }
            else if (earliestHeldUntil is null || heldUntil < earliestHeldUntil)
            {
                earliestHeldUntil = heldUntil;
            }
        }

        return earliestHeldUntil is { } until
            ? new ErasureOutcome.Held(until, "NFR-LEG-5 acceptance evidence")
            : new ErasureOutcome.Completed();
    }
}
