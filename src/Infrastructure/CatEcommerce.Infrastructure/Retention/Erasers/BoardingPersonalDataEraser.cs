using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Infrastructure.Retention.Erasers;

/// <summary>
/// HO-XCT-001 section 5.2 C-ALL-2 / section 8.8 -- the Boarding eraser (context key <c>boarding</c>). A boarding stay is a FINANCIAL record (D-9: the agreed quote, deposit and charges), so
/// for the financial retention period (<c>retention.financialYears</c>) the stay and its check-in evidence are HELD, not erased; once every stay of the customer is past the period there
/// is nothing left to hold. It mutates nothing (contract rule 3): Boarding keeps the customer by id only (names live on the Account) and the cat-level clinical rows follow the clinical
/// retention, not the customer's erasure. Stay media follow their own purge source (<c>boarding-media</c>).
/// </summary>
public sealed class BoardingPersonalDataEraser(CatEcommerceDbContext db, IBusinessSettings settings, IOperationsClock clock) : IPersonalDataEraser
{
    public string ContextKey => "boarding";

    public async Task<ErasureOutcome> EraseAsync(Guid customerAccountId, DateTimeOffset asOf, UserContext actor, CancellationToken ct)
    {
        var stays = await db.BoardingStays.AsNoTracking().Where(s => s.CustomerId == customerAccountId && s.Status != BoardingStayStatus.Enquiry)
            .Select(s => new { s.CheckedOutOn, s.CancelledOn, s.NoShowRecordedOn, s.DepartureOn }).ToListAsync(ct);
        if (stays.Count == 0) return new ErasureOutcome.Completed();

        var years = settings.GetInt(SettingKeys.RetentionFinancialYears);
        var today = await clock.LocalDateOfAsync(asOf, ct);
        DateOnly? earliest = null;

        foreach (var stay in stays)
        {
            var anchor = stay.CheckedOutOn ?? stay.CancelledOn ?? stay.NoShowRecordedOn;
            var anchorDate = anchor is { } instant ? await clock.LocalDateOfAsync(instant, ct) : stay.DepartureOn;
            var heldUntil = anchorDate.AddYears(years);
            if (heldUntil > today && (earliest is null || heldUntil < earliest)) earliest = heldUntil;
        }

        return earliest is { } until ? new ErasureOutcome.Held(until, "D-9 boarding stay financial record") : new ErasureOutcome.Completed();
    }
}
