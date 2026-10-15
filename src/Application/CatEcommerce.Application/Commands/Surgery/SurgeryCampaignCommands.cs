using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Surgery;

/// <summary>One price of a campaign: a sterilisation service and its fixed amount (the currency is the shop currency, <c>commerce.currencyCode</c>).</summary>
public sealed record SurgeryCampaignPriceInput(Guid ServiceId, decimal Amount);

/// <summary>HO-SRG-001 section 11.1 C16 (US-SRG-11) -- create a community-cat campaign price list.</summary>
public sealed class CreateSurgeryCampaignCommand : Command
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    public required DateOnly ValidFrom { get; init; }
    public required DateOnly ValidTo { get; init; }
    public IReadOnlyList<Guid> BranchIds { get; init; } = [];
    public required IReadOnlyList<SurgeryCampaignPriceInput> Prices { get; init; }
    public int? MaxCases { get; init; }
}

/// <summary>C16 -- change a campaign. The code never changes; the prices are locked once a surgery is booked under it.</summary>
public sealed class UpdateSurgeryCampaignCommand : Command
{
    public required Guid CampaignId { get; init; }
    public required int ExpectedVersion { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    public required DateOnly ValidFrom { get; init; }
    public required DateOnly ValidTo { get; init; }
    public IReadOnlyList<Guid> BranchIds { get; init; } = [];
    public required IReadOnlyList<SurgeryCampaignPriceInput> Prices { get; init; }
    public int? MaxCases { get; init; }
}

/// <summary>C16 -- deactivate or reactivate. A campaign is never deleted.</summary>
public sealed class SetSurgeryCampaignActiveCommand : Command
{
    public required Guid CampaignId { get; init; }
    public required bool IsActive { get; init; }
}
