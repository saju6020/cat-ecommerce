using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Boarding;

/// <summary>
/// HO-BRD-001 section 8.15 / CONTRACTS #12 -- Boarding's read-only view of the charges it pushed to Billing. A charge is BILLED when a non-discarded invoice carries a line for it;
/// the net of a charge is <c>quantity x unit price - line discount</c>.
/// </summary>
public sealed class BoardingChargeLedger(CatEcommerceDbContext db) : IBoardingChargeLedger
{
    public async Task<IReadOnlyList<BoardingChargeRow>> GetRowsAsync(Guid stayId, CancellationToken ct = default)
    {
        var charges = await db.Charges.AsNoTracking()
            .Where(c => c.SourceKind == ChargeSourceKind.BoardingStay && c.SourceId == stayId)
            .Select(c => new { c.Id, c.SourceLineRef, c.Status, c.Quantity, c.UnitPriceAmount, c.LineDiscountAmount })
            .ToListAsync(ct);
        if (charges.Count == 0) return [];

        var chargeIds = charges.Select(c => c.Id).ToList();
        var billed = await BilledIdsAsync(chargeIds, ct);
        var credited = await CreditedNetAsync(chargeIds, ct);
        return charges.Select(c => new BoardingChargeRow(
            c.SourceLineRef, c.Id, c.Status == ChargeStatus.Voided, billed.Contains(c.Id), c.Quantity * c.UnitPriceAmount - c.LineDiscountAmount, credited.GetValueOrDefault(c.Id))).ToList();
    }

    public async Task<IReadOnlyList<StayDueForInvoicing>> GetStaysDueForInvoicingAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        // Checked in: a cycle invoice when a boundary has been reached. Checked out within the last 90 days: the FINAL invoice (cycle 0) for whatever is still uninvoiced.
        var recently = now.AddDays(-90);
        var stays = await db.BoardingStays.AsNoTracking()
            .Where(s => s.Status == BoardingStayStatus.CheckedIn || (s.Status == BoardingStayStatus.CheckedOut && s.CheckedOutOn != null && s.CheckedOutOn >= recently))
            .Select(s => new { s.Id, s.BranchId, s.CustomerId, s.ArrivalOn, s.DepartureOn, s.Status }).ToListAsync(ct);
        if (stays.Count == 0) return [];

        var zones = await db.Branches.AsNoTracking().Select(b => new { b.Id, b.TimeZoneId }).ToDictionaryAsync(b => b.Id, b => b.TimeZoneId, ct);
        var stayIds = stays.Select(s => s.Id).ToList();
        var charges = await db.Charges.AsNoTracking()
            .Where(c => c.SourceKind == ChargeSourceKind.BoardingStay && stayIds.Contains(c.SourceId) && c.Status == ChargeStatus.Active)
            .Select(c => new { c.Id, c.SourceId, c.ServiceDate }).ToListAsync(ct);
        var billed = await BilledIdsAsync(charges.Select(c => c.Id).ToList(), ct);

        var due = new List<StayDueForInvoicing>();
        foreach (var stay in stays)
        {
            if (stay.Status == BoardingStayStatus.CheckedOut)
            {
                if (charges.Any(c => c.SourceId == stay.Id && !billed.Contains(c.Id)))
                    due.Add(new StayDueForInvoicing(stay.Id, stay.BranchId, stay.CustomerId, 0, stay.DepartureOn));
                continue;
            }

            var today = Domain.DomainServices.BoardingOccupancyWindow.LocalDateOf(now, zones.GetValueOrDefault(stay.BranchId, "UTC"));
            var cycle = (today.DayNumber - stay.ArrivalOn.DayNumber) / StayRules.NightsPerCycle;
            if (cycle < 1) continue;

            var boundary = stay.ArrivalOn.AddDays(StayRules.NightsPerCycle * cycle - 1);
            if (charges.Any(c => c.SourceId == stay.Id && c.ServiceDate <= boundary && !billed.Contains(c.Id)))
                due.Add(new StayDueForInvoicing(stay.Id, stay.BranchId, stay.CustomerId, cycle, boundary));
        }

        return due;
    }

    /// <summary>The net credited per charge: Billing credit-note lines point at invoice lines, invoice lines at charges. Projected, untracked.</summary>
    private async Task<Dictionary<Guid, decimal>> CreditedNetAsync(IReadOnlyCollection<Guid> chargeIds, CancellationToken ct)
    {
        if (chargeIds.Count == 0) return [];

        var lines = await db.Invoices.AsNoTracking()
            .Where(i => i.Status != InvoiceStatus.Discarded)
            .SelectMany(i => i.Lines)
            .Where(l => l.ChargeId != null && chargeIds.Contains(l.ChargeId.Value))
            .Select(l => new { LineId = l.Id, ChargeId = l.ChargeId!.Value })
            .ToListAsync(ct);
        if (lines.Count == 0) return [];

        var lineIds = lines.Select(l => l.LineId).ToList();
        var credits = await db.CreditNotes.AsNoTracking()
            .SelectMany(n => n.Lines)
            .Where(l => lineIds.Contains(l.InvoiceLineId))
            .Select(l => new { l.InvoiceLineId, l.NetAmount })
            .ToListAsync(ct);

        var byLine = credits.GroupBy(c => c.InvoiceLineId).ToDictionary(g => g.Key, g => g.Sum(c => c.NetAmount));
        return lines.GroupBy(l => l.ChargeId).ToDictionary(g => g.Key, g => g.Sum(l => byLine.GetValueOrDefault(l.LineId)));
    }

    private async Task<HashSet<Guid>> BilledIdsAsync(IReadOnlyCollection<Guid> chargeIds, CancellationToken ct)
    {
        if (chargeIds.Count == 0) return [];

        var ids = await db.Invoices.AsNoTracking()
            .Where(i => i.Status != InvoiceStatus.Discarded)
            .SelectMany(i => i.Lines)
            .Where(l => l.ChargeId != null && chargeIds.Contains(l.ChargeId.Value))
            .Select(l => l.ChargeId!.Value)
            .ToListAsync(ct);
        return ids.ToHashSet();
    }
}

/// <summary>
/// HO-BRD-001 section 8.14 / CONTRACTS #13 -- replaces the fail-closed fake: the cat's vet spending headroom for HO-EMR-001 and HO-SRG-001. A stay or a cat that cannot be found is
/// fail-closed (not authorised, zero headroom) so a consumer falls back to the approval workflow.
/// </summary>
public sealed class BoardingVetSpendingPolicy(CatEcommerceDbContext db, IBusinessSettings businessSettings) : IBoardingVetSpendingPolicy
{
    public async Task<VetSpendingHeadroomResult> GetHeadroomAsync(Guid stayId, Guid catId, CancellationToken ct)
    {
        // Projected columns, then the pure rule the aggregate also calls (no aggregate is rehydrated).
        var stay = await db.BoardingStays.AsNoTracking().Where(s => s.Id == stayId && s.Cats.Any(c => c.CatId == catId))
            .Select(s => new
            {
                s.CurrencyCode,
                Cat = s.Cats.Where(c => c.CatId == catId).Select(c => new { c.VetVisitAuthorised, c.VetSpendingLimit }).First(),
                Approved = s.Approvals.Where(a => a.CatId == catId && a.Status == VetApprovalStatus.Approved).Select(a => a.Amount).ToList(),
                Committed = s.Commitments.Where(c => c.CatId == catId).Select(c => c.Amount).ToList(),
            })
            .FirstOrDefaultAsync(ct);
        if (stay is null)
        {
            var currency = businessSettings.GetString(SettingKeys.CommerceCurrencyCode);
            return new VetSpendingHeadroomResult(false, Money.Zero(currency), Money.Zero(currency), Money.Zero(currency));
        }

        return VetSpendingHeadroom.Compute(stay.Cat.VetVisitAuthorised, stay.Cat.VetSpendingLimit, stay.Approved, stay.Committed, stay.CurrencyCode);
    }
}

/// <summary>
/// HO-BRD-001 section 8.15 / CONTRACTS #12 -- replaces the empty fake: the uninvoiced, active boarding charges of a stay in the shape the contract names. Merged Billing PULLS nothing from
/// here (mismatch 2: it selects the charges itself); this stays for any consumer that asks the original question.
/// </summary>
public sealed class BoardingBillableSource(CatEcommerceDbContext db) : IBoardingBillableSource
{
    public async Task<IReadOnlyList<BoardingUninvoicedLineDto>> GetUninvoicedLinesAsync(Guid stayId, int? upToNight, CancellationToken ct)
    {
        var arrival = await db.BoardingStays.AsNoTracking().Where(s => s.Id == stayId).Select(s => (DateOnly?)s.ArrivalOn).FirstOrDefaultAsync(ct);
        if (arrival is null) return [];

        var charges = await db.Charges.AsNoTracking()
            .Where(c => c.SourceKind == ChargeSourceKind.BoardingStay && c.SourceId == stayId && c.Status == ChargeStatus.Active)
            .OrderBy(c => c.ServiceDate).ThenBy(c => c.SourceLineRef)
            .Select(c => new { c.Id, c.Description, c.Quantity, c.UnitPriceAmount, c.LineDiscountAmount, c.CurrencyCode, c.TaxClassId, c.SourceLineRef, c.ServiceDate })
            .ToListAsync(ct);
        if (upToNight is { } night) charges = charges.Where(c => c.ServiceDate <= arrival.Value.AddDays(night - 1)).ToList();

        var chargeIds = charges.Select(c => c.Id).ToList();
        var billed = await db.Invoices.AsNoTracking().Where(i => i.Status != InvoiceStatus.Discarded).SelectMany(i => i.Lines)
            .Where(l => l.ChargeId != null && chargeIds.Contains(l.ChargeId.Value)).Select(l => l.ChargeId!.Value).ToListAsync(ct);

        return charges.Where(c => !billed.Contains(c.Id))
            .Select(c => new BoardingUninvoicedLineDto(
                c.Id, c.Description, new Money(c.Quantity * c.UnitPriceAmount - c.LineDiscountAmount, c.CurrencyCode), c.TaxClassId,
                c.SourceLineRef.Contains("AdditionalCat") ? BoardingStayChargeLineKind.AdditionalCat : BoardingStayChargeLineKind.RateUnit))
            .ToList();
    }
}
