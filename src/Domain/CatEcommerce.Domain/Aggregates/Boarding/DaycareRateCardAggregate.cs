using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Boarding;

/// <summary>HO-BRD-001 section 8.18 -- the daycare price list of a branch: an hourly rate and a day rate, effective-dated and append-only like the boarding rate card. A branch with no card in force cannot book daycare.</summary>
public sealed class DaycareRateCardAggregate : AggregateRoot
{
    public Guid BranchId { get; private set; }
    public decimal HourlyRate { get; private set; }
    public decimal DayRate { get; private set; }
    public Guid TaxClassId { get; private set; }
    public string CurrencyCode { get; private set; } = default!;
    public DateTimeOffset EffectiveFrom { get; private set; }
    public Guid SetBy { get; private set; }

    private DaycareRateCardAggregate() { }

    public DaycareRateCardAggregate(Guid id) : base(id) { }

    public void Create(Guid branchId, decimal hourlyRate, decimal dayRate, Guid taxClassId, string currencyCode, DateTimeOffset effectiveFrom, DateTimeOffset now, UserContext userContext)
    {
        if (effectiveFrom < now)
            throw new DomainRuleViolationException(BusinessViolationCodes.RateCardCannotBeBackdated, "A rate card cannot be effective in the past.", nameof(effectiveFrom));

        foreach (var (name, amount) in new[] { (nameof(hourlyRate), hourlyRate), (nameof(dayRate), dayRate) })
        {
            if (amount < 0m || amount > BillingRules.MaxAmount || decimal.Round(amount, 2) != amount)
                throw new DomainRuleViolationException(BusinessViolationCodes.InvalidRateCard, "Rates cannot be negative and have at most two decimals.", name);
        }

        if (taxClassId == Guid.Empty || string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Length != 3)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidRateCard, "A tax class and a currency are required.", nameof(taxClassId));

        BranchId = branchId;
        HourlyRate = hourlyRate;
        DayRate = dayRate;
        TaxClassId = taxClassId;
        CurrencyCode = currencyCode.ToUpperInvariant();
        EffectiveFrom = effectiveFrom;
        SetBy = userContext.UserId;

        SetDefaultValue(userContext);
        UpdateVersion();
    }

    public static DaycareRateCardAggregate? InForceAt(IEnumerable<DaycareRateCardAggregate> cards, DateTimeOffset instant) =>
        cards.Where(c => c.EffectiveFrom <= instant).OrderByDescending(c => c.EffectiveFrom).FirstOrDefault();
}
