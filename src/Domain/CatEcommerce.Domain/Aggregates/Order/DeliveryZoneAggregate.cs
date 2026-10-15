using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Order;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Order;

/// <summary>
/// HO-ORD-001 §8.8 (ADR-206), US-ORD-06 -- a delivery zone: the districts (and optionally postal codes) a branch delivers to, the service
/// levels offered there with their charges, weight bands, free-over threshold, lead time and slot windows, and the cash-on-delivery switch and
/// ceiling (BR-15). Edited <b>in place</b> (audited); ADR-049's snapshot rule -- the order copies the zone, level, charge and slot it was priced
/// with -- is what protects history, not append-only zone rows. All amounts are <c>decimal(12,2)</c> in the one <see cref="CurrencyCode"/> of the
/// zone (<c>commerce.currencyCode</c>, ADR-054).
///
/// The aggregate checks everything it can see of itself (<c>InvalidDeliveryZone</c>): name, priority, at least one coverage row and one service
/// level, at most one row per service level, non-negative two-decimal amounts, ascending weight bands whose open-ended band is last, a lead time
/// of 0 to 14 days and well-formed slot windows. "The branch exists and is active" and "the name is unique" span other records and are the
/// handler's checks (the latter also a unique index).
/// </summary>
public sealed class DeliveryZoneAggregate : AggregateRoot
{
    private readonly List<ZoneCoverage> _coverage = [];
    private readonly List<ZoneServiceLevel> _services = [];

    public Guid BranchId { get; private set; }
    public string Name { get; private set; } = default!;
    public int Priority { get; private set; }
    public bool IsActive { get; private set; }
    public bool CodEnabled { get; private set; }
    public decimal? CodMaxOrderValue { get; private set; }
    public string CurrencyCode { get; private set; } = default!;

    public IReadOnlyList<ZoneCoverage> Coverage => _coverage;
    public IReadOnlyList<ZoneServiceLevel> Services => _services;

    private DeliveryZoneAggregate() { }

    public DeliveryZoneAggregate(Guid id) : base(id) { }

    public void Create(ZoneDto dto)
    {
        Apply(dto);
        SetDefaultValue(dto.UserContext);
        Raise(dto.UserContext, dto.CorrelationId);
    }

    public void Update(ZoneDto dto)
    {
        if (dto.BranchId != BranchId)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidDeliveryZone, "A delivery zone cannot move to another branch. Create a new zone instead.", nameof(BranchId));

        Apply(dto);
        Raise(dto.UserContext, dto.CorrelationId);
    }

    public void SetActivation(bool isActive, UserContext userContext, Guid correlationId)
    {
        IsActive = isActive;
        Raise(userContext, correlationId);
    }

    /// <summary>The pure resolver's view of this zone.</summary>
    public ResolvableZone ToResolvable() =>
        new(Id, Name, Priority, IsActive, _coverage.Select(c => new CoverageRule(c.District, c.PostalCodes)).ToList());

    public ZoneServiceLevel? ServiceFor(DeliveryServiceLevel level) => _services.FirstOrDefault(s => s.ServiceLevel == level);

    private void Apply(ZoneDto dto)
    {
        Validate(dto);

        BranchId = dto.BranchId;
        Name = dto.Name.Trim();
        Priority = dto.Priority;
        IsActive = dto.IsActive;
        CodEnabled = dto.CodEnabled;
        CodMaxOrderValue = dto.CodMaxOrderValue;
        CurrencyCode = dto.CurrencyCode.ToUpperInvariant();

        _coverage.Clear();
        _coverage.AddRange(dto.Coverage.Select(c => new ZoneCoverage(Guid.NewGuid(), c.District.Trim(), c.PostalCodes.Select(p => p.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList())));

        _services.Clear();
        _services.AddRange(dto.Services.Select(s => new ZoneServiceLevel(Guid.NewGuid(), s)));
    }

    private void Raise(UserContext userContext, Guid correlationId)
    {
        AddDomainEvent(new DeliveryZoneChangedEvent { DeliveryZoneId = Id, BranchId = BranchId, IsActive = IsActive, CorrelationId = correlationId, UserContext = userContext });
        UpdateVersion();
    }

    private static void Validate(ZoneDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Trim().Length > DeliveryZoneRules.MaxNameLength)
            throw Invalid($"A zone name is required (up to {DeliveryZoneRules.MaxNameLength} characters).", nameof(Name));

        if (dto.Priority < 0)
            throw Invalid("Priority cannot be negative. A lower number wins.", nameof(Priority));

        if (string.IsNullOrWhiteSpace(dto.CurrencyCode) || dto.CurrencyCode.Length != 3)
            throw Invalid("A currency code is required.", nameof(CurrencyCode));

        if (dto.CodMaxOrderValue is { } ceiling) RequireMoney(ceiling, "The cash-on-delivery ceiling", nameof(CodMaxOrderValue));

        if (dto.Coverage.Count == 0)
            throw Invalid("A zone must cover at least one district.", nameof(Coverage));

        if (dto.Coverage.Count > DeliveryZoneRules.MaxCoverageRows)
            throw Invalid($"A zone can cover at most {DeliveryZoneRules.MaxCoverageRows} districts.", nameof(Coverage));

        foreach (var coverage in dto.Coverage)
        {
            if (string.IsNullOrWhiteSpace(coverage.District) || coverage.District.Trim().Length > DeliveryZoneRules.MaxDistrictLength)
                throw Invalid($"Each covered district needs a name (up to {DeliveryZoneRules.MaxDistrictLength} characters).", nameof(Coverage));

            if (coverage.PostalCodes.Count > DeliveryZoneRules.MaxPostalCodesPerCoverage)
                throw Invalid($"A district can list at most {DeliveryZoneRules.MaxPostalCodesPerCoverage} postal codes.", nameof(Coverage));

            if (coverage.PostalCodes.Any(p => string.IsNullOrWhiteSpace(p) || p.Trim().Length > DeliveryZoneRules.MaxPostalCodeLength))
                throw Invalid($"A postal code needs 1 to {DeliveryZoneRules.MaxPostalCodeLength} characters.", nameof(Coverage));
        }

        if (dto.Services.Count == 0)
            throw Invalid("A zone must offer at least one service level.", nameof(Services));

        if (dto.Services.Count > DeliveryZoneRules.MaxServiceLevels || dto.Services.Select(s => s.ServiceLevel).Distinct().Count() != dto.Services.Count)
            throw Invalid("A zone offers each service level at most once.", nameof(Services));

        foreach (var service in dto.Services) ValidateService(service);
    }

    private static void ValidateService(ZoneServiceDto service)
    {
        RequireMoney(service.BaseCharge, "The base charge", nameof(Services));
        if (service.FreeOverOrderValue is { } freeOver) RequireMoney(freeOver, "The free-delivery threshold", nameof(Services));

        if (service.LeadTimeDays is < 0 or > DeliveryZoneRules.MaxLeadTimeDays)
            throw Invalid($"The lead time is 0 to {DeliveryZoneRules.MaxLeadTimeDays} days.", nameof(Services));

        ValidateBands(service.WeightBands);
        ValidateWindows(service.SlotWindows);
    }

    private static void ValidateBands(IReadOnlyList<WeightBand> bands)
    {
        if (bands.Count > DeliveryZoneRules.MaxWeightBands)
            throw Invalid($"A service level has at most {DeliveryZoneRules.MaxWeightBands} weight bands.", nameof(Services));

        for (var i = 0; i < bands.Count; i++)
        {
            RequireMoney(bands[i].Surcharge, "A weight-band surcharge", nameof(Services));

            if (bands[i].UpToGrams is null)
            {
                if (i != bands.Count - 1) throw Invalid("Only the last weight band can be open-ended.", nameof(Services));
                continue;
            }

            if (bands[i].UpToGrams <= 0) throw Invalid("A weight band's upper limit must be above zero grams.", nameof(Services));
            if (i > 0 && bands[i - 1].UpToGrams is { } previous && bands[i].UpToGrams <= previous)
                throw Invalid("Weight bands must be listed in ascending order of weight.", nameof(Services));
        }
    }

    private static void ValidateWindows(IReadOnlyList<SlotWindow> windows)
    {
        if (windows.Count > DeliveryZoneRules.MaxSlotWindows)
            throw Invalid($"A service level has at most {DeliveryZoneRules.MaxSlotWindows} slot windows.", nameof(Services));

        foreach (var window in windows)
        {
            if (!Enum.IsDefined(window.DayOfWeek) || window.Start >= window.End)
                throw Invalid("Each slot window needs a valid day and a start before its end.", nameof(Services));
        }

        foreach (var day in windows.GroupBy(w => w.DayOfWeek))
        {
            var ordered = day.OrderBy(w => w.Start).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                if (ordered[i].Start < ordered[i - 1].End)
                    throw Invalid("Slot windows on the same day cannot overlap.", nameof(Services));
            }
        }
    }

    private static void RequireMoney(decimal amount, string what, string property)
    {
        if (amount < 0 || decimal.Round(amount, 2) != amount || amount > 9_999_999_999.99m)
            throw Invalid($"{what} must be zero or more, with at most two decimal places.", property);
    }

    private static DomainRuleViolationException Invalid(string message, string property) =>
        new(BusinessViolationCodes.InvalidDeliveryZone, message, property);
}
