using System.Text.RegularExpressions;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Surgery;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Surgery;

/// <summary>
/// HO-SRG-001 section 8.11 (US-SRG-11, ADR-171) -- a community-cat campaign price list. A code (unique), dates (branch-local, inclusive), the branches it covers (none listed =
/// all), one fixed price per STERILISATION service, and an optional cap on cases. Staff apply it at booking with a written eligibility note; the surgery snapshots the price. Once
/// any surgery is booked under a campaign its price list is LOCKED (<c>CampaignPriceLocked</c>) so the campaign report stays honest. A campaign is deactivated, never deleted.
/// </summary>
public sealed partial class SurgeryCampaignAggregate : AggregateRoot
{
    private readonly List<Guid> _branchIds = [];
    private readonly List<CampaignServicePrice> _prices = [];

    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public DateOnly ValidFrom { get; private set; }
    public DateOnly ValidTo { get; private set; }
    public IReadOnlyList<Guid> BranchIds => _branchIds;
    public IReadOnlyList<CampaignServicePrice> Prices => _prices;
    public int? MaxCases { get; private set; }
    public bool IsActive { get; private set; }

    private SurgeryCampaignAggregate() { }

    public SurgeryCampaignAggregate(Guid id) : base(id) { }

    public void Create(SurgeryCampaignDto dto)
    {
        Validate(dto);

        Code = NormaliseCode(dto.Code);
        Apply(dto);
        IsActive = true;

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new SurgeryCampaignChangedEvent { CampaignId = Id, IsActive = true, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    /// <summary>The code is the campaign's identity and never changes. The price list is locked once <paramref name="casesBooked"/> is above zero; the cap cannot drop below the cases already booked.</summary>
    public void Update(SurgeryCampaignDto dto, int casesBooked)
    {
        Validate(dto);

        if (NormaliseCode(dto.Code) != Code)
            throw Invalid("A campaign code cannot be changed.", nameof(dto.Code));
        if (casesBooked > 0 && PricesChanged(dto.Prices))
            throw new DomainRuleViolationException(BusinessViolationCodes.CampaignPriceLocked,
                $"{casesBooked} surgery booking(s) already use this campaign, so its prices can no longer change. Create a new campaign instead.", nameof(dto.Prices));
        if (dto.MaxCases is { } cap && cap < casesBooked)
            throw Invalid($"The cap cannot be lower than the {casesBooked} case(s) already booked.", nameof(dto.MaxCases));

        Apply(dto);

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new SurgeryCampaignChangedEvent { CampaignId = Id, IsActive = IsActive, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    /// <summary>Deactivate or reactivate. The same state is a no-op.</summary>
    public void SetActive(bool active, Platform.Infrastructure.Common.Security.UserContext userContext, Guid correlationId)
    {
        if (IsActive == active) return;

        IsActive = active;
        SetDefaultValue(userContext);
        AddDomainEvent(new SurgeryCampaignChangedEvent { CampaignId = Id, IsActive = active, CorrelationId = correlationId, UserContext = userContext });
        UpdateVersion();
    }

    /// <summary>
    /// Whether this campaign gives a price to a booking, and which. In order: active; the surgery's branch-local date inside the dates; the branch covered; a price for the service;
    /// the cap not reached (<paramref name="casesBooked"/> = non-cancelled surgeries already under the campaign); an eligibility note (1 to 500 characters). Each failure is its own
    /// reason, shown to reception.
    /// </summary>
    public CampaignApplication Evaluate(Guid branchId, Guid serviceId, DateOnly localDate, string? eligibilityNote, int casesBooked) =>
        Evaluate(IsActive, ValidFrom, ValidTo, _branchIds, _prices.FirstOrDefault(p => p.ServiceId == serviceId)?.Price, MaxCases, branchId, localDate, eligibilityNote, casesBooked);

    /// <summary>
    /// The rules of <see cref="Evaluate(Guid, Guid, DateOnly, string?, int)"/> over plain values, so the read side can apply the very same decision to projected columns without
    /// rehydrating the aggregate (ADR-012). <paramref name="priceForService"/> is the campaign's price for the booked service, or null when it has none.
    /// </summary>
    public static CampaignApplication Evaluate(
        bool isActive, DateOnly validFrom, DateOnly validTo, IReadOnlyCollection<Guid> campaignBranchIds, Money? priceForService, int? maxCases,
        Guid branchId, DateOnly localDate, string? eligibilityNote, int casesBooked)
    {
        if (!isActive)
            return Refuse("This campaign is not active.");
        if (localDate < validFrom || localDate > validTo)
            return Refuse($"This campaign runs from {validFrom:yyyy-MM-dd} to {validTo:yyyy-MM-dd}; the surgery is on {localDate:yyyy-MM-dd}.");
        if (campaignBranchIds.Count > 0 && !campaignBranchIds.Contains(branchId))
            return Refuse("This campaign does not cover this branch.");

        if (priceForService is null)
            return Refuse("This campaign has no price for this procedure.");
        if (maxCases is { } cap && casesBooked >= cap)
            return Refuse($"This campaign has reached its limit of {cap} case(s).");

        var note = eligibilityNote?.Trim();
        if (string.IsNullOrEmpty(note) || note.Length > SurgeryRules.EligibilityNoteMax)
            return Refuse($"Say who qualifies for this campaign (an eligibility note of up to {SurgeryRules.EligibilityNoteMax} characters).");

        return new CampaignApplication(priceForService, null);
    }

    private static CampaignApplication Refuse(string reason) => new(null, reason);

    private bool PricesChanged(IReadOnlyList<CampaignPriceInput> incoming)
    {
        if (incoming.Count != _prices.Count) return true;

        return incoming.Any(i => _prices.FirstOrDefault(p => p.ServiceId == i.ServiceId) is not { } existing || existing.Price != i.Price);
    }

    private void Apply(SurgeryCampaignDto dto)
    {
        Name = dto.Name.Trim();
        Description = dto.Description.Trim();
        ValidFrom = dto.ValidFrom;
        ValidTo = dto.ValidTo;
        MaxCases = dto.MaxCases;

        _branchIds.Clear();
        _branchIds.AddRange(dto.BranchIds.Distinct());

        // Rewriting an identical list would only churn rows; the list is replaced only when it changed (and then only while no case is booked).
        if (PricesChanged(dto.Prices))
        {
            _prices.Clear();
            foreach (var price in dto.Prices)
                _prices.Add(new CampaignServicePrice(Guid.NewGuid(), price.ServiceId, price.Price));
        }
    }

    private static void Validate(SurgeryCampaignDto dto)
    {
        var code = NormaliseCode(dto.Code);
        if (code.Length == 0 || code.Length > SurgeryRules.CampaignCodeMax || !CodePattern().IsMatch(code))
            throw Invalid($"A campaign code is 1 to {SurgeryRules.CampaignCodeMax} letters, digits, hyphens or underscores.", nameof(dto.Code));
        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Trim().Length > SurgeryRules.CampaignNameMax)
            throw Invalid($"A campaign name is required (up to {SurgeryRules.CampaignNameMax} characters).", nameof(dto.Name));
        if (dto.Description is null || dto.Description.Trim().Length > SurgeryRules.CampaignDescriptionMax)
            throw Invalid($"A campaign description is at most {SurgeryRules.CampaignDescriptionMax} characters.", nameof(dto.Description));
        if (dto.ValidTo < dto.ValidFrom)
            throw Invalid("A campaign cannot end before it starts.", nameof(dto.ValidTo));
        if (dto.MaxCases is < 1)
            throw Invalid("The cap on cases must be at least 1, or left empty for no cap.", nameof(dto.MaxCases));
        if (dto.Prices.Count == 0 || dto.Prices.Count > SurgeryRules.MaxCampaignPrices)
            throw Invalid($"A campaign prices between 1 and {SurgeryRules.MaxCampaignPrices} procedures.", nameof(dto.Prices));
        if (dto.Prices.Select(p => p.ServiceId).Distinct().Count() != dto.Prices.Count)
            throw Invalid("A procedure can be priced once in a campaign.", nameof(dto.Prices));
        if (dto.Prices.Select(p => p.Price.CurrencyCode).Distinct().Count() != 1)
            throw Invalid("Every price in a campaign is in one currency.", nameof(dto.Prices));
        if (dto.Prices.Any(p => p.Price.Amount < 0))
            throw Invalid("A campaign price cannot be negative.", nameof(dto.Prices));
        if (dto.Prices.Any(p => !p.IsSterilisation))
            throw new DomainRuleViolationException(BusinessViolationCodes.CampaignServiceNotSterilisation, "A campaign can price spay and neuter procedures only.", nameof(dto.Prices));
    }

    private static string NormaliseCode(string code) => (code ?? "").Trim().ToUpperInvariant();

    [GeneratedRegex("^[A-Z0-9_-]+$")]
    private static partial Regex CodePattern();

    private static DomainRuleViolationException Invalid(string message, string property) => new(BusinessViolationCodes.SurgeryCampaignInvalid, message, property);
}
