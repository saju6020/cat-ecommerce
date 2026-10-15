using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Boarding;

/// <summary>
/// HO-BRD-001 section 8.20/12.2 (ADR-220, renumbered ADR-353): the ONE loader behind the availability query and every unit-taking command. Read-only
/// (<c>AsNoTracking</c>), projected, async; the availability itself is computed by the pure <see cref="BoardingAvailabilityFinder"/> -- never in SQL.
/// </summary>
public sealed class BoardingInputs(CatEcommerceDbContext db, IBusinessSettings settings) : IBoardingInputs
{
    public async Task<BoardingBranchSettings?> GetSettingsAsync(Guid branchId, CancellationToken ct = default)
    {
        var zone = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == branchId).Select(b => b.TimeZoneId).FirstOrDefaultAsync(ct);
        if (zone is null) return null;

        return new BoardingBranchSettings(
            zone,
            settings.GetTimeRange(SettingKeys.BoardingCheckInWindow, branchId),
            settings.GetTimeRange(SettingKeys.BoardingCheckOutWindow, branchId),
            settings.GetInt(SettingKeys.BoardingTurnaroundHours, branchId),
            settings.GetInt(SettingKeys.BoardingMaxStayNights, branchId),
            settings.GetInt(SettingKeys.BoardingBookingHorizonDays, branchId),
            settings.GetInt(SettingKeys.BoardingAlternativeSearchDays, branchId),
            settings.GetInt(SettingKeys.BoardingReservationHoldHours, branchId),
            settings.GetDecimal(SettingKeys.BoardingDepositPercent, branchId),
            settings.GetDecimal(SettingKeys.BoardingDepositFixedAmount, branchId),
            new PricingSnapshot
            {
                LongStayThresholdNights = settings.GetInt(SettingKeys.BoardingLongStayThresholdNights, branchId),
                LongStayDiscountPercent = settings.GetDecimal(SettingKeys.BoardingLongStayDiscountPercent, branchId),
                EarlyCheckoutChargedNights = settings.GetInt(SettingKeys.BoardingEarlyCheckoutChargedNights, branchId),
                EarlyCheckoutRefundAsCredit = settings.GetBool(SettingKeys.BoardingEarlyCheckoutRefundAsCredit, branchId),
            },
            settings.GetString(SettingKeys.CommerceCurrencyCode),
            settings.GetInt(SettingKeys.ClinicalVaccinationDueSoonDays),
            settings.GetInt(SettingKeys.BoardingCancellationWindowHours, branchId),
            settings.GetInt(SettingKeys.BoardingNoShowGraceHours, branchId),
            settings.GetBool(SettingKeys.BoardingRequireBelongingsPhotos, branchId),
            settings.GetInt(SettingKeys.BoardingTaskOverdueGraceMinutes, branchId));
    }

    public async Task<BoardingInputSet?> LoadAsync(Guid branchId, DateOnly arrivalOn, DateOnly departureOn, Guid? excludeStayId, DateTimeOffset now, CancellationToken ct = default)
    {
        var branchSettings = await GetSettingsAsync(branchId, ct);
        if (branchSettings is null) return null;

        // Widen the busy span by the alternative-search radius (plus the turnaround) so the finder can test the shifted ranges from the same load.
        var radius = branchSettings.AlternativeSearchDays + 1;
        var span = BoardingOccupancyWindow.Compute(
            arrivalOn.AddDays(-radius), departureOn.AddDays(radius), branchSettings.TimeZoneId, branchSettings.CheckInWindow, branchSettings.CheckOutWindow, branchSettings.TurnaroundHours);

        var units = await db.BoardingUnits.AsNoTracking()
            .Where(u => u.BranchId == branchId)
            .Select(u => new AvailabilityUnit(u.Id, u.Code, u.Type, u.CapacityCats, u.Block, u.SortOrder, u.IsActive))
            .ToListAsync(ct);

        var allocations = await db.BoardingStays.AsNoTracking()
            .Where(s => s.BranchId == branchId && (excludeStayId == null || s.Id != excludeStayId))
            .SelectMany(s => s.Allocations)
            .Where(a => a.IsActive && a.OccupiedTo > span.From && a.OccupiedFrom < span.To)
            .Select(a => new { a.UnitId, a.OccupiedFrom, a.OccupiedTo })
            .ToListAsync(ct);

        var blocks = await db.BoardingMaintenanceBlocks.AsNoTracking()
            .Where(b => b.BranchId == branchId && !b.IsCancelled && b.OccupiedTo > span.From && b.OccupiedFrom < span.To)
            .Select(b => new { b.UnitId, b.OccupiedFrom, b.OccupiedTo })
            .ToListAsync(ct);

        var busy = allocations.Concat(blocks)
            .GroupBy(r => r.UnitId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<OccupancyRange>)g.Select(r => new OccupancyRange(r.OccupiedFrom, r.OccupiedTo)).ToList());

        var cards = await db.BoardingRateCards.AsNoTracking().Where(c => c.BranchId == branchId && c.EffectiveFrom <= now)
            .Select(c => new { c.Id, c.UnitType, c.NightlyRate, c.WeeklyRate, c.MonthlyRate, c.AdditionalCatNightlyRate, c.TaxClassId, c.CurrencyCode, c.EffectiveFrom }).ToListAsync(ct);
        var inForce = cards
            .GroupBy(c => c.UnitType.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.EffectiveFrom).First())
            .ToDictionary(
                c => c.Key,
                c => new RateCardSnapshot
                {
                    CardId = c.Value.Id, UnitType = c.Value.UnitType, NightlyRate = c.Value.NightlyRate, WeeklyRate = c.Value.WeeklyRate, MonthlyRate = c.Value.MonthlyRate,
                    AdditionalCatNightlyRate = c.Value.AdditionalCatNightlyRate, TaxClassId = c.Value.TaxClassId, CurrencyCode = c.Value.CurrencyCode,
                });

        return new BoardingInputSet(branchSettings, units, busy, inForce);
    }

    public async Task<IReadOnlyList<CatOverlap>> FindCatOverlapsAsync(IReadOnlyCollection<Guid> catIds, OccupancyRange range, Guid? excludeStayId, CancellationToken ct = default)
    {
        var ids = catIds.ToList();
        var rows = await db.BoardingStays.AsNoTracking()
            .Where(s => excludeStayId == null || s.Id != excludeStayId)
            .SelectMany(s => s.Cats, (s, c) => new { s.ArrivalOn, s.DepartureOn, c.CatId, c.IsActive, c.OccupiedFrom, c.OccupiedTo })
            .Where(r => r.IsActive && ids.Contains(r.CatId) && r.OccupiedFrom < range.To && range.From < r.OccupiedTo)
            .ToListAsync(ct);

        return rows.Select(r => new CatOverlap(r.CatId, r.ArrivalOn, r.DepartureOn)).ToList();
    }
}
