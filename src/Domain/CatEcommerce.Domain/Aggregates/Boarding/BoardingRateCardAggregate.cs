using CatEcommerce.Domain.Events.Boarding;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Boarding;

/// <summary>
/// HO-BRD-001 section 8.2 (US-BRD-03, ADR-221 as renumbered ADR-354). One set of cards PER BRANCH: section 16.1 gives every test branch its own rate cards, and a
/// card names a tax class and a currency a branch owns, so the handover unique index on (unit type, effective-from) is read as per branch (section 1 Amended). Append-only and effective-dated (HO-ADM-001 ADR-049): created once, never mutated;
/// a correction is a later card. The card in force at instant <c>t</c> is the one with the greatest <see cref="EffectiveFrom"/> not after <c>t</c>
/// (<see cref="InForceAt"/>). A unit type with no card in force is not bookable. "No back-dating" is the aggregate's, given "now" by the handler.
/// </summary>
public sealed class BoardingRateCardAggregate : AggregateRoot
{
    public Guid BranchId { get; private set; }
    public BoardingUnitType UnitType { get; private set; } = default!;
    public decimal NightlyRate { get; private set; }
    public decimal WeeklyRate { get; private set; }
    public decimal MonthlyRate { get; private set; }
    public decimal AdditionalCatNightlyRate { get; private set; }
    public Guid TaxClassId { get; private set; }
    public string CurrencyCode { get; private set; } = default!;
    public DateTimeOffset EffectiveFrom { get; private set; }
    public Guid SetBy { get; private set; }

    private BoardingRateCardAggregate() { }

    public BoardingRateCardAggregate(Guid id) : base(id) { }

    public void Create(BoardingRateCardCreateDto dto)
    {
        if (dto.EffectiveFrom < dto.Now)
            throw new DomainRuleViolationException(BusinessViolationCodes.RateCardCannotBeBackdated, "A rate card cannot be effective in the past.", nameof(dto.EffectiveFrom));

        foreach (var (name, amount) in new[]
                 {
                     (nameof(dto.NightlyRate), dto.NightlyRate), (nameof(dto.WeeklyRate), dto.WeeklyRate),
                     (nameof(dto.MonthlyRate), dto.MonthlyRate), (nameof(dto.AdditionalCatNightlyRate), dto.AdditionalCatNightlyRate),
                 })
        {
            if (amount < 0m || amount > BillingRules.MaxAmount || decimal.Round(amount, 2) != amount)
                throw new DomainRuleViolationException(BusinessViolationCodes.InvalidRateCard, "Rates cannot be negative and have at most two decimals.", name);
        }

        if (dto.TaxClassId == Guid.Empty || string.IsNullOrWhiteSpace(dto.CurrencyCode) || dto.CurrencyCode.Length != 3)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidRateCard, "A tax class and a currency are required.", nameof(dto.TaxClassId));

        BranchId = dto.BranchId;
        UnitType = dto.UnitType;
        NightlyRate = dto.NightlyRate;
        WeeklyRate = dto.WeeklyRate;
        MonthlyRate = dto.MonthlyRate;
        AdditionalCatNightlyRate = dto.AdditionalCatNightlyRate;
        TaxClassId = dto.TaxClassId;
        CurrencyCode = dto.CurrencyCode.ToUpperInvariant();
        EffectiveFrom = dto.EffectiveFrom;
        SetBy = dto.SetBy;

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new BoardingRateCardSetEvent { RateCardId = Id, UnitType = UnitType, EffectiveFrom = EffectiveFrom, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    public RateCardSnapshot ToSnapshot() => new()
    {
        CardId = Id, UnitType = UnitType, NightlyRate = NightlyRate, WeeklyRate = WeeklyRate, MonthlyRate = MonthlyRate,
        AdditionalCatNightlyRate = AdditionalCatNightlyRate, TaxClassId = TaxClassId, CurrencyCode = CurrencyCode,
    };

    /// <summary>The card in force for <paramref name="unitType"/> at <paramref name="instant"/>, or <c>null</c> when none is.</summary>
    public static BoardingRateCardAggregate? InForceAt(IEnumerable<BoardingRateCardAggregate> cards, BoardingUnitType unitType, DateTimeOffset instant) =>
        cards.Where(c => c.UnitType == unitType && c.EffectiveFrom <= instant).OrderByDescending(c => c.EffectiveFrom).FirstOrDefault();
}
