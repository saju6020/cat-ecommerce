using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Entities;

/// <summary>HO-ORD-001 §8.8 -- one service level (standard or express) of a delivery zone: charge, free-over threshold, weight bands, lead time and
/// slot windows. Owned by <c>DeliveryZoneAggregate</c>; weight bands and slot windows are small value lists kept as <c>jsonb</c>.</summary>
public sealed class ZoneServiceLevel
{
    public Guid Id { get; private set; }
    public DeliveryServiceLevel ServiceLevel { get; private set; } = default!;
    public bool IsActive { get; private set; }
    public decimal BaseCharge { get; private set; }
    public decimal? FreeOverOrderValue { get; private set; }
    public int LeadTimeDays { get; private set; }
    public IReadOnlyList<WeightBand> WeightBands { get; private set; } = [];
    public IReadOnlyList<SlotWindow> SlotWindows { get; private set; } = [];

    private ZoneServiceLevel() { }

    public ZoneServiceLevel(Guid id, ZoneServiceDto dto)
    {
        Id = id;
        ServiceLevel = dto.ServiceLevel;
        IsActive = dto.IsActive;
        BaseCharge = dto.BaseCharge;
        FreeOverOrderValue = dto.FreeOverOrderValue;
        LeadTimeDays = dto.LeadTimeDays;
        WeightBands = dto.WeightBands;
        SlotWindows = dto.SlotWindows;
    }

    public ZoneChargeRules ToChargeRules() => new(BaseCharge, FreeOverOrderValue, WeightBands);
}
