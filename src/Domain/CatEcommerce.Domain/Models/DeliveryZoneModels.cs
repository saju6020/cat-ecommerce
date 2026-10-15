using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>A weight band of a service level: the surcharge for a parcel up to <paramref name="UpToGrams"/>. The last band is open-ended
/// (<c>null</c>) and applies to every heavier parcel (HO-ORD-001 §8.8).</summary>
public sealed record WeightBand(int? UpToGrams, decimal Surcharge);

/// <summary>A delivery slot window in branch-local time (HO-ORD-001 §8.8). Slots are capacity-free preference windows (OQ-13).</summary>
public sealed record SlotWindow(DayOfWeek DayOfWeek, TimeOnly Start, TimeOnly End)
{
    /// <summary>"10:00–13:00".</summary>
    public string Label => $"{Start:HH\\:mm}–{End:HH\\:mm}";
}

/// <summary>One district a zone serves; <see cref="PostalCodes"/> empty means the whole district.</summary>
public sealed record CoverageDto(string District, IReadOnlyList<string> PostalCodes);

/// <summary>One service level of a zone.</summary>
public sealed record ZoneServiceDto(
    DeliveryServiceLevel ServiceLevel,
    bool IsActive,
    decimal BaseCharge,
    decimal? FreeOverOrderValue,
    int LeadTimeDays,
    IReadOnlyList<WeightBand> WeightBands,
    IReadOnlyList<SlotWindow> SlotWindows);

/// <summary>HO-ORD-001 §11.3 -- the whole definition of a delivery zone as the create and update commands carry it.
/// <paramref name="CurrencyCode"/> is <c>commerce.currencyCode</c>, resolved by the handler.</summary>
public sealed record ZoneDto(
    Guid BranchId,
    string Name,
    int Priority,
    bool IsActive,
    bool CodEnabled,
    decimal? CodMaxOrderValue,
    string CurrencyCode,
    IReadOnlyList<CoverageDto> Coverage,
    IReadOnlyList<ZoneServiceDto> Services,
    UserContext UserContext,
    Guid CorrelationId);

/// <summary>What the resolver needs of one zone's coverage row (pure input).</summary>
public sealed record CoverageRule(string District, IReadOnlyList<string> PostalCodes);

/// <summary>What the pure resolver, charge and slot calculators need of a zone (a snapshot of the zone's rules, loaded by projection).</summary>
public sealed record ResolvableZone(Guid ZoneId, string Name, int Priority, bool IsActive, IReadOnlyList<CoverageRule> Coverage);

/// <summary>The charge rules of one service level (pure input to <c>DeliveryChargeCalculator</c>).</summary>
public sealed record ZoneChargeRules(decimal BaseCharge, decimal? FreeOverOrderValue, IReadOnlyList<WeightBand> WeightBands);

/// <summary>A slot a customer may choose: a date and one window.</summary>
public sealed record DeliverySlot(DateOnly Date, TimeOnly WindowStart, TimeOnly WindowEnd, string Label);
