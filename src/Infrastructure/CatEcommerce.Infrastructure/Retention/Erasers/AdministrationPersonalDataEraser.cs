using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
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
/// <b>Retention period.</b> The `retention.financialYears` setting (HO-XCT-001 section 10.2, ADR-301), default 6 years (NFR-LEG-5 acceptance evidence, D-9). Rewired from a fixed default by HO-XCT-001 Phase B.
///
/// Mutates only through <see cref="PolicyAcceptanceAggregate"/>'s own public method and its
/// repository (contract rule 3) -- never raw SQL.
/// </summary>
public sealed class AdministrationPersonalDataEraser(
    CatEcommerceDbContext db,
    IAggregateRootRepository<PolicyAcceptanceAggregate> acceptanceRepository,
    IBusinessSettings settings,
    IOperationsClock clock)
    : IPersonalDataEraser
{
    public string ContextKey => "administration";

    public async Task<ErasureOutcome> EraseAsync(Guid customerAccountId, DateTimeOffset asOf, UserContext actor, CancellationToken ct)
    {
        var acceptanceIds = await db.Set<PolicyAcceptanceAggregate>()
            .AsNoTracking()
            .Where(a => a.CustomerAccountId == customerAccountId && a.IpAddress != null)
            .Select(a => a.Id)
            .ToListAsync(ct);

        if (acceptanceIds.Count == 0) return new ErasureOutcome.Completed();

        // HO-XCT-001 (Phase B): the period is the retention.financialYears setting (ADR-301), and "today" is the main branch calendar date of asOf.
        var retentionYears = settings.GetInt(SettingKeys.RetentionFinancialYears);
        var today = await clock.LocalDateOfAsync(asOf, ct);
        DateOnly? earliestHeldUntil = null;

        foreach (var id in acceptanceIds)
        {
            var acceptance = await acceptanceRepository.GetByIdAsync(id);
            if (acceptance is null || acceptance.IpAddress is null) continue;

            // The same calendar as "today": the main branch local date (gate fix 2026-10-12, CODE_REVIEW 15) -- an acceptance made 00:00-05:59 Dhaka falls on the previous UTC date and would be redacted a day early.
            var acceptedOnLocal = await clock.LocalDateOfAsync(acceptance.AcceptedOn, ct);
            var heldUntil = acceptedOnLocal.AddYears(retentionYears);
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
